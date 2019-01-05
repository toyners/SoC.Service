﻿using System;
using System.Collections.Generic;
using System.Linq;
using Jabberwocky.SoC.Library;
using Jabberwocky.SoC.Library.DevelopmentCards;
using Jabberwocky.SoC.Library.GameBoards;
using Jabberwocky.SoC.Library.GameEvents;
using Jabberwocky.SoC.Library.Interfaces;
using NUnit.Framework;
using SoC.Library.ScenarioTests.PlayerTurn;

namespace SoC.Library.ScenarioTests
{
    internal class LocalGameControllerScenarioRunner
    {
        #region Enums
        public enum GameEventTypes
        {
            LargestArmyEvent
        }

        public enum EventPositions
        {
            Any,
            Last
        }
        #endregion

        #region Fields
        private static LocalGameControllerScenarioRunner localGameControllerScenarioBuilder;
        private readonly ScenarioDevelopmentCardHolder developmentCardHolder = new ScenarioDevelopmentCardHolder();
        private readonly Dictionary<Guid, List<DevelopmentCard>> developmentCardsByPlayerId = new Dictionary<Guid, List<DevelopmentCard>>();
        private readonly List<PlayerSetupAction> firstRoundSetupActions = new List<PlayerSetupAction>(4);
        private readonly Dictionary<Type, GameEvent> lastEventsByType = new Dictionary<Type, GameEvent>();
        private readonly Dictionary<string, IPlayer> playersByName = new Dictionary<string, IPlayer>();
        private readonly Queue<Instruction> playerInstructions = new Queue<Instruction>();
        private readonly ScenarioPlayerPool playerPool = new ScenarioPlayerPool();
        private readonly List<BasePlayerTurn> playerTurns = new List<BasePlayerTurn>();
        private readonly List<PlayerSetupAction> secondRoundSetupActions = new List<PlayerSetupAction>(4);
        private readonly Dictionary<string, ScenarioComputerPlayer> computerPlayersByName = new Dictionary<string, ScenarioComputerPlayer>();
        private readonly List<IPlayer> players = new List<IPlayer>(4);
        private List<GameEvent> actualEvents = null;
        private TurnToken currentToken;
        private Dictionary<GameEventTypes, Delegate> eventHandlersByGameEventType;
        private int expectedEventCount;
        private GameBoard gameBoard;
        private LocalGameController localGameController = null;
        private Queue<GameEvent> relevantEvents = null;
        #endregion

        #region Construction
        private LocalGameControllerScenarioRunner()
        {
            this.NumberGenerator = new ScenarioNumberGenerator();
        }
        #endregion

        #region Properties
        internal ScenarioNumberGenerator NumberGenerator { get; }
        #endregion

        #region Methods
        public static LocalGameControllerScenarioRunner LocalGameController()
        {
            return localGameControllerScenarioBuilder = new LocalGameControllerScenarioRunner();
        }

        public LocalGameControllerScenarioRunner Build(Dictionary<GameEventTypes, Delegate> eventHandlersByGameEventType = null, int expectedEventCount = -1)
        {
            if (this.gameBoard == null)
                this.gameBoard = new GameBoard(BoardSizes.Standard);

            this.localGameController = new LocalGameController(
                this.NumberGenerator, 
                this.playerPool, 
                this.gameBoard, 
                this.developmentCardHolder);
            this.localGameController.DiceRollEvent = this.DiceRollEventHandler;
            this.localGameController.DevelopmentCardPurchasedEvent = (DevelopmentCard c) => { this.actualEvents.Add(new BuyDevelopmentCardEvent(this.players[0].Id)); };
            this.localGameController.ErrorRaisedEvent = (ErrorDetails e) => { Assert.Fail(e.Message); };
            this.localGameController.GameEvents = this.GameEventsHandler;
            this.localGameController.LargestArmyEvent = (newPlayerId, previousPlayerId) => { this.actualEvents.Add(new LargestArmyChangedEvent(newPlayerId, previousPlayerId)); };
            this.localGameController.PlayKnightCardEvent = (PlayKnightCardEvent p) => { this.actualEvents.Add(p); };
            this.localGameController.ResourcesTransferredEvent = (ResourceTransactionList list) =>
            {
                this.actualEvents.Add(new ResourceTransactionEvent(this.players[0].Id, list));
            };
            this.localGameController.StartPlayerTurnEvent = (TurnToken t) => { this.currentToken = t; };

            this.eventHandlersByGameEventType = eventHandlersByGameEventType;
            this.expectedEventCount = expectedEventCount;
            this.relevantEvents = new Queue<GameEvent>();
            this.actualEvents = new List<GameEvent>();

            return this;
        }

