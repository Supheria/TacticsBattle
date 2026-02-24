using System.Collections.Generic;
using Godot;
using TacticsBattle.Models;

namespace TacticsBattle.Systems;

/// <summary>
/// Pure-function movement algorithms.
/// Takes data in, returns data out — no state, no services, no Godot nodes.
/// MapService delegates here; tests can call these functions directly.
/// </summary>
public static class MovementSystem
{
    private static readonly Vector2I[] Dirs =
        { Vector2I.Up, Vector2I.Down, Vector2I.Left, Vector2I.Right };

    /// <summary>
    /// BFS reachable tiles respecting movement point costs and the no-overlap rule.
    /// A unit may pass through allied tiles but cannot stop on any occupied tile.
    /// Enemy-occupied tiles block passage entirely.
    /// </summary>
    public static List<Vector2I> GetReachableTiles(
        Tile[,] tiles, int mapW, int mapH, Unit unit)
    {
        var reachable = new List<Vector2I>();
        var queue     = new Queue<(Vector2I pos, int mp)>();
        var visited   = new HashSet<Vector2I>();

        queue.Enqueue((unit.Position, unit.MoveRange));
        visited.Add(unit.Position);

        while (queue.Count > 0)
        {
            var (pos, mp) = queue.Dequeue();

            if (pos != unit.Position && tiles[pos.X, pos.Y].OccupyingUnit == null)
                reachable.Add(pos);

            if (mp <= 0) continue;

            foreach (var dir in Dirs)
            {
                var next = pos + dir;
                if (!InBounds(next, mapW, mapH) || visited.Contains(next)) continue;
                var tile = tiles[next.X, next.Y];
                if (!tile.IsWalkable) continue;
                if (tile.OccupyingUnit != null && tile.OccupyingUnit.Team != unit.Team) continue;
                visited.Add(next);
                queue.Enqueue((next, mp - tile.MovementCost));
            }
        }
        return reachable;
    }

    /// <summary>
    /// Dijkstra from <paramref name="origin"/> considering only terrain costs (units ignored).
    /// Returns every reachable tile with its minimum movement-cost distance.
    /// Used by the AI to navigate through fords, passes, and other terrain features.
    /// </summary>
    public static Dictionary<Vector2I, int> TerrainDistances(
        Tile[,] tiles, int mapW, int mapH, Vector2I origin)
    {
        var dist = new Dictionary<Vector2I, int> { [origin] = 0 };
        var pq   = new PriorityQueue<Vector2I, int>();
        pq.Enqueue(origin, 0);

        while (pq.Count > 0)
        {
            pq.TryDequeue(out var pos, out int d);
            if (d > dist.GetValueOrDefault(pos, int.MaxValue)) continue;

            foreach (var dir in Dirs)
            {
                var next = pos + dir;
                if (!InBounds(next, mapW, mapH)) continue;
                var tile = tiles[next.X, next.Y];
                if (!tile.IsWalkable) continue;
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

    public static int ManhattanDistance(Vector2I a, Vector2I b) =>
        System.Math.Abs(a.X - b.X) + System.Math.Abs(a.Y - b.Y);

    private static bool InBounds(Vector2I p, int w, int h) =>
        p.X >= 0 && p.Y >= 0 && p.X < w && p.Y < h;
}
