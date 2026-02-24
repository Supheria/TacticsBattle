using Godot;
using GodotSharpDI.Abstractions;
using TacticsBattle.Hosts;

namespace TacticsBattle.Scope;

[Modules(Hosts = [
    typeof(StrategyHost),
    typeof(LevelRegistryHost),
    typeof(SceneRouterHost),
    typeof(GameStateHost),
    typeof(MapHost),
    typeof(BattleHost),
    typeof(AudioHost),
])]
public partial class BattleScope : Node, IScope
{
    public override partial void _Notification(int what);
}
