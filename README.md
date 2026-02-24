# TacticsBattle

A playable 3D turn-based tactics game built with **Godot 4.5.1 + C#** demonstrating:

- **GodotSharpDI 1.2.0-rc.1** — full DI best-practice showcase
- **ECS-inspired data/logic separation** — pure Systems, pure data Models, stateful Services
- **Strategy pattern** — swappable unit stats, tile rules, and AI without touching game logic
- **Component system** — attach behaviours (poison, slow, counter-attack, aura…) per unit per level
- **Procedural audio** — synthesised BGM and SFX with no binary asset files

---

## Quick Start

```bash
unzip TacticsBattle.zip
cd proj
godot project.godot   # opens editor → F5 to run
```

Requirements: Godot 4.5.1 (.NET) · .NET SDK 8+  
NuGet restores `GodotSharpDI 1.2.0-rc.1` automatically on first build.

---

## Game Rules

### Turn structure

Each round consists of a **Player Turn** followed by an **Enemy Turn**.

1. **Player Turn** — control all your units in any order
   - Select a unit (left-click)
   - Optionally **Move** — click a blue highlighted tile within movement range
   - Optionally **Attack** — click a red highlighted tile within attack range
   - Each unit may move and attack once per turn (in either order)
   - Press **End Turn** (or **Enter**) when ready
2. **Enemy Turn** — AI moves and attacks automatically
3. **Repeat** until one side has no living units

### Victory & defeat

- **Victory**: all enemy units are eliminated
- **Defeat**: all your units are eliminated

### Status effects (tick at the start of each team's turn)

| Icon | Effect | Source |
|------|--------|--------|
| ☠ | **Poisoned** — deals damage each turn; stacks by taking the longest duration | Mage default |
| ❄ | **Slowed** — reduces effective movement range | Archer default |

Statuses expire automatically when `TurnsRemaining` reaches 0.

---

## Characters & Attributes

### Archetypes

| Type | HP | ATK | DEF | MOVE | RNG | Default component |
|------|----|-----|-----|------|-----|-------------------|
| Warrior | 120 | 30 | 20 | 3 | 1 | `ArmorComponent(-5 dmg/hit)` |
| Archer  |  80 | 40 | 10 | 2 | 3 | `SlowOnHit(−1 move, 1t)` |
| Mage    |  60 | 60 |  5 | 2 | 2 | `PoisonOnHit(8dmg, 2t)` |

### Attribute reference

| Attribute | Description |
|-----------|-------------|
| **HP / MaxHp** | Current and maximum hit points. Unit dies at 0. |
| **ATK** | Base attack damage. Formula: `max(1, ATK − DEF − Armor)` × [0.9–1.1] |
| **DEF** | Reduces incoming damage. Combined with `ArmorComponent` for total reduction. |
| **MOVE** | Base movement range in tiles (movement point budget per turn). |
| **Effective MOVE** | `MOVE + MovementBonus − SlowReduction` (shown in parentheses when different) |
| **RNG** | Attack range in Manhattan-distance tiles. |

### Component catalogue

| Component | Type | Effect |
|-----------|------|--------|
| `ArmorComponent(n)` | Passive | Reduces every hit by `n` damage (before variance) |
| `MovementBonusComponent(n)` | Passive | Adds `n` to effective movement range |
| `PoisonOnHitComponent(dmg, dur)` | On-Attack | Applies/refreshes Poisoned on target |
| `SlowOnHitComponent(red, dur)` | On-Attack | Applies/refreshes Slowed on target |
| `PushBackOnHitComponent(dist)` | On-Attack | Knocks target `dist` tiles away |
| `CounterAttackComponent(ratio)` | On-Hit | Reflects `ratio×damage` to attacker |
| `ThornComponent(flat)` | On-Hit | Reflects `flat` damage to attacker per hit |
| `HealAuraComponent(hp, r)` | Aura | Heals all allies within `r` tiles by `hp` each turn |
| `PoisonedComponent` | Status | Deals damage each turn, decrements `TurnsRemaining` |
| `SlowedComponent` | Status | Reduces `EffectiveMoveRange`, decrements `TurnsRemaining` |

---

## Levels

