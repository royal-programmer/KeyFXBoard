namespace KeyFXBoard.Core.Filtering;

public sealed class FilterSettings
{
    public RepeatMode Repeat { get; set; } = RepeatMode.Off;
    public float RepeatRateLimitHz { get; set; } = 8f;
    public PlayOn PlayOn { get; set; } = PlayOn.Down;
    public ModifierPolicy ModifierPolicy { get; set; } = ModifierPolicy.Ignore;
    public bool IgnoreInjected { get; set; } = true;
    public VariantMode VariantMode { get; set; } = VariantMode.Random;
    public bool SilenceUnmapped { get; set; }
    public List<string> SilentGroups { get; set; } = [];
    public List<string> SilentKeys { get; set; } = [];
    public bool HoldSustain { get; set; }
    public float ReleaseMs { get; set; } = 280;
    public string? ForceSampleKey { get; set; }
}
