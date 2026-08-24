using KeyFXBoard.Core.Abstractions;
using KeyFXBoard.Core.Audio;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using NAudio.Wave;

namespace KeyFXBoard.Windows.Audio;

public sealed class WasapiOutput : IAudioOutput
{
    private readonly int _latencyMs;
    private readonly MMDeviceEnumerator _notifications = new();
    private readonly DeviceChangeClient _client;
    private readonly System.Threading.Timer _debounce;
    private MMDevice? _device;
    private NAudio.Wave.WasapiOut? _output;
    private EngineSampleProvider? _provider;

    public WasapiOutput(int latencyMs = 20)
    {
        _latencyMs = latencyMs;
        _client = new DeviceChangeClient(ScheduleDeviceRefresh);
        _debounce = new System.Threading.Timer(_ =>
        {
            try
            {
                DevicesChanged?.Invoke();
            }
            catch
            {
                // Listeners must not throw back into WASAPI.
            }
        }, null, Timeout.Infinite, Timeout.Infinite);
        try
        {
            _notifications.RegisterEndpointNotificationCallback(_client);
        }
        catch
        {
            // Live device list still refreshes if the user reopens the combo.
        }
    }

    public bool IsRunning => _output?.PlaybackState == PlaybackState.Playing;
    public string DeviceId { get; private set; } = AudioDeviceInfo.DefaultId;
    public string DeviceName { get; private set; } = "Windows default";
    public string? StartWarning { get; private set; }
    public event Action? DevicesChanged;

    public IReadOnlyList<AudioDeviceInfo> ListDevices()
    {
        using var enumerator = new MMDeviceEnumerator();
        var defaultId = TryDefaultId(enumerator);
        var defaultName = TryFriendlyName(enumerator, defaultId);
        var list = new List<AudioDeviceInfo>
        {
            new(
                AudioDeviceInfo.DefaultId,
                defaultName is null ? "Windows default" : $"Windows default ({defaultName})",
                true)
        };

        foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
        {
            try
            {
                list.Add(new AudioDeviceInfo(device.ID, device.FriendlyName, device.ID == defaultId));
            }
            finally
            {
                device.Dispose();
            }
        }

        return list;
    }

    public void Start(FillBuffer fill, string? deviceId)
    {
        ArgumentNullException.ThrowIfNull(fill);
        Stop();
        StartWarning = null;

        using var enumerator = new MMDeviceEnumerator();
        var requested = string.IsNullOrWhiteSpace(deviceId) ? AudioDeviceInfo.DefaultId : deviceId;
        _device = OpenDevice(enumerator, requested, out var usedId, out var warning);
        StartWarning = warning;
        DeviceId = usedId;
        DeviceName = usedId == AudioDeviceInfo.DefaultId
            ? $"Windows default ({_device.FriendlyName})"
            : _device.FriendlyName;

        _provider = new EngineSampleProvider(fill);
        _output = new NAudio.Wave.WasapiOut(_device, AudioClientShareMode.Shared, useEventSync: true, _latencyMs);
        _output.Init(_provider);
        _output.Play();
    }

    public void Stop()
    {
        if (_output is not null)
        {
            _output.Stop();
            _output.Dispose();
            _output = null;
        }

        _provider = null;
        _device?.Dispose();
        _device = null;
    }

    public void Dispose()
    {
        try
        {
            _notifications.UnregisterEndpointNotificationCallback(_client);
        }
        catch
        {
            // Already gone.
        }

        _debounce.Dispose();
        Stop();
        _notifications.Dispose();
    }

    private void ScheduleDeviceRefresh() => _debounce.Change(400, Timeout.Infinite);

    private static MMDevice OpenDevice(MMDeviceEnumerator enumerator, string requestedId, out string usedId, out string? warning)
    {
        warning = null;
        if (!string.Equals(requestedId, AudioDeviceInfo.DefaultId, StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var exact = enumerator.GetDevice(requestedId);
                if (exact.State == DeviceState.Active)
                {
                    usedId = exact.ID;
                    return exact;
                }

                var name = exact.FriendlyName;
                exact.Dispose();
                warning = $"“{name}” is not connected. Using Windows default.";
            }
            catch
            {
                warning = "The saved output is gone. Using Windows default.";
            }
        }

        usedId = AudioDeviceInfo.DefaultId;
        return OpenDefault(enumerator);
    }

    private static MMDevice OpenDefault(MMDeviceEnumerator enumerator)
    {
        try
        {
            var console = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Console);
            if (console.State == DeviceState.Active)
            {
                return console;
            }

            console.Dispose();
        }
        catch
        {
            // Fall through to the first active endpoint.
        }

        MMDevice? first = null;
        foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
        {
            if (first is null)
            {
                first = device;
            }
            else
            {
                device.Dispose();
            }
        }

        return first ?? throw new InvalidOperationException("No active audio output is connected.");
    }

    private static string? TryDefaultId(MMDeviceEnumerator enumerator)
    {
        try
        {
            using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Console);
            return device.State == DeviceState.Active ? device.ID : null;
        }
        catch
        {
            return null;
        }
    }

    private static string? TryFriendlyName(MMDeviceEnumerator enumerator, string? id)
    {
        if (id is null)
        {
            return null;
        }

        try
        {
            using var device = enumerator.GetDevice(id);
            return device.State == DeviceState.Active ? device.FriendlyName : null;
        }
        catch
        {
            return null;
        }
    }

    private sealed class EngineSampleProvider : ISampleProvider
    {
        private readonly FillBuffer _fill;

        public EngineSampleProvider(FillBuffer fill)
        {
            _fill = fill;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(SampleBuffer.SampleRate, SampleBuffer.Channels);
        }

        public WaveFormat WaveFormat { get; }

        public int Read(float[] buffer, int offset, int count)
        {
            _fill(buffer.AsSpan(offset, count));
            return count;
        }
    }

    [System.Runtime.InteropServices.ComVisible(true)]
    private sealed class DeviceChangeClient : IMMNotificationClient
    {
        private readonly Action _notify;

        public DeviceChangeClient(Action notify) => _notify = notify;

        public void OnDeviceStateChanged(string deviceId, DeviceState newState) => _notify();

        public void OnDeviceAdded(string pwstrDeviceId) => _notify();

        public void OnDeviceRemoved(string deviceId) => _notify();

        public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
        {
            if (flow == DataFlow.Render)
            {
                _notify();
            }
        }

        public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key)
        {
            // Volume and other mixer noise. Ignore so we do not restart audio.
        }
    }
}
