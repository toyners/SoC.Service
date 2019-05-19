﻿
namespace SoC.Library.ScenarioTests
{
    using System;
    using Jabberwocky.SoC.Library;
    using Jabberwocky.SoC.Library.GameBoards;
    using Jabberwocky.SoC.Library.GameEvents;
    using Jabberwocky.SoC.Library.Interfaces;

    public class Scenarios
    {
        const string Adam = "Adam";
        const string Babara = "Barbara";
        const string Charlie = "Charlie";
        const string Dana = "Dana";

        const uint Adam_FirstSettlementLocation = 12u;
        const uint Babara_FirstSettlementLocation = 18u;
        const uint Charlie_FirstSettlementLocation = 25u;
        const uint Dana_FirstSettlementLocation = 31u;

        const uint Dana_SecondSettlementLocation = 33u;
        const uint Charlie_SecondSettlementLocation = 35u;
        const uint Babara_SecondSettlementLocation = 43u;
        const uint Adam_SecondSettlementLocation = 40u;

        const uint Adam_FirstRoadEndLocation = 4;
        const uint Babara_FirstRoadEndLocation = 17;
        const uint Charlie_FirstRoadEndLocation = 15;
        const uint Dana_FirstRoadEndLocation = 30;

        const uint Dana_SecondRoadEndLocation = 32;
        const uint Charlie_SecondRoadEndLocation = 24;
        const uint Babara_SecondRoadEndLocation = 44;
        const uint Adam_SecondRoadEndLocation = 39;

        [Scenario]
        public void AllPlayersCollectResourcesAsPartOfGameSetup(string[] args)
        {
            throw new NotImplementedException();
        }

