using Godot;
using GodotSharpDI.Abstractions;
using TacticsBattle.Hosts;

namespace TacticsBattle.Scope;

/// <summary>
/// DI scope for Level 1 — Forest Skirmish.
/// Level1ConfigHost provides ILevelConfigService; MapHost reads it via WaitFor.
/// Swapping Level1ConfigHost → Level2ConfigHost here is the ONLY change needed to run a different level.
/// </summary>
[Modules(Hosts = [typeof(Level1ConfigHost), typeof(GameStateHost), typeof(MapHost), typeof(BattleHost)])]
public partial class Level1Scope : Node, IScope
{
    public override partial void _Notification(int what);
}
