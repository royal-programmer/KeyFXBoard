namespace KeyFXBoard.Core.Profiles;

public static class VirtualRoomCatalog
{
    public const string Default = "default";
    public const string Dry = "dry";
    public const string Small = "small";
    public const string Hall = "hall";
    public const string Surround = "surround";

    public static readonly (string Id, string Name)[] Rooms =
    [
        (Default, "Default"),
        (Dry, "Dry"),
        (Small, "Small"),
        (Hall, "Hall"),
        (Surround, "Surround")
    ];

    public static string MapId(string? id) =>
        Rooms.Any(r => r.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
            ? Rooms.First(r => r.Id.Equals(id, StringComparison.OrdinalIgnoreCase)).Id
            : Default;

    public static FxSettings CreateFx(string? roomId) => MapId(roomId) switch
    {
        Dry => DryRoom(),
        Small => SmallRoom(),
        Hall => HallRoom(),
        Surround => SurroundRoom(),
        _ => DefaultRoom()
    };

    public static void ApplyTo(ProfileDocument profile, string? roomId)
    {
        var id = MapId(roomId);
        profile.VirtualRoomId = id;
        profile.Fx = CreateFx(id);
    }

    private static FxSettings DefaultRoom() => new()
    {
        Limiter = new LimiterSettings()
    };

    private static FxSettings DryRoom() => new()
    {
        Saturation = new SaturationSettings { Enabled = true, Style = "Tape", Drive = 0.12f, Mix = 0.15f },
        Limiter = new LimiterSettings()
    };

    private static FxSettings SmallRoom() => new()
    {
        Eq = new EqSettings { Enabled = true, BassDb = 0.5f, AirDb = 1 },
        Delay = new DelaySettings { Enabled = true, TimeMs = 90, Feedback = 0.12f, Mix = 0.08f },
        Reverb = new ReverbSettings { Enabled = true, Decay = 0.28f, Damping = 0.5f, Mix = 0.18f },
        Convolver = new ConvolverSettings { Enabled = true, Ir = "Short", Mix = 0.12f },
        Limiter = new LimiterSettings()
    };

    private static FxSettings HallRoom() => new()
    {
        InputGainDb = -1,
        Eq = new EqSettings { Enabled = true, BassDb = 1.5f, AirDb = 2 },
        Compressor = new CompressorSettings { Enabled = true, ThresholdDb = -18, Ratio = 2.2f, MakeupDb = 1.5f },
        Delay = new DelaySettings { Enabled = true, TimeMs = 180, Feedback = 0.22f, Mix = 0.14f },
        Convolver = new ConvolverSettings { Enabled = true, Ir = "Medium", Mix = 0.18f },
        Reverb = new ReverbSettings { Enabled = true, Decay = 0.62f, Damping = 0.35f, Mix = 0.38f },
        Limiter = new LimiterSettings()
    };

    private static FxSettings SurroundRoom() => new()
    {
        Eq = new EqSettings { Enabled = true, BassDb = 1, AirDb = 1.5f },
        Delay = new DelaySettings { Enabled = true, TimeMs = 110, Feedback = 0.16f, Mix = 0.1f },
        Reverb = new ReverbSettings { Enabled = true, Decay = 0.42f, Damping = 0.4f, Mix = 0.22f },
        Width = new WidthSettings { Enabled = true, Mix = 0.72f },
        Crossfeed = new CrossfeedSettings { Enabled = true, Mix = 0.55f },
        Chorus = new ChorusSettings { Enabled = true, Mix = 0.18f, RateHz = 0.55f, Depth = 0.35f },
        Limiter = new LimiterSettings()
    };
}
