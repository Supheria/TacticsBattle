# TacticsBattle

A playable **3D turn-based tactics (战旗)** game built with **Godot 4.5.1 + C#** that demonstrates the [GodotSharpDI 1.2.0](https://github.com/GodotSharpDI) dependency-injection framework — including level selection, level-scoped service configuration, and the full play loop.

All geometry is procedurally generated; no external assets are required.

---

## Quick Start

### Prerequisites

| Tool | Version |
|---|---|
| [Godot Engine (Mono/.NET)](https://godotengine.org/download) | 4.5.1 |
| .NET SDK | 8.0+ |

### Run

```bash
git clone <repo-url>
cd TacticsBattle
godot project.godot      # opens editor → press F5
```

NuGet automatically restores `GodotSharpDI 1.2.0-rc.1` on first build.

---

## How to Play

| Action | Input |
|---|---|
| **Select own unit** | Left-click a **blue** unit |
| **Move** | Left-click a **cyan** tile |
| **Attack** | Left-click a **red** tile (enemy in range) |
| **View enemy info** | Left-click any enemy unit → info panel appears |
| **Deselect** | Click same unit again, or an empty non-highlighted tile |
| **End turn** | *End Turn* button  or  **Enter** |

### Highlight colours

| Colour | Meaning |
|---|---|
| 🟡 Yellow | Your selected unit |
| 🔵 Cyan | Reachable move tiles |
| 🔴 Red | Attackable enemies |
| 🟠 Orange | Enemy's potential move range (enemy info view) |

### Rules

- **No-overlap**: no two units (friend or foe) may occupy the same tile.
- Units may move *through* allied tiles but cannot *stop* on an occupied tile.
- Each unit may **move once** and **attack once** per turn (in either order).
- Game ends when all units of one side are defeated.

### Unit types

| Type | HP | ATK | DEF | Move | Range |
|---|---|---|---|---|---|
| Warrior | 120 | 30 | 20 | 3 | 1 |
| Archer | 80 | 40 | 10 | 2 | 3 |
| Mage | 60 | 60 | 5 | 2 | 2 |

---

## Project Structure

```
TacticsBattle/
├── project.godot               ← main scene: LevelSelectScene
├── TacticsBattle.csproj        ← NuGet: GodotSharpDI 1.2.0-rc.1
│
├── Scenes/
│   ├── LevelSelectScene.tscn   ← level-select menu
│   ├── Level1Scene.tscn        ← Forest Skirmish (Easy, 8×8, 3v3)
│   ├── Level2Scene.tscn        ← River Crossing  (Medium, 10×8, 4v5)
│   └── Level3Scene.tscn        ← Mountain Pass   (Hard, 8×12, 3v7)
│
└── Scripts/
    ├── Models/
    │   ├── Unit.cs             ← HP, ATK, DEF, position, action flags
    │   ├── Tile.cs             ← type, walkable, movement cost
    │   └── LevelConfig.cs      ← immutable level descriptor (size, theme, spawns)
    │
    ├── Services/
    │   ├── IGameStateService / GameStateService   ← turns, phases, selection events
    │   ├── IMapService       / MapService         ← grid, pathfinding, no-overlap BFS
    │   ├── IBattleService    / BattleService      ← damage, AI turn
    │   ├── ILevelConfigService / LevelConfigService  ← wraps LevelConfig for DI
    │   └── ILevelMenuService / LevelMenuService   ← level list for the menu UI
    │
    ├── Hosts/
    │   ├── LevelMenuHost.cs       ← provides ILevelMenuService (menu scene only)
    │   ├── Level1ConfigHost.cs    ← provides ILevelConfigService for level 1
    │   ├── Level2ConfigHost.cs    ← provides ILevelConfigService for level 2
    │   ├── Level3ConfigHost.cs    ← provides ILevelConfigService for level 3
    │   ├── GameStateHost.cs       ← provides IGameStateService
    │   ├── MapHost.cs             ← waits for ILevelConfigService → provides IMapService
    │   └── BattleHost.cs          ← waits for state+map → provides IBattleService
    │
    ├── Scope/
    │   ├── LevelSelectScope.cs    ← [Modules(LevelMenuHost)]
    │   ├── Level1Scope.cs         ← [Modules(Level1ConfigHost, GameStateHost, MapHost, BattleHost)]
    │   ├── Level2Scope.cs         ← [Modules(Level2ConfigHost, …)]
    │   └── Level3Scope.cs         ← [Modules(Level3ConfigHost, …)]
    │
    ├── Users/
    │   ├── LevelSelectUI.cs       ← menu cards, navigates to level scenes
    │   ├── UnitManager.cs         ← reads LevelConfig to spawn units
    │   ├── BattleRenderer3D.cs    ← 3D world, camera, tile grid, unit meshes, input
    │   ├── BattleUI.cs            ← 2D HUD, battle log (scrollable), unit info panel
    │   └── AIController.cs        ← listens for EnemyTurn → calls RunEnemyTurn()
    │
    └── Tests/
        ├── TestBattleScope.cs  ← 4×4 test scope
        ├── Test*Host.cs        ← test hosts
        └── DIIntegrationTest.cs ← 14 assertions (run TestScene.tscn)
```

---

## GodotSharpDI Architecture

The key insight is that **swapping a single `[Host]` in a Scope is the only change needed to load a different level**. All services (`MapService`, `BattleService`, `UnitManager`) read `ILevelConfigService` and automatically configure themselves.

```
LevelSelectScene
  └─ LevelSelectScope   [Modules(LevelMenuHost)]
       └─ LevelMenuHost         [Provide → ILevelMenuService]
  └─ LevelSelectUI      [User] ─ [Inject ILevelMenuService]
                                  clicks → ChangeSceneToFile(Level1Scene)

Level1Scene
  └─ Level1Scope   [Modules(Level1ConfigHost, GameStateHost, MapHost, BattleHost)]
       │
       ├─ Level1ConfigHost   [Provide → ILevelConfigService]   (8×8 Forest, 3v3)
       │    ↑ swap to Level2ConfigHost for a 10×8 River map 4v5, no other change needed
       │
       ├─ GameStateHost      [Provide → IGameStateService]
       │
       ├─ MapHost            [Inject ILevelConfigService]       ← WaitFor
       │                     [Provide → IMapService]
       │
       └─ BattleHost         [Inject IGameStateService, IMapService]  ← WaitFor
                             [Provide → IBattleService]

  ├─ UnitManager      [User]  [Inject all 4 services] → spawns units from LevelConfig
  ├─ BattleRenderer3D [User]  [Inject all 4 services] → 3D world + mouse input
  ├─ BattleUI         [User]  [Inject IGameState + IBattle] → HUD + unit info panel
  └─ AIController     [User]  [Inject IGameState + IBattle] → enemy AI
```

### Features demonstrated

| GodotSharpDI feature | Location |
|---|---|
| `[Host]` service provider | all `*Host.cs` |
| `[User]` service consumer | all `Users/*.cs` |
| `[Modules(Hosts=[…])]` scope | all `*Scope.cs` |
| `[Provide(ExposedTypes=[…])]` | all Hosts |
| `[Inject]` field injection | BattleHost, all Users |
| `WaitFor` ordering | MapHost waits for LevelConfigService; BattleHost waits for both |
| `IDependenciesResolved` | BattleHost, UnitManager, BattleRenderer3D, BattleUI, AIController |
| Scene-scoped DI isolation | each Level*Scene has its own scope; LevelSelectScene is entirely separate |

---

## Integration Tests

```bash
# Change main scene to TestScene.tscn in project.godot, then:
godot --headless --path . --scene res://Scenes/TestScene.tscn
```

Expected: `14 passed, 0 failed`

---

## License

MIT — see [LICENSE.md](LICENSE.md).
