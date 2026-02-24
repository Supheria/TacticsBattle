using System;
using TacticsBattle.Models;

namespace TacticsBattle.Services;

public interface IBattleService
{
    /// <summary>Calculate damage dealt from attacker to defender.</summary>
    int CalculateDamage(Unit attacker, Unit defender);

    /// <summary>Execute an attack action. Returns actual damage dealt.</summary>
    int ExecuteAttack(Unit attacker, Unit defender);

    /// <summary>Run the full enemy AI turn asynchronously.</summary>
    void RunEnemyTurn();

    event Action<Unit, Unit, int> OnAttackExecuted;
    event Action<Unit> OnUnitDefeated;
    event Action OnEnemyTurnFinished;
}
