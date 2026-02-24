using Godot;
using GodotSharpDI.Abstractions;
using TacticsBattle.Hosts;

namespace TacticsBattle.Scope;

/// <summary>
/// Unified battle scope — used by ALL levels via a single BattleScene.tscn.
/// Which level to play is communicated through SelectedLevel.Index (written
/// by SceneRouterService before the scene loads).
///
/// Previously required Level1Scope + Level2Scope + Level3Scope (3 nearly
/// identical files). Now one scope serves every level.
/// </summary>
[Modules(Hosts = [
    typeof(LevelRegistryHost),
    typeof(SceneRouterHost),
    typeof(GameStateHost),
    typeof(MapHost),
    typeof(BattleHost),
])]
public partial class BattleScope : Node, IScope
{
    public override partial void _Notification(int what);
}
