using KeyFXBoard.Core.Audio;
using KeyFXBoard.Core.Filtering;
using KeyFXBoard.Core.Keys;

namespace KeyFXBoard.Core.Packs;

public sealed class PackRuntime
{
    private readonly Dictionary<int, SampleBuffer[]> _down = new();
    private readonly Dictionary<int, SampleBuffer[]> _up = new();
    private readonly SampleBuffer[] _fallbackDown;
    private readonly SampleBuffer[] _fallbackUp;
    private readonly Dictionary<int, string> _keyNotes = new();
    private readonly Dictionary<string, SampleBuffer> _notes = new(StringComparer.OrdinalIgnoreCase);
    private int _cycle;
    private readonly int _octaveDownVk;
    private readonly int _octaveUpVk;
    private readonly HashSet<int> _octaveReset;

    public PackRuntime(
        string packId,
        string name,
        SampleBuffer[] fallbackDown,
        SampleBuffer[] fallbackUp,
        SampleBuffer? preview,
        IReadOnlyDictionary<int, SampleBuffer[]> down,
        IReadOnlyDictionary<int, SampleBuffer[]> up,
        IReadOnlyDictionary<int, string>? keyNotes = null,
        IReadOnlyDictionary<string, SampleBuffer>? notes = null,
        int octaveDownVk = 0,
        int octaveUpVk = 0,
        IEnumerable<int>? octaveReset = null)
    {
        PackId = packId;
        Name = name;
        Preview = preview;
        _fallbackDown = fallbackDown;
        _fallbackUp = fallbackUp;
        _octaveDownVk = octaveDownVk;
        _octaveUpVk = octaveUpVk;
        _octaveReset = octaveReset is null ? [] : [.. octaveReset.Where(v => v != 0)];
        foreach (var pair in down)
        {
            _down[pair.Key] = pair.Value;
        }

        foreach (var pair in up)
        {
            _up[pair.Key] = pair.Value;
        }

        if (keyNotes is not null)
        {
            foreach (var pair in keyNotes)
            {
                _keyNotes[pair.Key] = pair.Value;
            }
        }

        if (notes is not null)
        {
            foreach (var pair in notes)
            {
                _notes[pair.Key] = pair.Value;
            }
        }
    }

    public string PackId { get; }
    public string Name { get; }
    public SampleBuffer? Preview { get; }
    public bool IsChromatic => _keyNotes.Count > 0;

    public bool MapsVirtualKey(int virtualKey)
    {
        if (TryOctaveDelta(virtualKey, out _) || TryOctaveReset(virtualKey))
        {
            return true;
        }

        if (_keyNotes.ContainsKey(virtualKey) || _down.ContainsKey(virtualKey) || _up.ContainsKey(virtualKey))
        {
            return true;
        }

        if (_keyNotes.Count > 0 || _down.Count > 0 || _up.Count > 0)
        {
            return false;
        }

        return _fallbackDown.Length > 0 || _fallbackUp.Length > 0;
    }

    public bool TryOctaveDelta(int virtualKey, out int semitones)
    {
        semitones = 0;
        if (virtualKey != 0 && virtualKey == _octaveDownVk)
        {
            semitones = -12;
            return true;
        }

        if (virtualKey != 0 && virtualKey == _octaveUpVk)
        {
            semitones = 12;
            return true;
        }

        return false;
    }

    public bool TryOctaveReset(int virtualKey) =>
        virtualKey != 0 && _octaveReset.Contains(virtualKey);

    public SampleBuffer? GetNote(string note) =>
        _notes.TryGetValue(note, out var buffer) ? buffer : null;

    public static PackRuntime SingleSample(string packId, string name, SampleBuffer sample) =>
        new(packId, name, [sample], [sample], sample, new Dictionary<int, SampleBuffer[]>(), new Dictionary<int, SampleBuffer[]>());

    public SampleBuffer? Resolve(KeyEvent e, VariantMode variant, int octaveShift = 0, string? forceSampleKey = null)
    {
        if (!string.IsNullOrWhiteSpace(forceSampleKey) &&
            KeyCatalog.TryGetVirtualKey(forceSampleKey, out var forcedVk))
        {
            var forced = Find(_down, forcedVk, _fallbackDown);
            if (forced.Length > 0)
            {
                return Pick(forced, variant);
            }
        }

        if (_keyNotes.TryGetValue(e.Key.VirtualKey, out var note))
        {
            var shifted = PianoLayout.Transpose(note, octaveShift);
            return GetNote(shifted) ?? GetNote(note);
        }

        var list = e.Kind == KeyKind.Up
            ? FindUp(e.Key.VirtualKey)
            : Find(_down, e.Key.VirtualKey, _fallbackDown);

        if (list.Length == 0)
        {
            return null;
        }

        return Pick(list, variant);
    }

    private SampleBuffer[] FindUp(int vk)
    {
        var up = Find(_up, vk, _fallbackUp);
        return up.Length > 0 ? up : Find(_down, vk, _fallbackDown);
    }

    private static SampleBuffer[] Find(Dictionary<int, SampleBuffer[]> map, int vk, SampleBuffer[] fallback) =>
        map.TryGetValue(vk, out var samples) && samples.Length > 0 ? samples : fallback;

    private SampleBuffer Pick(SampleBuffer[] list, VariantMode variant)
    {
        if (list.Length == 1 || variant == VariantMode.First)
        {
            return list[0];
        }

        if (variant == VariantMode.Cycle)
        {
            var index = _cycle++ % list.Length;
            return list[index];
        }

        return list[Random.Shared.Next(list.Length)];
    }
}
