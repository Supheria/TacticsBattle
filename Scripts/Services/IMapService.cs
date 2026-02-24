using System.Collections.Generic;
using Godot;
using TacticsBattle.Models;

namespace TacticsBattle.Services;

public interface IMapService
{
    int MapWidth  { get; }
    int MapHeight { get; }

    Tile GetTile(int x, int y);
    Tile GetTile(Vector2I pos);
    bool IsValidPosition(int x, int y);
    bool IsValidPosition(Vector2I pos);

    Unit? GetUnitAt(Vector2I pos);
    void PlaceUnit(Unit unit, Vector2I pos);
    void MoveUnit(Unit unit, Vector2I newPos);

    List<Vector2I> GetReachableTiles(Unit unit);
    List<Unit>     GetAttackableTargets(Unit attacker);
    int            ManhattanDistance(Vector2I a, Vector2I b);

    /// <summary>
    /// Dijkstra from <paramref name="start"/> over terrain only (no unit blocking).
    /// Returns a dictionary mapping every reachable tile to its movement-cost distance.
    /// Used by the enemy AI to navigate around water, mountains and other terrain features.
    /// </summary>
    Dictionary<Vector2I, int> TerrainDistances(Vector2I start);
}
