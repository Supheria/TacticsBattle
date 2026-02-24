using Godot;
using GodotSharpDI.Abstractions;
using TacticsBattle.Hosts;

namespace TacticsBattle.Scope;

/// <summary>
/// Root DI scope for the battle scene.
/// Lists all [Host] types that participate in dependency resolution.
/// All [Host] and [User] nodes must be children (or descendants) of this node.
/// </summary>
[Modules(Hosts = [typeof(GameStateHost), typeof(MapHost), typeof(BattleHost)])]
public partial class BattleScope : Node, IScope
{
    public override partial void _Notification(int what);

    public override void _Ready()
    {
        GD.Print("[BattleScope] Scope initialised. DI wiring begins...");
    }
}
