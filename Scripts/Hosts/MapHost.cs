using Godot;
using GodotSharpDI.Abstractions;
using TacticsBattle.Services;

namespace TacticsBattle.Hosts;

/// <summary>
/// DI Host node that constructs and exposes MapService (8x8 grid) to the scope.
/// </summary>
[Host]
public sealed partial class MapHost : Node
{
    [Export] public int MapWidth  { get; set; } = 8;
    [Export] public int MapHeight { get; set; } = 8;

    private MapService? _service;

    [Provide(ExposedTypes = [typeof(IMapService)])]
    public MapService MapSvc
    {
        get
        {
            _service ??= new MapService(MapWidth, MapHeight);
            return _service;
        }
    }

    public override partial void _Notification(int what);

    public override void _Ready()
    {
        GD.Print($"[MapHost] Ready — will provide IMapService ({MapWidth}x{MapHeight})");
    }
}
