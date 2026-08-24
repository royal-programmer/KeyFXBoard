using KeyFXBoard.Core.Keys;

namespace KeyFXBoard.Core.Packs;

/// <summary>Piano map: Z-row C3, A-row C4 (home), Q-row C5. Page Down/Up shift the whole span; Home/End reset.</summary>
public static class PianoLayout
{
    public const string MapId = "piano-v2";
    public const string OctaveDownKey = "PageDown";
    public const string OctaveUpKey = "PageUp";
    public const int MinMidi = 36; // C2
    public const int MaxMidi = 84; // C6

    public static readonly string[] OctaveResetKeys = ["Home", "End"];

    /// <summary>Z-row C3–B3, A-row C4–B4, Q-row C5–B5. Home-octave sharps on 2 3 5 6 7; C5 sharps on 9 0 - = [.</summary>
    public static readonly IReadOnlyDictionary<string, string> KeyNotes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["KeyZ"] = "C3", ["KeyX"] = "D3", ["KeyC"] = "E3", ["KeyV"] = "F3",
        ["KeyB"] = "G3", ["KeyN"] = "A3", ["KeyM"] = "B3",

        ["KeyA"] = "C4", ["D2"] = "C#4", ["KeyS"] = "D4", ["D3"] = "D#4", ["KeyD"] = "E4",
        ["KeyF"] = "F4", ["D5"] = "F#4", ["KeyG"] = "G4", ["D6"] = "G#4", ["KeyH"] = "A4",
        ["D7"] = "A#4", ["KeyJ"] = "B4",

        ["KeyQ"] = "C5", ["D9"] = "C#5", ["KeyW"] = "D5", ["D0"] = "D#5", ["KeyE"] = "E5",
        ["KeyR"] = "F5", ["OemMinus"] = "F#5", ["KeyT"] = "G5", ["OemPlus"] = "G#5",
        ["KeyY"] = "A5", ["OemLeftBracket"] = "A#5", ["KeyU"] = "B5"
    };

    public static string OctaveLabel(int shift)
    {
        var steps = shift / 12;
        if (steps == 0)
        {
            return "Octave 0 (A-row = C4)";
        }

        var sign = steps > 0 ? "+" : "";
        return $"Octave {sign}{steps} (A-row = {Transpose("C4", shift)})";
    }

    public static int MidiOf(string note)
    {
        var n = note.Trim();
        var sharp = n.Contains('#');
        var letter = n[0];
        var octave = n[^1] - '0';
        var semis = letter switch
        {
            'C' => 0,
            'D' => 2,
            'E' => 4,
            'F' => 5,
            'G' => 7,
            'A' => 9,
            'B' => 11,
            _ => throw new ArgumentOutOfRangeException(nameof(note), note)
        };
        if (sharp)
        {
            semis++;
        }

        return (octave + 1) * 12 + semis;
    }

    public static string NameOfMidi(int midi)
    {
        var names = new[] { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
        var n = ((midi % 12) + 12) % 12;
        var octave = midi / 12 - 1;
        return names[n] + octave;
    }

    public static string Transpose(string note, int semitones)
    {
        var midi = Math.Clamp(MidiOf(note) + semitones, MinMidi, MaxMidi);
        return NameOfMidi(midi);
    }

    public static IEnumerable<string> ChromaticNotes()
    {
        for (var midi = MinMidi; midi <= MaxMidi; midi++)
        {
            yield return NameOfMidi(midi);
        }
    }

    public static string SampleFileName(string note) => note.Replace("#", "s", StringComparison.Ordinal) + ".wav";

    public static bool TryGetNote(int virtualKey, out string note)
    {
        var name = KeyCatalog.NameOf(new KeyId(virtualKey));
        return KeyNotes.TryGetValue(name, out note!);
    }

    public static bool IsBlack(int midi)
    {
        var pc = ((midi % 12) + 12) % 12;
        return pc is 1 or 3 or 6 or 8 or 10;
    }

    public static string KeyboardLabel(string keyName) => keyName switch
    {
        "OemComma" => ",",
        "OemPeriod" => ".",
        "OemLeftBracket" => "[",
        "OemRightBracket" => "]",
        "OemMinus" => "-",
        "OemPlus" => "=",
        "PageDown" => "PgDn",
        "PageUp" => "PgUp",
        "OemSemicolon" => ";",
        "OemQuotes" => "'",
        "OemQuestion" => "/",
        "OemBackslash" => "\\",
        "OemTilde" => "`",
        _ when keyName.StartsWith("Key", StringComparison.OrdinalIgnoreCase) && keyName.Length == 4
            => keyName[^1].ToString().ToUpperInvariant(),
        _ when keyName.Length == 2 && keyName[0] is 'D' or 'd' && char.IsDigit(keyName[1])
            => keyName[1].ToString(),
        _ => keyName
    };

    public static IReadOnlyList<PianoKeyBinding> MapBindings(int octaveShift = 0)
    {
        var best = new Dictionary<int, PianoKeyBinding>();
        foreach (var (keyName, note) in KeyNotes)
        {
            var sounding = Transpose(note, octaveShift);
            var midi = MidiOf(sounding);
            if (midi < MinMidi || midi > MaxMidi)
            {
                continue;
            }

            var candidate = new PianoKeyBinding(midi, sounding, keyName, KeyboardLabel(keyName), octaveShift);
            if (!best.TryGetValue(midi, out var existing) ||
                Math.Abs(MidiOf(note) - 60) < Math.Abs(MidiOf(KeyNotes[existing.KeyName]) - 60))
            {
                best[midi] = candidate;
            }
        }

        return best.Values.OrderBy(b => b.Midi).ToList();
    }

    public static bool TryGetBinding(string note, out PianoKeyBinding binding, int octaveShift = 0) =>
        TryGetBinding(MidiOf(note), out binding, octaveShift);

    public static bool TryGetBinding(int midi, out PianoKeyBinding binding, int octaveShift = 0)
    {
        foreach (var item in MapBindings(octaveShift))
        {
            if (item.Midi == midi)
            {
                binding = item;
                return true;
            }
        }

        binding = default;
        return false;
    }
}

public readonly record struct PianoKeyBinding(int Midi, string Note, string KeyName, string KeyLabel, int OctaveShift);
