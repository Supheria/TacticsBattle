namespace TacticsBattle.Models.Components;

/// <summary>Reduces all incoming damage by a flat amount (minimum 1 still applies).</summary>
public sealed record ArmorComponent(int FlatReduction) : IPassiveComponent;

/// <summary>Adds bonus tiles to movement range each turn.</summary>
public sealed record MovementBonusComponent(int BonusRange) : IPassiveComponent;