        [Scenario]
        public void AllPlayersCollectResourcesAsPartOfTurnStart(string[] args)
        {
            var firstTurnCollectedResources = CreateExpectedCollectedResources()
                .Add(Adam, Adam_FirstSettlementLocation, ResourceClutch.OneBrick)
                .Add(Babara, Babara_SecondSettlementLocation, ResourceClutch.OneGrain)
                .Build();

            var secondTurnCollectedResources = CreateExpectedCollectedResources()
                .Add(Babara, Babara_FirstSettlementLocation, ResourceClutch.OneOre)
                .Add(Charlie, Charlie_FirstSettlementLocation, ResourceClutch.OneLumber)
                .Add(Charlie, Charlie_SecondSettlementLocation, ResourceClutch.OneLumber)
                .Add(Dana, Dana_FirstSettlementLocation, ResourceClutch.OneOre)
                .Build();

            var thirdTurnCollectedResources = CreateExpectedCollectedResources()
                .Add(Charlie, Charlie_SecondSettlementLocation, ResourceClutch.OneOre)
                .Build();

            var fourTurnCollectedResources = CreateExpectedCollectedResources()
                .Add(Adam, Adam_FirstSettlementLocation, ResourceClutch.OneWool)
                .Add(Babara, Babara_SecondSettlementLocation, ResourceClutch.OneWool)
                .Build();

            this.CompletePlayerInfrastructureSetup(args)
                .WhenPlayer(Adam)
                    .ReceivesDiceRollEvent(4, 4).ThenDoNothing()
                    .ReceivesResourceCollectedEvent(firstTurnCollectedResources)
                    .ThenVerifyPlayerState().WithResources(ResourceClutch.OneBrick).EndPlayerStateMeasuring()
                    .ThenEndTurn()
                .WhenPlayer(Babara)
                    .ReceivesResourceCollectedEvent(firstTurnCollectedResources)
                    .ThenVerifyPlayerState().WithResources(ResourceClutch.OneGrain).EndPlayerStateMeasuring()
                .WhenPlayer(Charlie)
                    .ReceivesResourceCollectedEvent(firstTurnCollectedResources)
                    .ThenVerifyPlayerState().WithResources(ResourceClutch.Zero).EndPlayerStateMeasuring()
                .WhenPlayer(Dana)
                    .ReceivesResourceCollectedEvent(firstTurnCollectedResources)
                    .ThenVerifyPlayerState().WithResources(ResourceClutch.Zero).EndPlayerStateMeasuring()

                .WhenPlayer(Babara)
                    .ReceivesDiceRollEvent(3, 3).ThenDoNothing()
                    .ReceivesResourceCollectedEvent(secondTurnCollectedResources)
                    .ThenVerifyPlayerState().WithResources(ResourceClutch.OneGrain + ResourceClutch.OneOre).EndPlayerStateMeasuring()
                    .ThenEndTurn()
                .WhenPlayer(Adam)
                    .ReceivesResourceCollectedEvent(secondTurnCollectedResources)
                    .ThenVerifyPlayerState().WithResources(ResourceClutch.OneBrick).EndPlayerStateMeasuring()
                .WhenPlayer(Charlie)
                    .ReceivesResourceCollectedEvent(secondTurnCollectedResources)
                    .ThenVerifyPlayerState().WithResources(ResourceClutch.OneLumber * 2).EndPlayerStateMeasuring()
                .WhenPlayer(Dana)
                    .ReceivesResourceCollectedEvent(secondTurnCollectedResources)
                    .ThenVerifyPlayerState().WithResources(ResourceClutch.OneOre).EndPlayerStateMeasuring()

                .WhenPlayer(Charlie)
                    .ReceivesDiceRollEvent(1, 2).ThenDoNothing()
                    .ReceivesResourceCollectedEvent(thirdTurnCollectedResources)
                    .ThenVerifyPlayerState().WithResources((ResourceClutch.OneLumber * 2) + ResourceClutch.OneOre).EndPlayerStateMeasuring()
                    .ThenEndTurn()
                .WhenPlayer(Adam)
                    .ReceivesResourceCollectedEvent(thirdTurnCollectedResources)
                    .ThenVerifyPlayerState().WithResources(ResourceClutch.OneBrick).EndPlayerStateMeasuring()
                .WhenPlayer(Babara)
                    .ReceivesResourceCollectedEvent(thirdTurnCollectedResources)
                    .ThenVerifyPlayerState().WithResources(ResourceClutch.OneGrain + ResourceClutch.OneOre).EndPlayerStateMeasuring()
                .WhenPlayer(Dana)
                    .ReceivesResourceCollectedEvent(thirdTurnCollectedResources)
                    .ThenVerifyPlayerState().WithResources(ResourceClutch.OneOre).EndPlayerStateMeasuring()

                .WhenPlayer(Dana)
                    .ReceivesDiceRollEvent(6, 4).ThenDoNothing()
                    .ReceivesResourceCollectedEvent(fourTurnCollectedResources)
                    .ThenVerifyPlayerState().WithResources(ResourceClutch.OneOre).EndPlayerStateMeasuring()
                    .ThenEndTurn()
                .WhenPlayer(Adam)
                    .ReceivesResourceCollectedEvent(fourTurnCollectedResources)
                    .ThenVerifyPlayerState().WithResources(ResourceClutch.OneBrick + ResourceClutch.OneWool).EndPlayerStateMeasuring()
                .WhenPlayer(Babara)
                    .ReceivesResourceCollectedEvent(fourTurnCollectedResources)
                    .ThenVerifyPlayerState().WithResources(ResourceClutch.OneGrain + ResourceClutch.OneOre + ResourceClutch.OneWool).EndPlayerStateMeasuring()
                .WhenPlayer(Charlie)
                    .ReceivesResourceCollectedEvent(fourTurnCollectedResources)
                    .ThenVerifyPlayerState().WithResources((ResourceClutch.OneLumber * 2) + ResourceClutch.OneOre).EndPlayerStateMeasuring()

                .WhenPlayer(Adam)
                    .ReceivesDiceRollEvent(1, 1)
                    .ThenQuitGame()
                .WhenPlayer(Babara)
                    .ReceivesDiceRollEvent(1, 1)
                    .ThenQuitGame()
                .WhenPlayer(Charlie)
                    .ReceivesDiceRollEvent(1, 1)
                    .ThenQuitGame()
                .Run();
        }

