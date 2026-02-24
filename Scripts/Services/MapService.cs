using System.Collections.Generic;
using Godot;
using TacticsBattle.Models;

namespace TacticsBattle.Services;

public class MapService : IMapService
{
    private readonly Tile[,] _tiles;

    public int MapWidth { get; }
    public int MapHeight { get; }

    public MapService(int width = 8, int height = 8)
    {
        MapWidth = width;
        MapHeight = height;
        _tiles = new Tile[width, height];
        GenerateMap();
    }

    private void GenerateMap()
    {
        // Simple deterministic map layout
        for (int x = 0; x < MapWidth; x++)
        for (int y = 0; y < MapHeight; y++)
        {
            var type = (x, y) switch
            {
                (3, 3) or (3, 4) or (4, 3) or (4, 4) => TileType.Forest,
                (0, 7) or (7, 0) or (1, 7) or (7, 1) => TileType.Mountain,
                (6, 6) or (6, 7) or (7, 6) or (7, 7) => TileType.Water,
                _ => TileType.Grass,
            };
            _tiles[x, y] = new Tile(new Vector2I(x, y), type);
        }
    }

    public Tile GetTile(int x, int y) => _tiles[x, y];
    public Tile GetTile(Vector2I pos) => _tiles[pos.X, pos.Y];
    public bool IsValidPosition(int x, int y) => x >= 0 && y >= 0 && x < MapWidth && y < MapHeight;
    public bool IsValidPosition(Vector2I pos) => IsValidPosition(pos.X, pos.Y);

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

    public List<Vector2I> GetReachableTiles(Unit unit)
    {
        var reachable = new List<Vector2I>();
        var queue = new Queue<(Vector2I pos, int remaining)>();
        var visited = new HashSet<Vector2I>();

        queue.Enqueue((unit.Position, unit.MoveRange));
        visited.Add(unit.Position);

        while (queue.Count > 0)
        {
            var (pos, remaining) = queue.Dequeue();
            reachable.Add(pos);

            if (remaining <= 0) continue;

            var dirs = new[] { Vector2I.Up, Vector2I.Down, Vector2I.Left, Vector2I.Right };
            foreach (var dir in dirs)
            {
                var next = pos + dir;
                if (!IsValidPosition(next) || visited.Contains(next)) continue;
                var tile = GetTile(next);
                if (!tile.IsWalkable) continue;
                if (tile.OccupyingUnit != null && tile.OccupyingUnit.Team != unit.Team) continue;
                visited.Add(next);
                queue.Enqueue((next, remaining - tile.MovementCost));
            }
        }
        reachable.Remove(unit.Position);
        return reachable;
    }

    public List<Unit> GetAttackableTargets(Unit attacker)
    {
        var targets = new List<Unit>();
        for (int x = 0; x < MapWidth; x++)
        for (int y = 0; y < MapHeight; y++)
        {
            var unit = _tiles[x, y].OccupyingUnit;
            if (unit == null || unit.Team == attacker.Team || !unit.IsAlive) continue;
            if (ManhattanDistance(attacker.Position, unit.Position) <= attacker.AttackRange)
                targets.Add(unit);
        }
        return targets;
    }

    public int ManhattanDistance(Vector2I a, Vector2I b) =>
        System.Math.Abs(a.X - b.X) + System.Math.Abs(a.Y - b.Y);
}