### Level 0 — Forest Skirmish ★☆☆ (8×8, 3v3)

Introduction to archetypes and default components. Balanced open terrain with
a central forest cluster and corner obstacles.

Units: Arthur (Warrior), Lyra (Archer), Merlin (Mage) vs Orc A, Orc B, Goblin.

### Level 1 — River Crossing ★★☆ (10×8, 4v5)

A horizontal river blocks the centre (y=3–4). Two two-tile-wide fords at
x=2–3 and x=7–8 create tactical chokepoints.

**Special units:**
- **Orc C** — extra `CounterAttackComponent(35%)` — risky target for weak units
- **Shaman** — extra `HealAuraComponent(+12HP/turn, r=2)` — high-priority target

### Level 2 — Mountain Pass ★★★ (8×12, 3v7)

Mountain walls at x=0–1 and x=6–7 channel everyone through a 4-tile central
corridor (x=2–5). Player defends the south end against 7 enemies.

**Special units:**
- **Scout A/B** — extra `PushBackOnHitComponent(1)` — knocks players back up the pass
- **Orc C** — extra `MovementBonusComponent(+1)` — breaks through faster
- **Warlord** — stacked `ArmorComponent(+10)` + `CounterAttack(50%)` + `ThornComponent(8)` — devastating to attack carelessly

---

## Controls

| Action | Input |
|--------|-------|
| Select own unit | Left-click blue unit |
| Move | Left-click cyan tile |
| Attack | Left-click red tile |
| View enemy info | Left-click red unit |
| Deselect | Left-click empty tile |
| End Turn | Button or **Enter** |
| Pause / Settings | **ESC** |

---

## Project Structure

```
proj/
├── project.godot                  Entry point: LevelSelectScene
├── TacticsBattle.csproj           NuGet: GodotSharpDI 1.2.0-rc.1
│
├── Scenes/
│   ├── LevelSelectScene.tscn      Menu scene
│   ├── BattleScene.tscn           Single scene for ALL levels
│   └── TestScene.tscn             DI integration tests
│
└── Scripts/
    ├── Audio/                     Audio subsystem
    │   ├── IAudioService.cs       Strategy interface
    │   ├── AudioEnums.cs          BgmTrack + SfxEvent enums
    │   └── AudioService.cs        Godot implementation (procedural PCM synthesis)
    │
    ├── Models/                    Pure data — zero Godot, zero DI
    │   ├── Unit.cs                Unit instance (stats injected at construction)
    │   ├── UnitTemplate.cs        Immutable stat record
    │   ├── Tile.cs                Tile instance (rules injected at construction)
    │   ├── TileRules.cs           TileRule record
    │   ├── LevelDefinition.cs     Level data record + UnitSpawnInfo
    │   ├── LevelRegistry.cs       Static list of all levels (edit here to add levels)
    │   ├── SelectedLevel.cs       Inter-scene index carrier (static int)
    │   └── Components/
    │       ├── IUnitComponent.cs  Interface hierarchy
    │       ├── PassiveComponents.cs
    │       ├── OnAttackComponents.cs
    │       ├── OnHitComponents.cs
    │       ├── StatusComponents.cs
    │       └── AuraComponents.cs
    │
    ├── Systems/                   Pure static functions — zero Godot, zero DI
    │   ├── MovementSystem.cs      Dijkstra reachable tiles + terrain distances
    │   ├── CombatSystem.cs        Damage formula + AttackResult pipeline
    │   └── AISystem.cs            Enemy decision planning
    │
    ├── Services/                  Stateful interfaces + implementations (DI-managed)
    │   ├── IGameStateService / GameStateService
    │   ├── IMapService       / MapService        (delegates to MovementSystem)
    │   ├── IBattleService    / BattleService     (delegates to CombatSystem + AISystem)
    │   ├── ILevelRegistryService / LevelRegistryService
    │   ├── ISceneRouterService                   (implemented by SceneRouterHost)
    │   ├── IUnitDataProvider / StandardUnitDataProvider   ← strategy
    │   ├── ITileRuleProvider / StandardTileRuleProvider   ← strategy
    │   └── IUnitFactory      / UnitFactory
    │
    ├── Hosts/                     DI providers (Godot nodes with [Host])
    │   ├── StrategyHost           provides ITileRuleProvider + IUnitDataProvider + IUnitFactory
    │   ├── LevelRegistryHost      provides ILevelRegistryService
    │   ├── SceneRouterHost        provides ISceneRouterService (owns scene paths)
    │   ├── GameStateHost          provides IGameStateService
    │   ├── MapHost                provides IMapService (WaitFor registry + tileRules)
    │   ├── BattleHost             provides IBattleService (WaitFor gs + map; cached instance)
    │   ├── AudioHost              provides IAudioService (battle scenes)
    │   └── LevelSelectAudioHost   provides IAudioService (menu scene)
    │
    ├── Scope/
    │   ├── BattleScope            [Modules] for all battle levels
    │   └── LevelSelectScope       [Modules] for menu
    │
    ├── Users/                     DI consumers (Godot nodes with [User])
    │   ├── UnitManager            spawns units via IUnitFactory
    │   ├── BattleUI               HUD + unit info + pause menu + audio controls
    │   ├── BattleRenderer3D       3D world + status orbs + mouse input
    │   ├── AIController           triggers enemy turn
    │   └── LevelSelectUI          menu cards + play buttons
    │
    └── Tests/                     DI integration assertions
        └── *.cs
```

