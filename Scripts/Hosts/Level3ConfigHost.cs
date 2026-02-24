using System.Collections.Generic;
using Godot;
using GodotSharpDI.Abstractions;
using TacticsBattle.Models;
using TacticsBattle.Services;

namespace TacticsBattle.Hosts;

/// <summary>
/// [Host] for Level 3 — Mountain Pass (8×12, 3 v 7).
/// Mountain walls at x=0-1 and x=6-7 channel everyone through the 4-tile
/// centre corridor (x=2-5).  Hard mode: player defends a bottleneck.
/// </summary>
[Host]
public sealed partial class Level3ConfigHost : Node
{
    private static readonly LevelConfig Cfg = new()
    {
        LevelName   = "Mountain Pass",
        Description = "Hold the pass against overwhelming odds!",
        Difficulty  = "Hard",
        MapWidth    = 8,
        MapHeight   = 12,
        Theme       = MapTheme.Mountain,
        Units       = new List<UnitSpawnInfo>
        {
            // ── Player (3 units, bottom of pass) ─────────────────────────────
            new("Arthur", UnitType.Warrior, Team.Player, new Vector2I(3, 10)),
            new("Lyra",   UnitType.Archer,  Team.Player, new Vector2I(4, 10)),
            new("Merlin", UnitType.Mage,    Team.Player, new Vector2I(3, 11)),

            // ── Enemy (7 units, top of pass) ─────────────────────────────────
            // Spread across corridor width so multiple lanes are threatened
            new("Orc A",   UnitType.Warrior, Team.Enemy, new Vector2I(2, 0)),
            new("Orc B",   UnitType.Warrior, Team.Enemy, new Vector2I(5, 0)),
            new("Orc C",   UnitType.Warrior, Team.Enemy, new Vector2I(3, 1)),
            new("Orc D",   UnitType.Warrior, Team.Enemy, new Vector2I(4, 1)),
            new("Scout A", UnitType.Archer,  Team.Enemy, new Vector2I(2, 2)),
            new("Scout B", UnitType.Archer,  Team.Enemy, new Vector2I(5, 2)),
            new("Shaman",  UnitType.Mage,    Team.Enemy, new Vector2I(3, 2)),
        },
    };

    [Provide(ExposedTypes = [typeof(ILevelConfigService)])]
    public LevelConfigService Config => new LevelConfigService(Cfg);

    public override partial void _Notification(int what);
}
