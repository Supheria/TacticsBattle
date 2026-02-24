using System;
using System.Collections.Generic;
using Godot;
using TacticsBattle.Models;
using TacticsBattle.Models.Components;
using TacticsBattle.Systems;

namespace TacticsBattle.Services;

public class BattleService : IBattleService
{
    private readonly IGameStateService _gs;
    private readonly IMapService       _map;

    public event Action<Unit, Unit, int>?             OnAttackExecuted;
    public event Action<Unit>?                        OnUnitDefeated;
    public event Action<Unit, IStatusComponent>?      OnStatusApplied;
    public event Action<Unit, int>?                   OnCounterDamage;
    public event Action<Unit, Vector2I>?              OnUnitPushed;
    public event Action<Unit, IStatusComponent, int>? OnStatusTick;
    public event Action<Unit, Unit, int>?             OnAuraHeal;
    public event Action?                              OnEnemyTurnFinished;

    public BattleService(IGameStateService gs, IMapService map) { _gs = gs; _map = map; }

    // ── Core ──────────────────────────────────────────────────────────────────

    public int CalculateDamage(Unit a, Unit d) => CombatSystem.CalculateDamage(a, d);

    public int ExecuteAttack(Unit attacker, Unit defender)
    {
        if (attacker.HasAttacked) return 0;
        var result = CombatSystem.ResolveAttack(attacker, defender);
        GD.Print($"{attacker.Name} → {defender.Name}  -{result.Damage} HP  ({defender.Hp}/{defender.MaxHp})");
        OnAttackExecuted?.Invoke(attacker, defender, result.Damage);
        ApplyEffects(result.Effects);
        if (result.DefenderDefeated) KillUnit(defender);
        return result.Damage;
    }

    // ── Effect pipeline ───────────────────────────────────────────────────────

    private void ApplyEffects(IReadOnlyList<PendingEffect> effects)
    {
        foreach (var fx in effects)
        {
            switch (fx)
            {
                case ApplyStatusEffect s:
                    if (!s.Target.IsAlive) break;
                    s.Target.AddOrRefreshStatus(s.Status);
                    GD.Print($"  ↳ {s.Target.Name} {s.Status.DisplayEmoji}{s.Status.DisplayName} ({s.Status.TurnsRemaining}t)");
                    OnStatusApplied?.Invoke(s.Target, s.Status);
                    break;

                case CounterDamageEffect cd:
                    if (!cd.Target.IsAlive) break;
                    cd.Target.Hp = Math.Max(0, cd.Target.Hp - cd.Damage);
                    GD.Print($"  ↳ Counter! {cd.Target.Name} -{cd.Damage} HP ({cd.Target.Hp}/{cd.Target.MaxHp})");
                    OnCounterDamage?.Invoke(cd.Target, cd.Damage);
                    if (!cd.Target.IsAlive) KillUnit(cd.Target);
                    break;

                case PushBackEffect pb:
                    if (!pb.Target.IsAlive) break;
                    TryPushUnit(pb.Target, pb.Direction, pb.Distance);
                    break;
            }
        }
    }

    private void TryPushUnit(Unit unit, Vector2I dir, int steps)
    {
        for (int i = 0; i < steps; i++)
        {
            var next = unit.Position + dir;
            if (!_map.IsValidPosition(next)) break;
            var tile = _map.GetTile(next);
            if (!tile.IsWalkable || tile.OccupyingUnit != null) break;
            _map.MoveUnit(unit, next);
        }
        GD.Print($"  ↳ {unit.Name} pushed → {unit.Position}");
        OnUnitPushed?.Invoke(unit, unit.Position);
    }

    private void KillUnit(Unit unit)
    {
        // Guard: only process units that ARE dead but still occupy a map tile.
        // unit.IsAlive == false means HP == 0 — that's exactly when we want to act.
        // !IsOnMap means already removed (Position == -1,-1) — avoid double-removal.
        if (unit.IsAlive || !IsOnMap(unit)) return;
        GD.Print($"  ☠ {unit.Name} defeated!");
        _map.MoveUnit(unit, new Vector2I(-1, -1));
        _gs.RemoveUnit(unit);
        OnUnitDefeated?.Invoke(unit);
        _gs.CheckVictoryCondition();
    }

    private static bool IsOnMap(Unit u) => u.Position.X >= 0 && u.Position.Y >= 0;

