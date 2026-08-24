using System.Diagnostics;
using System.Runtime.InteropServices;
using KeyFXBoard.Core.Abstractions;
using KeyFXBoard.Core.Keys;

namespace KeyFXBoard.Windows.Hook;

public sealed class LowLevelKeyboardSource : IKeyboardSource
{
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;
    private const uint LlkhfUp = 0x80;
    private const uint LlkhfInjected = 0x10;
    private const uint WmQuit = 0x0012;
    private const int VkControl = 0x11;
    private const int VkMenu = 0x12;
    private const int VkLWin = 0x5B;
    private const int VkRWin = 0x5C;

    private readonly HashSet<int> _pressed = [];
    private readonly LowLevelKeyboardProc _proc;
    private readonly object _gate = new();

    private Action<KeyEvent>? _onEvent;
    private Thread? _thread;
    private uint _threadId;
    private nint _hook;
    private Exception? _startError;

    public LowLevelKeyboardSource()
    {
        _proc = HookCallback;
    }

    public bool IsAttached { get; private set; }

    public void Start(Action<KeyEvent> onEvent)
    {
        ArgumentNullException.ThrowIfNull(onEvent);
        lock (_gate)
        {
            if (_thread is not null)
            {
                throw new InvalidOperationException("Keyboard source is already started.");
            }

            _onEvent = onEvent;
            _startError = null;
            using var started = new ManualResetEventSlim(false);
            _thread = new Thread(() => HookThread(started))
            {
                IsBackground = true,
                Name = "KeyFX-Hook"
            };
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.Start();
            if (!started.Wait(TimeSpan.FromSeconds(3)))
            {
                throw new TimeoutException("Keyboard hook thread did not start.");
            }

            if (_startError is not null)
            {
                _thread = null;
                throw new InvalidOperationException("Failed to install the keyboard hook.", _startError);
            }
        }
    }

    public void Stop()
    {
        Thread? thread;
        lock (_gate)
        {
            thread = _thread;
            if (thread is null)
            {
                return;
            }

            if (_threadId != 0)
            {
                PostThreadMessage(_threadId, WmQuit, UIntPtr.Zero, 0);
            }
        }

        if (!thread.Join(TimeSpan.FromSeconds(2)))
        {
            thread.Interrupt();
        }

        lock (_gate)
        {
            _thread = null;
            _threadId = 0;
            _onEvent = null;
            IsAttached = false;
            _pressed.Clear();
        }
    }

    public void Dispose() => Stop();

    private void HookThread(ManualResetEventSlim started)
    {
        _threadId = GetCurrentThreadId();
        try
        {
            _hook = SetWindowsHookExW(WhKeyboardLl, _proc, GetModuleHandleW(null), 0);
            if (_hook == 0)
            {
                using var process = Process.GetCurrentProcess();
                var module = process.MainModule?.BaseAddress ?? 0;
                _hook = SetWindowsHookExW(WhKeyboardLl, _proc, module, 0);
            }

            if (_hook == 0)
            {
                _startError = new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                started.Set();
                return;
            }

            IsAttached = true;
            started.Set();

            while (GetMessageW(out var msg, 0, 0, 0) > 0)
            {
                TranslateMessage(ref msg);
                DispatchMessageW(ref msg);
            }
        }
        catch (Exception ex)
        {
            _startError = ex;
            started.Set();
        }
        finally
        {
            if (_hook != 0)
            {
                UnhookWindowsHookEx(_hook);
                _hook = 0;
            }

            IsAttached = false;
        }
    }

    private nint HookCallback(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0)
        {
            var info = Marshal.PtrToStructure<KbdLlHookStruct>(lParam);
            var vk = unchecked((int)info.VkCode);
            var injected = (info.Flags & LlkhfInjected) != 0;
            var isUp = (info.Flags & LlkhfUp) != 0
                       || wParam == WmKeyUp
                       || wParam == WmSysKeyUp;
            var isDownMessage = wParam is WmKeyDown or WmSysKeyDown;

            if (isUp || isDownMessage)
            {
                var kind = Classify(vk, isUp);
                var ev = new KeyEvent(
                    KeyId.FromVirtualKey(vk),
                    kind,
                    injected,
                    IsDown(VkControl),
                    IsDown(VkMenu),
                    IsDown(VkLWin) || IsDown(VkRWin));

                _onEvent?.Invoke(ev);
            }
        }

        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    private KeyKind Classify(int vk, bool isUp)
    {
        if (isUp)
        {
            _pressed.Remove(vk);
            return KeyKind.Up;
        }

        if (!_pressed.Add(vk))
        {
            return KeyKind.Repeat;
        }

        return KeyKind.Down;
    }

    private static bool IsDown(int vk) => (GetKeyState(vk) & 0x8000) != 0;

    private delegate nint LowLevelKeyboardProc(int nCode, nint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct KbdLlHookStruct
    {
        public uint VkCode;
        public uint ScanCode;
        public uint Flags;
        public uint Time;
        public nuint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Msg
    {
        public nint Hwnd;
        public uint Message;
        public nuint WParam;
        public nint LParam;
        public uint Time;
        public int X;
        public int Y;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWindowsHookExW(int idHook, LowLevelKeyboardProc lpfn, nint hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(nint hhk);

    [DllImport("user32.dll")]
    private static extern nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int nVirtKey);

    [DllImport("user32.dll")]
    private static extern bool PostThreadMessage(uint idThread, uint msg, UIntPtr wParam, nint lParam);

    [DllImport("user32.dll")]
    private static extern int GetMessageW(out Msg lpMsg, nint hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref Msg lpMsg);

    [DllImport("user32.dll")]
    private static extern nint DispatchMessageW(ref Msg lpMsg);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandleW(string? lpModuleName);
}
