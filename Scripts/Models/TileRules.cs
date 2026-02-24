namespace TacticsBattle.Models;

/// <summary>
/// Immutable movement rule for one tile type.
/// Produced by ITileRuleProvider; consumed by MapService at tile creation.
/// </summary>
public sealed record TileRule(bool Walkable, int MovementCost);
