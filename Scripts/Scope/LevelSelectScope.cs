using Godot;
using GodotSharpDI.Abstractions;
using TacticsBattle.Hosts;

namespace TacticsBattle.Scope;

/// <summary>
/// DI scope for the level-select screen.
/// Minimal: only LevelMenuHost is needed.
/// </summary>
[Modules(Hosts = [typeof(LevelMenuHost)])]
public partial class LevelSelectScope : Node, IScope
{
    public override partial void _Notification(int what);
}