        [Scenario]
        public void AllPlayersCompleteSetup(string[] args)
        {
            var expectedGameBoardSetup = new GameBoardSetup(new GameBoard(BoardSizes.Standard));
            var playerOrder = new[] { Adam, Babara, Charlie, Dana };
            ScenarioRunner.CreateScenarioRunner(args)
                .WithPlayer(Adam)
                .WithPlayer(Babara)
                .WithPlayer(Charlie)
                .WithPlayer(Dana)
                .WithTurnOrder(playerOrder)
                .WhenPlayer(Adam)
                    .ReceivesGameJoinedEvent().ThenDoNothing()
                    .ReceivesPlayerSetupEvent().ThenDoNothing()
                    .ReceivesInitialBoardSetupEvent(expectedGameBoardSetup).ThenDoNothing()
                    .ReceivesPlayerOrderEvent(playerOrder)
                .WhenPlayer(Babara)
                    .ReceivesGameJoinedEvent()
                    .ReceivesPlayerSetupEvent()
                    .ReceivesInitialBoardSetupEvent(expectedGameBoardSetup)
                    .ReceivesPlayerOrderEvent(playerOrder)
                .WhenPlayer(Charlie)
                    .ReceivesGameJoinedEvent()
                    .ReceivesPlayerSetupEvent()
                    .ReceivesInitialBoardSetupEvent(expectedGameBoardSetup)
                    .ReceivesPlayerOrderEvent(playerOrder)
                .WhenPlayer(Dana)
                    .ReceivesGameJoinedEvent()
                    .ReceivesPlayerSetupEvent()
                    .ReceivesInitialBoardSetupEvent(expectedGameBoardSetup)
                    .ReceivesPlayerOrderEvent(playerOrder)
                .WhenPlayer(Adam)
                    .ReceivesPlaceInfrastructureSetupEvent()
                    .ThenPlaceStartingInfrastructure(Adam_FirstSettlementLocation, Adam_FirstRoadEndLocation)
                    .VerifyAllPlayersReceivedInfrastructurePlacedEvent(Adam, Adam_FirstSettlementLocation, Adam_FirstRoadEndLocation)
                .WhenPlayer(Babara)
                    .ReceivesPlaceInfrastructureSetupEvent()
                    .ThenPlaceStartingInfrastructure(Babara_FirstSettlementLocation, Babara_FirstRoadEndLocation)
                    .VerifyAllPlayersReceivedInfrastructurePlacedEvent(Babara, Babara_FirstSettlementLocation, Babara_FirstRoadEndLocation)
                .WhenPlayer(Charlie)
                    .ReceivesPlaceInfrastructureSetupEvent()
                    .ThenPlaceStartingInfrastructure(Charlie_FirstSettlementLocation, Charlie_FirstRoadEndLocation)
                    .VerifyAllPlayersReceivedInfrastructurePlacedEvent(Charlie, Charlie_FirstSettlementLocation, Charlie_FirstRoadEndLocation)
                .WhenPlayer(Dana)
                    .ReceivesPlaceInfrastructureSetupEvent()
                    .ThenPlaceStartingInfrastructure(Dana_FirstSettlementLocation, Dana_FirstRoadEndLocation)
                    .VerifyAllPlayersReceivedInfrastructurePlacedEvent(Dana, Dana_FirstSettlementLocation, Dana_FirstRoadEndLocation)
                    .ReceivesPlaceInfrastructureSetupEvent()
                    .ThenPlaceStartingInfrastructure(Dana_SecondSettlementLocation, Dana_SecondRoadEndLocation)
                    .VerifyAllPlayersReceivedInfrastructurePlacedEvent(Dana, Dana_SecondSettlementLocation, Dana_SecondRoadEndLocation)
                .WhenPlayer(Charlie)
                    .ReceivesPlaceInfrastructureSetupEvent()
                    .ThenPlaceStartingInfrastructure(Charlie_SecondSettlementLocation, Charlie_SecondRoadEndLocation)
                    .VerifyAllPlayersReceivedInfrastructurePlacedEvent(Charlie, Charlie_SecondSettlementLocation, Charlie_SecondRoadEndLocation)
                .WhenPlayer(Babara)
                    .ReceivesPlaceInfrastructureSetupEvent()
                    .ThenPlaceStartingInfrastructure(Babara_SecondSettlementLocation, Babara_SecondRoadEndLocation)
                    .VerifyAllPlayersReceivedInfrastructurePlacedEvent(Babara, Babara_SecondSettlementLocation, Babara_SecondRoadEndLocation)
                .WhenPlayer(Adam)
                    .ReceivesPlaceInfrastructureSetupEvent()
                    .ThenPlaceStartingInfrastructure(Adam_SecondSettlementLocation, Adam_SecondRoadEndLocation)
                    .VerifyAllPlayersReceivedInfrastructurePlacedEvent(Adam, Adam_SecondSettlementLocation, Adam_SecondRoadEndLocation)
                    .ReceivesConfirmGameStartEvent()
                    .ThenQuitGame()
                .WhenPlayer(Babara)
                    .ReceivesConfirmGameStartEvent()
                    .ThenQuitGame()
                .WhenPlayer(Charlie)
                    .ReceivesConfirmGameStartEvent()
                    .ThenQuitGame()
                .WhenPlayer(Dana)
                    .ReceivesConfirmGameStartEvent()
                    .ThenQuitGame()
                .Run();
        }

