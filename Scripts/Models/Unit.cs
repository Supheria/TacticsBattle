using Godot;

namespace TacticsBattle.Models;

public enum UnitType { Warrior, Archer, Mage }
public enum Team     { Player, Enemy }

/// <summary>
/// Pure data container for one unit instance.
/// Stats are loaded from UnitTemplateLibrary — no numbers hardcoded here.
/// </summary>
public class Unit
{
    public int     Id       { get; }
    public string  Name     { get; }
    public UnitType Type    { get; }
    public Team     Team    { get; }

    public int MaxHp      { get; }
    public int Hp         { get; set; }
    public int Attack     { get; }
    public int Defense    { get; }
    public int MoveRange  { get; }
    public int AttackRange { get; }

    public Vector2I Position    { get; set; }
    public bool     HasMoved    { get; set; }
    public bool     HasAttacked { get; set; }
    public bool     IsAlive     => Hp > 0;

    public Unit(int id, string name, UnitType type, Team team, Vector2I position)
    {
        Id   = id;
        Name = name;
        Type = type;
        Team = team;
        Position = position;

        // Stats come from the library — Unit itself contains no stat numbers
        var tmpl = UnitTemplateLibrary.Get(type);
        MaxHp       = tmpl.MaxHp;
        Hp          = tmpl.MaxHp;
        Attack      = tmpl.Attack;
        Defense     = tmpl.Defense;
        MoveRange   = tmpl.MoveRange;
        AttackRange = tmpl.AttackRange;
    }

    public void ResetActions() { HasMoved = false; HasAttacked = false; }

    public override string ToString() =>
        $"[{Team}] {Name} ({Type}) HP:{Hp}/{MaxHp} @ ({Position.X},{Position.Y})";
}
