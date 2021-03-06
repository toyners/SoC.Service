﻿
namespace Jabberwocky.SoC.Library.UnitTests.LocalGameController_Tests
{
    using System;
    using System.Collections.Generic;
    using GameEvents;
    using Jabberwocky.SoC.Library.PlayerData;
    using Jabberwocky.SoC.Library.UnitTests.Extensions;
    using Mock;
    using NUnit.Framework;
    using Shouldly;

    [TestFixture]
    [Category("All")]
    [Category("LocalGameController")]
    [Category("LocalGameController.BuildRoadSegment")]
    public class LocalGameController_BuildRoadSegment_Tests : LocalGameControllerTestBase
    {
        #region Methods
        [Test]
        public void BuildRoadSegment_ValidScenario_RoadSegmentBuiltEventRaised()
        {
            // Arrange
            MockDice mockDice = null;
            MockPlayer player;
            MockComputerPlayer firstOpponent, secondOpponent, thirdOpponent;
            var localGameController = this.CreateLocalGameControllerAndCompleteGameSetup(out mockDice, out player, out firstOpponent, out secondOpponent, out thirdOpponent);
            mockDice.AddSequence(new[] { 8u });
            player.AddResources(new ResourceClutch(1, 0, 1, 0, 0));

            PlayerDataBase playerDataBase = null;
            //localGameController.RoadSegmentBuiltEvent = (PlayerDataBase p) => { playerDataBase = p; };
            GameToken turnToken = null;
            localGameController.StartPlayerTurnEvent = (GameToken t) => { turnToken = t; };
            localGameController.StartGamePlay();

            // Act
            localGameController.BuildRoadSegment(turnToken, 4u, 3u);

            // Assert
            //playerDataBase.ShouldNotBeNull();
            Assert.Fail("Fail");
        }

        [Test]
        public void BuildRoadSegment_RequiredResourcesAvailable_PlayerResourcesUpdated()
        {
            // Arrange
            var testInstances = LocalGameControllerTestCreator.CreateTestInstances(new MockGameBoardWithNoResourcesCollected());
            var localGameController = testInstances.LocalGameController;
            var player = testInstances.MainPlayer;
            LocalGameControllerTestSetup.LaunchGameAndCompleteSetup(localGameController);
            testInstances.Dice.AddSequence(new[] { 8u });

            player.AddResources(ResourceClutch.RoadSegment);

            GameToken turnToken = null;
            localGameController.StartPlayerTurnEvent = (GameToken t) => { turnToken = t; };
            localGameController.StartGamePlay();

            // Act
            localGameController.BuildRoadSegment(turnToken, 4u, 3u);

            // Assert
            player.Resources.BrickCount.ShouldBe(0);
            player.Resources.LumberCount.ShouldBe(0);
        }

        [Test]
        public void BuildRoadSegment_MainPlayerFirstToBuildLongestRoad_LongestRoadEventRaised()
        {
            // Arrange
            var testInstances = LocalGameControllerTestCreator.CreateTestInstances();
            var localGameController = testInstances.LocalGameController;
            LocalGameControllerTestSetup.LaunchGameAndCompleteSetup(localGameController);

            testInstances.Dice.AddSequence(new[] { 8u });
            var player = testInstances.MainPlayer;
            player.AddResources(ResourceClutch.RoadSegment * 5);

            Guid previousPlayerId = player.Id; // Ensure there is a noticable state change;
            Guid newPlayerId = Guid.NewGuid();
            localGameController.LongestRoadBuiltEvent = (Guid pid, Guid nid) => { previousPlayerId = pid; newPlayerId = nid; };

            GameToken turnToken = null;
            localGameController.StartPlayerTurnEvent = (GameToken t) => { turnToken = t; };
            localGameController.StartGamePlay();

            // Act
            localGameController.BuildRoadSegment(turnToken, 4, 3);
            localGameController.BuildRoadSegment(turnToken, 3, 2);
            localGameController.BuildRoadSegment(turnToken, 2, 1);
            localGameController.BuildRoadSegment(turnToken, 1, 0);

            // Assert
            previousPlayerId.ShouldBe(Guid.Empty);
            newPlayerId = player.Id;
        }

