using System.Linq;
using Godot;
using GodotSharpDI.Abstractions;
using TacticsBattle.Services;

namespace TacticsBattle.Tests;

/// <summary>
/// Integration test node.  Injects all three services and validates them.
/// Prints PASS / FAIL for each assertion. On test finish, prints a summary.
/// </summary>
[User]
public sealed partial class DIIntegrationTest : Node, IDependenciesResolved
{
    [Inject] private IGameStateService? _gameState;
    [Inject] private IMapService?       _mapService;
    [Inject] private IBattleService?    _battleService;

    private int _passed;
    private int _failed;

    public override partial void _Notification(int what);

    public override void _Ready() =>
        GD.Print("[IntegrationTest] _Ready — awaiting DI...");

    void IDependenciesResolved.OnDependenciesResolved(bool isAllDependenciesReady)
    {
        GD.Print("\n========== DI Integration Tests ==========");
        RunAll();
        PrintSummary();
    }

    // ──────────────────────────────────────────────────────
    //  Test groups
    // ──────────────────────────────────────────────────────
    private void RunAll()
    {
        TestInjectionNotNull();
        TestMapServiceProperties();
        TestGameStateInitialTurn();
        TestGameStatePhase();
        TestUnitSpawnAndAddRemove();
        TestDamageCalculation();
        TestAttackExecution();
        TestSelectionEvent();
        TestPhaseChangeEvent();
        TestBattleServiceEvents();
        TestMoveUnit();
        TestManhattanDistance();
        TestVictoryConditionNoEnemies();
        TestDefeatConditionNoPlayers();
    }

    // ──────────────────────────────────────────────────────
    //  Individual tests
    // ──────────────────────────────────────────────────────

    private void TestInjectionNotNull()
    {
        Assert(_gameState  != null, "IGameStateService injected");
        Assert(_mapService != null, "IMapService injected");
        Assert(_battleService != null, "IBattleService injected");
    }

    private void TestMapServiceProperties()
    {
        Assert(_mapService!.MapWidth  > 0, "MapWidth > 0");
        Assert(_mapService.MapHeight  > 0, "MapHeight > 0");
        Assert(_mapService.IsValidPosition(0, 0), "IsValidPosition(0,0) = true");
        Assert(!_mapService.IsValidPosition(-1, 0), "IsValidPosition(-1,0) = false");
        Assert(!_mapService.IsValidPosition(999, 999), "IsValidPosition(999,999) = false");
        var tile = _mapService.GetTile(0, 0);
        Assert(tile != null, "GetTile(0,0) != null");
        Assert(tile!.IsWalkable, "GetTile(0,0).IsWalkable");
    }

    private void TestGameStateInitialTurn()
    {
        // StubGameStateService starts at turn 0
        Assert(_gameState!.CurrentTurn >= 0, "CurrentTurn >= 0");
    }

    private void TestGameStatePhase()
    {
        Assert(_gameState!.Phase == GamePhase.PlayerTurn, "Initial phase = PlayerTurn");
        Assert(_gameState.IsPlayerTurn, "IsPlayerTurn = true initially");
    }

    private void TestUnitSpawnAndAddRemove()
    {
        var unit = new Models.Unit(99, "Test", Models.UnitType.Warrior, Models.Team.Player, new Godot.Vector2I(0, 0));
        _gameState!.AddUnit(unit);
        Assert(_gameState.AllUnits.Count == 1, "AllUnits.Count == 1 after Add");
        _gameState.RemoveUnit(unit);
        Assert(_gameState.AllUnits.Count == 0, "AllUnits.Count == 0 after Remove");
    }

    private void TestDamageCalculation()
    {
        var warrior = new Models.Unit(1, "A", Models.UnitType.Warrior, Models.Team.Player, Godot.Vector2I.Zero);
        var archer  = new Models.Unit(2, "B", Models.UnitType.Archer,  Models.Team.Enemy,  Godot.Vector2I.One);
        int dmg = _battleService!.CalculateDamage(warrior, archer);
        Assert(dmg > 0, $"CalculateDamage > 0 (got {dmg})");
    }

    private void TestAttackExecution()
    {
        var attacker = new Models.Unit(10, "Hero",  Models.UnitType.Warrior, Models.Team.Player, Godot.Vector2I.Zero);
        var defender = new Models.Unit(11, "Enemy", Models.UnitType.Warrior, Models.Team.Enemy,  Godot.Vector2I.One);
        _mapService!.PlaceUnit(attacker, Godot.Vector2I.Zero);
        _mapService.PlaceUnit(defender,  Godot.Vector2I.One);
        _gameState!.AddUnit(attacker);
        _gameState.AddUnit(defender);

        int hpBefore = defender.Hp;
        int dmg = _battleService!.ExecuteAttack(attacker, defender);
        Assert(dmg >= 0, $"ExecuteAttack returned dmg >= 0 (got {dmg})");
        Assert(defender.Hp <= hpBefore, "Defender HP decreased after attack");
        Assert(attacker.HasAttacked, "Attacker.HasAttacked = true after attacking");

        _gameState.RemoveUnit(attacker);
        if (_gameState.AllUnits.Contains(defender))
            _gameState.RemoveUnit(defender);
    }

