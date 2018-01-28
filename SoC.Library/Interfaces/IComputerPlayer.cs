﻿
using System;
using System.Collections.Generic;
using Jabberwocky.SoC.Library.GameBoards;

namespace Jabberwocky.SoC.Library.Interfaces
{
  public enum PlayerAction
  {
    EndTurn,
    BuildCity,
    BuildRoad,
    BuyDevelopmentCard,
    PlayKnightCard,
    PlayMonopolyCard,
    PlayYearOfPlentyCard,
  }

  public interface IComputerPlayer : IPlayer
  {
    #region Methods
    UInt32 ChooseCityLocation(GameBoardData gameBoardData);
    UInt32 ChooseSettlementLocation(GameBoardData gameBoardData);
    void ChooseRoad(GameBoardData gameBoardData, out UInt32 startRoadLocation, out UInt32 endRoadLocation);
    void ChooseInitialInfrastructure(GameBoardData gameBoardData, out UInt32 settlementLocation, out UInt32 roadEndLocation);
    KnightDevelopmentCard ChooseKnightCard();
    MonopolyDevelopmentCard ChooseMonopolyCard();
    YearOfPlentyDevelopmentCard ChooseYearOfPlentyCard();
    ResourceClutch ChooseResouresToCollectFromBank();
    ResourceClutch ChooseResourcesToDrop();
    ResourceTypes ChooseResourceTypeToRob();
    UInt32 ChooseRobberLocation();
    IPlayer ChoosePlayerToRob(IEnumerable<IPlayer> otherPlayers);
    PlayerAction GetPlayerAction();
    void AddDevelopmentCard(DevelopmentCard developmentCard);
    #endregion
  }
}