        [Test]
        public void BuildRoadSegment_MainPlayerFirstToBuildLongestRoad_MainPlayerGetsTwoVictoryPoints()
        {
            // Arrange
            var testInstances = LocalGameControllerTestCreator.CreateTestInstances();
            var localGameController = testInstances.LocalGameController;
            LocalGameControllerTestSetup.LaunchGameAndCompleteSetup(localGameController);

            testInstances.Dice.AddSequence(new[] { 8u });
            var player = testInstances.MainPlayer;
            player.AddResources(ResourceClutch.RoadSegment * 5);

            GameToken turnToken = null;
            localGameController.StartPlayerTurnEvent = (GameToken t) => { turnToken = t; };
            localGameController.StartGamePlay();

            // Act
            localGameController.BuildRoadSegment(turnToken, 4, 3);
            localGameController.BuildRoadSegment(turnToken, 3, 2);
            localGameController.BuildRoadSegment(turnToken, 2, 1);
            localGameController.BuildRoadSegment(turnToken, 1, 0);

            // Assert
            player.VictoryPoints.ShouldBe(4u);
        }

        [Test]
        public void BuildRoadSegment_MainPlayerBuildsLongerRoadThanOpponent_LongestRoadEventRaised()
        {
            // Arrange
            var testInstances = LocalGameControllerTestCreator.CreateTestInstances();
            var localGameController = testInstances.LocalGameController;
            LocalGameControllerTestSetup.LaunchGameAndCompleteSetup(localGameController);

            testInstances.Dice.AddSequence(new[] { 8u, 8u, 8u, 8u, 8u });

            var player = testInstances.MainPlayer;
            player.AddResources(ResourceClutch.RoadSegment * 7);

            var firstOpponent = testInstances.FirstOpponent;
            firstOpponent.AddResources(ResourceClutch.RoadSegment * 6);
            firstOpponent.AddBuildRoadSegmentInstruction(new BuildRoadSegmentInstruction { Locations = new UInt32[] { 17, 16, 16, 27, 27, 28, 28, 29, 29, 18 } });

            GameToken turnToken = null;
            localGameController.StartPlayerTurnEvent = (GameToken t) => { turnToken = t; };

            Guid previousLongestRoadPlayerId = Guid.Empty;
            Guid newLongestRoadPlayerId = Guid.Empty;
            localGameController.LongestRoadBuiltEvent = (Guid p, Guid n) => { previousLongestRoadPlayerId = p; newLongestRoadPlayerId = n; };

            localGameController.StartGamePlay();

            localGameController.BuildRoadSegment(turnToken, 4, 3);
            localGameController.BuildRoadSegment(turnToken, 3, 2);
            localGameController.BuildRoadSegment(turnToken, 2, 10);
            localGameController.BuildRoadSegment(turnToken, 10, 9);
            localGameController.EndTurn(turnToken); // First opponent builds 6 road segments to take the longest road title

            // Build two more road segments for a new longest road of 7
            localGameController.BuildRoadSegment(turnToken, 9, 8);
            localGameController.BuildRoadSegment(turnToken, 8, 0);

            // Assert
            previousLongestRoadPlayerId.ShouldBe(firstOpponent.Id);
            newLongestRoadPlayerId.ShouldBe(player.Id);
        }

