using Godot;
using GodotSharpDI.Abstractions;
using TacticsBattle.Hosts;

namespace TacticsBattle.Scope;

/// <summary>
/// Scope for the level-select screen.
/// Provides: ILevelRegistryService + ISceneRouterService.
/// No battle services needed here.
/// </summary>
[Modules(Hosts = [typeof(LevelRegistryHost), typeof(SceneRouterHost)])]
public partial class LevelSelectScope : Node, IScope
{
    public override partial void _Notification(int what);
}
