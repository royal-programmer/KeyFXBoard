namespace KeyFXBoard.Core.Abstractions;

public delegate void FillBuffer(Span<float> interleavedStereo);

public interface IAudioOutput : IDisposable
{
    bool IsRunning { get; }
    string DeviceId { get; }
    string DeviceName { get; }
    string? StartWarning { get; }
    /// <summary>WASAPI shared-mode latency hint in milliseconds.</summary>
    int LatencyMs { get; set; }
    IReadOnlyList<AudioDeviceInfo> ListDevices();
    event Action? DevicesChanged;
    void Start(FillBuffer fill, string? deviceId);
    void Stop();
}
