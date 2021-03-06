﻿
namespace Jabberwocky.SoC.Library.UnitTests.LocalGameController_Tests
{
    using System;
    using System.Collections.Generic;
    using GameEvents;
    using Jabberwocky.SoC.Library.UnitTests.Extensions;
    using Mock;
    using NUnit.Framework;
    using Shouldly;

    [TestFixture]
    [Category("All")]
    [Category("LocalGameController")]
    [Category("LocalGameController.BuildCity")]
    public class LocalGameController_BuildCity_Tests : LocalGameControllerTestBase
    {
        #region Tests
        [Test]
        public void BuildCity_OffBoard_MeaningfulErrorIsReceived()
        {
            // Arrange
            MockDice mockDice = null;
            MockPlayer player;
            MockComputerPlayer firstOpponent, secondOpponent, thirdOpponent;
            var localGameController = this.CreateLocalGameControllerAndCompleteGameSetup(out mockDice, out player, out firstOpponent, out secondOpponent, out thirdOpponent);

            mockDice.AddSequence(new[] { 8u });
            player.AddResources(ResourceClutch.City);

            bool cityBuilt = false;
            localGameController.CityBuiltEvent = c => { cityBuilt = true; };

            GameToken turnToken = null;
            localGameController.StartPlayerTurnEvent = (GameToken t) => { turnToken = t; };

            ErrorDetails errorDetails = null;
            localGameController.ErrorRaisedEvent = (ErrorDetails e) => { errorDetails = e; };

            localGameController.StartGamePlay();

            // Act
            localGameController.BuildCity(turnToken, 100);

            // Assert
            cityBuilt.ShouldBeFalse();
            errorDetails.ShouldNotBeNull();
            errorDetails.Message.ShouldBe("Cannot build city. Location 100 is outside of board range (0 - 53).");
        }

        [Test]
        public void BuildCity_OnExistingSettlementBelongingToOpponent_MeaningfulErrorIsReceived()
        {
            // Arrange
            MockDice mockDice = null;
            MockPlayer player;
            MockComputerPlayer firstOpponent, secondOpponent, thirdOpponent;
            var localGameController = this.CreateLocalGameControllerAndCompleteGameSetup(out mockDice, out player, out firstOpponent, out secondOpponent, out thirdOpponent);

            mockDice.AddSequence(new[] { 8u });
            player.AddResources(ResourceClutch.City);

            bool cityBuilt = false;
            localGameController.CityBuiltEvent = c => { cityBuilt = true; };

            GameToken turnToken = null;
            localGameController.StartPlayerTurnEvent = (GameToken t) => { turnToken = t; };

            ErrorDetails errorDetails = null;
            localGameController.ErrorRaisedEvent = (ErrorDetails e) => { errorDetails = e; };

            localGameController.StartGamePlay();

            // Act
            localGameController.BuildCity(turnToken, FirstSettlementOneLocation);

            // Assert
            cityBuilt.ShouldBeFalse();
            errorDetails.ShouldNotBeNull();
            errorDetails.Message.ShouldBe("Cannot build city. Location " + FirstSettlementOneLocation + " is owned by player '" + FirstOpponentName + "'.");
        }

        [Test]
        public void BuildCity_OnExistingSettlementBelongingToPlayer_CityBuiltEventRaised()
        {
            // Arrange
            MockDice mockDice = null;
            MockPlayer player;
            MockComputerPlayer firstOpponent, secondOpponent, thirdOpponent;
            var localGameController = this.CreateLocalGameControllerAndCompleteGameSetup(out mockDice, out player, out firstOpponent, out secondOpponent, out thirdOpponent);

            mockDice.AddSequence(new[] { 8u });
            player.AddResources(ResourceClutch.City);

            Boolean cityBuilt = false;
            localGameController.CityBuiltEvent = c => { cityBuilt = true; };

            GameToken turnToken = null;
            localGameController.StartPlayerTurnEvent = (GameToken t) => { turnToken = t; };

            ErrorDetails errorDetails = null;
            localGameController.ErrorRaisedEvent = (ErrorDetails e) => { errorDetails = e; };

            localGameController.StartGamePlay();

            // Act
            localGameController.BuildCity(turnToken, MainSettlementOneLocation);

            // Assert
            cityBuilt.ShouldBeTrue();
            errorDetails.ShouldBeNull();
        }