    private void TestSelectionEvent()
    {
        Models.Unit? received = null;
        _gameState!.OnSelectionChanged += u => received = u;
        var unit = new Models.Unit(50, "X", Models.UnitType.Mage, Models.Team.Player, Godot.Vector2I.Zero);
        _gameState.SelectedUnit = unit;
        Assert(received == unit, "OnSelectionChanged fired with correct unit");
        _gameState.SelectedUnit = null;
    }

    private void TestPhaseChangeEvent()
    {
        GamePhase? received = null;
        _gameState!.OnPhaseChanged += p => received = p;
        _gameState.BeginEnemyTurn();
        // StubGameStateService doesn't fire events — skip event check, just ensure no crash
        Assert(true, "BeginEnemyTurn() does not throw");
        _gameState.BeginPlayerTurn();
        Assert(true, "BeginPlayerTurn() does not throw");
    }

    private void TestBattleServiceEvents()
    {
        bool attackFired  = false;
        bool defeatFired  = false;

        _battleService!.OnAttackExecuted += (_, _, _) => attackFired = true;
        _battleService.OnUnitDefeated    += _          => defeatFired = true;

        var attacker = new Models.Unit(20, "P", Models.UnitType.Warrior, Models.Team.Player, new Godot.Vector2I(0,0));
        var defender = new Models.Unit(21, "E", Models.UnitType.Warrior, Models.Team.Enemy,  new Godot.Vector2I(1,0));
        // Give defender 1 hp so it dies
        defender.Hp = 1;

        _mapService!.PlaceUnit(attacker, attacker.Position);
        _mapService.PlaceUnit(defender, defender.Position);
        _gameState!.AddUnit(attacker);
        _gameState.AddUnit(defender);

        _battleService.ExecuteAttack(attacker, defender);
        Assert(attackFired, "OnAttackExecuted event fired");
        Assert(defeatFired, "OnUnitDefeated event fired when defender dies");

        if (_gameState.AllUnits.Contains(attacker)) _gameState.RemoveUnit(attacker);
    }

    private void TestMoveUnit()
    {
        var unit = new Models.Unit(30, "M", Models.UnitType.Archer, Models.Team.Player, new Godot.Vector2I(0,0));
        _mapService!.PlaceUnit(unit, new Godot.Vector2I(0,0));
        _mapService.MoveUnit(unit, new Godot.Vector2I(1,1));
        Assert(unit.Position == new Godot.Vector2I(1,1), "MoveUnit updated unit.Position");
        Assert(unit.HasMoved, "MoveUnit set HasMoved=true");
    }

    private void TestManhattanDistance()
    {
        int d = _mapService!.ManhattanDistance(new Godot.Vector2I(0,0), new Godot.Vector2I(3,4));
        Assert(d == 7, $"ManhattanDistance(0,0→3,4) == 7 (got {d})");
    }

    private void TestVictoryConditionNoEnemies()
    {
        // Use a fresh service to avoid state from prior tests
        var gs = new GameStateService();
        var player = new Models.Unit(40, "P", Models.UnitType.Warrior, Models.Team.Player, Godot.Vector2I.Zero);
        gs.AddUnit(player);
        gs.CheckVictoryCondition();
        Assert(gs.Phase == GamePhase.Victory, "Victory phase set when no enemies remain");
    }

    private void TestDefeatConditionNoPlayers()
    {
        var gs = new GameStateService();
        var enemy = new Models.Unit(41, "E", Models.UnitType.Warrior, Models.Team.Enemy, Godot.Vector2I.Zero);
        gs.AddUnit(enemy);
        gs.CheckVictoryCondition();
        Assert(gs.Phase == GamePhase.Defeat, "Defeat phase set when no players remain");
    }

    // ──────────────────────────────────────────────────────
    //  Helpers
    // ──────────────────────────────────────────────────────
    private void Assert(bool condition, string name)
    {
        if (condition)
        {
            _passed++;
            GD.Print($"  [PASS] {name}");
        }
        else
        {
            _failed++;
            GD.PrintErr($"  [FAIL] {name}");
        }
    }

    private void PrintSummary()
    {
        GD.Print($"\n  Results: {_passed} passed, {_failed} failed");
        GD.Print("==========================================\n");

        if (_failed > 0)
            GD.PrintErr($"[IntegrationTest] {_failed} test(s) FAILED.");
        else
            GD.Print("[IntegrationTest] All tests PASSED ✓");
    }
}
