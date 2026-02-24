namespace TacticsBattle.Models.Components;

/// <summary>Applies a poison status to the target on hit.</summary>
public sealed record PoisonOnHitComponent(int DamagePerTurn, int Duration) : IOnAttackComponent;

/// <summary>Applies a slow status to the target on hit, reducing its movement range.</summary>
public sealed record SlowOnHitComponent(int MoveReduction, int Duration) : IOnAttackComponent;

/// <summary>Pushes the target one tile away from the attacker on hit.</summary>
public sealed record PushBackOnHitComponent(int Distance = 1) : IOnAttackComponent;
