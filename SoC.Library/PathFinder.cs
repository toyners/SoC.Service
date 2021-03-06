﻿
namespace Jabberwocky.SoC.Library
{
  using System;
  using System.Collections.Generic;

  public static class PathFinder
  {
    public static List<uint> GetPathBetweenPoints(uint startIndex, uint endIndex, uint locationCount, Func<uint, uint, bool> canBeReached)
    {
      var closedSet = new HashSet<UInt32>();
      var openSet = new HashSet<UInt32>();
      // For each point, which point it can most efficiently be reached from.
      // If a point can be reached from many points, mostEfficientNeighbour will eventually contain the
      // most efficient previous step.
      var mostEfficientNeighbour = new Dictionary<UInt32, UInt32>();

      // For each point, the cost of getting from the start point to that point.
      var distancesFromStartToPoint = new Dictionary<UInt32, Single>();

      // For each point, the total cost of getting from the start point to the goal
      // by passing by that point. That value is partly known, partly heuristic.
      var totalCostOfStartToGoalViaThisPoint = new Dictionary<UInt32, Single>();

      openSet.Add(startIndex); // Index of first node

      // The cost of going from start to start is zero.
      distancesFromStartToPoint.Add(startIndex, 0f);

      UInt32 distanceCoveringAllPermanentPoints = locationCount;

      totalCostOfStartToGoalViaThisPoint.Add(startIndex, distanceCoveringAllPermanentPoints);

      List<UInt32> path = null;
      while (openSet.Count > 0)
      {
        var currentIndex = GetIndexFromOpenSetWithLowestTotalCost(openSet, totalCostOfStartToGoalViaThisPoint);
        if (currentIndex == endIndex)
        {
          path = ConstructPath(currentIndex, startIndex, mostEfficientNeighbour);
          break;
        }

        openSet.Remove(currentIndex);
        closedSet.Add(currentIndex);

        for (uint index = 0; index < locationCount; index++)
        {
          if (currentIndex == index)
          {
            continue;
          }

          if (closedSet.Contains(index))
          {
            continue;
          }

          var canReach = canBeReached(currentIndex, index);
          if (!canReach)
          {
            continue;
          }

          var workingDistance = distancesFromStartToPoint[currentIndex] + 1;

          if (!openSet.Contains(index))
          {
            openSet.Add(index); // Discovered a new point
          }
          else if (workingDistance >= distancesFromStartToPoint[index])
          {
            continue; // This is not a better path.
          }

          // This path is the best until now. Record it!
          mostEfficientNeighbour[index] = currentIndex;
          distancesFromStartToPoint[index] = workingDistance;

          var distanceOfEstimatedPathFromStartToEnd = workingDistance + distanceCoveringAllPermanentPoints;
          totalCostOfStartToGoalViaThisPoint[index] = distanceOfEstimatedPathFromStartToEnd;
        }
      }

      return path;
    }

    private static List<UInt32> ConstructPath(UInt32 currentIndex, UInt32 startIndex, Dictionary<UInt32, UInt32> mostEfficientNeighbour)
    {
      var list = new List<UInt32>();
      list.Add(currentIndex);

      while (mostEfficientNeighbour.ContainsKey(currentIndex))
      {
        currentIndex = mostEfficientNeighbour[currentIndex];
        if (currentIndex == startIndex)
        {
          return list;
        }

        list.Add(currentIndex);
      }

      throw new Exception("Should not get here");
    }

    private static UInt32 GetIndexFromOpenSetWithLowestTotalCost(HashSet<UInt32> openSet, Dictionary<UInt32, Single> totalCostOfStartToGoalViaThisPoint)
    {
      var workingDistance = Single.MaxValue;
      UInt32? lowestIndex = null;
      foreach (var index in openSet)
      {
        if (totalCostOfStartToGoalViaThisPoint[index] < workingDistance)
        {
          lowestIndex = index;
          workingDistance = totalCostOfStartToGoalViaThisPoint[index];
        }
      }

      if (lowestIndex == null)
      {
        throw new Exception("Should not get here");
      }

      return lowestIndex.Value;
    }
  }
}
