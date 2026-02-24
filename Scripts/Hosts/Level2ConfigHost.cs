using System.Collections.Generic;
using Godot;
using GodotSharpDI.Abstractions;
using TacticsBattle.Models;
using TacticsBattle.Services;

namespace TacticsBattle.Hosts;

/// <summary>
/// [Host] for Level 2 — River Crossing (10x8, 4 v 5).
/// Swapping this host into the scope automatically reconfigures the whole game.
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
            new("Arthur", UnitType.Warrior, Team.Player, new Vector2I(1, 6)),
            new("Golem",  UnitType.Warrior, Team.Player, new Vector2I(3, 6)),
            new("Lyra",   UnitType.Archer,  Team.Player, new Vector2I(6, 6)),
            new("Merlin", UnitType.Mage,    Team.Player, new Vector2I(8, 6)),
            new("Orc A",  UnitType.Warrior, Team.Enemy,  new Vector2I(1, 0)),
            new("Orc B",  UnitType.Warrior, Team.Enemy,  new Vector2I(5, 0)),
            new("Orc C",  UnitType.Warrior, Team.Enemy,  new Vector2I(8, 0)),
            new("Scout",  UnitType.Archer,  Team.Enemy,  new Vector2I(3, 1)),
            new("Shaman", UnitType.Mage,    Team.Enemy,  new Vector2I(6, 1)),
        },
    };

    [Provide(ExposedTypes = [typeof(ILevelConfigService)])]
    public LevelConfigService Config => new LevelConfigService(Cfg);

    public override partial void _Notification(int what);
}
