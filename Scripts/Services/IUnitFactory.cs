using System.Collections.Generic;
using Godot;
using TacticsBattle.Models;
using TacticsBattle.Models.Components;

namespace TacticsBattle.Services;

/// <summary>
/// Factory strategy for creating Unit instances.
/// Injects IUnitDataProvider so Unit construction is decoupled from static lookups.
/// </summary>
public interface IUnitFactory
{
    Unit Create(int id, string name, UnitType type, Team team, Vector2I position,
                IEnumerable<IUnitComponent>? extraComponents = null);
}
