using System.Collections.Generic;

namespace TacticsBattle.Services;

public record LevelMenuItem(
    string Name,
    string Description,
    string Difficulty,
    string ScenePath);

/// <summary>Exposes level metadata for the level-select UI.</summary>
public interface ILevelMenuService
{
    IReadOnlyList<LevelMenuItem> Levels { get; }
}
