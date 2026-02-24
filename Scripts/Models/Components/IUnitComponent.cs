namespace TacticsBattle.Models.Components;

/// <summary>Marker interface for all unit components.</summary>
public interface IUnitComponent { }

/// <summary>Always-on stat modifier (applied during damage/movement calculation).</summary>
public interface IPassiveComponent : IUnitComponent { }

/// <summary>Triggers when THIS unit successfully attacks another.</summary>
public interface IOnAttackComponent : IUnitComponent { }

/// <summary>Triggers when THIS unit receives a hit.</summary>
public interface IOnHitComponent : IUnitComponent { }

/// <summary>
/// Temporary status effect.  Ticks at the start of the affected unit's team turn.
/// Mutable class (not record) because TurnsRemaining changes in place.
/// </summary>
public abstract class IStatusComponent : IUnitComponent
{
    public int  TurnsRemaining { get; set; }
    public abstract string DisplayName  { get; }
    public abstract string DisplayEmoji { get; }
}

/// <summary>Affects nearby allies at the start of each team turn.</summary>
public interface IAuraComponent : IUnitComponent { }
