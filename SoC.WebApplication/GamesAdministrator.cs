﻿
namespace SoC.WebApplication
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Jabberwocky.SoC.Library;
    using Jabberwocky.SoC.Library.GameBoards;
    using Jabberwocky.SoC.Library.GameEvents;
    using Jabberwocky.SoC.Library.Interfaces;
    using Jabberwocky.SoC.Library.PlayerActions;
    using Microsoft.AspNetCore.SignalR;
    using SoC.WebApplication.Hubs;
    using SoC.WebApplication.Requests;

    public class GamesAdministrator : IGamesAdministrator, IPlayerRequestReceiver
    {
        private readonly CancellationToken cancellationToken;
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private readonly IHubContext<GameHub> gameHubContext;
        private readonly ConcurrentDictionary<Guid, GameManagerToken> inPlayGamesById = new ConcurrentDictionary<Guid, GameManagerToken>();
        private List<GameManagerToken> inPlayGames = new List<GameManagerToken>();
        private readonly ConcurrentQueue<GameRequest> gameRequests = new ConcurrentQueue<GameRequest>();
        private readonly ConcurrentDictionary<Guid, GameSessionDetails> gamesToLaunchById = new ConcurrentDictionary<Guid, GameSessionDetails>();
        private readonly Task mainGameTask;
        private readonly INumberGenerator numberGenerator = new NumberGenerator();

        public GamesAdministrator(IHubContext<GameHub> gameHubContext)
        {
            this.gameHubContext = gameHubContext;
            this.cancellationToken = this.cancellationTokenSource.Token;
            this.mainGameTask = Task.Factory.StartNew(o => { this.ProcessInPlayGames(); }, null, this.cancellationToken);
        }

        public void AddGame(GameSessionDetails gameDetails)
        {
            this.gamesToLaunchById.TryAdd(gameDetails.Id, gameDetails);
        }

        public void ConfirmGameJoin(ConfirmGameJoinRequest confirmGameJoinRequest)
        {
            var gameId = Guid.Parse(confirmGameJoinRequest.GameId);
            if (this.gamesToLaunchById.TryGetValue(gameId, out var gameDetails))
            {
                var playerId = Guid.Parse(confirmGameJoinRequest.PlayerId);
                var player = gameDetails.Players.First(pd => pd.Id.Equals(playerId));
                player.ConnectionId = confirmGameJoinRequest.ConnectionId;

                var playerWithoutConnectionId = gameDetails.Players.FirstOrDefault(pd => pd.ConnectionId == null);
                if (playerWithoutConnectionId == null)
                {
                    if (this.gamesToLaunchById.TryRemove(gameDetails.Id, out var gd))
                    {
                        var gameManagerToken = this.LaunchGame(gameDetails);
                        if (this.inPlayGamesById.TryAdd(gameDetails.Id, gameManagerToken))
                            this.inPlayGames.Add(gameManagerToken);
                    }
                }
            }
        }

        public void PlayerAction(PlayerActionRequest playerActionRequest)
        {
            if (this.inPlayGamesById.TryGetValue(playerActionRequest.GameId, out var gameManagerToken))
            {
                PlayerAction playerAction = null;
                gameManagerToken.GameManager.Post(playerAction);
            }
        }

        public void Shutdown()
        {
            throw new NotImplementedException();
        }

        private void ProcessInPlayGames()
        {
            try
            {
                var clearDownCount = 0;
                while (true)
                {
                    for (var index = 0; index < this.inPlayGames.Count; index++)
                    {
                        var gameManagerToken = this.inPlayGames[index];
                        if (gameManagerToken != null)
                        {
                            var gameManagerTask = gameManagerToken.Task;
                            var clearDownGame = false;
                            if (gameManagerTask.IsCompletedSuccessfully)
                            {
                                clearDownGame = true;
                            }
                            else if (gameManagerTask.IsFaulted)
                            {
                                clearDownGame = true;
                            }

                            if (clearDownGame)
                            {
                                var gameManager = gameManagerToken.GameManager;
                                if (this.inPlayGamesById.TryRemove(gameManager.Id, out var gmt))
                                {
                                    this.inPlayGames[index] = null;
                                    clearDownCount++;
                                }
                            }
                        }
                    }

                    if (clearDownCount > 10)
                    {
                        this.inPlayGames = this.inPlayGames.Where(token => token != null).ToList();
                        clearDownCount = 0;
                    }

                    Thread.Sleep(100);
                }
            }
            catch (OperationCanceledException)
            {

            }
        }

        private GameManagerToken LaunchGame(GameSessionDetails gameDetails)
        {
            var playerIds = new Queue<Guid>();

            var connectionIdsByPlayerId = new Dictionary<Guid, string>();
            gameDetails.Players.ForEach(player =>
            {
                connectionIdsByPlayerId.Add(player.Id, player.ConnectionId);
                playerIds.Enqueue(player.Id);
            });

            Dictionary<Guid, IEventReceiver> eventReceiversByPlayerId = null;
            List<Bot> bots = null;
            if (gameDetails.TotalBotCount > 0)
            {
                bots = new List<Bot>();
                eventReceiversByPlayerId = new Dictionary<Guid, IEventReceiver>();
                var botNumber = 1;
                while (gameDetails.TotalBotCount-- > 0)
                {
                    var bot = new Bot("Bot #" + (botNumber++), gameDetails.Id, this);
                    bots.Add(bot);
                    eventReceiversByPlayerId.Add(bot.Id, bot);
                    playerIds.Enqueue(bot.Id);
                }
            }
            
            var eventSender = new EventSender(this.gameHubContext, connectionIdsByPlayerId, eventReceiversByPlayerId);

            var gameManager = new GameManager(
                gameDetails.Id,
                this.numberGenerator,
                new GameBoard(BoardSizes.Standard),
                new DevelopmentCardHolder(),
                new PlayerFactory(),
                eventSender,
                new GameOptions
                {
                    Players = gameDetails.NumberOfPlayers,
                    TurnTimeInSeconds = 120
                }
            );

            gameManager.SetIdGenerator(() => { return playerIds.Dequeue(); });

            gameDetails.Players.ForEach(player =>
            {
                gameManager.JoinGame(player.UserName);
            });

            if (bots != null)
            {
                bots.ForEach(bot => 
                {
                    gameManager.JoinGame(bot.Name);
                });
            }

            return new GameManagerToken(gameManager, gameManager.StartGameAsync());
        }

        private class GameManagerToken
        {
            public GameManagerToken(GameManager gameManager, Task task)
            {
                this.GameManager = gameManager;
                this.Task = task;
            }

            public GameManager GameManager { get; private set; }
            public Task Task { get; private set; }
        }

        private class EventSender : IEventSender
        {
            private readonly IHubContext<GameHub> gameHubContext;
            private readonly Dictionary<Guid, string> connectionIdsByPlayerId;
            private readonly Dictionary<Guid, IEventReceiver> eventReceiversByPlayerId;
            public EventSender(IHubContext<GameHub> gameHubContext,
                Dictionary<Guid, string> connectionIdsByPlayerId,
                Dictionary<Guid, IEventReceiver> eventReceiversByPlayerId)
            {
                this.gameHubContext = gameHubContext;
                this.connectionIdsByPlayerId = connectionIdsByPlayerId;
                this.eventReceiversByPlayerId = eventReceiversByPlayerId;
            }

            public void Send(GameEvent gameEvent, Guid playerId)
            {
                if (this.eventReceiversByPlayerId.ContainsKey(playerId))
                {
                    this.eventReceiversByPlayerId[playerId].Post(gameEvent);
                }
                else if (this.connectionIdsByPlayerId.ContainsKey(playerId))
                {
                    var connectionId = this.connectionIdsByPlayerId[playerId];
                    this.gameHubContext.Clients.Client(connectionId).SendAsync("GameEvent", gameEvent);
                }
                else
                {
                    throw new NotImplementedException("Should not get here");
                }
            }
        }
    }
}