        [Scenario]
        public void AllOtherPlayersQuit(string[] args)
        {
            this.CompletePlayerInfrastructureSetup(args)
                .WithNoResourceCollection()
                .WhenPlayer(Adam)
                    .ReceivesDiceRollEvent(3, 3).ThenEndTurn()
                    .ReceivesPlayerQuitEvent(Babara).ThenDoNothing()
                    .ReceivesPlayerQuitEvent(Charlie).ThenDoNothing()
                    .ReceivesPlayerQuitEvent(Dana).ThenDoNothing()
                    .ReceivesPlayerWonEvent(Adam, 2).ThenDoNothing()
                .WhenPlayer(Babara)
                    .ReceivesDiceRollEvent(3, 3).ThenQuitGame()
                .WhenPlayer(Charlie)
                    .ReceivesPlayerQuitEvent(Babara).ThenDoNothing()
                    .ReceivesDiceRollEvent(3, 3).ThenQuitGame()
                .WhenPlayer(Dana)
                    .ReceivesPlayerQuitEvent(Babara).ThenDoNothing()
                    .ReceivesPlayerQuitEvent(Charlie).ThenDoNothing()
                    .ReceivesDiceRollEvent(3, 3).ThenQuitGame()
                .VerifyPlayer(Babara)
                    .DidNotReceiveEvent<GameWinEvent>()
                    .DidNotReceivePlayerQuitEvent(Babara)
                    .DidNotReceivePlayerQuitEvent(Charlie)
                    .DidNotReceivePlayerQuitEvent(Dana)
                .VerifyPlayer(Charlie)
                    .DidNotReceiveEvent<GameWinEvent>()
                    .DidNotReceivePlayerQuitEvent(Charlie)
                    .DidNotReceivePlayerQuitEvent(Dana)
                .VerifyPlayer(Dana)
                    .DidNotReceiveEvent<GameWinEvent>()
                    .DidNotReceivePlayerQuitEvent(Dana)
                .Run();
        }

        [Scenario]
        public void PlayerBuildsCity(string[] args)
        {
            throw new NotImplementedException();
        }

        [Scenario]
        public void PlayerBuildsCityAndWins(string[] args)
        {
            throw new NotImplementedException();
        }

        [Scenario]
        public void PlayerBuildsSettlement(string[] args)
        {
            throw new NotImplementedException();
        }

        [Scenario]
        public void PlayerBuildsSettlementAndWins(string[] args)
        {
            this.CompletePlayerInfrastructureSetup(args)
                .WithNoResourceCollection()
                .WithInitialPlayerSetupFor(Adam, Resources(ResourceClutch.Settlement), VictoryPoints(9))
                .WithInitialActionsFor(Adam, null)
                .WithPlayer(Adam)
                .Run();
        }

