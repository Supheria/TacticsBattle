using System;
using Godot;
using TacticsBattle.Models;

namespace TacticsBattle.Systems;

/// <summary>
/// Pure-function combat calculations.
/// No events, no state — returns values and mutates only the
/// Unit data structs it receives (caller fires events).
/// </summary>
public static class CombatSystem
{
    /// <summary>
    /// Calculate damage with ±10 % random variance.
    /// Minimum 1 damage always applied.
    /// </summary>
    public static int CalculateDamage(Unit attacker, Unit defender)
    {
        float baseDmg  = Math.Max(1, attacker.Attack - defender.Defense);
        float variance = 0.9f + GD.Randf() * 0.2f;
        return (int)(baseDmg * variance);
    }

    /// <summary>
    /// Apply damage to defender.  Returns (damage, wasDefeated).
    /// Does NOT remove the unit from any collection — caller handles that.
    /// </summary>
    public static (int damage, bool defeated) ApplyAttack(Unit attacker, Unit defender)
    {
        int dmg = CalculateDamage(attacker, defender);
        defender.Hp         = Math.Max(0, defender.Hp - dmg);
        attacker.HasAttacked = true;
        return (dmg, !defender.IsAlive);
    }
}
