using Godot;
using GodotSharpDI.Abstractions;
using TacticsBattle.Services;

namespace TacticsBattle.Tests;

/// <summary>
/// Integration-test Host: provides BattleService, waiting for
/// IGameStateService and IMapService to be ready first.
/// </summary>
[Host]
public sealed partial class TestBattleHost : Node, IDependenciesResolved
{
    [Inject] private IGameStateService? _gs;
    [Inject] private IMapService?       _map;

    [Provide(
        ExposedTypes = [typeof(IBattleService)],
        WaitFor      = [nameof(_gs), nameof(_map)]
    )]
    public BattleService BattleSvc => new BattleService(_gs!, _map!);

    public override partial void _Notification(int what);

    void IDependenciesResolved.OnDependenciesResolved(bool ok)
        => GD.Print($"[TestBattleHost] OnDependenciesResolved(allReady={ok})");
}
