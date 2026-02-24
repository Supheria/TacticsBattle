using Godot;
using GodotSharpDI.Abstractions;
using TacticsBattle.Models;
using TacticsBattle.Services;

namespace TacticsBattle.Hosts;

/// <summary>
/// [Host] that provides ISceneRouterService.
/// This is the ONLY place in the codebase that references scene file paths.
/// Every other node (UI, menus, overlays) calls the service methods.
/// </summary>
[Host]
public sealed partial class SceneRouterHost : Node, ISceneRouterService
{
    // ── Scene file paths — single source of truth ─────────────────────────
    private const string BattleScenePath      = "res://Scenes/BattleScene.tscn";
    private const string LevelSelectScenePath = "res://Scenes/LevelSelectScene.tscn";

    [Provide(ExposedTypes = [typeof(ISceneRouterService)])]
    public SceneRouterHost Router => this;

    public override partial void _Notification(int what);

    // ── ISceneRouterService ───────────────────────────────────────────────

    public void GoToBattle(int levelIndex)
    {
        SelectedLevel.Index = levelIndex;
        GD.Print($"[SceneRouter] → Battle, level {levelIndex} ({LevelRegistry.Get(levelIndex)?.Name})");
        GetTree().ChangeSceneToFile(BattleScenePath);
    }

    public void GoToMenu()
    {
        GD.Print("[SceneRouter] → Level Select");
        GetTree().ChangeSceneToFile(LevelSelectScenePath);
    }

    public void RestartBattle()
    {
        GD.Print("[SceneRouter] → Restart");
        GetTree().Paused = false;
        GetTree().ReloadCurrentScene();
    }
}
