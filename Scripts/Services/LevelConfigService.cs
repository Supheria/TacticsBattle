using TacticsBattle.Models;

namespace TacticsBattle.Services;

/// <summary>Simple wrapper that adapts a LevelConfig value to ILevelConfigService.</summary>
public class LevelConfigService : ILevelConfigService
{
    public LevelConfig Config { get; }
    public LevelConfigService(LevelConfig config) => Config = config;
}
