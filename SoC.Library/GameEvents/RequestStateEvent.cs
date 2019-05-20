﻿
namespace Jabberwocky.SoC.Library.GameEvents
{
    using System;

    public class RequestStateEvent : GameEvent
    {
        public RequestStateEvent(Guid playerId) : base(playerId)
        {
        }

        public ResourceClutch Resources { get; set; }
        public uint RoadSegments { get; set; }
        public uint VictoryPoints { get; set; }
    }
}
