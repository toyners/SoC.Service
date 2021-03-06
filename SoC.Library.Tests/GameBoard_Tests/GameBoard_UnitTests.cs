﻿
namespace Jabberwocky.SoC.Library.UnitTests.GameBoard_Tests
{
  using System;
  using System.Collections.Generic;
  using GameBoards;
  using Jabberwocky.SoC.Library.UnitTests.Extensions;
  using LocalGameController_Tests;
  using NUnit.Framework;
  using Shouldly;

  [TestFixture]
  [Category("All")]
  [Category("GameBoard")]
  [Category("GameBoard.<Miscellenous>")]
  public class GameBoard_UnitTests : GameBoardTestBase
  {
    #region Methods
    [Test]
    public void GetPathBetweenLocations_StartAndEndAreSame_ReturnsNull()
    {
      var gameBoardData = new GameBoard(BoardSizes.Standard);
      var result = gameBoardData.GetPathBetweenLocations(0, 0);
      result.ShouldBeNull();
    }

    [Test]
    [TestCase(1u, 0u)]
    [TestCase(8u, 48u)]
    public void GetPathBetweenLocations_StartAndEndAreNeighbours_ReturnsOneStep(UInt32 endPoint, UInt32 stepIndex)
    {
      var gameBoardData = new GameBoard(BoardSizes.Standard);
      var result = gameBoardData.GetPathBetweenLocations(0, endPoint);
      result.ShouldBe(new List<UInt32> { endPoint });
    }

    [Test]
    public void GetPathBetweenLocations_StartAndEndAreNeighbours()
    {
      var gameBoardData = new GameBoard(BoardSizes.Standard);
      var result = gameBoardData.GetPathBetweenLocations(0, 10);
      result.ShouldBe(new List<UInt32> { 10, 2, 1 });
    }

    [Test]
    public void GetSettlementsForPlayers_EmptyBoard_ReturnsNull()
    {
      var gameBoardData = new GameBoard(BoardSizes.Standard);
      var settlements = gameBoardData.GetSettlementsForPlayer(Guid.NewGuid());
      settlements.ShouldBeNull();
    }

    [Test]
    public void GetSettlementsForPlayers_PlayerHasNoSettlementsOnBoard_ReturnsNull()
    {
      // Arrange
      var gameBoardData = new GameBoard(BoardSizes.Standard);
      gameBoardData.PlaceStartingInfrastructure(Guid.NewGuid(), FirstPlayerSettlementLocation, FirstPlayerRoadEndLocation);

      // Act
      var settlements = gameBoardData.GetSettlementsForPlayer(Guid.NewGuid());

      // Assert
      settlements.ShouldBeNull();
    }

    [Test]
    public void GetSettlementsForPlayers_PlayerHasFirstInfrastructureOnBoard_ReturnsOneSettlement()
    {
      // Arrange
      var playerId = Guid.NewGuid();
      var gameBoardData = new GameBoard(BoardSizes.Standard);
      gameBoardData.PlaceStartingInfrastructure(playerId, FirstPlayerSettlementLocation, FirstPlayerRoadEndLocation);

      // Act
      var settlements = gameBoardData.GetSettlementsForPlayer(playerId);

      // Assert
      settlements.Count.ShouldBe(1);
      settlements.ShouldContain(FirstPlayerSettlementLocation);
    }

    [Test]
    public void GetSettlementsForPlayers_PlayerHasAllInfrastructureOnBoard_ReturnsBothSettlements()
    {
      // Arrange
      var playerId = Guid.NewGuid();
      var gameBoardData = new GameBoard(BoardSizes.Standard);
      gameBoardData.PlaceStartingInfrastructure(playerId, FirstPlayerSettlementLocation, FirstPlayerRoadEndLocation);
      gameBoardData.PlaceStartingInfrastructure(playerId, SecondPlayerSettlementLocation, SecondPlayerRoadEndLocation);

      // Act
      var settlements = gameBoardData.GetSettlementsForPlayer(playerId);

      // Assert
      settlements.Count.ShouldBe(2);
      settlements.ShouldContain(FirstPlayerSettlementLocation);
      settlements.ShouldContain(SecondPlayerSettlementLocation);
    }

