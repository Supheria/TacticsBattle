namespace TacticsBattle.Audio;

/// <summary>
/// Audio strategy interface.
/// Allows swapping to a stub/null implementation in tests, or to a
/// platform-specific implementation without touching any game logic.
/// </summary>
public interface IAudioService
{
    // ── Background music ──────────────────────────────────────────────────────
    void PlayBgm(BgmTrack track);
    void StopBgm();
    void SetBgmVolume(float linearVolume); // 0.0 – 1.0

    // ── Sound effects ─────────────────────────────────────────────────────────
    void PlaySfx(SfxEvent sfx);
    void SetSfxVolume(float linearVolume);
}
