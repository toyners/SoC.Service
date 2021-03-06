﻿
namespace Jabberwocky.SoC.Library
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Jabberwocky.SoC.Library.DevelopmentCards;
    using Jabberwocky.SoC.Library.Enums;
    using Jabberwocky.SoC.Library.GameBoards;
    using Jabberwocky.SoC.Library.GameEvents;
    using Jabberwocky.SoC.Library.Interfaces;
    using Jabberwocky.SoC.Library.PlayerActions;

    public class GameManager : IGameManager, IPlayerActionReceiver
    {
        #region Fields
        private readonly ActionManager actionManager;
        private readonly ConcurrentQueue<PlayerAction> actionRequests = new ConcurrentQueue<PlayerAction>();
        private readonly ISet<DevelopmentCard> cardsBoughtThisTurn = new HashSet<DevelopmentCard>();
        private readonly Dictionary<Guid, ChooseLostResourcesEvent> chooseLostResourcesEventByPlayerId = new Dictionary<Guid, ChooseLostResourcesEvent>();
        private readonly IDevelopmentCardHolder developmentCardHolder;
        private readonly IEventSender eventSender;
        private readonly GameBoard gameBoard;
        private readonly ILog log = new Log();
        private readonly IActionLog actionLog = null;
        private readonly INumberGenerator numberGenerator;
        protected readonly IPlayerFactory playerFactory;

        private IPlayer currentPlayer;
        protected Func<Guid> idGenerator;
        private bool isGameSetup = true;
        private bool developmentCardPlayerThisTurn;
        private Guid[] playerIdsInRobberHex;
        protected int playerIndex = 0;
        private IDictionary<Guid, IPlayer> playersById;
        private IPlayer playerWithLargestArmy;
        private IPlayer playerWithLongestRoad;
        private uint robberHex = 0;

        protected IPlayer[] players;
        private readonly IGameTimer turnTimer;

        // TODO: Review this - cleaner way to do this?
        private Tuple<Guid, ResourceClutch> initialDirectTradeOffer;
        private Dictionary<Guid, ResourceClutch> answeringDirectTradeOffers = new Dictionary<Guid, ResourceClutch>();

        // Only needed for scenario running?
        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private CancellationToken cancellationToken;
        #endregion

        #region Construction
        public GameManager(
            INumberGenerator numberGenerator,
            GameBoard gameBoard,
            IDevelopmentCardHolder developmentCardHolder,
            IPlayerFactory playerFactory,
            IEventSender eventSender,
            GameOptions gameOptions)
            : this(Guid.NewGuid(), numberGenerator, gameBoard, developmentCardHolder, playerFactory, eventSender, gameOptions)
        { }

        public GameManager(
            Guid id,
            INumberGenerator numberGenerator,
            GameBoard gameBoard,
            IDevelopmentCardHolder developmentCardHolder,
            IPlayerFactory playerFactory,
            IEventSender eventSender,
            GameOptions gameOptions)
        {
            this.Id = id;
            this.numberGenerator = numberGenerator;
            this.gameBoard = gameBoard;
            this.developmentCardHolder = developmentCardHolder;
            this.turnTimer = new GameServerTimer(gameOptions.TurnTimeInSeconds);
            this.idGenerator = () => { return Guid.NewGuid(); };
            this.eventSender = eventSender;
            this.actionManager = new ActionManager();
            this.playerFactory = playerFactory;
            this.players = new IPlayer[gameOptions.Players + gameOptions.MaxAIPlayers];
        }
        #endregion

        #region Properties
        public Guid Id { get; private set; }
        public bool IsFinished { get; private set; }
        #endregion

        #region Methods
        public void JoinGame(string playerName)
        {
            var player = this.playerFactory.CreatePlayer(playerName, this.idGenerator.Invoke());
            this.players[this.playerIndex++] = player;
            this.RaiseEvent(new GameJoinedEvent(player.Id), player);
        }

        public void Quit()
        {
            this.cancellationTokenSource.Cancel();
        }

        public void SaveLog(string filePath) => this.log.WriteToFile(filePath);

        public void SetIdGenerator(Func<Guid> idGenerator)
        {
            this.idGenerator = idGenerator ?? 
                throw new NullReferenceException("Parameter 'idGenerator' cannot be null");
        }

        public Task StartGameAsync()
        {
            this.cancellationToken = this.cancellationTokenSource.Token;

            // Launch server processing on separate thread
            return Task.Factory.StartNew(() =>
            {
                Thread.CurrentThread.Name = "Local Game Server";
                try
                {
                    this.playersById = this.players.ToDictionary(p => p.Id, p => p);

                    var playerIdsByName = this.players.ToDictionary(p => p.Name, p => p.Id);
                    var playerNames = this.players.Select(player => player.Name).ToArray();
                    this.RaiseEvent(new PlayerSetupEvent(playerNames, playerIdsByName));

                    var gameBoardSetup = new GameBoardSetup(this.gameBoard);
                    this.RaiseEvent(new InitialBoardSetupEvent(gameBoardSetup));

                    this.players = PlayerTurnOrderCreator.Create(this.players, this.numberGenerator);
                    var playerIds = this.players.Select(player => player.Id).ToArray();
                    this.RaiseEvent(new PlayerOrderEvent(playerIds));

                    try
                    {
                        this.GameSetup();
                        this.WaitForGameStartConfirmationFromPlayers();
                        this.MainGameLoop();
                        this.CaretakerLoop();
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                }
                catch (Exception e)
                {
                    this.log.Add($"ERROR: {e.Message}: {e.StackTrace}");
                    //TODO: Send game error message to clients
                    throw e;
                }
                finally
                {
                    this.IsFinished = true;
                }
            });
        }

        private void CaretakerLoop()
        {
            while (true)
            {
                var requestStateAction = this.WaitForRequestStateAction();
                this.ProcessRequestStateAction(requestStateAction);
            }
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

        private void GameSetup()
        {
            // Place first settlement
            for (int i = 0; i < this.players.Length; i++)
            {
                this.GameSetupForPlayer(this.players[i]);
            }

            // Place second settlement
            for (int i = this.players.Length - 1; i >= 0; i--)
            {
                this.GameSetupForPlayer(this.players[i]);
            }

            this.isGameSetup = false;
        }

        private void GameSetupForPlayer(IPlayer player)
        {
            var placeSetupInfrastructureEvent = new PlaceSetupInfrastructureEvent();
            this.actionManager.SetExpectedActionsForPlayer(player.Id, 
                typeof(PlaceSetupInfrastructureAction),
                typeof(QuitGameAction));
            this.RaiseEvent(placeSetupInfrastructureEvent, player);
            var playerAction = this.WaitForPlayerAction();
            this.turnTimer.Reset();
            this.ProcessPlayerAction(playerAction);
        }

        private DevelopmentCard GetDevelopmentCardToBePlayed(DevelopmentCardTypes developmentCardType)
        {
            if (this.developmentCardPlayerThisTurn)
            {
                this.RaiseEvent(new GameErrorEvent(this.currentPlayer.Id, (int)ErrorCodes.CannotPlayDevelopmentCard, "Cannot play more than one development card per turn"),
                    this.currentPlayer);
                return null;
            }

            DevelopmentCard card = null;
            if ((card = this.currentPlayer.HeldCards.FirstOrDefault(c => c.Type == developmentCardType &&
                !this.cardsBoughtThisTurn.Contains(c))) == null)
            {
                string developmentCardTypeName = null;
                switch (developmentCardType)
                {
                    case DevelopmentCardTypes.Knight: developmentCardTypeName = "Knight"; break;
                    case DevelopmentCardTypes.Monopoly: developmentCardTypeName = "Monopoly"; break;
                    case DevelopmentCardTypes.RoadBuilding: developmentCardTypeName = "Road building"; break;
                    case DevelopmentCardTypes.YearOfPlenty: developmentCardTypeName = "Year of Plenty"; break;
                }

                this.RaiseEvent(new GameErrorEvent(this.currentPlayer.Id, (int)ErrorCodes.NoDevelopmentCardCanBePlayed, $"No {developmentCardTypeName} card owned that can be played this turn"),
                    this.currentPlayer);
                return null;
            }

            return card;
        }

        private string GetErrorMessage(Type actualAction, HashSet<Type> expectedActions)
        {
            if (expectedActions == null || expectedActions.Count == 0)
                return $"Received action type {actualAction.Name}. No action expected";
            
            var expectedActionsNames = expectedActions.Select(
                    expectedAction => expectedAction.Name).ToList();

            expectedActionsNames.Sort();

            return $"Received action type {actualAction.Name}. Expected one of {string.Join(", ", expectedActionsNames)}";
        }

        private IPlayer GetWinningPlayer()
        {
            return this.players.FirstOrDefault(player => player.VictoryPoints >= 10);
        }

        private bool IsAutomaticPlayerAction(PlayerAction playerAction)
        {
            return playerAction is RequestStateAction || playerAction is QuitGameAction;
        }

        private void MainGameLoop()
        {
            this.playerIndex = -1;
            this.StartTurn();
            this.turnTimer.Reset();
            var isFinished = false;
            while (!isFinished)
            {
                var playerAction = this.WaitForPlayerAction();
                this.turnTimer.Reset();
                isFinished = this.ProcessPlayerAction(playerAction);
            }
        }

        private void PlaceInfrastructure(IPlayer player, uint settlementLocation, uint roadEndLocation)
        {
            try
            {
                this.gameBoard.PlaceStartingInfrastructure(player.Id, settlementLocation, roadEndLocation);
                player.PlaceStartingInfrastructure();
            }
            catch (Exception e)
            {
                // TODO: Send back message to user
            }
        }

        public void PlayerActionEventHandler(PlayerAction playerAction)
        {
            // Leave all validation and processing to the game server thread
            this.actionRequests.Enqueue(playerAction);
        }

        private IEnumerable<IPlayer> PlayersExcept(params Guid[] playerIds) => this.playersById.Select(kv => kv.Value).Where(player => !playerIds.Contains(player.Id));

        private void ProcessAcceptDirectTradeAction(AcceptDirectTradeAction acceptDirectTradeAction)
        {
            var sellingResources = this.answeringDirectTradeOffers[acceptDirectTradeAction.SellerId];
            var buyingResources = this.initialDirectTradeOffer.Item2;
            var buyingPlayer = this.playersById[this.initialDirectTradeOffer.Item1];
            var sellingPlayer = this.playersById[acceptDirectTradeAction.SellerId];

            buyingPlayer.AddResources(buyingResources);
            sellingPlayer.RemoveResources(buyingResources);
            buyingPlayer.RemoveResources(sellingResources);
            sellingPlayer.AddResources(sellingResources);

            var acceptTradeEvent = new AcceptTradeEvent(
                this.initialDirectTradeOffer.Item1,
                buyingResources,
                acceptDirectTradeAction.SellerId,
                sellingResources);

            this.RaiseEvent(acceptTradeEvent, this.currentPlayer);
            this.RaiseEvent(acceptTradeEvent, this.PlayersExcept(this.currentPlayer.Id));
        }

        private void ProcessAnswerDirectTradeOfferAction(AnswerDirectTradeOfferAction answerDirectTradeOfferAction)
        {
            var answerDirectTradeOfferEvent = new AnswerDirectTradeOfferEvent(
                answerDirectTradeOfferAction.InitiatingPlayerId, answerDirectTradeOfferAction.WantedResources);

            this.answeringDirectTradeOffers.Add(
                answerDirectTradeOfferAction.InitiatingPlayerId,
                answerDirectTradeOfferAction.WantedResources);

            // Initial player gets chance to confirm. 
            this.RaiseEvent(
                answerDirectTradeOfferEvent,
                this.playersById[answerDirectTradeOfferAction.InitialPlayerId]);

            // Other two players gets informational event
            var informationalAnswerDirectTradeOfferEvent = new AnswerDirectTradeOfferEvent(
                answerDirectTradeOfferAction.InitiatingPlayerId, answerDirectTradeOfferAction.WantedResources);

            var otherPlayers = this.PlayersExcept(
                    answerDirectTradeOfferAction.InitiatingPlayerId,
                    answerDirectTradeOfferAction.InitialPlayerId);

            this.RaiseEvent(informationalAnswerDirectTradeOfferEvent, otherPlayers);
        }

        private void ProcessBuyDevelopmentCardAction()
        {
            if (!this.currentPlayer.CanBuyDevelopmentCard)
            {
                this.RaiseEvent(new GameErrorEvent(this.currentPlayer.Id, (int)ErrorCodes.NotEnoughResourcesForDevelopmentCard, "Not enough resources for buying development card"), this.currentPlayer);
            }
            else
            {
                this.currentPlayer.BuyDevelopmentCard();
                this.developmentCardHolder.TryGetNextCard(out var card);
                this.currentPlayer.HeldCards.Add(card);
                this.cardsBoughtThisTurn.Add(card);
                this.RaiseEvent(new DevelopmentCardBoughtEvent(this.currentPlayer.Id, card.Type));
                this.RaiseEvent(new DevelopmentCardBoughtEvent(this.currentPlayer.Id), this.PlayersExcept(this.currentPlayer.Id));
            }
        }

        private void ProcessMakeDirectTradeOfferAction(MakeDirectTradeOfferAction makeDirectTradeOfferAction)
        {
            this.initialDirectTradeOffer = new Tuple<Guid, ResourceClutch>(
                makeDirectTradeOfferAction.InitiatingPlayerId,
                makeDirectTradeOfferAction.WantedResources);

            this.actionManager.AddExpectedActionsForPlayer(this.currentPlayer.Id,
                typeof(AcceptDirectTradeAction));

            var makeDirectTradeOfferEvent = new MakeDirectTradeOfferEvent(
                makeDirectTradeOfferAction.InitiatingPlayerId, makeDirectTradeOfferAction.WantedResources);

            var otherPlayers = this.PlayersExcept(makeDirectTradeOfferAction.InitiatingPlayerId).ToList();
            otherPlayers.ForEach(player => {
                this.actionManager.SetExpectedActionsForPlayer(player.Id, typeof(AnswerDirectTradeOfferAction));
                this.RaiseEvent(makeDirectTradeOfferEvent, player);
            });
        }

        private void ProcessNewRobberPlacement()
        {
            this.playerIdsInRobberHex = this.gameBoard.GetPlayersForHex(this.robberHex);
            if (this.playerIdsInRobberHex != null)
            {
                if (this.playerIdsInRobberHex.Length == 1)
                {
                    if (this.playerIdsInRobberHex[0] != this.currentPlayer.Id)
                    {
                        var player = this.playersById[this.playerIdsInRobberHex[0]];

                        var resourceIndex = this.numberGenerator.GetRandomNumberBetweenZeroAndMaximum(player.Resources.Count);
                        var robbedResource = player.LoseResourceAtIndex(resourceIndex);
                        this.currentPlayer.AddResources(robbedResource);
                        this.RaiseEvent(new ResourcesGainedEvent(robbedResource), this.currentPlayer);
                        this.RaiseEvent(new ResourcesLostEvent(robbedResource, this.currentPlayer.Id, ResourcesLostEvent.ReasonTypes.Robbed), player);
                        this.RaiseEvent(new ResourcesLostEvent(robbedResource, this.currentPlayer.Id, ResourcesLostEvent.ReasonTypes.Witness),
                            this.PlayersExcept(this.currentPlayer.Id, player.Id));
                    }
                }
                else
                {
                    var resourceCountsByPlayerId = this.playerIdsInRobberHex.Where(playerId => playerId != this.currentPlayer.Id).ToDictionary(playerId => playerId, playerId => this.playersById[playerId].Resources.Count);
                    this.RaiseEvent(new RobbingChoicesEvent(this.currentPlayer.Id, resourceCountsByPlayerId));

                    this.actionManager.SetExpectedActionsForPlayer(this.currentPlayer.Id, typeof(SelectResourceFromPlayerAction));
                    this.ProcessPlayerAction(this.WaitForPlayerAction());

                    this.SetStandardExpectedActionsForCurrentPlayer();
                }
            }
        }

        private bool ProcessPlaceCityAction(PlaceCityAction placeCityAction)
        {
            try
            {
                PlayerPlacementStatusCodes verificationState = this.currentPlayer.CanPlaceCity;
                if (verificationState == PlayerPlacementStatusCodes.NotEnoughResources)
                {
                    this.RaiseEvent(new GameErrorEvent(this.currentPlayer.Id, (int)ErrorCodes.NotEnoughResourcesForCity, "Not enough resources for placing city"),
                        this.currentPlayer);
                    return false;
                }

                this.gameBoard.PlaceCity(this.currentPlayer.Id,
                    placeCityAction.CityLocation);
                this.currentPlayer.PlaceCity();

                this.RaiseEvent(new CityPlacedEvent(this.currentPlayer.Id,
                    placeCityAction.CityLocation));

                var winningPlayer = this.GetWinningPlayer();
                if (winningPlayer != null)
                {
                    this.RaiseEvent(new GameWinEvent(winningPlayer.Id, winningPlayer.VictoryPoints));
                    return true;
                }
            }
            catch (GameBoard.PlacementException pe)
            {
                switch (pe.VerificationStatus)
                {
                    case GameBoard.VerificationStatus.LocationIsNotOwned:
                    {
                        var occupyingPlayer = this.playersById[pe.OtherPlayerId];
                        this.RaiseEvent(new GameErrorEvent(this.currentPlayer.Id, (int)ErrorCodes.LocationAlreadyOccupiedByPlayer, $"Location ({placeCityAction.CityLocation}) already occupied by {occupyingPlayer.Name}"),
                            this.currentPlayer);
                        break;
                    }
                    case GameBoard.VerificationStatus.LocationIsAlreadyCity:
                    {
                        this.RaiseEvent(new GameErrorEvent(this.currentPlayer.Id, (int)ErrorCodes.LocationAlreadyOccupiedByPlayer, $"Location ({placeCityAction.CityLocation}) already occupied by you"),
                            this.currentPlayer);
                        break;
                    }
                    case GameBoard.VerificationStatus.LocationIsNotSettled:
                    {
                        this.RaiseEvent(new GameErrorEvent(this.currentPlayer.Id, (int)ErrorCodes.LocationDoesNotHaveSettlement, $"Location ({placeCityAction.CityLocation}) not an settlement"),
                            this.currentPlayer);
                        break;
                    }
                    case GameBoard.VerificationStatus.LocationForCityIsInvalid:
                    {
                        this.RaiseEvent(new GameErrorEvent(this.currentPlayer.Id, (int)ErrorCodes.LocationIsInvalid, $"Location ({placeCityAction.CityLocation}) is invalid"),
                            this.currentPlayer);
                        break;
                    }
                    default:
                    {
                        this.RaiseEvent(new GameErrorEvent(this.currentPlayer.Id, (int)ErrorCodes.UnknownError, $"Unknown error"),
                            this.currentPlayer);
                        break;
                    }
                }
            }

            return false;
        }

        private bool ProcessPlaceRoadSegmentAction(PlaceRoadSegmentAction placeRoadSegmentAction)
        {
            PlayerPlacementStatusCodes verificationState = this.currentPlayer.CanPlaceRoadSegments(1);
            if (verificationState == PlayerPlacementStatusCodes.NoRoadSegments)
            {
                this.RaiseEvent(new GameErrorEvent(this.currentPlayer.Id, (int)ErrorCodes.NotEnoughRoadSegments, "Not enough road segments to place"),
                    this.currentPlayer);
                return false;
            }
            else if (verificationState == PlayerPlacementStatusCodes.NotEnoughResources)
            {
                this.RaiseEvent(new GameErrorEvent(this.currentPlayer.Id, (int)ErrorCodes.NotEnoughResourcesForRoadSegment, "Not enough resources for placing road segment"),
                    this.currentPlayer);
                return false;
            }

            var statusCode = this.gameBoard.TryPlaceRoadSegment(this.currentPlayer.Id,
                placeRoadSegmentAction.StartLocation,
                placeRoadSegmentAction.EndLocation);

            if (!this.VerifyPlaceRoadSegmentStatus(statusCode,
                placeRoadSegmentAction.StartLocation,
                placeRoadSegmentAction.EndLocation))
            {
                return false;
            }

            this.currentPlayer.PlaceRoadSegment();

            this.RaiseEvent(new RoadSegmentPlacedEvent(this.currentPlayer.Id, 
                placeRoadSegmentAction.StartLocation,
                placeRoadSegmentAction.EndLocation));

            if (this.currentPlayer.PlacedRoadSegments >= 5)
            {
                if (this.gameBoard.TryGetLongestRoadDetails(out var playerId, out var locations) && locations.Length > 5)
                {
                    if (playerId == this.currentPlayer.Id && (this.playerWithLongestRoad == null || this.playerWithLongestRoad != this.currentPlayer))
                    {
                        Guid? previousPlayerId = null;
                        if (this.playerWithLongestRoad != null)
                        {
                            this.playerWithLongestRoad.HasLongestRoad = false;
                            previousPlayerId = this.playerWithLongestRoad.Id;
                        }

                        this.playerWithLongestRoad = this.currentPlayer;
                        this.playerWithLongestRoad.HasLongestRoad = true;
                        this.RaiseEvent(new LongestRoadBuiltEvent(this.playerWithLongestRoad.Id, locations, previousPlayerId));
                    }

                    if (this.currentPlayer.VictoryPoints >= 10)
                    {
                        this.RaiseEvent(new GameWinEvent(this.currentPlayer.Id, this.currentPlayer.VictoryPoints));
                        return true;
                    }
                }
            }

            return false;
        }

        private void ProcessPlaceRobberAction(PlaceRobberAction placeRobberAction)
        {
            if (this.robberHex == placeRobberAction.Hex)
            {
                this.RaiseEvent(new GameErrorEvent(this.currentPlayer.Id, (int)ErrorCodes.NewRobberHexIsSameAsCurrentRobberHex, "New robber hex cannot be the same as previous robber hex"),
                    this.currentPlayer);
                return;
            }

            this.actionManager.SetExpectedActionsForPlayer(this.currentPlayer.Id, null);
            this.robberHex = placeRobberAction.Hex;
            this.RaiseEvent(new RobberPlacedEvent(this.currentPlayer.Id, this.robberHex));

            this.ProcessNewRobberPlacement();
        }

        private bool ProcessPlaceSettlementAction(PlaceSettlementAction placeSettlementAction)
        {
            try
            {
                PlayerPlacementStatusCodes verificationState = this.currentPlayer.CanPlaceSettlement;
                if (verificationState == PlayerPlacementStatusCodes.NoSettlements) { 
                    this.RaiseEvent(new GameErrorEvent(this.currentPlayer.Id, (int)ErrorCodes.NoSettlements, "No settlements to place"),
                        this.currentPlayer);
                    return false;
                }
                else if (verificationState == PlayerPlacementStatusCodes.NotEnoughResources)
                {
                    this.RaiseEvent(new GameErrorEvent(this.currentPlayer.Id, (int)ErrorCodes.NotEnoughResourcesForSettlement, "Not enough resources for placing settlement"),
                        this.currentPlayer);
                    return false;
                }

                this.gameBoard.PlaceSettlement(this.currentPlayer.Id,
                    placeSettlementAction.SettlementLocation);
                this.currentPlayer.PlaceSettlement();

                this.RaiseEvent(new SettlementPlacedEvent(this.currentPlayer.Id,
                    placeSettlementAction.SettlementLocation));

                var winningPlayer = this.GetWinningPlayer();
                if (winningPlayer != null)
                {
                    this.RaiseEvent(new GameWinEvent(winningPlayer.Id, winningPlayer.VictoryPoints));
                    return true;
                }
            }
            catch (GameBoard.PlacementException pe)
            {
                switch (pe.VerificationStatus)
                {
                    case GameBoard.VerificationStatus.LocationIsOccupied:
                    {
                        var occupyingPlayer = this.playersById[pe.OtherPlayerId];
                        var occupyingPlayerName = occupyingPlayer == this.currentPlayer ? "you" : occupyingPlayer.Name;
                        this.RaiseEvent(new GameErrorEvent(this.currentPlayer.Id, (int)ErrorCodes.LocationAlreadyOccupiedByPlayer, $"Location ({placeSettlementAction.SettlementLocation}) already occupied by {occupyingPlayerName}"),
                            this.currentPlayer);
                        break;
                    }
                    case GameBoard.VerificationStatus.SettlementNotConnectedToExistingRoad:
                    {
                        this.RaiseEvent(new GameErrorEvent(this.currentPlayer.Id, (int)ErrorCodes.LocationNotConnectedToRoadSystem, $"Location ({placeSettlementAction.SettlementLocation}) is not connected to your road system"),
                            this.currentPlayer);
                        break;
                    }
                    case GameBoard.VerificationStatus.LocationForSettlementIsInvalid:
                    {
                        this.RaiseEvent(new GameErrorEvent(this.currentPlayer.Id, (int)ErrorCodes.LocationIsInvalid, $"Location ({placeSettlementAction.SettlementLocation}) is invalid"),
                            this.currentPlayer);
                        break;
                    }
                    default:
                    {
                        this.RaiseEvent(new GameErrorEvent(this.currentPlayer.Id, (int)ErrorCodes.UnknownError, $"Unknown error"),
                            this.currentPlayer);
                        break;
                    }
                }
            }

            return false;
        }

        private void ProcessPlaceSetupInfrastructureAction(PlaceSetupInfrastructureAction placeSetupInfrastructureAction)
        {
            var player = this.playersById[placeSetupInfrastructureAction.InitiatingPlayerId];
            this.actionManager.SetExpectedActionsForPlayer(player.Id, null);
            var settlementLocation = placeSetupInfrastructureAction.SettlementLocation;
            var roadEndLocation = placeSetupInfrastructureAction.RoadEndLocation;
            this.PlaceInfrastructure(player, settlementLocation, roadEndLocation);
            this.RaiseEvent(new SetupInfrastructurePlacedEvent(player.Id, settlementLocation, roadEndLocation));
        }

        private bool ProcessPlayerAction(PlayerAction playerAction)
        {
            if (playerAction is AcceptDirectTradeAction acceptDirectTradeAction)
            {
                this.ProcessAcceptDirectTradeAction(acceptDirectTradeAction);
                return false;
            }

            if (playerAction is AnswerDirectTradeOfferAction answerDirectTradeOfferAction)
            {
                this.ProcessAnswerDirectTradeOfferAction(answerDirectTradeOfferAction);
                return false;
            }

            if (playerAction is BuyDevelopmentCardAction buyDevelopmentCardAction)
            {
                this.ProcessBuyDevelopmentCardAction();
                return false;
            }

            if (playerAction is EndOfTurnAction)
            {
                this.StartTurn();
                return false;
            }

            if (playerAction is MakeDirectTradeOfferAction makeDirectTradeOfferAction)
            {
                this.ProcessMakeDirectTradeOfferAction(makeDirectTradeOfferAction);
                return false;
            }

            if (playerAction is PlaceCityAction placeCityAction)
            {
                return this.ProcessPlaceCityAction(placeCityAction);
            }

            if (playerAction is PlaceSetupInfrastructureAction placeSetupInfrastructureAction)
            {
                this.ProcessPlaceSetupInfrastructureAction(placeSetupInfrastructureAction);
                return false;
            }

            if (playerAction is PlaceRoadSegmentAction placeRoadSegmentAction)
            {
                return this.ProcessPlaceRoadSegmentAction(placeRoadSegmentAction);
            }

            if (playerAction is PlayKnightCardAction playKnightCardAction)
            {
                return this.ProcessPlayKnightCardAction(playKnightCardAction);
            }

            if (playerAction is PlayMonopolyCardAction playMonopolyCardAction)
            {
                this.ProcessPlayMonopolyCardAction(playMonopolyCardAction);
                return false;
            }

            if (playerAction is PlayRoadBuildingCardAction playRoadBuildingCardAction)
            {
                return this.ProcessPlayRoadBuildingCardAction(playRoadBuildingCardAction); 
            }

            if (playerAction is PlayYearOfPlentyCardAction playYearOfPlentyCardAction)
            {
                this.ProcessPlayYearOfPlentyCardAction(playYearOfPlentyCardAction);
                return false;
            }

            if (playerAction is SelectResourceFromPlayerAction selectResourceFromPlayerAction)
            {
                if (!this.playerIdsInRobberHex.Contains(selectResourceFromPlayerAction.SelectedPlayerId))
                {
                    this.RaiseEvent(new GameErrorEvent(this.currentPlayer.Id, (int)ErrorCodes.InvalidPlayerSelection, "Invalid player selection"),
                        this.currentPlayer);
                }
                else
                {
                    var player = this.playersById[selectResourceFromPlayerAction.SelectedPlayerId];
                    var resourceIndex = this.numberGenerator.GetRandomNumberBetweenZeroAndMaximum(player.Resources.Count);
                    var robbedResource = player.LoseResourceAtIndex(resourceIndex);
                    this.currentPlayer.AddResources(robbedResource);
                    this.RaiseEvent(new ResourcesGainedEvent(robbedResource), this.currentPlayer);
                    this.RaiseEvent(new ResourcesLostEvent(robbedResource, this.currentPlayer.Id, ResourcesLostEvent.ReasonTypes.Robbed), player);
                    this.RaiseEvent(new ResourcesLostEvent(robbedResource, this.currentPlayer.Id, ResourcesLostEvent.ReasonTypes.Witness),
                        this.PlayersExcept(this.currentPlayer.Id, player.Id));
                }

                return false;
            }

            if (playerAction is PlaceRobberAction placeRobberAction)
            {
                this.ProcessPlaceRobberAction(placeRobberAction);
                return false;
            }

            if (playerAction is PlaceSettlementAction placeSettlementAction)
            {
                return this.ProcessPlaceSettlementAction(placeSettlementAction);
            }

            if (playerAction is QuitGameAction quitGameAction)
            {
                return this.ProcessQuitGameAction(quitGameAction);
            }

            if (playerAction is RequestStateAction requestStateAction)
            {
                this.ProcessRequestStateAction(requestStateAction);
                return false;
            }

            throw new Exception($"Player action {playerAction.GetType()} not recognised.");
        }

        private void ProcessPlayerQuit(Guid playerId)
        {
            this.players = this.players.Where(player => player.Id != playerId).ToArray();
            this.playersById.Remove(playerId);
        }

        private bool ProcessPlayKnightCardAction(PlayKnightCardAction playKnightCardAction)
        {
            DevelopmentCard card = this.GetDevelopmentCardToBePlayed(DevelopmentCardTypes.Knight);
            if (card == null)
                return false;
        
            if (this.robberHex == playKnightCardAction.NewRobberHex)
            {
                this.RaiseEvent(new GameErrorEvent(this.currentPlayer.Id, (int)ErrorCodes.NewRobberHexIsSameAsCurrentRobberHex, "New robber hex cannot be the same as previous robber hex"),
                    this.currentPlayer);
                return false;
            }

            this.currentPlayer.PlayDevelopmentCard(card);

            this.robberHex = playKnightCardAction.NewRobberHex;
            this.developmentCardPlayerThisTurn = true;
            this.RaiseEvent(new KnightCardPlayedEvent(this.currentPlayer.Id, this.robberHex));

            if (this.currentPlayer.PlayedKnightCards >= 3)
            {
                if (this.playerWithLargestArmy == null)
                {
                    this.RaiseEvent(new LargestArmyChangedEvent(this.currentPlayer.Id, null));
                    this.playerWithLargestArmy = this.currentPlayer;
                    this.currentPlayer.HasLargestArmy = true;
                }
                else if (this.currentPlayer != this.playerWithLargestArmy &&
                    this.currentPlayer.PlayedKnightCards > this.playerWithLargestArmy.PlayedKnightCards)
                {
                    var previousPlayer = this.playerWithLargestArmy;
                    previousPlayer.HasLargestArmy = false;
                    this.playerWithLargestArmy = this.currentPlayer;
                    this.currentPlayer.HasLargestArmy = true;

                    this.RaiseEvent(new LargestArmyChangedEvent(this.currentPlayer.Id, previousPlayer.Id));
                }

                if (this.currentPlayer.VictoryPoints >= 10)
                {
                    this.RaiseEvent(new GameWinEvent(this.currentPlayer.Id, this.currentPlayer.VictoryPoints));
                    return true;
                }
            }

            this.ProcessNewRobberPlacement();
            return false;
        }

        private void ProcessPlayMonopolyCardAction(PlayMonopolyCardAction playMonopolyCardAction)
        {
            DevelopmentCard card = this.GetDevelopmentCardToBePlayed(DevelopmentCardTypes.Monopoly);
            if (card == null)
                return;

            var resourceTransactionList = new ResourceTransactionList();
            foreach (var player in this.PlayersExcept(this.currentPlayer.Id))
            {
                var resourceClutch = player.LoseResourcesOfType(playMonopolyCardAction.ResourceType);
                if (resourceClutch != ResourceClutch.Zero)
                {
                    resourceTransactionList.Add(new ResourceTransaction(this.currentPlayer.Id, player.Id, resourceClutch));
                    this.currentPlayer.AddResources(resourceClutch);
                }
            }

            this.currentPlayer.PlayDevelopmentCard(card);

            this.RaiseEvent(new PlayMonopolyCardEvent(this.currentPlayer.Id, resourceTransactionList));
        }

        private bool ProcessPlayRoadBuildingCardAction(PlayRoadBuildingCardAction playRoadBuildingCardAction)
        {
            DevelopmentCard card = this.GetDevelopmentCardToBePlayed(DevelopmentCardTypes.RoadBuilding);
            if (card == null)
                return false;

            PlayerPlacementStatusCodes verificationState = this.currentPlayer.CanPlaceRoadSegments(2);
            if (verificationState == PlayerPlacementStatusCodes.NoRoadSegments)
            {
                this.RaiseEvent(new GameErrorEvent(this.currentPlayer.Id, (int)ErrorCodes.NotEnoughRoadSegments, "Not enough road segments to place"),
                    this.currentPlayer);
                return false;
            }

            var statusCode = this.gameBoard.CanPlaceRoadSegment(this.currentPlayer.Id,
                playRoadBuildingCardAction.FirstRoadSegmentStartLocation,
                playRoadBuildingCardAction.FirstRoadSegmentEndLocation);

            if (!this.VerifyPlaceRoadSegmentStatus(statusCode,
                playRoadBuildingCardAction.FirstRoadSegmentStartLocation,
                playRoadBuildingCardAction.FirstRoadSegmentEndLocation))
            {
                return false;
            }

            statusCode = this.gameBoard.CanPlaceRoadSegment(this.currentPlayer.Id,
                playRoadBuildingCardAction.SecondRoadSegmentStartLocation,
                playRoadBuildingCardAction.SecondRoadSegmentEndLocation,
                playRoadBuildingCardAction.FirstRoadSegmentStartLocation,
                playRoadBuildingCardAction.FirstRoadSegmentEndLocation);

            if (!this.VerifyPlaceRoadSegmentStatus(statusCode,
                playRoadBuildingCardAction.SecondRoadSegmentStartLocation,
                playRoadBuildingCardAction.SecondRoadSegmentEndLocation))
            {
                return false;
            }

            this.gameBoard.TryPlaceRoadSegment(this.currentPlayer.Id,
                playRoadBuildingCardAction.FirstRoadSegmentStartLocation,
                playRoadBuildingCardAction.FirstRoadSegmentEndLocation);

            this.gameBoard.TryPlaceRoadSegment(this.currentPlayer.Id,
                playRoadBuildingCardAction.SecondRoadSegmentStartLocation,
                playRoadBuildingCardAction.SecondRoadSegmentEndLocation);

            this.currentPlayer.PlaceRoadSegment(false);
            this.currentPlayer.PlaceRoadSegment(false);

            this.currentPlayer.PlayDevelopmentCard(card);
            this.developmentCardPlayerThisTurn = true;

            this.RaiseEvent(new RoadBuildingCardPlayedEvent(this.currentPlayer.Id,
                playRoadBuildingCardAction.FirstRoadSegmentStartLocation,
                playRoadBuildingCardAction.FirstRoadSegmentEndLocation,
                playRoadBuildingCardAction.SecondRoadSegmentStartLocation,
                playRoadBuildingCardAction.SecondRoadSegmentEndLocation));

            if (this.currentPlayer.PlacedRoadSegments >= 5)
            {
                if (this.gameBoard.TryGetLongestRoadDetails(out var playerId, out var locations) && locations.Length > 5)
                {
                    if (playerId == this.currentPlayer.Id && (this.playerWithLongestRoad == null || this.playerWithLongestRoad != this.currentPlayer))
                    {
                        Guid? previousPlayerId = null;
                        if (this.playerWithLongestRoad != null)
                        {
                            this.playerWithLongestRoad.HasLongestRoad = false;
                            previousPlayerId = this.playerWithLongestRoad.Id;
                        }

                        this.playerWithLongestRoad = this.currentPlayer;
                        this.playerWithLongestRoad.HasLongestRoad = true;
                        this.RaiseEvent(new LongestRoadBuiltEvent(this.playerWithLongestRoad.Id, locations, previousPlayerId));
                    }

                    if (this.currentPlayer.VictoryPoints >= 10)
                    {
                        this.RaiseEvent(new GameWinEvent(this.currentPlayer.Id, this.currentPlayer.VictoryPoints));
                        return true;
                    }
                }
            }

            return false;
        }

        private void ProcessPlayYearOfPlentyCardAction(PlayYearOfPlentyCardAction playYearOfPlentyCardAction)
        {
            DevelopmentCard card = this.GetDevelopmentCardToBePlayed(DevelopmentCardTypes.YearOfPlenty);
            if (card == null)
                return;

            ResourceClutch resources = ResourceClutch.Zero;
            foreach (var resource in new[] { playYearOfPlentyCardAction.FirstResource, playYearOfPlentyCardAction.SecondResource })
            {
                switch (resource)
                {
                    case ResourceTypes.Brick: resources += ResourceClutch.OneBrick; break;
                    case ResourceTypes.Grain: resources += ResourceClutch.OneGrain; break;
                    case ResourceTypes.Lumber: resources += ResourceClutch.OneLumber; break;
                    case ResourceTypes.Ore: resources += ResourceClutch.OneOre; break;
                    case ResourceTypes.Wool: resources += ResourceClutch.OneWool; break;
                }
            }

            this.currentPlayer.AddResources(resources);
            this.currentPlayer.PlayDevelopmentCard(card);

            this.RaiseEvent(new YearOfPlentyCardPlayedEvent(this.currentPlayer.Id,
                playYearOfPlentyCardAction.FirstResource,
                playYearOfPlentyCardAction.SecondResource));
        }

        private bool ProcessQuitGameAction(QuitGameAction quitGameAction)
        {
            this.ProcessPlayerQuit(quitGameAction.InitiatingPlayerId);
            this.playerIndex--;
            this.RaiseEvent(new PlayerQuitEvent(quitGameAction.InitiatingPlayerId));
            if (this.players.Length == 1)
            {
                this.RaiseEvent(new GameWinEvent(this.players[0].Id, this.players[0].VictoryPoints));
                return true;
            }
            else if (!this.isGameSetup)
            {
                this.StartTurn();
            }

            return false;
        }

        private void ProcessRequestStateAction(RequestStateAction requestStateAction)
        {
            var player = this.playersById[requestStateAction.InitiatingPlayerId];
            var requestStateEvent = new RequestStateEvent(player.Id);
            requestStateEvent.Cities = player.RemainingCities;

            requestStateEvent.HeldCards = 0;
            requestStateEvent.DevelopmentCardsByCount = player.HeldCards?.Aggregate(new Dictionary<DevelopmentCardTypes, int>(),
                (dict, card) => {
                    if (!dict.ContainsKey(card.Type))
                        dict.Add(card.Type, 1);
                    else
                        dict[card.Type]++;
                    requestStateEvent.HeldCards++;
                    return dict;
                });
            requestStateEvent.PlayedKnightCards = player.PlayedKnightCards;
            requestStateEvent.Resources = player.Resources;
            requestStateEvent.RoadSegments = player.RemainingRoadSegments;
            requestStateEvent.Settlements = player.RemainingSettlements;
            requestStateEvent.VictoryPoints = player.VictoryPoints;
            this.RaiseEvent(requestStateEvent, player);
        }

        private void RaiseEvent(GameEvent gameEvent)
        {
            this.RaiseEvent(gameEvent, this.players);
        }

        private void RaiseEvent(GameEvent gameEvent, IEnumerable<IPlayer> players)
        {
            var message = $"Sending {this.ToPrettyString(gameEvent)} " +
                $"to {string.Join(", ", players.Select(player => player.Name))}";
            this.log.Add(message);
            foreach (var player in players)
                this.eventSender.Send(gameEvent, player.Id);
        }

        protected void RaiseEvent(GameEvent gameEvent, IPlayer player)
        {
            this.log.Add($"Sending {this.ToPrettyString(gameEvent)} to {player.Name}");
            this.eventSender.Send(gameEvent, player.Id);
        }

        private void SetStandardExpectedActionsForCurrentPlayer()
        {
            this.actionManager.SetExpectedActionsForPlayer(this.currentPlayer.Id,
                typeof(BuyDevelopmentCardAction),
                typeof(EndOfTurnAction), typeof(MakeDirectTradeOfferAction),
                typeof(PlaceCityAction), typeof(PlaceRoadSegmentAction),
                typeof(PlaceSettlementAction), typeof(PlayKnightCardAction),
                typeof(PlayMonopolyCardAction), typeof(PlayRoadBuildingCardAction),
                typeof(PlayYearOfPlentyCardAction));

            foreach (var player in this.PlayersExcept(this.currentPlayer.Id))
                this.actionManager.SetExpectedActionsForPlayer(player.Id, null);
        }

        private void StartTurn()
        {
            this.ChangeToNextPlayer();
            this.cardsBoughtThisTurn.Clear();
            this.developmentCardPlayerThisTurn = false;

            foreach (var player in this.players)
                this.actionManager.SetExpectedActionsForPlayer(player.Id, null);

            this.numberGenerator.RollTwoDice(out var dice1, out var dice2);
            if (dice1 + dice2 != 7)
            {
                this.StartTurnWithResourceCollection(dice1, dice2);
                this.SetStandardExpectedActionsForCurrentPlayer();
            }
            else
            {
                this.StartTurnWithRobberPlacement(dice1, dice2);
            }
        }

        private void StartTurnWithResourceCollection(uint dice1, uint dice2)
        {
            var resourcesCollectedByPlayerId = this.gameBoard.GetResourcesForRoll(dice1 + dice2);
            foreach (var keyValuePair in resourcesCollectedByPlayerId)
            {
                if (this.playersById.TryGetValue(keyValuePair.Key, out var player))
                {
                    foreach (var resourceCollection in keyValuePair.Value)
                        player.AddResources(resourceCollection.Resources);
                }
            }

            var startPlayerTurnEvent = new StartTurnEvent(this.currentPlayer.Id, dice1, dice2, resourcesCollectedByPlayerId);
            this.RaiseEvent(startPlayerTurnEvent);
        }

        private void StartTurnWithRobberPlacement(uint dice1, uint dice2)
        {
            var startPlayerTurnEvent = new StartTurnEvent(this.currentPlayer.Id, dice1, dice2, null);
            this.RaiseEvent(startPlayerTurnEvent);
            this.WaitForLostResourcesFromPlayers();
            this.WaitForRobberPlacement();
        }

        private string ToPrettyString(GameEvent gameEvent)
        {
            var message = $"{gameEvent.SimpleTypeName}";
            if (gameEvent is StartTurnEvent startTurnEvent)
                message += $", Dice rolls {startTurnEvent.Dice1} {startTurnEvent.Dice2}";
            return message;
        }

        private string ToPrettyString(IEnumerable<string> playerNames)
        {
            return $"{string.Join(", ", playerNames)}";
        }

        private bool VerifyPlaceRoadSegmentStatus(PlacementStatusCodes statusCode, uint roadSegmentStartLocation, uint roadSegmentEndLocation)
        {
            switch (statusCode)
            {
                case PlacementStatusCodes.RoadNotConnectedToExistingRoad:
                {
                    this.RaiseEvent(new GameErrorEvent(this.currentPlayer.Id, (int)ErrorCodes.LocationNotConnectedToRoadSystem,
                        $"Cannot place road segment because locations ({roadSegmentStartLocation}, {roadSegmentEndLocation}) are not connected to existing road"),
                        this.currentPlayer);
                    return false;
                }
                case PlacementStatusCodes.RoadIsOccupied:
                {
                    this.RaiseEvent(new GameErrorEvent(this.currentPlayer.Id, (int)ErrorCodes.LocationsInvalidForRoadSegment,
                        $"Cannot place road segment on existing road segment ({roadSegmentStartLocation}, {roadSegmentEndLocation})"),
                        this.currentPlayer);
                    return false;
                }
                case PlacementStatusCodes.RoadIsOffBoard:
                {
                    this.RaiseEvent(new GameErrorEvent(this.currentPlayer.Id, (int)ErrorCodes.LocationsInvalidForRoadSegment,
                        $"Locations ({roadSegmentStartLocation}, {roadSegmentEndLocation}) invalid for placing road segment"),
                        this.currentPlayer);
                    return false;
                }
                case PlacementStatusCodes.NoDirectConnection:
                {
                    this.RaiseEvent(new GameErrorEvent(this.currentPlayer.Id, (int)ErrorCodes.LocationsNotDirectlyConnected,
                        $"Locations ({roadSegmentStartLocation}, {roadSegmentEndLocation}) not directly connected when placing road segment"),
                        this.currentPlayer);
                    return false;
                }
            }

            return true;
        }

        private void WaitForGameStartConfirmationFromPlayers()
        {
            foreach (var player in this.players) {
                this.actionManager.SetExpectedActionsForPlayer(player.Id, typeof(QuitGameAction), typeof(ConfirmGameStartAction));
                this.RaiseEvent(new ConfirmGameStartEvent(), player);
            }

            var playersToConfirm = new HashSet<IPlayer>(this.players);
            while (playersToConfirm.Count > 0)
            {
                var playerAction = this.WaitForPlayerAction();
                if (playerAction is ConfirmGameStartAction confirmGameStartAction)
                {
                    playersToConfirm.Remove(this.playersById[confirmGameStartAction.InitiatingPlayerId]);
                }
                else if (playerAction is QuitGameAction quitGameAction)
                {
                    playersToConfirm.Remove(this.playersById[quitGameAction.InitiatingPlayerId]);
                    this.ProcessPlayerQuit(quitGameAction.InitiatingPlayerId);
                }
            }

            if (this.players.Length == 0)
                throw new OperationCanceledException();

            var resourcesCollectedByPlayerId = new Dictionary<Guid, ResourceCollection[]>();
            foreach (var player in this.players)
            {
                var settlementlocation = this.gameBoard.GetSettlementsForPlayer(player.Id)[1];
                var resourcesForLocation = this.gameBoard.GetResourcesForLocation(settlementlocation);
                player.AddResources(resourcesForLocation);
                var resourceCollection = new ResourceCollection(settlementlocation, resourcesForLocation);
                resourcesCollectedByPlayerId.Add(player.Id, new[] { resourceCollection });
            }
            var resourcesCollectedEvent = new ResourcesCollectedEvent(resourcesCollectedByPlayerId);
            this.RaiseEvent(resourcesCollectedEvent);
        }

        private void WaitForLostResourcesFromPlayers()
        {
            foreach (var player in this.players)
            {
                this.actionManager.SetExpectedActionsForPlayer(player.Id, null);
                if (player.Resources.Count > 7)
                {
                    this.actionManager.SetExpectedActionsForPlayer(player.Id, typeof(LoseResourcesAction));
                    var resourceCount = player.Resources.Count / 2;
                    var chooseLostResourceEvent = new ChooseLostResourcesEvent(resourceCount);
                    this.chooseLostResourcesEventByPlayerId[player.Id] = chooseLostResourceEvent;
                    this.RaiseEvent(new ChooseLostResourcesEvent(resourceCount), player);
                }
            }

            while (this.chooseLostResourcesEventByPlayerId.Count > 0)
            {
                var loseResourcesAction = (LoseResourcesAction)this.WaitForPlayerAction();
                var player = this.playersById[loseResourcesAction.InitiatingPlayerId];
                var chooseLostResourcesEvent = this.chooseLostResourcesEventByPlayerId[player.Id];

                if (loseResourcesAction.Resources.Count != chooseLostResourcesEvent.ResourceCount)
                {
                    var expectedResourceCount = chooseLostResourcesEvent.ResourceCount + " resource";
                    if (chooseLostResourcesEvent.ResourceCount > 1)
                        expectedResourceCount += "s";
                    this.RaiseEvent(new GameErrorEvent(player.Id, (int)ErrorCodes.IncorrectResourceCount, $"Expected {expectedResourceCount} but received {loseResourcesAction.Resources.Count}"), player);
                    continue;
                }

                if (player.Resources - loseResourcesAction.Resources < ResourceClutch.Zero)
                {
                    this.RaiseEvent(new GameErrorEvent(player.Id, (int)ErrorCodes.IncorrectResourceCount, "Resources sent results in negative counts"), player);
                    continue;
                }

                player.RemoveResources(loseResourcesAction.Resources);
                this.actionManager.SetExpectedActionsForPlayer(player.Id, null);
                this.chooseLostResourcesEventByPlayerId.Remove(player.Id);
                this.RaiseEvent(new ResourcesLostEvent(loseResourcesAction.Resources, Guid.Empty, ResourcesLostEvent.ReasonTypes.TooManyResources));
            }
        }

        private PlayerAction WaitForPlayerAction()
        {
            while (true)
            {
                Thread.Sleep(50);
                this.cancellationToken.ThrowIfCancellationRequested();

                if (this.turnTimer.IsLate)
                {
                    // Out of time so game should be killed
                    throw new TimeoutException($"Time out exception waiting for player '{this.currentPlayer.Name}'");
                }

                if (this.playerActions.TryDequeue(out var playerAction))
                {
                    var playerActionTypeName = playerAction.GetType().Name;
                    var playerName = this.playersById[playerAction.InitiatingPlayerId].Name;
                    this.log.Add($"Received {playerActionTypeName} from {playerName}");

                    if (playerAction is RequestStateAction ||
                        playerAction is QuitGameAction)
                        return playerAction;

                    if (!this.actionManager.ValidateAction(playerAction))
                    {
                        var expectedActions = this.actionManager.GetExpectedActionsForPlayer(playerAction.InitiatingPlayerId);
                        var errorMessage = this.GetErrorMessage(playerAction.GetType(), expectedActions);
                        this.RaiseEvent(new GameErrorEvent(playerAction.InitiatingPlayerId, (int)ErrorCodes.IllegalPlayerAction, errorMessage),
                            this.playersById[playerAction.InitiatingPlayerId]);
                        this.log.Add($"FAILED: Action Validation - {playerName}, {playerActionTypeName}");
                        continue;
                    }

                    this.log.Add($"Validated {playerActionTypeName} from {playerName}");
                    return playerAction;
                }
            }
        }

        private RequestStateAction WaitForRequestStateAction()
        {
            while (true)
            {
                Thread.Sleep(50);
                this.cancellationToken.ThrowIfCancellationRequested();

                if (this.playerActions.TryDequeue(out var playerAction))
                {
                    var playerActionTypeName = playerAction.GetType().Name;
                    var playerName = this.playersById[playerAction.InitiatingPlayerId].Name;

                    if (!(playerAction is RequestStateAction))
                    {
                        var errorMessage = $"Received action type {playerActionTypeName}. Expected RequestStateAction";
                        this.RaiseEvent(new GameErrorEvent(playerAction.InitiatingPlayerId, (int)ErrorCodes.IllegalPlayerAction, errorMessage),
                            this.playersById[playerAction.InitiatingPlayerId]);
                        this.log.Add($"FAILED: Action Validation - {playerName}, {playerActionTypeName}");
                        continue;
                    }
                    
                    this.log.Add($"Received {playerActionTypeName} from {playerName}");

                    
                    return (RequestStateAction)playerAction;
                }
            }
        }

        private void WaitForRobberPlacement()
        {
            this.RaiseEvent(new PlaceRobberEvent(), this.currentPlayer);
            this.actionManager.SetExpectedActionsForPlayer(this.currentPlayer.Id, typeof(PlaceRobberAction));
            this.ProcessPlayerAction(this.WaitForPlayerAction());
        }

        private PlayerAction WaitForPlayerAction(HashSet<Type> allowedActionTypes, List<Guid?> players, IGameTimer[] turnTimers, PlayerAction[] timeOutActions)
        {
            while (true)
            {
                Thread.Sleep(50);
                this.cancellationToken.ThrowIfCancellationRequested();

                for (var index = 0; index < turnTimers.Length; index++)
                {
                    if (turnTimers[index].IsLate)
                    {
                        // TODO: return time out player action
                        // players[index] = null;
                        // return timeOutActions[index]
                    }
                }

                if (this.playerActions.TryDequeue(out var playerAction))
                {
                    var playerActionTypeName = playerAction.GetType().Name;
                    var playerName = this.playersById[playerAction.InitiatingPlayerId].Name;
                    var message = $"Received {playerActionTypeName} from {playerName}";

                    if (this.IsAutomaticPlayerAction(playerAction))
                    {
                        //players[index] = null;
                        this.log.Add(message + " AUTOMATIC");
                        return playerAction;
                    }

                    if (!players.Contains(playerAction.InitiatingPlayerId))
                    {
                        this.log.Add(message + " PLAYER MISSING");
                        continue;
                    }

                    if (!allowedActionTypes.Contains(playerAction.GetType()))
                    {
                        this.log.Add(message + " NO MATCH");
                        continue;
                    }

                    var index = players.FindIndex(playerId => playerId == playerAction.InitiatingPlayerId);
                    players[index] = null;

                    this.log.Add($"Validated {playerActionTypeName} from {playerName}");
                    return playerAction;
                }
            }
        }

        private ConcurrentQueue<PlayerAction> playerActions = new ConcurrentQueue<PlayerAction>();

        public void Post(PlayerAction playerAction)
        {
            this.playerActions.Enqueue(playerAction);
        }
        #endregion

        #region Structures
        public interface IActionManager
        {
            void AddExpectedActionsForPlayer(Guid playerId, params Type[] actionsTypes);
            HashSet<Type> GetExpectedActionsForPlayer(Guid playerId);
            void SetExpectedActionsForPlayer(Guid playerId, params Type[] actionTypes);
            bool ValidateAction(PlayerAction playerAction);
        }

        private class ActionManager : IActionManager
        {
            private readonly Dictionary<Guid, HashSet<Type>> actionTypesByPlayerId = new Dictionary<Guid, HashSet<Type>>();

            public void AddExpectedActionsForPlayer(Guid playerId, params Type[] actionTypes)
            {
                if (actionTypes == null || actionTypes.Length == 0)
                    throw new Exception("Must add at least one action type to player");

                foreach (var actionType in actionTypes)
                    this.actionTypesByPlayerId[playerId].Add(actionType);
            }

            public HashSet<Type> GetExpectedActionsForPlayer(Guid playerId)
            {
                return this.actionTypesByPlayerId[playerId];
            }

            public void SetExpectedActionsForPlayer(Guid playerId, params Type[] actionTypes)
            {
                if (actionTypes == null || actionTypes.Length == 0)
                    this.actionTypesByPlayerId[playerId] = null;
                else
                    this.actionTypesByPlayerId[playerId] = new HashSet<Type>(actionTypes);
            }

            public bool ValidateAction(PlayerAction playerAction)
            {
                var initiatingPlayerId = playerAction.InitiatingPlayerId;
                if (this.actionTypesByPlayerId.ContainsKey(initiatingPlayerId))
                {
                    return 
                        this.actionTypesByPlayerId[initiatingPlayerId] != null &&
                        this.actionTypesByPlayerId[initiatingPlayerId].Contains(playerAction.GetType());
                }

                return false;
            }
        }
        #endregion
    }
}