    [Test]
    public void GetSettlementsForPlayers_PlayerBuildsSettlementOnBoard_ReturnsAllThreeSettlements()
    {
      // Arrange
      var playerId = Guid.NewGuid();
      var gameBoard = new GameBoard(BoardSizes.Standard);
      gameBoard.PlaceStartingInfrastructure(playerId, FirstPlayerSettlementLocation, FirstPlayerRoadEndLocation);
      gameBoard.PlaceStartingInfrastructure(playerId, SecondPlayerSettlementLocation, SecondPlayerRoadEndLocation);
      gameBoard.PlaceRoadSegment(playerId, FirstPlayerRoadEndLocation, 10);
      gameBoard.PlaceSettlement(playerId, 10);

      // Act
      var settlements = gameBoard.GetSettlementsForPlayer(playerId);

      // Assert
      settlements.Count.ShouldBe(3);
      settlements.ShouldContain(FirstPlayerSettlementLocation);
      settlements.ShouldContain(SecondPlayerSettlementLocation);
      settlements.ShouldContain(10u);
    }

    [Test]
    [TestCase(12u, 1, 0, 0, 1, 1)]
    [TestCase(45u, 0, 1, 0, 1, 0)]
    [TestCase(53u, 0, 1, 0, 0, 0)]
    [TestCase(20u, 0, 1, 1, 1, 0)]
    public void GetResourcesForLocation_StandardBoard_ReturnsExpectedResources(UInt32 location, Int32 expectedBrickCount, Int32 expectedGrainCount, Int32 expectedLumberCount, Int32 expectedOreCount, Int32 expectedWoolCount)
    {
      var gameBoardData = new GameBoard(BoardSizes.Standard);
      var result = gameBoardData.GetResourcesForLocation(location);
      result.BrickCount.ShouldBe(expectedBrickCount);
      result.GrainCount.ShouldBe(expectedGrainCount);
      result.LumberCount.ShouldBe(expectedLumberCount);
      result.OreCount.ShouldBe(expectedOreCount);
      result.WoolCount.ShouldBe(expectedWoolCount);
    }

    [Test]
    public void GetResourcesForRoll_StandardBoard_ReturnsCorrectResourcesForMatchingNeighbouringLocations()
    {
      var player1_Id = Guid.NewGuid();
      var player2_Id = Guid.NewGuid();
      var player3_Id = Guid.NewGuid();

      var roll = 8u;
      var gameBoardData = new GameBoard(BoardSizes.Standard);
      gameBoardData.PlaceStartingInfrastructure(player1_Id, 12, 11);
      gameBoardData.PlaceStartingInfrastructure(player1_Id, 53, 52);
      gameBoardData.PlaceStartingInfrastructure(player2_Id, 43, 42);
      gameBoardData.PlaceStartingInfrastructure(player3_Id, 39, 47);

      var result = gameBoardData.GetResourcesForRoll(roll);

      var expected = new Dictionary<Guid, ResourceCollection[]>();
      expected.Add(player1_Id, new ResourceCollection[] 
      {
        new ResourceCollection(12u, ResourceClutch.OneBrick),
        new ResourceCollection(53u, ResourceClutch.OneGrain)
      });

      expected.Add(player2_Id, new ResourceCollection[] { new ResourceCollection(43u, ResourceClutch.OneGrain) });

      result.ShouldContainExact(expected);
    }

    /// <summary>
    /// Verify that the collected resources are correct when all three hex locations are owned by the same
    /// player.
    /// </summary>
    [Test]
    public void GetResourcesForRoll_AllLocationsOnHexOwnedBySamePlayer_ReturnsCorrectResources()
    {
      var playerId = Guid.NewGuid();

      var roll = 8u;
      var gameBoardData = new GameBoard(BoardSizes.Standard);
      gameBoardData.PlaceStartingInfrastructure(playerId, 2, 1);
      gameBoardData.PlaceStartingInfrastructure(playerId, 11, 12);
      gameBoardData.PlaceRoadSegment(playerId, 12, 4);
      gameBoardData.PlaceSettlement(playerId, 4);

      var result = gameBoardData.GetResourcesForRoll(roll);

      var expected = new Dictionary<Guid, ResourceCollection[]>();
      expected.Add(playerId, new ResourceCollection[]
      {
        new ResourceCollection(4u, ResourceClutch.OneBrick),
        new ResourceCollection(2u, ResourceClutch.OneBrick),
        new ResourceCollection(11u, ResourceClutch.OneBrick)
      });

      result.ShouldContainExact(expected);
    }

