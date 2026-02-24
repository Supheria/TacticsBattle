using Godot;
using GodotSharpDI.Abstractions;
using TacticsBattle.Models;
using TacticsBattle.Services;

namespace TacticsBattle.Users;

[User]
public sealed partial class UnitManager : Node, IDependenciesResolved
{
    [Inject] private IGameStateService?     _gs;
    [Inject] private IMapService?           _map;
    [Inject] private IBattleService?        _battle;
    [Inject] private ILevelRegistryService? _registry;
    [Inject] private IUnitFactory?          _factory;

    private int _nextId = 1;

    public override partial void _Notification(int what);
    public override void _Ready() => GD.Print("[UnitManager] Waiting for DI...");

    void IDependenciesResolved.OnDependenciesResolved(bool ok)
    {
        if (!ok) { GD.PrintErr("[UnitManager] DI FAILED."); return; }
        SpawnUnits();
        _battle!.OnUnitDefeated    += u  => GD.Print($"[UnitManager] ☠ {u.Name}");
        _battle.OnEnemyTurnFinished += () => GD.Print("[UnitManager] Enemy turn done.");
        _gs!.BeginPlayerTurn();
    }

    private void SpawnUnits()
    {
        var level = _registry!.ActiveLevel;
        GD.Print($"[UnitManager] Spawning \"{level.Name}\"");
        foreach (var s in level.Units)
        {
            // IUnitFactory resolves template + default components from IUnitDataProvider,
            // then stacks ExtraComponents from the level definition on top.
            var unit = _factory!.Create(_nextId++, s.Name, s.Type, s.Team, s.Position,
                                        s.ExtraComponents);
            _gs!.AddUnit(unit);
            _map!.PlaceUnit(unit, s.Position);
            GD.Print($"  + {unit}");
        }
    }

    public bool TryMoveSelected(Vector2I target)
    {
        if (_gs?.SelectedUnit is not { Team: Team.Player, HasMoved: false } u) return false;
        if (!_map!.GetReachableTiles(u).Contains(target)) return false;
        if (_map.GetUnitAt(target) != null) return false;
        _map.MoveUnit(u, target);
        _gs.NotifyUnitMoved(u);
        return true;
    }

    public bool TryAttackTarget(Unit target)
    {
        if (_gs?.SelectedUnit is not { Team: Team.Player, HasAttacked: false } a) return false;
        _battle!.ExecuteAttack(a, target);
        return true;
    }
}
