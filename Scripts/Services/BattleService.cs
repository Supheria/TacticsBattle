using System;
using System.Collections.Generic;
using Godot;
using TacticsBattle.Models;
using TacticsBattle.Systems;

namespace TacticsBattle.Services;

/// <summary>
/// Stateful battle service: manages event dispatch and coordinates
/// combat / AI execution. All algorithms are in CombatSystem and AISystem.
/// </summary>
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

    public int CalculateDamage(Unit attacker, Unit defender) =>
        CombatSystem.CalculateDamage(attacker, defender);

    public int ExecuteAttack(Unit attacker, Unit defender)
    {
        if (attacker.HasAttacked) return 0;

        var (dmg, defeated) = CombatSystem.ApplyAttack(attacker, defender);
        GD.Print($"{attacker.Name} → {defender.Name}  -{dmg} HP  ({defender.Hp}/{defender.MaxHp})");
        OnAttackExecuted?.Invoke(attacker, defender, dmg);

        if (defeated)
        {
            GD.Print($"  ☠ {defender.Name} defeated!");
            _mapService.MoveUnit(defender, new Vector2I(-1, -1)); // off-map
            _gameState.RemoveUnit(defender);
            OnUnitDefeated?.Invoke(defender);
            _gameState.CheckVictoryCondition();
        }
        return dmg;
    }

    public void RunEnemyTurn()
    {
        GD.Print("--- Enemy AI ---");
        var enemies = new List<Unit>(_gameState.EnemyUnits);

        var actions = AISystem.PlanTurn(
            enemies:      enemies,
            players:      _gameState.PlayerUnits,
            getReachable: u => _mapService.GetReachableTiles(u),
            getTargets:   u => _mapService.GetAttackableTargets(u),
            terrainDist:  pos => _mapService.TerrainDistances(pos));

        foreach (var action in actions)
        {
            if (!action.Enemy.IsAlive) continue;

            // Move
            if (action.MoveTo != action.Enemy.Position)
            {
                GD.Print($"  {action.Enemy.Name} → {action.MoveTo}");
                _mapService.MoveUnit(action.Enemy, action.MoveTo);
            }

            // Attack (re-query after move; position changed)
            var realTargets = _mapService.GetAttackableTargets(action.Enemy);
            if (realTargets.Count > 0)
                ExecuteAttack(action.Enemy, realTargets[0]);
        }

        GD.Print("--- Enemy done ---");
        OnEnemyTurnFinished?.Invoke();
        _gameState.EndTurn();
    }
}
