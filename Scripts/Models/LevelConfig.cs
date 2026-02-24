using System.Collections.Generic;
using Godot;

namespace TacticsBattle.Models;

public enum MapTheme { Forest, River, Mountain }

/// <summary>Spawn descriptor for one unit in a level.</summary>
public record UnitSpawnInfo(string Name, UnitType Type, Team Team, Vector2I Position);

/// <summary>Immutable data class describing a complete level configuration.</summary>
public class LevelConfig
{
    public string  LevelName   { get; init; } = "";
    public string  Description { get; init; } = "";
    public string  Difficulty  { get; init; } = "";
    public int     MapWidth    { get; init; } = 8;
    public int     MapHeight   { get; init; } = 8;
    public MapTheme Theme      { get; init; } = MapTheme.Forest;
    public IReadOnlyList<UnitSpawnInfo> Units { get; init; } = new List<UnitSpawnInfo>();
}
