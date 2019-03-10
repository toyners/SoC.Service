﻿

namespace SoC.Library.ScenarioTests
{
    using Jabberwocky.SoC.Library;

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

        const uint Adam_FirstRoadEnd = 4;
        const uint Babara_FirstRoadEnd = 17;
        const uint Charlie_FirstRoadEnd = 15;
        const uint Dana_FirstRoadEnd = 30;

        const uint Dana_SecondRoadEnd = 32;
        const uint Charlie_SecondRoadEnd = 24;
        const uint Babara_SecondRoadEnd = 44;
        const uint Adam_SecondRoadEnd = 39;

        //[Scenario]
        public void Scenario_AllPlayersCompleteSetup(string[] args)
        {
            this.CompletePlayerSetup(args)
                .WithNoResourceCollection()
                .PlayerSetupEvent()
                .InitialBoardSetupEvent()
                .PlayerInfrastructureSetup(Adam, Adam_FirstSettlementLocation, Adam_FirstRoadEnd)
                .PlayerInfrastructureSetup(Babara, Babara_FirstSettlementLocation, Babara_FirstRoadEnd)
                .PlayerInfrastructureSetup(Charlie, Charlie_FirstSettlementLocation, Charlie_FirstRoadEnd)
                .PlayerInfrastructureSetup(Dana, Dana_FirstSettlementLocation, Dana_FirstRoadEnd)
                .PlayerInfrastructureSetup(Dana, Dana_SecondSettlementLocation, Dana_SecondRoadEnd)
                .PlayerInfrastructureSetup(Charlie, Charlie_SecondSettlementLocation, Charlie_SecondRoadEnd)
                .PlayerInfrastructureSetup(Babara, Babara_SecondSettlementLocation, Babara_SecondRoadEnd)
                .PlayerInfrastructureSetup(Adam, Adam_SecondSettlementLocation, Adam_SecondRoadEnd)
                .Run();
        }

        //[Scenario]
        public void Scenario_PlayerTradesOneResourceWithPlayer(string[] args)
        {
            var adamResources = ResourceClutch.OneWool;
            var babaraResources = ResourceClutch.OneGrain;
            this.CompletePlayerInfrastructureSetup(args)
                .WithNoResourceCollection()
                .WithStartingResourcesForPlayer(Adam, adamResources)
                .WithStartingResourcesForPlayer(Babara, babaraResources)
                .PlayerTurn(Adam, 3, 3).EndTurn()
                .PlayerTurn(Babara, 3, 3)
                    .MakeDirectTradeOffer(ResourceClutch.OneWool)
                    .MakeDirectTradeOfferEvent(Adam, Babara, ResourceClutch.OneWool)
                    .MakeDirectTradeOfferEvent(Charlie, Babara, ResourceClutch.OneWool)
                    .MakeDirectTradeOfferEvent(Dana, Babara, ResourceClutch.OneWool)
                    .AnswerDirectTradeOffer(Adam, ResourceClutch.OneGrain)
                    .AnswerDirectTradeOfferEvent(Babara, Adam, ResourceClutch.OneGrain)
                    .AnswerDirectTradeOfferEvent(Charlie, Adam, ResourceClutch.OneGrain)
                    .AnswerDirectTradeOfferEvent(Dana, Adam, ResourceClutch.OneGrain)
                    .TradeWithPlayerCompletedEvent(Adam, Babara, ResourceClutch.OneWool, Adam, ResourceClutch.OneGrain)
                    .TradeWithPlayerCompletedEvent(Babara, Babara, ResourceClutch.OneWool, Adam, ResourceClutch.OneGrain)
                    .TradeWithPlayerCompletedEvent(Charlie, Babara, ResourceClutch.OneWool, Adam, ResourceClutch.OneGrain)
                    .TradeWithPlayerCompletedEvent(Dana, Babara, ResourceClutch.OneWool, Adam, ResourceClutch.OneGrain)
                    .State(Adam)
                        .Resources(ResourceClutch.OneGrain)
                        .End()
                    .State(Babara)
                        .Resources(ResourceClutch.OneWool)
                        .End()
                    .EndTurn()
                .Run();
        }

