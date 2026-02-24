using System;
using System.Collections.Generic;
using Godot;
using TacticsBattle.Models;

namespace TacticsBattle.Services;

public class GameStateService : IGameStateService
{
    private readonly List<Unit> _allUnits = new();
    private Unit? _selectedUnit;

    public int CurrentTurn { get; private set; } = 1;
    public GamePhase Phase { get; private set; } = GamePhase.PlayerTurn;

    public IReadOnlyList<Unit> AllUnits    => _allUnits;
    public IReadOnlyList<Unit> PlayerUnits => _allUnits.FindAll(u => u.Team == Team.Player && u.IsAlive);
    public IReadOnlyList<Unit> EnemyUnits  => _allUnits.FindAll(u => u.Team == Team.Enemy  && u.IsAlive);

    public Unit? SelectedUnit
    {
        get => _selectedUnit;
        set { _selectedUnit = value; OnSelectionChanged?.Invoke(_selectedUnit); }
    }

    public event Action<GamePhase>? OnPhaseChanged;
    public event Action<int>?       OnTurnStarted;
    public event Action<Unit?>?     OnSelectionChanged;
    public event Action<Unit>?      OnUnitMoved;

    public void AddUnit(Unit unit)    => _allUnits.Add(unit);
    public void RemoveUnit(Unit unit) => _allUnits.Remove(unit);
    public void NotifyUnitMoved(Unit unit) => OnUnitMoved?.Invoke(unit);

    public void BeginPlayerTurn()
    {
        Phase = GamePhase.PlayerTurn;
        foreach (var u in PlayerUnits) u.ResetActions();
        GD.Print($"=== Turn {CurrentTurn} - Player Phase ===");
        OnTurnStarted?.Invoke(CurrentTurn);
        OnPhaseChanged?.Invoke(Phase);
    }

    public void BeginEnemyTurn()
    {
        Phase = GamePhase.EnemyTurn;
        foreach (var u in EnemyUnits) u.ResetActions();
        GD.Print($"=== Turn {CurrentTurn} - Enemy Phase ===");
        OnPhaseChanged?.Invoke(Phase);
    }

    public void EndTurn()
    {
        if (Phase == GamePhase.PlayerTurn) BeginEnemyTurn();
        else if (Phase == GamePhase.EnemyTurn) { CurrentTurn++; BeginPlayerTurn(); }
    }

    public void CheckVictoryCondition()
    {
        if (EnemyUnits.Count == 0 && Phase != GamePhase.Victory)
        {
            Phase = GamePhase.Victory;
            GD.Print("*** VICTORY! All enemies defeated! ***");
            OnPhaseChanged?.Invoke(Phase);
        }
        else if (PlayerUnits.Count == 0 && Phase != GamePhase.Defeat)
        {
            Phase = GamePhase.Defeat;
            GD.Print("*** DEFEAT! All player units lost! ***");
            OnPhaseChanged?.Invoke(Phase);
        }
    }
}
