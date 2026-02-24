using System;
using System.Collections.Generic;
using Godot;
using TacticsBattle.Models;
using TacticsBattle.Models.Components;

namespace TacticsBattle.Systems;

// ── Effect value objects returned by CombatSystem ────────────────────────────
// BattleService processes these; no service dependency inside CombatSystem.

public abstract record PendingEffect;

/// <summary>Apply a status component to a unit (poison, slow…).</summary>
public sealed record ApplyStatusEffect(Unit Target, IStatusComponent Status) : PendingEffect;

/// <summary>Deal reflected / counter damage directly to a unit.</summary>
public sealed record CounterDamageEffect(Unit Target, int Damage) : PendingEffect;

/// <summary>Push a unit N tiles in a cardinal direction away from attacker.</summary>
public sealed record PushBackEffect(Unit Target, Vector2I Direction, int Distance) : PendingEffect;

/// <summary>Everything that happened as a result of one attack.</summary>
public sealed record AttackResult(
    int                        Damage,
    bool                       DefenderDefeated,
    IReadOnlyList<PendingEffect> Effects);

// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Pure-function combat resolver.
/// Reads component bags, computes all effects, returns an AttackResult.
/// No state, no events — the caller (BattleService) handles those.
/// </summary>
public static class CombatSystem
{
    public static int CalculateDamage(Unit attacker, Unit defender)
    {
        // Base: ATK − DEF − passive armor
        int armor = defender.GetComponent<ArmorComponent>()?.FlatReduction ?? 0;
        float base_ = Math.Max(1f, attacker.Attack - defender.Defense - armor);
        float var_  = 0.90f + GD.Randf() * 0.20f;
        return (int)(base_ * var_);
    }

    /// <summary>
    /// Fully resolve one attack, including all component interactions.
    /// Returns an AttackResult — caller applies it and fires events.
    /// </summary>
    public static AttackResult ResolveAttack(Unit attacker, Unit defender)
    {
        int dmg = CalculateDamage(attacker, defender);
        defender.Hp          = Math.Max(0, defender.Hp - dmg);
        attacker.HasAttacked = true;

        var effects = new List<PendingEffect>();

        // ── Attacker on-attack components ─────────────────────────────────────
        foreach (var c in attacker.GetComponents<IOnAttackComponent>())
        {
            switch (c)
            {
                case PoisonOnHitComponent p:
                    effects.Add(new ApplyStatusEffect(defender,
                        new PoisonedComponent(p.DamagePerTurn, p.Duration)));
                    break;

                case SlowOnHitComponent s:
                    effects.Add(new ApplyStatusEffect(defender,
                        new SlowedComponent(s.MoveReduction, s.Duration)));
                    break;

                case PushBackOnHitComponent pb:
                    // Direction = unit step away from attacker
                    var raw = defender.Position - attacker.Position;
                    var dir = Math.Abs(raw.X) >= Math.Abs(raw.Y)
                        ? new Vector2I(Math.Sign(raw.X), 0)
                        : new Vector2I(0, Math.Sign(raw.Y));
                    effects.Add(new PushBackEffect(defender, dir, pb.Distance));
                    break;
            }
        }

        // ── Defender on-hit components (only if defender survived) ────────────
        if (defender.IsAlive)
        {
            foreach (var c in defender.GetComponents<IOnHitComponent>())
            {
                switch (c)
                {
                    case CounterAttackComponent ca:
                        effects.Add(new CounterDamageEffect(attacker,
                            Math.Max(1, (int)(dmg * ca.DamageRatio))));
                        break;

                    case ThornComponent th:
                        effects.Add(new CounterDamageEffect(attacker, th.ReflectDamage));
                        break;
                }
            }
        }

        return new AttackResult(dmg, !defender.IsAlive, effects);
    }
}
