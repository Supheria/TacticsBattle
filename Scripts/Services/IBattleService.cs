using System;
using TacticsBattle.Models;
using TacticsBattle.Models.Components;

namespace TacticsBattle.Services;

public interface IBattleService
{
    // ── Core ─────────────────────────────────────────────────────────────
    int  CalculateDamage(Unit attacker, Unit defender);
    int  ExecuteAttack(Unit attacker, Unit defender);
    void RunEnemyTurn();

    // ── Attack events ─────────────────────────────────────────────────────
    /// <summary>attacker, defender, damage</summary>
    event Action<Unit, Unit, int>  OnAttackExecuted;
    event Action<Unit>             OnUnitDefeated;

    // ── Component-driven events ───────────────────────────────────────────
    /// <summary>A status component was applied to a unit.</summary>
    event Action<Unit, IStatusComponent> OnStatusApplied;

    /// <summary>Counter-damage or thorn damage landed on attacker.</summary>
    event Action<Unit, int>              OnCounterDamage;

    /// <summary>A unit was pushed to a new position.</summary>
    event Action<Unit, Godot.Vector2I>   OnUnitPushed;

    // ── Turn-start events ─────────────────────────────────────────────────
    /// <summary>A status effect ticked (e.g. poison dealt damage).</summary>
    event Action<Unit, IStatusComponent, int> OnStatusTick;

    /// <summary>An aura healed a unit. (healer, recipient, amount)</summary>
    event Action<Unit, Unit, int>             OnAuraHeal;

    // ── Called at the START of each team turn to tick statuses & auras ────
    void ProcessTurnStart(TacticsBattle.Models.Team team);

    event Action? OnEnemyTurnFinished;
}
