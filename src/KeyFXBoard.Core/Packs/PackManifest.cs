namespace KeyFXBoard.Core.Packs;

public sealed class PackManifest
{
    public int SchemaVersion { get; set; }
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Version { get; set; } = "1.0.0";
    public string Author { get; set; } = "";
    public string License { get; set; } = "";
    public string? Homepage { get; set; }
    public string? Description { get; set; }
    public string? Icon { get; set; }
    public string? Preview { get; set; }
    public PackDefaults? Defaults { get; set; }
    public Dictionary<string, PackKeySamples>? Keys { get; set; }
    public PackKeySamples? Fallback { get; set; }
    public Dictionary<string, string>? Notes { get; set; }
    public Dictionary<string, string>? KeyNotes { get; set; }
    public string? OctaveDown { get; set; }
    public string? OctaveUp { get; set; }
    public List<string>? OctaveReset { get; set; }

    public IEnumerable<string> EnumerateSamplePaths()
    {
        if (!string.IsNullOrWhiteSpace(Preview))
        {
            yield return Preview;
        }

        if (!string.IsNullOrWhiteSpace(Icon))
        {
            yield return Icon;
        }

        foreach (var path in Fallback?.EnumeratePaths() ?? [])
        {
            yield return path;
        }

        if (Notes is not null)
        {
            foreach (var path in Notes.Values)
            {
                yield return path;
            }
        }

        if (Keys is null)
        {
            yield break;
        }

        foreach (var samples in Keys.Values)
        {
            foreach (var path in samples.EnumeratePaths())
            {
                yield return path;
            }
        }
    }
}

public sealed class PackDefaults
{
    public string? PlayOn { get; set; }
    public string? VariantMode { get; set; }
}

public sealed class PackKeySamples
{
    public List<string>? Down { get; set; }
    public List<string>? Up { get; set; }

    public IEnumerable<string> EnumeratePaths()
    {
        foreach (var path in Down ?? [])
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                yield return path;
            }
        }

        foreach (var path in Up ?? [])
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                yield return path;
            }
        }
    }
}
