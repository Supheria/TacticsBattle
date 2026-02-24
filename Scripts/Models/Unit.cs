using Godot;

namespace TacticsBattle.Models;

public enum UnitType { Warrior, Archer, Mage }
public enum Team { Player, Enemy }

public class Unit
{
    public int Id { get; }
    public string Name { get; }
    public UnitType Type { get; }
    public Team Team { get; }

    public int MaxHp { get; }
    public int Hp { get; set; }
    public int Attack { get; }
    public int Defense { get; }
    public int MoveRange { get; }
    public int AttackRange { get; }

    public Vector2I Position { get; set; }
    public bool HasMoved { get; set; }
    public bool HasAttacked { get; set; }
    public bool IsAlive => Hp > 0;

    public Unit(int id, string name, UnitType type, Team team, Vector2I position)
    {
        Id = id;
        Name = name;
        Type = type;
        Team = team;
        Position = position;

        (MaxHp, Attack, Defense, MoveRange, AttackRange) = type switch
        {
            UnitType.Warrior => (120, 30, 20, 3, 1),
            UnitType.Archer  => (80,  40, 10, 2, 3),
            UnitType.Mage    => (60,  60,  5, 2, 2),
            _                => (100, 25, 15, 2, 1),
        };
        Hp = MaxHp;
    }

    public void ResetActions()
    {
        HasMoved = false;
        HasAttacked = false;
    }

    public override string ToString() =>
        $"[{Team}] {Name} ({Type}) HP:{Hp}/{MaxHp} @ ({Position.X},{Position.Y})";
}
