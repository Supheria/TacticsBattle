using System.Collections.Generic;
using Godot;
using GodotSharpDI.Abstractions;
using TacticsBattle.Models;
using TacticsBattle.Services;

namespace TacticsBattle.Hosts;

/// <summary>
/// [Host] for Level 2 — River Crossing (10×8, 4 v 5).
///
/// River at y = 3-4 with TWO-tile-wide fords (x = 2-3, x = 7-8).
/// Enemy spawn positions are deliberately spread across the map so they
/// approach both fords simultaneously rather than clustering at one crossing.
/// </summary>
[Host]
public sealed partial class Level2ConfigHost : Node
{
    private static readonly LevelConfig Cfg = new()
    {
        LevelName   = "River Crossing",
        Description = "Cross the river before the enemy flanks you!",
        Difficulty  = "Medium",
        MapWidth    = 10,
        MapHeight   = 8,
        Theme       = MapTheme.River,
        Units       = new List<UnitSpawnInfo>
        {
            // ── Player (4 units, south bank) ─────────────────────────────────
            // Spread across width — some near left ford, some near right ford
            new("Arthur", UnitType.Warrior, Team.Player, new Vector2I(1, 6)),
            new("Golem",  UnitType.Warrior, Team.Player, new Vector2I(4, 7)),
            new("Lyra",   UnitType.Archer,  Team.Player, new Vector2I(6, 6)),
            new("Merlin", UnitType.Mage,    Team.Player, new Vector2I(8, 7)),

            // ── Enemy (5 units, north bank) ──────────────────────────────────
            // Deliberately placed far apart so the AI approaches BOTH fords,
            // preventing a single-ford bottleneck.
            new("Orc A",  UnitType.Warrior, Team.Enemy, new Vector2I(0, 0)),  // far left
            new("Orc B",  UnitType.Warrior, Team.Enemy, new Vector2I(4, 1)),  // centre
            new("Orc C",  UnitType.Warrior, Team.Enemy, new Vector2I(9, 0)),  // far right
            new("Scout",  UnitType.Archer,  Team.Enemy, new Vector2I(2, 0)),  // left ford approach
            new("Shaman", UnitType.Mage,    Team.Enemy, new Vector2I(7, 1)),  // right ford approach
        },
    };

    [Provide(ExposedTypes = [typeof(ILevelConfigService)])]
    public LevelConfigService Config => new LevelConfigService(Cfg);

    public override partial void _Notification(int what);
}