    // ── Turn-start: status ticks then aura heals ──────────────────────────────
    //
    // BUG FIX (status tick count not decrementing):
    //   Old code used "goto nextUnit" after poison kill, skipping TurnsRemaining--.
    //   SlowedComponent fell through the switch without any tick notification.
    //   Fix: process tick effect AND decrement in one unified path per status.
    //   Dead units from poison are killed immediately; loop continues on survivors.

    public void ProcessTurnStart(Team team)
    {
        var units = team == Team.Player
            ? new List<Unit>(_gs.PlayerUnits)
            : new List<Unit>(_gs.EnemyUnits);

        // ── 1. Status ticks ───────────────────────────────────────────────────
        foreach (var unit in units)
        {
            if (!unit.IsAlive || !IsOnMap(unit)) continue;

            var toRemove = new List<IStatusComponent>();

            foreach (var status in unit.GetComponents<IStatusComponent>())
            {
                // Apply effect
                int effectDmg = 0;
                switch (status)
                {
                    case PoisonedComponent p:
                        unit.Hp = Math.Max(0, unit.Hp - p.DamagePerTurn);
                        effectDmg = p.DamagePerTurn;
                        GD.Print($"  ☠ {unit.Name} poison -{effectDmg} HP ({unit.Hp}/{unit.MaxHp})");
                        break;
                    case SlowedComponent:
                        GD.Print($"  ❄ {unit.Name} slowed ({status.TurnsRemaining}t remaining)");
                        break;
                }

                // Always fire tick event (renderer updates label / HP bar)
                OnStatusTick?.Invoke(unit, status, effectDmg);

                // Decrement — always, regardless of type
                status.TurnsRemaining--;
                if (status.TurnsRemaining <= 0)
                    toRemove.Add(status);
            }

            // Remove expired statuses and notify renderer
            foreach (var s in toRemove)
            {
                unit.RemoveComponent(s);
                GD.Print($"  {unit.Name}: {s.DisplayName} expired");
                OnStatusApplied?.Invoke(unit, s);   // renderer re-syncs orbs
            }

            // Kill AFTER all statuses on this unit are processed
            if (!unit.IsAlive) KillUnit(unit);
        }

        // ── 2. Aura heals ─────────────────────────────────────────────────────
        // Re-query: some units may have died from poison above
        var survivors = team == Team.Player
            ? new List<Unit>(_gs.PlayerUnits)
            : new List<Unit>(_gs.EnemyUnits);

        foreach (var healer in survivors)
        {
            if (!healer.IsAlive) continue;
            foreach (var aura in healer.GetComponents<HealAuraComponent>())
            {
                foreach (var ally in survivors)
                {
                    if (!ally.IsAlive || ally == healer) continue;
                    if (_map.ManhattanDistance(healer.Position, ally.Position) > aura.Radius) continue;
                    int heal = Math.Min(aura.AmountPerTurn, ally.MaxHp - ally.Hp);
                    if (heal <= 0) continue;
                    ally.Hp += heal;
                    GD.Print($"  ✚ {healer.Name} aura → {ally.Name} +{heal}");
                    OnAuraHeal?.Invoke(healer, ally, heal);
                }
            }
        }
    }

    // ── Enemy AI turn ─────────────────────────────────────────────────────────

    public void RunEnemyTurn()
    {
        GD.Print("--- Enemy AI ---");
        var enemies = new List<Unit>(_gs.EnemyUnits);
        var actions = AISystem.PlanTurn(
            enemies, _gs.PlayerUnits,
            u => _map.GetReachableTiles(u),
            u => _map.GetAttackableTargets(u),
            p => _map.TerrainDistances(p));

        foreach (var action in actions)
        {
            // Stop processing if the game ended mid-turn (all players defeated)
            if (_gs.Phase == GamePhase.Defeat || _gs.Phase == GamePhase.Victory) break;
            if (!action.Enemy.IsAlive || !IsOnMap(action.Enemy)) continue;
            if (action.MoveTo != action.Enemy.Position)
            {
                GD.Print($"  {action.Enemy.Name} → {action.MoveTo}");
                _map.MoveUnit(action.Enemy, action.MoveTo);
            }
            var targets = _map.GetAttackableTargets(action.Enemy);
            if (targets.Count > 0) ExecuteAttack(action.Enemy, targets[0]);
        }
        GD.Print("--- Enemy done ---");
        OnEnemyTurnFinished?.Invoke();
        _gs.EndTurn();
    }
}
