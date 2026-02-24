# TacticsBattle

A playable **3D turn-based tactics (战旗)** game built with **Godot 4.5.1 + C#** that showcases the [GodotSharpDI](https://github.com/GodotSharpDI) dependency-injection framework.

All geometry is procedurally generated from simple primitives — no external assets required.

---

## Screenshots / Layout

```
┌────────────────────────────────────────────────┐
│ Turn  1                     [isometric 3D view] │
│ Phase: PlayerTurn                               │
│ ─────────────────                               │
│ End Turn  [Enter]           ┌──── 8×8 grid ───┐│
│ ─────────────────           │ 🟦🟦 Orc B 🟦🟦 ││
│ Battle Log                  │  🟦 Orc A  🟦   ││
│  • Phase → PlayerTurn       │    🟨 Goblin     ││
│  • Turn 1 ───               │ ░░░░░░░░░░░░░░  ││
│  Arthur → Orc A  -18 HP     │   Arthur  Merlin ││
│                             │    🟢  Lyra 🟢  ││
│                             └─────────────────┘│
│ Arthur [Player·Warrior]                         │
│ HP 120/120   ATK 30  DEF 20   MOVE ●  ATK ●   │
└────────────────────────────────────────────────┘
```

---

## Quick Start

### Prerequisites

