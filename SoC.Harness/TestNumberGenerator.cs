﻿
namespace SoC.Harness
{
    using System;
    using System.Collections.Generic;
    using Jabberwocky.SoC.Library;
    using Jabberwocky.SoC.Library.Interfaces;

    public class TestNumberGenerator : INumberGenerator
    {
        private Dice dice;
        private Queue<Tuple<uint, uint>> diceRolls;

        public TestNumberGenerator()
        {
            this.diceRolls = new Queue<Tuple<uint, uint>>(new[]
            {
                new Tuple<uint, uint>(6, 6),
                new Tuple<uint, uint>(3, 3),
                new Tuple<uint, uint>(1, 3),
                new Tuple<uint, uint>(2, 1),
                new Tuple<uint, uint>(6, 5),
                new Tuple<uint, uint>(5, 4),
                new Tuple<uint, uint>(4, 3),
                new Tuple<uint, uint>(3, 2),

                new Tuple<uint, uint>(1, 5),
            });

            this.dice = new Dice();
        }

        public int GetRandomNumberBetweenZeroAndMaximum(int exclusiveMaximum)
        {
            return this.dice.GetRandomNumberBetweenZeroAndMaximum(exclusiveMaximum);
        }

        public void RollTwoDice(out uint dice1, out uint dice2)
        {
            if (this.diceRolls.Count > 0)
            {
                var diceRoll = this.diceRolls.Dequeue();
                dice1 = diceRoll.Item1;
                dice2 = diceRoll.Item2;
                return;
            }

            this.dice.RollTwoDice(out dice1, out dice2);
        }
    }
}