        [Test]
        public void BuildRoadSegment_MainPlayerBuildsLongerRoadThanOpponent_VictoryPointsChangesFromOpponentToPlayer()
        {
            // Arrange
            var testInstances = LocalGameControllerTestCreator.CreateTestInstances();
            var localGameController = testInstances.LocalGameController;
            LocalGameControllerTestSetup.LaunchGameAndCompleteSetup(localGameController);

            testInstances.Dice.AddSequence(new[] { 8u, 8u, 8u, 8u, 8u });

            var player = testInstances.MainPlayer;
            player.AddResources(ResourceClutch.RoadSegment * 7);

            var firstOpponent = testInstances.FirstOpponent;
            firstOpponent.AddResources(ResourceClutch.RoadSegment * 6);

            GameToken turnToken = null;
            localGameController.StartPlayerTurnEvent = (GameToken t) => { turnToken = t; };

            Guid previousLongestRoadPlayerId = Guid.Empty;
            Guid newLongestRoadPlayerId = Guid.Empty;
            localGameController.LongestRoadBuiltEvent = (Guid p, Guid n) => { previousLongestRoadPlayerId = p; newLongestRoadPlayerId = n; };

            localGameController.StartGamePlay();

            localGameController.BuildRoadSegment(turnToken, 4, 3);
            localGameController.BuildRoadSegment(turnToken, 3, 2);
            localGameController.BuildRoadSegment(turnToken, 2, 10);
            localGameController.BuildRoadSegment(turnToken, 10, 9);
            localGameController.EndTurn(turnToken);

            localGameController.BuildRoadSegment(turnToken, 9, 8);
            localGameController.BuildRoadSegment(turnToken, 8, 0);

            // Assert
            player.VictoryPoints.ShouldBe(4u);
            firstOpponent.VictoryPoints.ShouldBe(2u);
        }

        [Test]
        [TestCase(new UInt32[] { 4, 3 })]
        [TestCase(new UInt32[] { 4, 3, 3, 2 })]
        [TestCase(new UInt32[] { 4, 3, 3, 2, 2, 1 })]
        public void BuildRoadSegment_AddToRoadShorterThanFiveSegments_LongestRoadEventNotRaised(UInt32[] roadLocations)
        {
            // Arrange
            MockDice mockDice = null;
            MockPlayer player;
            MockComputerPlayer firstOpponent, secondOpponent, thirdOpponent;
            var localGameController = this.CreateLocalGameControllerAndCompleteGameSetup(out mockDice, out player, out firstOpponent, out secondOpponent, out thirdOpponent);

            mockDice.AddSequence(new[] { 8u });
            var roadCount = roadLocations.Length / 2;
            var brickCount = roadCount;
            var lumberCount = roadCount;
            player.AddResources(new ResourceClutch(brickCount, 0, lumberCount, 0, 0));

            Boolean longestRoadBuiltEventRaised = false;
            localGameController.LongestRoadBuiltEvent = (Guid pid, Guid nid) => { longestRoadBuiltEventRaised = true; };

            GameToken turnToken = null;
            localGameController.StartPlayerTurnEvent = (GameToken t) => { turnToken = t; };
            localGameController.StartGamePlay();

            // Act
            for (var index = 0; index < roadLocations.Length; index += 2)
            {
                localGameController.BuildRoadSegment(turnToken, roadLocations[index], roadLocations[index + 1]);
            }

            // Assert
            longestRoadBuiltEventRaised.ShouldBeFalse();
        }

        [Test]
        public void BuildRoadSegment_OnExistingRoadSegment_MeaningfulErrorIsReceived()
        {
            // Arrange
            MockDice mockDice = null;
            MockPlayer player;
            MockComputerPlayer firstOpponent, secondOpponent, thirdOpponent;
            var localGameController = this.CreateLocalGameControllerAndCompleteGameSetup(out mockDice, out player, out firstOpponent, out secondOpponent, out thirdOpponent);

            mockDice.AddSequence(new[] { 8u });
            player.AddResources(new ResourceClutch(1, 0, 1, 0, 0));

            localGameController.LongestRoadBuiltEvent = (Guid pid, Guid nid) => { throw new NotImplementedException(); };
            ErrorDetails errorDetails = null;
            localGameController.ErrorRaisedEvent = (ErrorDetails e) => { errorDetails = e; };

            GameToken turnToken = null;
            localGameController.StartPlayerTurnEvent = (GameToken t) => { turnToken = t; };
            PlayerDataBase playerData = null;
            //localGameController.RoadSegmentBuiltEvent = (PlayerDataBase p) => { playerData = p; };
            localGameController.StartGamePlay();

            // Act
            localGameController.BuildRoadSegment(turnToken, MainRoadOneEnd, MainSettlementOneLocation);

            // Assert
            Assert.Fail("Fail");
            playerData.ShouldBeNull();
            errorDetails.ShouldNotBeNull();
            errorDetails.Message.ShouldBe("Cannot build road segment. Road segment between " + MainRoadOneEnd + " and " + MainSettlementOneLocation + " already exists.");
        }

