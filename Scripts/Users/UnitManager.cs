using System.Collections.Generic;
using Godot;
using GodotSharpDI.Abstractions;
using TacticsBattle.Models;
using TacticsBattle.Services;

namespace TacticsBattle.Users;

/// <summary>
/// DI User: injects all three services, spawns the initial unit set,
/// and exposes move/attack helpers for BattleRenderer3D.
/// </summary>
[User]
public sealed partial class UnitManager : Node, IDependenciesResolved
{
    [Inject] private IGameStateService? _gameState;
    [Inject] private IMapService?       _mapService;
    [Inject] private IBattleService?    _battleService;

    private readonly List<Unit> _spawnedUnits = new();
    private int _nextId = 1;

    public override partial void _Notification(int what);

    public override void _Ready()
    {
        GD.Print("[UnitManager] _Ready — waiting for DI...");
    }

    void IDependenciesResolved.OnDependenciesResolved(bool isAllDependenciesReady)
    {
        if (!isAllDependenciesReady)
        {
            GD.PrintErr("[UnitManager] Dependency injection FAILED — cannot spawn units.");
            return;
        }

        GD.Print("[UnitManager] DI ready — spawning units.");
        SpawnInitialUnits();
        SubscribeEvents();
    }

    // ──────────────────────────────────────────────────────
    //  Spawning
    // ──────────────────────────────────────────────────────
    private void SpawnInitialUnits()
    {
        // Player units (bottom rows)
        SpawnUnit("Arthur",  UnitType.Warrior, Team.Player, new Vector2I(1, 6));
        SpawnUnit("Lyra",    UnitType.Archer,  Team.Player, new Vector2I(3, 7));
        SpawnUnit("Merlin",  UnitType.Mage,    Team.Player, new Vector2I(5, 6));

        // Enemy units (top rows)
        SpawnUnit("Orc A",   UnitType.Warrior, Team.Enemy,  new Vector2I(2, 1));
        SpawnUnit("Orc B",   UnitType.Warrior, Team.Enemy,  new Vector2I(5, 0));
        SpawnUnit("Goblin",  UnitType.Archer,  Team.Enemy,  new Vector2I(4, 2));

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
        _battleService!.OnUnitDefeated += unit =>
            GD.Print($"[UnitManager] Unit defeated: {unit.Name}");

        _battleService.OnEnemyTurnFinished += () =>
            GD.Print("[UnitManager] Enemy turn finished — player may now act.");
    }

    // ──────────────────────────────────────────────────────
    //  Public helpers called by BattleRenderer3D
    // ──────────────────────────────────────────────────────

    /// <summary>Move selected unit to target tile. Returns true if successful.</summary>
    public bool TryMoveSelected(Vector2I target)
    {
        if (_gameState?.SelectedUnit is not { } unit) return false;
        if (unit.Team != Team.Player || unit.HasMoved) return false;
        var reachable = _mapService!.GetReachableTiles(unit);
        if (!reachable.Contains(target)) return false;
        _mapService.MoveUnit(unit, target);
        _gameState.NotifyUnitMoved(unit);
        GD.Print($"[UnitManager] Moved {unit.Name} to {target}");
        return true;
    }

    /// <summary>Attack a specific enemy unit using the selected player unit.</summary>
    public bool TryAttackTarget(Unit target)
    {
        if (_gameState?.SelectedUnit is not { } attacker) return false;
        if (attacker.Team != Team.Player || attacker.HasAttacked) return false;
        _battleService!.ExecuteAttack(attacker, target);
        return true;
    }
}
