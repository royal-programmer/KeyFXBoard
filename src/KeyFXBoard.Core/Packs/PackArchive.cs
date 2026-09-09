using System.IO.Compression;
using System.Text.Json;

namespace KeyFXBoard.Core.Packs;

public static class PackArchive
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static PackManifest ReadManifest(string packRoot)
    {
        var path = Path.Combine(packRoot, "manifest.json");
        if (!File.Exists(path))
        {
            throw new PackException("manifest.json is missing.");
        }

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<PackManifest>(json, JsonOptions)
                   ?? throw new PackException("manifest.json is empty.");
        }
        catch (JsonException)
        {
            throw new PackException("manifest.json is not valid JSON.");
        }
    }

    public static void WriteManifest(string packRoot, PackManifest manifest)
    {
        Directory.CreateDirectory(packRoot);
        var json = JsonSerializer.Serialize(manifest, JsonOptions);
        File.WriteAllText(Path.Combine(packRoot, "manifest.json"), json);
    }

    public static void Extract(string packFile, string destination)
    {
        var info = new FileInfo(packFile);
        if (!info.Exists)
        {
            throw new PackException("Pack file was not found.");
        }

        if (info.Length > PackPathRules.MaxPackArchiveBytes)
        {
            throw new PackException(
                $"Pack is too large ({info.Length / (1024 * 1024)} MB). Limit is {PackPathRules.MaxPackArchiveBytes / (1024 * 1024)} MB.");
        }

        Directory.CreateDirectory(destination);
        using var zip = ZipFile.OpenRead(packFile);
        if (zip.Entries.Count > PackPathRules.MaxPackEntries)
        {
            throw new PackException($"Pack has too many files (limit {PackPathRules.MaxPackEntries}).");
        }

        long uncompressed = 0;
        foreach (var entry in zip.Entries)
        {
            var full = entry.FullName.Replace('\\', '/');
            if (full.Split('/', StringSplitOptions.RemoveEmptyEntries).Any(part => part is ".."))
            {
                throw new PackException($"Unsafe path in pack: {entry.FullName}");
            }

            if (entry.Length < 0)
            {
                throw new PackException("Pack entry has an invalid size.");
            }

            uncompressed += entry.Length;
            if (uncompressed > PackPathRules.MaxPackUncompressedBytes)
            {
                throw new PackException(
                    $"Unpacked pack would be too large (limit {PackPathRules.MaxPackUncompressedBytes / (1024 * 1024)} MB).");
            }
        }

        var prefix = DetectSingleRootPrefix(zip);

        foreach (var entry in zip.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name) && entry.FullName.EndsWith('/'))
            {
                continue;
            }

            var relative = entry.FullName.Replace('\\', '/');
            if (prefix is not null)
            {
                if (!relative.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                relative = relative[prefix.Length..];
            }

            if (string.IsNullOrWhiteSpace(relative) || relative.EndsWith('/'))
            {
                continue;
            }

            var dest = PackPathRules.ResolveUnder(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            entry.ExtractToFile(dest, overwrite: true);
        }
    }

    public static void ZipFolder(string folder, string packFile)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(packFile)!);
        if (File.Exists(packFile))
        {
            File.Delete(packFile);
        }

        ZipFile.CreateFromDirectory(folder, packFile, CompressionLevel.SmallestSize, includeBaseDirectory: false);
    }

    private static string? DetectSingleRootPrefix(ZipArchive zip)
    {
        var relatives = zip.Entries
            .Select(e => e.FullName.Replace('\\', '/'))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToList();

        if (relatives.Count == 0)
        {
            return null;
        }

        var roots = relatives
            .Select(name => name.Split('/', 2)[0])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (roots.Count != 1)
        {
            return null;
        }

        var root = roots[0];
        if (root is "." or "..")
        {
            return null;
        }

        var hasNested = relatives.Any(name =>
            name.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase));
        var hasRootFile = relatives.Any(name =>
            name.Equals(root, StringComparison.OrdinalIgnoreCase));

        return hasNested && !hasRootFile ? root + "/" : null;
    }
}
