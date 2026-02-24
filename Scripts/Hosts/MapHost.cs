using Godot;
using GodotSharpDI.Abstractions;
using TacticsBattle.Services;

namespace TacticsBattle.Hosts;

/// <summary>
/// [Host] that provides IMapService.
/// Waits for ILevelRegistryService so it can read the active level's
/// map dimensions and theme (WaitFor demonstrates DI ordering).
/// </summary>
[Host]
public sealed partial class MapHost : Node, IDependenciesResolved
{
    [Inject] private ILevelRegistryService? _registry;

    [Provide(
        ExposedTypes = [typeof(IMapService)],
        WaitFor      = [nameof(_registry)]
    )]
    public MapService MapSvc => new MapService(
        _registry!.ActiveLevel.MapWidth,
        _registry.ActiveLevel.MapHeight,
        _registry.ActiveLevel.Theme);

    public override partial void _Notification(int what);

    void IDependenciesResolved.OnDependenciesResolved(bool ok)
    {
        if (!ok) { GD.PrintErr("[MapHost] ILevelRegistryService missing!"); return; }
        var lvl = _registry!.ActiveLevel;
        GD.Print($"[MapHost] {lvl.MapWidth}×{lvl.MapHeight} map, theme={lvl.Theme}");
    }
}
