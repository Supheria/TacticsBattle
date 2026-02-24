using Godot;
using GodotSharpDI.Abstractions;
using TacticsBattle.Models;
using TacticsBattle.Services;

namespace TacticsBattle.Tests;

/// <summary>
/// Integration-test Host: provides a tiny 4×4 map for fast unit tests.
/// Uses StandardTileRuleProvider directly (no need to inject for tests).
///
/// GDI_D041 warning is expected when compiled alongside MapHost —
/// TestMapHost only appears in TestBattleScope and is never loaded
/// in BattleScene at runtime.
/// </summary>
[Host]
public sealed partial class TestMapHost : Node
{
    private MapService? _map;

    [Provide(ExposedTypes = [typeof(IMapService)])]
    public MapService MapSvc => _map ??= new MapService(
        4, 4, MapTheme.Forest, new StandardTileRuleProvider());

    public override partial void _Notification(int what);
}
