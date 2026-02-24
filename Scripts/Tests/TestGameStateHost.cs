using Godot;
using GodotSharpDI.Abstractions;
using TacticsBattle.Services;

namespace TacticsBattle.Tests;

/// <summary>
/// Integration-test Host: provides real GameStateService so tests can exercise
/// full business logic (victory/defeat conditions, events, etc.).
/// </summary>
[Host]
public sealed partial class TestGameStateHost : Node
{
    [Provide(ExposedTypes = [typeof(IGameStateService)])]
    public GameStateService GameStateSvc => new GameStateService();

    public override partial void _Notification(int what);
}