        [Scenario]
        public void PlayerPlacesRoad(string[] args)
        {
            this.CompletePlayerInfrastructureSetup(args)
                .WithNoResourceCollection()
                .WithInitialPlayerSetupFor(Adam, Resources(ResourceClutch.RoadSegment))
                .WhenPlayer(Adam)
                    .ReceivesDiceRollEvent(3, 3)
                    .ThenPlaceRoadSegment(4, 3)
                .VerifyAllPlayersReceiveRoadSegmentPlacedEvent(Adam, 4, 3)
                .Run();
        }

        [Scenario]
        public void PlayerPlaysKnightCard(string[] args)
        {
            throw new NotImplementedException();
        }

        [Scenario]
        public void PlayerRollsSeven(string[] args)
        {
            throw new NotImplementedException();
        }

        [Scenario]
        public void PlayerRollsSevenAndAllPlayersWithMoreThanSevenResourcesLoseResources(string[] args)
        {
            throw new NotImplementedException();
        }

        [Scenario]
        public void PlayerRollsSevenAndSelectedHexHasNoPlayers(string[] args)
        {
            throw new NotImplementedException();
        }

        [Scenario]
        public void PlayerRollsSevenAndSelectedHexHasOnePlayer(string[] args)
        {
            throw new NotImplementedException();
        }

        [Scenario]
        public void PlayerRollsSevenAndGetsResourceFromSelectedPlayer(string[] args)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Passing in an id of a player that is not on the selected robber hex when choosing the resource 
        /// causes an error to be raised.
        /// </summary>
        [Scenario]
        public void PlayerRollsSevenAndSelectsInvalidPlayer(string[] args)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// The robber hex set by the player has only player settlements so calling the CallingChooseResourceFromOpponent 
        /// method raises an error
        /// </summary>
        [Scenario]
        public void PlayerRollsSevenAndHasNoCompetitorsOnSelectedHex(string[] args)
        {
            throw new NotImplementedException();
        }

        [Scenario]
        public void PlayerQuitsDuringFirstRoundOfGameSetup(string[] args)
        {
            var expectedGameBoardSetup = new GameBoardSetup(new GameBoard(BoardSizes.Standard));
            var playerOrder = new[] { Adam, Babara, Charlie, Dana };
            ScenarioRunner.CreateScenarioRunner(args)
                .WithPlayer(Adam).WithPlayer(Babara).WithPlayer(Charlie).WithPlayer(Dana)
                .WithTurnOrder(playerOrder)
                .WhenPlayer(Adam)
                    .ReceivesPlaceInfrastructureSetupEvent().ThenQuitGame()
                .WhenPlayer(Babara)
                    .ReceivesPlayerQuitEvent(Adam).ThenDoNothing()
                .WhenPlayer(Charlie)
                    .ReceivesPlayerQuitEvent(Adam).ThenDoNothing()
                .WhenPlayer(Dana)
                    .ReceivesPlayerQuitEvent(Adam).ThenDoNothing()
                .Run();
        }

