# TacticsBattle

A playable 3D turn-based tactics game built with **Godot 4.5.1 + C#** that demonstrates
an architecture combining **ECS data principles** with **GodotSharpDI dependency injection**.

---

## Architecture

The project is organized into four distinct layers, each with a strict role.

```
┌─────────────────────────────────────────────────────────────────────┐
│  DATA LAYER  (pure C#, static, zero Godot, zero DI)                 │
│                                                                      │
│  UnitTemplateLibrary   — stat blocks by UnitType (single source)    │
│  TileRuleLibrary       — walkable + movement cost by TileType        │
│  LevelRegistry         — all level definitions in one place          │
│  SelectedLevel         — inter-scene index carrier (static int)      │
└──────────────────────────────┬──────────────────────────────────────┘
                               │ read-only
┌──────────────────────────────▼──────────────────────────────────────┐
│  SYSTEMS LAYER  (pure static functions, zero Godot, zero DI)        │
│                                                                      │
│  MovementSystem  — BFS reachable tiles + Dijkstra terrain distances  │
│  CombatSystem    — damage formula + attack application               │
│  AISystem        — enemy decision planning (returns EnemyAction[])   │
└──────────────────────────────┬──────────────────────────────────────┘
                               │ called by
┌──────────────────────────────▼──────────────────────────────────────┐
│  SERVICES LAYER  (stateful, DI-managed, use Systems internally)     │
│                                                                      │
│  IGameStateService / GameStateService  — turns, phases, events      │
│  IMapService       / MapService        — grid, placement, delegates  │
│                                          pathfinding to Systems      │
│  IBattleService    / BattleService     — orchestrates combat + AI    │
│  ILevelRegistryService / LevelRegistryService  — facade over static  │
│                                          LevelRegistry + SelectedLevel│
│  ISceneRouterService   / SceneRouterHost — ONLY place with paths    │
└──────────────────────────────┬──────────────────────────────────────┘
                               │ injected into
┌──────────────────────────────▼──────────────────────────────────────┐
│  GODOT NODE LAYER  (DI Hosts + Users, Godot-specific)               │
│                                                                      │
│  Hosts:  LevelRegistryHost  SceneRouterHost  GameStateHost           │
│          MapHost  BattleHost                                         │
│  Scopes: LevelSelectScope   BattleScope (one for all levels)         │
│  Users:  LevelSelectUI  UnitManager  BattleUI                        │
│          BattleRenderer3D  AIController                              │
└─────────────────────────────────────────────────────────────────────┘
```

### Key design decisions

**Data is static, services are DI**  
`LevelRegistry`, `UnitTemplateLibrary`, `TileRuleLibrary` are static C# classes — no
allocation, no injection. Services inject `ILevelRegistryService`, which is a thin
DI-visible facade over those static classes. This gives you the testability of DI
with the simplicity of plain data.

**Systems are pure functions**  
`MovementSystem.GetReachableTiles()`, `CombatSystem.ApplyAttack()`, `AISystem.PlanTurn()`
take data in, return data out, and have no side effects. Services call them and fire events.
You can unit-test a pathfinding edge case with a single function call.

**One scene for all levels**  
`BattleScene.tscn` is the only battle scene. `SceneRouterService.GoToBattle(index)` writes
`SelectedLevel.Index` then loads it. `LevelRegistryHost` reads that index and provides the
active `LevelDefinition`. The scene tree is identical; only the data differs.

**ISceneRouterService owns all paths**  
`res://Scenes/BattleScene.tscn` and `res://Scenes/LevelSelectScene.tscn` appear in exactly
one file: `SceneRouterHost.cs`. Every UI node calls `_router.GoToBattle(n)` or
`_router.GoToMenu()` — no string literals anywhere else.

---

## Adding a new level

Edit exactly **one file**: `Scripts/Models/LevelRegistry.cs`.

```csharp
// In LevelRegistry.All, append:
new LevelDefinition(
    Index:       3,
    Name:        "Desert Raid",
    Description: "Ambush the convoy!",
    Difficulty:  "★★★  Hard",
    MapWidth:    10, MapHeight: 10,
    Theme:       MapTheme.Forest,   // reuse existing theme or add one to MapService
    Units: new[]
    {
        new UnitSpawnInfo("Hero",  UnitType.Warrior, Team.Player, new Vector2I(5, 9)),
        // ...
    }),
```

That's it. The menu card appears automatically. The battle loads automatically.

---

## Project structure

```
TacticsBattle/
├── project.godot               main scene: LevelSelectScene.tscn
├── TacticsBattle.csproj        NuGet: GodotSharpDI 1.2.0-rc.1
│
├── Scenes/
│   ├── LevelSelectScene.tscn
│   ├── BattleScene.tscn        ← ONE scene for every level
│   └── TestScene.tscn
│
└── Scripts/
    ├── Models/          Pure data (no Godot, no DI)
    │   ├── LevelDefinition.cs   record — what a level is
    │   ├── LevelRegistry.cs     static list of all levels
    │   ├── SelectedLevel.cs     static int for inter-scene routing
    │   ├── UnitTemplate.cs      stat blocks + UnitTemplateLibrary
    │   ├── TileRules.cs         movement rules + TileRuleLibrary
    │   ├── Unit.cs              instance data (reads from library)
    │   └── Tile.cs              instance data (reads from library)
    │
    ├── Systems/         Pure functions (no Godot, no DI)
    │   ├── MovementSystem.cs    BFS + Dijkstra
    │   ├── CombatSystem.cs      damage calculation
    │   └── AISystem.cs          enemy planning
    │
    ├── Services/        Stateful + DI interfaces
    │   ├── IGameStateService / GameStateService
    │   ├── IMapService       / MapService        (delegates to Systems)
    │   ├── IBattleService    / BattleService     (delegates to Systems)
    │   ├── ILevelRegistryService / LevelRegistryService
    │   └── ISceneRouterService   (implemented by SceneRouterHost)
    │
    ├── Hosts/           DI providers
    │   ├── LevelRegistryHost    provides ILevelRegistryService
    │   ├── SceneRouterHost      provides ISceneRouterService (owns scene paths)
    │   ├── GameStateHost
    │   ├── MapHost              WaitFor ILevelRegistryService
    │   └── BattleHost           WaitFor IGameState + IMap
    │
    ├── Scope/
    │   ├── LevelSelectScope     [Modules(LevelRegistryHost, SceneRouterHost)]
    │   └── BattleScope          [Modules(all 5 hosts)]
    │
    ├── Users/           DI consumers + Godot nodes
    │   ├── LevelSelectUI        [Inject ILevelRegistry + ISceneRouter]
    │   ├── UnitManager          [Inject all 4 services]
    │   ├── BattleUI             [Inject all 5 services]
    │   ├── BattleRenderer3D     [Inject 4 services]
    │   └── AIController         [Inject IGameState + IBattle]
    │
    └── Tests/
        └── *.cs                 14 DI integration assertions
```

---

## Controls

| Action | Input |
|---|---|
| Select own unit | Left-click blue unit |
| Move | Left-click cyan tile |
| Attack | Left-click red tile |
| View enemy info | Left-click enemy unit |
| Deselect | Click elsewhere |
| End turn | Button or **Enter** |
| Pause / Settings | **ESC** |

---

## Quick start

```bash
git clone <repo>
cd TacticsBattle
godot project.godot   # F5 to run
```

Requires Godot 4.5.1 (.NET) and .NET SDK 8+.
NuGet restores `GodotSharpDI 1.2.0-rc.1` automatically.