        [Scenario]
        public void Scenario_PlayerTradesOneResourceWithPlayer2(string[] args)
        {
            var adamResources = ResourceClutch.OneWool;
            var babaraResources = ResourceClutch.OneGrain;

            ScenarioRunner.CreateScenarioRunner(args)
                .WithPlayer(Adam)
                .WithPlayer(Babara)
                .WithPlayer(Charlie)
                .WithPlayer(Dana)
                .WithTurnOrder(Adam, Babara, Charlie, Dana)
                .WithNoResourceCollection()
                .WithStartingResourcesForPlayer(Adam, adamResources)
                .WithStartingResourcesForPlayer(Babara, babaraResources)
                .Label(Adam, "Round 1 - Adam")
                .WhenDiceRollEvent(Adam, 3, 3)
                .Label(Babara, "Round 1 - Babara")
                .WhenDiceRollEvent(Babara, 3, 3)
                    .MakeDirectTradeOffer(ResourceClutch.OneWool)
                .WhenMakeDirectTradeOfferEvent(Adam, Babara, ResourceClutch.OneWool)
                    .MakeDirectTradeOffer(ResourceClutch.OneGrain)
                
                //.MakeDirectTradeOfferEvent(Charlie, Babara, ResourceClutch.OneWool)
                //.MakeDirectTradeOfferEvent(Dana, Babara, ResourceClutch.OneWool)
                //    .AnswerDirectTradeOffer(Adam, ResourceClutch.OneGrain)
                .WhenAnswerDirectTradeOfferEvent(Babara, Adam, ResourceClutch.OneGrain)
                    .ConfirmDirectTrade()
                //.AnswerDirectTradeOfferEvent(Dana, Adam, ResourceClutch.OneGrain)
                //.TradeWithPlayerCompletedEvent(Adam, Babara, ResourceClutch.OneWool, Adam, ResourceClutch.OneGrain)
                //.TradeWithPlayerCompletedEvent(Babara, Babara, ResourceClutch.OneWool, Adam, ResourceClutch.OneGrain)
                //.TradeWithPlayerCompletedEvent(Charlie, Babara, ResourceClutch.OneWool, Adam, ResourceClutch.OneGrain)
                //.TradeWithPlayerCompletedEvent(Dana, Babara, ResourceClutch.OneWool, Adam, ResourceClutch.OneGrain)
                //.State(Adam)
                //    .Resources(ResourceClutch.OneGrain)
                //    .End()
                //.State(Babara)
                //    .Resources(ResourceClutch.OneWool)
                //    .End()
                .Run();
        }

        private LocalGameControllerScenarioRunner CompletePlayerSetup(string[] args)
        {
            return LocalGameControllerScenarioRunner.LocalGameController(args)
                .WithPlayer(Adam)
                .WithPlayer(Babara)
                .WithPlayer(Charlie)
                .WithPlayer(Dana)
                .WithTurnOrder(Adam, Babara, Charlie, Dana);
        }

        private LocalGameControllerScenarioRunner CompletePlayerInfrastructureSetup(string[] args)
        {
            return LocalGameControllerScenarioRunner.LocalGameController(args)
                .WithPlayer(Adam)
                .WithPlayer(Babara)
                .WithPlayer(Charlie)
                .WithPlayer(Dana)
                .WithTurnOrder(Adam, Babara, Charlie, Dana)
                .PlayerInfrastructureSetup(Adam, Adam_FirstSettlementLocation, Adam_FirstRoadEnd)
                .PlayerInfrastructureSetup(Babara, Babara_FirstSettlementLocation, Babara_FirstRoadEnd)
                .PlayerInfrastructureSetup(Charlie, Charlie_FirstSettlementLocation, Charlie_FirstRoadEnd)
                .PlayerInfrastructureSetup(Dana, Dana_FirstSettlementLocation, Dana_FirstRoadEnd)
                .PlayerInfrastructureSetup(Dana, Dana_SecondSettlementLocation, Dana_SecondRoadEnd)
                .PlayerInfrastructureSetup(Charlie, Charlie_SecondSettlementLocation, Charlie_SecondRoadEnd)
                .PlayerInfrastructureSetup(Babara, Babara_SecondSettlementLocation, Babara_SecondRoadEnd)
                .PlayerInfrastructureSetup(Adam, Adam_SecondSettlementLocation, Adam_SecondRoadEnd);
        }
    }
}
