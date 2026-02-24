using Godot;
using GodotSharpDI.Abstractions;
using TacticsBattle.Services;

namespace TacticsBattle.Hosts;

[Host]
public sealed partial class MapHost : Node, IDependenciesResolved
{
    [Inject] private ILevelRegistryService? _registry;
    [Inject] private ITileRuleProvider?     _tileRules;

    [Provide(
        ExposedTypes = [typeof(IMapService)],
        WaitFor      = [nameof(_registry), nameof(_tileRules)]
    )]
    public MapService MapSvc => _mapSvc ??= new MapService(
        _registry!.ActiveLevel.MapWidth,
        _registry.ActiveLevel.MapHeight,
        _registry.ActiveLevel.Theme,
        _tileRules!);

    private MapService? _mapSvc;

    public override partial void _Notification(int what);

    void IDependenciesResolved.OnDependenciesResolved(bool ok)
    {
        if (!ok) { GD.PrintErr("[MapHost] DI FAILED."); return; }
        var lvl = _registry!.ActiveLevel;
        GD.Print($"[MapHost] {lvl.MapWidth}×{lvl.MapHeight} {lvl.Theme} map ready.");
    }
}