        [Test]
        public void BuildRoadSegment_NotConnectedToExistingInfrastructure_MeaningfulErrorIsReceived()
        {
            // Arrange
            MockDice mockDice = null;
            MockPlayer player;
            MockComputerPlayer firstOpponent, secondOpponent, thirdOpponent;
            var localGameController = this.CreateLocalGameControllerAndCompleteGameSetup(out mockDice, out player, out firstOpponent, out secondOpponent, out thirdOpponent);

            mockDice.AddSequence(new[] { 8u });
            player.AddResources(new ResourceClutch(1, 0, 1, 0, 0));

            localGameController.LongestRoadBuiltEvent = (Guid pid, Guid nid) => { throw new NotImplementedException(); };
            ErrorDetails errorDetails = null;
            localGameController.ErrorRaisedEvent = (ErrorDetails e) => { errorDetails = e; };

            GameToken turnToken = null;
            localGameController.StartPlayerTurnEvent = (GameToken t) => { turnToken = t; };
            localGameController.StartGamePlay();

            // Act
            localGameController.BuildRoadSegment(turnToken, 0, 1);

            // Assert
            errorDetails.ShouldNotBeNull();
            errorDetails.Message.ShouldBe("Cannot build road segment. Road segment [0, 1] not connected to existing road segment.");
        }

        [Test]
        public void BuildRoadSegment_OffBoard_MeaningfulErrorIsReceived()
        {
            // Arrange
            MockDice mockDice = null;
            MockPlayer player;
            MockComputerPlayer firstOpponent, secondOpponent, thirdOpponent;
            var localGameController = this.CreateLocalGameControllerAndCompleteGameSetup(out mockDice, out player, out firstOpponent, out secondOpponent, out thirdOpponent);

            mockDice.AddSequence(new[] { 8u });
            player.AddResources(new ResourceClutch(1, 0, 1, 0, 0));

            localGameController.LongestRoadBuiltEvent = (Guid pid, Guid nid) => { throw new NotImplementedException(); };
            ErrorDetails errorDetails = null;
            localGameController.ErrorRaisedEvent = (ErrorDetails e) => { errorDetails = e; };

            GameToken turnToken = null;
            localGameController.StartPlayerTurnEvent = (GameToken t) => { turnToken = t; };
            localGameController.StartGamePlay();

            // Act
            localGameController.BuildRoadSegment(turnToken, 100, 101);

            // Assert
            errorDetails.ShouldNotBeNull();
            errorDetails.Message.ShouldBe("Cannot build road segment. Locations 100 and/or 101 are outside of board range (0 - 53).");
        }