        public LocalGameControllerScenarioRunner BuildCityEvent(string playerName, uint cityLocation)
        {
            var playerId = this.playersByName[playerName].Id;
            this.relevantEvents.Enqueue(new CityBuiltEvent(playerId, cityLocation));
            return this;
        }

        public LocalGameControllerScenarioRunner BuildRoadEvent(string playerName, uint roadSegmentStart, uint roadSegmentEnd)
        {
            var playerId = this.playersByName[playerName].Id;
            this.relevantEvents.Enqueue(new RoadSegmentBuiltEvent(playerId, roadSegmentStart, roadSegmentEnd));
            return this;
        }

        public LocalGameControllerScenarioRunner BuildSettlementEvent(string playerName, uint settlementLocation)
        {
            var playerId = this.playersByName[playerName].Id;
            this.relevantEvents.Enqueue(new SettlementBuiltEvent(playerId, settlementLocation));
            return this;
        }

        public LocalGameControllerScenarioRunner DiceRollEvent(string playerName, uint dice1, uint dice2)
        {
            var player = this.playersByName[playerName];

            var expectedDiceRollEvent = new DiceRollEvent(player.Id, dice1, dice2);
            this.relevantEvents.Enqueue(expectedDiceRollEvent);

            return this;
        }

        private ScenarioPlayer expectedPlayer = null;
        private readonly Dictionary<string, ScenarioPlayer> expectedPlayersByName = new Dictionary<string, ScenarioPlayer>();
        public LocalGameControllerScenarioRunner ExpectPlayer(string mainPlayerName)
        {
            this.expectedPlayer = new ScenarioPlayer(mainPlayerName);
            this.expectedPlayersByName.Add(mainPlayerName, this.expectedPlayer);
            return this;
        }

        public IPlayer GetPlayerFromName(string playerName)
        {
            return this.playersByName[playerName];
        }

        public LocalGameControllerScenarioRunner IgnoredEvents(Type matchingType, uint count)
        {
            while (count-- > 0)
                this.relevantEvents.Enqueue(new IgnoredEvent(matchingType));

            return this;
        }

        public LocalGameControllerScenarioRunner IgnoredEvent(Type matchingType)
        {
            return this.IgnoredEvents(matchingType, 1);
        }

        public LocalGameControllerScenarioRunner LargestArmyChangedEvent(string newPlayerName, string previousPlayerName = null, EventPositions eventPosition = EventPositions.Any)
        {
            var newPlayer = this.playersByName[newPlayerName];
            Guid previousPlayerId = Guid.Empty;
            if (previousPlayerName != null)
                previousPlayerId = this.playersByName[previousPlayerName].Id;
            var expectedLargestArmyChangedEvent = new LargestArmyChangedEvent(newPlayer.Id, previousPlayerId);
            this.relevantEvents.Enqueue(expectedLargestArmyChangedEvent);

            if (eventPosition == EventPositions.Last)
                this.lastEventsByType.Add(expectedLargestArmyChangedEvent.GetType(), expectedLargestArmyChangedEvent);

            return this;
        }

        public LocalGameControllerScenarioRunner LongestRoadBuiltEvent(string newPlayerName)
        {
            var newPlayer = this.playersByName[newPlayerName];
            var expectedLongestRoadBuiltEvent = new LongestRoadBuiltEvent(newPlayer.Id, Guid.Empty);
            this.relevantEvents.Enqueue(expectedLongestRoadBuiltEvent);
            return this;
        }

