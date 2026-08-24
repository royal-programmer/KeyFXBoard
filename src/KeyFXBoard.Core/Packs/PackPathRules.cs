using System.Text.RegularExpressions;

namespace KeyFXBoard.Core.Packs;

public static class PackPathRules
{
    public static readonly Regex PackIdPattern = new("^[a-z0-9-]{2,64}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    public const long MaxSampleBytes = 8L * 1024 * 1024;
    public const long MaxDecodedBytes = 64L * 1024 * 1024;

    private static readonly HashSet<string> SeededIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "piano-classic",
        CustomSampleLibrary.PackId
    };

    public static readonly string[] RetiredPackIds =
    [
        "guns-arcade",
        "bombs-arcade"
    ];

    public static bool IsFactoryId(string id) =>
        id.StartsWith("factory-", StringComparison.Ordinal) || SeededIds.Contains(id);

    public static bool IsHiddenLibraryPack(string id) =>
        id.Equals("piano-classic", StringComparison.OrdinalIgnoreCase) ||
        RetiredPackIds.Contains(id, StringComparer.OrdinalIgnoreCase);

    public static void EnsureSafeRelativePath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new PackException("A sample path is empty.");
        }

        var normalized = relativePath.Replace('\\', '/');
        if (Path.IsPathRooted(normalized) ||
            normalized.Contains("://", StringComparison.Ordinal) ||
            normalized.Split('/', StringSplitOptions.RemoveEmptyEntries).Any(part => part is ".."))
        {
            throw new PackException($"Unsafe path in pack: {relativePath}");
        }
    }

    public static string ResolveUnder(string root, string relativePath)
    {
        EnsureSafeRelativePath(relativePath);
        var rootFull = Path.GetFullPath(root);
        if (!rootFull.EndsWith(Path.DirectorySeparatorChar))
        {
            rootFull += Path.DirectorySeparatorChar;
        }

        var dest = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!dest.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
        {
            throw new PackException($"Unsafe path in pack: {relativePath}");
        }

        return dest;
    }
}
