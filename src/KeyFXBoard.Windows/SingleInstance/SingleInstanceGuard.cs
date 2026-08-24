namespace KeyFXBoard.Windows.SingleInstance;

public sealed class SingleInstanceGuard : IDisposable
{
    public const string MutexName = @"Local\KeyFXBoard.SingleInstance";
    public const string ShowEventName = @"Local\KeyFXBoard.ShowWindow";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _show;
    private readonly CancellationTokenSource _cts = new();
    private Thread? _listenThread;
    private bool _disposed;

    private SingleInstanceGuard(Mutex mutex, EventWaitHandle show)
    {
        _mutex = mutex;
        _show = show;
    }

    public static bool TryStartPrimary(out SingleInstanceGuard? guard)
    {
        var mutex = new Mutex(initiallyOwned: true, MutexName, out var created);
        if (!created)
        {
            mutex.Dispose();
            try
            {
                using var existing = EventWaitHandle.OpenExisting(ShowEventName);
                existing.Set();
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                // Primary is shutting down; just exit.
            }

            guard = null;
            return false;
        }

        var show = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        guard = new SingleInstanceGuard(mutex, show);
        return true;
    }

    public void ListenForActivation(Action onActivate)
    {
        ArgumentNullException.ThrowIfNull(onActivate);
        _listenThread = new Thread(() =>
        {
            while (!_cts.IsCancellationRequested)
            {
                if (_show.WaitOne(TimeSpan.FromMilliseconds(400)))
                {
                    onActivate();
                }
            }
        })
        {
            IsBackground = true,
            Name = "KeyFX-SingleInstance"
        };
        _listenThread.Start();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cts.Cancel();
        _show.Set();
        _listenThread?.Join(TimeSpan.FromSeconds(1));
        _show.Dispose();
        _cts.Dispose();
        _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}
