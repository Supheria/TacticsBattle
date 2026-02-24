using System.Collections.Generic;
using Godot;
using GodotSharpDI.Abstractions;
using TacticsBattle.Models;
using TacticsBattle.Services;

namespace TacticsBattle.Users;

/// <summary>
/// DI User: reads ILevelConfigService to spawn units, then manages move/attack helpers.
/// Injecting ILevelConfigService (alongside the three core services) is all that is
/// needed to switch levels — no code changes beyond swapping the [Host] in the scope.
/// </summary>
[User]
public sealed partial class UnitManager : Node, IDependenciesResolved
{
    [Inject] private IGameStateService?   _gameState;
    [Inject] private IMapService?         _mapService;
    [Inject] private IBattleService?      _battleService;
    [Inject] private ILevelConfigService? _levelConfig;

    private int _nextId = 1;

    public override partial void _Notification(int what);

    public override void _Ready() => GD.Print("[UnitManager] Waiting for DI...");

    void IDependenciesResolved.OnDependenciesResolved(bool ok)
    {
        if (!ok) { GD.PrintErr("[UnitManager] DI FAILED."); return; }
        SpawnInitialUnits();
        SubscribeEvents();
        _gameState!.BeginPlayerTurn();
    }

    private void SpawnInitialUnits()
    {
        foreach (var s in _levelConfig!.Config.Units)
            Spawn(s.Name, s.Type, s.Team, s.Position);
    }

    private void Spawn(string name, UnitType type, Team team, Vector2I pos)
    {
        var unit = new Unit(_nextId++, name, type, team, pos);
        _gameState!.AddUnit(unit);
        _mapService!.PlaceUnit(unit, pos);
        GD.Print($"  Spawned: {unit}");
    }

    private void SubscribeEvents()
    {
        _battleService!.OnUnitDefeated     += u => GD.Print($"[UnitManager] ☠ {u.Name}");
        _battleService.OnEnemyTurnFinished += ()  => GD.Print("[UnitManager] Enemy done.");
    }

    // ── Public helpers for BattleRenderer3D ──────────────────────────────────

    public bool TryMoveSelected(Vector2I target)
    {
        if (_gameState?.SelectedUnit is not { Team: Team.Player, HasMoved: false } unit) return false;
        if (!_mapService!.GetReachableTiles(unit).Contains(target)) return false;
        // FIX: also guard that destination is empty (no overlap)
        if (_mapService.GetUnitAt(target) != null) return false;
        _mapService.MoveUnit(unit, target);
        _gameState.NotifyUnitMoved(unit);
        return true;
    }

    public bool TryAttackTarget(Unit target)
    {
        if (_gameState?.SelectedUnit is not { Team: Team.Player, HasAttacked: false } attacker) return false;
        _battleService!.ExecuteAttack(attacker, target);
        return true;
    }
}
