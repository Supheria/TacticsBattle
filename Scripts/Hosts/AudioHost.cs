using Godot;
using GodotSharpDI.Abstractions;
using TacticsBattle.Audio;

namespace TacticsBattle.Hosts;

/// <summary>
/// Single audio host used by BOTH BattleScope and LevelSelectScope.
/// The BgmTrack to play on start is set in the scene by subclassing
/// or via the exported property, avoiding duplicate [Host] declarations
/// that would cause GDI_D041.
///
/// BattleScene  → sets StartTrack = BgmTrack.Battle  (default)
/// LevelSelect  → sets StartTrack = BgmTrack.Menu
/// </summary>
[Host]
public sealed partial class AudioHost : Node, IAudioService, IDependenciesResolved
{
    [Export]
    public BgmTrack StartTrack { get; set; } = BgmTrack.Battle;

    private AudioService? _impl;

    [Provide(ExposedTypes = [typeof(IAudioService)])]
    public AudioHost AudioSvc => this;

    public override partial void _Notification(int what);

    public override void _Ready()
    {
        var bgm = new AudioStreamPlayer { Name = "BGM", VolumeDb = -5 };
        AddChild(bgm);
        var pool = new AudioStreamPlayer[8];
        for (int i = 0; i < 8; i++)
        {
            pool[i] = new AudioStreamPlayer { Name = $"SFX{i}", VolumeDb = 0 };
            AddChild(pool[i]);
        }
        _impl = new AudioService(bgm, pool);
    }

    void IDependenciesResolved.OnDependenciesResolved(bool ok)
    {
        if (!ok)
            return;
        PlayBgm(StartTrack);
        GD.Print($"[AudioHost] BGM started: {StartTrack}");
    }

    public void PlayBgm(BgmTrack t) => _impl?.PlayBgm(t);

    public void StopBgm() => _impl?.StopBgm();

    public void SetBgmVolume(float v) => _impl?.SetBgmVolume(v);

    public void PlaySfx(SfxEvent s) => _impl?.PlaySfx(s);

    public void SetSfxVolume(float v) => _impl?.SetSfxVolume(v);
}
