using System.Collections.Concurrent;
using KeyFXBoard.Core.Audio;
using KeyFXBoard.Core.Filtering;
using KeyFXBoard.Core.Keys;
using KeyFXBoard.Core.Packs;
using KeyFXBoard.Core.Profiles;

namespace KeyFXBoard.Core.Hosting;

public sealed class Engine
{
    private readonly ConcurrentQueue<(SampleBuffer Buffer, float Gain, int HoldKey, bool Sustain)> _triggers = new();
    private readonly ConcurrentQueue<(int HoldKey, float ReleaseSec)> _releases = new();
    private readonly Random _rng = new();
    private VoicePool _voices = new(24);
    private ProfileSnapshot? _profile;
    private PackRuntime? _legacyPack;
    private volatile FxGraph _fx = FxGraph.Create(new FxSettings());
    private volatile bool _muted;
    private volatile bool _silent;
    private volatile float _volume = 0.7f;
    private volatile float _appTrim = 1f;
    private volatile float _velocityRandom = 0.12f;
    private volatile float _outputBoost = 1f;
    private int _octaveShift;

    public Engine(int polyphony = 24)
    {
        _voices = new VoicePool(polyphony);
        Filter = new KeyFilter();
    }

    public KeyFilter Filter { get; }

    public int ActiveVoices => _voices.ActiveVoices;

    public bool Muted
    {
        get => _muted;
        set => _muted = value;
    }

    public float Volume
    {
        get => _volume;
        set => _volume = Math.Clamp(value, 0f, 1f);
    }

    public float AppTrim
    {
        get => _appTrim;
        set => _appTrim = Math.Clamp(value, 0f, 1f);
    }

    public float OutputBoost
    {
        get => _outputBoost;
        set => _outputBoost = Math.Clamp(value, 1f, 8f);
    }

    public float VelocityRandom
    {
        get => _velocityRandom;
        set => _velocityRandom = Math.Clamp(value, 0f, 0.5f);
    }

    public int OctaveShift => _octaveShift;

    public event Action<int>? OctaveShiftChanged;
    public event Action<string, bool>? PianoNoteChanged;
    public bool PianoVisualEnabled { get; set; }

    public string? ActiveProfileName => _profile?.Name;
    public string? ActivePackName => _profile?.Primary.Name ?? _legacyPack?.Name;
    public string ResidentPacks => _profile is null
        ? ActivePackName ?? ""
        : string.Join(", ", _profile.ResidentPackNames.Distinct());

    public void SetClick(SampleBuffer buffer)
    {
        _legacyPack = PackRuntime.SingleSample("factory-click", "Factory Click", buffer);
        _profile = null;
    }

    public void SetPack(PackRuntime pack)
    {
        _legacyPack = pack;
        _profile = null;
    }

    public void ApplyProfile(ProfileSnapshot profile)
    {
        if (_voices.Polyphony != profile.Polyphony)
        {
            _voices = new VoicePool(profile.Polyphony);
        }
        else
        {
            _voices.StopAll();
        }

        _profile = profile;
        _legacyPack = profile.Primary;
        _fx = profile.Fx;
        _silent = profile.Silent;
        _volume = profile.Volume;
        _velocityRandom = profile.VelocityRandom;
        _octaveShift = 0;
        CopyFilter(profile.Filter);
        OctaveShiftChanged?.Invoke(_octaveShift);
    }

    public void ApplyLive(ProfileDocument doc)
    {
        var polyphony = Math.Clamp(doc.Behavior.Polyphony, 1, 64);
        if (_voices.Polyphony != polyphony)
        {
            _voices = new VoicePool(polyphony);
        }

        _fx = FxGraph.Create(doc.Fx);
        _silent = doc.Silent;
        _volume = Math.Clamp(doc.Output.MasterVolume, 0f, 1f);
        _velocityRandom = Math.Clamp(doc.Behavior.VelocityRandom, 0f, 0.5f);
        CopyFilter(new FilterSettings
        {
            Repeat = doc.Behavior.Repeat,
            RepeatRateLimitHz = doc.Behavior.RepeatRateLimitHz,
            PlayOn = doc.Behavior.PlayOn,
            ModifierPolicy = doc.Behavior.ModifierPolicy,
            IgnoreInjected = doc.Behavior.IgnoreInjected,
            VariantMode = doc.Behavior.VariantMode,
            SilenceUnmapped = doc.Behavior.SilenceUnmapped,
            SilentGroups = [.. doc.Behavior.SilentGroups],
            SilentKeys = [.. doc.Behavior.SilentKeys],
            HoldSustain = doc.Behavior.HoldSustain,
            ReleaseMs = doc.Behavior.ReleaseMs,
            ForceSampleKey = doc.Behavior.ForceSampleKey
        });
    }

    public void Play(SampleBuffer sample)
    {
        if (!_muted && !_silent)
        {
            _triggers.Enqueue((sample, _volume * _appTrim, 0, false));
        }
    }

