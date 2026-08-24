using KeyFXBoard.Core.Audio;
using KeyFXBoard.Core.Keys;

namespace KeyFXBoard.Core.Packs;

public delegate SampleBuffer DecodeSample(string path, string id);

public static class PackLoader
{
    public static PackRuntime Load(string packDirectory, DecodeSample decode)
    {
        var manifest = PackArchive.ReadManifest(packDirectory);
        PackManifestValidator.Validate(manifest, packDirectory);

        long decodedBytes = 0;
        var cache = new Dictionary<string, SampleBuffer>(StringComparer.OrdinalIgnoreCase);

        SampleBuffer LoadPath(string relative)
        {
            if (cache.TryGetValue(relative, out var existing))
            {
                return existing;
            }

            var full = PackPathRules.ResolveUnder(packDirectory, relative);
            var buffer = decode(full, $"{manifest.Id}:{relative}");
            decodedBytes += buffer.Data.Length * sizeof(float);
            if (decodedBytes > PackPathRules.MaxDecodedBytes)
            {
                throw new PackException("Pack is too large in memory (over 64 MB).");
            }

            cache[relative] = buffer;
            return buffer;
        }

        SampleBuffer[] LoadList(IEnumerable<string>? paths) =>
            (paths ?? []).Where(p => !string.IsNullOrWhiteSpace(p)).Select(LoadPath).ToArray();

        var notes = new Dictionary<string, SampleBuffer>(StringComparer.OrdinalIgnoreCase);
        if (manifest.Notes is not null)
        {
            foreach (var (note, path) in manifest.Notes)
            {
                notes[note] = LoadPath(path);
            }
        }

        var keyNotes = new Dictionary<int, string>();
        if (manifest.KeyNotes is not null)
        {
            foreach (var (name, note) in manifest.KeyNotes)
            {
                if (KeyCatalog.TryGetVirtualKey(name, out var vk))
                {
                    keyNotes[vk] = note;
                }
            }
        }

        var octaveDown = 0;
        var octaveUp = 0;
        var octaveReset = new List<int>();
        if (manifest.OctaveDown is not null)
        {
            KeyCatalog.TryGetVirtualKey(manifest.OctaveDown, out octaveDown);
        }

        if (manifest.OctaveUp is not null)
        {
            KeyCatalog.TryGetVirtualKey(manifest.OctaveUp, out octaveUp);
        }

        foreach (var name in manifest.OctaveReset ?? [])
        {
            if (KeyCatalog.TryGetVirtualKey(name, out var vk))
            {
                octaveReset.Add(vk);
            }
        }

        var down = new Dictionary<int, SampleBuffer[]>();
        var up = new Dictionary<int, SampleBuffer[]>();
        if (manifest.Keys is not null)
        {
            foreach (var (name, samples) in manifest.Keys)
            {
                if (!KeyCatalog.TryGetVirtualKey(name, out var vk))
                {
                    continue;
                }

                var downList = LoadList(samples.Down);
                if (downList.Length > 0)
                {
                    down[vk] = downList;
                }

                var upList = LoadList(samples.Up);
                if (upList.Length > 0)
                {
                    up[vk] = upList;
                }
            }
        }

        SampleBuffer? preview = null;
        if (!string.IsNullOrWhiteSpace(manifest.Preview))
        {
            preview = LoadPath(manifest.Preview);
        }

        var fallbackDown = LoadList(manifest.Fallback?.Down);
        var fallbackUp = LoadList(manifest.Fallback?.Up);
        if (fallbackDown.Length == 0 && notes.Count == 0 && down.Count == 0 && cache.Count > 0)
        {
            fallbackDown = [cache.Values.First()];
        }

        return new PackRuntime(
            manifest.Id,
            manifest.Name,
            fallbackDown,
            fallbackUp,
            preview,
            down,
            up,
            keyNotes,
            notes,
            octaveDown,
            octaveUp,
            octaveReset);
    }
}
