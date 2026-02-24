using System;
using System.Collections.Generic;
using Godot;
using TacticsBattle.Models;

namespace TacticsBattle.Services;

public class GameStateService : IGameStateService
{
    // Set by BattleHost after construction to avoid circular ctor dependency
    public IBattleService? BattleService { private get; set; }

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

    public void AddUnit(Unit unit)         => _allUnits.Add(unit);
    public void RemoveUnit(Unit unit)      => _allUnits.Remove(unit);
    public void NotifyUnitMoved(Unit unit) => OnUnitMoved?.Invoke(unit);

    private bool IsGameOver => Phase == GamePhase.Victory || Phase == GamePhase.Defeat;

    public void BeginPlayerTurn()
    {
        if (IsGameOver) return;   // FIX: never restart if game is already decided
        Phase = GamePhase.PlayerTurn;
        foreach (var u in PlayerUnits) u.ResetActions();
        GD.Print($"=== Turn {CurrentTurn} - Player Phase ===");
        BattleService?.ProcessTurnStart(Team.Player);
        if (IsGameOver) return;   // FIX: status ticks may have ended the game
        OnTurnStarted?.Invoke(CurrentTurn);
        OnPhaseChanged?.Invoke(Phase);
    }

    public void BeginEnemyTurn()
    {
        if (IsGameOver) return;
        Phase = GamePhase.EnemyTurn;
        foreach (var u in EnemyUnits) u.ResetActions();
        GD.Print($"=== Turn {CurrentTurn} - Enemy Phase ===");
        BattleService?.ProcessTurnStart(Team.Enemy);
        if (IsGameOver) return;   // FIX: status ticks may have ended the game
        OnPhaseChanged?.Invoke(Phase);
    }

    public void EndTurn()
    {
        if (IsGameOver) return;   // FIX: don't advance turns after game over
        if      (Phase == GamePhase.PlayerTurn) BeginEnemyTurn();
        else if (Phase == GamePhase.EnemyTurn)  { CurrentTurn++; BeginPlayerTurn(); }
    }

    public void CheckVictoryCondition()
    {
        if (IsGameOver) return;   // already decided
        if (EnemyUnits.Count == 0)
        {
            Phase = GamePhase.Victory;
            GD.Print("*** VICTORY! ***");
            OnPhaseChanged?.Invoke(Phase);
        }
        else if (PlayerUnits.Count == 0)
        {
            Phase = GamePhase.Defeat;
            GD.Print("*** DEFEAT! ***");
            OnPhaseChanged?.Invoke(Phase);
        }
    }
}
