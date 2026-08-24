namespace KeyFXBoard.Core.Packs;

public static class PackManifestValidator
{
    public static void Validate(PackManifest manifest, string packRoot)
    {
        if (manifest.SchemaVersion != 1)
        {
            throw new PackException(
                manifest.SchemaVersion > 1
                    ? "This pack needs a newer Key FX Board."
                    : "Pack manifest schemaVersion must be 1.");
        }

        if (!PackPathRules.PackIdPattern.IsMatch(manifest.Id ?? ""))
        {
            throw new PackException("Pack id must be 2–64 characters of a–z, 0–9, or hyphen.");
        }

        if (string.IsNullOrWhiteSpace(manifest.Name) || manifest.Name.Length > 80)
        {
            throw new PackException("Pack name must be 1–80 characters.");
        }

        if (string.IsNullOrWhiteSpace(manifest.Version))
        {
            throw new PackException("Pack version is missing.");
        }

        var hasSamples = false;
        foreach (var relative in manifest.EnumerateSamplePaths())
        {
            var full = PackPathRules.ResolveUnder(packRoot, relative);
            if (!File.Exists(full))
            {
                throw new PackException($"Missing file: {relative}");
            }

            var isWav = relative.EndsWith(".wav", StringComparison.OrdinalIgnoreCase);
            if (isWav)
            {
                hasSamples = true;
                var size = new FileInfo(full).Length;
                if (size > PackPathRules.MaxSampleBytes)
                {
                    throw new PackException($"WAV too large (over 8 MB): {relative}");
                }
            }
        }

        if (!hasSamples)
        {
            throw new PackException("Pack has no WAV samples.");
        }
    }
}