    [Test]
    [TestCase(5u, 42u, 41u, ResourceTypes.Brick)]
    [TestCase(2u, 23u, 22u, ResourceTypes.Grain)]
    [TestCase(11u, 27u, 28u, ResourceTypes.Lumber)]
    [TestCase(6u, 20u, 21u, ResourceTypes.Ore)]
    [TestCase(10u, 12u, 13u, ResourceTypes.Wool)]
    public void GetResourcesForRoll_StandardBoard_ReturnsCorrectResources(UInt32 diceRoll, UInt32 settlementLocation, UInt32 roadEndLocation, ResourceTypes expectedType)
    {
      var playerId = Guid.NewGuid();
      var gameBoardData = new GameBoard(BoardSizes.Standard);
      gameBoardData.PlaceStartingInfrastructure(playerId, settlementLocation, roadEndLocation);
      var result = gameBoardData.GetResourcesForRoll(diceRoll);

      ResourceClutch expectedResources = default(ResourceClutch);
      switch (expectedType)
      {
        case ResourceTypes.Brick: expectedResources = ResourceClutch.OneBrick; break;
        case ResourceTypes.Grain: expectedResources = ResourceClutch.OneGrain; break;
        case ResourceTypes.Lumber: expectedResources = ResourceClutch.OneLumber; break;
        case ResourceTypes.Ore: expectedResources = ResourceClutch.OneOre; break;
        case ResourceTypes.Wool: expectedResources = ResourceClutch.OneWool; break;
      }

      result.Count.ShouldBe(1);
      result.ShouldContainKey(playerId);
      var actual = result[playerId];
      actual[0].Location.ShouldBe(settlementLocation);
      actual[0].Resources.ShouldBe(expectedResources);
    }

    [Test]
    public void GetPlayersForLocation_OnePlayerOnHex_ReturnPlayerIds()
    {
      // Arrange
      var playerId = Guid.NewGuid();
      var gameBoardData = new GameBoard(BoardSizes.Standard);
      gameBoardData.PlaceStartingInfrastructure(playerId, 0, 8);

      // Act
      var results = gameBoardData.GetPlayersForHex(0);

      // Assert
      results.Length.ShouldBe(1);
      results.ShouldContain(playerId);
    }

    [Test]
    public void GetPlayersForHex_MultiplePlayersOnHex_ReturnPlayerIds()
    {
      // Arrange
      var firstPlayerId = Guid.NewGuid();
      var secondPlayerId = Guid.NewGuid();
      var gameBoardData = new GameBoard(BoardSizes.Standard);
      gameBoardData.PlaceStartingInfrastructure(firstPlayerId, 0, 8);
      gameBoardData.PlaceStartingInfrastructure(secondPlayerId, 2, 1);

      // Act
      var results = gameBoardData.GetPlayersForHex(0);

      // Assert
      results.Length.ShouldBe(2);
      results.ShouldContain(firstPlayerId);
      results.ShouldContain(secondPlayerId);
    }

    [Test]
    public void GetPlayersForHex_MultiplePlayerSettlementsOnHex_ReturnPlayerIds()
    {
      // Arrange
      var playerId = Guid.NewGuid();
      var gameBoardData = new GameBoard(BoardSizes.Standard);
      gameBoardData.PlaceStartingInfrastructure(playerId, 0, 8);
      gameBoardData.PlaceStartingInfrastructure(playerId, SecondPlayerSettlementLocation, SecondPlayerRoadEndLocation);
      gameBoardData.PlaceRoadSegment(playerId, 8, 9);
      gameBoardData.PlaceSettlement(playerId, 9);

      // Act
      var results = gameBoardData.GetPlayersForHex(0);

      // Assert
      results.Length.ShouldBe(1);
      results.ShouldContain(playerId);
    }

    [Test]
    public void GetPlayersForHex_NoPlayerSettlementsOnHex_ReturnNull()
    {
      // Arrange
      var playerId = Guid.NewGuid();
      var gameBoardData = new GameBoard(BoardSizes.Standard);

      // Act
      var results = gameBoardData.GetPlayersForHex(0);

      // Assert
      results.ShouldBeNull();
    }

