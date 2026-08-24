using KeyFXBoard.Core.Abstractions;
using KeyFXBoard.Core.Audio;

namespace KeyFXBoard.Core.Packs;

public static class FactoryPackSeeder
{
    public const string FactoryId = "factory-click";

    public static void Ensure(IAppPaths paths)
    {
        Directory.CreateDirectory(paths.PacksDirectory);
        Directory.CreateDirectory(paths.ExamplesDirectory);

        var factoryDir = Path.Combine(paths.PacksDirectory, FactoryId);
        if (!File.Exists(Path.Combine(factoryDir, "manifest.json")))
        {
            WriteFolder(
                factoryDir,
                new PackManifest
                {
                    SchemaVersion = 1,
                    Id = FactoryId,
                    Name = "Factory Click",
                    Version = "1.0.0",
                    Author = "Key FX Board",
                    License = "CC0-1.0",
                    Description = "Original placeholder click for Key FX Board.",
                    Preview = "preview.wav",
                    Fallback = new PackKeySamples { Down = ["samples/default_down.wav"], Up = [] }
                },
                ClickSampleFactory.Create(FactoryId));
        }

        ThemePackSeeder.Ensure(paths);

        var example = Path.Combine(paths.ExamplesDirectory, "soft-tick-1.0.0.kfxpack");
        if (!File.Exists(example))
        {
            var temp = Path.Combine(Path.GetTempPath(), "KeyFXBoard-soft-tick-" + Guid.NewGuid().ToString("N"));
            try
            {
                WriteFolder(
                    temp,
                    new PackManifest
                    {
                        SchemaVersion = 1,
                        Id = "soft-tick",
                        Name = "Soft Tick",
                        Version = "1.0.0",
                        Author = "Key FX Board",
                        License = "CC0-1.0",
                        Description = "Example pack you can install, preview, and uninstall.",
                        Preview = "preview.wav",
                        Fallback = new PackKeySamples { Down = ["samples/default_down.wav"], Up = [] }
                    },
                    ClickSampleFactory.CreateSoft("soft-tick"));
                PackArchive.ZipFolder(temp, example);
            }
            finally
            {
                if (Directory.Exists(temp))
                {
                    Directory.Delete(temp, recursive: true);
                }
            }
        }

        ThemePackSeeder.Ensure(paths);
    }

    private static void WriteFolder(string directory, PackManifest manifest, SampleBuffer sample)
    {
        Directory.CreateDirectory(Path.Combine(directory, "samples"));
        WavWriter.Write(Path.Combine(directory, "samples", "default_down.wav"), sample);
        WavWriter.Write(Path.Combine(directory, "preview.wav"), sample);
        PackArchive.WriteManifest(directory, manifest);
    }
}
