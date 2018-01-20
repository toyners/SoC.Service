﻿
namespace Jabberwocky.SoC.Library
{
  using System;

  public class PlayMonopolyCardEvent : GameEvent
  {
    public readonly ResourceTransactionList ResourceTransactionList;

    public PlayMonopolyCardEvent(Guid playerId, ResourceTransactionList resourceTransactionList) : base(playerId)
    {
      this.ResourceTransactionList = resourceTransactionList;
    }

    public override Boolean Equals(Object obj)
    {
      if (!base.Equals(obj))
      {
        return false;
      }

      var other = (PlayMonopolyCardEvent)obj;

      if (this.ResourceTransactionList == null && other.ResourceTransactionList == null)
      {
        return true;
      }

      if (this.ResourceTransactionList == null || other.ResourceTransactionList == null)
      {
        return false;
      }

      if (this.ResourceTransactionList.Count != other.ResourceTransactionList.Count)
      {
        return false;
      }

      for (var i = 0; i < this.ResourceTransactionList.Count; i++)
      {
        var left = this.ResourceTransactionList[i];
        var right = other.ResourceTransactionList[i];
        if (left.GivingPlayerId != right.GivingPlayerId ||
            left.ReceivingPlayerId != right.ReceivingPlayerId ||
            left.Resources != right.Resources)
        {
          return false;
        }
      }

      return true;
    }
  }
}