        public LocalGameControllerScenarioRunner ResourcesCollectedEvent(string playerName, uint location, ResourceClutch resourceClutch)
        {
            var player = this.playersByName[playerName];

            return this.ResourcesCollectedEvent(player.Id, new[] { new ResourceCollection(location, resourceClutch) });
        }

        public LocalGameControllerScenarioRunner ResourcesCollectedEvent(Guid playerId, ResourceCollection[] resourceCollection)
        {
            var expectedDiceRollEvent = new ResourcesCollectedEvent(playerId, resourceCollection);
            this.relevantEvents.Enqueue(expectedDiceRollEvent);
            return this;
        }

        public LocalGameControllerScenarioRunner ResourcesGainedEvent(string receivingPlayerName, string givingPlayerName, ResourceClutch expectedResources)
        {
            var receivingPlayer = this.playersByName[receivingPlayerName];
            var givingPlayer = this.playersByName[givingPlayerName];
            var resourceTransaction = new ResourceTransaction(receivingPlayer.Id, givingPlayer.Id, expectedResources);
            var expectedResourceTransactonEvent = new ResourceTransactionEvent(receivingPlayer.Id, resourceTransaction);
            this.relevantEvents.Enqueue(expectedResourceTransactonEvent);
            return this;
        }

        public LocalGameController Run()
        {
            this.localGameController.JoinGame();
            this.localGameController.LaunchGame();
            this.localGameController.StartGameSetup();

            var placeInfrastructureInstruction = (PlaceInfrastructureInstruction)this.playerInstructions.Dequeue();
            this.localGameController.ContinueGameSetup(placeInfrastructureInstruction.SettlementLocation, placeInfrastructureInstruction.RoadEndLocation);

            placeInfrastructureInstruction = (PlaceInfrastructureInstruction)this.playerInstructions.Dequeue();
            this.localGameController.CompleteGameSetup(placeInfrastructureInstruction.SettlementLocation, placeInfrastructureInstruction.RoadEndLocation);

            this.localGameController.StartGamePlay();

            this.CompleteGamePlay();

            

            if (this.relevantEvents != null && this.actualEvents != null)
            {
                if (this.expectedEventCount != -1)
                    Assert.AreEqual(this.expectedEventCount, this.actualEvents.Count, $"Expected event count {this.expectedEventCount} but found actual event count {this.actualEvents.Count}");

                var actualEventIndex = 0;
                while (this.relevantEvents.Count > 0)
                {
                    GameEvent lastEvent = null;
                    var expectedEvent = this.relevantEvents.Dequeue();
                    var foundEvent = false;
                    while (actualEventIndex < this.actualEvents.Count)
                    {
                        var actualEvent = this.actualEvents[actualEventIndex++];

                        if (this.lastEventsByType.TryGetValue(actualEvent.GetType(), out lastEvent) && lastEvent == null)
                        {
                            Assert.Fail($"{actualEvent} event found after last event of type {actualEvent.GetType()} was matched.");
                        }

                        if (expectedEvent.Equals(actualEvent))
                        {
                            if (this.lastEventsByType.TryGetValue(actualEvent.GetType(), out lastEvent) && lastEvent != null)
                            {
                                this.lastEventsByType[actualEvent.GetType()] = null;
                            }

                            foundEvent = true;
                            break;
                        }
                    }

                    if (!foundEvent)
                        Assert.Fail(this.ToMessage(expectedEvent));
                }
            }

            foreach (var expectedPlayerPair in this.expectedPlayersByName)
            {
                var expectedPlayer = expectedPlayerPair.Value;
                var actualPlayer = this.playersByName[expectedPlayer.Name];

                Assert.AreEqual(expectedPlayer.VictoryPoints, actualPlayer.VictoryPoints, $"Expected player '{actualPlayer.Name}' to have {expectedPlayer.VictoryPoints} victory points but has {actualPlayer.VictoryPoints} victory points");
            }

            return this.localGameController;
        }

        public LocalGameControllerScenarioRunner GameWinEvent(string firstOpponentName, uint expectedVictoryPoints)
        {
            var playerId = this.playersByName[firstOpponentName].Id;
            var expectedGameWonEvent = new GameWinEvent(playerId, expectedVictoryPoints);
            this.relevantEvents.Enqueue(expectedGameWonEvent);
            return this;
        }

