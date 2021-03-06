﻿
namespace SoC.Harness.Views
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Windows.Media.Imaging;
    using Jabberwocky.SoC.Library;
    using Jabberwocky.SoC.Library.GameBoards;
    using SoC.Harness.ViewModels;

    public partial class PlayAreaControl : UserControl, INotifyPropertyChanged
    {
        #region Fields
        private const string blueSettlementImagePath = @"..\resources\settlements\blue_settlement.png";
        private const string redSettlementImagePath = @"..\resources\settlements\red_settlement.png";
        private const string greenSettlementImagePath = @"..\resources\settlements\green_settlement.png";
        private const string yellowSettlementImagePath = @"..\resources\settlements\yellow_settlement.png";

        private const string blueRoadHorizontalImagePath = @"..\resources\roads\blue_road_horizontal.png";
        private const string blueRoadLeftImagePath = @"..\resources\roads\blue_road_left.png";
        private const string blueRoadRightImagePath = @"..\resources\roads\blue_road_right.png";
        private const string redRoadHorizontalImagePath = @"..\resources\roads\red_road_horizontal.png";
        private const string redRoadLeftImagePath = @"..\resources\roads\red_road_left.png";
        private const string redRoadRightImagePath = @"..\resources\roads\red_road_right.png";
        private const string greenRoadHorizontalImagePath = @"..\resources\roads\green_road_horizontal.png";
        private const string greenRoadLeftImagePath = @"..\resources\roads\green_road_left.png";
        private const string greenRoadRightImagePath = @"..\resources\roads\green_road_right.png";
        private const string yellowRoadHorizontalImagePath = @"..\resources\roads\yellow_road_horizontal.png";
        private const string yellowRoadLeftImagePath = @"..\resources\roads\yellow_road_left.png";
        private const string yellowRoadRightImagePath = @"..\resources\roads\yellow_road_right.png";

        private const string greenBigIconImagePath = @"..\resources\icons\big_green_icon.png";
        private const string greenBigSelectedIconImagePath = @"..\resources\icons\big_selected_green_icon.png";
        private const string redBigIconImagePath = @"..\resources\icons\big_red_icon.png";
        private const string redBigSelectedIconImagePath = @"..\resources\icons\big_selected_red_icon.png";
        private const string yellowBigIconImagePath = @"..\resources\icons\big_yellow_icon.png";
        private const string yellowBigSelectedIconImagePath = @"..\resources\icons\big_selected_yellow_icon.png";

        private SettlementButtonControl[] settlementButtonControls;
        private Dictionary<string, RoadButtonControl> roadButtonControls;
        private Dictionary<Guid, string> settlementImagesByPlayerId;
        private Dictionary<Guid, string[]> roadImagesByPlayerId;
        private Dictionary<Guid, Tuple<string, string>> bigIconImagesByPlayerId;
        private Guid playerId;
        private HashSet<RoadButtonControl> visibleRoadButtonControls = new HashSet<RoadButtonControl>();
        private IList<ResourceButton> resourceControls = new List<ResourceButton>();
        private string resourceSelectionMessage;
        private PropertyChangedEventArgs confirmMessageChanged = new PropertyChangedEventArgs("ResourceSelectionMessage");
        private Image robberImage, selectedRobberLocationImage;
        private ControllerViewModel controllerViewModel;
        private int workingNumberOfResourcesToSelect;
        private Image currentRobberLocationHoverImage = null;
        private Dictionary<Image, Tuple<uint, Point>> locationsByImage = new Dictionary<Image, Tuple<uint, Point>>();
        private PlayerButton[] playerButtons;
        private PlayerButton selectedPlayerButton = null;
        #endregion

        #region Construction
        public PlayAreaControl()
        {
            this.InitializeComponent();

            this.playerButtons = new[] { this.LeftPlayerButton, this.MiddlePlayerButton, this.RightPlayerButton };
            this.LeftPlayerButton.ButtonClickEventHandler =
            this.MiddlePlayerButton.ButtonClickEventHandler =
            this.RightPlayerButton.ButtonClickEventHandler = this.PlayerSelectionButton_Click;
        }
        #endregion

        #region Properties
        
        public string ResourceSelectionMessage
        {
            get { return this.resourceSelectionMessage; }
            private set
            {
                this.resourceSelectionMessage = value;
                this.PropertyChanged.Invoke(this, this.confirmMessageChanged);
            }
        }
        #endregion

        #region Events
        public event PropertyChangedEventHandler PropertyChanged;
        #endregion

        #region Methods
        public void ContinueGame()
        {
            Task.Factory.StartNew(() =>
            {
                this.controllerViewModel.ContinueGame();
            });
        }

        public void EndTurn()
        {
            this.ConfirmButton.Visibility = this.EndTurnButton.Visibility = Visibility.Hidden;
            this.controllerViewModel.EndTurn();
        }

        public void Initialise(ControllerViewModel controllerViewModel)
        {
            this.DataContext = this.controllerViewModel = controllerViewModel;
            this.controllerViewModel.GameJoinedEvent += this.InitialisePlayerViews;
            this.controllerViewModel.InitialBoardSetupEvent += this.InitialBoardSetupEventHandler;
            this.controllerViewModel.RobberEvent += this.RobberEventHandler;
            this.controllerViewModel.RobbingChoicesEvent += this.RobbingChoicesEventHandler;
            this.controllerViewModel.StartPhaseEvent += this.StartPhaseEventHandler;
            this.controllerViewModel.PlaceInfrastructureEvent += this.PlaceInfrastructureEventHandler;
        }

        private void PlaceInfrastructureEventHandler(Guid playerId, uint settlementLocation, uint roadSegmentEndLocation)
        {
            this.PlaceSettlement(settlementLocation, playerId);
            this.PlaceRoad(settlementLocation, roadSegmentEndLocation, playerId);
        }

        public void InitialisePlayerViews(PlayerViewModel player1, PlayerViewModel player2, PlayerViewModel player3, PlayerViewModel player4)
        {
            this.playerId = player1.Id;

            this.settlementImagesByPlayerId = new Dictionary<Guid, string>();
            this.settlementImagesByPlayerId.Add(player1.Id, blueSettlementImagePath);
            this.settlementImagesByPlayerId.Add(player2.Id, redSettlementImagePath);
            this.settlementImagesByPlayerId.Add(player3.Id, greenSettlementImagePath);
            this.settlementImagesByPlayerId.Add(player4.Id, yellowSettlementImagePath);

            this.roadImagesByPlayerId = new Dictionary<Guid, string[]>();
            this.roadImagesByPlayerId.Add(player1.Id, new[] { blueRoadHorizontalImagePath, blueRoadLeftImagePath, blueRoadRightImagePath });
            this.roadImagesByPlayerId.Add(player2.Id, new[] { redRoadHorizontalImagePath, redRoadLeftImagePath, redRoadRightImagePath });
            this.roadImagesByPlayerId.Add(player3.Id, new[] { greenRoadHorizontalImagePath, greenRoadLeftImagePath, greenRoadRightImagePath });
            this.roadImagesByPlayerId.Add(player4.Id, new[] { yellowRoadHorizontalImagePath, yellowRoadLeftImagePath, yellowRoadRightImagePath });

            this.bigIconImagesByPlayerId = new Dictionary<Guid, Tuple<string, string>>();
            this.bigIconImagesByPlayerId.Add(player2.Id, new Tuple<string, string>(redBigIconImagePath, redBigSelectedIconImagePath));
            this.bigIconImagesByPlayerId.Add(player3.Id, new Tuple<string, string>(greenBigIconImagePath, greenBigSelectedIconImagePath));
            this.bigIconImagesByPlayerId.Add(player4.Id, new Tuple<string, string>(yellowBigIconImagePath, yellowBigSelectedIconImagePath));
        }

        public void RobberEventHandler(PlayerViewModel player, int numberOfResourcesToSelect)
        {
            if (numberOfResourcesToSelect > 0)
            {
                // Display resources for player to discard
                this.workingNumberOfResourcesToSelect = numberOfResourcesToSelect;
                this.ResourceSelectionMessage = $"Select {this.workingNumberOfResourcesToSelect} more resources to drop";
                this.ResourceSelectionConfirmButton.IsEnabled = false;

                var width = 100;
                var gutter = 10;
                var midX = 400;
                var midY = 200;
                var x = midX - ((player.Resources.Count * width) + ((player.Resources.Count - 1) * gutter) / 2);

                int resourceIndex = 0;
                for (; resourceIndex < player.Resources.Count; resourceIndex++)
                {
                    if (resourceIndex >= this.resourceControls.Count)
                    {
                        // Need a new resource control
                        var newButton = new ResourceButton(this.ResourceSelectedEventHandler);
                        this.resourceControls.Add(newButton);
                        this.ResourceSelectionLayer.Children.Add(newButton);
                    }

                    var resourceButton = this.resourceControls[resourceIndex];

                    var resourceType = this.GetResourceTypeAt(resourceIndex, player.Resources);
                    this.GetResourceCardImages(resourceType, out var imagePath, out var selectedImagePath);
                    resourceButton.OriginalImagePath = resourceButton.ImagePath = imagePath;
                    resourceButton.SelectedImagePath = selectedImagePath;
                    resourceButton.ResourceType = resourceType;

                    Canvas.SetLeft(resourceButton, x);
                    Canvas.SetTop(resourceButton, midY);
                    x += width + gutter;

                    resourceButton.Visibility = Visibility.Visible;
                }

                // Hide resource controls that are not needed this time.
                var resourceControlIndex = resourceIndex;
                for (; resourceControlIndex < this.resourceControls.Count; resourceControlIndex++)
                {
                    this.resourceControls[resourceControlIndex].Visibility = Visibility.Hidden;
                }

                this.ResourceSelectionLayer.Visibility = Visibility.Visible;
                return;
            }

            // Select hex to place robber
            this.RobberSelectionLayer.Visibility = Visibility.Visible;
        }

        public void StartGame()
        {
            this.BoardLayer.Visibility = Visibility.Visible;

            Task.Factory.StartNew(() =>
            {
                this.controllerViewModel.StartGame();
            });
        }

        private void BuildBackButton_Click(object sender, RoutedEventArgs e)
        {
            this.BuildActions.Visibility = Visibility.Hidden;
            this.PhaseActions.Visibility = Visibility.Visible;
        }

        private void BuildButton_Click(object sender, RoutedEventArgs e)
        {
            this.BuildActions.Visibility = Visibility.Visible;
        }

        private void BuildRoadButton_Click(object sender, RoutedEventArgs e)
        {
            this.RoadSelectionLayer.Visibility = Visibility.Visible;
            // Turn on the road buttons for all possible choices
            var connections = this.controllerViewModel.GetValidConnectionsForPlayerInfrastructure(this.playerId);
            if (connections == null || connections.Count == 0)
                return; //TODO: Show message that user has no valid connections to build on

            foreach (var connection in connections)
            {
                this.roadButtonControls[$"{connection.Location1}-{connection.Location2}"].Visibility = Visibility.Visible;
            }
        }

        private void BuildSettlementButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void BuyButton_Click(object sender, RoutedEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            this.EndTurn();
        }

        private void EndTurnButton_Click(object sender, RoutedEventArgs e)
        {
            this.EndTurn();
        }

        private ResourceTypes GetResourceTypeAt(int index, ResourceClutch resources)
        {
            if (index < resources.BrickCount)
                return ResourceTypes.Brick;

            if (index < resources.BrickCount + resources.GrainCount)
                return ResourceTypes.Grain;

            if (index < resources.BrickCount + resources.GrainCount + resources.LumberCount)
                return ResourceTypes.Lumber;

            if (index < resources.BrickCount + resources.GrainCount + resources.LumberCount + resources.OreCount)
                return ResourceTypes.Ore;

            return ResourceTypes.Wool;
        }

        private void GetResourceCardImages(ResourceTypes resourceType, out string imagePath, out string selectedImagePath)
        {
            switch (resourceType)
            {
                case ResourceTypes.Brick:
                {
                    imagePath = @"..\resources\resourcecards\brickcard.png";
                    selectedImagePath = @"..\resources\resourcecards\selected_brickcard.png";
                    break;
                }
                case ResourceTypes.Grain:
                {
                    imagePath = @"..\resources\resourcecards\graincard.png";
                    selectedImagePath = @"..\resources\resourcecards\selected_graincard.png";
                    break;
                }
                case ResourceTypes.Lumber:
                {
                    imagePath = @"..\resources\resourcecards\lumbercard.png";
                    selectedImagePath = @"..\resources\resourcecards\selected_lumbercard.png";
                    break;
                }
                case ResourceTypes.Ore:
                {
                    imagePath = @"..\resources\resourcecards\orecard.png";
                    selectedImagePath = @"..\resources\resourcecards\selected_orecard.png";
                    break;
                }
                default:
                {
                    imagePath = @"..\resources\resourcecards\woolcard.png";
                    selectedImagePath = @"..\resources\resourcecards\selected_woolcard.png";
                    break;
                }
            }
        }

        private void InitialBoardSetupEventHandler()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                this.InitialiseBoardLayer();

                this.InitialiseSettlementSelectionLayer();

                this.InitialiseRoadSelectionLayer();

                var settlementData = this.controllerViewModel.GetInitialSettlementData();
                if (settlementData != null)
                {
                    this.PlaceSettlements(settlementData);

                    var roadData = this.controllerViewModel.GetInitialRoadSegmentData();
                    if (roadData != null)
                        this.PlaceRoads(roadData);

                    var cityData = this.controllerViewModel.GetInitialCityData();
                    if (cityData != null)
                        this.PlaceCities(cityData);
                }
            });
        }

        private void PlaceCities(Dictionary<uint, Guid> cityData)
        {
        }

        private void PlaceRoads(Tuple<uint, uint, Guid>[] roadData)
        {
            foreach (var tuple in roadData)
                this.PlaceRoad(tuple.Item1, tuple.Item2, tuple.Item3);
        }

        private void PlaceRoad(uint startLocation, uint endLocation, Guid playerId)
        {
            var key = $"{startLocation}-{endLocation}";
            var control = this.roadButtonControls[key];
            this.PlaceRoad(control, playerId);
        }

        private void PlaceRoad(RoadButtonControl control, Guid playerId)
        {
            var roadImagePath = this.roadImagesByPlayerId[playerId][(int)control.RoadImageType];
            control.Visibility = Visibility.Hidden;
            this.PlaceRoadControl(control.X, control.Y, roadImagePath);
        }

        private void PlaceSettlements(Dictionary<uint, Guid> settlementData)
        {
            foreach (var kv in settlementData)
                this.PlaceSettlement(kv.Key, kv.Value);
        }

        private void PlaceSettlement(uint location, Guid playerId)
        {
            var control = this.settlementButtonControls[location];
            var settlementImagePath = this.settlementImagesByPlayerId[playerId];
            this.PlaceBuilding(control.X, control.Y, location.ToString(), settlementImagePath, this.SettlementLayer);

            this.HideLocalSettlementButtons(control);
        }

        private void HideLocalSettlementButtons(SettlementButtonControl control)
        {
            control.Visibility = Visibility.Hidden;
            var neighbouringLocations = this.controllerViewModel.GetNeighbouringLocationsFrom(control.Location);
            foreach (var neighbouringLocation in neighbouringLocations)
            {
                this.settlementButtonControls[neighbouringLocation].Visibility = Visibility.Hidden;
            }
        }

        private void InitialiseBoardLayer()
        {
            var resourceBitmaps = this.CreateResourceBitmaps();
            var numberBitmaps = this.CreateNumberBitmaps();

            var middleX = (int)this.BoardLayer.Width / 2;
            var middleY = (int)this.BoardLayer.Height / 2;
            const int cellHeight = 90;
            const int cellWidth = 90;

            var hexLayoutData = new[]
            {
                new HexLayoutData { X = middleX - (cellWidth * 2) + 4, Y = middleY - (cellHeight / 2) - cellHeight, Count = 3 },
                new HexLayoutData { X = middleX - (cellWidth / 4) * 5, Y = middleY - (cellHeight * 2), Count = 4 },
                new HexLayoutData { X = middleX - (cellWidth / 2), Y = middleY - (cellHeight / 2) - (cellHeight * 2), Count = 5 },
                new HexLayoutData { X = middleX + (cellWidth / 4) - 2, Y = middleY - (cellHeight * 2), Count = 4 },
                new HexLayoutData { X = middleX + cellWidth - 4, Y = middleY - (cellHeight / 2) - cellHeight, Count = 3 }
            };

            BitmapImage resourceBitmap = null;
            BitmapImage numberBitmap = null;
            var hexData = this.controllerViewModel.GetHexData();
            uint hexIndex = 0;

            foreach (var hexLayout in hexLayoutData)
            {
                var count = hexLayout.Count;
                var x = hexLayout.X;
                var y = hexLayout.Y;

                while (count-- > 0)
                {
                    var hexDetails = hexData[hexIndex];

                    this.GetBitmaps(hexDetails, resourceBitmaps, numberBitmaps, out resourceBitmap, out numberBitmap);

                    if (hexDetails.ResourceType == null)
                    {
                        // Initial starting location for the robber is the Desert hex
                        var robberBitmap = new BitmapImage(new Uri(@"resources\robber.png", UriKind.Relative));
                        this.robberImage = this.CreateImage(robberBitmap);
                        this.RobberLayer.Children.Add(this.robberImage);
                        Canvas.SetLeft(this.robberImage, x + 2);
                        Canvas.SetTop(this.robberImage, y);

                        var selectedRobberLocationBitmap = new BitmapImage(new Uri(@"resources\robber_selection.png", UriKind.Relative));
                        this.selectedRobberLocationImage = this.CreateImage(selectedRobberLocationBitmap);
                        this.selectedRobberLocationImage.MouseLeftButtonUp += this.Location_MouseClick;
                        this.RobberSelectionLayer.Children.Add(this.selectedRobberLocationImage);
                    }

                    this.PlaceHex(hexIndex, resourceBitmap, numberBitmap, x, y);
                    y += cellHeight;
                    hexIndex++;
                }
            }
        }

        private void InitialiseSettlementSelectionLayer()
        {
            this.settlementButtonControls = new SettlementButtonControl[GameBoard.StandardBoardLocationCount];

            uint location = 0;
            var settlementLayoutData = new[]
            {
            new SettlementLayoutData { X = 240, Y = 82, Dx = 21, Dy = 44, DirectionX = -1, Count = 7 },
            new SettlementLayoutData { X = 304, Y = 38, Dx = 21, Dy = 44, DirectionX = -1, Count = 9 },
            new SettlementLayoutData { X = 368, Y = -6, Dx = 21, Dy = 44, DirectionX = -1, Count = 11 },
            new SettlementLayoutData { X = 412, Y = -6, Dx = 21, Dy = 44, DirectionX = 1, Count = 11 },
            new SettlementLayoutData { X = 476, Y = 38, Dx = 21, Dy = 44, DirectionX = 1, Count = 9 },
            new SettlementLayoutData { X = 540, Y = 82, Dx = 21, Dy = 44, DirectionX = 1, Count = 7 },
          };

            foreach (var settlementData in settlementLayoutData)
            {
                var count = settlementData.Count;
                var x = settlementData.X;
                var y = settlementData.Y;
                var direction = settlementData.DirectionX;
                var dx = settlementData.Dx;
                var dy = settlementData.Dy;

                while (count-- > 0)
                {
                    var control = this.PlaceSettlementButton(x, y, location, location.ToString());
                    this.settlementButtonControls[location++] = control;
                    y += dy;

                    x += direction * dx;
                    direction = direction == -1 ? 1 : -1;
                }
            }
        }

        private void InitialiseRoadSelectionLayer()
        {
            string roadLeftIndicatorImagePath = @"..\resources\roads\road_left_indicator.png";
            string roadLeftImagePath = @"..\resources\roads\road_left.png";
            string roadRightIndicatorImagePath = @"..\resources\roads\road_right_indicator.png";
            string roadRightImagePath = @"..\resources\roads\road_right.png";
            string roadHorizontalIndicatorImagePath = @"..\resources\roads\road_horizontal_indicator.png";
            this.roadButtonControls = new Dictionary<string, RoadButtonControl>();

            var verticalRoadLayoutData = this.GetVerticalRoadLayoutData();

            string indicatorImagePath = null;
            string imagePath = null;
            RoadButtonControl.RoadImageTypes roadImageType;
            foreach (var verticalRoadLayout in verticalRoadLayoutData)
            {
                var useRightImage = verticalRoadLayout.StartWithRightImage;
                for (var index = 0; index < verticalRoadLayout.Locations.Length; index++)
                {
                    if (useRightImage)
                    {
                        indicatorImagePath = roadRightIndicatorImagePath;
                        imagePath = roadRightImagePath;
                        roadImageType = RoadButtonControl.RoadImageTypes.Right;
                    }
                    else
                    {
                        indicatorImagePath = roadLeftIndicatorImagePath;
                        imagePath = roadLeftImagePath;
                        roadImageType = RoadButtonControl.RoadImageTypes.Left;
                    }

                    var locationA = verticalRoadLayout.Locations[index];
                    var locationB = locationA - 1;
                    var control = this.PlaceRoadButton(locationA, locationB, verticalRoadLayout.XCoordinate, verticalRoadLayout.YCoordinates[index], indicatorImagePath, roadImageType);
                    useRightImage = !useRightImage;

                    this.roadButtonControls.Add(control.Id, control);
                    this.roadButtonControls.Add(control.AlternativeId, control);
                }
            }

            var horizontalRoadLayoutData = this.GetHorizontalRoadLayoutData();

            foreach (var horizontalRoadLayout in horizontalRoadLayoutData)
            {
                for (var index = 0; index < horizontalRoadLayout.Locations.Length; index++)
                {
                    var locationA = horizontalRoadLayout.Locations[index].Start;
                    var locationB = horizontalRoadLayout.Locations[index].End;
                    var control = this.PlaceRoadButton(locationA, locationB, horizontalRoadLayout.XCoordinate, horizontalRoadLayout.YCoordinates[index], roadHorizontalIndicatorImagePath, RoadButtonControl.RoadImageTypes.Horizontal);

                    this.roadButtonControls.Add(control.Id, control);
                    this.roadButtonControls.Add(control.AlternativeId, control);
                }
            }
        }

        private HorizontalRoadLayoutData[] GetHorizontalRoadLayoutData()
        {
            return new HorizontalRoadLayoutData[]
            {
            new HorizontalRoadLayoutData
            {
              XCoordinate = 246,
              Locations = new [] { new RoadData(0, 8), new RoadData(2, 10), new RoadData(4, 12), new RoadData(6, 14)},
              YCoordinates = new int[] { 87, 175, 264, 352 }
            },
            new HorizontalRoadLayoutData
            {
              XCoordinate = 312,
              Locations = new [] { new RoadData(7, 17), new RoadData(9, 19), new RoadData(11, 21), new RoadData(13, 23), new RoadData(15, 25)},
              YCoordinates = new int[] { 42, 130, 218, 308, 395 }
            },
            new HorizontalRoadLayoutData
            {
              XCoordinate = 378,
              Locations = new [] { new RoadData(16, 27), new RoadData(18, 29), new RoadData(20, 31), new RoadData(22, 33), new RoadData(24, 35), new RoadData(26, 37)},
              YCoordinates = new int[] { -3, 85, 174, 263, 354, 441 }
            },
            new HorizontalRoadLayoutData
            {
              XCoordinate = 444,
              Locations = new [] { new RoadData(28, 38), new RoadData(30, 40), new RoadData(32, 42), new RoadData(34, 44), new RoadData(36, 46)},
              YCoordinates = new int[] { 42, 130, 218, 308, 395 }
            },
            new HorizontalRoadLayoutData
            {
              XCoordinate = 510,
              Locations = new [] { new RoadData(39, 47), new RoadData(41, 49), new RoadData(43, 51), new RoadData(45, 53)},
              YCoordinates = new int[] { 87, 175, 264, 352 }
            }
            };
        }

        private VerticalRoadLayoutData[] GetVerticalRoadLayoutData()
        {
            return new VerticalRoadLayoutData[]
            {
            new VerticalRoadLayoutData
            {
              XCoordinate = 210,
              StartWithRightImage = true,
              Locations = new uint[] { 1, 2, 3, 4, 5, 6 },
              YCoordinates = new int[] { 89, 132, 177, 220, 266, 309 }
            },
            new VerticalRoadLayoutData
            {
              XCoordinate = 277,
              StartWithRightImage = true,
              Locations = new uint[] { 8, 9, 10, 11, 12, 13, 14, 15 },
              YCoordinates = new int[] { 44, 89, 132, 177, 220, 266, 309, 354 }
            },
            new VerticalRoadLayoutData
            {
              XCoordinate = 342,
              StartWithRightImage = true,
              Locations = new uint[] { 17, 18, 19, 20, 21, 22, 23, 24, 25, 26 },
              YCoordinates = new int[] { 1, 44, 89, 132, 177, 220, 266, 309, 354, 399 }
            },
            new VerticalRoadLayoutData
            {
              XCoordinate = 408,
              StartWithRightImage = false,
              Locations = new uint[] { 28, 29, 30, 31, 32, 33, 34, 35, 36, 37 },
              YCoordinates = new int[] { 1, 44, 89, 132, 177, 220, 266, 309, 354, 399 }
            },
            new VerticalRoadLayoutData
            {
              XCoordinate = 472,
              StartWithRightImage = false,
              Locations = new uint[] { 39, 40, 41, 42, 43, 44, 45, 46 },
              YCoordinates = new int[] { 44, 89, 132, 177, 220, 266, 309, 354 }
            },
            new VerticalRoadLayoutData
            {
              XCoordinate = 538,
              StartWithRightImage = false,
              Locations = new uint[] { 48, 49, 50, 51, 52, 53 },
              YCoordinates = new int[] { 89, 132, 177, 220, 266, 309 }
            }
            };
        }

        private Image CreateImage(BitmapImage bitmapImage)
        {
            return new Image
            {
                Width = bitmapImage.Width * 2,
                Height = bitmapImage.Height * 2,
                Source = bitmapImage,
                StretchDirection = StretchDirection.Both
            };
        }

        private Dictionary<int, BitmapImage> CreateNumberBitmaps()
        {
#pragma warning disable IDE0009 // Member access should be qualified.
            return new Dictionary<int, BitmapImage>
          {
            { 2, new BitmapImage(new Uri(@"resources\productionfactors\2.png", UriKind.Relative)) },
            { 3, new BitmapImage(new Uri(@"resources\productionfactors\3.png", UriKind.Relative)) },
            { 4, new BitmapImage(new Uri(@"resources\productionfactors\4.png", UriKind.Relative)) },
            { 5, new BitmapImage(new Uri(@"resources\productionfactors\5.png", UriKind.Relative)) },
            { 6, new BitmapImage(new Uri(@"resources\productionfactors\6.png", UriKind.Relative)) },
            { 8, new BitmapImage(new Uri(@"resources\productionfactors\8.png", UriKind.Relative)) },
            { 9, new BitmapImage(new Uri(@"resources\productionfactors\9.png", UriKind.Relative)) },
            { 10, new BitmapImage(new Uri(@"resources\productionfactors\10.png", UriKind.Relative)) },
            { 11, new BitmapImage(new Uri(@"resources\productionfactors\11.png", UriKind.Relative)) },
            { 12, new BitmapImage(new Uri(@"resources\productionfactors\12.png", UriKind.Relative)) }
          };
#pragma warning restore IDE0009 // Member access should be qualified.
        }

        private Dictionary<ResourceTypes, BitmapImage> CreateResourceBitmaps()
        {
#pragma warning disable IDE0009 // Member access should be qualified.
            return new Dictionary<ResourceTypes, BitmapImage>
          {
            { ResourceTypes.Brick, new BitmapImage(new Uri(@"resources\hextypes\brick.png", UriKind.Relative)) },
            { ResourceTypes.Grain, new BitmapImage(new Uri(@"resources\hextypes\grain.png", UriKind.Relative)) },
            { ResourceTypes.Lumber, new BitmapImage(new Uri(@"resources\hextypes\lumber.png", UriKind.Relative)) },
            { ResourceTypes.Ore, new BitmapImage(new Uri(@"resources\hextypes\ore.png", UriKind.Relative)) },
            { ResourceTypes.Wool, new BitmapImage(new Uri(@"resources\hextypes\wool.png", UriKind.Relative)) }
          };
#pragma warning restore IDE0009 // Member access should be qualified.
        }

        private void GetBitmaps(HexInformation hexData, Dictionary<ResourceTypes, BitmapImage> resourceBitmaps, Dictionary<int, BitmapImage> numberBitmaps, out BitmapImage resourceBitmap, out BitmapImage numberBitmap)
        {
            if (!hexData.ResourceType.HasValue)
            {
                resourceBitmap = new BitmapImage(new Uri(@"resources\hextypes\desert.png", UriKind.Relative));
            }
            else
            {
                resourceBitmap = resourceBitmaps[hexData.ResourceType.Value];
            }

            numberBitmap = (hexData.ProductionFactor != 0 ? numberBitmaps[hexData.ProductionFactor] : null);
        }

        private void Location_MouseClick(object sender, MouseButtonEventArgs e)
        {
            if (!this.controllerViewModel.SelectRobberLocation)
            {
                return;
            }

            var location = this.locationsByImage[this.currentRobberLocationHoverImage];
            Canvas.SetLeft(this.robberImage, location.Item2.X);
            Canvas.SetTop(this.robberImage, location.Item2.Y);
            this.RobberSelectionLayer.Visibility = Visibility.Hidden;
            this.controllerViewModel.SetRobberHex(location.Item1);
        }

        private void Location_MouseHover(object sender, MouseEventArgs e)
        {
            if (!this.controllerViewModel.SelectRobberLocation || sender == this.currentRobberLocationHoverImage)
            {
                return;
            }

            this.currentRobberLocationHoverImage = (Image)sender;
            this.RobberSelectionLayer.Visibility = Visibility.Visible;
            var location = this.locationsByImage[this.currentRobberLocationHoverImage];
            Canvas.SetLeft(this.selectedRobberLocationImage, location.Item2.X);
            Canvas.SetTop(this.selectedRobberLocationImage, location.Item2.Y);
        }

        private void PlaceHex(uint hexIndex, BitmapImage resourceBitmap, BitmapImage numberBitmap, int x, int y)
        {
            var resourceImage = this.CreateImage(resourceBitmap);
            this.BoardLayer.Children.Add(resourceImage);
            Canvas.SetLeft(resourceImage, x);
            Canvas.SetTop(resourceImage, y);

            if (numberBitmap == null)
            {
                this.locationsByImage.Add(resourceImage, new Tuple<uint, Point>(hexIndex, new Point(x, y)));
                resourceImage.MouseEnter += this.Location_MouseHover;
                return;
            }

            var numberImage = this.CreateImage(numberBitmap);
            numberImage.MouseEnter += this.Location_MouseHover;
            this.BoardLayer.Children.Add(numberImage);
            Canvas.SetLeft(numberImage, x);
            Canvas.SetTop(numberImage, y);

            this.locationsByImage.Add(numberImage, new Tuple<uint, Point>(hexIndex, new Point(x, y)));
        }

        private void PlaceRoadControl(double x, double y, string imagePath)
        {
            var control = new RoadControl(imagePath);
            this.RoadLayer.Children.Add(control);
            Canvas.SetLeft(control, x);
            Canvas.SetTop(control, y);
        }

        private RoadButtonControl PlaceRoadButton(uint start, uint end, double x, double y, string imagePath, RoadButtonControl.RoadImageTypes roadImageType)
        {
            var control = new RoadButtonControl(start, end, x, y, imagePath, roadImageType, this.RoadSelectedEventHandler);
            this.RoadSelectionLayer.Children.Add(control);
            Canvas.SetLeft(control, x);
            Canvas.SetTop(control, y);

            return control;
        }

        private SettlementButtonControl PlaceSettlementButton(double x, double y, uint id, string toolTip)
        {
            var control = new SettlementButtonControl(id, x, y, this.SettlementSelectedEventHandler);
            control.ToolTip = toolTip;
            this.SettlementSelectionLayer.Children.Add(control);
            Canvas.SetLeft(control, x);
            Canvas.SetTop(control, y);

            return control;
        }

        private void PlaceBuilding(double x, double y, string toolTip, string imagePath, Canvas canvas)
        {
            var control = new BuildingControl(imagePath);
            if (!string.IsNullOrEmpty(toolTip))
            {
                control.ToolTip = toolTip;
            }

            canvas.Children.Add(control);
            Canvas.SetLeft(control, x);
            Canvas.SetTop(control, y);
        }

        private void PlayerSelectionButton_Click(PlayerButton playerButton)
        {
            if (this.selectedPlayerButton != null && playerButton != this.selectedPlayerButton)
            {
                this.selectedPlayerButton.IsSelected = false;
                this.selectedPlayerButton = playerButton;
            }

            this.selectedPlayerButton = playerButton;
            this.PlayerSelectionConfirmButton.IsEnabled = true;
        }

        private void PlayerSelectionConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            this.controllerViewModel.GetRandomResourceFromOpponent(this.selectedPlayerButton.PlayerId);
        }

        private void ResourceSelectedEventHandler(ResourceButton resourceButton)
        {
            this.workingNumberOfResourcesToSelect -= resourceButton.IsSelected ? 1 : -1;
            this.ResourceSelectionMessage = $"Select {this.workingNumberOfResourcesToSelect} more resources to drop";
            this.ResourceSelectionConfirmButton.IsEnabled = this.workingNumberOfResourcesToSelect == 0;
        }

        private void ResourceSelectionConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            var brickCount = 0;
            var grainCount = 0;
            var lumberCount = 0;
            var oreCount = 0;
            var woolCount = 0;

            foreach (var resourceButton in this.resourceControls)
            {
                if (resourceButton.IsSelected)
                {
                    switch (resourceButton.ResourceType)
                    {
                        case ResourceTypes.Brick: brickCount++; break;
                        case ResourceTypes.Grain: grainCount++; break;
                        case ResourceTypes.Lumber: lumberCount++; break;
                        case ResourceTypes.Ore: oreCount++; break;
                        case ResourceTypes.Wool: woolCount++; break;
                    }
                }
            }

            this.ResourceSelectionLayer.Visibility = Visibility.Hidden;
            this.controllerViewModel.DropResourcesFromPlayer(new ResourceClutch(brickCount, grainCount, lumberCount, oreCount, woolCount));
        }

        private void RoadSelectedEventHandler(RoadButtonControl roadButtonControl)
        {
            this.controllerViewModel.InitialRoadEndLocation =
                roadButtonControl.Start == this.controllerViewModel.InitialSettlementLocation
                ? roadButtonControl.End : roadButtonControl.Start;

            this.PlaceRoad(roadButtonControl, this.playerId);

            foreach (var visibleRoadButtonControl in this.visibleRoadButtonControls)
            {
                visibleRoadButtonControl.Visibility = Visibility.Hidden;
            }

            this.visibleRoadButtonControls.Clear();

            this.RoadSelectionLayer.Visibility = Visibility.Hidden;

            if (!this.controllerViewModel.InGameSetup)
            {
                this.EndTurnButton.Visibility = Visibility.Visible;
                this.BuildActions.Visibility = Visibility.Hidden;

                this.controllerViewModel.BuildRoadSegment(roadButtonControl.Start, roadButtonControl.End);
            }
            else
            {
                this.ConfirmButton.Visibility = Visibility.Visible;
            }
        }

        private void RobbingChoicesEventHandler(List<Tuple<Guid, string, int>> choices)
        {
            var playerButtonIndex = 0;
            foreach (var tuple in choices)
            {
                var images = this.bigIconImagesByPlayerId[tuple.Item1];
                var playerButton = this.playerButtons[playerButtonIndex++];
                playerButton.Visibility = Visibility.Visible;
                playerButton.PlayerId = tuple.Item1;
                playerButton.ImagePath = playerButton.OriginalImagePath = images.Item1;
                playerButton.SelectedImagePath = images.Item2;
                playerButton.PlayerName = tuple.Item2;
                playerButton.ResourceCountMessage = $"{tuple.Item3} resource{(tuple.Item3 != 1 ? "s" : "")}";
            }

            // Hide player buttons that are not needed
            for (; playerButtonIndex < this.playerButtons.Length; playerButtonIndex++)
                this.playerButtons[playerButtonIndex].Visibility = Visibility.Hidden;

            this.PlayerSelectionConfirmButton.IsEnabled = false;
            this.PlayerSelectionLayer.Visibility = Visibility.Visible;
        }

        private void SettlementSelectedEventHandler(SettlementButtonControl settlementButtonControl)
        {
            this.controllerViewModel.InitialSettlementLocation = settlementButtonControl.Location;
            this.controllerViewModel.ShowSettlementSelection = false;

            // Turn off the controls for the location and its neighbours
            this.HideLocalSettlementButtons(settlementButtonControl);

            this.PlaceBuilding(settlementButtonControl.X, settlementButtonControl.Y, string.Empty, @"..\resources\settlements\blue_settlement.png", this.SettlementLayer);

            // Turn on the possible road controls for the location
            var roadEndLocations = this.controllerViewModel.GetValidConnectedLocationsFrom(settlementButtonControl.Location);
            for (var index = 0; index < roadEndLocations.Length; index++)
            {
                var id = string.Format("{0}-{1}", settlementButtonControl.Location, roadEndLocations[index]);
                var roadButtonControl = this.roadButtonControls[id];
                roadButtonControl.Visibility = Visibility.Visible;
                this.visibleRoadButtonControls.Add(roadButtonControl);
            }

            this.RoadSelectionLayer.Visibility = Visibility.Visible;
        }

        private void StartPhaseEventHandler(PhaseActions playerActions)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                this.TradeButton.Visibility = Visibility.Visible;
                this.TradeMarketButton.IsEnabled = playerActions.CanTradeWithMarket;
                this.TradePlayerButton.IsEnabled = playerActions.CanTradeWithPlayers;

                this.BuildSettlementButton.IsEnabled = playerActions.CanBuildSettlement;
                this.BuildSettlementButton.ToolTip = playerActions.BuildSettlementMessages;

                this.BuildRoadButton.IsEnabled = playerActions.CanBuildRoad;
                this.BuildRoadButton.ToolTip = playerActions.BuildRoadMessages;

                this.BuildCityButton.IsEnabled = playerActions.CanBuildCity;
                this.BuildCityButton.ToolTip = playerActions.BuildCityMessages;

                this.BuildButton.Visibility = Visibility.Visible;
                this.BuildButton.IsEnabled = playerActions.CanBuildSettlement | playerActions.CanBuildRoad | playerActions.CanBuildCity;
                this.BuildButton.ToolTip = playerActions.BuildSettlementMessages + playerActions.BuildRoadMessages + playerActions.BuildCityMessages;

                this.BuyButton.Visibility = Visibility.Visible;
                this.BuyButton.IsEnabled = playerActions.CanBuyDevelopmentCard;
                this.UseButton.Visibility = Visibility.Visible;
                this.UseButton.IsEnabled = playerActions.CanUseDevelopmentCard;

                this.PhaseActions.Visibility = Visibility.Visible;
                this.EndTurnButton.Visibility = Visibility.Visible;
            });
        }

        private void TradeBackButton_Click(object sender, RoutedEventArgs e)
        {
            this.TradeActions.Visibility = Visibility.Hidden;
            this.PhaseActions.Visibility = Visibility.Visible;
        }

        private void TradeButton_Click(object sender, RoutedEventArgs e)
        {
            this.PhaseActions.Visibility = Visibility.Hidden;
            this.TradeActions.Visibility = Visibility.Visible;
        }

        private void TradeMarketButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void TradePlayerButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void UseButton_Click(object sender, RoutedEventArgs e)
        {
            throw new NotImplementedException();
        }
        #endregion

        #region Structures
        private struct HexLayoutData
        {
            public int X, Y;
            public uint Count;
        }

        public struct RoadData
        {
            public uint Start, End;

            public RoadData(uint start, uint end)
            {
                this.Start = start;
                this.End = end;
            }
        }

        public struct SettlementLayoutData
        {
            public int X, Y, Dx, Dy, DirectionX;
            public uint Count;
        }

        public struct VerticalRoadLayoutData
        {
            public int XCoordinate;
            public uint[] Locations;
            public int[] YCoordinates;
            public bool StartWithRightImage;
        }

        public struct HorizontalRoadLayoutData
        {
            public int XCoordinate;
            public RoadData[] Locations;
            public int[] YCoordinates;
        }
        #endregion
    }
}
