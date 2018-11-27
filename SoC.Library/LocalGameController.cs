﻿
namespace Jabberwocky.SoC.Library
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Xml;
    using Enums;
    using GameActions;
    using GameBoards;
    using GameEvents;
    using Interfaces;
    using Jabberwocky.SoC.Library.PlayerData;
    using Jabberwocky.SoC.Library.Store;
    using Newtonsoft.Json;

    public class LocalGameController : IGameController
    {
        #region Enums
        public enum GamePhases
        {
            Initial,
            WaitingLaunch,
            StartGameSetup,
            ChooseResourceFromOpponent,
            ContinueGameSetup,
            CompleteGameSetup,
            FinalisePlayerTurnOrder,
            StartGamePlay,
            ContinueGamePlay,
            SetRobberHex,
            DropResources,
            Quitting,
            NextStep,
            GameOver
        }

        [Flags]
        public enum BuildStatuses
        {
            Successful = 0,
            NoSettlements,
            NotEnoughResourcesForSettlement,
            NoRoads,
            NotEnoughResourcesForRoad,
            NoCities,
            NotEnoughResourcesForCity
        }

        public enum BuyStatuses
        {
            Successful = 0,
            NoCards,
            NotEnoughResources
        }
        #endregion

        #region Fields
        private IPlayerPool playerPool;
        private bool cardPlayedThisTurn;
        private HashSet<DevelopmentCard> cardsPlayed;
        private HashSet<DevelopmentCard> cardsPurchasedThisTurn;
        private INumberGenerator numberGenerator;
        private GameBoard gameBoard;
        private int playerIndex;
        private IPlayer[] players;
        private Dictionary<Guid, IPlayer> playersById;
        private IPlayer[] computerPlayers;
        private IPlayer playerWithLargestArmy;
        private IPlayer playerWithLongestRoad;
        private IPlayer mainPlayer;
        private ResourceUpdate gameSetupResources;
        private int resourcesToDrop;
        private uint robberHex;
        private Dictionary<Guid, int> robbingChoices;
        private TurnToken currentTurnToken;
        private IPlayer currentPlayer;
        private IDevelopmentCardHolder developmentCardHolder;
        private uint dice1, dice2;
        #endregion

        #region Construction
        public LocalGameController(INumberGenerator dice, IPlayerPool playerPool)
            : this(dice, playerPool, new GameBoard(BoardSizes.Standard), new DevelopmentCardHolder()) { }

        public LocalGameController(INumberGenerator dice, IPlayerPool computerPlayerFactory, GameBoard gameBoard, IDevelopmentCardHolder developmentCardHolder)
        {
            this.numberGenerator = dice;
            this.playerPool = computerPlayerFactory;
            this.gameBoard = gameBoard;
            this.developmentCardHolder = developmentCardHolder;
            this.GamePhase = GamePhases.Initial;
            this.cardsPlayed = new HashSet<DevelopmentCard>();
            this.cardsPurchasedThisTurn = new HashSet<DevelopmentCard>();
        }
        #endregion

        #region Properties
        [JsonProperty]
        public Guid GameId { get; private set; }
        public GamePhases GamePhase { get; private set; }
        #endregion

        #region Events
        public Action<GameBoardUpdate> BoardUpdatedEvent { get; set; }
        public Action CityBuiltEvent { get; set; }
        public Action<DevelopmentCard> DevelopmentCardPurchasedEvent { get; set; }
        public Action<uint, uint> DiceRollEvent { get; set; }
        public Action<ErrorDetails> ErrorRaisedEvent { get; set; }
        public Action<GameBoard> InitialBoardSetupEvent { get; set; }
        public Action<PlayerDataBase[]> GameJoinedEvent { get; set; }
        public Action<PlayerDataBase[], GameBoard> GameLoadedEvent { get; set; }
        public Action<Guid> GameOverEvent { get; set; }
        public Action<ResourceUpdate> GameSetupResourcesEvent { get; set; }
        public Action<GameBoardUpdate> GameSetupUpdateEvent { get; set; }
        public Action<Guid, Guid> LargestArmyEvent { get; set; }
        public Action<ClientAccount> LoggedInEvent { get; set; }
        public Action<Guid, Guid> LongestRoadBuiltEvent { get; set; }
        public Action<Guid, List<GameEvent>> OpponentActionsEvent { get; set; }
        public Action<Dictionary<Guid, ResourceCollection[]>> ResourcesCollectedEvent { get; set; }
        public Action<ResourceUpdate> ResourcesLostEvent { get; set; }
        public Action<ResourceTransactionList> ResourcesTransferredEvent { get; set; }
        public Action RoadSegmentBuiltEvent { get; set; }
        public Action<int> RobberEvent { get; set; }
        public Action<Dictionary<Guid, int>> RobbingChoicesEvent { get; set; }
        public Action SettlementBuiltEvent { get; set; }
        public Action<GameBoardUpdate> StartInitialSetupTurnEvent { get; set; }
        public Action<TurnToken> StartPlayerTurnEvent { get; set; }
        public Action<PlayerDataModel[]> TurnOrderFinalisedEvent { get; set; }
        #endregion

        #region Methods
        public void BuildCity(TurnToken turnToken, uint location)
        {
            if (!this.VerifyTurnToken(turnToken) || !this.VerifyBuildCityRequest(location))
            {
                return;
            }

            this.BuildCity(location);

            this.CityBuiltEvent?.Invoke();

            this.CheckMainPlayerIsWinner();
        }

        public void BuildRoadSegment(TurnToken turnToken, uint roadStartLocation, uint roadEndLocation)
        {
            if (!this.VerifyTurnToken(turnToken) || !this.VerifyBuildRoadSegmentRequest(roadStartLocation, roadEndLocation))
            {
                return;
            }

            this.BuildRoadSegment(roadStartLocation, roadEndLocation);
            this.RoadSegmentBuiltEvent?.Invoke();

            Guid previousPlayerWithLongestRoadId;
            if (this.PlayerHasJustBuiltTheLongestRoad(out previousPlayerWithLongestRoadId))
            {
                this.LongestRoadBuiltEvent?.Invoke(previousPlayerWithLongestRoadId, this.mainPlayer.Id);
            }
        
            this.CheckMainPlayerIsWinner();
        }

        public void BuildSettlement(TurnToken turnToken, uint location)
        {
            if (!this.VerifyTurnToken(turnToken) || !this.VerifyBuildSettlementRequest(location))
            {
                return;
            }

            this.BuildSettlement(location);
            this.SettlementBuiltEvent?.Invoke();

            this.CheckMainPlayerIsWinner();
        }

        public void BuyDevelopmentCard(TurnToken turnToken)
        {
            if (!this.VerifyTurnToken(turnToken) || !this.VerifyBuyDevelopmentCardRequest())
            {
                return;
            }

            DevelopmentCard developmentCard = this.BuyDevelopmentCard();
            this.DevelopmentCardPurchasedEvent?.Invoke(developmentCard);
        }

        public BuildStatuses CanBuildCity()
        {
            BuildStatuses result = BuildStatuses.Successful;
            if (this.mainPlayer.Resources < ResourceClutch.City)
                result |= BuildStatuses.NotEnoughResourcesForCity;

            if (this.mainPlayer.RemainingCities == 0)
                result |= BuildStatuses.NoCities;

            return result;
        }

        public BuildStatuses CanBuildRoadSegment()
        {
            BuildStatuses result = BuildStatuses.Successful;
            if (this.mainPlayer.Resources < ResourceClutch.RoadSegment)
                result |= BuildStatuses.NotEnoughResourcesForRoad;

            if (this.mainPlayer.RemainingRoadSegments == 0)
                result |= BuildStatuses.NoRoads;

            return result;
        }

        public BuildStatuses CanBuildSettlement()
        {
            BuildStatuses result = BuildStatuses.Successful;
            if (this.mainPlayer.Resources < ResourceClutch.Settlement)
                result |= BuildStatuses.NotEnoughResourcesForSettlement;

            if (this.mainPlayer.RemainingSettlements == 0)
                result |= BuildStatuses.NoSettlements;

            return result;
        }

        public BuyStatuses CanBuyDevelopmentCard()
        {
            if (!this.developmentCardHolder.HasCards)
                return BuyStatuses.NoCards;

            if (this.mainPlayer.Resources < ResourceClutch.DevelopmentCard)
                return BuyStatuses.NotEnoughResources;

            return BuyStatuses.Successful;
        }

        public void ChooseResourceFromOpponent(Guid opponentId)
        {
            if (this.GamePhase == GamePhases.NextStep)
            {
                var message = "Cannot call 'ChooseResourceFromOpponent' when 'RobbingChoicesEvent' is not raised.";
                var errorDetails = new ErrorDetails(message);
                this.ErrorRaisedEvent?.Invoke(errorDetails);
                return;
            }

            if (this.GamePhase != GamePhases.ChooseResourceFromOpponent)
            {
                var message = "Cannot call 'ChooseResourceFromOpponent' until 'SetRobberLocation' has completed.";
                var errorDetails = new ErrorDetails(message);
                this.ErrorRaisedEvent?.Invoke(errorDetails);
                return;
            }

            if (!this.robbingChoices.ContainsKey(opponentId))
            {
                var message = "Cannot pick resource card from invalid opponent.";
                var errorDetails = new ErrorDetails(message);
                this.ErrorRaisedEvent?.Invoke(errorDetails);
                return;
            }

            var opponent = this.playersById[opponentId];
            var takenResource = this.GetResourceFromPlayer(opponent);
            this.mainPlayer.AddResources(takenResource);

            var resourceTransactionList = new ResourceTransactionList();
            resourceTransactionList.Add(new ResourceTransaction(this.mainPlayer.Id, opponentId, takenResource));
            this.ResourcesTransferredEvent?.Invoke(resourceTransactionList);
        }

        // 05 Complete game setup
        public void CompleteGameSetup(uint settlementLocation, uint roadEndLocation)
        {
            if (this.GamePhase != GamePhases.CompleteGameSetup)
            {
                var errorDetails = new ErrorDetails("Cannot call 'CompleteGameSetup' until 'ContinueGameSetup' has completed.");
                this.ErrorRaisedEvent?.Invoke(errorDetails);
                return;
            }

            if (!this.VerifyStartingInfrastructurePlacementRequest(settlementLocation, roadEndLocation))
            {
                return;
            }

            this.gameBoard.PlaceStartingInfrastructure(this.mainPlayer.Id, settlementLocation, roadEndLocation);
            this.mainPlayer.PlaceStartingInfrastructure();
            this.CollectInitialResourcesForPlayer(this.mainPlayer.Id, settlementLocation);
            this.mainPlayer.AddResources(this.gameSetupResources.Resources[this.mainPlayer.Id]);

            GameBoardUpdate gameBoardUpdate = this.CompleteSetupForComputerPlayers(this.gameBoard, null);
            this.GameSetupUpdateEvent?.Invoke(gameBoardUpdate);

            this.GameSetupResourcesEvent?.Invoke(this.gameSetupResources);
            this.GamePhase = GamePhases.FinalisePlayerTurnOrder;
        }

        public void ContinueGamePlay()
        {
            if (this.GamePhase != GamePhases.ContinueGamePlay)
            {
                var errorDetails = new ErrorDetails("Can only call 'ContinueGamePlay' when loading from file.");
                this.ErrorRaisedEvent?.Invoke(errorDetails);
                return;
            }

            var playerData = this.CreatePlayerDataViews();
            this.GameJoinedEvent?.Invoke(playerData);
            this.InitialBoardSetupEvent?.Invoke(this.gameBoard);

            this.currentTurnToken = new TurnToken();
            this.StartPlayerTurnEvent?.Invoke(this.currentTurnToken);

            this.DiceRollEvent?.Invoke(this.dice1, this.dice2);
        }

        // 04 Continue game setup
        public void ContinueGameSetup(uint settlementLocation, uint roadEndLocation)
        {
            if (this.GamePhase != GamePhases.ContinueGameSetup)
            {
                var errorDetails = new ErrorDetails("Cannot call 'ContinueGameSetup' until 'StartGameSetup' has completed.");
                this.ErrorRaisedEvent?.Invoke(errorDetails);
                return;
            }

            if (!this.VerifyStartingInfrastructurePlacementRequest(settlementLocation, roadEndLocation))
            {
                return;
            }

            var gameBoardData = this.gameBoard;
            gameBoardData.PlaceStartingInfrastructure(this.mainPlayer.Id, settlementLocation, roadEndLocation);
            this.mainPlayer.PlaceStartingInfrastructure();

            GameBoardUpdate gameBoardUpdate = this.ContinueSetupForComputerPlayers(gameBoardData);

            this.playerIndex = this.players.Length - 1;
            gameBoardUpdate = this.CompleteSetupForComputerPlayers(gameBoardData, gameBoardUpdate);

            this.GameSetupUpdateEvent?.Invoke(gameBoardUpdate);
            this.GamePhase = GamePhases.CompleteGameSetup;
        }

        public void DropResources(ResourceClutch resourceClutch)
        {
            // TODO: Valid the parameter - the total should match the expected resources to drop
            // when robber roll occurred.
            this.mainPlayer.RemoveResources(resourceClutch);
        }

        public void EndTurn(TurnToken turnToken)
        {
            if (turnToken != this.currentTurnToken)
            {
                return;
            }

            this.ClearDevelopmentCardProcessingForTurn();
            this.ChangeToNextPlayer();

            while (this.currentPlayer.IsComputer)
            {
                var computerPlayer = this.currentPlayer as IComputerPlayer;

                computerPlayer.BuildInitialPlayerActions(null);
                var events = new List<GameEvent>();

                ComputerPlayerAction playerAction;
                while ((playerAction = computerPlayer.GetPlayerAction()) != null)
                {
                    switch (playerAction.Action)
                    {
                        case ComputerPlayerActionTypes.BuildCity:
                        {
                            var location = computerPlayer.ChooseCityLocation();
                            this.BuildCity(location);

                            events.Add(new CityBuiltEvent(computerPlayer.Id, location));

                            this.CheckComputerPlayerIsWinner(computerPlayer, events);

                            break;
                        }

                        case ComputerPlayerActionTypes.BuildRoadSegment:
                        {
                            var buildRoadAction = (BuildRoadSegmentAction)playerAction;
                            this.BuildRoadSegment(buildRoadAction.StartLocation, buildRoadAction.EndLocation);
                            events.Add(new RoadSegmentBuiltEvent(computerPlayer.Id, buildRoadAction.StartLocation, buildRoadAction.EndLocation));

                            Guid previousPlayerWithLongestRoadId;
                            if (this.PlayerHasJustBuiltTheLongestRoad(out previousPlayerWithLongestRoadId))
                            {
                                events.Add(new LongestRoadBuiltEvent(computerPlayer.Id, previousPlayerWithLongestRoadId));
                            }

                            this.CheckComputerPlayerIsWinner(computerPlayer, events);

                            break;
                        }

                        case ComputerPlayerActionTypes.BuildSettlement:
                        {
                            var location = computerPlayer.ChooseSettlementLocation();
                            this.BuildSettlement(location);

                            events.Add(new SettlementBuiltEvent(computerPlayer.Id, location));

                            this.CheckComputerPlayerIsWinner(computerPlayer, events);

                            break;
                        }

                        case ComputerPlayerActionTypes.BuyDevelopmentCard:
                        {
                            var developmentCard = this.BuyDevelopmentCard();
                            computerPlayer.AddDevelopmentCard(developmentCard);
                            events.Add(new BuyDevelopmentCardEvent(computerPlayer.Id));
                            break;
                        }

                        case ComputerPlayerActionTypes.PlayKnightCard:
                        {
                            var knightCard = computerPlayer.ChooseKnightCard();
                            var newRobberHex = computerPlayer.ChooseRobberLocation();
                            this.PlayKnightDevelopmentCard(knightCard, newRobberHex);
                            events.Add(new PlayKnightCardEvent(computerPlayer.Id));

                            var playersOnHex = this.gameBoard.GetPlayersForHex(newRobberHex);
                            if (playersOnHex != null)
                            {
                                var otherPlayers = this.GetPlayersFromIds(playersOnHex);
                                var robbedPlayer = computerPlayer.ChoosePlayerToRob(otherPlayers);
                                var takenResource = this.GetResourceFromPlayer(robbedPlayer);

                                computerPlayer.AddResources(takenResource);
                                var resourceTransaction = new ResourceTransaction(computerPlayer.Id, robbedPlayer.Id, takenResource);
                                var resourceLostEvent = new ResourceTransactionEvent(computerPlayer.Id, resourceTransaction);
                                events.Add(resourceLostEvent);
                            }

                            Guid previousPlayerId;
                            if (this.PlayerHasJustBuiltTheLargestArmy(out previousPlayerId))
                            {
                                events.Add(new LargestArmyChangedEvent(previousPlayerId, this.playerWithLargestArmy.Id));
                            }

                            this.CheckComputerPlayerIsWinner(computerPlayer, events);

                            break;
                        }

                        case ComputerPlayerActionTypes.PlayMonopolyCard:
                        {
                            var monopolyCard = computerPlayer.ChooseMonopolyCard();
                            var resourceType = computerPlayer.ChooseResourceTypeToRob();
                            var opponents = this.GetOpponentsForPlayer(computerPlayer);
                            var resourceTransations = this.GetAllResourcesFromOpponentsOfType(computerPlayer, opponents, resourceType);
                            if (resourceTransations != null)
                            {
                                this.AddResourcesToCurrentPlayer(computerPlayer, resourceTransations);
                            }

                            events.Add(new PlayMonopolyCardEvent(computerPlayer.Id, resourceTransations));
                            break;
                        }

                        case ComputerPlayerActionTypes.PlayYearOfPlentyCard:
                        {
                            var yearOfPlentyCard = computerPlayer.ChooseYearOfPlentyCard();
                            var resourcesCollected = computerPlayer.ChooseResourcesToCollectFromBank();
                            computerPlayer.AddResources(resourcesCollected);

                            var resourceTransaction = new ResourceTransaction(computerPlayer.Id, this.playerPool.GetBankId(), resourcesCollected);
                            var resourceTransactions = new ResourceTransactionList();
                            resourceTransactions.Add(resourceTransaction);

                            events.Add(new PlayYearOfPlentyCardEvent(computerPlayer.Id, resourceTransactions));
                            break;
                        }

                        case ComputerPlayerActionTypes.TradeWithBank:
                        {
                            var tradeWithBankAction = (TradeWithBankAction)playerAction;

                            var receivingResources = ResourceClutch.CreateFromResourceType(tradeWithBankAction.ReceivingType);
                            receivingResources *= tradeWithBankAction.ReceivingCount;

                            var paymentResources = ResourceClutch.CreateFromResourceType(tradeWithBankAction.GivingType);
                            paymentResources *= (tradeWithBankAction.ReceivingCount * 4);

                            computerPlayer.RemoveResources(paymentResources);
                            computerPlayer.AddResources(receivingResources);

                            events.Add(new TradeWithBankEvent(computerPlayer.Id, this.playerPool.GetBankId(), paymentResources, receivingResources));
                            break;
                        }

                        default: throw new NotImplementedException("Player action '" + playerAction + "' is not recognised.");
                    }
                }

                if (events.Count > 0)
                {
                    this.OpponentActionsEvent?.Invoke(computerPlayer.Id, events);
                }

                this.ClearDevelopmentCardProcessingForTurn();
                this.ChangeToNextPlayer();
            }

            this.currentTurnToken = new TurnToken();
            this.StartPlayerTurnEvent?.Invoke(this.currentTurnToken);

            this.numberGenerator.RollTwoDice(out var dice1, out var dice2);
            var resourceRoll = dice1 + dice2;
            this.DiceRollEvent?.Invoke(dice1, dice2);

            var turnResources = this.CollectTurnResources(resourceRoll);
            this.ResourcesCollectedEvent?.Invoke(turnResources);

            foreach (var kv in turnResources)
            {
                var player = this.playersById[kv.Key];
                foreach (var resourceCollection in kv.Value)
                {
                    player.AddResources(resourceCollection.Resources);
                }
            }
        }

        // 06 Finalise player turn order
        public void FinalisePlayerTurnOrder()
        {
            if (this.GamePhase != GamePhases.FinalisePlayerTurnOrder)
            {
                var errorDetails = new ErrorDetails("Cannot call 'FinalisePlayerTurnOrder' until 'CompleteGameSetup' has completed.");
                this.ErrorRaisedEvent?.Invoke(errorDetails);
                return;
            }

            // Set the order for the main game loop
            this.players = PlayerTurnOrderCreator.Create(this.players, this.numberGenerator);
            var playerData = this.CreatePlayerDataViews();
            this.TurnOrderFinalisedEvent?.Invoke(playerData);
            this.GamePhase = GamePhases.StartGamePlay;
        }

        public GameState GetState()
        {
            return new GameState();
        }

        // 01 Join game
        public void JoinGame(GameOptions gameOptions = null)
        {
            if (this.GamePhase != GamePhases.Initial)
            {
                var errorDetails = new ErrorDetails("Cannot call 'JoinGame' more than once.");
                this.ErrorRaisedEvent?.Invoke(errorDetails);
                return;
            }

            if (gameOptions == null)
            {
                gameOptions = new GameOptions { MaxPlayers = 1, MaxAIPlayers = 3 };
            }

            this.CreatePlayers(gameOptions);
            var playerData = this.CreatePlayerDataViews();
            this.GameJoinedEvent?.Invoke(playerData);
            this.GamePhase = GamePhases.WaitingLaunch;
        }

        // 02 Launch Game
        public void LaunchGame()
        {
            if (this.GamePhase != GamePhases.WaitingLaunch)
            {
                var errorDetails = new ErrorDetails("Cannot call 'LaunchGame' without joining game.");
                this.ErrorRaisedEvent?.Invoke(errorDetails);
                return;
            }

            this.InitialBoardSetupEvent?.Invoke(this.gameBoard);
            this.GamePhase = GamePhases.StartGameSetup;
        }

        /// <summary>
        /// Load the game controller data from stream.
        /// </summary>
        /// <param name="stream">Stream containing game controller data.</param>
        [Obsolete("Deprecated. Use Load(IGameDataReader<GameDataSectionKeys, GameDataValueKeys, ResourceTypes> reader) instead.")]
        public void Load(Stream stream)
        {
            try
            {
                var loadedPlayers = new List<IPlayer>();
                using (var reader = XmlReader.Create(stream, new XmlReaderSettings { CloseInput = false, IgnoreWhitespace = true, IgnoreComments = true }))
                {
                    while (!reader.EOF)
                    {
                        if (reader.Name == "player" && reader.NodeType == XmlNodeType.Element)
                        {
                            var player = this.playerPool.CreatePlayer(reader);
                            loadedPlayers.Add(player);
                        }

                        if (reader.Name == "resources" && reader.NodeType == XmlNodeType.Element)
                        {
                            this.gameBoard.LoadHexResources(reader);
                        }

                        if (reader.Name == "production" && reader.NodeType == XmlNodeType.Element)
                        {
                            this.gameBoard.LoadHexProduction(reader);
                        }

                        if (reader.Name == "settlements" && reader.NodeType == XmlNodeType.Element)
                        {
                            this.gameBoard.ClearSettlements();
                        }

                        if (reader.Name == "settlement" && reader.NodeType == XmlNodeType.Element)
                        {
                            var playerId = Guid.Parse(reader.GetAttribute("playerid"));
                            var location = uint.Parse(reader.GetAttribute("location"));

                            //this.gameBoard.InternalPlaceSettlement(playerId, location);
                        }

                        if (reader.Name == "roads" && reader.NodeType == XmlNodeType.Element)
                        {
                            this.gameBoard.ClearRoads();
                        }

                        if (reader.Name == "road" && reader.NodeType == XmlNodeType.Element)
                        {
                            var playerId = Guid.Parse(reader.GetAttribute("playerid"));
                            var start = uint.Parse(reader.GetAttribute("start"));
                            var end = uint.Parse(reader.GetAttribute("end"));

                            //this.gameBoard.InternalPlaceRoadSegment(playerId, start, end);
                        }

                        reader.Read();
                    }
                }

                if (loadedPlayers.Count > 0)
                {
                    this.mainPlayer = loadedPlayers[0];
                    this.players = new IPlayer[loadedPlayers.Count];
                    this.players[0] = this.mainPlayer;
                    this.playersById = new Dictionary<Guid, IPlayer>(this.players.Length);
                    this.playersById.Add(this.mainPlayer.Id, this.mainPlayer);

                    for (var index = 1; index < loadedPlayers.Count; index++)
                    {
                        var player = loadedPlayers[index];
                        this.players[index] = player;
                        this.playersById.Add(player.Id, player);
                    }
                }
                else
                {
                    this.CreatePlayers(new GameOptions());
                }

                var playerDataViews = this.CreatePlayerDataViews();

                this.GameLoadedEvent?.Invoke(playerDataViews, this.gameBoard);
            }
            catch (Exception e)
            {
                throw new Exception("Exception thrown during board loading.", e);
            }
        }

        public void Load(string filePath)
        {
            var content = File.ReadAllText(filePath);
            var gameModel = JsonConvert.DeserializeObject<GameModel>(content);

            this.gameBoard = new GameBoard(BoardSizes.Standard, gameModel.Board);

            this.computerPlayers = new IPlayer[3]; // TODO - Change to handle different number of computer players
            this.players = new IPlayer[4]; // TODO - Change to handle different number of players

            this.mainPlayer = this.players[0] = new Player(gameModel.Player1);
            this.computerPlayers[0] = this.players[1] = new ComputerPlayer(gameModel.Player2, this.numberGenerator);
            this.computerPlayers[1] = this.players[2] = new ComputerPlayer(gameModel.Player3, this.numberGenerator);
            this.computerPlayers[2] = this.players[3] = new ComputerPlayer(gameModel.Player4, this.numberGenerator);

            this.playersById = new Dictionary<Guid, IPlayer>(4);
            foreach (var player in this.players)
                this.playersById.Add(player.Id, player);

            this.dice1 = gameModel.Dice1;
            this.dice2 = gameModel.Dice2;

            this.GamePhase = GamePhases.ContinueGamePlay;
        }

        [Obsolete("Deprecated. Use Load(string filePath) instead.")]
        public void Load(IGameDataReader<GameDataSectionKeys, GameDataValueKeys, ResourceTypes> reader)
        {
            try
            {
                this.gameBoard.Load(reader);

                var loadedPlayers = new List<IPlayer>();

                var player = this.playerPool.CreatePlayer(reader[GameDataSectionKeys.PlayerOne]);
                loadedPlayers.Add(player);

                IGameDataSection<GameDataSectionKeys, GameDataValueKeys, ResourceTypes> data = null;
                var key = GameDataSectionKeys.PlayerTwo;
                while (key <= GameDataSectionKeys.PlayerFour && (data = reader[key++]) != null)
                {
                    player = this.playerPool.CreateComputerPlayer(data, this.gameBoard, this.numberGenerator);
                    loadedPlayers.Add(player);
                }

                if (loadedPlayers.Count > 0)
                {
                    this.mainPlayer = loadedPlayers[0];
                    this.players = new IPlayer[loadedPlayers.Count];
                    this.players[0] = this.mainPlayer;
                    this.playersById = new Dictionary<Guid, IPlayer>(this.players.Length);
                    this.playersById.Add(this.mainPlayer.Id, this.mainPlayer);

                    for (var index = 1; index < loadedPlayers.Count; index++)
                    {
                        player = loadedPlayers[index];
                        this.players[index] = player;
                        this.playersById.Add(player.Id, player);
                    }
                }
                else
                {
                    this.CreatePlayers(new GameOptions());
                }

                var playerDataViews = this.CreatePlayerDataViews();

                this.GameLoadedEvent?.Invoke(playerDataViews, this.gameBoard);
            }
            catch (Exception e)
            {
                throw new Exception("Exception thrown during board loading.", e);
            }
        }

        public void Quit()
        {
            this.GamePhase = GamePhases.Quitting;
        }

        public void Save(string filePath)
        {
            var gameModel = new GameModel();
            gameModel.Board = new GameBoardModel(this.gameBoard);
            gameModel.Player1 = new PlayerModel(this.mainPlayer);
            gameModel.Player2 = new PlayerModel(this.computerPlayers[0]);
            gameModel.Player3 = new PlayerModel(this.computerPlayers[1]);
            gameModel.Player4 = new PlayerModel(this.computerPlayers[2]);
            gameModel.RobberLocation = this.robberHex;
            gameModel.DevelopmentCards = this.developmentCardHolder.GetDevelopmentCards();
            gameModel.Dice1 = this.dice1;
            gameModel.Dice2 = this.dice2;
            var content = JsonConvert.SerializeObject(gameModel, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(filePath, content);
        }

        public void SetRobberHex(uint location)
        {
            if (this.GamePhase != GamePhases.SetRobberHex)
            {
                var resourceDropErrorDetails = new ErrorDetails(String.Format("Cannot set robber location until expected resources ({0}) have been dropped via call to DropResources method.", this.resourcesToDrop));
                this.ErrorRaisedEvent?.Invoke(resourceDropErrorDetails);
                return;
            }

            var playerIds = this.gameBoard.GetPlayersForHex(location);
            if (this.PlayerIdsIsEmptyOrOnlyContainsMainPlayer(playerIds))
            {
                this.GamePhase = GamePhases.NextStep;
                this.RobbingChoicesEvent?.Invoke(null);
                return;
            }

            this.robbingChoices = new Dictionary<Guid, int>();
            foreach (var playerId in playerIds)
            {
                this.robbingChoices.Add(playerId, this.playersById[playerId].ResourcesCount);
            }

            this.GamePhase = GamePhases.ChooseResourceFromOpponent;
            this.RobbingChoicesEvent?.Invoke(this.robbingChoices);
        }

        public void StartGamePlay()
        {
            if (this.GamePhase != GamePhases.StartGamePlay)
            {
                var errorDetails = new ErrorDetails("Cannot call 'StartGamePlay' until 'FinalisePlayerTurnOrder' has completed.");
                this.ErrorRaisedEvent?.Invoke(errorDetails);
                return;
            }

            this.playerIndex = 0;
            this.currentPlayer = this.players[this.playerIndex];
            this.currentTurnToken = new TurnToken();
            this.StartPlayerTurnEvent?.Invoke(this.currentTurnToken);

            this.numberGenerator.RollTwoDice(out this.dice1, out this.dice2);
            this.DiceRollEvent?.Invoke(this.dice1, this.dice2);

            var resourceRoll = this.dice1 + this.dice2;
            if (resourceRoll != 7)
            {
                var turnResources = this.CollectTurnResources(resourceRoll);
                this.ResourcesCollectedEvent?.Invoke(turnResources);

                foreach (var kv in turnResources)
                {
                    var player = this.playersById[kv.Key];
                    foreach (var resourceCollection in kv.Value)
                    {
                        player.AddResources(resourceCollection.Resources);
                    }
                }
            }
            else
            {
                ResourceUpdate resourcesDroppedByComputerPlayers = null;

                for (var index = 0; index < this.players.Length; index++)
                {
                    var player = this.players[index];

                    if (!player.IsComputer)
                    {
                        continue;
                    }

                    if (player.ResourcesCount > 7)
                    {
                        var computerPlayer = (IComputerPlayer)player;
                        var resourcesToDropByComputerPlayer = computerPlayer.ChooseResourcesToDrop();

                        if (resourcesDroppedByComputerPlayers == null)
                        {
                            resourcesDroppedByComputerPlayers = new ResourceUpdate();
                        }

                        resourcesDroppedByComputerPlayers.Resources.Add(computerPlayer.Id, resourcesToDropByComputerPlayer);
                    }
                }

                this.GamePhase = GamePhases.SetRobberHex;

                if (this.mainPlayer.ResourcesCount > 7)
                {
                    this.resourcesToDrop = this.mainPlayer.ResourcesCount / 2;
                    this.GamePhase = GamePhases.DropResources;
                }

                if (resourcesDroppedByComputerPlayers != null)
                {
                    this.ResourcesLostEvent?.Invoke(resourcesDroppedByComputerPlayers);
                }

                this.RobberEvent?.Invoke(this.resourcesToDrop);
                return;
            }
        }

        // 03 Start game setup
        public bool StartGameSetup()
        {
            if (this.GamePhase != GamePhases.StartGameSetup)
            {
                return false;
            }

            this.players = PlayerTurnOrderCreator.Create(this.players, this.numberGenerator);

            this.playerIndex = 0;
            GameBoardUpdate gameBoardUpdate = this.ContinueSetupForComputerPlayers(this.gameBoard);
            this.GameSetupUpdateEvent?.Invoke(gameBoardUpdate);
            this.GamePhase = GamePhases.ContinueGameSetup;

            return true;
        }

        /// <summary>
        /// Trade resources with the bank at a 4-to-1 ratio. Errors will be returned if the transaction cannot be completed.
        /// </summary>
        /// <param name="turnToken">Token of the current turn.</param>
        /// <param name="receivingResourceType">Resource type that the player wants to receive.</param>
        /// <param name="receivingCount">Resource amount that the player wants to receive.</param>
        /// <param name="givingResourceType">Resource type that the player is giving. Must have at least 4 and be divisible by 4.</param>
        public void TradeWithBank(TurnToken turnToken, ResourceTypes receivingResourceType, int receivingCount, ResourceTypes givingResourceType)
        {
            if (!this.VerifyTurnToken(turnToken))
            {
                return;
            }

            int resourceCount = 0;
            switch (givingResourceType)
            {
                case ResourceTypes.Brick: resourceCount = this.mainPlayer.BrickCount; break;
                case ResourceTypes.Grain: resourceCount = this.mainPlayer.GrainCount; break;
                case ResourceTypes.Lumber: resourceCount = this.mainPlayer.LumberCount; break;
                case ResourceTypes.Ore: resourceCount = this.mainPlayer.OreCount; break;
                case ResourceTypes.Wool: resourceCount = this.mainPlayer.WoolCount; break;
            }

            if (!this.VerifyTradeWithBank(receivingCount, resourceCount, givingResourceType, receivingResourceType))
            {
                return;
            }

            var receivingResource = ResourceClutch.CreateFromResourceType(receivingResourceType);
            receivingResource *= receivingCount;

            var paymentResource = ResourceClutch.CreateFromResourceType(givingResourceType);
            paymentResource *= (receivingCount * 4);

            this.mainPlayer.RemoveResources(paymentResource);
            this.mainPlayer.AddResources(receivingResource);

            var resourceTransactionList = new ResourceTransactionList();
            resourceTransactionList.Add(new ResourceTransaction(this.playerPool.GetBankId(), this.mainPlayer.Id, paymentResource));
            resourceTransactionList.Add(new ResourceTransaction(this.mainPlayer.Id, this.playerPool.GetBankId(), receivingResource));

            this.ResourcesTransferredEvent?.Invoke(resourceTransactionList);
        }

        public void UseKnightCard(TurnToken turnToken, KnightDevelopmentCard developmentCard, uint newRobberHex)
        {
            this.ProcessKnightCard(turnToken, developmentCard, newRobberHex, null);
        }

        public void UseKnightCard(TurnToken turnToken, KnightDevelopmentCard developmentCard, uint newRobberHex, Guid playerId)
        {
            this.ProcessKnightCard(turnToken, developmentCard, newRobberHex, playerId);
        }

        public void UseMonopolyCard(TurnToken turnToken, MonopolyDevelopmentCard monopolyCard, ResourceTypes resourceType)
        {
            if (!this.VerifyParametersForUsingDevelopmentCard(turnToken, monopolyCard, "monopoly"))
            {
                return;
            }

            var opponents = this.GetOpponentsForPlayer(this.mainPlayer);
            var resourceTransactions = this.GetAllResourcesFromOpponentsOfType(this.mainPlayer, opponents, resourceType);
            if (resourceTransactions != null)
            {
                this.AddResourcesToCurrentPlayer(this.mainPlayer, resourceTransactions);
            }

            this.PlayDevelopmentCard(monopolyCard);

            this.ResourcesTransferredEvent?.Invoke(resourceTransactions);
        }

        public void UseYearOfPlentyCard(TurnToken turnToken, YearOfPlentyDevelopmentCard yearOfPlentyCard, ResourceTypes firstChoice, ResourceTypes secondChoice)
        {
            if (!this.VerifyParametersForUsingDevelopmentCard(turnToken, yearOfPlentyCard, "year of plenty"))
            {
                return;
            }

            var resources = ResourceClutch.Zero;
            foreach (var resourceChoice in new[] { firstChoice, secondChoice })
            {
                switch (resourceChoice)
                {
                    case ResourceTypes.Brick: resources.BrickCount++; break;
                    case ResourceTypes.Lumber: resources.LumberCount++; break;
                    case ResourceTypes.Grain: resources.GrainCount++; break;
                    case ResourceTypes.Ore: resources.OreCount++; break;
                    case ResourceTypes.Wool: resources.WoolCount++; break;
                }
            }

            this.mainPlayer.AddResources(resources);

            this.PlayDevelopmentCard(yearOfPlentyCard);

            var resourceTransactions = new ResourceTransactionList();
            resourceTransactions.Add(new ResourceTransaction(this.mainPlayer.Id, this.playerPool.GetBankId(), resources));
            this.ResourcesTransferredEvent?.Invoke(resourceTransactions);
        }

        private void AddResourcesToList(List<ResourceTypes> resources, ResourceTypes resourceType, int total)
        {
            for (var i = 0; i < total; i++)
            {
                resources.Add(resourceType);
            }
        }

        private void AddResourcesToCurrentPlayer(IPlayer player, ResourceTransactionList resourceTransactions)
        {
            for (var i = 0; i < resourceTransactions.Count; i++)
            {
                player.AddResources(resourceTransactions[i].Resources);
            }
        }

        private void AddResourcesToPlayer(ResourceTransactionList resourceTransactions)
        {
            for (var i = 0; i < resourceTransactions.Count; i++)
            {
                var resourceTransaction = resourceTransactions[i];
                var player = this.playersById[resourceTransaction.ReceivingPlayerId];
                player.AddResources(resourceTransaction.Resources);
            }
        }

        private void BuildCity(uint location)
        {
            this.gameBoard.PlaceCity(this.currentPlayer.Id, location);
            this.currentPlayer.PlaceCity();
        }

        private void BuildRoadSegment(uint roadStartLocation, uint roadEndLocation)
        {
            this.gameBoard.PlaceRoadSegment(this.currentPlayer.Id, roadStartLocation, roadEndLocation);
            this.currentPlayer.PlaceRoadSegment();
        }

        private void BuildSettlement(uint location)
        {
            this.gameBoard.PlaceSettlement(this.currentPlayer.Id, location);
            this.currentPlayer.PlaceSettlement();
        }

        private DevelopmentCard BuyDevelopmentCard()
        {
            DevelopmentCard developmentCard;
            this.developmentCardHolder.TryGetNextCard(out developmentCard);
            this.currentPlayer.PayForDevelopmentCard();
            this.cardsPurchasedThisTurn.Add(developmentCard);
            return developmentCard;
        }

        private void ChangeToNextPlayer()
        {
            this.playerIndex++;
            if (this.playerIndex == this.players.Length)
            {
                this.playerIndex = 0;
            }

            this.currentPlayer = this.players[this.playerIndex];
        }

        private void CheckComputerPlayerIsWinner(IComputerPlayer computerPlayer, List<GameEvent> events)
        {
            if (computerPlayer.VictoryPoints >= 10)
            {
                events.Add(new GameWinEvent(computerPlayer.Id));
                this.GamePhase = GamePhases.GameOver;
            }
        }

        private void CheckMainPlayerIsWinner()
        {
            if (this.mainPlayer.VictoryPoints >= 10)
            {
                this.GameOverEvent?.Invoke(this.mainPlayer.Id);
                this.GamePhase = GamePhases.GameOver;
            }
        }

        private Dictionary<Guid, ResourceCollection[]> CollectTurnResources(uint diceRoll)
        {
            return this.gameBoard.GetResourcesForRoll(diceRoll);
        }

        private void CollectInitialResourcesForPlayer(Guid playerId, uint settlementLocation)
        {
            if (this.gameSetupResources == null)
            {
                this.gameSetupResources = new ResourceUpdate();
            }

            var resources = this.gameBoard.GetResourcesForLocation(settlementLocation);
            this.gameSetupResources.Resources.Add(playerId, resources);
        }

        private GameBoardUpdate ContinueSetupForComputerPlayers(GameBoard gameBoardData)
        {
            GameBoardUpdate gameBoardUpdate = null;

            while (this.playerIndex < this.players.Length)
            {
                var player = this.players[this.playerIndex++];

                if (!player.IsComputer)
                {
                    return gameBoardUpdate;
                }

                if (gameBoardUpdate == null)
                {
                    gameBoardUpdate = new GameBoardUpdate
                    {
                        NewSettlements = new List<Tuple<uint, Guid>>(),
                        NewRoads = new List<Tuple<uint, uint, Guid>>()
                    };
                }

                var computerPlayer = (IComputerPlayer)player;
                uint chosenSettlementLocation, chosenRoadSegmentEndLocation;
                computerPlayer.ChooseInitialInfrastructure(out chosenSettlementLocation, out chosenRoadSegmentEndLocation);
                gameBoardData.PlaceStartingInfrastructure(computerPlayer.Id, chosenSettlementLocation, chosenRoadSegmentEndLocation);

                computerPlayer.PlaceStartingInfrastructure();

                gameBoardUpdate.NewSettlements.Add(new Tuple<uint, Guid>(chosenSettlementLocation, computerPlayer.Id));
                gameBoardUpdate.NewRoads.Add(new Tuple<uint, uint, Guid>(chosenSettlementLocation, chosenRoadSegmentEndLocation, computerPlayer.Id));
            }

            return gameBoardUpdate;
        }

        private void CompleteResourceTransactionBetweenPlayers(IPlayer playerToTakeResourceFrom)
        {
            var takenResource = this.GetResourceFromPlayer(playerToTakeResourceFrom);
            this.mainPlayer.AddResources(takenResource);

            var resourceTransactionList = new ResourceTransactionList();
            resourceTransactionList.Add(new ResourceTransaction(this.mainPlayer.Id, playerToTakeResourceFrom.Id, takenResource));

            this.ResourcesTransferredEvent?.Invoke(resourceTransactionList);
        }

        private GameBoardUpdate CompleteSetupForComputerPlayers(GameBoard gameBoardData, GameBoardUpdate gameBoardUpdate)
        {
            while (this.playerIndex >= 0)
            {
                var player = this.players[this.playerIndex--];

                if (!player.IsComputer)
                {
                    return gameBoardUpdate;
                }

                if (gameBoardUpdate == null)
                {
                    gameBoardUpdate = new GameBoardUpdate
                    {
                        NewSettlements = new List<Tuple<uint, Guid>>(),
                        NewRoads = new List<Tuple<uint, uint, Guid>>()
                    };
                }

                var computerPlayer = (IComputerPlayer)player;
                uint chosenSettlementLocation, chosenRoadSegmentEndLocation;
                computerPlayer.ChooseInitialInfrastructure(out chosenSettlementLocation, out chosenRoadSegmentEndLocation);
                gameBoardData.PlaceStartingInfrastructure(computerPlayer.Id, chosenSettlementLocation, chosenRoadSegmentEndLocation);

                computerPlayer.PlaceStartingInfrastructure();

                gameBoardUpdate.NewSettlements.Add(new Tuple<uint, Guid>(chosenSettlementLocation, computerPlayer.Id));
                gameBoardUpdate.NewRoads.Add(new Tuple<uint, uint, Guid>(chosenSettlementLocation, chosenRoadSegmentEndLocation, computerPlayer.Id));

                this.CollectInitialResourcesForPlayer(computerPlayer.Id, chosenSettlementLocation);
                computerPlayer.AddResources(this.gameSetupResources.Resources[computerPlayer.Id]);
            }

            return gameBoardUpdate;
        }

        private PlayerDataModel[] CreatePlayerDataViews()
        {
            var playerDataViews = new PlayerDataModel[this.players.Length];

            for (var index = 0; index < playerDataViews.Length; index++)
            {
                playerDataViews[index] = this.players[index].GetDataModel();
            }

            return playerDataViews;
        }

        private void CreatePlayers(GameOptions gameOptions)
        {
            this.mainPlayer = this.playerPool.CreatePlayer();
            this.players = new IPlayer[gameOptions.MaxAIPlayers + 1];
            this.players[0] = this.mainPlayer;
            this.playersById = new Dictionary<Guid, IPlayer>(this.players.Length);
            this.playersById.Add(this.mainPlayer.Id, this.mainPlayer);
            this.computerPlayers = new IPlayer[gameOptions.MaxAIPlayers];

            var index = 1;
            while ((gameOptions.MaxAIPlayers--) > 0)
            {
                var computerPlayer = this.playerPool.CreateComputerPlayer(this.gameBoard, this.numberGenerator);
                this.players[index] = computerPlayer;
                this.playersById.Add(computerPlayer.Id, computerPlayer);
                this.computerPlayers[index - 1] = computerPlayer;
                index++;
            }
        }

        private IPlayer DeterminePlayerWithMostKnightCards()
        {
            IPlayer playerWithMostKnightCards = null;
            uint workingKnightCardCount = 3;

            foreach (var player in this.players)
            {
                if (player.KnightCards > workingKnightCardCount)
                {
                    playerWithMostKnightCards = player;
                    workingKnightCardCount = player.KnightCards;
                }
                else if (player.KnightCards == workingKnightCardCount)
                {
                    playerWithMostKnightCards = (playerWithMostKnightCards == null ? player : null);
                }
            }

            return playerWithMostKnightCards;
        }

        private void ClearDevelopmentCardProcessingForTurn()
        {
            this.cardsPurchasedThisTurn.Clear();
            this.cardPlayedThisTurn = false;
        }

        private ResourceTransactionList GetAllResourcesFromOpponentsOfType(IPlayer player, IEnumerable<IPlayer> opponents, ResourceTypes resourceType)
        {
            ResourceTransactionList transactionList = null;
            foreach (var opponent in opponents)
            {
                var resources = opponent.LoseResourcesOfType(resourceType);

                if (resources != ResourceClutch.Zero)
                {
                    if (transactionList == null)
                    {
                        transactionList = new ResourceTransactionList();
                    }

                    transactionList.Add(new ResourceTransaction(player.Id, opponent.Id, resources));
                }
            }

            return transactionList;
        }

        private IEnumerable<IPlayer> GetOpponentsForPlayer(IPlayer player)
        {
            var opponents = new List<IPlayer>(3);
            foreach (var opponent in this.players)
            {
                if (opponent != player)
                {
                    opponents.Add(opponent);
                }
            }

            return opponents;
        }

        private IEnumerable<IPlayer> GetPlayersFromIds(Guid[] playerIds)
        {
            var playerList = new List<IPlayer>();
            foreach (var playerId in playerIds)
            {
                playerList.Add(this.playersById[playerId]);
            }

            return playerList;
        }

        private ResourceClutch GetResourceFromPlayer(IPlayer player)
        {
            var resourceIndex = this.numberGenerator.GetRandomNumberBetweenZeroAndMaximum(player.ResourcesCount);
            return player.LoseResourceAtIndex(resourceIndex);
        }

        private void HandlePlaceRoadError(GameBoard.VerificationStatus status)
        {
            var message = String.Empty;
            switch (status)
            {
                case GameBoard.VerificationStatus.RoadIsOffBoard: message = "Cannot place road segment because board location is not valid."; break;
                case GameBoard.VerificationStatus.RoadIsOccupied: message = "Cannot place road segment because road segment already exists."; break;
                case GameBoard.VerificationStatus.NoDirectConnection: message = "Cannot build road segment because no direct connection between start location and end location."; break;
                case GameBoard.VerificationStatus.RoadNotConnectedToExistingRoad: message = "Cannot place road segment because it is not connected to an existing road segment."; break;
                default: message = "Road build segment status not recognised: " + status; break;
            }

            this.ErrorRaisedEvent?.Invoke(new ErrorDetails(message));
        }

        private void LoadHexes()
        {

        }

        private void PlayDevelopmentCard(DevelopmentCard developmentCard)
        {
            this.cardPlayedThisTurn = true;
            this.cardsPlayed.Add(developmentCard);
        }

        private Boolean PlayerHasJustBuiltTheLargestArmy(out Guid previousPlayerId)
        {
            previousPlayerId = Guid.Empty;
            var playerWithMostKnightCards = this.DeterminePlayerWithMostKnightCards();
            if (playerWithMostKnightCards == this.currentPlayer && this.playerWithLargestArmy != this.currentPlayer)
            {
                previousPlayerId = Guid.Empty;

                if (this.playerWithLargestArmy != null)
                {
                    this.playerWithLargestArmy.HasLargestArmy = false;
                    previousPlayerId = this.playerWithLargestArmy.Id;
                }

                this.playerWithLargestArmy = playerWithMostKnightCards;
                this.playerWithLargestArmy.HasLargestArmy = true;
                return true;
            }

            return false;
        }

        private Boolean PlayerHasJustBuiltTheLongestRoad(out Guid previousPlayerId)
        {
            previousPlayerId = Guid.Empty;
            if (this.currentPlayer.RoadSegmentsBuilt < 5)
            {
                return false;
            }

            Guid longestRoadPlayerId = Guid.Empty;
            uint[] road = null;
            if (this.gameBoard.TryGetLongestRoadDetails(out longestRoadPlayerId, out road) && road.Length > 5)
            {
                var longestRoadPlayer = this.playersById[longestRoadPlayerId];
                if (longestRoadPlayer == this.currentPlayer && this.playerWithLongestRoad != longestRoadPlayer)
                {
                    previousPlayerId = Guid.Empty;

                    if (this.playerWithLongestRoad != null)
                    {
                        this.playerWithLongestRoad.HasLongestRoad = false;
                        previousPlayerId = this.playerWithLongestRoad.Id;
                    }

                    this.playerWithLongestRoad = longestRoadPlayer;
                    this.playerWithLongestRoad.HasLongestRoad = true;
                    return true;
                }
            }

            return false;
        }

        private Boolean PlayerIdsIsEmptyOrOnlyContainsMainPlayer(Guid[] playerIds)
        {
            return playerIds == null || playerIds.Length == 0 ||
                   (playerIds.Length == 1 && playerIds[0] == this.mainPlayer.Id);
        }

        private void PlayKnightDevelopmentCard(KnightDevelopmentCard developmentCard, uint newRobberHex)
        {
            this.PlayDevelopmentCard(developmentCard);
            this.currentPlayer.PlaceKnightDevelopmentCard();
            this.robberHex = newRobberHex;
        }

        private void ProcessKnightCard(TurnToken turnToken, KnightDevelopmentCard developmentCard, uint newRobberHex, Guid? playerId)
        {
            if (!this.VerifyParametersForUsingDevelopmentCard(turnToken, developmentCard, "knight"))
            {
                return;
            }

            if (!this.VerifyPlacementOfRobber(newRobberHex))
            {
                return;
            }

            if (playerId.HasValue && !this.VerifyPlayerForResourceTransactionWhenUsingKnightCard(newRobberHex, playerId.Value))
            {
                return;
            }

            this.PlayKnightDevelopmentCard(developmentCard, newRobberHex);

            if (playerId.HasValue)
            {
                this.CompleteResourceTransactionBetweenPlayers(this.playersById[playerId.Value]);
            }

            Guid previousPlayerId;
            if (this.PlayerHasJustBuiltTheLargestArmy(out previousPlayerId))
            {
                this.LargestArmyEvent?.Invoke(previousPlayerId, this.playerWithLargestArmy.Id);
            }

            this.CheckMainPlayerIsWinner();
        }

        private void TryRaiseRoadSegmentBuildingError()
        {
            if (this.currentPlayer.RemainingRoadSegments == 0)
            {
                this.ErrorRaisedEvent?.Invoke(new ErrorDetails("Cannot build road segment. All road segments already built."));
                return;
            }

            if (this.currentPlayer.BrickCount == 0 && this.currentPlayer.LumberCount == 0)
            {
                this.ErrorRaisedEvent?.Invoke(new ErrorDetails("Cannot build road segment. Missing 1 brick and 1 lumber."));
                return;
            }

            if (this.currentPlayer.BrickCount == 0)
            {
                this.ErrorRaisedEvent?.Invoke(new ErrorDetails("Cannot build road segment. Missing 1 brick."));
                return;
            }

            if (this.currentPlayer.LumberCount == 0)
            {
                this.ErrorRaisedEvent?.Invoke(new ErrorDetails("Cannot build road segment. Missing 1 lumber."));
                return;
            }
        }

        private Boolean VerifyBuildCityRequest(uint location)
        {
            if (this.GamePhase == GamePhases.GameOver)
            {
                this.ErrorRaisedEvent?.Invoke(new ErrorDetails("Cannot build city. Game is over."));
                return false;
            }

            if (this.currentPlayer.RemainingCities == 0)
            {
                this.ErrorRaisedEvent?.Invoke(new ErrorDetails("Cannot build city. All cities already built."));
                return false;
            }

            if (this.currentPlayer.GrainCount < Constants.GrainForBuildingCity && this.currentPlayer.OreCount < Constants.OreForBuildingCity)
            {
                var missingGrainCount = (Constants.GrainForBuildingCity - this.currentPlayer.GrainCount);
                var missingOreCount = (Constants.OreForBuildingCity - this.currentPlayer.OreCount);
                this.ErrorRaisedEvent?.Invoke(new ErrorDetails("Cannot build city. Missing " + missingGrainCount + " grain and " + missingOreCount + " ore."));
                return false;
            }

            if (this.currentPlayer.GrainCount < Constants.GrainForBuildingCity)
            {
                var missingGrainCount = (Constants.GrainForBuildingCity - this.currentPlayer.GrainCount);
                this.ErrorRaisedEvent?.Invoke(new ErrorDetails("Cannot build city. Missing " + missingGrainCount + " grain."));
                return false;
            }

            if (this.currentPlayer.OreCount < Constants.OreForBuildingCity)
            {
                var missingOreCount = (Constants.OreForBuildingCity - this.currentPlayer.OreCount);
                this.ErrorRaisedEvent?.Invoke(new ErrorDetails("Cannot build city. Missing " + missingOreCount + " ore."));
                return false;
            }

            var placeCityResults = this.gameBoard.CanPlaceCity(this.currentPlayer.Id, location);

            if (placeCityResults.Status == GameBoard.VerificationStatus.LocationForCityIsInvalid)
            {
                this.ErrorRaisedEvent?.Invoke(new ErrorDetails("Cannot build city. Location " + location + " is outside of board range (0 - 53)."));
                return false;
            }

            if (placeCityResults.Status == GameBoard.VerificationStatus.LocationIsNotOwned)
            {
                var player = this.playersById[placeCityResults.PlayerId];
                this.ErrorRaisedEvent?.Invoke(new ErrorDetails("Cannot build city. Location " + location + " is owned by player '" + player.Name + "'."));
                return false;
            }

            if (placeCityResults.Status == GameBoard.VerificationStatus.LocationIsNotSettled)
            {
                this.ErrorRaisedEvent?.Invoke(new ErrorDetails("Cannot build city. No settlement at location " + location + "."));
                return false;
            }

            if (placeCityResults.Status == GameBoard.VerificationStatus.LocationIsAlreadyCity)
            {
                var player = this.playersById[placeCityResults.PlayerId];
                if (player == this.currentPlayer)
                {
                    this.ErrorRaisedEvent?.Invoke(new ErrorDetails("Cannot build city. There is already a city at location " + location + " that belongs to you."));
                }
                else
                {
                    this.ErrorRaisedEvent?.Invoke(new ErrorDetails("Cannot build city. There is already a city at location " + location + " belonging to '" + player.Name + "'."));
                }

                return false;
            }

            return true;
        }

        private bool VerifyBuildRoadSegmentRequest(uint roadStartLocation, uint roadEndLocation)
        {
            if (this.GamePhase == GamePhases.GameOver)
            {
                this.ErrorRaisedEvent?.Invoke(new ErrorDetails("Cannot build road segment. Game is over."));
                return false;
            }

            if (this.CanBuildRoadSegment() != BuildStatuses.Successful)
            {
                this.TryRaiseRoadSegmentBuildingError();
                return false;
            }

            if (!this.VerifyRoadSegmentPlacing(roadStartLocation, roadEndLocation))
            {
                return false;
            }

            return true;
        }

        private Boolean VerifyBuildSettlementRequest(uint settlementLocation)
        {
            if (this.GamePhase == GamePhases.GameOver)
            {
                ErrorRaisedEvent?.Invoke(new ErrorDetails("Cannot build settlement. Game is over."));
                return false;
            }

            return this.VerifySettlementBuilding() && this.VerifySettlementPlacing(settlementLocation);
        }

        private Boolean VerifyBuyDevelopmentCardRequest()
        {
            if (this.GamePhase == GamePhases.GameOver)
            {
                this.ErrorRaisedEvent?.Invoke(new ErrorDetails("Cannot buy development card. Game is over."));
                return false;
            }

            if (!this.developmentCardHolder.HasCards)
            {
                this.ErrorRaisedEvent?.Invoke(new ErrorDetails("Cannot buy development card. No more cards available"));
                return false;
            }

            if (this.currentPlayer.GrainCount < 1 && this.currentPlayer.OreCount < 1 && this.currentPlayer.WoolCount < 1)
            {
                this.ErrorRaisedEvent?.Invoke(new ErrorDetails("Cannot buy development card. Missing 1 grain and 1 ore and 1 wool."));
                return false;
            }

            if (this.currentPlayer.GrainCount < 1 && this.currentPlayer.OreCount < 1)
            {
                this.ErrorRaisedEvent?.Invoke(new ErrorDetails("Cannot buy development card. Missing 1 grain and 1 ore."));
                return false;
            }

            if (this.currentPlayer.GrainCount < 1 && this.currentPlayer.WoolCount < 1)
            {
                this.ErrorRaisedEvent?.Invoke(new ErrorDetails("Cannot buy development card. Missing 1 grain and 1 wool."));
                return false;
            }

            if (this.currentPlayer.OreCount < 1 && this.currentPlayer.WoolCount < 1)
            {
                this.ErrorRaisedEvent?.Invoke(new ErrorDetails("Cannot buy development card. Missing 1 ore and 1 wool."));
                return false;
            }

            if (this.currentPlayer.GrainCount < 1)
            {
                this.ErrorRaisedEvent?.Invoke(new ErrorDetails("Cannot buy development card. Missing 1 grain."));
                return false;
            }

            if (this.currentPlayer.OreCount < 1)
            {
                this.ErrorRaisedEvent?.Invoke(new ErrorDetails("Cannot buy development card. Missing 1 ore."));
                return false;
            }

            if (this.currentPlayer.WoolCount < 1)
            {
                this.ErrorRaisedEvent?.Invoke(new ErrorDetails("Cannot buy development card. Missing 1 wool."));
                return false;
            }

            return true;
        }

        private Boolean VerifyPlayerForResourceTransactionWhenUsingKnightCard(uint newRobberHex, Guid playerId)
        {
            var playerIdsOnHex = new List<Guid>(this.gameBoard.GetPlayersForHex(newRobberHex));
            if (!playerIdsOnHex.Contains(playerId))
            {
                this.ErrorRaisedEvent?.Invoke(new ErrorDetails("Player Id (" + playerId + ") does not match with any players on hex " + newRobberHex + "."));
                return false;
            }

            return true;
        }

        private Boolean VerifyParametersForUsingDevelopmentCard(TurnToken turnToken, DevelopmentCard developmentCard, String shortCardType)
        {
            if (!this.VerifyTurnToken(turnToken))
            {
                return false;
            }

            if (this.GamePhase == GamePhases.GameOver)
            {
                this.ErrorRaisedEvent?.Invoke(new ErrorDetails("Cannot use " + shortCardType + " card. Game is over."));
                return false;
            }

            if (developmentCard == null)
            {
                this.ErrorRaisedEvent?.Invoke(new ErrorDetails("Development card parameter is null."));
                return false;
            }

            if (cardsPurchasedThisTurn.Contains(developmentCard))
            {
                this.ErrorRaisedEvent?.Invoke(new ErrorDetails("Cannot use development card that has been purchased this turn."));
                return false;
            }

            if (this.cardPlayedThisTurn)
            {
                this.ErrorRaisedEvent?.Invoke(new ErrorDetails("Cannot play more than one development card in a turn."));
                return false;
            }

            if (this.cardsPlayed.Contains(developmentCard))
            {
                this.ErrorRaisedEvent?.Invoke(new ErrorDetails("Cannot play the same development card more than once."));
                return false;
            }

            return true;
        }

        private Boolean VerifyPlacementOfRobber(uint newRobberHex)
        {
            if (!this.gameBoard.CanPlaceRobber(newRobberHex))
            {
                this.ErrorRaisedEvent?.Invoke(new ErrorDetails("Cannot move robber to hex " + newRobberHex + " because it is out of bounds (0.. " + (GameBoard.StandardBoardHexCount - 1) + ")."));
                return false;
            }

            if (newRobberHex == this.robberHex)
            {
                this.ErrorRaisedEvent?.Invoke(new ErrorDetails("Cannot place robber back on present hex (" + this.robberHex + ")."));
                return false;
            }

            return true;
        }

        private Boolean VerifyRoadSegmentPlacing(uint settlementLocation, uint roadEndLocation)
        {
            var placeRoadStatus = this.gameBoard.CanPlaceRoad(this.currentPlayer.Id, settlementLocation, roadEndLocation);
            return this.VerifyRoadSegmentPlacing(placeRoadStatus, settlementLocation, roadEndLocation);
        }

        private Boolean VerifyRoadSegmentPlacing(GameBoard.VerificationResults verificationResults, uint settlementLocation, uint roadEndLocation)
        {
            if (verificationResults.Status == GameBoard.VerificationStatus.RoadIsOffBoard)
            {
                this.ErrorRaisedEvent?.Invoke(new ErrorDetails("Cannot build road segment. Locations " + settlementLocation + " and/or " + roadEndLocation + " are outside of board range (0 - 53)."));
                return false;
            }

            if (verificationResults.Status == GameBoard.VerificationStatus.NoDirectConnection)
            {
                this.ErrorRaisedEvent?.Invoke(new ErrorDetails("Cannot build road segment. No direct connection between locations [" + settlementLocation + ", " + roadEndLocation + "]."));
                return false;
            }

            if (verificationResults.Status == GameBoard.VerificationStatus.RoadIsOccupied)
            {
                this.ErrorRaisedEvent?.Invoke(new ErrorDetails("Cannot build road segment. Road segment between " + settlementLocation + " and " + roadEndLocation + " already exists."));
                return false;
            }

            if (verificationResults.Status == GameBoard.VerificationStatus.RoadNotConnectedToExistingRoad)
            {
                this.ErrorRaisedEvent?.Invoke(new ErrorDetails("Cannot build road segment. Road segment [" + settlementLocation + ", " + roadEndLocation + "] not connected to existing road segment."));
                return false;
            }

            return true;
        }

        private Boolean VerifySettlementBuilding()
        {
            if (this.mainPlayer.RemainingSettlements == 0)
            {
                this.ErrorRaisedEvent?.Invoke(new ErrorDetails("Cannot build settlement. All settlements already built."));
                return false;
            }

            String message = null;
            if (this.mainPlayer.BrickCount == 0)
            {
                message += "1 brick and ";
            }

            if (this.mainPlayer.GrainCount == 0)
            {
                message += "1 grain and ";
            }

            if (this.mainPlayer.LumberCount == 0)
            {
                message += "1 lumber and ";
            }

            if (this.mainPlayer.WoolCount == 0)
            {
                message += "1 wool and ";
            }

            if (message != null)
            {
                message = message.Substring(0, message.Length - " and ".Length);
                message += ".";
                message = "Cannot build settlement. Missing " + message;
                this.ErrorRaisedEvent?.Invoke(new ErrorDetails(message));
                return false;
            }

            return true;
        }

        private Boolean VerifySettlementPlacing(uint settlementLocation)
        {
            var verificationResults = this.gameBoard.CanPlaceSettlement(this.mainPlayer.Id, settlementLocation);
            return this.VerifySettlementPlacing(verificationResults, settlementLocation);
        }

        private Boolean VerifySettlementPlacing(GameBoard.VerificationResults verificationResults, uint settlementLocation)
        {
            if (verificationResults.Status == GameBoard.VerificationStatus.LocationForSettlementIsInvalid)
            {
                this.ErrorRaisedEvent(new ErrorDetails("Cannot build settlement. Location " + settlementLocation + " is outside of board range (0 - 53)."));
                return false;
            }

            if (verificationResults.Status == GameBoard.VerificationStatus.TooCloseToSettlement)
            {
                var player = this.playersById[verificationResults.PlayerId];
                if (player == this.currentPlayer)
                {
                    this.ErrorRaisedEvent(new ErrorDetails("Cannot build settlement. Too close to own settlement at location " + verificationResults.LocationIndex + "."));
                }
                else
                {
                    this.ErrorRaisedEvent(new ErrorDetails("Cannot build settlement. Too close to player '" + player.Name + "' at location " + verificationResults.LocationIndex + "."));
                }

                return false;
            }

            if (verificationResults.Status == GameBoard.VerificationStatus.LocationIsOccupied)
            {
                var player = this.playersById[verificationResults.PlayerId];
                if (player == this.currentPlayer)
                {
                    this.ErrorRaisedEvent(new ErrorDetails("Cannot build settlement. Location " + verificationResults.LocationIndex + " already settled by you."));
                }
                else
                {
                    this.ErrorRaisedEvent(new ErrorDetails("Cannot build settlement. Location " + settlementLocation + " already settled by player '" + player.Name + "'."));
                }

                return false;
            }

            if (verificationResults.Status == GameBoard.VerificationStatus.SettlementNotConnectedToExistingRoad)
            {
                this.ErrorRaisedEvent(new ErrorDetails("Cannot build settlement. Location " + verificationResults.LocationIndex + " not connected to existing road."));
                return false;
            }

            return true;
        }

        private Boolean VerifyStartingInfrastructurePlacementRequest(uint settlementLocation, uint roadEndLocation)
        {
            var verificationResults = this.gameBoard.CanPlaceStartingInfrastructure(this.mainPlayer.Id, settlementLocation, roadEndLocation);
            return this.VerifySettlementPlacing(verificationResults, settlementLocation) && this.VerifyRoadSegmentPlacing(verificationResults, settlementLocation, roadEndLocation);
        }

        private Boolean VerifyTradeWithBank(int receivingCount, int resourceCount, ResourceTypes givingResourceType, ResourceTypes receivingResourceType)
        {
            if (this.GamePhase == GamePhases.GameOver)
            {
                this.ErrorRaisedEvent?.Invoke(new ErrorDetails("Cannot trade with bank. Game is over."));
                return false;
            }

            if (receivingCount <= 0)
            {
                this.ErrorRaisedEvent?.Invoke(new ErrorDetails("Cannot complete trade with bank: Receiving count must be positive. Was " + receivingCount + "."));
                return false;
            }

            if (resourceCount < receivingCount * 4)
            {
                var errorMessage = "Cannot complete trade with bank: Need to pay " + (receivingCount * 4) + " " + givingResourceType.ToString().ToLower() + " for " + receivingCount + " " + receivingResourceType.ToString().ToLower() + ". Only paying " + resourceCount + ".";
                this.ErrorRaisedEvent?.Invoke(new ErrorDetails(errorMessage));
                return false;
            }

            return true;
        }

        private Boolean VerifyTurnToken(TurnToken turnToken)
        {
            if (turnToken == null)
            {
                this.ErrorRaisedEvent?.Invoke(new ErrorDetails("Turn token is null."));
                return false;
            }

            if (turnToken != this.currentTurnToken)
            {
                this.ErrorRaisedEvent?.Invoke(new ErrorDetails("Turn token not recognised."));
                return false;
            }

            return true;
        }
        #endregion
    }
}
