﻿
namespace Jabberwocky.SoC.Library.UnitTests.LocalGameController_Tests
{
    using System;
    using System.Collections.Generic;
    using GameBoards;
    using GameEvents;
    using Jabberwocky.SoC.Library.UnitTests.Extensions;
    using Mock;
    using NSubstitute;
    using NUnit.Framework;
    using Shouldly;

    [TestFixture]
    [Category("All")]
    [Category("LocalGameController")]
    [Category("LocalGameController.TradeWithBank")]
    public class LocalGameController_TradeWithBank_Tests
    {
        #region Methods
        [Test]
        public void TradeWithBank_TurnTokenIsNull_MeaningfulErrorIsReceived()
        {
            // Arrange
            var testInstances = this.TestSetup();
            var localGameController = testInstances.LocalGameController;

            ErrorDetails errorDetails = null;
            localGameController.ErrorRaisedEvent = (ErrorDetails e) => { errorDetails = e; };

            localGameController.StartGamePlay();

            // Act
            localGameController.TradeWithBank(null, ResourceTypes.Grain, 0, ResourceTypes.Brick);

            // Assert
            errorDetails.ShouldNotBeNull();
            errorDetails.Message.ShouldBe("Turn token is null.");
        }

        [Test]
        public void TradeWithBank_TurnTokenNotCorrect_MeaningfulErrorIsReceived()
        {
            // Arrange
            var testInstances = this.TestSetup();
            var localGameController = testInstances.LocalGameController;

            ErrorDetails errorDetails = null;
            localGameController.ErrorRaisedEvent = (ErrorDetails e) => { errorDetails = e; };

            localGameController.StartGamePlay();

            // Act
            localGameController.TradeWithBank(new GameToken(), ResourceTypes.Grain, 0, ResourceTypes.Brick);

            // Assert
            errorDetails.ShouldNotBeNull();
            errorDetails.Message.ShouldBe("Turn token not recognised.");
        }

        [Test]
        [TestCase(4, 1, 0, 0)]
        [TestCase(4, 1, 3, 0)]
        [TestCase(8, 1, 0, 4)]
        [TestCase(8, 1, 3, 4)]
        [TestCase(8, 2, 0, 0)]
        [TestCase(8, 2, 1, 0)]
        public void TradeWithBank_LegitmateResourcesForTransaction_ResourceTransactionCompleted(Int32 brickCount, Int32 receivingCount, Int32 otherCount, Int32 leftOverBrickCount)
        {
            // Arrange
            var bankId = Guid.NewGuid();
            var testInstances = this.TestSetupWithExplictGameBoard(bankId, new MockGameBoardWithNoResourcesCollected());
            var localGameController = testInstances.LocalGameController;

            var paymentResources = ResourceClutch.OneBrick * (receivingCount * 4);
            var requestedResources = ResourceClutch.OneGrain * receivingCount;

            var player = testInstances.MainPlayer;
            player.AddResources(ResourceClutch.OneBrick * brickCount);
            player.AddResources(ResourceClutch.OneWool * otherCount);

            GameToken turnToken = null;
            localGameController.StartPlayerTurnEvent = (GameToken t) => { turnToken = t; };

            ResourceTransactionList resources = null;
            localGameController.ResourcesTransferredEvent = (ResourceTransactionList r) => { resources = r; };

            localGameController.StartGamePlay();

            // Act
            localGameController.TradeWithBank(turnToken, ResourceTypes.Grain, receivingCount, ResourceTypes.Brick);

            // Assert
            resources.ShouldNotBeNull();

            var expected = new ResourceTransactionList();
            expected.Add(new ResourceTransaction(bankId, player.Id, paymentResources));
            expected.Add(new ResourceTransaction(player.Id, bankId, requestedResources));

            resources.ShouldBe(expected);

            player.Resources.Count.ShouldBe(receivingCount + otherCount + leftOverBrickCount);
            player.Resources.BrickCount.ShouldBe(leftOverBrickCount);
            player.Resources.GrainCount.ShouldBe(receivingCount);
            player.Resources.WoolCount.ShouldBe(otherCount);
        }

