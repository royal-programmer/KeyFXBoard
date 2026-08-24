using KeyFXBoard.Core.Audio;
using KeyFXBoard.Core.Filtering;
using KeyFXBoard.Core.Keys;
using KeyFXBoard.Core.Packs;

namespace KeyFXBoard.Core.Profiles;

public static class ProfileCompiler
{
    public static ProfileSnapshot Compile(
        ProfileDocument doc,
        Func<string, PackRuntime?> loadPack,
        PackRuntime fallback,
        out string? warning)
    {
        warning = null;
        var primary = loadPack(doc.PrimaryPackId);
        if (primary is null)
        {
            warning = $"Primary pack “{doc.PrimaryPackId}” is missing. Using Factory Click.";
            primary = fallback;
        }

        var overlays = new List<OverlayRuntime>();
        foreach (var overlay in doc.Overlays)
        {
            var pack = loadPack(overlay.PackId);
            if (pack is null)
            {
                warning = Concat(warning, $"Overlay pack “{overlay.PackId}” is not installed (left on disk requirement only).");
                continue;
            }

            var keys = new HashSet<int>();
            foreach (var name in overlay.Keys)
            {
                if (KeyCatalog.TryGetVirtualKey(name, out var vk))
                {
                    keys.Add(vk);
                }
            }

            if (keys.Count > 0)
            {
                overlays.Add(new OverlayRuntime { VirtualKeys = keys, Pack = pack });
            }
        }

        var filter = new FilterSettings
        {
            Repeat = doc.Behavior.Repeat,
            RepeatRateLimitHz = doc.Behavior.RepeatRateLimitHz,
            PlayOn = doc.Behavior.PlayOn,
            ModifierPolicy = doc.Behavior.ModifierPolicy,
            IgnoreInjected = doc.Behavior.IgnoreInjected,
            VariantMode = doc.Behavior.VariantMode,
            SilenceUnmapped = doc.Behavior.SilenceUnmapped,
            SilentGroups = [.. doc.Behavior.SilentGroups],
            SilentKeys = [.. doc.Behavior.SilentKeys],
            HoldSustain = doc.Behavior.HoldSustain,
            ReleaseMs = doc.Behavior.ReleaseMs,
            ForceSampleKey = doc.Behavior.ForceSampleKey
        };

        return new ProfileSnapshot
        {
            Id = doc.Id,
            Name = doc.Name,
            Primary = primary,
            Overlays = overlays,
            Filter = filter,
            Volume = Math.Clamp(doc.Output.MasterVolume, 0, 1),
            VelocityRandom = Math.Clamp(doc.Behavior.VelocityRandom, 0, 0.5f),
            Polyphony = Math.Clamp(doc.Behavior.Polyphony, 1, 64),
            Silent = doc.Silent,
            Fx = FxGraph.Create(doc.Fx)
        };
    }

    private static string Concat(string? current, string next) =>
        string.IsNullOrWhiteSpace(current) ? next : current + " " + next;
}
