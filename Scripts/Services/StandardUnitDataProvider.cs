using System.Collections.Generic;
using TacticsBattle.Models;
using TacticsBattle.Models.Components;

namespace TacticsBattle.Services;

/// <summary>
/// Default unit data strategy — balanced stats used in all three levels.
/// Identical to the old static UnitTemplateLibrary, now a swappable service.
///
/// Example alternative: HardModeUnitDataProvider could give enemies +50% HP
/// without touching any other code.
/// </summary>
public sealed class StandardUnitDataProvider : IUnitDataProvider
{
    private static readonly IReadOnlyDictionary<UnitType, (UnitTemplate tmpl, IReadOnlyList<IUnitComponent> comps)> Data =
        new Dictionary<UnitType, (UnitTemplate, IReadOnlyList<IUnitComponent>)>
        {
            [UnitType.Warrior] = (
                new UnitTemplate(MaxHp:120, Attack:30, Defense:20, MoveRange:3, AttackRange:1),
                new IUnitComponent[] { new ArmorComponent(FlatReduction:5) }),

            [UnitType.Archer]  = (
                new UnitTemplate(MaxHp:80,  Attack:40, Defense:10, MoveRange:2, AttackRange:3),
                new IUnitComponent[] { new SlowOnHitComponent(MoveReduction:1, Duration:1) }),

            [UnitType.Mage]    = (
                new UnitTemplate(MaxHp:60,  Attack:60, Defense: 5, MoveRange:2, AttackRange:2),
                new IUnitComponent[] { new PoisonOnHitComponent(DamagePerTurn:8, Duration:2) }),
        };

    public UnitTemplate GetTemplate(UnitType type) =>
        Data.TryGetValue(type, out var d) ? d.tmpl
        : throw new System.ArgumentException($"No template for {type}");

    public IReadOnlyList<IUnitComponent> GetDefaultComponents(UnitType type) =>
        Data.TryGetValue(type, out var d) ? d.comps
        : throw new System.ArgumentException($"No components for {type}");
}
