﻿
namespace Jabberwocky.SoC.Library.UnitTests
{
  using System;
  using System.Collections.Generic;
  using GameBoards;
  using Interfaces;
  using NSubstitute;
  using NUnit.Framework;
  using Shouldly;

  [TestFixture]
  public class LocalGameController_UnitTests
  {
    #region Methods
    [Test]
    public void StartJoiningGame_DefaultGameOptions_PlayerDataPassedBack()
    {
      var localGameController = this.CreateLocalGameController();

      PlayerBase[] players = null;
      localGameController.GameJoinedEvent = (PlayerBase[] p) => { players = p; };
      localGameController.StartJoiningGame(new GameOptions());
      
      players.ShouldNotBeNull();
      players.Length.ShouldBe(4);
      players[0].ShouldBeOfType<PlayerData>();
      players[1].ShouldBeOfType<PlayerView>();
      players[2].ShouldBeOfType<PlayerView>();
      players[3].ShouldBeOfType<PlayerView>();
    }

    [Test]
    public void StartJoiningGame_NullGameOptions_PlayerDataPassedBack()
    {
      var localGameController = this.CreateLocalGameController();

      PlayerBase[] players = null;
      localGameController.GameJoinedEvent = (PlayerBase[] p) => { players = p; };
      localGameController.StartJoiningGame(null);
      localGameController.Quit();

      players.ShouldNotBeNull();
      players.Length.ShouldBe(4);
      players[0].ShouldBeOfType<PlayerData>();
      players[1].ShouldBeOfType<PlayerView>();
      players[2].ShouldBeOfType<PlayerView>();
      players[3].ShouldBeOfType<PlayerView>();
    }

    [Test]
    public void StartJoiningGame_TryLaunchingGameWithoutJoining_ReturnsFalse()
    {
      var localGameController = this.CreateLocalGameController();
      localGameController.TryLaunchGame().ShouldBeFalse();
    }

    [Test]
    public void StartJoiningGame_TryLaunchingGameAfterJoining_ReturnsTrue()
    {
      var localGameController = this.CreateLocalGameController();
      localGameController.StartJoiningGame(null);
      localGameController.TryLaunchGame().ShouldBeTrue();
    }

    [Test]
    public void StartJoiningGame_GameLaunchedWithPlayerFirstInInitialSetupRound_InitialBoardPassedBack()
    {
      var mockDice = NSubstitute.Substitute.For<IDice>();
      mockDice.RollTwoDice().Returns(12u, 10u, 8u, 2u);
      var localGameController = this.CreateLocalGameController(mockDice, new ComputerPlayerFactory());

      PlayerBase[] players = null;
      GameBoardData gameBoardData = null;
      localGameController.GameJoinedEvent = (PlayerBase[] p) => { players = p; };
      localGameController.InitialBoardSetupEvent = (GameBoardData g) => { gameBoardData = g; };

      localGameController.StartJoiningGame(null);
      localGameController.TryLaunchGame();

      gameBoardData.ShouldNotBeNull();
      gameBoardData.Settlements.Count.ShouldBe(players.Length);
      gameBoardData.Settlements.ShouldContain(new KeyValuePair<Guid, List<Location>>(players[0].Id, null));
      gameBoardData.Settlements.ShouldContain(new KeyValuePair<Guid, List<Location>>(players[1].Id, null));
      gameBoardData.Settlements.ShouldContain(new KeyValuePair<Guid, List<Location>>(players[2].Id, null));
      gameBoardData.Settlements.ShouldContain(new KeyValuePair<Guid, List<Location>>(players[3].Id, null));

      gameBoardData.Roads.Count.ShouldBe(players.Length);
      gameBoardData.Roads.ShouldContain(new KeyValuePair<Guid, List<Trail>>(players[0].Id, null));
      gameBoardData.Roads.ShouldContain(new KeyValuePair<Guid, List<Trail>>(players[1].Id, null));
      gameBoardData.Roads.ShouldContain(new KeyValuePair<Guid, List<Trail>>(players[2].Id, null));
      gameBoardData.Roads.ShouldContain(new KeyValuePair<Guid, List<Trail>>(players[3].Id, null));
    }

    [Test]
    public void StartJoiningGame_GameLaunchedWithPlayerSecondInInitialSetupRound_InitialBoardPassedBack()
    {
      var mockDice = NSubstitute.Substitute.For<IDice>();
      mockDice.RollTwoDice().Returns(12u, 10u, 8u, 2u);
      var localGameController = this.CreateLocalGameController(mockDice, new ComputerPlayerFactory());

      PlayerBase[] players = null;
      GameBoardData gameBoardData = null;
      localGameController.GameJoinedEvent = (PlayerBase[] p) => { players = p; };
      localGameController.InitialBoardSetupEvent = (GameBoardData g) => { gameBoardData = g; };

      localGameController.StartJoiningGame(null);
      localGameController.TryLaunchGame();

      gameBoardData.ShouldNotBeNull();
      gameBoardData.Settlements.Count.ShouldBe(players.Length);
      gameBoardData.Settlements.ShouldContain(new KeyValuePair<Guid, List<Location>>(players[0].Id, null));
      gameBoardData.Settlements.ShouldContain(new KeyValuePair<Guid, List<Location>>(players[1].Id, new List<Location>()));
      gameBoardData.Settlements.ShouldContain(new KeyValuePair<Guid, List<Location>>(players[2].Id, null));
      gameBoardData.Settlements.ShouldContain(new KeyValuePair<Guid, List<Location>>(players[3].Id, null));

      gameBoardData.Roads.Count.ShouldBe(players.Length);
      gameBoardData.Roads.ShouldContain(new KeyValuePair<Guid, List<Trail>>(players[0].Id, null));
      gameBoardData.Roads.ShouldContain(new KeyValuePair<Guid, List<Trail>>(players[1].Id, new List<Trail>()));
      gameBoardData.Roads.ShouldContain(new KeyValuePair<Guid, List<Trail>>(players[2].Id, null));
      gameBoardData.Roads.ShouldContain(new KeyValuePair<Guid, List<Trail>>(players[3].Id, null));
    }

    [Test]
    public void StartJoiningGame_GameLaunched_TurnTokenPassedBack()
    {
      /*var localGameController = this.CreateLocalGameController();

      Guid turnToken = Guid.Empty;
      localGameController.StartInitialSetupTurnEvent = (Guid t, ) => { turnToken = t; };
      localGameController.StartJoiningGame(null);
      localGameController.Quit();

      turnToken.ShouldNotBe(Guid.Empty);*/
    }

    private LocalGameController CreateLocalGameController()
    {
      return this.CreateLocalGameController(new Dice(), new ComputerPlayerFactory());
    }

    private LocalGameController CreateLocalGameController(IDice diceRoller, IComputerPlayerFactory computerPlayerFactory)
    {
      var localGameController = new LocalGameController(diceRoller, computerPlayerFactory);
      localGameController.GameJoinedEvent = (PlayerBase[] players) => { };
      return localGameController;
    }
    #endregion 
  }
}
