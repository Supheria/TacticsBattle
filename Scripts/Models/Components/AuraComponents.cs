namespace TacticsBattle.Models.Components;

/// <summary>
/// Heals all living allied units within <see cref="Radius"/> tiles
/// at the start of each friendly team turn.
/// </summary>
public sealed record HealAuraComponent(int AmountPerTurn, int Radius = 2) : IAuraComponent;
