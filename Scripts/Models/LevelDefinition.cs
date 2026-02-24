using System.Collections.Generic;
using Godot;
using TacticsBattle.Models.Components;

namespace TacticsBattle.Models;

public enum MapTheme { Forest, River, Mountain }

/// <summary>
/// Spawn descriptor for one unit.
/// ExtraComponents are stacked on top of the archetype's DefaultComponents,
/// enabling per-level customisation without changing archetype definitions.
/// </summary>
public sealed record UnitSpawnInfo(
    string                          Name,
    UnitType                        Type,
    Team                            Team,
    Vector2I                        Position,
    IReadOnlyList<IUnitComponent>?  ExtraComponents = null);

/// <summary>
/// Immutable level record — pure data, no scene paths, no services.
/// </summary>
public sealed record LevelDefinition(
    int                           Index,
    string                        Name,
    string                        Description,
    string                        Difficulty,
    int                           MapWidth,
    int                           MapHeight,
    MapTheme                      Theme,
    IReadOnlyList<UnitSpawnInfo>  Units);