| Tool | Version |
|---|---|
| [Godot Engine (Mono/.NET)](https://godotengine.org/download) | 4.5.1 |
| .NET SDK | 8.0+ |

### Run

```bash
git clone <this-repo>
cd TacticsBattle
godot project.godot          # opens in editor, then press F5
# — or headless —
godot --headless --path . --scene res://Scenes/BattleScene.tscn
```

Godot's NuGet restore will pull `GodotSharpDI 1.2.0-rc.1` automatically on first build.

---

## How to Play

| Action | Input |
|---|---|
| **Select a unit** | Left-click a blue unit |
| **Move** | Left-click a **cyan** tile (within move range) |
| **Attack** | Left-click a **red** tile (enemy in attack range) |
| **Deselect** | Left-click the selected unit again, or an empty non-highlighted tile |
| **End turn** | Click *End Turn* button, or press **Enter** |

Colour coding on the board:

| Colour | Meaning |
|---|---|
| 🟡 Yellow | Currently selected unit |
| 🔵 Cyan | Reachable move tiles |
| 🔴 Red | Attackable enemy tiles |

After the player ends their turn, the **enemy AI** automatically moves and attacks, then the next player turn begins.

---

## Game Rules

- **6 units** total: 3 player (blue), 3 enemy (red).
- Each unit may **move once** and **attack once** per turn (in either order).
- The game ends when all units of one side are defeated.
- **Unit types:**

| Type | HP | ATK | DEF | Move | Range |
|---|---|---|---|---|---|
| Warrior | 120 | 30 | 20 | 3 | 1 |
| Archer | 80 | 40 | 10 | 2 | 3 |
| Mage | 60 | 60 | 5 | 2 | 2 |

- **Terrain** modifies movement cost (Forest +1, Mountain +2) and Water is impassable.

---

## Project Structure

```
TacticsBattle/
├── project.godot
├── TacticsBattle.csproj          ← NuGet: GodotSharpDI 1.2.0-rc.1
├── TacticsBattle.sln
│
├── Scenes/
│   ├── BattleScene.tscn          ← main playable scene
│   └── TestScene.tscn            ← integration-test scene (headless)
│
└── Scripts/
    ├── Models/
    │   ├── Unit.cs               ← Unit data (HP, ATK, DEF, position…)
    │   └── Tile.cs               ← Tile data (type, walkable, cost)
    │
    ├── Services/
    │   ├── IGameStateService.cs  ← phase/turn/selection management
    │   ├── IMapService.cs        ← grid, pathfinding, unit placement
    │   ├── IBattleService.cs     ← damage calculation, AI turn
    │   ├── GameStateService.cs
    │   ├── MapService.cs
    │   └── BattleService.cs
    │
    ├── Hosts/                    ← [Host] nodes — service providers
    │   ├── GameStateHost.cs      ← exposes IGameStateService
    │   ├── MapHost.cs            ← exposes IMapService
    │   └── BattleHost.cs        ← waits for both, exposes IBattleService
    │
    ├── Scope/
    │   └── BattleScope.cs        ← [Modules] root; lists all Hosts
    │
    ├── Users/                    ← [User] nodes — service consumers
    │   ├── UnitManager.cs        ← spawns units; exposes TryMove/TryAttack
    │   ├── BattleRenderer3D.cs   ← 3D world, camera, input handling
    │   ├── BattleUI.cs           ← 2D HUD overlay (CanvasLayer)
    │   └── AIController.cs       ← triggers enemy AI on phase change
    │
    └── Tests/
        ├── TestBattleScope.cs    ← test DI scope (4×4 map)
        ├── TestGameStateHost.cs
        ├── TestMapHost.cs
        ├── TestBattleHost.cs
        └── DIIntegrationTest.cs  ← 14 integration assertions
```

---

## Dependency-Injection Architecture

```
BattleScope  [Modules(Hosts = [GameStateHost, MapHost, BattleHost])]
│
├── GameStateHost  [Host]
│     └── [Provide(IGameStateService)]  ← lazy singleton
│
├── MapHost  [Host]
│     └── [Provide(IMapService)]        ← lazy singleton, 8×8 grid
│
├── BattleHost  [Host]
│     ├── [Inject]  IGameStateService
│     ├── [Inject]  IMapService
│     └── [Provide(IBattleService, WaitFor=[_gameStateService, _mapService])]
│
├── UnitManager  [User]
│     ├── [Inject] IGameStateService + IMapService + IBattleService
│     └── IDependenciesResolved → SpawnInitialUnits() + BeginPlayerTurn()
│
├── BattleRenderer3D  [User]  (Node3D)
│     ├── [Inject] IGameStateService + IMapService + IBattleService
│     └── IDependenciesResolved → BuildWorld() (camera, tiles, unit meshes)
│         _UnhandledInput()     → raycasts mouse clicks → select/move/attack
│
├── BattleUI  [User]  (CanvasLayer)
│     ├── [Inject] IGameStateService + IBattleService
│     └── IDependenciesResolved → subscribes to events → updates HUD
│
└── AIController  [User]
      ├── [Inject] IGameStateService + IBattleService
      └── IDependenciesResolved → OnPhaseChanged → CallDeferred(RunAI)
```

### Key GodotSharpDI features demonstrated

| Feature | Where |
|---|---|
| `[Host]` service provider nodes | `GameStateHost`, `MapHost`, `BattleHost` |
| `[User]` service consumer nodes | `UnitManager`, `BattleRenderer3D`, `BattleUI`, `AIController` |
| `[Modules(Hosts=[…])]` scope wiring | `BattleScope` |
| `[Provide(ExposedTypes=[…])]` interface exposure | All Hosts |
| `[Inject]` field injection | `BattleHost`, all Users |
| `WaitFor` — dependency ordering | `BattleHost` waits for both IGameStateService + IMapService |
| `IDependenciesResolved` callback | `BattleHost`, `UnitManager`, `BattleRenderer3D`, `BattleUI`, `AIController` |

---

## Running Integration Tests

```bash
# Change main scene to TestScene, then run headless:
godot --headless --path . --scene res://Scenes/TestScene.tscn
```

Expected output (14 assertions):
```
========== DI Integration Tests ==========
  [PASS] IGameStateService injected
  [PASS] IMapService injected
  [PASS] IBattleService injected
  [PASS] MapWidth > 0
  ...
  Results: 14 passed, 0 failed
==========================================
[IntegrationTest] All tests PASSED ✓
```

---

## Dependencies

| Package | Version | Purpose |
|---|---|---|
| [GodotSharpDI](https://www.nuget.org/packages/GodotSharpDI) | `1.2.0-rc.1` | DI source generator + runtime |
| Godot.NET.Sdk | `4.5.1` | Godot C# bindings |

No additional NuGet packages or external assets are required.

---

## License

MIT — see [LICENSE.md](LICENSE.md).