    [Test]
    public void GetHexInformation_StandardBoard_ReturnsResourceTypeArray()
    {
      // Arrange
      var gameBoard = new GameBoard(BoardSizes.Standard);

      // Act
      var data = gameBoard.GetHexData();

      // Assert
      data[0].ShouldBe(new HexInformation { ResourceType = null, ProductionFactor = 0});
      data[1].ShouldBe(new HexInformation { ResourceType = ResourceTypes.Brick, ProductionFactor = 8});
      data[2].ShouldBe(new HexInformation { ResourceType = ResourceTypes.Ore, ProductionFactor = 5});

      data[3].ShouldBe(new HexInformation { ResourceType = ResourceTypes.Brick, ProductionFactor = 4});
      data[4].ShouldBe(new HexInformation { ResourceType = ResourceTypes.Lumber, ProductionFactor = 3});
      data[5].ShouldBe(new HexInformation { ResourceType = ResourceTypes.Wool, ProductionFactor = 10});
      data[6].ShouldBe(new HexInformation { ResourceType = ResourceTypes.Grain, ProductionFactor = 2});

      data[7].ShouldBe(new HexInformation { ResourceType = ResourceTypes.Lumber, ProductionFactor = 11});
      data[8].ShouldBe(new HexInformation { ResourceType = ResourceTypes.Ore, ProductionFactor = 6});
      data[9].ShouldBe(new HexInformation { ResourceType = ResourceTypes.Grain, ProductionFactor = 11});
      data[10].ShouldBe(new HexInformation { ResourceType = ResourceTypes.Wool, ProductionFactor = 9});
      data[11].ShouldBe(new HexInformation { ResourceType = ResourceTypes.Lumber, ProductionFactor = 6});

      data[12].ShouldBe(new HexInformation { ResourceType = ResourceTypes.Wool, ProductionFactor = 12});
      data[13].ShouldBe(new HexInformation { ResourceType = ResourceTypes.Brick, ProductionFactor = 5});
      data[14].ShouldBe(new HexInformation { ResourceType = ResourceTypes.Lumber, ProductionFactor = 4});
      data[15].ShouldBe(new HexInformation { ResourceType = ResourceTypes.Ore, ProductionFactor = 3});

      data[16].ShouldBe(new HexInformation { ResourceType = ResourceTypes.Grain, ProductionFactor = 9});
      data[17].ShouldBe(new HexInformation { ResourceType = ResourceTypes.Wool, ProductionFactor = 10});
      data[18].ShouldBe(new HexInformation { ResourceType = ResourceTypes.Grain, ProductionFactor = 8});
    }

    [Test]
    public void GetSettlementInformation_OneSettlement_ReturnsSettlementDetails()
    {
      // Arrange
      var playerId = Guid.NewGuid();
      var gameBoard = new GameBoard(BoardSizes.Standard);
      var settlementLocation = 12u;
      gameBoard.PlaceStartingInfrastructure(playerId, FirstPlayerSettlementLocation, FirstPlayerRoadEndLocation);

      // Act
      var settlements = gameBoard.GetSettlementData();

      // Assert
      settlements.Count.ShouldBe(1);
      settlements.ShouldContainKeyAndValue(FirstPlayerSettlementLocation, playerId);
    }

    [Test]
    public void GetRoadInformation_OneRoad_ReturnsRoadDetails()
    {
      // Arrange
      var playerId = Guid.NewGuid();
      var gameBoard = new GameBoard(BoardSizes.Standard);
      gameBoard.PlaceStartingInfrastructure(playerId, 12, 4);

      // Act
      var roads = gameBoard.GetRoadData();

      // Assert
      roads.Length.ShouldBe(1);
      roads[0].ShouldBe(new Tuple<UInt32, UInt32, Guid>(12, 4, playerId));
    }

    [Test]
    public void GetProductionValuesForLocation_LocationWithThreeResourceProducers_ReturnsExpectedProductionValues()
    {
      // Arrange
      var gameBoard = new GameBoard(BoardSizes.Standard);

      // Act
      var productionValues = gameBoard.GetProductionValuesForLocation(12u);

      // Assert
      productionValues.Length.ShouldBe(3);
      productionValues.ShouldContain(8);
      productionValues.ShouldContain(5);
      productionValues.ShouldContain(10);
    }

