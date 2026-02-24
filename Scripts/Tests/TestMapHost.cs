using Godot;
using GodotSharpDI.Abstractions;
using TacticsBattle.Services;

namespace TacticsBattle.Tests;

/// <summary>
/// Integration-test Host: provides a small 4×4 MapService to keep tests fast.
/// </summary>
[Host]
public sealed partial class TestMapHost : Node
{
    [Provide(ExposedTypes = [typeof(IMapService)])]
    public MapService MapSvc => new MapService(width: 4, height: 4);

    public override partial void _Notification(int what);
}
