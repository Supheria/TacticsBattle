using Godot;
using GodotSharpDI.Abstractions;
using TacticsBattle.Services;

namespace TacticsBattle.Hosts;

[Host]
public sealed partial class GameStateHost : Node
{
    private GameStateService? _service;

    [Provide(ExposedTypes = [typeof(IGameStateService)])]
    public GameStateService GameStateSvc
    {
        get { _service ??= new GameStateService(); return _service; }
    }

    public override partial void _Notification(int what);
}
