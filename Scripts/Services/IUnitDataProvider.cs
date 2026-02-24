using System.Collections.Generic;
using TacticsBattle.Models;
using TacticsBattle.Models.Components;

namespace TacticsBattle.Services;

/// <summary>
/// Strategy interface for unit stat and component data.
/// Swap implementations to change unit balance (e.g. HardModeUnitDataProvider).
/// Replaces the static UnitTemplateLibrary — same data, now DI-injectable
/// and replaceable per scope.
/// </summary>
public interface IUnitDataProvider
{
    UnitTemplate              GetTemplate(UnitType type);
    IReadOnlyList<IUnitComponent> GetDefaultComponents(UnitType type);
}
