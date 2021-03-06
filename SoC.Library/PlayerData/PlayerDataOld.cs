﻿
namespace Jabberwocky.SoC.Library.PlayerData
{
    using System;
    using Jabberwocky.SoC.Library.DevelopmentCards;

    public class PlayerDataOld : PlayerDataBase
    {
        public PlayerDataOld() : base(null)
        {
        }

        public DevelopmentCard[] DevelopmentCards { get; private set; }

        public UInt32 BrickCount { get; private set; }
        public UInt32 GrainCount { get; private set; }
        public UInt32 LumberCount { get; private set; }
        public UInt32 OreCount { get; private set; }
        public UInt32 WoolCount { get; private set; }
    }
}
