using Godot;
using GodotSharpDI.Abstractions;
using TacticsBattle.Services;

namespace TacticsBattle.Hosts;

/// <summary>
/// [Host] that provides IBattleService.
///
/// BUG FIX: The property getter "=> new BattleService(...)" was creating a new
/// instance on every access.  OnDependenciesResolved called BattleSvc once
/// (instance B) while the DI framework had already injected a different call
/// (instance A) to every User — so GameStateService.BattleService pointed to B
/// but all event subscribers were on A.  Result: ProcessTurnStart fired but
/// status ticks and orb-removal events never reached the renderer.
///
/// Fix: cache the instance in a backing field; [Provide] getter returns the
/// same object every time.
/// </summary>
[Host]
public sealed partial class BattleHost : Node, IDependenciesResolved
{
    [Inject] private IGameStateService? _gs;
    [Inject] private IMapService?       _map;

    // ── Cached singleton — created once, reused on every access ──────────────
    private BattleService? _battleSvc;

    [Provide(
        ExposedTypes = [typeof(IBattleService)],
        WaitFor      = [nameof(_gs), nameof(_map)]
    )]
    public BattleService BattleSvc => _battleSvc ??= new BattleService(_gs!, _map!);

    public override partial void _Notification(int what);

    void IDependenciesResolved.OnDependenciesResolved(bool ok)
    {
        if (!ok) { GD.PrintErr("[BattleHost] DI FAILED."); return; }
        // Wire back-reference once, on the same cached instance
        if (_gs is GameStateService concrete)
            concrete.BattleService = BattleSvc;
        GD.Print("[BattleHost] BattleService ready and back-ref wired.");
    }
}
