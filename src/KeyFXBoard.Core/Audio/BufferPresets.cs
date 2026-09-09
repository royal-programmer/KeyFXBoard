namespace KeyFXBoard.Core.Audio;

public static class BufferPresets
{
    public const string LowLatency = "LowLatency";
    public const string Stable = "Stable";
    public const string Safe = "Safe";

    public static readonly (string Id, string Label, int LatencyMs)[] All =
    [
        (LowLatency, "Low latency", 10),
        (Stable, "Stable (recommended)", 20),
        (Safe, "Safe", 40)
    ];

    public static int LatencyMs(string? preset)
    {
        foreach (var item in All)
        {
            if (item.Id.Equals(preset, StringComparison.OrdinalIgnoreCase))
            {
                return item.LatencyMs;
            }
        }

        return 20;
    }

    public static string Normalize(string? preset)
    {
        foreach (var item in All)
        {
            if (item.Id.Equals(preset, StringComparison.OrdinalIgnoreCase))
            {
                return item.Id;
            }
        }

        return Stable;
    }
}
