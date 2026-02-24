using Godot;
using GodotSharpDI.Abstractions;
using TacticsBattle.Hosts;

namespace TacticsBattle.Scope;

[Modules(Hosts = [
    typeof(LevelRegistryHost),
    typeof(SceneRouterHost),
    typeof(AudioHost),
])]
public partial class LevelSelectScope : Node, IScope
{
    public override partial void _Notification(int what);
}