        [Test]
        [TestCase(0, 0, "Cannot build city. Missing 2 grain and 3 ore.")]
        [TestCase(1, 1, "Cannot build city. Missing 1 grain and 2 ore.")]
        [TestCase(1, 2, "Cannot build city. Missing 1 grain and 1 ore.")]
        [TestCase(2, 0, "Cannot build city. Missing 3 ore.")]
        [TestCase(0, 3, "Cannot build city. Missing 2 grain.")]
        public void BuildCity_InsufficientResources_MeaningfulErrorIsReceived(Int32 grainCount, Int32 oreCount, String expectedMessage)
        {
            // Arrange
            var testInstances = LocalGameControllerTestCreator.CreateTestInstances(new MockGameBoardWithNoResourcesCollected());
            var localGameController = testInstances.LocalGameController;
            var player = testInstances.MainPlayer;
            LocalGameControllerTestSetup.LaunchGameAndCompleteSetup(localGameController);

            testInstances.Dice.AddSequence(new[] { 8u });

            player.AddResources(new ResourceClutch(0, grainCount, 0, oreCount, 0));

            Boolean cityBuilt = false;
            localGameController.CityBuiltEvent = c => { cityBuilt = true; };

            GameToken turnToken = null;
            localGameController.StartPlayerTurnEvent = (GameToken t) => { turnToken = t; };

            ErrorDetails errorDetails = null;
            localGameController.ErrorRaisedEvent = (ErrorDetails e) => { errorDetails = e; };

            localGameController.StartGamePlay();

            // Act
            localGameController.BuildCity(turnToken, MainSettlementOneLocation);

            // Assert
            cityBuilt.ShouldBeFalse();
            errorDetails.ShouldNotBeNull();
            errorDetails.Message.ShouldBe(expectedMessage);
        }

        [Test]
        public void BuildCity_AllCitiesAreBuilt_MeaningfulErrorIsReceived()
        {
            // Arrange
            MockDice mockDice = null;
            MockPlayer player;
            MockComputerPlayer firstOpponent, secondOpponent, thirdOpponent;
            var localGameController = this.CreateLocalGameControllerAndCompleteGameSetup(out mockDice, out player, out firstOpponent, out secondOpponent, out thirdOpponent);

            mockDice.AddSequence(new[] { 8u });
            player.AddResources(ResourceClutch.RoadSegment * 4);
            player.AddResources(ResourceClutch.Settlement * 3);
            player.AddResources(ResourceClutch.City * 5);

            GameToken turnToken = null;
            localGameController.StartPlayerTurnEvent = (GameToken t) => { turnToken = t; };

            ErrorDetails errorDetails = null;
            localGameController.ErrorRaisedEvent = (ErrorDetails e) =>
            {
                if (errorDetails != null)
                {
                    throw new Exception("Error already raised: " + errorDetails.Message);
                }

                errorDetails = e;
            };

            localGameController.StartGamePlay();
            localGameController.BuildCity(turnToken, MainSettlementOneLocation);
            localGameController.BuildCity(turnToken, MainSettlementTwoLocation);
            localGameController.BuildRoadSegment(turnToken, 4, 3);
            localGameController.BuildRoadSegment(turnToken, 4, 5);
            localGameController.BuildSettlement(turnToken, 3);
            localGameController.BuildCity(turnToken, 3);
            localGameController.BuildSettlement(turnToken, 5);
            localGameController.BuildCity(turnToken, 5);
            localGameController.BuildRoadSegment(turnToken, 3, 2);
            localGameController.BuildRoadSegment(turnToken, 2, 1);
            localGameController.BuildSettlement(turnToken, 1);

            // Act
            localGameController.BuildCity(turnToken, 1);

            // Assert
            errorDetails.ShouldNotBeNull();
            errorDetails.Message.ShouldBe("Cannot build city. All cities already built.");
        }

