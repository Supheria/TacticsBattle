namespace TacticsBattle.Services;

/// <summary>
/// Abstracts all scene navigation.
/// UI nodes call these methods; no scene path strings leak into UI code.
/// The single place where scene paths are defined is SceneRouterHost.
/// </summary>
public interface ISceneRouterService
{
    /// <summary>Load the battle scene for the given level index.</summary>
    void GoToBattle(int levelIndex);

    /// <summary>Return to the level-select screen.</summary>
    void GoToMenu();

    /// <summary>Reload the current battle scene (restart).</summary>
    void RestartBattle();
}
