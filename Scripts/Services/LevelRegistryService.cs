using System.Collections.Generic;
using TacticsBattle.Models;

namespace TacticsBattle.Services;

/// <summary>
/// Implements ILevelRegistryService by reading from the static LevelRegistry
/// and SelectedLevel.  No data is stored here — this is just the DI-visible
/// facade over pure-static data structures.
/// </summary>
public sealed class LevelRegistryService : ILevelRegistryService
{
    public IReadOnlyList<LevelDefinition> AllLevels  => LevelRegistry.All;
    public LevelDefinition                ActiveLevel =>
        LevelRegistry.Get(SelectedLevel.Index)
        ?? throw new System.InvalidOperationException(
               $"No level at index {SelectedLevel.Index}");
}
