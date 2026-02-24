using System;
using Godot;
using TacticsBattle.Models;

namespace TacticsBattle.Services;

public class BattleService : IBattleService
{
    private readonly IGameStateService _gameState;
    private readonly IMapService _mapService;

    public event Action<Unit, Unit, int>? OnAttackExecuted;
    public event Action<Unit>? OnUnitDefeated;
    public event Action? OnEnemyTurnFinished;

    public BattleService(IGameStateService gameState, IMapService mapService)
    {
        _gameState = gameState;
        _mapService = mapService;
    }

    public int CalculateDamage(Unit attacker, Unit defender)
    {
        float baseDamage = Math.Max(1, attacker.Attack - defender.Defense);
        // Simple variance ±10%
        float variance = 0.9f + (float)(GD.Randf() * 0.2f);
        return (int)(baseDamage * variance);
    }

    public int ExecuteAttack(Unit attacker, Unit defender)
    {
        if (attacker.HasAttacked)
        {
            GD.Print($"{attacker.Name} already attacked this turn.");
            return 0;
        }

        int damage = CalculateDamage(attacker, defender);
        defender.Hp = Math.Max(0, defender.Hp - damage);
        attacker.HasAttacked = true;

        GD.Print($"{attacker.Name} attacks {defender.Name} for {damage} damage! ({defender.Hp}/{defender.MaxHp} HP left)");
        OnAttackExecuted?.Invoke(attacker, defender, damage);

        if (!defender.IsAlive)
        {
            GD.Print($"{defender.Name} was defeated!");
            _mapService.MoveUnit(defender, new Godot.Vector2I(-1, -1)); // Remove from map
            _gameState.RemoveUnit(defender);
            OnUnitDefeated?.Invoke(defender);
            _gameState.CheckVictoryCondition();
        }

        return damage;
    }

    public void RunEnemyTurn()
    {
        GD.Print("--- Enemy AI thinking... ---");
        var enemies = _gameState.EnemyUnits;

        foreach (var enemy in enemies)
        {
            RunEnemyUnitAI(enemy);
        }

        GD.Print("--- Enemy turn complete ---");
        OnEnemyTurnFinished?.Invoke();
        _gameState.EndTurn();
    }

    private void RunEnemyUnitAI(Unit enemy)
    {
        // Find nearest player unit
        Unit? nearest = null;
        int minDist = int.MaxValue;
        foreach (var player in _gameState.PlayerUnits)
        {
            int dist = _mapService.ManhattanDistance(enemy.Position, player.Position);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = player;
            }
        }

        if (nearest == null) return;

        // Try to attack
        var targets = _mapService.GetAttackableTargets(enemy);
        if (targets.Count > 0)
        {
            ExecuteAttack(enemy, targets[0]);
            return;
        }

        // Move toward nearest player
        if (!enemy.HasMoved)
        {
            var reachable = _mapService.GetReachableTiles(enemy);
            Godot.Vector2I bestMove = enemy.Position;
            int bestDist = minDist;

            foreach (var tile in reachable)
            {
                int d = _mapService.ManhattanDistance(tile, nearest.Position);
                if (d < bestDist)
                {
                    bestDist = d;
                    bestMove = tile;
                }
            }

            if (bestMove != enemy.Position)
            {
                GD.Print($"{enemy.Name} moves from {enemy.Position} to {bestMove}");
                _mapService.MoveUnit(enemy, bestMove);
            }

            // Attack after moving
            targets = _mapService.GetAttackableTargets(enemy);
            if (targets.Count > 0)
            {
                ExecuteAttack(enemy, targets[0]);
            }
        }
    }
}
