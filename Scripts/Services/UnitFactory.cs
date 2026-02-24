using System.Collections.Generic;
using Godot;
using TacticsBattle.Models;
using TacticsBattle.Models.Components;

namespace TacticsBattle.Services;

/// <summary>
/// Resolves unit templates and default components via IUnitDataProvider,
/// then constructs Unit instances.
/// </summary>
public sealed class UnitFactory : IUnitFactory
{
    private readonly IUnitDataProvider _data;
    public UnitFactory(IUnitDataProvider data) => _data = data;

    public Unit Create(int id, string name, UnitType type, Team team, Vector2I position,
                       IEnumerable<IUnitComponent>? extraComponents = null)
    {
        var template = _data.GetTemplate(type);
        var defaults = _data.GetDefaultComponents(type);
        return new Unit(id, name, type, team, position, template, defaults, extraComponents);
    }
}
