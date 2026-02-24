using System.Collections.Generic;
using Godot;

namespace TacticsBattle.Models;

/// <summary>
/// Static registry of every level definition.
/// Previously scattered across Level1ConfigHost, Level2ConfigHost, Level3ConfigHost.
/// Now in one place: adding a level = adding one entry here.
/// </summary>
public static class LevelRegistry
{
    public static IReadOnlyList<LevelDefinition> All { get; } = new[]
    {
        // ── Level 0 — Forest Skirmish ──────────────────────────────────────
        new LevelDefinition(
            Index:       0,
            Name:        "Forest Skirmish",
            Description: "A balanced clash in the forest.\nLearn the basics of movement and combat.",
            Difficulty:  "★☆☆  Easy",
            MapWidth:    8,
            MapHeight:   8,
            Theme:       MapTheme.Forest,
            Units: new[]
            {
                new UnitSpawnInfo("Arthur", UnitType.Warrior, Team.Player, new Vector2I(1, 6)),
                new UnitSpawnInfo("Lyra",   UnitType.Archer,  Team.Player, new Vector2I(3, 7)),
                new UnitSpawnInfo("Merlin", UnitType.Mage,    Team.Player, new Vector2I(5, 6)),
                new UnitSpawnInfo("Orc A",  UnitType.Warrior, Team.Enemy,  new Vector2I(2, 1)),
                new UnitSpawnInfo("Orc B",  UnitType.Warrior, Team.Enemy,  new Vector2I(5, 0)),
                new UnitSpawnInfo("Goblin", UnitType.Archer,  Team.Enemy,  new Vector2I(4, 2)),
            }),

        // ── Level 1 — River Crossing ───────────────────────────────────────
        new LevelDefinition(
            Index:       1,
            Name:        "River Crossing",
            Description: "Cross the river before the enemy flanks you!\n4 v 5 — two wide fords matter.",
            Difficulty:  "★★☆  Medium",
            MapWidth:    10,
            MapHeight:   8,
            Theme:       MapTheme.River,
            Units: new[]
            {
                new UnitSpawnInfo("Arthur", UnitType.Warrior, Team.Player, new Vector2I(1, 6)),
                new UnitSpawnInfo("Golem",  UnitType.Warrior, Team.Player, new Vector2I(4, 7)),
                new UnitSpawnInfo("Lyra",   UnitType.Archer,  Team.Player, new Vector2I(6, 6)),
                new UnitSpawnInfo("Merlin", UnitType.Mage,    Team.Player, new Vector2I(8, 7)),
                new UnitSpawnInfo("Orc A",  UnitType.Warrior, Team.Enemy,  new Vector2I(0, 0)),
                new UnitSpawnInfo("Orc B",  UnitType.Warrior, Team.Enemy,  new Vector2I(4, 1)),
                new UnitSpawnInfo("Orc C",  UnitType.Warrior, Team.Enemy,  new Vector2I(9, 0)),
                new UnitSpawnInfo("Scout",  UnitType.Archer,  Team.Enemy,  new Vector2I(2, 0)),
                new UnitSpawnInfo("Shaman", UnitType.Mage,    Team.Enemy,  new Vector2I(7, 1)),
            }),

        // ── Level 2 — Mountain Pass ────────────────────────────────────────
        new LevelDefinition(
            Index:       2,
            Name:        "Mountain Pass",
            Description: "Hold the pass against overwhelming odds!\n3 v 7 — every decision counts.",
            Difficulty:  "★★★  Hard",
            MapWidth:    8,
            MapHeight:   12,
            Theme:       MapTheme.Mountain,
            Units: new[]
            {
                new UnitSpawnInfo("Arthur",  UnitType.Warrior, Team.Player, new Vector2I(3, 10)),
                new UnitSpawnInfo("Lyra",    UnitType.Archer,  Team.Player, new Vector2I(4, 10)),
                new UnitSpawnInfo("Merlin",  UnitType.Mage,    Team.Player, new Vector2I(3, 11)),
                new UnitSpawnInfo("Orc A",   UnitType.Warrior, Team.Enemy,  new Vector2I(2,  0)),
                new UnitSpawnInfo("Orc B",   UnitType.Warrior, Team.Enemy,  new Vector2I(5,  0)),
                new UnitSpawnInfo("Orc C",   UnitType.Warrior, Team.Enemy,  new Vector2I(3,  1)),
                new UnitSpawnInfo("Orc D",   UnitType.Warrior, Team.Enemy,  new Vector2I(4,  1)),
                new UnitSpawnInfo("Scout A", UnitType.Archer,  Team.Enemy,  new Vector2I(2,  2)),
                new UnitSpawnInfo("Scout B", UnitType.Archer,  Team.Enemy,  new Vector2I(5,  2)),
                new UnitSpawnInfo("Shaman",  UnitType.Mage,    Team.Enemy,  new Vector2I(3,  2)),
            }),
    };

    /// <summary>Safe indexed access; returns null if out of range.</summary>
    public static LevelDefinition? Get(int index) =>
        index >= 0 && index < All.Count ? All[index] : null;
}
