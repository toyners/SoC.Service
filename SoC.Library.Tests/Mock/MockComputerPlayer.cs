﻿
namespace Jabberwocky.SoC.Library.UnitTests.Mock
{
    using System;
    using System.Collections.Generic;
    using Enums;
    using Interfaces;
    using Jabberwocky.SoC.Library.DevelopmentCards;
    using Jabberwocky.SoC.Library.PlayerActions;
    using Jabberwocky.SoC.Library.PlayerData;

    /// <summary>
    /// Used to set opponent player behaviour for testing purposes
    /// </summary>
    public class MockComputerPlayer : ComputerPlayer
    {
        #region Fields
        public UInt32 HiddenDevelopmentCards;
        public UInt32 ResourceCards;
        public List<DevelopmentCardTypes> DisplayedDevelopmentCards;

        public Queue<UInt32> CityLocations = new Queue<UInt32>();
        private Dictionary<DevelopmentCardTypes, Queue<DevelopmentCard>> developmentCards = new Dictionary<DevelopmentCardTypes, Queue<DevelopmentCard>>();
        private Queue<PlayKnightInstruction> playKnightCardActions = new Queue<PlayKnightInstruction>();
        private Queue<PlayMonopolyCardInstruction> playMonopolyCardActions = new Queue<PlayMonopolyCardInstruction>();
        private Queue<PlayYearOfPlentyCardInstruction> playYearOfPlentyCardActions = new Queue<PlayYearOfPlentyCardInstruction>();
        private Queue<TradeWithBankInstruction> tradeWithBankInstructions = new Queue<TradeWithBankInstruction>();
        private Queue<PlaceRoadSegmentAction> buildRoadSegmentActions = new Queue<PlaceRoadSegmentAction>();
        public Queue<UInt32> SettlementLocations = new Queue<UInt32>();
        public Queue<Tuple<UInt32, UInt32>> InitialInfrastructure = new Queue<Tuple<UInt32, UInt32>>();
        public Queue<ComputerPlayerActionTypes> Actions = new Queue<ComputerPlayerActionTypes>();
        public ResourceClutch DroppedResources;
        #endregion

        #region Construction
        public MockComputerPlayer(String name) : base(name, null, null, null) { }
        #endregion

        #region Methods
        public MockComputerPlayer AddBuildCityInstruction(BuildCityInstruction instruction)
        {
            this.CityLocations.Enqueue(instruction.Location);
            this.Actions.Enqueue(ComputerPlayerActionTypes.BuildCity);
            return this;
        }

        public MockComputerPlayer AddBuildSettlementInstruction(BuildSettlementInstruction instruction)
        {
            this.SettlementLocations.Enqueue(instruction.Location);
            this.Actions.Enqueue(ComputerPlayerActionTypes.BuildSettlement);
            return this;
        }

        public void AddInitialInfrastructureChoices(UInt32 firstSettlmentLocation, UInt32 firstRoadEndLocation, UInt32 secondSettlementLocation, UInt32 secondRoadEndLocation)
        {
            this.InitialInfrastructure.Enqueue(new Tuple<UInt32, UInt32>(firstSettlmentLocation, firstRoadEndLocation));
            this.InitialInfrastructure.Enqueue(new Tuple<UInt32, UInt32>(secondSettlementLocation, secondRoadEndLocation));
        }

        public MockComputerPlayer AddBuyDevelopmentCardChoice(UInt32 count)
        {
            for (; count > 0; count--)
            {
                this.Actions.Enqueue(ComputerPlayerActionTypes.BuyDevelopmentCard);
            }

            return this;
        }

