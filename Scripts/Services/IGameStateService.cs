using System;
using System.Collections.Generic;
using TacticsBattle.Models;

namespace TacticsBattle.Services;

public enum GamePhase { PlayerTurn, EnemyTurn, Victory, Defeat }

public interface IGameStateService
{
    int CurrentTurn { get; }
    GamePhase Phase { get; }
    bool IsPlayerTurn => Phase == GamePhase.PlayerTurn;

    IReadOnlyList<Unit> AllUnits    { get; }
    IReadOnlyList<Unit> PlayerUnits { get; }
    IReadOnlyList<Unit> EnemyUnits  { get; }

    Unit? SelectedUnit { get; set; }

    void AddUnit(Unit unit);
    void RemoveUnit(Unit unit);
    void BeginPlayerTurn();
    void BeginEnemyTurn();
    void EndTurn();
    void CheckVictoryCondition();

    /// <summary>Fires OnUnitMoved so the 3D renderer can sync world positions.</summary>
    void NotifyUnitMoved(Unit unit);

    event Action<GamePhase> OnPhaseChanged;
    event Action<int>       OnTurnStarted;
    event Action<Unit?>     OnSelectionChanged;
    event Action<Unit>      OnUnitMoved;
}
