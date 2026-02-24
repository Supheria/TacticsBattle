using System.Collections.Generic;
using Godot;
using TacticsBattle.Models;

namespace TacticsBattle.Systems;

/// <summary>
/// Enemy AI decision-making — pure static functions.
/// Decides WHAT to do; the caller (BattleService) executes the decisions
/// and fires events.
/// </summary>
public static class AISystem
{
    /// <summary>
    /// For each living enemy unit, return the best move tile (or current position
    /// if no better tile exists) and the attack target (or null).
    ///
    /// Uses terrain-aware Dijkstra distances to navigate through fords and passes
    /// without getting stuck at local Manhattan-distance minima.
    /// </summary>
    public static IEnumerable<EnemyAction> PlanTurn(
        IReadOnlyList<Unit> enemies,
        IReadOnlyList<Unit> players,
        System.Func<Unit, List<Vector2I>>              getReachable,
        System.Func<Unit, List<Unit>>                  getTargets,
        System.Func<Vector2I, Dictionary<Vector2I,int>> terrainDist)
    {
        foreach (var enemy in enemies)
        {
            if (!enemy.IsAlive) continue;

            // ── Find nearest player by terrain distance ────────────────────
            Unit? target    = null;
            int   minDist   = int.MaxValue;
            Dictionary<Vector2I, int>? distMap = null;

            foreach (var player in players)
            {
                var d    = terrainDist(player.Position);
                int dist = d.GetValueOrDefault(enemy.Position, int.MaxValue);
                if (dist < minDist) { minDist = dist; target = player; distMap = d; }
            }

            if (target == null || distMap == null) continue;

            // ── Choose best move tile ──────────────────────────────────────
            var reachable = getReachable(enemy);
            var bestTile  = enemy.Position;
            int bestDist  = minDist;

            foreach (var tile in reachable)
            {
                int d = distMap.GetValueOrDefault(tile, int.MaxValue);
                if (d < bestDist) { bestDist = d; bestTile = tile; }
            }

            // ── Choose attack target (after hypothetical move) ─────────────
            Unit? victim = null;
            var attackTargets = getTargets(enemy);
            if (attackTargets.Count > 0) victim = attackTargets[0];

            yield return new EnemyAction(enemy, bestTile, victim);
        }
    }

    /// <summary>The plan for one enemy unit this turn.</summary>
    public sealed record EnemyAction(
        Unit     Enemy,
        Vector2I MoveTo,
        Unit?    AttackTarget);
}
