﻿
namespace Jabberwocky.SoC.Library.GameEvents
{
    using System;

    public class TradeWithPlayerCompletedEvent : GameEvent
    {
        public readonly Guid BuyingPlayerId;
        public readonly ResourceClutch BuyingResources;
        public readonly Guid SellingPlayerId;
        public readonly ResourceClutch SellingResources;

        public TradeWithPlayerCompletedEvent(Guid buyingPlayerId, ResourceClutch buyingResources, Guid sellingPlayerId, ResourceClutch sellingResources) : base(Guid.Empty)
        {
            this.BuyingPlayerId = buyingPlayerId;
            this.BuyingResources = buyingResources;
            this.SellingPlayerId = sellingPlayerId;
            this.SellingResources = sellingResources;
        }
    }
}
