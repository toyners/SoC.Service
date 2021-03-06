﻿
namespace Jabberwocky.SoC.Library.Store
{
  public interface IGameDataReader<SectionKey, Key, Enum>
  {
    IGameDataSection<SectionKey, Key, Enum> this[SectionKey sectionKey] { get; }
    IGameDataSection<SectionKey, Key, Enum>[] GetSections(SectionKey sectionKey);
  }
}