        [Scenario]
        public void PlayerQuitsDuringSecondRoundOfGameSetup(string[] args)
        {
            var expectedGameBoardSetup = new GameBoardSetup(new GameBoard(BoardSizes.Standard));
            var playerOrder = new[] { Adam, Babara, Charlie, Dana };
            ScenarioRunner.CreateScenarioRunner(args)
                .WithPlayer(Adam).WithPlayer(Babara).WithPlayer(Charlie).WithPlayer(Dana)
                .WithTurnOrder(playerOrder)
                .WhenPlayer(Adam)
                    .ReceivesPlaceInfrastructureSetupEvent().ThenPlaceStartingInfrastructure(Adam_FirstSettlementLocation, Adam_FirstRoadEndLocation)
                    .ReceivesPlaceInfrastructureSetupEvent().ThenQuitGame()
                .WhenPlayer(Babara)
                    .ReceivesPlaceInfrastructureSetupEvent().ThenPlaceStartingInfrastructure(Babara_FirstSettlementLocation, Babara_FirstRoadEndLocation)
                    .ReceivesPlaceInfrastructureSetupEvent().ThenPlaceStartingInfrastructure(Babara_SecondSettlementLocation, Babara_SecondRoadEndLocation)
                    .ReceivesPlayerQuitEvent(Adam).ThenDoNothing()
                .WhenPlayer(Charlie)
                    .ReceivesPlaceInfrastructureSetupEvent().ThenPlaceStartingInfrastructure(Charlie_FirstSettlementLocation, Charlie_FirstRoadEndLocation)
                    .ReceivesPlaceInfrastructureSetupEvent().ThenPlaceStartingInfrastructure(Charlie_SecondSettlementLocation, Charlie_SecondRoadEndLocation)
                    .ReceivesPlayerQuitEvent(Adam).ThenDoNothing()
                .WhenPlayer(Dana)
                    .ReceivesPlaceInfrastructureSetupEvent().ThenPlaceStartingInfrastructure(Dana_FirstSettlementLocation, Dana_FirstRoadEndLocation)
                    .ReceivesPlaceInfrastructureSetupEvent().ThenPlaceStartingInfrastructure(Dana_SecondSettlementLocation, Dana_SecondRoadEndLocation)
                    .ReceivesPlayerQuitEvent(Adam).ThenDoNothing()
                .VerifyPlayer(Adam)
                    .DidNotReceiveEvent<ConfirmGameStartEvent>()
                .Run();
        }

        [Scenario]
        public void PlayerSendsIncorrectCommandDuringGameStartConfirmation(string[] args)
        {
            var expectedGameBoardSetup = new GameBoardSetup(new GameBoard(BoardSizes.Standard));
            var playerOrder = new[] { Adam, Babara, Charlie, Dana };
            ScenarioRunner.CreateScenarioRunner(args)
                .WithPlayer(Adam).WithPlayer(Babara).WithPlayer(Charlie).WithPlayer(Dana)
                .WithTurnOrder(playerOrder)
                .WhenPlayer(Adam)
                    .ReceivesPlaceInfrastructureSetupEvent()
                    .ThenPlaceStartingInfrastructure(Adam_FirstSettlementLocation, Adam_FirstRoadEndLocation)
                    .ReceivesPlaceInfrastructureSetupEvent()
                    .ThenPlaceStartingInfrastructure(Adam_SecondSettlementLocation, Adam_SecondRoadEndLocation)
                    .ReceivesConfirmGameStartEvent()
                    .ThenEndTurn()
                    .ReceivesGameErrorEvent("302", "Invalid action: Expected ConfirmGameStartAction or QuitGameAction")
                .WhenPlayer(Babara)
                    .ReceivesPlaceInfrastructureSetupEvent()
                    .ThenPlaceStartingInfrastructure(Babara_FirstSettlementLocation, Babara_FirstRoadEndLocation)
                    .ReceivesPlaceInfrastructureSetupEvent()
                    .ThenPlaceStartingInfrastructure(Babara_SecondSettlementLocation, Babara_SecondRoadEndLocation)
                .WhenPlayer(Charlie)
                    .ReceivesPlaceInfrastructureSetupEvent()
                    .ThenPlaceStartingInfrastructure(Charlie_FirstSettlementLocation, Charlie_FirstRoadEndLocation)
                    .ReceivesPlaceInfrastructureSetupEvent()
                    .ThenPlaceStartingInfrastructure(Charlie_SecondSettlementLocation, Charlie_SecondRoadEndLocation)
                .WhenPlayer(Dana)
                    .ReceivesPlaceInfrastructureSetupEvent()
                    .ThenPlaceStartingInfrastructure(Dana_FirstSettlementLocation, Dana_FirstRoadEndLocation)
                    .ReceivesPlaceInfrastructureSetupEvent()
                    .ThenPlaceStartingInfrastructure(Dana_SecondSettlementLocation, Dana_SecondRoadEndLocation)
                .Run();
        }

