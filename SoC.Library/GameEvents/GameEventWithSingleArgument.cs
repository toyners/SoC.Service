﻿

namespace Jabberwocky.SoC.Library.GameEvents
{
    using System;

    public class GameEventWithSingleArgument<T> : GameEvent
    {
        protected readonly T Item;
        public GameEventWithSingleArgument(T item) : base(Guid.Empty) => this.Item = item;
    }
}