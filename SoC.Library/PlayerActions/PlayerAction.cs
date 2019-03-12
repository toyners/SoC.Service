﻿
namespace Jabberwocky.SoC.Library.GameActions
{
    using System;
    using Enums;

    public class PlayerAction
    {
        public readonly ComputerPlayerActionTypes ActionType;
        public readonly Guid PlayerId;

        public PlayerAction(GameToken token)
        {
            this.Token = token;
        }

        public PlayerAction(ComputerPlayerActionTypes action)
        {
            this.ActionType = action;
        }

        public PlayerAction(Guid playerId)
        {
            this.PlayerId = playerId;
        }

        public GameToken Token { get; private set; }
    }
}
