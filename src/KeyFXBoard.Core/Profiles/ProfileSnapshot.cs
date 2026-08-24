using KeyFXBoard.Core.Audio;
using KeyFXBoard.Core.Filtering;
using KeyFXBoard.Core.Keys;
using KeyFXBoard.Core.Packs;

namespace KeyFXBoard.Core.Profiles;

public sealed class OverlayRuntime
{
    public required HashSet<int> VirtualKeys { get; init; }
    public required PackRuntime Pack { get; init; }
}

public sealed class ProfileSnapshot
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required PackRuntime Primary { get; init; }
    public required IReadOnlyList<OverlayRuntime> Overlays { get; init; }
    public required FilterSettings Filter { get; init; }
    public required float Volume { get; init; }
    public required float VelocityRandom { get; init; }
    public required int Polyphony { get; init; }
    public required bool Silent { get; init; }
    public required FxGraph Fx { get; init; }

    public IEnumerable<string> ResidentPackNames
    {
        get
        {
            yield return Primary.Name;
            foreach (var overlay in Overlays)
            {
                yield return overlay.Pack.Name;
            }
        }
    }

    public SampleBuffer? Resolve(in KeyEvent e, int octaveShift = 0)
    {
        foreach (var overlay in Overlays)
        {
            if (overlay.VirtualKeys.Contains(e.Key.VirtualKey))
            {
                return overlay.Pack.Resolve(
                    e,
                    Filter.VariantMode,
                    overlay.Pack.IsChromatic ? octaveShift : 0,
                    overlay.Pack.IsChromatic ? null : Filter.ForceSampleKey);
            }
        }

        return Primary.Resolve(e, Filter.VariantMode, octaveShift, Filter.ForceSampleKey);
    }

    public bool MapsVirtualKey(int virtualKey)
    {
        if (!string.IsNullOrWhiteSpace(Filter.ForceSampleKey) && !Primary.IsChromatic)
        {
            return true;
        }

        foreach (var overlay in Overlays)
        {
            if (overlay.VirtualKeys.Contains(virtualKey))
            {
                return true;
            }
        }

        return Primary.MapsVirtualKey(virtualKey);
    }
}