        [Test]
        public void BuildRoadSegment_NoDirectConnection_MeaningfulErrorIsReceived()
        {
            // Arrange
            MockDice mockDice = null;
            MockPlayer player;
            MockComputerPlayer firstOpponent, secondOpponent, thirdOpponent;
            var localGameController = this.CreateLocalGameControllerAndCompleteGameSetup(out mockDice, out player, out firstOpponent, out secondOpponent, out thirdOpponent);

            mockDice.AddSequence(new[] { 8u });
            player.AddResources(new ResourceClutch(1, 0, 1, 0, 0));

            localGameController.LongestRoadBuiltEvent = (Guid pid, Guid nid) => { throw new NotImplementedException(); };
            ErrorDetails errorDetails = null;
            localGameController.ErrorRaisedEvent = (ErrorDetails e) => { errorDetails = e; };

            GameToken turnToken = null;
            localGameController.StartPlayerTurnEvent = (GameToken t) => { turnToken = t; };
            localGameController.StartGamePlay();

            // Act
            localGameController.BuildRoadSegment(turnToken, MainRoadOneEnd, 0);

            // Assert
            errorDetails.ShouldNotBeNull();
            errorDetails.Message.ShouldBe("Cannot build road segment. No direct connection between locations [4, 0].");
        }

        [Test]
        [TestCase(0, 0, "Cannot build road segment. Missing 1 brick and 1 lumber.")]
        [TestCase(1, 0, "Cannot build road segment. Missing 1 lumber.")]
        [TestCase(0, 1, "Cannot build road segment. Missing 1 brick.")]
        public void BuildRoadSegment_WithoutRequiredResourcesAvailable_MeaningfulErrorIsReceived(Int32 brickCount, Int32 lumberCount, String expectedErrorMessage)
        {
            // Arrange
            var testInstances = LocalGameControllerTestCreator.CreateTestInstances(new MockGameBoardWithNoResourcesCollected());
            var localGameController = testInstances.LocalGameController;
            var player = testInstances.MainPlayer;
            LocalGameControllerTestSetup.LaunchGameAndCompleteSetup(localGameController);
            testInstances.Dice.AddSequence(new[] { 8u });

            player.AddResources(new ResourceClutch(brickCount, 0, lumberCount, 0, 0));

            ErrorDetails errorDetails = null;
            localGameController.ErrorRaisedEvent = (ErrorDetails e) => { errorDetails = e; };

            GameToken turnToken = null;
            localGameController.StartPlayerTurnEvent = (GameToken t) => { turnToken = t; };
            localGameController.StartGamePlay();

            // Act
            localGameController.BuildRoadSegment(turnToken, 4u, 3u);

            // Assert
            errorDetails.ShouldNotBeNull();
            errorDetails.Message.ShouldBe(expectedErrorMessage);
        }

        [Test]
        public void BuildRoadSegment_AllRoadSegmentsAreBuilt_MeaningfulErrorIsReceived()
        {
            // Arrange
            MockDice mockDice = null;
            MockPlayer player;
            MockComputerPlayer firstOpponent, secondOpponent, thirdOpponent;
            var localGameController = this.CreateLocalGameControllerAndCompleteGameSetup(out mockDice, out player, out firstOpponent, out secondOpponent, out thirdOpponent);
            mockDice.AddSequence(new[] { 8u });
            player.AddResources(new ResourceClutch(15, 0, 15, 0, 0));

            ErrorDetails errorDetails = null;
            localGameController.ErrorRaisedEvent = (ErrorDetails e) =>
            {
                if (errorDetails != null)
                {
                    // Ensure that the error details are only received once.
                    throw new Exception("Already received error details");
                }

                errorDetails = e;
            };

            GameToken turnToken = null;
            localGameController.StartPlayerTurnEvent = (GameToken t) => { turnToken = t; };
            localGameController.StartGamePlay();

            var roadSegmentDetails = new UInt32[] { 4, 3, 3, 2, 2, 1, 1, 0, 0, 8, 8, 7, 7, 17, 17, 16, 16, 27, 27, 28, 28, 38, 38, 39, 39, 47 };
            for (var index = 0; index < roadSegmentDetails.Length; index += 2)
            {
                localGameController.BuildRoadSegment(turnToken, roadSegmentDetails[index], roadSegmentDetails[index + 1]);
            }

            // Act
            localGameController.BuildRoadSegment(turnToken, 47, 48);

            // Assert
            errorDetails.ShouldNotBeNull();
            errorDetails.Message.ShouldBe("Cannot build road segment. All road segments already built.");
        }

