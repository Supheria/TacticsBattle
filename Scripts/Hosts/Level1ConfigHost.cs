using System.Collections.Generic;
using Godot;
using GodotSharpDI.Abstractions;
using TacticsBattle.Models;
using TacticsBattle.Services;

namespace TacticsBattle.Hosts;

/// <summary>
/// [Host] for Level 1 — Forest Skirmish (8x8, 3 v 3).
/// Provides ILevelConfigService; MapHost and UnitManager depend on it.
/// </summary>
[Host]
public sealed partial class Level1ConfigHost : Node
{
    private static readonly LevelConfig Cfg = new()
    {
        LevelName   = "Forest Skirmish",
        Description = "A balanced clash in the forest.",
        Difficulty  = "Easy",
        MapWidth    = 8,
        MapHeight   = 8,
        Theme       = MapTheme.Forest,
        Units       = new List<UnitSpawnInfo>
        {
            new("Arthur", UnitType.Warrior, Team.Player, new Vector2I(1, 6)),
            new("Lyra",   UnitType.Archer,  Team.Player, new Vector2I(3, 7)),
            new("Merlin", UnitType.Mage,    Team.Player, new Vector2I(5, 6)),
            new("Orc A",  UnitType.Warrior, Team.Enemy,  new Vector2I(2, 1)),
            new("Orc B",  UnitType.Warrior, Team.Enemy,  new Vector2I(5, 0)),
            new("Goblin", UnitType.Archer,  Team.Enemy,  new Vector2I(4, 2)),
        },
    };

    [Provide(ExposedTypes = [typeof(ILevelConfigService)])]
    public LevelConfigService Config => new LevelConfigService(Cfg);

    public override partial void _Notification(int what);
}