        public override void AddDevelopmentCard(DevelopmentCard developmentCard)
        {
            DevelopmentCardTypes? developmentCardType = null;

            if (developmentCard is KnightDevelopmentCard)
            {
                developmentCardType = DevelopmentCardTypes.Knight;
            }

            if (developmentCard is MonopolyDevelopmentCard)
            {
                developmentCardType = DevelopmentCardTypes.Monopoly;
            }

            if (developmentCard is YearOfPlentyDevelopmentCard)
            {
                developmentCardType = DevelopmentCardTypes.YearOfPlenty;
            }

            if (developmentCardType == null)
            {
                throw new Exception("Development card is not recognised.");
            }

            var key = developmentCardType.Value;
            if (!this.developmentCards.ContainsKey(key))
            {
                var queue = new Queue<DevelopmentCard>();
                queue.Enqueue(developmentCard);
                this.developmentCards.Add(key, queue);
            }
            else
            {
                this.developmentCards[key].Enqueue(developmentCard);
            }
        }

        public MockComputerPlayer AddBuildRoadSegmentInstruction(BuildRoadSegmentInstruction instruction)
        {
            for (var index = 0; index < instruction.Locations.Length; index += 2)
            {
                var startLocation = instruction.Locations[index];
                var endLocation = instruction.Locations[index + 1];

                this.buildRoadSegmentActions.Enqueue(new PlaceRoadSegmentAction(Guid.Empty, startLocation, endLocation));
                this.Actions.Enqueue(ComputerPlayerActionTypes.BuildRoadSegment);
            }

            return this;
        }

        public MockComputerPlayer AddPlaceKnightCardInstruction(PlayKnightInstruction playKnightCardInstruction)
        {
            this.playKnightCardActions.Enqueue(playKnightCardInstruction);
            this.Actions.Enqueue(ComputerPlayerActionTypes.PlayKnightCard);
            return this;
        }

        public MockComputerPlayer AddPlaceMonopolyCardInstruction(PlayMonopolyCardInstruction playMonopolyCardInstruction)
        {
            this.playMonopolyCardActions.Enqueue(playMonopolyCardInstruction);
            this.Actions.Enqueue(ComputerPlayerActionTypes.PlayMonopolyCard);
            return this;
        }

        public MockComputerPlayer AddPlayYearOfPlentyCardInstruction(PlayYearOfPlentyCardInstruction playYearOfPlentyCardInstruction)
        {
            this.playYearOfPlentyCardActions.Enqueue(playYearOfPlentyCardInstruction);
            this.Actions.Enqueue(ComputerPlayerActionTypes.PlayYearOfPlentyCard);
            return this;
        }

        public MockComputerPlayer AddTradeWithBankInstruction(TradeWithBankInstruction tradeWithBankInstruction)
        {
            this.tradeWithBankInstructions.Enqueue(tradeWithBankInstruction);
            this.Actions.Enqueue(ComputerPlayerActionTypes.TradeWithBank);
            return this;
        }

        public override void BuildInitialPlayerActions(PlayerDataModel[] playerData, bool rolledSeven)
        {
            // Nothing to do - all actions set by instruction.
        }

        public override UInt32 ChooseCityLocation()
        {
            return this.CityLocations.Dequeue();
        }

        public override void ChooseInitialInfrastructure(out UInt32 settlementLocation, out UInt32 roadEndLocation)
        {
            var infrastructure = this.InitialInfrastructure.Dequeue();
            settlementLocation = infrastructure.Item1;
            roadEndLocation = infrastructure.Item2;
        }

        public override KnightDevelopmentCard GetKnightCard()
        {
            return this.developmentCards[DevelopmentCardTypes.Knight].Dequeue() as KnightDevelopmentCard;
        }

        public override IPlayer ChoosePlayerToRob(IEnumerable<IPlayer> otherPlayers)
        {
            var action = this.playKnightCardActions.Dequeue();
            foreach (var otherPlayer in otherPlayers)
            {
                if (otherPlayer.Id == action.RobbedPlayerId)
                {
                    return otherPlayer;
                }
            }

            throw new Exception("Cannot find player with Id '" + action.RobbedPlayerId + "' when choosing player to rob.");
        }

        public override MonopolyDevelopmentCard ChooseMonopolyCard()
        {
            return this.developmentCards[DevelopmentCardTypes.Monopoly].Dequeue() as MonopolyDevelopmentCard;
        }

