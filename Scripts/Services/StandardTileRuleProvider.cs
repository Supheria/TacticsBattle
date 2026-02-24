using System.Collections.Generic;
using TacticsBattle.Models;

namespace TacticsBattle.Services;

/// <summary>
/// Default tile movement strategy — standard traversal costs.
/// </summary>
public sealed class StandardTileRuleProvider : ITileRuleProvider
{
    private static readonly IReadOnlyDictionary<TileType, TileRule> Rules =
        new Dictionary<TileType, TileRule>
        {
            [TileType.Grass]    = new(Walkable:true,  MovementCost:1),
            [TileType.Forest]   = new(Walkable:true,  MovementCost:2),
            [TileType.Mountain] = new(Walkable:true,  MovementCost:3),
            [TileType.Water]    = new(Walkable:false, MovementCost:99),
        };

    public TileRule GetRule(TileType type) =>
        Rules.TryGetValue(type, out var r) ? r
        : throw new System.ArgumentException($"No rule for TileType.{type}");
}
