﻿
namespace Jabberwocky.SoC.Library.GameEvents
{
  using System;
  using GameEvents;

  public class LargestArmyChangedEvent : GameEvent
  {
    #region Fields
    public readonly Guid PreviousPlayerId;
    #endregion

    #region Construction
    public LargestArmyChangedEvent(Guid playerId, Guid previousPlayerId) : base(playerId)
    {
      this.PreviousPlayerId = previousPlayerId;
    }
    #endregion

    #region Methods
    public override Boolean Equals(Object obj)
    {
      if (!base.Equals(obj))
      {
        return false;
      }

      return this.PreviousPlayerId == ((LargestArmyChangedEvent)obj).PreviousPlayerId;
    }

    public override Int32 GetHashCode()
    {
      return base.GetHashCode();
    }
    #endregion
  }
}
