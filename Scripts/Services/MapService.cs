using System.Collections.Generic;
using Godot;
using TacticsBattle.Models;
using TacticsBattle.Systems;

namespace TacticsBattle.Services;

/// <summary>
/// Stateful map service: owns the tile grid and unit placement.
/// All pathfinding algorithms are delegated to MovementSystem (pure functions).
/// </summary>
public class MapService : IMapService
{
    private readonly Tile[,] _tiles;

    public int MapWidth  { get; }
    public int MapHeight { get; }

    public MapService(int width = 8, int height = 8, MapTheme theme = MapTheme.Forest)
    {
        MapWidth  = width;
        MapHeight = height;
        _tiles    = new Tile[width, height];
        GenerateMap(theme);
    }

    // ── Map generation ────────────────────────────────────────────────────────

    private void GenerateMap(MapTheme theme)
    {
        for (int x = 0; x < MapWidth;  x++)
        for (int y = 0; y < MapHeight; y++)
            _tiles[x, y] = new Tile(new Vector2I(x, y), PickTileType(x, y, theme));
    }

    private TileType PickTileType(int x, int y, MapTheme theme) => theme switch
    {
        MapTheme.River    => RiverTile(x, y),
        MapTheme.Mountain => MountainTile(x, y),
        _                 => ForestTile(x, y),
    };

    private static TileType ForestTile(int x, int y) => (x, y) switch
    {
        (3,3) or (3,4) or (4,3) or (4,4) => TileType.Forest,
        (0,7) or (7,0) or (1,7) or (7,1) => TileType.Mountain,
        (6,6) or (6,7) or (7,6) or (7,7) => TileType.Water,
        _                                 => TileType.Grass,
    };

    // Two-tile-wide fords at x=2-3 and x=7-8
    private TileType RiverTile(int x, int y)
    {
        bool isFord = x == 2 || x == 3 || x == 7 || x == 8;
        if ((y == 3 || y == 4) && !isFord) return TileType.Water;
        if ((x <= 1 || x >= 8) && y <= 1)  return TileType.Forest;
        if ((x == 0 || x == 9) && y >= 5)  return TileType.Mountain;
        return TileType.Grass;
    }

    private TileType MountainTile(int x, int y)
    {
        if ((x == 0 || x == 7) && y >= 2 && y <= 9)  return TileType.Mountain;
        if ((x == 1 || x == 6) && y >= 3 && y <= 8)  return TileType.Mountain;
        if ((x == 3 || x == 4) && y >= 4 && y <= 6)  return TileType.Forest;
        if ((x == 2 || x == 5) && y == 8)             return TileType.Water;
        return TileType.Grass;
    }

    // ── IMapService — direct tile access ─────────────────────────────────────

    public Tile GetTile(int x, int y)         => _tiles[x, y];
    public Tile GetTile(Vector2I pos)         => _tiles[pos.X, pos.Y];
    public bool IsValidPosition(int x, int y) => x >= 0 && y >= 0 && x < MapWidth && y < MapHeight;
    public bool IsValidPosition(Vector2I p)   => IsValidPosition(p.X, p.Y);

    public Unit? GetUnitAt(Vector2I pos) =>
        IsValidPosition(pos) ? _tiles[pos.X, pos.Y].OccupyingUnit : null;

    public void PlaceUnit(Unit unit, Vector2I pos)
    {
        if (!IsValidPosition(pos)) return;
        _tiles[pos.X, pos.Y].OccupyingUnit = unit;
        unit.Position = pos;
    }

    public void MoveUnit(Unit unit, Vector2I newPos)
    {
        if (!IsValidPosition(unit.Position) || !IsValidPosition(newPos)) return;
        _tiles[unit.Position.X, unit.Position.Y].OccupyingUnit = null;
        _tiles[newPos.X, newPos.Y].OccupyingUnit = unit;
        unit.Position = newPos;
        unit.HasMoved = true;
    }

    public List<Unit> GetAttackableTargets(Unit attacker)
    {
        var targets = new List<Unit>();
        for (int x = 0; x < MapWidth;  x++)
        for (int y = 0; y < MapHeight; y++)
        {
            var u = _tiles[x, y].OccupyingUnit;
            if (u == null || u.Team == attacker.Team || !u.IsAlive) continue;
            if (MovementSystem.ManhattanDistance(attacker.Position, u.Position) <= attacker.AttackRange)
                targets.Add(u);
        }
        return targets;
    }

    // ── Delegate pathfinding to MovementSystem (pure functions) ──────────────

    public List<Vector2I> GetReachableTiles(Unit unit) =>
        MovementSystem.GetReachableTiles(_tiles, MapWidth, MapHeight, unit);

    public Dictionary<Vector2I, int> TerrainDistances(Vector2I origin) =>
        MovementSystem.TerrainDistances(_tiles, MapWidth, MapHeight, origin);

    public int ManhattanDistance(Vector2I a, Vector2I b) =>
        MovementSystem.ManhattanDistance(a, b);
}