        public override ResourceClutch ChooseResourcesToCollectFromBank()
        {
            var action = this.playYearOfPlentyCardActions.Dequeue();

            var resources = ResourceClutch.Zero;
            foreach (var resourceChoice in new[] { action.FirstResourceChoice, action.SecondResourceChoice })
            {
                switch (resourceChoice)
                {
                    case ResourceTypes.Brick: resources += ResourceClutch.OneBrick; break;
                    case ResourceTypes.Grain: resources += ResourceClutch.OneGrain; break;
                    case ResourceTypes.Lumber: resources += ResourceClutch.OneLumber; break;
                    case ResourceTypes.Ore: resources += ResourceClutch.OneOre; break;
                    case ResourceTypes.Wool: resources += ResourceClutch.OneWool; break;
                }
            }

            return resources;
        }

        public override ResourceClutch ChooseResourcesToDrop()
        {
            return this.DroppedResources;
        }

        public override ResourceTypes ChooseResourceTypeToRob()
        {
            var action = this.playMonopolyCardActions.Dequeue();
            return action.ResourceType;
        }

        public override uint ChooseRobberLocation()
        {
            return this.playKnightCardActions.Peek().RobberHex;
        }

        public override UInt32 ChooseSettlementLocation()
        {
            return this.SettlementLocations.Dequeue();
        }

        public override YearOfPlentyDevelopmentCard ChooseYearOfPlentyCard()
        {
            return this.developmentCards[DevelopmentCardTypes.YearOfPlenty].Dequeue() as YearOfPlentyDevelopmentCard;
        }

        public MockComputerPlayer EndTurn()
        {
            this.Actions.Enqueue(ComputerPlayerActionTypes.EndTurn);
            return this;
        }

        public override PlayerAction GetPlayerAction()
        {
            if (this.Actions.Count == 0)
            {
                return null;
            }

            var actionType = this.Actions.Dequeue();
            if (actionType == ComputerPlayerActionTypes.EndTurn)
            {
                return null;
            }

            PlayerAction playerAction = null;
            switch (actionType)
            {
                case ComputerPlayerActionTypes.BuildRoadSegment:
                    {
                        playerAction = this.buildRoadSegmentActions.Dequeue();
                        break;
                    }

                case ComputerPlayerActionTypes.TradeWithBank:
                    {
                        var tradeWithBankInstruction = this.tradeWithBankInstructions.Dequeue();
                        playerAction = new TradeWithBankAction(
                          tradeWithBankInstruction.GivingType,
                          tradeWithBankInstruction.ReceivingType,
                          tradeWithBankInstruction.ReceivingCount);
                        break;
                    }

                default:
                    {
                        //playerAction = new PlayerAction(actionType);
                        break;
                    }
            }

            return playerAction;
        }

        private List<DevelopmentCardTypes> CreateListOfDisplayedDevelopmentCards()
        {
            // TODO: Use Jabberwocky
            if (this.DisplayedDevelopmentCards == null || this.DisplayedDevelopmentCards.Count == 0)
            {
                return null;
            }

            return new List<DevelopmentCardTypes>(this.DisplayedDevelopmentCards);
        }
        #endregion
    }

    public struct PlayKnightInstruction
    {
        public UInt32 RobberHex;
        public Guid RobbedPlayerId;
    }

    public struct PlayMonopolyCardInstruction
    {
        public ResourceTypes ResourceType;
    }

    public struct PlayYearOfPlentyCardInstruction
    {
        public ResourceTypes FirstResourceChoice;
        public ResourceTypes SecondResourceChoice;
    }

    public struct TradeWithBankInstruction
    {
        public ResourceTypes GivingType;
        public ResourceTypes ReceivingType;
        public Int32 ReceivingCount;
    }

    public struct BuildRoadSegmentInstruction
    {
        public UInt32[] Locations;
    }

    public struct BuildSettlementInstruction
    {
        public UInt32 Location;
    }

    public struct BuildCityInstruction
    {
        public UInt32 Location;
    }
}
