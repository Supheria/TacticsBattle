using System.Linq;
using Godot;
using GodotSharpDI.Abstractions;
using TacticsBattle.Models;
using TacticsBattle.Services;

namespace TacticsBattle.Tests;

/// <summary>
/// Integration test node.  Injects all services and validates them.
/// Uses IUnitDataProvider to construct Units (matching new ctor signature).
/// Prints PASS / FAIL for each assertion; summary at the end.
/// </summary>
[User]
public sealed partial class DIIntegrationTest : Node, IDependenciesResolved
{
    [Inject] private IGameStateService? _gameState;
    [Inject] private IMapService?       _map;
    [Inject] private IBattleService?    _battle;
    [Inject] private IUnitDataProvider? _unitData;

    private int _pass, _fail;

    public override partial void _Notification(int what);

    void IDependenciesResolved.OnDependenciesResolved(bool ok)
    {
        if (!ok) { GD.PrintErr("[DITest] DI FAILED"); return; }
        GD.Print("=== DI Integration Tests ===");

        RunAll();

        GD.Print($"=== Results: {_pass} PASS  {_fail} FAIL ===");
        if (_fail == 0) GD.Print("✓ All tests passed!");
        else            GD.PrintErr($"✗ {_fail} test(s) failed!");
    }

    // Helper: resolve a fresh template for the given type
    private Unit MakeUnit(int id, string name, UnitType type, Team team, Vector2I pos)
    {
        var tmpl     = _unitData!.GetTemplate(type);
        var defaults = _unitData.GetDefaultComponents(type);
        return new Unit(id, name, type, team, pos, tmpl, defaults);
    }

    private void RunAll()
    {
        TestDiServicesNotNull();
        TestUnitSpawnAndAddRemove();
        TestDamageCalculation();
        TestMovementReachable();
        TestAttackRange();
        TestVictoryCondition();
        TestComponentBag();
    }

    private void TestDiServicesNotNull()
    {
        Assert(_gameState != null, "IGameStateService injected");
        Assert(_map        != null, "IMapService injected");
        Assert(_battle     != null, "IBattleService injected");
        Assert(_unitData   != null, "IUnitDataProvider injected");
    }

    private void TestUnitSpawnAndAddRemove()
    {
        var unit = MakeUnit(99, "Test", UnitType.Warrior, Team.Player, new Vector2I(0, 0));
        _gameState!.AddUnit(unit);
        Assert(_gameState.AllUnits.Count == 1, "AllUnits.Count == 1 after Add");
        _gameState.RemoveUnit(unit);
        Assert(_gameState.AllUnits.Count == 0, "AllUnits.Count == 0 after Remove");
    }

    private void TestDamageCalculation()
    {
        var warrior = MakeUnit(1, "A", UnitType.Warrior, Team.Player, Vector2I.Zero);
        var archer  = MakeUnit(2, "B", UnitType.Archer,  Team.Enemy,  Vector2I.One);
        int dmg = _battle!.CalculateDamage(warrior, archer);
        Assert(dmg > 0, $"Damage > 0 (got {dmg})");
        // Warrior ATK=30, Archer DEF=10 → base ≈ 20 (±10%)
        Assert(dmg >= 1 && dmg <= 60, $"Damage in plausible range (got {dmg})");
    }

    private void TestMovementReachable()
    {
        var unit  = MakeUnit(3, "Mover", UnitType.Warrior, Team.Player, new Vector2I(1, 1));
        _map!.PlaceUnit(unit, new Vector2I(1, 1));
        var reach = _map.GetReachableTiles(unit);
        Assert(reach.Count > 0, $"Warrior at (1,1) has reachable tiles (got {reach.Count})");
        _map.MoveUnit(unit, new Vector2I(-1,-1)); // cleanup
    }

    private void TestAttackRange()
    {
        var a = MakeUnit(4, "Att", UnitType.Warrior, Team.Player, new Vector2I(0, 0));
        var d = MakeUnit(5, "Def", UnitType.Archer,  Team.Enemy,  new Vector2I(1, 0));
        _map!.PlaceUnit(a, a.Position);
        _map.PlaceUnit(d, d.Position);
        var targets = _map.GetAttackableTargets(a);
        Assert(targets.Count == 1, $"Warrior sees 1 adjacent enemy (got {targets.Count})");
        // Cleanup
        _map.MoveUnit(a, new Vector2I(-1,-1));
        _map.MoveUnit(d, new Vector2I(-1,-1));
    }

    private void TestVictoryCondition()
    {
        bool victoryFired = false;
        _gameState!.OnPhaseChanged += p => { if (p == GamePhase.Victory) victoryFired = true; };

        var player = MakeUnit(10, "P", UnitType.Warrior, Team.Player, Vector2I.Zero);
        var enemy  = MakeUnit(11, "E", UnitType.Mage,    Team.Enemy,  Vector2I.One);
        _gameState.AddUnit(player);
        _gameState.AddUnit(enemy);
        _gameState.RemoveUnit(enemy);
        _gameState.CheckVictoryCondition();
        Assert(victoryFired, "Victory fires when all enemies removed");
        // Cleanup
        _gameState.RemoveUnit(player);
    }

    private void TestComponentBag()
    {
        var mage = MakeUnit(20, "M", UnitType.Mage, Team.Player, Vector2I.Zero);
        Assert(mage.Components.Count > 0, "Mage has default component(s)");
        var poison = mage.GetComponent<TacticsBattle.Models.Components.PoisonOnHitComponent>();
        Assert(poison != null, "Mage has PoisonOnHitComponent by default");
        Assert(poison!.DamagePerTurn == 8, $"Poison damage == 8 (got {poison.DamagePerTurn})");
    }

    private void Assert(bool condition, string message)
    {
        if (condition) { GD.Print($"  PASS: {message}"); _pass++; }
        else           { GD.PrintErr($"  FAIL: {message}"); _fail++; }
    }
}
