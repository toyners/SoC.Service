﻿
namespace Jabberwocky.SoC.Service
{
  using System;
  using System.Collections.Concurrent;
  using System.Collections.Generic;
  using System.Threading;
  using System.Threading.Tasks;
  using Library;

  public class GameSessionManager
  {
    public enum States
    {
      Stopped,
      Stopping,
      Running
    }

    #region Fields
    private List<IServiceProviderCallback> clients;
    private Dictionary<Guid, GameSession> gameSessions;
    private ConcurrentQueue<IServiceProviderCallback> waitingForGameQueue;
    private Task matchingTask;
    private UInt32 maximumPlayerCount;
    private IDiceRollerFactory diceRollerFactory;
    private CancellationTokenSource cancellationTokenSource;
    #endregion

    #region Construction
    public GameSessionManager(IDiceRollerFactory diceRollerFactory, UInt32 maximumPlayerCount = 1)
    {
      this.clients = new List<IServiceProviderCallback>();
      this.waitingForGameQueue = new ConcurrentQueue<IServiceProviderCallback>();
      this.gameSessions = new Dictionary<Guid, GameSession>();
      this.maximumPlayerCount = maximumPlayerCount;
      this.diceRollerFactory = diceRollerFactory;
      this.cancellationTokenSource = new CancellationTokenSource();
      this.State = States.Stopped;
    }
    #endregion

    #region Properties
    public States State { get; private set; }
    #endregion

    #region Methods
    public void AddClient(IServiceProviderCallback client)
    {
      // TODO: Check for null reference
      this.waitingForGameQueue.Enqueue(client);
    }

    public void ConfirmGameInitialized(Guid gameToken, IServiceProviderCallback client)
    {
      if (!this.gameSessions.ContainsKey(gameToken))
      {
        throw new NotImplementedException(); //TODO: Change for Meaningful exception
      }

      var gameSession = this.gameSessions[gameToken];
      gameSession.ConfirmGameInitialized(client);
    }

    public void RemoveClient(Guid gameToken, IServiceProviderCallback client)
    {
      if (!this.gameSessions.ContainsKey(gameToken))
      {
        throw new NotImplementedException(); //TODO: Change for Meaningful exception
      }

      var gameSession = this.gameSessions[gameToken];
      gameSession.RemoveClient(client);
    }

    public void ProcessMessage(Guid gameToken, UInt32 message)
    {
      if (!this.gameSessions.ContainsKey(gameToken))
      {
        throw new NotImplementedException();
      }

      var gameSession = this.gameSessions[gameToken];
      gameSession.ProcessMessage(message);
    }

    public void Stop()
    {
      if (this.State != States.Running)
      {
        return;
      }

      this.cancellationTokenSource.Cancel();
    }

    public void Start()
    {
      if (this.State != States.Stopped)
      {
        return;
      }

      var cancellationToken = this.cancellationTokenSource.Token;

      this.matchingTask = Task.Factory.StartNew(() => { this.MatchPlayersWithGames(cancellationToken); });
    }

    private GameSession AddToNewGameSession(IServiceProviderCallback client, CancellationToken cancellationToken)
    {
      var gameSession = new GameSession(this.diceRollerFactory.Create(), this.maximumPlayerCount, cancellationToken);
      gameSession.AddClient(client);
      this.gameSessions.Add(gameSession.GameToken, gameSession);
      return gameSession;
    }

    private void MatchPlayersWithGames(CancellationToken cancellationToken)
    {
      try
      {
        this.State = States.Running;
        while (true)
        {
          while (this.waitingForGameQueue.IsEmpty)
          {
            cancellationToken.ThrowIfCancellationRequested();
            Thread.Sleep(500);
          }

          IServiceProviderCallback client;
          var gotClient = this.waitingForGameQueue.TryDequeue(out client);
          if (!gotClient)
          {
            // Couldn't get the client from the queue (probably because another thread got it).
            continue;
          }

          GameSession gameSession = null;
          if (!this.TryAddToCurrentGameSession(client, out gameSession))
          {
            gameSession = this.AddToNewGameSession(client, cancellationToken);
          }

          if (!gameSession.NeedsClient)
          {
            // Game is full so start it
            gameSession.StartGame();
          }
        }
      }
      catch (OperationCanceledException)
      {
        // Shutting down - ignore exception
        this.State = States.Stopping;
        foreach (var gameSession in this.gameSessions.Values)
        {
          while (gameSession.State != States.Stopped)
          {
            Thread.Sleep(50);
          }
        }
      }
      finally
      {
        this.State = States.Stopped;
      }
    }

