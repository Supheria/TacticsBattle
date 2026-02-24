using Godot;
using GodotSharpDI.Abstractions;
using TacticsBattle.Services;

namespace TacticsBattle.Hosts;

/// <summary>
/// DI Host that depends on IGameStateService and IMapService before it can
/// construct BattleService. Demonstrates the WaitFor feature.
/// </summary>
[Host]
public sealed partial class BattleHost : Node, IDependenciesResolved
{
    [Inject]
    private IGameStateService? _gameStateService;

    [Inject]
    private IMapService? _mapService;

    // BattleService needs both services injected first → WaitFor
    [Provide(
        ExposedTypes  = [typeof(IBattleService)],
        WaitFor       = [nameof(_gameStateService), nameof(_mapService)]
    )]
    public BattleService BattleSvc =>
        new BattleService(_gameStateService!, _mapService!);

    public override partial void _Notification(int what);

    void IDependenciesResolved.OnDependenciesResolved(bool isAllDependenciesReady)
    {
        if (isAllDependenciesReady)
            GD.Print("[BattleHost] All dependencies resolved — BattleService is ready.");
        else
            GD.PrintErr("[BattleHost] Some dependencies FAILED to resolve!");
    }
}