    public void PreviewActivePack()
    {
        var pack = _profile?.Primary ?? _legacyPack;
        if (pack?.Preview is { } preview)
        {
            Play(preview);
            return;
        }

        if (pack is null)
        {
            return;
        }

        var sample = pack.Resolve(
            new KeyEvent(KeyId.FromVirtualKey(0x51), KeyKind.Down, false, false, false, false),
            Filter.Settings.VariantMode,
            _octaveShift,
            Filter.Settings.ForceSampleKey);
        if (sample is not null)
        {
            Play(sample);
        }
    }

    public void Handle(KeyEvent e)
    {
        NotifyPianoVisual(e);

        if (_muted || _silent)
        {
            return;
        }

        var pack = _profile?.Primary ?? _legacyPack;
        if (e.Kind == KeyKind.Down && pack is not null && pack.TryOctaveReset(e.Key.VirtualKey))
        {
            if (_octaveShift != 0)
            {
                _octaveShift = 0;
                OctaveShiftChanged?.Invoke(_octaveShift);
            }

            return;
        }

        if (e.Kind == KeyKind.Down && pack is not null && pack.TryOctaveDelta(e.Key.VirtualKey, out var delta))
        {
            var next = Math.Clamp(_octaveShift + delta, -24, 24);
            if (next != _octaveShift)
            {
                _octaveShift = next;
                OctaveShiftChanged?.Invoke(_octaveShift);
            }

            return;
        }

        var sustain = Filter.Settings.HoldSustain && pack is { IsChromatic: true };
        if (sustain && e.Kind == KeyKind.Up)
        {
            _releases.Enqueue((e.Key.VirtualKey, Math.Clamp(Filter.Settings.ReleaseMs, 40, 2000) / 1000f));
            return;
        }

        if (sustain && e.Kind == KeyKind.Repeat)
        {
            return;
        }

        if (!Filter.ShouldPlay(in e))
        {
            return;
        }

        if (Filter.Settings.SilenceUnmapped && string.IsNullOrWhiteSpace(Filter.Settings.ForceSampleKey))
        {
            var mapped = _profile?.MapsVirtualKey(e.Key.VirtualKey)
                         ?? pack?.MapsVirtualKey(e.Key.VirtualKey)
                         ?? false;
            if (!mapped)
            {
                return;
            }
        }

        var sample = _profile?.Resolve(in e, _octaveShift)
                     ?? pack?.Resolve(e, Filter.Settings.VariantMode, _octaveShift, Filter.Settings.ForceSampleKey);
        if (sample is not null)
        {
            _triggers.Enqueue((sample, NextGain(), sustain ? e.Key.VirtualKey : 0, sustain));
        }
    }

    public void FillBuffer(Span<float> interleavedStereo)
    {
        while (_releases.TryDequeue(out var release))
        {
            _voices.ReleaseHold(release.HoldKey, release.ReleaseSec);
        }

        while (_triggers.TryDequeue(out var trigger))
        {
            _voices.Trigger(trigger.Buffer, trigger.Gain, trigger.HoldKey, trigger.Sustain);
        }

        _voices.Mix(interleavedStereo);
        var boost = _outputBoost;
        if (boost > 1.001f)
        {
            for (var i = 0; i < interleavedStereo.Length; i++)
            {
                interleavedStereo[i] *= boost;
            }
        }

        if (!_silent)
        {
            _fx.Process(interleavedStereo);
        }
    }

    public void StopAllVoices() => _voices.StopAll();

    private void NotifyPianoVisual(KeyEvent e)
    {
        if (!PianoVisualEnabled || e.Kind == KeyKind.Repeat)
        {
            return;
        }

        if (!PianoLayout.TryGetNote(e.Key.VirtualKey, out var note))
        {
            return;
        }

        var sounding = PianoLayout.Transpose(note, _octaveShift);
        PianoNoteChanged?.Invoke(sounding, e.Kind != KeyKind.Up);
    }

    private float NextGain()
    {
        var spread = _velocityRandom;
        var baseGain = _volume * _appTrim;
        if (spread <= 0f)
        {
            return Math.Clamp(baseGain, 0f, 2f);
        }

        var jitter = 1f + (((float)_rng.NextDouble() * 2f) - 1f) * spread;
        return Math.Clamp(baseGain * jitter, 0f, 2f);
    }

    private void CopyFilter(FilterSettings source)
    {
        Filter.Settings.Repeat = source.Repeat;
        Filter.Settings.RepeatRateLimitHz = source.RepeatRateLimitHz;
        Filter.Settings.PlayOn = source.PlayOn;
        Filter.Settings.ModifierPolicy = source.ModifierPolicy;
        Filter.Settings.IgnoreInjected = source.IgnoreInjected;
        Filter.Settings.VariantMode = source.VariantMode;
        Filter.Settings.SilenceUnmapped = source.SilenceUnmapped;
        Filter.Settings.SilentGroups = [.. source.SilentGroups];
        Filter.Settings.SilentKeys = [.. source.SilentKeys];
        Filter.Settings.HoldSustain = source.HoldSustain;
        Filter.Settings.ReleaseMs = source.ReleaseMs;
        Filter.Settings.ForceSampleKey = source.ForceSampleKey;
    }
}
