﻿
namespace SoC.Harness
{
  using System;
  using System.Collections.Generic;
  using System.Windows;
  using System.Xml;
  using Jabberwocky.SoC.Library;
  using Jabberwocky.SoC.Library.GameBoards;
  using Jabberwocky.SoC.Library.Interfaces;
  using Jabberwocky.SoC.Library.Storage;
  using SoC.Harness.ViewModels;

  /// <summary>
  /// Interaction logic for MainWindow.xaml
  /// </summary>
  public partial class MainWindow : Window
  {
    LocalGameController localGameController;

    public MainWindow()
    {
      this.InitializeComponent();

      this.PlayArea.EndTurnEvent = this.EndTurnEventHandler;
      this.PlayArea.StartGameEvent = this.StartGameEventHandler;

      this.localGameController = new LocalGameController(new NumberGenerator(), new PlayerPool());
      this.localGameController.GameJoinedEvent = this.GameJoinedEventHandler;
      this.localGameController.InitialBoardSetupEvent = this.InitialBoardSetupEventHandler;
    }

    private void StartGameEventHandler()
    {
      this.localGameController.JoinGame();
      this.localGameController.LaunchGame();
      this.localGameController.StartGameSetup();
    }

    private void EndTurnEventHandler(int message, object data)
    {
      switch (message)
      {
        case 1: {
            var tuple = (Tuple<uint, uint>)data;
            this.localGameController.ContinueGameSetup(tuple.Item1, tuple.Item2);
            break;
        }
      }
    }

    private void GameJoinedEventHandler(PlayerDataModel[] playerDataModels)
    {
      this.TopLeftPlayer.DataContext = new PlayerViewModel(playerDataModels[0]);
      this.BottomLeftPlayer.DataContext = new PlayerViewModel(playerDataModels[1]);
      this.TopRightPlayer.DataContext = new PlayerViewModel(playerDataModels[2]);
      this.BottomRightPlayer.DataContext = new PlayerViewModel(playerDataModels[3]);
    }

    private void InitialBoardSetupEventHandler(GameBoard board)
    {
      this.PlayArea.Initialise(board);
    }
  }
}
