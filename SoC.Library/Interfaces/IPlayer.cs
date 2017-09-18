﻿
namespace Jabberwocky.SoC.Library.Interfaces
{
  using System;
  using System.IO;

  public interface IPlayer
  {
    #region Properties
    Int32 BrickCount { get; }
    Int32 GrainCount { get; }
    Int32 LumberCount { get; }
    Int32 OreCount { get; }
    Int32 WoolCount { get; }
    Guid Id { get; }
    String Name { get; }
    Int32 ResourcesCount { get; }
    Boolean IsComputer { get; }
    #endregion

    #region Methods
    PlayerDataView GetDataView();
    void AddResources(ResourceClutch resourceClutch);
    void RemoveResources(ResourceClutch resourceClutch);
    void Load(Stream stream);
    #endregion
  }
}
