﻿
namespace Jabberwocky.SoC.Library
{
  using System;
  using GameBoards;

  public static class AI
  {
    public static UInt32[] GetPossibleSettlementLocationsForBestReturningResourceType(GameBoard gameBoard, ResourceTypes resourceType, out UInt32 productionFactor)
    {
      return gameBoard.GetLocationsForResourceTypeWithProductionFactors(resourceType, out productionFactor);
    }

    public static UInt32[] GetRouteFromPlayerInfrastructureToLocation(GameBoard gameBoard, UInt32 location, Guid playerId)
    {
      throw new NotImplementedException();
    }

    public static UInt32[] GetLocationsSharedByResourceProducers()
    {
      throw new NotImplementedException();
    }

    public static UInt32[] GetLocationsOnHexClosestToAnotherHex()
    {
      throw new NotImplementedException();
    }
  }
}
