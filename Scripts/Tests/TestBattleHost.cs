using Godot;
using GodotSharpDI.Abstractions;
using TacticsBattle.Services;

namespace TacticsBattle.Tests;

[Host]
public sealed partial class TestBattleHost : Node, IDependenciesResolved
{
    [Inject] private IGameStateService? _gs;
    [Inject] private IMapService?       _map;

    private BattleService? _svc;

    [Provide(
        ExposedTypes = [typeof(IBattleService)],
        WaitFor      = [nameof(_gs), nameof(_map)]
    )]
    public BattleService BattleSvc => _svc ??= new BattleService(_gs!, _map!);

    public override partial void _Notification(int what);

    void IDependenciesResolved.OnDependenciesResolved(bool ok)
    {
        if (!ok) return;
        if (_gs is GameStateService concrete) concrete.BattleService = BattleSvc;
    }
}
