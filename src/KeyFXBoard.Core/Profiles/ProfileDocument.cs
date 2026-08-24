using KeyFXBoard.Core.Filtering;

namespace KeyFXBoard.Core.Profiles;

public sealed class ProfileDocument
{
    public int SchemaVersion { get; set; } = 1;
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsFactory { get; set; }
    public string? BasedOn { get; set; }
    public string VirtualRoomId { get; set; } = VirtualRoomCatalog.Default;
    public bool Silent { get; set; }
    public bool FxLocked { get; set; }
    public string PrimaryPackId { get; set; } = "factory-click";
    public List<ProfileOverlay> Overlays { get; set; } = [];
    public ProfileBehavior Behavior { get; set; } = new();
    public ProfileOutput Output { get; set; } = new();
    public FxSettings Fx { get; set; } = new();
}

public sealed class ProfileOverlay
{
    public string PackId { get; set; } = "";
    public List<string> Keys { get; set; } = [];
}

public sealed class ProfileBehavior
{
    public RepeatMode Repeat { get; set; } = RepeatMode.Off;
    public float RepeatRateLimitHz { get; set; } = 8f;
    public PlayOn PlayOn { get; set; } = PlayOn.Down;
    public ModifierPolicy ModifierPolicy { get; set; } = ModifierPolicy.Ignore;
    public bool IgnoreInjected { get; set; } = true;
    public int Polyphony { get; set; } = 24;
    public float VelocityRandom { get; set; } = 0.12f;
    public VariantMode VariantMode { get; set; } = VariantMode.Random;
    public bool SilenceUnmapped { get; set; }
    public List<string> SilentGroups { get; set; } = [];
    public List<string> SilentKeys { get; set; } = [];
    public bool HoldSustain { get; set; }
    public float ReleaseMs { get; set; } = 280;
    public string? ForceSampleKey { get; set; }
}

public sealed class ProfileOutput
{
    public string DeviceId { get; set; } = "default";
    public float MasterVolume { get; set; } = 0.7f;
}

public sealed class FxSettings
{
    public float InputGainDb { get; set; }
    public EqSettings Eq { get; set; } = new();
    public CompressorSettings Compressor { get; set; } = new();
    public SaturationSettings Saturation { get; set; } = new();
    public DynamicBassSettings DynamicBass { get; set; } = new();
    public ChorusSettings Chorus { get; set; } = new();
    public FlangerSettings Flanger { get; set; } = new();
    public PhaserSettings Phaser { get; set; } = new();
    public DelaySettings Delay { get; set; } = new();
    public ConvolverSettings Convolver { get; set; } = new();
    public ReverbSettings Reverb { get; set; } = new();
    public WidthSettings Width { get; set; } = new();
    public CrossfeedSettings Crossfeed { get; set; } = new();
    public LimiterSettings Limiter { get; set; } = new();
}

public sealed class DynamicBassSettings
{
    public bool Enabled { get; set; }
    public float Mix { get; set; } = 0.5f;
}

public sealed class ChorusSettings
{
    public bool Enabled { get; set; }
    public float Mix { get; set; } = 0.35f;
    public float RateHz { get; set; } = 0.8f;
    public float Depth { get; set; } = 0.5f;
}

public sealed class FlangerSettings
{
    public bool Enabled { get; set; }
    public float Mix { get; set; } = 0.3f;
    public float RateHz { get; set; } = 0.35f;
    public float Depth { get; set; } = 0.55f;
    public float Feedback { get; set; } = 0.35f;
}

public sealed class PhaserSettings
{
    public bool Enabled { get; set; }
    public float Mix { get; set; } = 0.35f;
    public float RateHz { get; set; } = 0.4f;
    public float Depth { get; set; } = 0.6f;
}

public sealed class ConvolverSettings
{
    public bool Enabled { get; set; }
    public float Mix { get; set; } = 0.25f;
    public string Ir { get; set; } = "Short";
}

public sealed class WidthSettings
{
    public bool Enabled { get; set; }
    public float Mix { get; set; } = 0.5f;
}

public sealed class CrossfeedSettings
{
    public bool Enabled { get; set; }
    public float Mix { get; set; } = 0.4f;
}

public sealed class EqSettings
{
    public bool Enabled { get; set; }
    public float BassDb { get; set; }
    public float AirDb { get; set; }
}

public sealed class CompressorSettings
{
    public bool Enabled { get; set; }
    public float ThresholdDb { get; set; } = -18;
    public float Ratio { get; set; } = 2.5f;
    public float AttackMs { get; set; } = 8;
    public float ReleaseMs { get; set; } = 80;
    public float MakeupDb { get; set; }
}

public sealed class SaturationSettings
{
    public bool Enabled { get; set; }
    public string Style { get; set; } = "Tape";
    public float Drive { get; set; } = 0.2f;
    public float Mix { get; set; } = 0.3f;
}

public sealed class DelaySettings
{
    public bool Enabled { get; set; }
    public float TimeMs { get; set; } = 180;
    public float Feedback { get; set; } = 0.25f;
    public float Mix { get; set; }
}

public sealed class ReverbSettings
{
    public bool Enabled { get; set; }
    public float Decay { get; set; } = 0.4f;
    public float Damping { get; set; } = 0.4f;
    public float Mix { get; set; }
}

public sealed class LimiterSettings
{
    public float CeilingDb { get; set; } = -0.3f;
}
