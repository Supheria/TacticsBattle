# TacticsBattle — GodotSharpDI Demo Project

A complete turn-based tactics (战旗) game demo built on **Godot 4.5.1 + C# + GodotSharpDI 1.2.0**.
Demonstrates the full DI attribute set: `[Host]`, `[User]`, `[Modules]`, `[Provide]`, `[Inject]`, `[WaitFor]`, and `IDependenciesResolved`.

---

## Directory Layout

```
<root>/
├── GodotSharpDI-1.2.0/          ← extracted from GodotSharpDI-1_2_0.zip
│   ├── GodotSharpDI.Abstractions/
│   └── GodotSharpDI.SourceGenerator/
└── TacticsBattle/               ← this project
    ├── project.godot
    ├── TacticsBattle.csproj
    ├── Scenes/
    │   ├── BattleScene.tscn     ← main game scene
    │   └── TestScene.tscn       ← integration-test scene
    └── Scripts/
        ├── Models/              Unit, Tile
        ├── Services/            IGameStateService, IMapService, IBattleService + impls
        ├── Hosts/               GameStateHost, MapHost, BattleHost
        ├── Users/               UnitManager, BattleUI, AIController
        ├── Scope/               BattleScope
        └── Tests/               TestBattleScope, TestHosts, DIIntegrationTest
```

---

## Setup

1. **Extract** `GodotSharpDI-1_2_0.zip` so `GodotSharpDI-1.2.0/` is a **sibling** of `TacticsBattle/`.
2. Open **Godot 4.5.1** and import `TacticsBattle/project.godot`.
3. The editor will build the C# solution automatically (or press **Build** in the top toolbar).
4. Press **F5** / Play to run the game (`BattleScene.tscn`).

---

## Running Integration Tests

1. In **Project → Project Settings → Application → Run → Main Scene**, set it to `res://Scenes/TestScene.tscn`.
2. Press **F5** — the Output panel will print PASS/FAIL for each assertion.
3. Reset main scene back to `BattleScene.tscn` for normal play.

Alternatively, run headless from command line:
```bash
godot --headless --path /path/to/TacticsBattle --scene res://Scenes/TestScene.tscn
```

---

## DI Architecture

```
BattleScope  [Modules(Hosts = [GameStateHost, MapHost, BattleHost])]
│
├── GameStateHost  [Host]
│     └── [Provide(ExposedTypes=[IGameStateService])]  GameStateSvc
│
├── MapHost  [Host]
│     └── [Provide(ExposedTypes=[IMapService])]  MapSvc
│
├── BattleHost  [Host]
│     ├── [Inject]  IGameStateService  _gameStateService
│     ├── [Inject]  IMapService        _mapService
│     └── [Provide(ExposedTypes=[IBattleService], WaitFor=[_gameStateService,_mapService])]  BattleSvc
│
├── UnitManager  [User]
│     ├── [Inject]  IGameStateService
│     ├── [Inject]  IMapService
│     └── [Inject]  IBattleService
│         → IDependenciesResolved.OnDependenciesResolved() spawns units
│
├── BattleUI  [User]  (CanvasLayer)
│     ├── [Inject]  IGameStateService
│     └── [Inject]  IBattleService
│         → builds HUD and subscribes to events
│
└── AIController  [User]
      ├── [Inject]  IGameStateService
      └── [Inject]  IBattleService
          → listens for EnemyTurn phase, calls IBattleService.RunEnemyTurn()
```

### Key DI features demonstrated

| Feature | Where |
|---|---|
| `[Host]` — service provider node | `GameStateHost`, `MapHost`, `BattleHost` |
| `[User]` — service consumer node | `UnitManager`, `BattleUI`, `AIController` |
| `[Modules(Hosts=[...])]` — scope wiring | `BattleScope` |
| `[Provide(ExposedTypes=[...])]` — expose as interface | All hosts |
| `[Inject]` — field/property injection | `BattleHost`, all users |
| `WaitFor` — dependency ordering | `BattleHost` waits for `_gameStateService` + `_mapService` |
| `IDependenciesResolved` | `BattleHost`, `UnitManager`, `BattleUI`, `AIController` |

---

## Game Flow

```
Game Start
  └─ BattleScope initialises DI graph
       └─ GameStateHost provides IGameStateService
       └─ MapHost provides IMapService
       └─ BattleHost waits for both, then provides IBattleService
            └─ UnitManager.OnDependenciesResolved() → spawns 6 units (3 player, 3 enemy)
            └─ BattleUI.OnDependenciesResolved()    → builds HUD, subscribes events
            └─ AIController.OnDependenciesResolved() → subscribes to phase changes

Player Turn
  → Click unit to select
  → Click tile to move (UnitManager.TryMoveSelected)
  → Click enemy to attack (UnitManager.TryAttackTarget)
  → Click "End Turn" button (BattleUI → IGameStateService.EndTurn)

Enemy Turn
  → AIController detects EnemyTurn phase
  → Calls IBattleService.RunEnemyTurn()
  → Each enemy: move toward nearest player, attack if in range
  → BattleService fires OnEnemyTurnFinished → EndTurn → Player Turn

Victory/Defeat
  → IGameStateService.CheckVictoryCondition()
  → BattleUI shows overlay
```

---

## Integration Tests (14 assertions)

| Test | What it verifies |
|---|---|
| `TestInjectionNotNull` | All 3 services successfully injected |
| `TestMapServiceProperties` | Grid bounds, valid/invalid positions, tile walkability |
| `TestGameStateInitialTurn` | Turn counter starts at 0 |
| `TestGameStatePhase` | Initial phase is PlayerTurn |
| `TestUnitSpawnAndAddRemove` | AddUnit / RemoveUnit round-trip |
| `TestDamageCalculation` | CalculateDamage > 0 |
| `TestAttackExecution` | HP decreases, HasAttacked set |
| `TestSelectionEvent` | OnSelectionChanged fires |
| `TestPhaseChangeEvent` | BeginEnemyTurn / BeginPlayerTurn don't throw |
| `TestBattleServiceEvents` | OnAttackExecuted + OnUnitDefeated fire correctly |
| `TestMoveUnit` | Position updated, HasMoved = true |
| `TestManhattanDistance` | Distance(0,0 → 3,4) = 7 |
| `TestVictoryConditionNoEnemies` | Phase → Victory when no enemies |
| `TestDefeatConditionNoPlayers` | Phase → Defeat when no players |
