using System.Collections.Generic;
using TacticsBattle.Models;

namespace TacticsBattle.Services;

/// <summary>
/// Single DI service that replaces the three separate ILevelConfigService
/// and ILevelMenuService interfaces.
///
/// Provides both:
///   - the full list of levels (for the menu)
///   - the currently-selected level's definition (for map/spawn setup)
/// </summary>
public interface ILevelRegistryService
{
    IReadOnlyList<LevelDefinition> AllLevels  { get; }
    LevelDefinition                ActiveLevel { get; }
}