    private Boolean TryAddToCurrentGameSession(IServiceProviderCallback client, out GameSession gameSession)
    {
      gameSession = null;
      foreach (var kv in this.gameSessions)
      {
        gameSession = kv.Value;
        if (gameSession.NeedsClient)
        {
          gameSession.AddClient(client);
          return true;
        }
      }

      return false;
    }
    #endregion

    #region Classes
    private class GameSession
    {
      #region Fields
      public GameManager Game;

      public Guid GameToken;

      private Board board;
      private CancellationToken cancellationToken;
      private Int32 clientCount;
      private IServiceProviderCallback[] clients;
      private Task gameTask;
      private ConcurrentQueue<UInt32> messages;
      #endregion

      #region Construction
      public GameSession(IDiceRoller diceRoller, UInt32 playerCount, CancellationToken cancellationToken)
      {
        this.GameToken = Guid.NewGuid();

        this.clients = new IServiceProviderCallback[playerCount];

        this.board = new Board(BoardSizes.Standard);
        this.Game = new GameManager(this.board, diceRoller, playerCount, new DevelopmentCardPile());
        this.messages = new ConcurrentQueue<UInt32>();
        this.cancellationToken = cancellationToken;
      }
      #endregion

      #region Properties
      public Boolean NeedsClient
      {
        get { return this.clientCount < this.clients.Length; }
      }

      public States State { get; private set; }
      #endregion

      #region Methods
      public void ConfirmGameInitialized(IServiceProviderCallback client)
      {
        for (UInt32 index = 0; index < this.clientCount; index++)
        {
          if (this.clients[index] == client)
          {
            this.messages.Enqueue(index);
            return;
          }
        }

        //TODO: Remove or make meaningful
        throw new NotImplementedException();
      }

      public void AddClient(IServiceProviderCallback client)
      {
        for (var i = 0; i < this.clients.Length; i++)
        {
          if (this.clients[i] == null)
          {
            var player = new Player(this.board);
            this.clients[i] = client;
            this.clientCount++;
            client.ConfirmGameJoined(this.GameToken);
            return;
          }
        }

        //TODO: Remove or make meaningful
        throw new NotImplementedException();
      }

      public void RemoveClient(IServiceProviderCallback client)
      {
        for (Int32 i = 0; i < this.clients.Length; i++)
        {
          if (this.clients[i] == client)
          {
            this.clients[i] = null;
            this.clientCount--;
            client.ConfirmGameLeft();
            return;
          }
        }

        //TODO: Remove or make meaningful
        throw new NotImplementedException();
      }

      public void ProcessMessage(UInt32 message)
      {
        throw new NotImplementedException();
      }

      public void StartGame()
      {
        this.gameTask = Task.Factory.StartNew(() =>
        {
          this.State = States.Running;
          try
          {
            var gameData = GameInitializationDataBuilder.Build(this.board);
            foreach (var client in this.clients)
            {
              client.InitializeGame(gameData);
            }

            var confirmationCount = 0;
            UInt32 clientIndex = 0;
            while (confirmationCount < this.clientCount)
            {
              this.cancellationToken.ThrowIfCancellationRequested();

              if (this.messages.TryDequeue(out clientIndex))
              {
                confirmationCount++;
                continue;
              }

              Thread.Sleep(50);
            }

            var playerIndexes = this.Game.GetFirstSetupPassOrder();
            var waitingForResponse = true;
            foreach (var playerIndex in playerIndexes)
            {
              var client = this.clients[playerIndex];
              client.PlaceTown();

              // Wait until response from client
              /*while (waitingForResponse)
              {
                Thread.Sleep(50);
              }*/
            }
          }
          catch (OperationCanceledException)
          {
            // Shutting down - ignore exception
            this.State = States.Stopping;
          }
          finally
          {
            this.State = States.Stopped;
          }
        });
      }
      #endregion
    }
    #endregion
  }
}