        [Test]
        [TestCase(3, 1)]
        [TestCase(4, 2)]
        [TestCase(7, 2)]
        public void TradeWithBank_PaymentIsWrong_MeaningfulErrorIsReceived(Int32 paymentCount, Int32 receivingCount)
        {
            // Arrange
            var bankId = Guid.NewGuid();
            var testInstances = this.TestSetupWithExplictGameBoard(bankId, new MockGameBoardWithNoResourcesCollected());
            var localGameController = testInstances.LocalGameController;
            var player = testInstances.MainPlayer;

            player.AddResources(ResourceClutch.OneBrick * paymentCount);

            GameToken turnToken = null;
            localGameController.StartPlayerTurnEvent = (GameToken t) => { turnToken = t; };

            localGameController.ResourcesTransferredEvent = (ResourceTransactionList r) => { throw new Exception("ResourcesTransferredEvent should not be called."); };

            ErrorDetails errorDetails = null;
            localGameController.ErrorRaisedEvent = (ErrorDetails e) => { errorDetails = e; };

            localGameController.StartGamePlay();

            // Act
            localGameController.TradeWithBank(turnToken, ResourceTypes.Grain, receivingCount, ResourceTypes.Brick);

            // Assert
            errorDetails.ShouldNotBeNull();
            errorDetails.Message.ShouldBe("Cannot complete trade with bank: Need to pay " + (receivingCount * 4) + " brick for " + receivingCount + " grain. Only paying " + paymentCount + ".");
        }

        [Test]
        [TestCase(0)]
        [TestCase(-1)]
        public void TradeWithBank_RequestedCountIsWrong_MeaningfulErrorIsReceived(Int32 receivingCount)
        {
            // Arrange
            var testInstances = this.TestSetup();
            var localGameController = testInstances.LocalGameController;
            var player = testInstances.MainPlayer;

            player.AddResources(ResourceClutch.OneBrick * 4);

            GameToken turnToken = null;
            localGameController.StartPlayerTurnEvent = (GameToken t) => { turnToken = t; };

            localGameController.ResourcesTransferredEvent = (ResourceTransactionList r) => { throw new Exception("ResourcesTransferredEvent should not be called."); };

            ErrorDetails errorDetails = null;
            localGameController.ErrorRaisedEvent = (ErrorDetails e) => { errorDetails = e; };

            localGameController.StartGamePlay();

            // Act
            localGameController.TradeWithBank(turnToken, ResourceTypes.Grain, receivingCount, ResourceTypes.Brick);

            // Assert
            errorDetails.ShouldNotBeNull();
            errorDetails.Message.ShouldBe("Cannot complete trade with bank: Receiving count must be positive. Was " + receivingCount + ".");
        }

        [Test]
        public void TradeWithBank_GameIsOver_MeaningfulErrorIsReceived()
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
            player.AddResources(ResourceClutch.OneBrick * 4);

            GameToken turnToken = null;
            localGameController.StartPlayerTurnEvent = (GameToken t) => { turnToken = t; };

            ErrorDetails errorDetails = null;
            localGameController.ErrorRaisedEvent = (ErrorDetails e) => { errorDetails = e; };

            localGameController.StartGamePlay();
            localGameController.BuildRoadSegment(turnToken, 4u, 3u);
            localGameController.BuildRoadSegment(turnToken, 3u, 2u);
            localGameController.BuildRoadSegment(turnToken, 2u, 1u);
            localGameController.BuildRoadSegment(turnToken, 1u, 0u); // Got 2VP for longest road (4VP)
            localGameController.BuildRoadSegment(turnToken, 2u, 10u);

            localGameController.BuildSettlement(turnToken, 3);
            localGameController.BuildSettlement(turnToken, 10);