        public LocalGameControllerScenarioRunner PlayKnightCardEvent(string playerName)
        {
            var player = this.playersByName[playerName];
            var expectedPlayKnightCardEvent = new PlayKnightCardEvent(player.Id);
            this.relevantEvents.Enqueue(expectedPlayKnightCardEvent);
            return this;
        }

        public ResourceCollectedEventGroup StartResourcesCollectedEvent(string playerName)
        {
            var player = this.playersByName[playerName];
            var eventGroup = new ResourceCollectedEventGroup(player.Id, this);
            return eventGroup;
        }

        public LocalGameControllerScenarioRunner WithComputerPlayer(string name)
        {
            this.CreatePlayer(name, true);
            return this;
        }

        public LocalGameControllerScenarioRunner WithMainPlayer(string name)
        {
            this.CreatePlayer(name, false);
            return this;
        }

        public LocalGameControllerScenarioRunner WithNoResourceCollection()
        {
            this.gameBoard = new ScenarioGameBoardWithNoResourcesCollected();
            return this;
        }

        public LocalGameControllerScenarioRunner WithPlayerSetup(string playerName, uint firstSettlementLocation, uint firstRoadEndLocation, uint secondSettlementLocation, uint secondRoadEndLocation)
        {
            if (playerName == this.players[0].Name)
            {
                this.playerInstructions.Enqueue(new PlaceInfrastructureInstruction(this.players[0].Id, firstSettlementLocation, firstRoadEndLocation));
                this.playerInstructions.Enqueue(new PlaceInfrastructureInstruction(this.players[0].Id, secondSettlementLocation, secondRoadEndLocation));
            }
            else
            {
                var computerPlayer = this.computerPlayersByName[playerName];
                computerPlayer.AddSetupInstructions(
                    new PlaceInfrastructureInstruction(computerPlayer.Id, firstSettlementLocation, firstRoadEndLocation),
                    new PlaceInfrastructureInstruction(computerPlayer.Id, secondSettlementLocation, secondRoadEndLocation));
            }

            return this;
        }

        public LocalGameControllerScenarioRunner WithTurnOrder(string firstPlayerName, string secondPlayerName, string thirdPlayerName, string fourthPlayerName)
        {
            var rolls = new uint[4];
            for (var index = 0; index < this.players.Count; index++)
            {
                var player = this.players[index];
                if (firstPlayerName == player.Name)
                    rolls[index] = 12;
                else if (secondPlayerName == player.Name)
                    rolls[index] = 10;
                else if (thirdPlayerName == player.Name)
                    rolls[index] = 8;
                else
                    rolls[index] = 6;
            }

            foreach (var roll in rolls)
                this.NumberGenerator.AddTwoDiceRoll(roll / 2, roll / 2);

            return this;
        }

        public BasePlayerTurn DuringPlayerTurn(string playerName, uint dice1, uint dice2)
        {
            this.NumberGenerator.AddTwoDiceRoll(dice1, dice2);

            BasePlayerTurn playerTurn = null;

            if (playerName == this.players[0].Name)
            {
                playerTurn = new HumanPlayerTurn(this, this.players[0]);
            }
            else
            {
                playerTurn = new ComputerPlayerTurn(this, this.computerPlayersByName[playerName]);
            }

            this.playerTurns.Add(playerTurn);
            return playerTurn;
        }

        public LocalGameControllerScenarioRunner BuyDevelopmentCardEvent(string playerName, DevelopmentCardTypes developmentCardType)
        {
            var player = this.playersByName[playerName];
            if (player is ScenarioPlayer mockPlayer)
            {

            }
            else if (player is ScenarioComputerPlayer mockComputerPlayer)
            {
                var expectedBuyDevelopmentCardEvent = new ScenarioBuyDevelopmentCardEvent(mockComputerPlayer, developmentCardType);
                this.relevantEvents.Enqueue(expectedBuyDevelopmentCardEvent);
            }

            return this;
        }

