﻿using System;
using System.Collections.Generic;
using System.Linq;
using Jabberwocky.SoC.Library;
using Jabberwocky.SoC.Library.DevelopmentCards;
using Jabberwocky.SoC.Library.Enums;
using Jabberwocky.SoC.Library.GameActions;
using Jabberwocky.SoC.Library.GameEvents;
using Jabberwocky.SoC.Library.Interfaces;
using NUnit.Framework;

namespace SoC.Library.ScenarioTests.PlayerTurn
{
    internal abstract class BasePlayerTurn
    {
        public readonly IPlayer player;
        protected readonly LocalGameControllerScenarioRunner runner;
        protected readonly Queue<ComputerPlayerAction> actions = new Queue<ComputerPlayerAction>();

        public BasePlayerTurn(IPlayer player, uint dice1, uint dice2, LocalGameControllerScenarioRunner runner)
        {
            this.runner = runner;
            this.player = player;
            this.Dice1 = dice1;
            this.Dice2 = dice2;
        }

        public Guid PlayerId { get { return this.player.Id; } }
        public uint Dice1 { get; }
        public uint Dice2 { get; }

        private PlayerActionBuilder actionBuilder;
        public PlayerActionBuilder Actions()
        {
            this.actionBuilder = new PlayerActionBuilder(this);
            return this.actionBuilder;
        }

        private ExpectedEventsBuilder expectedEventsBuilder;
        public ExpectedEventsBuilder Events()
        {
            this.expectedEventsBuilder = new ExpectedEventsBuilder(this, this.runner.playersByName);
            return this.expectedEventsBuilder;
        }

        private PlayerStateBuilder expectedPlayerState;
        public PlayerStateBuilder State()
        {
            this.expectedPlayerState = new PlayerStateBuilder(this);
            return this.expectedPlayerState;
        }

        public void CompareSnapshot()
        {
            if (this.expectedPlayerState == null)
                return;

            if (player.HeldCards.Count != this.expectedPlayerState.playerSnapshot.heldCards.Count)
                Assert.Fail("Held cards count is not same");

            if (player.HeldCards[0].Type != this.expectedPlayerState.playerSnapshot.heldCards[0])
                Assert.Fail("Held card does not match");
        }

        public LocalGameControllerScenarioRunner End()
        {
            return this.runner;
        }

        public LocalGameControllerScenarioRunner EndTurn()
        {
            return this.runner;
        }

        public virtual BasePlayerTurn BuildCity(uint cityLocation)
        {
            this.actions.Enqueue(new BuildCityAction(cityLocation));
            return this;
        }

        public virtual BasePlayerTurn BuildRoad(uint roadSegmentStart, uint roadSegmentEnd)
        {
            this.actions.Enqueue(new BuildRoadSegmentAction(roadSegmentStart, roadSegmentEnd));
            return this;
        }

        public virtual BasePlayerTurn BuildSettlement(uint settlementLocation)
        {
            this.actions.Enqueue(new BuildSettlementAction(settlementLocation));
            return this;
        }

        public virtual BasePlayerTurn BuyDevelopmentCard(DevelopmentCardTypes developmentCardType)
        {
            this.AddDevelopmentCard(this.PlayerId, developmentCardType);
            this.actions.Enqueue(new ComputerPlayerAction(ComputerPlayerActionTypes.BuyDevelopmentCard));
            return this;
        }

        public virtual BasePlayerTurn PlayKnightCard(uint hexLocation)
        {
            this.actions.Enqueue(new PlayKnightCardAction(hexLocation));
            return this;
        }

        public virtual BasePlayerTurn PlayKnightCardAndCollectFrom(uint hexLocation, string selectedPlayerName, ResourceTypes expectedSingleResource)
        {
            this.actions.Enqueue(new ScenarioPlayKnightCardAction(hexLocation, selectedPlayerName, expectedSingleResource));
            return this;
        }

        public virtual void ResolveActions(TurnToken turnToken, LocalGameController localGameController)
        {
            if (this.actionBuilder == null)
                return;

            foreach (var action in this.actionBuilder.playerActions)
            {
                if (action is ScenarioPlayKnightCardAction scenarioPlayKnightCardAction)
                {
                    var selectedPlayer = this.runner.GetPlayerFromName(scenarioPlayKnightCardAction.SelectedPlayerName);

                    var randomNumber = int.MinValue;
                    switch (scenarioPlayKnightCardAction.ExpectedSingleResource)
                    {
                        case ResourceTypes.Ore:
                            randomNumber = selectedPlayer.Resources.BrickCount +
                            selectedPlayer.Resources.GrainCount +
                            selectedPlayer.Resources.LumberCount;
                            break;
                        default: throw new Exception($"Resource type '{scenarioPlayKnightCardAction.ExpectedSingleResource}' not handled");
                    }

                    this.runner.NumberGenerator.AddRandomNumber(randomNumber);

                    var knightCard = (KnightDevelopmentCard)this.player.HeldCards.Where(c => c.Type == DevelopmentCardTypes.Knight).First();
                    localGameController.UseKnightCard(turnToken, knightCard, scenarioPlayKnightCardAction.NewRobberHex,
                        selectedPlayer.Id);
                }
                else if (action is PlayKnightCardAction playKnightCardAction)
                {
                    var knightCard = (KnightDevelopmentCard)this.player.HeldCards.Where(c => c.Type == DevelopmentCardTypes.Knight).First();
                    localGameController.UseKnightCard(turnToken, knightCard, playKnightCardAction.NewRobberHex, playKnightCardAction.PlayerId);
                }
                else if (action is ComputerPlayerAction)
                {
                    switch (action.ActionType)
                    {
                        case ComputerPlayerActionTypes.BuyDevelopmentCard: localGameController.BuyDevelopmentCard(turnToken); break;
                        default: throw new Exception($"Action type '{action.ActionType}' not handled");
                    }
                }
                else
                {
                    throw new Exception($"Action of type '{action.GetType()}' not handled");
                }
            }
        }

        protected void AddDevelopmentCard(Guid playerId, DevelopmentCardTypes developmentCardType)
        {
            this.runner.AddDevelopmentCardToBuy(playerId, developmentCardType);
        }

        internal List<GameEvent> GetExpectedEvents()
        {
            return this.expectedEventsBuilder.expectedEvents;
        }

        internal List<RunnerAction> GetRunnerActions()
        {
            if (this.actionBuilder != null)
                return this.actionBuilder.runnerActions;

            return null;
        }

        internal PlayerSnapshot GetPlayerSnapshot()
        {
            if (this.expectedPlayerState != null)
                return this.expectedPlayerState.playerSnapshot;

            return null;
        }
    }
}
