namespace TacticsBattle.Models;

/// <summary>
/// Immutable stat block for one unit archetype.
/// Produced by IUnitDataProvider; consumed by UnitFactory.
/// Contains no static lookups and no component data (components are separate).
/// </summary>
public sealed record UnitTemplate(
    int MaxHp,
    int Attack,
    int Defense,
    int MoveRange,
    int AttackRange);
