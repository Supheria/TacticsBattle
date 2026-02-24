using System.Collections.Generic;

namespace TacticsBattle.Models;

/// <summary>Immutable movement rules for one tile type.</summary>
public sealed record TileRule(bool Walkable, int MovementCost);

/// <summary>
/// Static registry of tile movement rules.
/// Adding a new TileType only requires adding one entry here.
/// </summary>
public static class TileRuleLibrary
{
    private static readonly IReadOnlyDictionary<TileType, TileRule> Rules =
        new Dictionary<TileType, TileRule>
        {
            [TileType.Grass]    = new(Walkable: true,  MovementCost: 1),
            [TileType.Forest]   = new(Walkable: true,  MovementCost: 2),
            [TileType.Mountain] = new(Walkable: true,  MovementCost: 3),
            [TileType.Water]    = new(Walkable: false, MovementCost: 99),
        };

    public static TileRule Get(TileType type) =>
        Rules.TryGetValue(type, out var r) ? r
        : throw new System.ArgumentException($"No rule for TileType.{type}");
}