        [Test]
        public void BuildCity_OnExistingCityBelongingToPlayer_MeaningfulErrorIsReceived()
        {
            // Arrange
            MockDice mockDice = null;
            MockPlayer player;
            MockComputerPlayer firstOpponent, secondOpponent, thirdOpponent;
            var localGameController = this.CreateLocalGameControllerAndCompleteGameSetup(out mockDice, out player, out firstOpponent, out secondOpponent, out thirdOpponent);

            mockDice.AddSequence(new[] { 8u });
            player.AddResources(ResourceClutch.City * 2);

            GameToken turnToken = null;
            localGameController.StartPlayerTurnEvent = (GameToken t) => { turnToken = t; };

            ErrorDetails errorDetails = null;
            localGameController.ErrorRaisedEvent = (ErrorDetails e) => { errorDetails = e; };

            localGameController.StartGamePlay();
            localGameController.BuildCity(turnToken, MainSettlementOneLocation);

            // Act
            localGameController.BuildCity(turnToken, MainSettlementOneLocation);

            // Assert
            errorDetails.ShouldNotBeNull();
            errorDetails.Message.ShouldBe("Cannot build city. There is already a city at location " + MainSettlementOneLocation + " that belongs to you.");
        }

        [Test]
        public void BuildCity_OnExistingCityBelongingToOpponent_MeaningfulErrorIsReceived()
        {
            // Arrange
            var testInstances = LocalGameControllerTestCreator.CreateTestInstances();
            var localGameController = testInstances.LocalGameController;
            LocalGameControllerTestSetup.LaunchGameAndCompleteSetup(localGameController);
            var player = testInstances.MainPlayer;
            var firstOpponent = testInstances.FirstOpponent;

            testInstances.Dice.AddSequence(new[] { 8u, 8u, 8u, 8u, 8u });
            player.AddResources(ResourceClutch.City);
            firstOpponent.AddResources(ResourceClutch.City);
            firstOpponent.AddBuildCityInstruction(new BuildCityInstruction { Location = FirstSettlementOneLocation });

            GameToken turnToken = null;
            localGameController.StartPlayerTurnEvent = (GameToken t) => { turnToken = t; };

            ErrorDetails errorDetails = null;
            localGameController.ErrorRaisedEvent = (ErrorDetails e) => { errorDetails = e; };

            localGameController.StartGamePlay();
            localGameController.EndTurn(turnToken);

            // Act
            localGameController.BuildCity(turnToken, FirstSettlementOneLocation);

            // Assert
            errorDetails.ShouldNotBeNull();
            errorDetails.Message.ShouldBe("Cannot build city. Location " + FirstSettlementOneLocation + " is owned by player '" + FirstOpponentName + "'.");
        }

        [Test]
        public void BuildCity_OnLocationThatIsEmpty_MeaningfulErrorIsReceived()
        {
            // Arrange
            MockDice mockDice = null;
            MockPlayer player;
            MockComputerPlayer firstOpponent, secondOpponent, thirdOpponent;
            var localGameController = this.CreateLocalGameControllerAndCompleteGameSetup(out mockDice, out player, out firstOpponent, out secondOpponent, out thirdOpponent);

            mockDice.AddSequence(new[] { 8u });
            player.AddResources(ResourceClutch.City);

            GameToken turnToken = null;
            localGameController.StartPlayerTurnEvent = (GameToken t) => { turnToken = t; };

            ErrorDetails errorDetails = null;
            localGameController.ErrorRaisedEvent = (ErrorDetails e) => { errorDetails = e; };

            localGameController.StartGamePlay();

            // Act
            localGameController.BuildCity(turnToken, 0);

            // Assert
            errorDetails.ShouldNotBeNull();
            errorDetails.Message.ShouldBe("Cannot build city. No settlement at location 0.");
        }