        [Scenario]
        public void PlayerSendsIncorrectCommandDuringGameSetup(string[] args)
        {
            var expectedGameBoardSetup = new GameBoardSetup(new GameBoard(BoardSizes.Standard));
            var playerOrder = new[] { Adam, Babara, Charlie, Dana };
            ScenarioRunner.CreateScenarioRunner(args)
                .WithPlayer(Adam).WithPlayer(Babara).WithPlayer(Charlie).WithPlayer(Dana)
                .WithTurnOrder(playerOrder)
                .WhenPlayer(Adam)
                    .ReceivesPlaceInfrastructureSetupEvent().ThenEndTurn()
                    .ReceivesGameErrorEvent("301", "Invalid action: Expected PlaceSetupInfrastructureAction or QuitGameAction")
                .Run();
        }

        [Scenario]
        public void PlayerTradesOneResourceWithAnotherPlayer(string[] args)
        {
            var adamResources = ResourceClutch.OneWool;
            var babaraResources = ResourceClutch.OneGrain;

            this.CompletePlayerInfrastructureSetup(args)
                .WithNoResourceCollection()
                .WithInitialPlayerSetupFor(Adam, Resources(adamResources))
                .WithInitialPlayerSetupFor(Babara, Resources(babaraResources))
                .WhenPlayer(Adam)
                    .ReceivesDiceRollEvent(3, 3)
                    .ThenEndTurn()
                .WhenPlayer(Babara)
                    .ReceivesDiceRollEvent(3, 3)
                    .ThenMakeDirectTradeOffer(ResourceClutch.OneWool)
                .WhenPlayer(Adam)
                    .ReceivesMakeDirectTradeOfferEvent(Babara, ResourceClutch.OneWool)
                    .ThenAnswerDirectTradeOffer(ResourceClutch.OneGrain)
                .WhenPlayer(Charlie)
                    .ReceivesMakeDirectTradeOfferEvent(Babara, ResourceClutch.OneWool).ThenDoNothing()
                .WhenPlayer(Dana)
                    .ReceivesMakeDirectTradeOfferEvent(Babara, ResourceClutch.OneWool).ThenDoNothing()
                .WhenPlayer(Babara)
                    .ReceivesAnswerDirectTradeOfferEvent(Adam, ResourceClutch.OneGrain)
                    .ThenAcceptTradeOffer(Adam)
                .WhenPlayer(Charlie)
                    .ReceivesAnswerDirectTradeOfferEvent(Adam, ResourceClutch.OneGrain).ThenDoNothing()
                .WhenPlayer(Dana)
                    .ReceivesAnswerDirectTradeOfferEvent(Adam, ResourceClutch.OneGrain).ThenDoNothing()
                .WhenPlayer(Adam)
                    .ReceivesAcceptDirectTradeEvent(Babara, ResourceClutch.OneWool, Adam, ResourceClutch.OneGrain)
                    .ThenVerifyPlayerState()
                        .WithResources(ResourceClutch.OneGrain)
                        .EndPlayerStateMeasuring()
                .WhenPlayer(Babara)
                    .ReceivesAcceptDirectTradeEvent(Babara, ResourceClutch.OneWool, Adam, ResourceClutch.OneGrain)
                    .ThenVerifyPlayerState()
                        .WithResources(ResourceClutch.OneWool)
                        .EndPlayerStateMeasuring()
                .WhenPlayer(Charlie)
                    .ReceivesAcceptDirectTradeEvent(Babara, ResourceClutch.OneWool, Adam, ResourceClutch.OneGrain)
                    .ThenDoNothing()
                .WhenPlayer(Dana)
                    .ReceivesAcceptDirectTradeEvent(Babara, ResourceClutch.OneWool, Adam, ResourceClutch.OneGrain)
                    .ThenDoNothing()
                .Run();
        }

        [Scenario]
        public void PlayerWithEightPointsGainsLargestArmyAndWins(string[] args)
        {
            throw new NotImplementedException();
        }

        [Scenario]
        public void PlayerWithEightPointsGainsLongestRoadAndWins(string[] args)
        {
            throw new NotImplementedException();
        }

        [Scenario]
        public void PlayerWithLargestArmyDoesNotRaiseEventWhenPlayingSubsequentKnight(string[] args)
        {
            throw new NotImplementedException();
        }

