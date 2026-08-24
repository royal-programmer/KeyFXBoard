using KeyFXBoard.Core.Abstractions;
using KeyFXBoard.Core.Audio;

namespace KeyFXBoard.Core.Packs;

public static class ThemePackSeeder
{
    public const string PianoId = "piano-classic";
    public const string ContentVersion = "1.3.0";

    public static void Ensure(IAppPaths paths)
    {
        Directory.CreateDirectory(paths.PacksDirectory);
        Directory.CreateDirectory(paths.ExamplesDirectory);
        CustomSampleLibrary.Ensure(paths);
        EnsurePiano(paths);
        RetireArcade(paths);
    }

    public static void RetireArcade(IAppPaths paths)
    {
        foreach (var id in PackPathRules.RetiredPackIds)
        {
            var dest = Path.Combine(paths.PacksDirectory, id);
            if (Directory.Exists(dest))
            {
                try
                {
                    Directory.Delete(dest, recursive: true);
                }
                catch
                {
                    // Locked files can wait for the next launch.
                }
            }

            DeleteExample(paths, $"{id}-1.0.0.kfxpack");
            DeleteExample(paths, $"{id}-1.1.0.kfxpack");
            DeleteExample(paths, $"{id}-1.2.0.kfxpack");
            DeleteExample(paths, $"{id}-1.3.0.kfxpack");
        }
    }

    private static void EnsurePiano(IAppPaths paths)
    {
        var dest = Path.Combine(paths.PacksDirectory, PianoId);
        if (NeedsWrite(dest))
        {
            WritePiano(dest);
            DeleteExample(paths, "piano-classic-1.0.0.kfxpack");
            DeleteExample(paths, "piano-classic-1.1.0.kfxpack");
            DeleteExample(paths, "piano-classic-1.2.0.kfxpack");
        }
    }

    private static void WritePiano(string directory)
    {
        var samplesDir = Path.Combine(directory, "samples");
        Directory.CreateDirectory(samplesDir);

        var notes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var note in PianoLayout.ChromaticNotes())
        {
            var relative = "samples/" + PianoLayout.SampleFileName(note);
            WavWriter.Write(Path.Combine(directory, relative), InstrumentSampleFactory.Piano(note, $"{PianoId}:{note}"));
            notes[note] = relative;
        }

        WavWriter.Write(Path.Combine(directory, "preview.wav"), InstrumentSampleFactory.Piano("C4", $"{PianoId}:preview"));

        PackArchive.WriteManifest(directory, new PackManifest
        {
            SchemaVersion = 1,
            Id = PianoId,
            Name = "Piano",
            Version = ContentVersion,
            Author = "Key FX Board",
            License = "CC0-1.0",
            Description = "Built-in piano instrument. Start it from Instruments — it is not a user pack.",
            Preview = "preview.wav",
            Notes = notes,
            KeyNotes = new Dictionary<string, string>(PianoLayout.KeyNotes, StringComparer.OrdinalIgnoreCase),
            OctaveDown = PianoLayout.OctaveDownKey,
            OctaveUp = PianoLayout.OctaveUpKey,
            OctaveReset = [.. PianoLayout.OctaveResetKeys]
        });
    }

    private static void DeleteExample(IAppPaths paths, string fileName)
    {
        var path = Path.Combine(paths.ExamplesDirectory, fileName);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static bool NeedsWrite(string directory)
    {
        var manifestPath = Path.Combine(directory, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            return true;
        }

        try
        {
            return !string.Equals(PackArchive.ReadManifest(directory).Version, ContentVersion, StringComparison.OrdinalIgnoreCase);
        }
        catch (PackException)
        {
            return true;
        }
    }
}
