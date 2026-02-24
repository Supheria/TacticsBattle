using Godot;
using GodotSharpDI.Abstractions;
using TacticsBattle.Hosts;

namespace TacticsBattle.Scope;

/// <summary>DI scope for Level 2 — River Crossing.</summary>
[Modules(Hosts = [typeof(Level2ConfigHost), typeof(GameStateHost), typeof(MapHost), typeof(BattleHost)])]
public partial class Level2Scope : Node, IScope
{
    public override partial void _Notification(int what);
}
