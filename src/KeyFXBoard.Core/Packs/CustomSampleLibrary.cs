using KeyFXBoard.Core.Abstractions;
using KeyFXBoard.Core.Audio;

namespace KeyFXBoard.Core.Packs;

public static class CustomSampleLibrary
{
    public const string PackId = "custom-sample";
    public const string PackName = "Custom sample";

    public static readonly string[] Extensions =
    [
        ".wav", ".mp3", ".wma", ".m4a", ".aac", ".flac", ".ogg"
    ];

    public static string DirectoryFor(IAppPaths paths) =>
        Path.Combine(paths.Root, "custom-samples");

    public static void Ensure(IAppPaths paths)
    {
        var dir = DirectoryFor(paths);
        Directory.CreateDirectory(dir);
        var readme = Path.Combine(dir, "README.txt");
        if (!File.Exists(readme))
        {
            File.WriteAllText(readme,
                "Drop audio files here (WAV, MP3, and other formats Windows can decode).\r\n" +
                "Pick Custom sample on Profiles, then choose the file in Custom sound.\r\n" +
                "Primary = every key. Overlay = Enter, Escape, and Space.\r\n");
        }
    }

    public static IReadOnlyList<string> ListFiles(IAppPaths paths)
    {
        var dir = DirectoryFor(paths);
        if (!Directory.Exists(dir))
        {
            return [];
        }

        return Directory.GetFiles(dir)
            .Where(path => Extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string? ResolveArmed(IAppPaths paths, string? armedFileName)
    {
        if (string.IsNullOrWhiteSpace(armedFileName))
        {
            return ListFiles(paths).FirstOrDefault();
        }

        var dest = Path.GetFullPath(Path.Combine(DirectoryFor(paths), Path.GetFileName(armedFileName)));
        var root = Path.GetFullPath(DirectoryFor(paths));
        if (!dest.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
            !dest.Equals(root, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return File.Exists(dest) ? dest : ListFiles(paths).FirstOrDefault();
    }

    public static PackRuntime CreatePack(string? filePath, SampleBuffer? sample)
    {
        if (sample is null)
        {
            return PackRuntime.SingleSample(PackId, PackName, Silent());
        }

        var name = filePath is null ? PackName : $"{PackName} ({Path.GetFileName(filePath)})";
        return PackRuntime.SingleSample(PackId, name, sample);
    }

    public static InstalledPack CatalogEntry(string? armedFileName) =>
        new()
        {
            Id = PackId,
            Name = PackName,
            Version = "1.0.0",
            Author = "You",
            License = "Your files",
            Directory = "",
            Description = string.IsNullOrWhiteSpace(armedFileName)
                ? "One sound from your custom folder. Primary = every key. Overlay = Enter, Escape, and Space."
                : $"Armed: {Path.GetFileName(armedFileName)}. Primary = every key. Overlay = Enter, Escape, and Space."
        };

    public static SampleBuffer Silent() =>
        new($"{PackId}:silent", new float[SampleBuffer.Channels * 8]);
}
