using Godot;
using GodotSharpDI.Abstractions;

namespace TacticsBattle.Tests;

/// <summary>
/// DI scope for integration tests. Uses real service implementations via
/// lightweight test hosts (4×4 map, no rendering).
/// </summary>
[Modules(Hosts = [typeof(TestGameStateHost), typeof(TestMapHost), typeof(TestBattleHost)])]
public partial class TestBattleScope : Node, IScope
{
    public override partial void _Notification(int what);

    public override void _Ready() =>
        GD.Print("[TestBattleScope] Integration test scope initialised.");
}
