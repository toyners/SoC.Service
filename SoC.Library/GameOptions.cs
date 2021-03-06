﻿
namespace Jabberwocky.SoC.Library
{
    using Enums;

    public class GameOptions
    {
        public GameConnectionTypes Connection = GameConnectionTypes.Local;

        public int Players { get; set; } = 1;
        public uint MaxAIPlayers { get; set; } = 3;
        public int TurnTimeInSeconds { get; set; } = 120;
    }
}