        [Test]
        public void BuildRoadSegment_TurnTokenNotCorrect_MeaningfulErrorIsReceived()
        {
            // Arrange
            MockDice mockDice = null;
            MockPlayer player;
            MockComputerPlayer firstOpponent, secondOpponent, thirdOpponent;
            var localGameController = this.CreateLocalGameControllerAndCompleteGameSetup(out mockDice, out player, out firstOpponent, out secondOpponent, out thirdOpponent);
            mockDice.AddSequence(new[] { 8u });

            ErrorDetails errorDetails = null;
            localGameController.ErrorRaisedEvent = (ErrorDetails e) => { errorDetails = e; };
            localGameController.StartGamePlay();

            // Act
            localGameController.BuildRoadSegment(new GameToken(), 4u, 3u);

            // Assert
            errorDetails.ShouldNotBeNull();
            errorDetails.Message.ShouldBe("Turn token not recognised.");
        }

        [Test]
        public void BuildRoadSegment_TurnTokenIsNull_MeaningfulErrorIsReceived()
        {
            // Arrange
            var testInstances = LocalGameControllerTestCreator.CreateTestInstances();
            var localGameController = testInstances.LocalGameController;
            LocalGameControllerTestSetup.LaunchGameAndCompleteSetup(localGameController);

            testInstances.Dice.AddSequence(new[] { 8u });

            ErrorDetails errorDetails = null;
            localGameController.ErrorRaisedEvent = (ErrorDetails e) => { errorDetails = e; };
            localGameController.StartGamePlay();

            // Act
            localGameController.BuildRoadSegment(null, 4u, 3u);

            // Assert
            errorDetails.ShouldNotBeNull();
            errorDetails.Message.ShouldBe("Turn token is null.");
        }

        [Test]
        public void BuildRoadSegment_GotEightVictoryPoints_EndOfGameEventRaisedWithPlayerAsWinner()
        {
            // Arrange
            var testInstances = LocalGameControllerTestCreator.CreateTestInstances(new MockGameBoardWithNoResourcesCollected());
            var localGameController = testInstances.LocalGameController;
            LocalGameControllerTestSetup.LaunchGameAndCompleteSetup(localGameController);

            testInstances.Dice.AddSequence(new[] { 8u });

            var player = testInstances.MainPlayer;
            player.AddResources(ResourceClutch.RoadSegment * 5);
            player.AddResources(ResourceClutch.Settlement * 3);
            player.AddResources(ResourceClutch.City * 3);

            GameToken turnToken = null;
            localGameController.StartPlayerTurnEvent = (GameToken t) => { turnToken = t; };

            Guid winningPlayer = Guid.Empty;
            localGameController.GameOverEvent = (Guid g) => { winningPlayer = g; };

            localGameController.StartGamePlay();
            localGameController.BuildRoadSegment(turnToken, 4u, 3u);
            localGameController.BuildRoadSegment(turnToken, 3u, 2u);
            localGameController.BuildRoadSegment(turnToken, 2u, 1u);
            localGameController.BuildRoadSegment(turnToken, 2u, 10u);

            localGameController.BuildSettlement(turnToken, 1);
            localGameController.BuildSettlement(turnToken, 3);
            localGameController.BuildSettlement(turnToken, 10);

            localGameController.BuildCity(turnToken, 1);
            localGameController.BuildCity(turnToken, 3);
            localGameController.BuildCity(turnToken, 10);

            // Act
            localGameController.BuildRoadSegment(turnToken, 1, 0);

            // Assert
            winningPlayer.ShouldBe(player.Id);
            player.VictoryPoints.ShouldBe(10u);
        }

