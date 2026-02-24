using System.Collections.Generic;

namespace TacticsBattle.Services;

public class LevelMenuService : ILevelMenuService
{
    public IReadOnlyList<LevelMenuItem> Levels { get; } = new[]
    {
        new LevelMenuItem(
            "Forest Skirmish",
            "A balanced 3 v 3 clash in the forest.\nLearn the basics of movement and combat.",
            "★☆☆  Easy",
            "res://Scenes/Level1Scene.tscn"),
        new LevelMenuItem(
            "River Crossing",
            "Cross the river before the enemy!\n4 v 5 — limited crossing points matter.",
            "★★☆  Medium",
            "res://Scenes/Level2Scene.tscn"),
        new LevelMenuItem(
            "Mountain Pass",
            "Defend the pass against overwhelming odds.\n3 v 7 — every decision counts.",
            "★★★  Hard",
            "res://Scenes/Level3Scene.tscn"),
    };
}
