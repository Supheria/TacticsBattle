using Godot;

namespace TacticsBattle.Models;

public enum TileType { Grass, Forest, Mountain, Water }

/// <summary>
/// Tile instance — stores pre-resolved movement rules (no static lookup).
/// ITileRuleProvider resolves these at map creation time; Tile is pure data.
/// </summary>
public class Tile
{
    public Vector2I Position     { get; }
    public TileType Type         { get; }
    public bool     IsWalkable   { get; }
    public int      MovementCost { get; }

    public Unit? OccupyingUnit { get; set; }

    public Tile(Vector2I position, TileType type, bool walkable, int moveCost)
    {
        Position     = position;
        Type         = type;
        IsWalkable   = walkable;
        MovementCost = moveCost;
    }
}