        [Test]
        public void BuildRoadSegment_GotNineVictoryPoints_EndOfGameEventRaisedWithPlayerAsWinner()
        {
            // Arrange
            var testInstances = LocalGameControllerTestCreator.CreateTestInstances(new MockGameBoardWithNoResourcesCollected());
            var localGameController = testInstances.LocalGameController;
            LocalGameControllerTestSetup.LaunchGameAndCompleteSetup(localGameController);

            testInstances.Dice.AddSequence(new[] { 8u });

            var player = testInstances.MainPlayer;
            player.AddResources(ResourceClutch.RoadSegment * 5);
            player.AddResources(ResourceClutch.Settlement * 3);
            player.AddResources(ResourceClutch.City * 4);

            GameToken turnToken = null;
            localGameController.StartPlayerTurnEvent = (GameToken t) => { turnToken = t; };

            Guid winningPlayer = Guid.Empty;
            localGameController.GameOverEvent = (Guid g) => { winningPlayer = g; };

            localGameController.StartGamePlay();
            localGameController.BuildRoadSegment(turnToken, 4u, 3u);
            localGameController.BuildRoadSegment(turnToken, 3u, 2u);
            localGameController.BuildRoadSegment(turnToken, 2u, 1u);
            localGameController.BuildRoadSegment(turnToken, 2u, 10u);

            localGameController.BuildSettlement(turnToken, 1);
            localGameController.BuildSettlement(turnToken, 3);
            localGameController.BuildSettlement(turnToken, 10);

            localGameController.BuildCity(turnToken, 1);
            localGameController.BuildCity(turnToken, 3);
            localGameController.BuildCity(turnToken, 10);
            localGameController.BuildCity(turnToken, 12);

            // Act
            localGameController.BuildRoadSegment(turnToken, 1, 0);

            // Assert
            winningPlayer.ShouldBe(player.Id);
            player.VictoryPoints.ShouldBe(11u);
        }

        [Test]
        public void BuildRoadSegment_GameIsOver_MeaningfulErrorIsReceived()
        {
            // Arrange
            var testInstances = LocalGameControllerTestCreator.CreateTestInstances(new MockGameBoardWithNoResourcesCollected());
            var localGameController = testInstances.LocalGameController;
            LocalGameControllerTestSetup.LaunchGameAndCompleteSetup(localGameController);

            testInstances.Dice.AddSequence(new[] { 8u });

            var player = testInstances.MainPlayer;
            player.AddResources(ResourceClutch.RoadSegment * 6);
            player.AddResources(ResourceClutch.Settlement * 3);
            player.AddResources(ResourceClutch.City * 4);

            GameToken turnToken = null;
            localGameController.StartPlayerTurnEvent = (GameToken t) => { turnToken = t; };

            ErrorDetails errorDetails = null;
            localGameController.ErrorRaisedEvent = (ErrorDetails e) => { errorDetails = e; };

            localGameController.StartGamePlay();
            localGameController.BuildRoadSegment(turnToken, 4u, 3u);
            localGameController.BuildRoadSegment(turnToken, 3u, 2u);
            localGameController.BuildRoadSegment(turnToken, 2u, 1u);
            localGameController.BuildRoadSegment(turnToken, 2u, 10u);

            localGameController.BuildSettlement(turnToken, 1);
            localGameController.BuildSettlement(turnToken, 3);
            localGameController.BuildSettlement(turnToken, 10);

            localGameController.BuildCity(turnToken, 1);
            localGameController.BuildCity(turnToken, 3);
            localGameController.BuildCity(turnToken, 10);
            localGameController.BuildCity(turnToken, 12);

            localGameController.BuildRoadSegment(turnToken, 1, 0); // Got 10VP, Game is over

            // Act
            localGameController.BuildRoadSegment(turnToken, LocalGameControllerTestCreator.MainRoadTwoEnd, 47);

            // Assert
            errorDetails.ShouldNotBeNull();
            errorDetails.Message.ShouldBe("Cannot build road segment. Game is over.");
        }

