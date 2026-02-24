using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using TacticsBattle.Models.Components;

namespace TacticsBattle.Models;

public enum UnitType { Warrior, Archer, Mage }
public enum Team     { Player, Enemy }

/// <summary>
/// Unit instance — pure data container.
/// Stats and default components are injected at construction time via UnitFactory,
/// which reads from IUnitDataProvider (strategy pattern).
/// Unit itself has zero static dependencies.
/// </summary>
public class Unit
{
    public int      Id          { get; }
    public string   Name        { get; }
    public UnitType Type        { get; }
    public Team     Team        { get; }

    public int  MaxHp       { get; }
    public int  Hp          { get; set; }
    public int  Attack      { get; }
    public int  Defense     { get; }
    public int  MoveRange   { get; }
    public int  AttackRange { get; }

    public Vector2I Position    { get; set; }
    public bool     HasMoved    { get; set; }
    public bool     HasAttacked { get; set; }
    public bool     IsAlive     => Hp > 0;

    /// <summary>Move range after passive bonuses / slow status.</summary>
    public int EffectiveMoveRange
    {
        get
        {
            int r = MoveRange;
            if (GetComponent<MovementBonusComponent>() is { } bonus) r += bonus.BonusRange;
            if (GetComponent<SlowedComponent>()        is { } slow)  r  = Math.Max(0, r - slow.MoveReduction);
            return r;
        }
    }

    // ── Component bag ─────────────────────────────────────────────────────────
    private readonly List<IUnitComponent> _components = new();
    public  IReadOnlyList<IUnitComponent> Components => _components;

    public T? GetComponent<T>() where T : class, IUnitComponent =>
        _components.OfType<T>().FirstOrDefault();

    public bool HasComponent<T>() where T : class, IUnitComponent =>
        _components.OfType<T>().Any();

    public IEnumerable<T> GetComponents<T>() where T : class, IUnitComponent =>
        _components.OfType<T>();

    public void AddComponent(IUnitComponent c) => _components.Add(c);

    public void AddOrRefreshStatus(IStatusComponent incoming)
    {
        var existing = _components.OfType<IStatusComponent>()
                                  .FirstOrDefault(c => c.GetType() == incoming.GetType());
        if (existing != null)
            existing.TurnsRemaining = Math.Max(existing.TurnsRemaining, incoming.TurnsRemaining);
        else
            _components.Add(incoming);
    }

    public void RemoveComponent(IUnitComponent c) => _components.Remove(c);
    public void RemoveComponents<T>() where T : class, IUnitComponent =>
        _components.RemoveAll(c => c is T);

    // ── Construction ──────────────────────────────────────────────────────────
    /// <summary>
    /// Called by UnitFactory — receives pre-resolved template and components.
    /// No static lookups inside this constructor.
    /// </summary>
    public Unit(int id, string name, UnitType type, Team team, Vector2I position,
                UnitTemplate template,
                IEnumerable<IUnitComponent>? defaultComponents = null,
                IEnumerable<IUnitComponent>? extraComponents   = null)
    {
        Id   = id; Name = name; Type = type; Team = team; Position = position;

        MaxHp       = template.MaxHp;
        Hp          = template.MaxHp;
        Attack      = template.Attack;
        Defense     = template.Defense;
        MoveRange   = template.MoveRange;
        AttackRange = template.AttackRange;

        if (defaultComponents != null) foreach (var c in defaultComponents) _components.Add(c);
        if (extraComponents   != null) foreach (var c in extraComponents)   _components.Add(c);
    }

    public void ResetActions() { HasMoved = false; HasAttacked = false; }

    public override string ToString() =>
        $"[{Team}] {Name} ({Type}) HP:{Hp}/{MaxHp} @({Position.X},{Position.Y})" +
        (_components.Count > 0
            ? $" [{string.Join(",", _components.Select(c => c.GetType().Name.Replace("Component","")))}]"
            : "");
}
