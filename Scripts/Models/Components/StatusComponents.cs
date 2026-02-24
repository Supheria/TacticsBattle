using Godot;

namespace TacticsBattle.Models.Components;

/// <summary>Unit takes damage at the start of its team's turn.</summary>
public sealed class PoisonedComponent : IStatusComponent
{
    public int    DamagePerTurn { get; }
    public override string DisplayName  => "Poisoned";
    public override string DisplayEmoji => "☠";
    public Color   DisplayColor => new(0.20f, 0.80f, 0.15f);

    public PoisonedComponent(int dmgPerTurn, int turns)
    {
        DamagePerTurn  = dmgPerTurn;
        TurnsRemaining = turns;
    }
}

/// <summary>Unit's effective move range is reduced for N turns.</summary>
public sealed class SlowedComponent : IStatusComponent
{
    public int    MoveReduction { get; }
    public override string DisplayName  => "Slowed";
    public override string DisplayEmoji => "❄";
    public Color   DisplayColor => new(0.30f, 0.60f, 1.00f);

    public SlowedComponent(int moveReduction, int turns)
    {
        MoveReduction  = moveReduction;
        TurnsRemaining = turns;
    }
}
