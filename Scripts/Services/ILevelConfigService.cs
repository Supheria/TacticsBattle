using TacticsBattle.Models;

namespace TacticsBattle.Services;

/// <summary>Provides the active level configuration to any injector that needs it.</summary>
public interface ILevelConfigService
{
    LevelConfig Config { get; }
}
