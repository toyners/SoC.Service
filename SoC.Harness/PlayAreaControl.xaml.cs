﻿
namespace SoC.Harness
{
  using System;
  using System.Collections.Generic;
  using System.Windows.Controls;
  using System.Windows.Media.Imaging;
  using Jabberwocky.SoC.Library;
  using Jabberwocky.SoC.Library.GameBoards;

  /// <summary>
  /// Interaction logic for BoardUI.xaml
  /// </summary>
  public partial class PlayAreaControl : UserControl
  {
    #region Construction
    public PlayAreaControl()
    {
      this.InitializeComponent();
    }
    #endregion

    #region Methods
    public void Initialise(IGameBoard board)
    {
      var resourceBitmaps = this.CreateResourceBitmaps();
      var numberBitmaps = this.CreateNumberBitmaps();

      var middleX = (int)this.Background.Width / 2;
      var middleY = (int)this.Background.Height / 2;
      const int cellHeight = 90;
      const int cellWidth = 90;

      var layoutColumnData = new[]
      {
        new LayoutColumnData { X = middleX - (cellWidth * 2) + 4, Y = middleY - (cellHeight / 2) - cellHeight, Count = 3 },
        new LayoutColumnData { X = middleX - (cellWidth / 4) * 5, Y = middleY - (cellHeight * 2), Count = 4 },
        new LayoutColumnData { X = middleX - (cellWidth / 2), Y = middleY - (cellHeight / 2) - (cellHeight * 2), Count = 5 },
        new LayoutColumnData { X = middleX + (cellWidth / 4) - 2, Y = middleY - (cellHeight * 2), Count = 4 },
        new LayoutColumnData { X = middleX + cellWidth - 4, Y = middleY - (cellHeight / 2) - cellHeight, Count = 3 }
      };

      BitmapImage resourceBitmap = null;
      BitmapImage numberBitmap = null;
      var hexData = board.GetHexInformation();
      int hexDataIndex = 0;

      foreach (var columnData in layoutColumnData)
      {
        var count = columnData.Count;
        var x = columnData.X;
        var y = columnData.Y;

        while (count-- > 0)
        {
          this.GetBitmaps(hexData[hexDataIndex++], resourceBitmaps, numberBitmaps, out resourceBitmap, out numberBitmap);
          this.PlaceHex(resourceBitmap, numberBitmap, x, y);
          y += cellHeight;
        }
      }
    }

    private Image CreateImage(BitmapImage bitmapImage, String name)
    {
      return new Image
      {
        Width = bitmapImage.Width * 2,
        Height = bitmapImage.Height * 2,
        Name = name,
        Source = bitmapImage,
        StretchDirection = StretchDirection.Both
      };
    }

    private Dictionary<uint, BitmapImage> CreateNumberBitmaps()
    {
#pragma warning disable IDE0009 // Member access should be qualified.
      return new Dictionary<uint, BitmapImage>
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
        { ResourceTypes.Brick, new BitmapImage(new Uri(@"resources\resourcetypes\brick.png", UriKind.Relative)) },
        { ResourceTypes.Grain, new BitmapImage(new Uri(@"resources\resourcetypes\grain.png", UriKind.Relative)) },
        { ResourceTypes.Lumber, new BitmapImage(new Uri(@"resources\resourcetypes\lumber.png", UriKind.Relative)) },
        { ResourceTypes.Ore, new BitmapImage(new Uri(@"resources\resourcetypes\ore.png", UriKind.Relative)) },
        { ResourceTypes.Wool, new BitmapImage(new Uri(@"resources\resourcetypes\wool.png", UriKind.Relative)) }
      };
#pragma warning restore IDE0009 // Member access should be qualified.
    }

    private void GetBitmaps(Tuple<ResourceTypes?, uint> hexData, Dictionary<ResourceTypes, BitmapImage> resourceBitmaps, Dictionary<uint, BitmapImage> numberBitmaps, out BitmapImage resourceBitmap, out BitmapImage numberBitmap)
    {
      if (!hexData.Item1.HasValue)
      {
        resourceBitmap = new BitmapImage(new Uri(@"resources\desert.png", UriKind.Relative));
      }
      else
      {
        resourceBitmap = resourceBitmaps[hexData.Item1.Value];
      }

      numberBitmap = (hexData.Item2 != 0 ? numberBitmaps[hexData.Item2] : null);
    }

    private void PlaceHex(BitmapImage resourceBitmap, BitmapImage numberBitmap, int x, int y)
    {
      var resourceImage = this.CreateImage(resourceBitmap, string.Empty);
      this.Background.Children.Add(resourceImage);
      Canvas.SetLeft(resourceImage, x);
      Canvas.SetTop(resourceImage, y);

      if (numberBitmap == null)
      {
        return;
      }

      var numberImage = this.CreateImage(numberBitmap, string.Empty);
      this.Background.Children.Add(numberImage);
      Canvas.SetLeft(numberImage, x);
      Canvas.SetTop(numberImage, y);
    }
    #endregion

    #region Structures
    private struct LayoutColumnData
    {
      public int X, Y;
      public uint Count;
    }
    #endregion
  }
}