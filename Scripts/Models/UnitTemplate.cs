using System.Collections.Generic;

namespace TacticsBattle.Models;

/// <summary>
/// Immutable stat block for one unit archetype.
/// The single source of truth for all unit stats — no numbers anywhere else.
/// </summary>
public sealed record UnitTemplate(
    int MaxHp,
    int Attack,
    int Defense,
    int MoveRange,
    int AttackRange);

/// <summary>
/// Static registry of every unit archetype.
/// Add a new type here; nothing else needs to change.
/// </summary>
public static class UnitTemplateLibrary
{
    private static readonly IReadOnlyDictionary<UnitType, UnitTemplate> Templates =
        new Dictionary<UnitType, UnitTemplate>
        {
            [UnitType.Warrior] = new(MaxHp: 120, Attack: 30, Defense: 20, MoveRange: 3, AttackRange: 1),
            [UnitType.Archer]  = new(MaxHp:  80, Attack: 40, Defense: 10, MoveRange: 2, AttackRange: 3),
            [UnitType.Mage]    = new(MaxHp:  60, Attack: 60, Defense:  5, MoveRange: 2, AttackRange: 2),
        };

    public static UnitTemplate Get(UnitType type) =>
        Templates.TryGetValue(type, out var t) ? t
        : throw new System.ArgumentException($"No template for UnitType.{type}");
}
