using Godot;
using GodotSharpDI.Abstractions;
using TacticsBattle.Models;
using TacticsBattle.Services;

namespace TacticsBattle.Hosts;

[Host]
public sealed partial class SceneRouterHost : Node, ISceneRouterService
{
    private const string BattleScenePath      = "res://Scenes/BattleScene.tscn";
    private const string LevelSelectScenePath = "res://Scenes/LevelSelectScene.tscn";

    [Provide(ExposedTypes = [typeof(ISceneRouterService)])]
    public SceneRouterHost Router => this;

    public override partial void _Notification(int what);

    public void GoToBattle(int levelIndex)
    {
        GetTree().Paused = false;   // always unpause — caller may be inside pause menu
        SelectedLevel.Index = levelIndex;
        GD.Print($"[SceneRouter] → Battle level {levelIndex}");
        GetTree().ChangeSceneToFile(BattleScenePath);
    }

    public void GoToMenu()
    {
        GetTree().Paused = false;   // BUG FIX: new scene inherits pause state if not cleared
        GD.Print("[SceneRouter] → Level Select");
        GetTree().ChangeSceneToFile(LevelSelectScenePath);
    }

    public void RestartBattle()
    {
        GetTree().Paused = false;
        GD.Print("[SceneRouter] → Restart");
        GetTree().ReloadCurrentScene();
    }
}