        [Test]
        public void BuildCity_OnLocationThatIsNotSettlement_MeaningfulErrorIsReceived()
        {
            // Arrange
            MockDice mockDice = null;
            MockPlayer player;
            MockComputerPlayer firstOpponent, secondOpponent, thirdOpponent;
            var localGameController = this.CreateLocalGameControllerAndCompleteGameSetup(out mockDice, out player, out firstOpponent, out secondOpponent, out thirdOpponent);

            mockDice.AddSequence(new[] { 8u });
            player.AddResources(ResourceClutch.RoadSegment);
            player.AddResources(ResourceClutch.City);

            GameToken turnToken = null;
            localGameController.StartPlayerTurnEvent = (GameToken t) => { turnToken = t; };

            ErrorDetails errorDetails = null;
            localGameController.ErrorRaisedEvent = (ErrorDetails e) => { errorDetails = e; };

            localGameController.StartGamePlay();
            localGameController.BuildRoadSegment(turnToken, 4, 3);

            // Act
            localGameController.BuildCity(turnToken, 3);

            // Assert
            errorDetails.ShouldNotBeNull();
            errorDetails.Message.ShouldBe("Cannot build city. No settlement at location 3.");
        }

        [Test]
        public void BuildCity_TurnTokenNotCorrect_MeaningfulErrorIsReceived()
        {
            // Arrange
            MockDice mockDice = null;
            MockPlayer player;
            MockComputerPlayer firstOpponent, secondOpponent, thirdOpponent;
            var localGameController = this.CreateLocalGameControllerAndCompleteGameSetup(out mockDice, out player, out firstOpponent, out secondOpponent, out thirdOpponent);

            mockDice.AddSequence(new[] { 8u });

            ErrorDetails errorDetails = null;
            localGameController.ErrorRaisedEvent = (ErrorDetails e) => { errorDetails = e; };

            GameToken turnToken = null;
            localGameController.StartPlayerTurnEvent = (GameToken t) => { turnToken = t; };
            localGameController.StartGamePlay();

            // Act
            localGameController.BuildCity(new GameToken(), 3);

            // Assert
            errorDetails.ShouldNotBeNull();
            errorDetails.Message.ShouldBe("Turn token not recognised.");
        }

        [Test]
        public void BuildCity_TurnTokenIsNull_MeaningfulErrorIsReceived()
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
            localGameController.BuildCity(null, 3);

            // Assert
            errorDetails.ShouldNotBeNull();
            errorDetails.Message.ShouldBe("Turn token is null.");
        }

        [Test]
        public void BuildCity_GotNineVictoryPoints_EndOfGameEventRaisedWithPlayerAsWinner()
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
            localGameController.BuildRoadSegment(turnToken, 1u, 0u); // Got 2VP for longest road
            localGameController.BuildRoadSegment(turnToken, 2u, 10u);

            localGameController.BuildSettlement(turnToken, 3u);
            localGameController.BuildSettlement(turnToken, 1u);
            localGameController.BuildSettlement(turnToken, 10u);

            localGameController.BuildCity(turnToken, 12u);
            localGameController.BuildCity(turnToken, 40u);

            // Act
            localGameController.BuildCity(turnToken, 3u);

            // Assert
            winningPlayer.ShouldBe(player.Id);
            player.VictoryPoints.ShouldBe(10u);
        }

        [Test]
        public void BuildCity_GameIsOver_MeaningfulErrorIsReceived()
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

            ErrorDetails errorDetails = null;
            localGameController.ErrorRaisedEvent = (ErrorDetails e) => { errorDetails = e; };

            localGameController.StartGamePlay();
            localGameController.BuildRoadSegment(turnToken, 4u, 3u);
            localGameController.BuildRoadSegment(turnToken, 3u, 2u);
            localGameController.BuildRoadSegment(turnToken, 2u, 1u);
            localGameController.BuildRoadSegment(turnToken, 1u, 0u); // Got 2VP for longest road
            localGameController.BuildRoadSegment(turnToken, 2u, 10u);

            localGameController.BuildSettlement(turnToken, 3u);
            localGameController.BuildSettlement(turnToken, 1u);
            localGameController.BuildSettlement(turnToken, 10u);

            localGameController.BuildCity(turnToken, 3u);
            localGameController.BuildCity(turnToken, 12u);
            localGameController.BuildCity(turnToken, 40u);

            // Act
            localGameController.BuildCity(turnToken, 1);

            // Assert
            errorDetails.ShouldNotBeNull();
            errorDetails.Message.ShouldBe("Cannot build city. Game is over.");
        }
        #endregion
    }
}
