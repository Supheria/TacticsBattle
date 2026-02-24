using Godot;

namespace TacticsBattle.Models;

public enum TileType { Grass, Forest, Mountain, Water }

public class Tile
{
    public Vector2I Position { get; }
    public TileType Type { get; }
    public bool IsWalkable => Type != TileType.Water;
    public int MovementCost => Type switch
    {
        TileType.Grass    => 1,
        TileType.Forest   => 2,
        TileType.Mountain => 3,
        _                 => 99,
    };

    public Unit? OccupyingUnit { get; set; }

    public Tile(Vector2I position, TileType type)
    {
        Position = position;
        Type = type;
    }
}
