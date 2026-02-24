using System.Collections.Generic;
using Godot;

namespace TacticsBattle.Models;

public enum MapTheme { Forest, River, Mountain }

/// <summary>Spawn descriptor: what unit, which team, where.</summary>
public sealed record UnitSpawnInfo(
    string   Name,
    UnitType Type,
    Team     Team,
    Vector2I Position);

/// <summary>
/// Immutable data record for one level.
/// Contains NO scene path and NO service references — pure data.
/// Scene routing is the router's job, not the level's.
/// </summary>
public sealed record LevelDefinition(
    int                        Index,
    string                     Name,
    string                     Description,
    string                     Difficulty,
    int                        MapWidth,
    int                        MapHeight,
    MapTheme                   Theme,
    IReadOnlyList<UnitSpawnInfo> Units);
