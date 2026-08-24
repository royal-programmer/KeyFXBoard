namespace KeyFXBoard.Core.Storage;

public sealed class AppSettings
{
    public int SchemaVersion { get; set; } = 1;
    public bool Autostart { get; set; }
    public string Theme { get; set; } = "Dark";
    public bool GlobalMute { get; set; }
    public bool MinimizeToTrayOnClose { get; set; } = true;
    public bool StartMinimized { get; set; } = true;
    public bool FirstRunCompleted { get; set; }
    public float Volume { get; set; } = 0.7f;
    public float VelocityRandom { get; set; } = 0.12f;
    public string BufferPreset { get; set; } = "Stable";
    public bool CheckForUpdates { get; set; } = true;
    public string ActivePackId { get; set; } = "factory-click";
    public string ActiveProfileId { get; set; } = "factory-default";
    public string AudioDeviceId { get; set; } = "default";
    public const float MaxOutputBoostDb = 18f;

    public float OutputBoostDb { get; set; }

    /// <summary>Installed packs hidden from profile pickers. They stay on disk.</summary>
    public List<string> DisabledPackIds { get; set; } = [];

    /// <summary>File name (not full path) of the armed custom sample inside custom-samples.</summary>
    public string? ArmedSampleFile { get; set; }
}
