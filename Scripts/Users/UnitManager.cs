using System.Collections.Generic;
using Godot;
using GodotSharpDI.Abstractions;
using TacticsBattle.Models;
using TacticsBattle.Services;

namespace TacticsBattle.Users;

/// <summary>
/// DI User: spawns units from ILevelRegistryService.ActiveLevel.Units.
/// No unit data, no stat numbers — all from LevelRegistry and UnitTemplateLibrary.
/// </summary>
[User]
public sealed partial class UnitManager : Node, IDependenciesResolved
{
    [Inject] private IGameStateService?    _gameState;
    [Inject] private IMapService?          _mapService;
    [Inject] private IBattleService?       _battleService;
    [Inject] private ILevelRegistryService? _registry;

    private int _nextId = 1;

    public override partial void _Notification(int what);
    public override void _Ready() => GD.Print("[UnitManager] Waiting for DI...");

    void IDependenciesResolved.OnDependenciesResolved(bool ok)
    {
        if (!ok) { GD.PrintErr("[UnitManager] DI FAILED."); return; }
        SpawnUnits();
        SubscribeEvents();
        _gameState!.BeginPlayerTurn();
    }

    private void SpawnUnits()
    {
        var level = _registry!.ActiveLevel;
        GD.Print($"[UnitManager] Spawning units for \"{level.Name}\"");
        foreach (var s in level.Units)
        {
            var unit = new Unit(_nextId++, s.Name, s.Type, s.Team, s.Position);
            _gameState!.AddUnit(unit);
            _mapService!.PlaceUnit(unit, s.Position);
            GD.Print($"  + {unit}");
        }
    }

    private void SubscribeEvents()
    {
        _battleService!.OnUnitDefeated    += u  => GD.Print($"[UnitManager] ☠ {u.Name}");
        _battleService.OnEnemyTurnFinished += () => GD.Print("[UnitManager] Enemy done.");
    }

    // ── Public helpers called by BattleRenderer3D ─────────────────────────────

    public bool TryMoveSelected(Vector2I target)
    {
        if (_gameState?.SelectedUnit is not { Team: Team.Player, HasMoved: false } unit) return false;
        if (!_mapService!.GetReachableTiles(unit).Contains(target)) return false;
        if (_mapService.GetUnitAt(target) != null) return false;
        _mapService.MoveUnit(unit, target);
        _gameState.NotifyUnitMoved(unit);
        return true;
    }

    public bool TryAttackTarget(Unit target)
    {
        if (_gameState?.SelectedUnit is not { Team: Team.Player, HasAttacked: false } atk) return false;
        _battleService!.ExecuteAttack(atk, target);
        return true;
    }
}
