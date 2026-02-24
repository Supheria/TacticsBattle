using Godot;
using GodotSharpDI.Abstractions;
using TacticsBattle.Services;

namespace TacticsBattle.Hosts;

[Host]
public sealed partial class BattleHost : Node, IDependenciesResolved
{
    [Inject] private IGameStateService? _gameStateService;
    [Inject] private IMapService?       _mapService;

    [Provide(
        ExposedTypes = [typeof(IBattleService)],
        WaitFor      = [nameof(_gameStateService), nameof(_mapService)]
    )]
    public BattleService BattleSvc => new BattleService(_gameStateService!, _mapService!);

    public override partial void _Notification(int what);

    void IDependenciesResolved.OnDependenciesResolved(bool ok)
        => GD.Print(ok ? "[BattleHost] BattleService ready." : "[BattleHost] FAILED.");
}