        public LocalGameControllerScenarioRunner WithStartingResourcesForPlayer(string playerName, ResourceClutch playerResources)
        {
            var player = this.playersByName[playerName];
            player.AddResources(playerResources);
            return this;
        }

        public LocalGameControllerScenarioRunner VictoryPoints(uint expectedVictoryPoints)
        {
            this.expectedPlayer.SetVictoryPoints(expectedVictoryPoints);
            return this;
        }

        internal void AddDevelopmentCardToBuy(Guid playerId, DevelopmentCardTypes developmentCardType)
        {
            DevelopmentCard developmentCard = null;
            switch (developmentCardType)
            {
                case DevelopmentCardTypes.Knight: developmentCard = new KnightDevelopmentCard(); break;
                default: throw new Exception($"Development card type {developmentCardType} not recognised");
            }

            this.developmentCardHolder.AddDevelopmentCard(developmentCard);

            if (!this.developmentCardsByPlayerId.TryGetValue(playerId, out var developmentCardsForPlayerId))
            {
                developmentCardsForPlayerId = new List<DevelopmentCard>();
                this.developmentCardsByPlayerId.Add(playerId, developmentCardsForPlayerId);
            }

            developmentCardsForPlayerId.Add(developmentCard);
        }

        private void CompleteGamePlay()
        {
            for (var index = 0; index < this.playerTurns.Count; index++)
            {
                var turn = this.playerTurns[index];
                if (turn is HumanPlayerTurn && index > 0)
                    this.localGameController.EndTurn(this.currentToken);

                turn.ResolveActions(this.currentToken, this.localGameController);
            }

            this.localGameController.EndTurn(this.currentToken);
        }

        private IPlayer CreatePlayer(string name, bool isComputerPlayer)
        {
            IPlayer player = isComputerPlayer
                ? new ScenarioComputerPlayer(name, this.NumberGenerator) as IPlayer
                : new ScenarioPlayer(name) as IPlayer;

            this.players.Add(player);
            this.playersByName.Add(name, player);
            this.playerPool.AddPlayer(player);
            if (isComputerPlayer)
                this.computerPlayersByName.Add(name, (ScenarioComputerPlayer)player);

            return player;
        }

        private void DiceRollEventHandler(Guid playerId, uint dice1, uint dice2)
        {
            this.actualEvents.Add(new DiceRollEvent(playerId, dice1, dice2));
        }

        private void GameEventsHandler(List<GameEvent> gameEvents)
        {
            this.actualEvents.AddRange(gameEvents);

            if (this.eventHandlersByGameEventType != null)
            {
                foreach (var gameEvent in gameEvents)
                {
                    if (gameEvent is LargestArmyChangedEvent && this.eventHandlersByGameEventType.TryGetValue(GameEventTypes.LargestArmyEvent, out var eventHandler))
                        ((Action<LargestArmyChangedEvent>)eventHandler).Invoke((LargestArmyChangedEvent)gameEvent);
                }
            }
        }

        private string ToMessage(GameEvent gameEvent)
        {
            var player = this.players.Where(p => p.Id.Equals(gameEvent.PlayerId)).FirstOrDefault();

            var message = $"Did not find {gameEvent.GetType()} event for '{player.Name}'.";
                
            if (gameEvent is ResourceTransactionEvent resourceTransactionEvent)
            {
                message += $"\r\nResource transaction count is {resourceTransactionEvent.ResourceTransactions.Count}";
                for (var index = 0; index < resourceTransactionEvent.ResourceTransactions.Count; index++)
                {
                    var resourceTransaction = resourceTransactionEvent.ResourceTransactions[index];
                    var receivingPlayer = this.players.Where(p => p.Id.Equals(resourceTransaction.ReceivingPlayerId)).FirstOrDefault();
                    var givingPlayer = this.players.Where(p => p.Id.Equals(resourceTransaction.GivingPlayerId)).FirstOrDefault();
                    message += $"\r\nTransaction is: Receiving player '{receivingPlayer.Name}', Giving player '{givingPlayer.Name}', Resources {resourceTransaction.Resources}";
                }
            }

            return message;
        }
        #endregion
    }
}
