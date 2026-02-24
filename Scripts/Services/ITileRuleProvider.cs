using TacticsBattle.Models;

namespace TacticsBattle.Services;

/// <summary>
/// Strategy interface for tile movement rules.
/// Swap to change traversal costs (e.g. a "mounted" mode where Forest = 1).
/// Replaces the static TileRuleLibrary.
/// </summary>
public interface ITileRuleProvider
{
    TileRule GetRule(TileType type);
}