---

## GodotSharpDI Best Practices — Implementation Guide

This project demonstrates every major GodotSharpDI pattern. Each pattern is
explained with its location and the reasoning behind the choice.

### 1. `[Provide]` with a cached backing field

```csharp
// ✗ WRONG — creates a new instance on every access
[Provide(...)] public BattleService BattleSvc => new BattleService(...);

// ✓ CORRECT — always returns the same object
private BattleService? _svc;
[Provide(...)] public BattleService BattleSvc => _svc ??= new BattleService(...);
```

**Why it matters**: the DI framework calls the getter to inject the service into
every `[User]` and `[Host]` that needs it. If the getter creates a new instance each
time, different consumers get different objects — events subscribed on one instance
will never fire on another. This bug caused status-tick events to be silently lost.

**Location**: `BattleHost.cs`, `MapHost.cs`, `AudioHost.cs`

### 2. `WaitFor` for ordered initialisation

```csharp
[Provide(
    ExposedTypes = [typeof(IMapService)],
    WaitFor      = [nameof(_registry), nameof(_tileRules)]
)]
public MapService MapSvc => ...;
```

`WaitFor` declares that the provided service must not be created until the listed
injected fields are non-null. GodotSharpDI schedules initialisation automatically —
no manual `_Ready()` sequencing, no event polling, no null checks in startup code.

**Dependency chain in this project**:
```
StrategyHost (no deps) ─────────────────────────┐
LevelRegistryHost (no deps) ─────────────────┐  │
                                             ▼  ▼
MapHost [WaitFor: _registry + _tileRules] ───► IMapService
                                                  │
GameStateHost (no deps) ──────────────────────┐  │
                                              ▼  ▼
BattleHost [WaitFor: _gs + _map] ────────────► IBattleService
```

**Location**: `MapHost.cs`, `BattleHost.cs`

### 3. `IDependenciesResolved` for post-injection work

```csharp
void IDependenciesResolved.OnDependenciesResolved(bool ok)
{
    if (!ok) { GD.PrintErr("DI failed"); return; }
    // All [Inject] fields are guaranteed non-null here
    SpawnUnits();
    _gs!.BeginPlayerTurn();
}
```

Never access injected services in `_Ready()` — the DI container may not have
resolved them yet. `OnDependenciesResolved` fires exactly once, after all
`WaitFor` dependencies are satisfied.

**Location**: every `[User]` and data-providing `[Host]`

### 4. Circular dependency: back-reference via setter

```csharp
// GameStateService
public IBattleService? BattleService { private get; set; }

// BattleHost.OnDependenciesResolved
if (_gs is GameStateService concrete)
    concrete.BattleService = BattleSvc;  // wired after construction
```

`GameStateService` needs to call `ProcessTurnStart` on `BattleService` at turn
boundaries, but `BattleService` also depends on `IGameStateService`. A constructor
cycle would deadlock the DI system.

