using Godot;
using GodotSharpDI.Abstractions;
using TacticsBattle.Services;

namespace TacticsBattle.Users;

/// <summary>
/// DI User: listens for the EnemyTurn phase and delegates to IBattleService.RunEnemyTurn().
/// </summary>
[User]
public sealed partial class AIController : Node, IDependenciesResolved
{
    [Inject] private IGameStateService? _gameState;
    [Inject] private IBattleService?    _battleService;

    public override partial void _Notification(int what);

    void IDependenciesResolved.OnDependenciesResolved(bool isAllDependenciesReady)
    {
        if (!isAllDependenciesReady)
        {
            GD.PrintErr("[AIController] DI failed — AI disabled.");
            return;
        }

        GD.Print("[AIController] DI ready — AI is active.");
        _gameState!.OnPhaseChanged += OnPhaseChanged;
    }

    private void OnPhaseChanged(GamePhase phase)
    {
        if (phase == GamePhase.EnemyTurn)
        {
            GD.Print("[AIController] Enemy turn detected — running AI...");
            // Defer so the phase-change event fully propagates first
            CallDeferred(MethodName.RunAI);
        }
    }

    private void RunAI()
    {
        _battleService?.RunEnemyTurn();
    }
}
