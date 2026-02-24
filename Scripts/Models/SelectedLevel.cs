namespace TacticsBattle.Models;

/// <summary>
/// Lightweight inter-scene state carrier.
/// The level-select screen writes Index; the battle scene reads it.
/// No autoload, no global node, no scene-tree dependency.
/// </summary>
public static class SelectedLevel
{
    public static int Index { get; set; } = 0;
}
