using Godot;

namespace TacticsBattle.Models;

public enum TileType { Grass, Forest, Mountain, Water }

/// <summary>
/// Pure data container for one map tile.
/// Movement rules come from TileRuleLibrary — no numbers hardcoded here.
/// </summary>
public class Tile
{
    public Vector2I Position { get; }
    public TileType Type     { get; }

    // Delegate to library — Tile holds no rule data itself
    public bool IsWalkable    => TileRuleLibrary.Get(Type).Walkable;
    public int  MovementCost  => TileRuleLibrary.Get(Type).MovementCost;

    public Unit? OccupyingUnit { get; set; }

    public Tile(Vector2I position, TileType type)
    {
        Position = position;
        Type     = type;
    }
}
