﻿using System;
using System.Collections.Generic;
using Jabberwocky.SoC.Library;
using Jabberwocky.SoC.Library.GameEvents;
using Jabberwocky.SoC.Library.Interfaces;
using Jabberwocky.SoC.Library.PlayerActions;
using SoC.Library.ScenarioTests.PlayerTurn;
using SoC.Library.ScenarioTests.ScenarioActions;

namespace SoC.Library.ScenarioTests.Builders
{
    internal class PlayerResponseBuilder
    {
        private readonly IDictionary<Guid, PlayerAction> actionsByPlayerId = new Dictionary<Guid, PlayerAction>();
        private readonly IDictionary<Guid, GameEvent> gameEventsByPlayerId = new Dictionary<Guid, GameEvent>();
        private readonly IDictionary<string, ResourceClutch> playerResourcesToDropByName = new Dictionary<string, ResourceClutch>();
        private Dictionary<string, IPlayer> playersByName;
        private readonly GameTurn playerTurn;

        public PlayerResponseBuilder(GameTurn playerTurn, Dictionary<string, IPlayer> playersByName)
        {
            this.playerTurn = playerTurn;
            this.playersByName = playersByName;
        }

        public GameTurn End()
        {
            this.playerTurn.PlayerResourcesToDropByName = this.playerResourcesToDropByName;
            this.playerTurn.ActionsByPlayerId = this.actionsByPlayerId;
            this.playerTurn.GameEventsByPlayerId = this.gameEventsByPlayerId;
            return this.playerTurn;
        }

        public PlayerResponseBuilder ResourcesToDrop(string playerName, ResourceClutch resourceClutch)
        {
            this.playerResourcesToDropByName.Add(playerName, resourceClutch);
            return this;
        }

        public PlayerResponseBuilder WhenDiceRollIsSevenThenDropResources(string playerName, ResourceClutch resourcesToDrop)
        {
            var player = this.playersByName[playerName];
            if (player is ScenarioPlayer)
            {
                this.gameEventsByPlayerId.Add(player.Id, new DiceRollEvent(player.Id, 3, 4));
                this.actionsByPlayerId.Add(player.Id, new ScenarioDropResourcesAction("", resourcesToDrop));
            }
            else
            {
                ((ScenarioComputerPlayer)player).AddResourcesToDrop(resourcesToDrop);
            }
            
            return this;
        }
    }
}
