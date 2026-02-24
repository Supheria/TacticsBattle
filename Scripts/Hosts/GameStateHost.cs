using Godot;
using GodotSharpDI.Abstractions;
using TacticsBattle.Services;

namespace TacticsBattle.Hosts;

/// <summary>
/// DI Host node that constructs and exposes GameStateService to the scope.
/// Must live inside a BattleScope node in the scene tree.
/// </summary>
[Host]
public sealed partial class GameStateHost : Node
{
    private GameStateService? _service;

    [Provide(ExposedTypes = [typeof(IGameStateService)])]
    public GameStateService GameStateSvc
    {
        get
        {
            _service ??= new GameStateService();
            return _service;
        }
    }

    public override partial void _Notification(int what);

    public override void _Ready()
    {
        GD.Print("[GameStateHost] Ready — will provide IGameStateService");
    }
}
