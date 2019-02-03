﻿
namespace Jabberwocky.SoC.Library.Interfaces
{
    using System.Collections.Generic;
    using GameActions;
    using Jabberwocky.SoC.Library.DevelopmentCards;
    using Jabberwocky.SoC.Library.GameEvents;
    using Jabberwocky.SoC.Library.PlayerData;

    public interface IComputerPlayer : IPlayer
    {
        #region Methods
        void AddDevelopmentCard(DevelopmentCard developmentCard);
        void BuildInitialPlayerActions(PlayerDataModel[] playerData, bool moveRobber);
        uint ChooseCityLocation();
        uint ChooseSettlementLocation();
        void ChooseInitialInfrastructure(out uint settlementLocation, out uint roadEndLocation);
        KnightDevelopmentCard GetKnightCard();
        MonopolyDevelopmentCard ChooseMonopolyCard();
        ResourceClutch ChooseResourcesToCollectFromBank();
        ResourceClutch ChooseResourcesToDrop();
        ResourceTypes ChooseResourceTypeToRob();
        uint ChooseRobberLocation();
        IPlayer ChoosePlayerToRob(IEnumerable<IPlayer> otherPlayers);
        ComputerPlayerAction PlayTurn(PlayerDataModel[] otherPlayerData, LocalGameController localGameController);
        DropResourcesAction GetDropResourcesAction();
        ComputerPlayerAction GetPlayerAction();
        YearOfPlentyDevelopmentCard ChooseYearOfPlentyCard();
        #endregion
    }
}