        [Scenario]
        public void PlayerWithLargestArmyDoesNotGetMoreVictoryPointsWhenPlayingSubsequentKnight(string[] args)
        {
            throw new NotImplementedException();
        }

        [Scenario]
        public void PlayerWithNinePointsGainsLargestArmyAndWins(string[] args)
        {
            throw new NotImplementedException();
        }

        [Scenario]
        public void PlayerWithNinePointsGainsLongestRoadAndWins(string[] args)
        {
            throw new NotImplementedException();
        }

        private static CollectedResourcesBuilder CreateExpectedCollectedResources()
        {
            return new CollectedResourcesBuilder();
        }

        private ScenarioRunner CompletePlayerInfrastructureSetup(string[] args)
        {
            return ScenarioRunner.CreateScenarioRunner(args)
                .WithPlayer(Adam)
                .WithPlayer(Babara)
                .WithPlayer(Charlie)
                .WithPlayer(Dana)
                .WithTurnOrder(new[] { Adam, Babara, Charlie, Dana })
                .WhenPlayer(Adam)
                    .ReceivesPlaceInfrastructureSetupEvent()
                    .ThenPlaceStartingInfrastructure(Adam_FirstSettlementLocation, Adam_FirstRoadEndLocation)
                .WhenPlayer(Babara)
                    .ReceivesPlaceInfrastructureSetupEvent()
                    .ThenPlaceStartingInfrastructure(Babara_FirstSettlementLocation, Babara_FirstRoadEndLocation)
                .WhenPlayer(Charlie)
                    .ReceivesPlaceInfrastructureSetupEvent()
                    .ThenPlaceStartingInfrastructure(Charlie_FirstSettlementLocation, Charlie_FirstRoadEndLocation)
                .WhenPlayer(Dana)
                    .ReceivesPlaceInfrastructureSetupEvent()
                    .ThenPlaceStartingInfrastructure(Dana_FirstSettlementLocation, Dana_FirstRoadEndLocation)
                    .ReceivesPlaceInfrastructureSetupEvent()
                    .ThenPlaceStartingInfrastructure(Dana_SecondSettlementLocation, Dana_SecondRoadEndLocation)
                .WhenPlayer(Charlie)
                    .ReceivesPlaceInfrastructureSetupEvent()
                    .ThenPlaceStartingInfrastructure(Charlie_SecondSettlementLocation, Charlie_SecondRoadEndLocation)
                .WhenPlayer(Babara)
                    .ReceivesPlaceInfrastructureSetupEvent()
                    .ThenPlaceStartingInfrastructure(Babara_SecondSettlementLocation, Babara_SecondRoadEndLocation)
                .WhenPlayer(Adam)
                    .ReceivesPlaceInfrastructureSetupEvent()
                    .ThenPlaceStartingInfrastructure(Adam_SecondSettlementLocation, Adam_SecondRoadEndLocation)
                    .ReceivesConfirmGameStartEvent()
                    .ThenConfirmGameStart()
                .WhenPlayer(Babara)
                    .ReceivesConfirmGameStartEvent()
                    .ThenConfirmGameStart()
                .WhenPlayer(Charlie)
                    .ReceivesConfirmGameStartEvent()
                    .ThenConfirmGameStart()
                .WhenPlayer(Dana)
                    .ReceivesConfirmGameStartEvent()
                    .ThenConfirmGameStart();
        }

        public static IPlayerSetupActions Resources(ResourceClutch resources) => new ResourceSetup(resources);

        public static IPlayerSetupActions VictoryPoints(uint value) => new VictoryPointSetup(value);
    }

    public class ResourceSetup : IPlayerSetupActions
    {
        private ResourceClutch resources;
        public ResourceSetup(ResourceClutch resources) => this.resources = resources;
        public void Process(IPlayer player) => player.AddResources(this.resources);
    }

    public class VictoryPointSetup : IPlayerSetupActions
    {
        private uint victoryPoints;
        public VictoryPointSetup(uint victoryPoints) => this.victoryPoints = victoryPoints;
        public void Process(IPlayer player) => throw new NotImplementedException();
    }
}
