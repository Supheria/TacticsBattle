using System;
using System.Collections.Generic;
using Godot;
using TacticsBattle.Audio;

namespace TacticsBattle.Audio;

/// <summary>
/// Pure C# audio implementation — does NOT inherit Node.
/// AudioStreamPlayer nodes are owned by AudioHost (which IS a Node).
/// This separates audio logic from scene-tree concerns.
///
/// PCM synthesis means zero binary audio assets are required.
/// Replace GenerateBgm/GenerateSfx with OGG/MP3 resources in production.
/// </summary>
public sealed class AudioService : IAudioService
{
    private readonly AudioStreamPlayer   _bgm;
    private readonly AudioStreamPlayer[] _sfxPool;
    private int                          _sfxNext;
    private const int                    PoolSize = 8;
    private BgmTrack                     _currentBgm = (BgmTrack)(-1);

    internal AudioService(AudioStreamPlayer bgm, AudioStreamPlayer[] sfxPool)
    {
        _bgm     = bgm;
        _sfxPool = sfxPool;
    }

    // ── IAudioService ─────────────────────────────────────────────────────────

    public void PlayBgm(BgmTrack track)
    {
        if (track == _currentBgm) return;
        _currentBgm  = track;
        _bgm.Stream  = GenerateBgm(track);
        _bgm.Play();
        GD.Print($"[Audio] BGM → {track}");
    }

    public void StopBgm() { _bgm.Stop(); _currentBgm = (BgmTrack)(-1); }

    public void SetBgmVolume(float v) =>
        _bgm.VolumeDb = LinearToDb(v);

    public void PlaySfx(SfxEvent sfx)
    {
        var p = _sfxPool[_sfxNext % PoolSize]; _sfxNext++;
        p.Stop(); p.Stream = GenerateSfx(sfx); p.Play();
    }

    public void SetSfxVolume(float v)
    {
        foreach (var p in _sfxPool) p.VolumeDb = LinearToDb(v);
    }

    // ── PCM synthesis ─────────────────────────────────────────────────────────

    private static AudioStream GenerateBgm(BgmTrack t) => t switch
    {
        BgmTrack.Menu    => BgmTone(220f, 3.0f, 0.18f, WaveShape.Sine),
        BgmTrack.Battle  => BgmTone(110f, 2.0f, 0.22f, WaveShape.Square),
        BgmTrack.Victory => BgmTone(440f, 1.5f, 0.25f, WaveShape.Sine),
        BgmTrack.Defeat  => BgmTone( 80f, 4.0f, 0.15f, WaveShape.Sine),
        _                => BgmTone(220f, 2.0f, 0.10f, WaveShape.Sine),
    };

    // BgmTone: identical to Tone but with LoopMode.Forward so music repeats
    private static AudioStreamWav BgmTone(float freq, float dur, float amp,
                                          WaveShape wave = WaveShape.Sine)
    {
        const int sr = 22050;
        int n   = (int)(sr * dur);
        var buf = new byte[n * 2];
        for (int i = 0; i < n; i++)
        {
            float t   = (float)i / sr;
            float raw = wave == WaveShape.Square
                ? (MathF.Sin(2 * MathF.PI * freq * t) >= 0 ? 1f : -1f)
                : MathF.Sin(2 * MathF.PI * freq * t);
            // gentle fade at start only (no release — seamless loop)
            float env = i < sr * 0.03f ? (float)i / (sr * 0.03f) : 1f;
            short s   = (short)(raw * amp * env * short.MaxValue);
            buf[i*2]   = (byte)(s & 0xFF);
            buf[i*2+1] = (byte)((s >> 8) & 0xFF);
        }
        return new AudioStreamWav
        {
            Format    = AudioStreamWav.FormatEnum.Format16Bits,
            Stereo    = false,
            MixRate   = sr,
            Data      = buf,
            LoopMode  = AudioStreamWav.LoopModeEnum.Forward,  // ← loops indefinitely
            LoopBegin = 0,
            LoopEnd   = n,
        };
    }

    private static AudioStream GenerateSfx(SfxEvent s) => s switch
    {
        SfxEvent.UiClick       => Tone(880f, 0.05f, 0.30f),
        SfxEvent.UnitSelect    => Tone(660f, 0.08f, 0.25f),
        SfxEvent.UnitMove      => Tone(440f, 0.12f, 0.20f),
        SfxEvent.AttackHit     => Noise(     0.10f, 0.40f),
        SfxEvent.UnitDeath     => Tone(200f, 0.30f, 0.50f, WaveShape.Square),
        SfxEvent.StatusApplied => Tone(550f, 0.15f, 0.30f),
        SfxEvent.StatusTick    => Tone(330f, 0.08f, 0.20f),
        SfxEvent.HealAura      => Tone(770f, 0.20f, 0.18f),
        SfxEvent.PushBack      => Noise(     0.08f, 0.35f),
        SfxEvent.Victory       => Tone(523f, 0.60f, 0.40f),
        SfxEvent.Defeat        => Tone( 98f, 0.80f, 0.40f, WaveShape.Square),
        _                      => Tone(440f, 0.05f, 0.15f),
    };

    private enum WaveShape { Sine, Square }

    private static AudioStreamWav Tone(float freq, float dur, float amp,
                                        WaveShape wave = WaveShape.Sine)
    {
        const int sr = 22050;
        int n   = (int)(sr * dur);
        var buf = new byte[n * 2];
        for (int i = 0; i < n; i++)
        {
            float t   = (float)i / sr;
            float env = Env(i, n);
            float raw = wave == WaveShape.Square
                ? (MathF.Sin(2 * MathF.PI * freq * t) >= 0 ? 1f : -1f)
                : MathF.Sin(2 * MathF.PI * freq * t);
            short s = (short)(raw * amp * env * short.MaxValue);
            buf[i*2]   = (byte)(s & 0xFF);
            buf[i*2+1] = (byte)((s >> 8) & 0xFF);
        }
        return MakeWav(buf, sr);
    }

    private static AudioStreamWav Noise(float dur, float amp)
    {
        const int sr = 22050;
        int n   = (int)(sr * dur);
        var buf = new byte[n * 2];
        var rng = new Random(42);
        for (int i = 0; i < n; i++)
        {
            float env = Env(i, n);
            float raw = ((float)rng.NextDouble() * 2f - 1f) * amp * env;
            short s   = (short)(raw * short.MaxValue);
            buf[i*2]   = (byte)(s & 0xFF);
            buf[i*2+1] = (byte)((s >> 8) & 0xFF);
        }
        return MakeWav(buf, sr);
    }

    private static float Env(int i, int n)
    {
        float t = (float)i / n;
        if (t < 0.05f) return t / 0.05f;
        if (t > 0.80f) return (1f - t) / 0.20f;
        return 1f;
    }

    // CS0246 FIX: correct Godot 4 class name is AudioStreamWav (not AudioStreamWAV)
    private static AudioStreamWav MakeWav(byte[] data, int sampleRate) => new AudioStreamWav
    {
        Format   = AudioStreamWav.FormatEnum.Format16Bits,
        Stereo   = false,
        MixRate  = sampleRate,
        Data     = data,
        LoopMode = AudioStreamWav.LoopModeEnum.Disabled,
    };

    private static float LinearToDb(float v) =>
        v <= 0.0001f ? -80f : 20f * MathF.Log10(v);
}
