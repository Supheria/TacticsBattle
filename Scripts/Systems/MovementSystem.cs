using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using TacticsBattle.Models;

namespace TacticsBattle.Systems;

/// <summary>
/// Pure-function movement algorithms. Zero state, zero Godot nodes, zero DI.
///
/// GetReachableTiles BUG FIX:
///   Old BFS marked tiles "visited" on enqueue rather than on optimal-cost arrival.
///   On mixed terrain (Grass cost=1, Forest cost=2) a tile reachable via a cheap
///   path (Forest→Grass remaining MP=1) was discarded when a longer path
///   (Grass→Grass remaining MP=2) had already enqueued it — so the second, better
///   path was never explored.
///   Fix: replace visited-bool with bestMp-dict (Dijkstra).  Only re-enqueue a
///   neighbour when a better remaining-MP is found.
/// </summary>
public static class MovementSystem
{
    private static readonly Vector2I[] Dirs =
        { Vector2I.Up, Vector2I.Down, Vector2I.Left, Vector2I.Right };

    /// <summary>
    /// Returns all tiles a unit can reach, respecting:
    ///   - terrain movement costs
    ///   - SlowedComponent (via EffectiveMoveRange)
    ///   - no-overlap: may pass through allies, cannot stop on any occupied tile
    ///   - enemy tiles block passage entirely
    /// </summary>
    public static List<Vector2I> GetReachableTiles(
        Tile[,] tiles, int mapW, int mapH, Unit unit)
    {
        // bestMp[pos] = best (highest) remaining movement points when reaching pos
        var bestMp = new Dictionary<Vector2I, int>();
        var pq     = new PriorityQueue<Vector2I, int>(); // min-heap on (-remaining)

        var start = unit.Position;
        bestMp[start] = unit.EffectiveMoveRange;
        pq.Enqueue(start, -unit.EffectiveMoveRange);

        while (pq.Count > 0)
        {
            pq.TryDequeue(out var pos, out _);
            int mp = bestMp[pos];
            if (mp <= 0) continue;

            foreach (var dir in Dirs)
            {
                var next = pos + dir;
                if (!InBounds(next, mapW, mapH)) continue;
                var tile = tiles[next.X, next.Y];
                if (!tile.IsWalkable) continue;
                // Enemy-occupied tiles block passage
                if (tile.OccupyingUnit != null && tile.OccupyingUnit.Team != unit.Team) continue;

                int remaining = mp - tile.MovementCost;
                if (remaining < 0) continue;

                // Only enqueue if this path leaves more movement points
                if (remaining > bestMp.GetValueOrDefault(next, -1))
                {
                    bestMp[next] = remaining;
                    pq.Enqueue(next, -remaining);
                }
            }
        }

        // Destinations: reachable non-start tiles that are currently empty
        return bestMp.Keys
            .Where(p => p != start && tiles[p.X, p.Y].OccupyingUnit == null)
            .ToList();
    }

    /// <summary>
    /// Dijkstra from origin over terrain only (units ignored).
    /// Used by AISystem to navigate corridors and fords correctly.
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
        Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

    private static bool InBounds(Vector2I p, int w, int h) =>
        p.X >= 0 && p.Y >= 0 && p.X < w && p.Y < h;

}
