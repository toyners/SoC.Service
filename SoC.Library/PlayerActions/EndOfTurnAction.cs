﻿
using System;

namespace Jabberwocky.SoC.Library.PlayerActions
{
    public class EndOfTurnAction : PlayerAction
    {
        public EndOfTurnAction(Guid initiatingPlayerId) : base(initiatingPlayerId)
        {
        }
    }
}