    [Test]
    public void GetProductionValuesForLocation_LocationWithTwoResourceProducers_ReturnsExpectedProductionValues()
    {
      // Arrange
      var gameBoard = new GameBoard(BoardSizes.Standard);

      // Act
      var productionValues = gameBoard.GetProductionValuesForLocation(4u);

      // Assert
      productionValues.Length.ShouldBe(2);
      productionValues.ShouldContain(8);
      productionValues.ShouldContain(5);
    }

    [Test]
    public void GetProductionValuesForLocation_LocationWithOneResourceProducers_ReturnsExpectedProductionValues()
    {
      // Arrange
      var gameBoard = new GameBoard(BoardSizes.Standard);

      // Act
      var productionValues = gameBoard.GetProductionValuesForLocation(3u);

      // Assert
      productionValues.Length.ShouldBe(1);
      productionValues.ShouldContain(8);
    }

    [Test]
    public void GetProductionValuesForLocation_LocationIsOnDesertOnly_ReturnsEmptyArray()
    {
      // Arrange
      var gameBoard = new GameBoard(BoardSizes.Standard);

      // Act
      var productionValues = gameBoard.GetProductionValuesForLocation(0u);

      // Assert
      productionValues.Length.ShouldBe(0);
    }

    [Test]
    [TestCase(ResourceTypes.Brick, new int[] { 2, 8, 3, 8, 4, 8, 10, 8, 11, 8, 12, 8, 30, 5, 31, 5, 32, 5, 40, 5, 41, 5, 42, 5, 7, 4, 8, 4, 9, 4, 17, 4, 18, 4, 19, 4 })]
    [TestCase(ResourceTypes.Grain, new int[] { 43, 8, 44, 8, 45, 8, 51, 8, 52, 8, 53, 8, 39, 9, 40, 9, 41, 9, 47, 9, 48, 9, 49, 9, 20, 11, 21, 11, 22, 11, 31, 11, 32, 11, 33, 11, 13, 2, 14, 2, 15, 2, 23, 2, 24, 2, 25, 2 })]
    [TestCase(ResourceTypes.Lumber, new int[] { 24, 6, 25, 6, 26, 6, 35, 6, 36, 6, 37, 6, 32, 4, 33, 4, 34, 4, 42, 4, 43, 4, 44, 4, 16, 11, 17, 11, 18, 11, 27, 11, 28, 11, 29, 11, 9, 3, 10, 3, 11, 3, 19, 3, 20, 3, 21, 3 })]
    [TestCase(ResourceTypes.Ore, new int[] { 18, 6, 19, 6, 20, 6, 29, 6, 30, 6, 31, 6, 4, 5, 5, 5, 6, 5, 12, 5, 13, 5, 14, 5, 34, 3, 35, 3, 36, 3, 44, 3, 45, 3, 46, 3 })]
    [TestCase(ResourceTypes.Wool, new int[] { 22, 9, 23, 9, 24, 9, 33, 9, 34, 9, 35, 9, 11, 10, 12, 10, 13, 10, 21, 10, 22, 10, 23, 10, 41, 10, 42, 10, 43, 10, 49, 10, 50, 10, 51, 10, 28, 12, 29, 12, 30, 12, 38, 12, 39, 12, 40, 12 })]
    public void GetLocationsForResourceProducerOrderedByProductionFactorDescending_StandardBoard_ReturnsLocationList(ResourceTypes resourceType, int[] expectedRawLocationProductionFactorData)
    {
      var gameBoard = new GameBoard(BoardSizes.Standard);

      var results = gameBoard.GetLocationsForResourceProducerOrderedByProductionFactorDescending(resourceType);

      results.Length.ShouldBe(expectedRawLocationProductionFactorData.Length / 2);
      results.ShouldContainExact(this.CreateExpectedLocationProductionFactorCollection(expectedRawLocationProductionFactorData));
    }

    private Tuple<uint, int>[] CreateExpectedLocationProductionFactorCollection(int[] data)
    {
      var result = new Tuple<uint, int>[data.Length / 2];
      var index = 0;
      for (var i = 0; i < data.Length; i += 2)
      {
        result[index++] = new Tuple<uint, int>((uint)data[i], data[i + 1]);
      }

      return result;
    }
    #endregion
  }
}
