using System.Collections.Generic;
using Godot;
using TacticsBattle.Models.Components;

namespace TacticsBattle.Models;

/// <summary>
/// Single source of truth for every level.
///
/// Components are layered in two places:
///   1. UnitTemplateLibrary.DefaultComponents  — archetype defaults
///   2. UnitSpawnInfo.ExtraComponents          — level-specific overrides
///
/// Adding a level: append one entry here. Nothing else changes.
/// </summary>
public static class LevelRegistry
{
    public static IReadOnlyList<LevelDefinition> All { get; } = new[]
    {
        // ── Level 0 — Forest Skirmish (Easy, 8×8, 3v3) ────────────────────────
        // Standard archetypes — demonstrates template DefaultComponents only.
        // Warriors: ArmorComponent  | Archers: SlowOnHit  | Mages: PoisonOnHit
        new LevelDefinition(
            Index: 0, Name: "Forest Skirmish",
            Description: "A balanced clash in the forest.\nLearn how unit traits interact.",
            Difficulty: "★☆☆  Easy",
            MapWidth: 8, MapHeight: 8, Theme: MapTheme.Forest,
            Units: new[]
            {
                new UnitSpawnInfo("Arthur", UnitType.Warrior, Team.Player, new Vector2I(1, 6)),
                new UnitSpawnInfo("Lyra",   UnitType.Archer,  Team.Player, new Vector2I(3, 7)),
                new UnitSpawnInfo("Merlin", UnitType.Mage,    Team.Player, new Vector2I(5, 6)),
                new UnitSpawnInfo("Orc A",  UnitType.Warrior, Team.Enemy,  new Vector2I(2, 1)),
                new UnitSpawnInfo("Orc B",  UnitType.Warrior, Team.Enemy,  new Vector2I(5, 0)),
                new UnitSpawnInfo("Goblin", UnitType.Archer,  Team.Enemy,  new Vector2I(4, 2)),
            }),

        // ── Level 1 — River Crossing (Medium, 10×8, 4v5) ──────────────────────
        // Introduces ExtraComponents on top of archetype defaults.
        // Shaman: HealAura heals nearby enemies each turn — must be prioritised.
        // Orc C:  extra CounterAttack — dangerous to attack with weak units.
        new LevelDefinition(
            Index: 1, Name: "River Crossing",
            Description: "Cross the river before the enemy flanks you!\nBeware the Shaman's healing aura.",
            Difficulty: "★★☆  Medium",
            MapWidth: 10, MapHeight: 8, Theme: MapTheme.River,
            Units: new[]
            {
                new UnitSpawnInfo("Arthur", UnitType.Warrior, Team.Player, new Vector2I(1, 6)),
                new UnitSpawnInfo("Golem",  UnitType.Warrior, Team.Player, new Vector2I(4, 7)),
                new UnitSpawnInfo("Lyra",   UnitType.Archer,  Team.Player, new Vector2I(6, 6)),
                new UnitSpawnInfo("Merlin", UnitType.Mage,    Team.Player, new Vector2I(8, 7)),
                new UnitSpawnInfo("Orc A",  UnitType.Warrior, Team.Enemy,  new Vector2I(0, 0)),
                new UnitSpawnInfo("Orc B",  UnitType.Warrior, Team.Enemy,  new Vector2I(4, 1)),
                // Orc C: base Warrior armor + extra CounterAttack — beware sending in low-HP units
                new UnitSpawnInfo("Orc C",  UnitType.Warrior, Team.Enemy,  new Vector2I(9, 0),
                    ExtraComponents: new IUnitComponent[]
                    {
                        new CounterAttackComponent(DamageRatio: 0.35f),
                    }),
                new UnitSpawnInfo("Scout",  UnitType.Archer,  Team.Enemy,  new Vector2I(2, 0)),
                // Shaman: base Mage PoisonOnHit + HealAura — a high-priority target
                new UnitSpawnInfo("Shaman", UnitType.Mage,    Team.Enemy,  new Vector2I(7, 1),
                    ExtraComponents: new IUnitComponent[]
                    {
                        new HealAuraComponent(AmountPerTurn: 12, Radius: 2),
                    }),
            }),

        // ── Level 2 — Mountain Pass (Hard, 8×12, 3v7) ─────────────────────────
        // Multiple extra components per unit — introduces PushBack.
        // Enemy Archers: PushBack — keeps player units away from the pass.
        // Warlord (special): ThornComponent + heavy CounterAttack.
        new LevelDefinition(
            Index: 2, Name: "Mountain Pass",
            Description: "Hold the pass against overwhelming odds!\nThe Warlord reflects all damage taken.",
            Difficulty: "★★★  Hard",
            MapWidth: 8, MapHeight: 12, Theme: MapTheme.Mountain,
            Units: new[]
            {
                new UnitSpawnInfo("Arthur",  UnitType.Warrior, Team.Player, new Vector2I(3, 10)),
                new UnitSpawnInfo("Lyra",    UnitType.Archer,  Team.Player, new Vector2I(4, 10)),
                new UnitSpawnInfo("Merlin",  UnitType.Mage,    Team.Player, new Vector2I(3, 11)),
                new UnitSpawnInfo("Orc A",   UnitType.Warrior, Team.Enemy,  new Vector2I(2,  0)),
                new UnitSpawnInfo("Orc B",   UnitType.Warrior, Team.Enemy,  new Vector2I(5,  0)),
                // Orc C: extra Movement — breaks through the pass faster
                new UnitSpawnInfo("Orc C",   UnitType.Warrior, Team.Enemy,  new Vector2I(3,  1),
                    ExtraComponents: new IUnitComponent[]
                    {
                        new MovementBonusComponent(BonusRange: 1),
                    }),
                new UnitSpawnInfo("Orc D",   UnitType.Warrior, Team.Enemy,  new Vector2I(4,  1)),
                // Scout A + B: PushBack — knocks player units back toward their spawn
                new UnitSpawnInfo("Scout A", UnitType.Archer,  Team.Enemy,  new Vector2I(2,  2),
                    ExtraComponents: new IUnitComponent[]
                    {
                        new PushBackOnHitComponent(Distance: 1),
                    }),
                new UnitSpawnInfo("Scout B", UnitType.Archer,  Team.Enemy,  new Vector2I(5,  2),
                    ExtraComponents: new IUnitComponent[]
                    {
                        new PushBackOnHitComponent(Distance: 1),
                    }),
                // Warlord: extra armor + CounterAttack + Thorn — attacks bounce back hard
                new UnitSpawnInfo("Warlord", UnitType.Warrior, Team.Enemy,  new Vector2I(3,  2),
                    ExtraComponents: new IUnitComponent[]
                    {
                        new ArmorComponent(FlatReduction: 10),          // stacks with archetype armor
                        new CounterAttackComponent(DamageRatio: 0.50f),
                        new ThornComponent(ReflectDamage: 8),
                    }),
            }),
    };

    public static LevelDefinition? Get(int index) =>
        index >= 0 && index < All.Count ? All[index] : null;
}