            localGameController.BuildCity(turnToken, 3);
            localGameController.BuildCity(turnToken, 10);
            localGameController.BuildCity(turnToken, 12);
            localGameController.BuildCity(turnToken, 40); // Got 10VP, Game over event raised

            // Act
            localGameController.TradeWithBank(turnToken, ResourceTypes.Grain, 1, ResourceTypes.Brick);

            // Assert
            errorDetails.ShouldNotBeNull();
            errorDetails.Message.ShouldBe("Cannot trade with bank. Game is over.");
        }

        [Test]
        public void Scenario_OpponentTradesWithBank()
        {
            // Arrange
            var bankId = Guid.NewGuid();
            var testInstances = this.TestSetupWithExplictGameBoard(bankId, new MockGameBoardWithNoResourcesCollected());
            var localGameController = testInstances.LocalGameController;

            testInstances.Dice.AddSequence(new uint[] { 8, 8, 8, 8 });

            var givingResources = ResourceClutch.OneGrain * 4;
            var firstOpponent = testInstances.FirstOpponent;
            firstOpponent.AddResources(givingResources);

            var tradeWithBankAction = new TradeWithBankInstruction { GivingType = ResourceTypes.Grain, ReceivingCount = 1, ReceivingType = ResourceTypes.Wool };
            firstOpponent.AddTradeWithBankInstruction(tradeWithBankAction).EndTurn();

            GameToken turnToken = null;
            localGameController.StartPlayerTurnEvent = (GameToken t) => { turnToken = t; };

            var gameEvents = new List<List<GameEvent>>();
            localGameController.GameEvents = (List<GameEvent> e) => { gameEvents.Add(e); };

            localGameController.StartGamePlay();

            // Act
            localGameController.EndTurn(turnToken);

            // Assert
            var expectedTradeWithBankEvent = new TradeWithBankEvent(firstOpponent.Id, bankId, givingResources, ResourceClutch.OneWool);
            gameEvents.Count.ShouldBe(8);
            gameEvents[2].Count.ShouldBe(2);
            gameEvents[2][1].ShouldBe(expectedTradeWithBankEvent);

            firstOpponent.Resources.Count.ShouldBe(1);
            firstOpponent.Resources.WoolCount.ShouldBe(1);
        }

        private LocalGameControllerTestCreator.TestInstances TestSetup()
        {
            var testInstances = LocalGameControllerTestCreator.CreateTestInstances(null, null, null, new GameBoard(BoardSizes.Standard));
            var localGameController = testInstances.LocalGameController;
            LocalGameControllerTestSetup.LaunchGameAndCompleteSetup(localGameController);

            testInstances.Dice.AddSequence(new[] { 8u }); // First turn roll i.e. no robber triggered

            return testInstances;
        }

        private LocalGameControllerTestCreator.TestInstances TestSetupWithExplictGameBoard(Guid bankId, GameBoard gameBoard)
        {
            MockPlayer player;
            MockComputerPlayer firstOpponent, secondOpponent, thirdOpponent;
            LocalGameControllerTestCreator.CreateDefaultPlayerInstances(out player, out firstOpponent, out secondOpponent, out thirdOpponent);
            var playerPool = LocalGameControllerTestCreator.CreateMockPlayerPool(player, firstOpponent, secondOpponent, thirdOpponent);
            playerPool.GetBankId().Returns(bankId);

            var playerSetup = new LocalGameControllerTestCreator.PlayerSetup(player, firstOpponent, secondOpponent, thirdOpponent, playerPool);

            var testInstances = LocalGameControllerTestCreator.CreateTestInstances(null, playerSetup, null, gameBoard);
            var localGameController = testInstances.LocalGameController;
            LocalGameControllerTestSetup.LaunchGameAndCompleteSetup(localGameController);

            testInstances.Dice.AddSequence(new[] { 8u }); // First turn roll i.e. no robber triggered

            return testInstances;
        }
        #endregion
    }
}
