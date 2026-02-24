using System.Collections.Generic;
using System.Linq;
using Godot;
using TacticsBattle.Models;
using TacticsBattle.Systems;

namespace TacticsBattle.Services;

/// <summary>
/// Stateful map service. Owns the tile grid and unit placement.
/// Pathfinding is delegated to MovementSystem (pure functions).
///
/// Tile rules are resolved via ITileRuleProvider (strategy pattern) —
/// swap the provider to change terrain traversal costs globally.
///
/// BUG FIX: MoveUnit now clears the origin cell before checking
/// destination validity, so units killed off-map (-1,-1) are properly
/// removed from their tile.
/// </summary>
public class MapService : IMapService
{
    private readonly Tile[,]          _tiles;
    private readonly ITileRuleProvider _rules;

    public int MapWidth  { get; }
    public int MapHeight { get; }

    public MapService(int width, int height, MapTheme theme, ITileRuleProvider rules)
    {
        MapWidth  = width;
        MapHeight = height;
        _rules    = rules;
        _tiles    = new Tile[width, height];
        GenerateMap(theme);
    }

    // ── Map generation ────────────────────────────────────────────────────────

    private void GenerateMap(MapTheme theme)
    {
        for (int x = 0; x < MapWidth;  x++)
        for (int y = 0; y < MapHeight; y++)
        {
            var type = PickTileType(x, y, theme);
            var rule = _rules.GetRule(type);
            _tiles[x, y] = new Tile(new Vector2I(x, y), type, rule.Walkable, rule.MovementCost);
        }
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

    private static TileType RiverTile(int x, int y)
    {
        bool isFord = x == 2 || x == 3 || x == 7 || x == 8;
        if ((y == 3 || y == 4) && !isFord) return TileType.Water;
        if ((x <= 1 || x >= 8) && y <= 1)  return TileType.Forest;
        if ((x == 0 || x == 9) && y >= 5)  return TileType.Mountain;
        return TileType.Grass;
    }

    private static TileType MountainTile(int x, int y)
    {
        if ((x == 0 || x == 7) && y >= 2 && y <= 9) return TileType.Mountain;
        if ((x == 1 || x == 6) && y >= 3 && y <= 8) return TileType.Mountain;
        if ((x == 3 || x == 4) && y >= 4 && y <= 6) return TileType.Forest;
        if ((x == 2 || x == 5) && y == 8)            return TileType.Water;
        return TileType.Grass;
    }

    // ── IMapService ───────────────────────────────────────────────────────────

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

    // BUG FIX: Always clear origin cell first.
    // Previously checked IsValidPosition(newPos) before clearing origin —
    // so a (-1,-1) "death move" left the corpse on its original tile, blocking movement.
    public void MoveUnit(Unit unit, Vector2I newPos)
    {
        // Clear origin (always, even if destination is off-map)
        if (IsValidPosition(unit.Position))
            _tiles[unit.Position.X, unit.Position.Y].OccupyingUnit = null;

        unit.Position = newPos;
        unit.HasMoved = true;

        // Place on destination only if in bounds
        if (IsValidPosition(newPos))
            _tiles[newPos.X, newPos.Y].OccupyingUnit = unit;
    }

    public List<Unit> GetAttackableTargets(Unit attacker) =>
        (from x in Enumerable.Range(0, MapWidth)
         from y in Enumerable.Range(0, MapHeight)
         let u = _tiles[x, y].OccupyingUnit
         where u != null && u.Team != attacker.Team && u.IsAlive
            && MovementSystem.ManhattanDistance(attacker.Position, u.Position) <= attacker.AttackRange
         select u).ToList();

    // Delegate pathfinding to pure-function Systems
    public List<Vector2I> GetReachableTiles(Unit unit) =>
        MovementSystem.GetReachableTiles(_tiles, MapWidth, MapHeight, unit);

    public Dictionary<Vector2I, int> TerrainDistances(Vector2I origin) =>
        MovementSystem.TerrainDistances(_tiles, MapWidth, MapHeight, origin);

    public int ManhattanDistance(Vector2I a, Vector2I b) =>
        MovementSystem.ManhattanDistance(a, b);
}