Solution: `GameStateService` holds a nullable setter-injected reference.
`BattleHost.OnDependenciesResolved` wires it up once both sides exist.

**Location**: `GameStateService.cs`, `BattleHost.cs`

### 5. Scope-scoped vs scene-scoped services

| Scope | Services provided |
|-------|------------------|
| `LevelSelectScope` | ILevelRegistryService, ISceneRouterService, IAudioService(menu) |
| `BattleScope` | all of the above + ITileRuleProvider, IUnitDataProvider, IUnitFactory, IGameStateService, IMapService, IBattleService, IAudioService(battle) |

Each scene has its own scope. Services are created fresh when the scene loads
and garbage-collected when it unloads. There are no global/autoload singletons.
`SelectedLevel.Index` (a plain static int) is the only cross-scene state —
intentionally minimal.

### 6. Strategy pattern via DI

```csharp
// To double all enemy HP for a hard-mode variant:
public sealed class HardModeUnitDataProvider : IUnitDataProvider
{
    public UnitTemplate GetTemplate(UnitType type)
    {
        var base_ = new StandardUnitDataProvider().GetTemplate(type);
        return type == UnitType.Warrior || ... // enemies only
            ? base_ with { MaxHp = base_.MaxHp * 2 }
            : base_;
    }
}

// In a HardBattleScope:
[Modules(Hosts = [typeof(HardStrategyHost), ...])]
```

Nothing in `MapService`, `BattleService`, `UnitManager`, or `BattleRenderer3D`
changes. The scope wires the alternative strategy; the rest is transparent.

**Location**: `StrategyHost.cs`, `IUnitDataProvider.cs`, `ITileRuleProvider.cs`

### 7. `[Host]` that is itself the service (`ISceneRouterService`)

```csharp
[Host]
public sealed partial class SceneRouterHost : Node, ISceneRouterService
{
    [Provide(ExposedTypes = [typeof(ISceneRouterService)])]
    public ISceneRouterService Router => this;
}
```

When the host *is* the service (no separate class needed), expose `this`.
The host node handles Godot scene-tree operations (`GetTree().ChangeSceneToFile`)
while consumers receive only the interface — they cannot call Godot APIs directly.

**Location**: `SceneRouterHost.cs`

---

## Adding a new level

Edit exactly one file: `Scripts/Models/LevelRegistry.cs`

```csharp
new LevelDefinition(
    Index: 3, Name: "Desert Raid", Difficulty: "★★★  Hard",
    Description: "Ambush the convoy!\nSurvive until reinforcements arrive.",
    MapWidth: 12, MapHeight: 10, Theme: MapTheme.Forest,
    Units: new[]
    {
        new UnitSpawnInfo("Hero", UnitType.Warrior, Team.Player, new Vector2I(6, 9)),
        // Extra components stack on top of archetype defaults:
        new UnitSpawnInfo("Veteran", UnitType.Warrior, Team.Player, new Vector2I(5, 9),
            ExtraComponents: new IUnitComponent[]
            {
                new ArmorComponent(FlatReduction: 15),
                new CounterAttackComponent(DamageRatio: 0.25f),
            }),
        // ...enemies...
    }),
```

The menu card, level routing, and scene loading all update automatically.

---

## Audio Architecture

The audio system is fully abstracted behind `IAudioService`:

```
IAudioService
  .PlayBgm(BgmTrack.Battle)      // switches background music
  .PlaySfx(SfxEvent.AttackHit)   // fire-and-forget effect
  .SetBgmVolume(0.55f)           // linear 0–1
  .SetSfxVolume(0.80f)           // linear 0–1
```

`AudioService` (the concrete implementation) uses **procedurally synthesised PCM**
so the project ships with zero binary audio files.  Replace `GenerateBgm()` and
`GenerateSfx()` with `AudioStreamOggVorbis.LoadFromFile(path)` calls to use real
audio in production.

An SFX pool of 8 `AudioStreamPlayer` nodes prevents overlapping effects from
cutting each other off.  Volume sliders in the pause menu write directly to
`IAudioService` — no coupling to the concrete type.
