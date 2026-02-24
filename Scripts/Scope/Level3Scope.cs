using Godot;
using GodotSharpDI.Abstractions;
using TacticsBattle.Hosts;

namespace TacticsBattle.Scope;

/// <summary>DI scope for Level 3 — Mountain Pass.</summary>
[Modules(Hosts = [typeof(Level3ConfigHost), typeof(GameStateHost), typeof(MapHost), typeof(BattleHost)])]
public partial class Level3Scope : Node, IScope
{
    public override partial void _Notification(int what);
}
