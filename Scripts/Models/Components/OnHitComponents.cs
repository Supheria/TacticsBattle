namespace TacticsBattle.Models.Components;

/// <summary>Reflects a percentage of received damage back to the attacker.</summary>
public sealed record CounterAttackComponent(float DamageRatio) : IOnHitComponent;

/// <summary>Reflects a flat amount of damage back to the attacker on every hit.</summary>
public sealed record ThornComponent(int ReflectDamage) : IOnHitComponent;