        [Test]
        public void Scenario_OpponentBuildsLongerRoadThanPlayer_LongestRoadEventReturned()
        {
            // Arrange
            var testInstances = LocalGameControllerTestCreator.CreateTestInstances();
            var localGameController = testInstances.LocalGameController;
            LocalGameControllerTestSetup.LaunchGameAndCompleteSetup(localGameController);
            var player = testInstances.MainPlayer;
            var firstOpponent = testInstances.FirstOpponent;

            testInstances.Dice.AddSequence(new uint[] { 8, 8, 8, 8, 8 });
            player.AddResources(ResourceClutch.RoadSegment * 5);

            firstOpponent.AddResources(ResourceClutch.RoadSegment * 6);
            firstOpponent.AddBuildRoadSegmentInstruction(new BuildRoadSegmentInstruction { Locations = new UInt32[] { 17, 16, 16, 27, 27, 28, 28, 29, 29, 18 } });

            var actualEvents = new List<List<GameEvent>>();
            localGameController.GameEvents = (List<GameEvent> e) => { actualEvents.Add(e); };

            GameToken turnToken = null;
            localGameController.StartPlayerTurnEvent = (GameToken t) => { turnToken = t; };
            localGameController.StartGamePlay();

            localGameController.BuildRoadSegment(turnToken, 4, 3);
            localGameController.BuildRoadSegment(turnToken, 3, 2);
            localGameController.BuildRoadSegment(turnToken, 2, 10);
            localGameController.BuildRoadSegment(turnToken, 10, 9);

            // Act - Opponent builds longer road.
            localGameController.EndTurn(turnToken);

            // Assert
            var expectedEvents = new GameEvent[] {
                new RoadSegmentPlacedEvent(firstOpponent.Id, 17, 16),
                new RoadSegmentPlacedEvent(firstOpponent.Id, 16, 27),
                new RoadSegmentPlacedEvent(firstOpponent.Id, 27, 28),
                new RoadSegmentPlacedEvent(firstOpponent.Id, 28, 29),
                new RoadSegmentPlacedEvent(firstOpponent.Id, 29, 18),
                new LongestRoadBuiltEvent(firstOpponent.Id, null, player.Id)
            };

            //actualEvents.ShouldContainExact(expectedEvents);
        }

        /// <summary>
        /// When the a player builds the longest road then they get 2VP and the previous longest road holder loses the 2VP.
        /// </summary>
        [Test]
        public void Scenario_OpponentBuildsLongerRoadThanPlayer_VictoryPointsChangesFromPlayerToOpponent()
        {
            // Arrange
            var testInstances = LocalGameControllerTestCreator.CreateTestInstances();
            var localGameController = testInstances.LocalGameController;
            LocalGameControllerTestSetup.LaunchGameAndCompleteSetup(localGameController);
            var player = testInstances.MainPlayer;
            var firstOpponent = testInstances.FirstOpponent;

            testInstances.Dice.AddSequence(new uint[] { 8, 8, 8, 8, 8 });
            player.AddResources(ResourceClutch.RoadSegment * 5);

            firstOpponent.AddResources(ResourceClutch.RoadSegment * 6);
            firstOpponent.AddBuildRoadSegmentInstruction(new BuildRoadSegmentInstruction { Locations = new UInt32[] { 17, 16, 16, 27, 27, 28, 28, 29, 29, 18 } });

            GameToken turnToken = null;
            localGameController.StartPlayerTurnEvent = (GameToken t) => { turnToken = t; };
            localGameController.StartGamePlay();

            localGameController.BuildRoadSegment(turnToken, 4, 3);
            localGameController.BuildRoadSegment(turnToken, 3, 2);
            localGameController.BuildRoadSegment(turnToken, 2, 10);
            localGameController.BuildRoadSegment(turnToken, 10, 9);

            player.VictoryPoints.ShouldBe(4u);
            firstOpponent.VictoryPoints.ShouldBe(2u);

            // Act - Opponent builds longer road.
            localGameController.EndTurn(turnToken);

            // Assert
            player.VictoryPoints.ShouldBe(2u);
            firstOpponent.VictoryPoints.ShouldBe(4u);
        }
        #endregion
    }
}
