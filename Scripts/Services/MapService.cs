using System.Collections.Generic;
using Godot;
using TacticsBattle.Models;

namespace TacticsBattle.Services;

public class MapService : IMapService
{
    private readonly Tile[,] _tiles;

    public int MapWidth  { get; }
    public int MapHeight { get; }

    private static readonly Vector2I[] Dirs =
        { Vector2I.Up, Vector2I.Down, Vector2I.Left, Vector2I.Right };

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

    // ── Level 2 — River Crossing (10 × 8) ────────────────────────────────────
    // Water band at y = 3-4.
    // FIX: TWO-tile-wide fords at x = 2-3 (left) and x = 7-8 (right) instead of
    //      single tiles — prevents a single enemy from fully blocking a crossing.
    private TileType RiverTile(int x, int y)
    {
        bool isFord = (x == 2 || x == 3 || x == 7 || x == 8);
        if ((y == 3 || y == 4) && !isFord) return TileType.Water;
        // Forest fringe at top corners for visual variety
        if ((x <= 1 || x >= 8) && y <= 1)  return TileType.Forest;
        // Rough terrain at outer edges south of river
        if ((x == 0 || x == 9) && y >= 5)  return TileType.Mountain;
        return TileType.Grass;
    }

    // ── Level 3 — Mountain Pass (8 × 12) ─────────────────────────────────────
    // Mountain walls channel units through a 4-tile-wide central corridor
    // (x = 2-5). Forest and water create tactical cover inside the pass.
    private TileType MountainTile(int x, int y)
    {
        // Outer walls throughout mid-section
        if ((x == 0 || x == 7) && y >= 2 && y <= 9)  return TileType.Mountain;
        // Inner walls narrow the pass in the middle rows
        if ((x == 1 || x == 6) && y >= 3 && y <= 8)  return TileType.Mountain;
        // Forest clusters inside the pass (tactical cover)
        if ((x == 3 || x == 4) && y >= 4 && y <= 6)  return TileType.Forest;
        // Water pool near the south end of the pass
        if ((x == 2 || x == 5) && y == 8)             return TileType.Water;
        return TileType.Grass;
    }

    // ── IMapService ───────────────────────────────────────────────────────────

    public Tile GetTile(int x, int y)            => _tiles[x, y];
    public Tile GetTile(Vector2I pos)            => _tiles[pos.X, pos.Y];
    public bool IsValidPosition(int x, int y)    => x >= 0 && y >= 0 && x < MapWidth && y < MapHeight;
    public bool IsValidPosition(Vector2I p)      => IsValidPosition(p.X, p.Y);

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

    /// <summary>
    /// BFS reachable tiles respecting movement costs and the no-overlap rule.
    /// Units may pass through allied tiles but can only stop on empty tiles.
    /// Enemy tiles block passage entirely.
    /// </summary>
    public List<Vector2I> GetReachableTiles(Unit unit)
    {
        var reachable = new List<Vector2I>();
        var queue     = new Queue<(Vector2I pos, int mp)>();
        var visited   = new HashSet<Vector2I>();

        queue.Enqueue((unit.Position, unit.MoveRange));
        visited.Add(unit.Position);

        while (queue.Count > 0)
        {
            var (pos, mp) = queue.Dequeue();

            // Destination must be empty (no-overlap rule)
            if (pos != unit.Position && _tiles[pos.X, pos.Y].OccupyingUnit == null)
                reachable.Add(pos);

            if (mp <= 0) continue;

            foreach (var dir in Dirs)
            {
                var next = pos + dir;
                if (!IsValidPosition(next) || visited.Contains(next)) continue;
                var tile = GetTile(next);
                if (!tile.IsWalkable) continue;
                // Cannot pass through enemy units
                if (tile.OccupyingUnit != null && tile.OccupyingUnit.Team != unit.Team) continue;
                visited.Add(next);
                queue.Enqueue((next, mp - tile.MovementCost));
            }
        }
        return reachable;
    }

    public List<Unit> GetAttackableTargets(Unit attacker)
    {
        var targets = new List<Unit>();
        for (int x = 0; x < MapWidth;  x++)
        for (int y = 0; y < MapHeight; y++)
        {
            var u = _tiles[x, y].OccupyingUnit;
            if (u == null || u.Team == attacker.Team || !u.IsAlive) continue;
            if (ManhattanDistance(attacker.Position, u.Position) <= attacker.AttackRange)
                targets.Add(u);
        }
        return targets;
    }

    public int ManhattanDistance(Vector2I a, Vector2I b) =>
        System.Math.Abs(a.X - b.X) + System.Math.Abs(a.Y - b.Y);

    /// <summary>
    /// Dijkstra from <paramref name="start"/> considering only terrain movement costs.
    /// Unit positions are IGNORED — this gives the true "geographic" distance through
    /// the terrain so the AI can find the right direction even through narrow corridors
    /// like river fords or mountain passes.
    /// </summary>
    public Dictionary<Vector2I, int> TerrainDistances(Vector2I start)
    {
        var dist = new Dictionary<Vector2I, int> { [start] = 0 };
        var pq   = new PriorityQueue<Vector2I, int>();
        pq.Enqueue(start, 0);

        while (pq.Count > 0)
        {
            pq.TryDequeue(out var pos, out int d);
            if (d > dist.GetValueOrDefault(pos, int.MaxValue)) continue; // stale entry

            foreach (var dir in Dirs)
            {
                var next = pos + dir;
                if (!IsValidPosition(next)) continue;
                var tile = GetTile(next);
                if (!tile.IsWalkable) continue;      // water / impassable
                int nd = d + tile.MovementCost;
                if (nd < dist.GetValueOrDefault(next, int.MaxValue))
                {
                    dist[next] = nd;
                    pq.Enqueue(next, nd);
                }
            }
        }
        return dist;
    }
}
