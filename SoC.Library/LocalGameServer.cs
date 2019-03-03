﻿

namespace Jabberwocky.SoC.Library
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Jabberwocky.SoC.Library.GameActions;
    using Jabberwocky.SoC.Library.GameBoards;
    using Jabberwocky.SoC.Library.GameEvents;
    using Jabberwocky.SoC.Library.Interfaces;

    public class LocalGameServer
    {
        private readonly ConcurrentQueue<ComputerPlayerAction> actionRequests = new ConcurrentQueue<ComputerPlayerAction>();
        private IPlayer currentPlayer;
        private TurnToken currentTurnToken;
        private readonly IDevelopmentCardHolder developmentCardHolder;
        private readonly EventRaiser eventRaiser = new EventRaiser();
        private bool isQuitting;
        private readonly GameBoard gameBoard;
        private readonly INumberGenerator numberGenerator;
        private Dictionary<Guid, IPlayer> playersById;
        private int playerIndex;
        private IPlayer[] players;
        private uint dice1, dice2;
        private IGameTimer turnTimer;
        private Func<Guid> idGenerator;

        public LocalGameServer(INumberGenerator numberGenerator, GameBoard gameBoard, IDevelopmentCardHolder developmentCardHolder)
        {
            this.numberGenerator = numberGenerator;
            this.gameBoard = gameBoard;
            this.developmentCardHolder = developmentCardHolder;
            this.turnTimer = new GameServerTimer();
            this.idGenerator = () => { return Guid.NewGuid(); };
        }

        public bool IsFinished { get; set; }

        public void SetTurnTimer(IGameTimer turnTimer)
        {
            if (turnTimer != null)
                this.turnTimer = turnTimer;
        }

        public void SetIdGenerator(Func<Guid> idGenerator)
        {
            if (idGenerator != null)
                this.idGenerator = idGenerator;
        }

        private event Action<Exception> GameExceptionEvent;

        public void JoinGame(string playerName, GameController gameController)
        {
            this.eventRaiser.AddEventHandler(playerName, gameController.GameEventHandler);
            this.GameExceptionEvent += gameController.GameExceptionHandler;
            gameController.PlayerActionEvent += this.PlayerActionEventHandler;
            this.players[this.playerIndex++] = new Player(playerName, this.idGenerator.Invoke());
        }

        public void LaunchGame(GameOptions gameOptions = null)
        {
            if (gameOptions == null)
                gameOptions = new GameOptions();

            this.playerIndex = 0;
            this.players = new IPlayer[gameOptions.MaxPlayers + gameOptions.MaxAIPlayers];
        }

        public void StartGameAsync()
        {
            // Launch server processing on separate thread
            Task.Factory.StartNew(() =>
            {
                Thread.CurrentThread.Name = "Local Game Server";
                try
                {
                    this.players = PlayerTurnOrderCreator.Create(this.players, this.numberGenerator);
                    // Notify (human?) players what the order is?

                    var playerIdsByName = this.players.ToDictionary(p => p.Name, p => p.Id);
                    this.eventRaiser.RaiseEvent(new PlayerSetupEvent(playerIdsByName));

                    var gameBoardSetup = new GameBoardSetup(this.gameBoard);
                    this.eventRaiser.RaiseEvent(new InitialBoardSetupEventArgs(gameBoardSetup));

                    // TODO: Send event with player details to everyone

                    this.GameSetup();
                    this.MainGameLoop();
                }
                catch (Exception e)
                {
                    this.GameExceptionEvent?.Invoke(e);
                }
            });
        }

        private void ChangeToNextPlayerTurn()
        {
            this.playerIndex++;
            if (this.playerIndex == this.players.Length)
            {
                this.playerIndex = 0;
            }

            this.currentPlayer = this.players[this.playerIndex];
        }

        private void CollectResourcesAtStartOfTurn(uint resourceRoll)
        {
            var resources = this.gameBoard.GetResourcesForRoll(resourceRoll);
            foreach (var player in this.players)
            {
                if (!resources.TryGetValue(player.Id, out var resourcesCollectionForPlayer))
                    continue;

                var resourcesCollectionOrderedByLocation = resourcesCollectionForPlayer
                    .OrderBy(rc => rc.Location).ToArray();

                foreach (var resourceCollection in resourcesCollectionForPlayer)
                    player.AddResources(resourceCollection.Resources);

                var resourcesCollectedEvent = new ResourcesCollectedEvent(player.Id, resourcesCollectionOrderedByLocation);
                this.eventRaiser.RaiseEvent(null, resourcesCollectedEvent);
            }
        }

        public void AddResourcesToPlayer(string playerName, ResourceClutch value)
        {
            // TODO: Return an error if player not found?
            this.players
                .Where(p => p.Name == playerName)
                .FirstOrDefault()
                ?.AddResources(value);
        }

        public void Quit()
        {
            this.isQuitting = true;
            this.eventRaiser.CanRaiseEvents = false;
        }

        private void GameSetup()
        {
            // Place first settlement
            for (int i = 0; i < this.players.Length; i++)
            {
                this.GameSetupLoop(this.players[i]);
            }

            // Place second settlement
            for (int i = this.players.Length - 1; i >= 0; i--)
            {
                this.GameSetupLoop(this.players[i]);
            }
        }

        private void GameSetupLoop(IPlayer player)
        {
            // 1) Notify player to choose settlement location (Pass in current locations)
            // 2) Pause waiting for player to return settlement choice
            this.currentTurnToken = new TurnToken();
            this.eventRaiser.RaiseEvent(player.Name, new PlaceSetupInfrastructureEvent(this.currentTurnToken));
            this.turnTimer.Reset();
            while (true)
            {
                Thread.Sleep(50);
                if (this.isQuitting)
                    return;

                if (this.turnTimer.IsLate)
                {
                    // Out of time so game should be killed
                    throw new Exception($"Time out exception waiting for player '{player.Name}'");
                }

                if (this.actionRequests.TryDequeue(out var playerAction))
                {
                    if (playerAction is EndOfTurnAction)
                    {
                        break;
                    }
                    else if (playerAction is PlaceInfrastructureAction placeInfrastructureAction)
                    {
                        this.PlaceInfrastructure(player, placeInfrastructureAction.SettlementLocation, placeInfrastructureAction.RoadEndLocation);
                    }
                }
            }
        }

        private void PlaceInfrastructure(IPlayer player, uint settlementLocation, uint roadEndLocation)
        {
            // TODO: Validation

            try
            {
                this.gameBoard.PlaceStartingInfrastructure(player.Id, settlementLocation, roadEndLocation);
                player.PlaceStartingInfrastructure();
            }
            catch (Exception e)
            {
                // TODO
            }
        }

        private void MainGameLoop()
        {
            if (this.isQuitting)
                return;

            this.playerIndex = -1;
            this.StartTurn();
            this.turnTimer.Reset();

            while (true)
            {
                Thread.Sleep(50);
                if (this.isQuitting)
                    return;

                var gotPlayerAction = this.actionRequests.TryDequeue(out var playerAction);

                if (this.turnTimer.IsLate ||
                    (gotPlayerAction && playerAction is EndOfTurnAction))
                {
                    // TODO: If late the send late message to player before starting new turn
                    this.StartTurn();
                    this.turnTimer.Reset();
                    continue;
                }

                if (!gotPlayerAction)
                    continue;

                // Player action to process
                this.ProcessPlayerAction(playerAction);
            }
        }

        private void PlayerActionEventHandler(TurnToken turnToken, ComputerPlayerAction action)
        {
            // TODO: Verify turn token
            this.actionRequests.Enqueue(action);
        }

        private void ProcessPlayerAction(ComputerPlayerAction playerAction)
        {
            if (playerAction is MakeDirectTradeOfferAction)
            {
                foreach (var kv in this.playersById.Where(k => k.Key != playerAction.PlayerId).ToList())
                {
                }
            }
        }

        private void StartTurn()
        {
            try
            {
                this.ChangeToNextPlayerTurn();
                this.currentTurnToken = new TurnToken();
                this.eventRaiser.RaiseEvent(this.currentPlayer.Name, new StartPlayerTurnEventArgs(this.currentTurnToken));

                this.numberGenerator.RollTwoDice(out this.dice1, out this.dice2);
                var diceRollEventArgs = new DiceRollEventArgs(this.dice1, this.dice2);
                this.eventRaiser.RaiseEvent(null, diceRollEventArgs);

                var resourceRoll = this.dice1 + this.dice2;
                if (resourceRoll != 7)
                {
                    this.CollectResourcesAtStartOfTurn(resourceRoll);
                }
                else
                {

                }
            }
            catch (Exception e)
            {
                if (!this.isQuitting)
                    this.GameExceptionEvent?.Invoke(e);
            }
        }

        #region Structures
        private class EventRaiser
        {
            private Dictionary<string, Action<GameEvent>> gameEventHandlersByPlayerName = new Dictionary<string, Action<GameEvent>>();
            private event Action<GameEvent> gameEventHandler;

            public bool CanRaiseEvents { get; set; } = true;

            public void AddEventHandler(string playerName, Action<GameEvent> gameEventHandler)
            {
                this.gameEventHandler += gameEventHandler;
                this.gameEventHandlersByPlayerName.Add(playerName, gameEventHandler);
            }

            public void RaiseEvent(GameEvent gameEvent)
            {
                if (!this.CanRaiseEvents)
                    return;

                this.gameEventHandler.Invoke(gameEvent);
            }

            public void RaiseEvent(string playerName, GameEvent gameEvent)
            {
                if (!this.CanRaiseEvents)
                    return;

                this.gameEventHandlersByPlayerName[playerName].Invoke(gameEvent);
            }
        }
        #endregion
    }

    public class StartPlayerTurnEventArgs : GameEventArg<TurnToken>
    {
        public StartPlayerTurnEventArgs(TurnToken item) : base(item) { }
    }

    public class DiceRollEventArgs : GameEvent
    {
        public uint Dice1, Dice2;
        public DiceRollEventArgs(uint dice1, uint dice2) : base(Guid.Empty)
        {
            this.Dice1 = dice1;
            this.Dice2 = dice2;
        }
    }
}
