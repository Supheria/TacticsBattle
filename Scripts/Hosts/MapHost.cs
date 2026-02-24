using Godot;
using GodotSharpDI.Abstractions;
using TacticsBattle.Models;
using TacticsBattle.Services;

namespace TacticsBattle.Hosts;

/// <summary>
/// [Host] that provides IMapService.
/// Waits for ILevelConfigService so it can read map dimensions and theme.
/// This WaitFor demonstrates GodotSharpDI's dependency ordering.
/// </summary>
[Host]
public sealed partial class MapHost : Node, IDependenciesResolved
{
    [Inject] private ILevelConfigService? _levelConfig;

    [Provide(
        ExposedTypes = [typeof(IMapService)],
        WaitFor      = [nameof(_levelConfig)]
    )]
    public MapService MapSvc => new MapService(
        _levelConfig!.Config.MapWidth,
        _levelConfig.Config.MapHeight,
        _levelConfig.Config.Theme);

    public override partial void _Notification(int what);

    void IDependenciesResolved.OnDependenciesResolved(bool ok)
    {
        if (ok) GD.Print($"[MapHost] Providing {_levelConfig!.Config.MapWidth}x{_levelConfig.Config.MapHeight} map ({_levelConfig.Config.Theme})");
        else    GD.PrintErr("[MapHost] ILevelConfigService missing!");
    }
}
