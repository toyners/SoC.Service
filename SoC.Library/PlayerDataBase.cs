﻿
namespace Jabberwocky.SoC.Library
{
    using System;
    using System.Collections.Generic;

    public class PlayerDataBase
    {
        public List<DevelopmentCard> PlayedDevelopmentCards;
        public bool HasLongestRoad;
        public Guid Id;
        public bool IsComputer;
        public string Name;
    }
}
