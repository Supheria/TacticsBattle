using System;
using System.Collections.Generic;
using Godot;
using TacticsBattle.Models;

namespace TacticsBattle.Services;

public class BattleService : IBattleService
{
    private readonly IGameStateService _gameState;
    private readonly IMapService       _mapService;

    public event Action<Unit, Unit, int>? OnAttackExecuted;
    public event Action<Unit>?            OnUnitDefeated;
    public event Action?                  OnEnemyTurnFinished;

    public BattleService(IGameStateService gameState, IMapService mapService)
    {
        _gameState  = gameState;
        _mapService = mapService;
    }

    public int CalculateDamage(Unit attacker, Unit defender)
    {
        float base_ = Math.Max(1, attacker.Attack - defender.Defense);
        float var_  = 0.9f + GD.Randf() * 0.2f;
        return (int)(base_ * var_);
    }

    public int ExecuteAttack(Unit attacker, Unit defender)
    {
        if (attacker.HasAttacked) return 0;

        int dmg = CalculateDamage(attacker, defender);
        defender.Hp = Math.Max(0, defender.Hp - dmg);
        attacker.HasAttacked = true;

        GD.Print($"{attacker.Name} → {defender.Name}  -{dmg} HP  ({defender.Hp}/{defender.MaxHp})");
        OnAttackExecuted?.Invoke(attacker, defender, dmg);

        if (!defender.IsAlive)
        {
            GD.Print($"  ☠ {defender.Name} defeated!");
            _mapService.MoveUnit(defender, new Vector2I(-1, -1));
            _gameState.RemoveUnit(defender);
            OnUnitDefeated?.Invoke(defender);
            _gameState.CheckVictoryCondition();
        }
        return dmg;
    }

    public void RunEnemyTurn()
    {
        GD.Print("--- Enemy AI ---");
        // Copy list: collection may change as units die
        var enemies = new List<Unit>(_gameState.EnemyUnits);
        foreach (var enemy in enemies)
        {
            if (enemy.IsAlive) RunEnemyUnitAI(enemy);
        }
        GD.Print("--- Enemy done ---");
        OnEnemyTurnFinished?.Invoke();
        _gameState.EndTurn();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Enemy AI — terrain-aware navigation
    //
    //  FIX: The old greedy Manhattan approach froze at river fords because
    //  moving toward the ford INCREASES the straight-line distance before it
    //  decreases it.  We now use TerrainDistances() (Dijkstra, terrain only)
    //  which correctly scores detours through corridors and crossings.
    //
    //  FIX: Old code attacked-then-returned without ever moving.  Now we
    //  always move first (to a better position), then attack.
    // ─────────────────────────────────────────────────────────────────────────

    private void RunEnemyUnitAI(Unit enemy)
    {
        // ── Step 1: Find target — nearest player by terrain distance ──────────
        Unit? target     = null;
        int   minTerrain = int.MaxValue;
        Dictionary<Vector2I, int>? distToTarget = null;

        foreach (var player in _gameState.PlayerUnits)
        {
            // Dijkstra from player tile (terrain only, ignoring units)
            var d = _mapService.TerrainDistances(player.Position);
            int dist = d.GetValueOrDefault(enemy.Position, int.MaxValue);
            if (dist < minTerrain)
            {
                minTerrain    = dist;
                target        = player;
                distToTarget  = d;
            }
        }

        if (target == null || distToTarget == null) return;

        // ── Step 2: Move toward target using terrain distance map ─────────────
        // Always move first (even if we can already attack) so ranged units
        // don't just sit at their spawn point and eventually run out of targets.
        if (!enemy.HasMoved)
        {
            var reachable = _mapService.GetReachableTiles(enemy);
            var bestTile  = enemy.Position;
            int bestDist  = minTerrain;

            foreach (var tile in reachable)
            {
                int d = distToTarget.GetValueOrDefault(tile, int.MaxValue);
                if (d < bestDist) { bestDist = d; bestTile = tile; }
            }

            if (bestTile != enemy.Position)
            {
                GD.Print($"  {enemy.Name} {enemy.Position} → {bestTile}  (terrain dist {minTerrain}→{bestDist})");
                _mapService.MoveUnit(enemy, bestTile);
            }
        }

        // ── Step 3: Attack ────────────────────────────────────────────────────
        var targets = _mapService.GetAttackableTargets(enemy);
        if (targets.Count > 0)
            ExecuteAttack(enemy, targets[0]);
    }
}
