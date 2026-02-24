using System.Collections.Generic;
using Godot;
using GodotSharpDI.Abstractions;
using TacticsBattle.Models;
using TacticsBattle.Services;

namespace TacticsBattle.Users;

[User]
public sealed partial class UnitManager : Node, IDependenciesResolved
{
    [Inject] private IGameStateService? _gameState;
    [Inject] private IMapService?       _mapService;
    [Inject] private IBattleService?    _battleService;

    private readonly List<Unit> _spawnedUnits = new();
    private int _nextId = 1;

    public override partial void _Notification(int what);

    public override void _Ready() => GD.Print("[UnitManager] _Ready — waiting for DI...");

    void IDependenciesResolved.OnDependenciesResolved(bool ok)
    {
        if (!ok) { GD.PrintErr("[UnitManager] DI FAILED."); return; }
        GD.Print("[UnitManager] DI ready — spawning units.");
        SpawnInitialUnits();
        SubscribeEvents();
    }

    private void SpawnInitialUnits()
    {
        SpawnUnit("Arthur", UnitType.Warrior, Team.Player, new Vector2I(1, 6));
        SpawnUnit("Lyra",   UnitType.Archer,  Team.Player, new Vector2I(3, 7));
        SpawnUnit("Merlin", UnitType.Mage,    Team.Player, new Vector2I(5, 6));
        SpawnUnit("Orc A",  UnitType.Warrior, Team.Enemy,  new Vector2I(2, 1));
        SpawnUnit("Orc B",  UnitType.Warrior, Team.Enemy,  new Vector2I(5, 0));
        SpawnUnit("Goblin", UnitType.Archer,  Team.Enemy,  new Vector2I(4, 2));
        _gameState!.BeginPlayerTurn();
    }

    private Unit SpawnUnit(string name, UnitType type, Team team, Vector2I pos)
    {
        var unit = new Unit(_nextId++, name, type, team, pos);
        _gameState!.AddUnit(unit);
        _mapService!.PlaceUnit(unit, pos);
        _spawnedUnits.Add(unit);
        GD.Print($"  Spawned: {unit}");
        return unit;
    }

    private void SubscribeEvents()
    {
        _battleService!.OnUnitDefeated     += u  => GD.Print($"[UnitManager] Defeated: {u.Name}");
        _battleService.OnEnemyTurnFinished += ()  => GD.Print("[UnitManager] Enemy done.");
    }

    // ── Public helpers called by BattleRenderer3D ──────────────────────────

    public bool TryMoveSelected(Vector2I target)
    {
        if (_gameState?.SelectedUnit is not { } unit) return false;
        if (unit.Team != Team.Player || unit.HasMoved) return false;
        if (!_mapService!.GetReachableTiles(unit).Contains(target)) return false;

        _mapService.MoveUnit(unit, target);
        _gameState.NotifyUnitMoved(unit);   // fires OnUnitMoved → renderer syncs 3D position
        GD.Print($"[UnitManager] Moved {unit.Name} to {target}");
        return true;
    }

    public bool TryAttackTarget(Unit target)
    {
        if (_gameState?.SelectedUnit is not { } attacker) return false;
        if (attacker.Team != Team.Player || attacker.HasAttacked) return false;
        _battleService!.ExecuteAttack(attacker, target);
        return true;
    }
}
