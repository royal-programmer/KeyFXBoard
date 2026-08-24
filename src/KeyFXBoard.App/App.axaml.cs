using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Avalonia.Threading;
using KeyFXBoard.App.Services;

namespace KeyFXBoard.App;

public partial class App : Application
{
    private AppRuntime? _runtime;
    private TrayIcon? _tray;
    private NativeMenuItem? _muteItem;
    private DateTime _lastTrayClick;
    private bool _quitRequested;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            RequestedThemeVariant = ThemeVariant.Dark;

            _runtime = new AppRuntime();
            _runtime.Start();
            _runtime.MuteChanged += RefreshTrayAppearance;
            _runtime.ProfilesChanged += RebuildTrayMenu;
            _runtime.InstrumentChanged += RebuildTrayMenu;

            var main = new MainWindow(_runtime, RequestQuit);
            desktop.MainWindow = main;
            try
            {
                InstallTray(desktop);
            }
            catch (Exception ex)
            {
                CrashLog.Write("InstallTray", ex);
            }
            ListenForSecondInstance(main);
            _ = ConsumeIncomingPackAsync(main);

            if (!_runtime.Settings.FirstRunCompleted)
            {
                main.Show();
                _ = OpenFirstRunAsync(main);
            }
            else if (!_runtime.Settings.StartMinimized)
            {
                main.Show();
            }

            desktop.Exit += (_, _) =>
            {
                _tray?.Dispose();
                _runtime.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private async Task OpenFirstRunAsync(Window owner)
    {
        if (_runtime is null)
        {
            return;
        }

        var firstRun = new FirstRunWindow(_runtime);
        await firstRun.ShowDialog(owner);
    }

    private void InstallTray(IClassicDesktopStyleApplicationLifetime desktop)
    {
        if (_runtime is null)
        {
            return;
        }

        _tray = new TrayIcon
        {
            ToolTipText = "Key FX Board",
            Icon = IconFactory.Create(_runtime.Engine.Muted),
            Menu = CreateTrayMenu(desktop)
        };
        _tray.Clicked += (_, _) => OnTrayClicked(desktop);
        TrayIcon.SetIcons(this, [_tray]);
        RefreshTrayAppearance();
    }

    private void OnTrayClicked(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var now = DateTime.UtcNow;
        if ((now - _lastTrayClick).TotalMilliseconds < 350)
        {
            _lastTrayClick = DateTime.MinValue;
            ShowMain(desktop);
            return;
        }

        _lastTrayClick = now;
        DispatcherTimer.RunOnce(() =>
        {
            if (_lastTrayClick != DateTime.MinValue &&
                (DateTime.UtcNow - _lastTrayClick).TotalMilliseconds >= 300)
            {
                _runtime?.SetMuted(!_runtime.Engine.Muted);
            }
        }, TimeSpan.FromMilliseconds(320));
    }

    private void ListenForSecondInstance(MainWindow main)
    {
        Program.Instance?.ListenForActivation(() =>
            Dispatcher.UIThread.Post(() =>
            {
                main.ShowFromTray();
                main.Activate();
                _ = ConsumeIncomingPackAsync(main);
            }));
    }

    private async Task ConsumeIncomingPackAsync(MainWindow main)
    {
        if (_runtime is null)
        {
            return;
        }

        var incoming = Program.StartupPackPath;
        Program.StartupPackPath = null;
        if (string.IsNullOrWhiteSpace(incoming) &&
            !_runtime.TryConsumePendingInstall(out incoming))
        {
            return;
        }

        main.ShowFromTray();
        await main.InstallPackFromPathAsync(incoming!);
    }

    private static void ShowMain(IClassicDesktopStyleApplicationLifetime desktop)
    {
        if (desktop.MainWindow is MainWindow window)
        {
            window.ShowFromTray();
            window.Activate();
        }
    }

    private void RefreshTrayAppearance()
    {
        if (_runtime is null || _tray is null)
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            _tray.Icon = IconFactory.Create(_runtime.Engine.Muted);
            _tray.ToolTipText = _runtime.Engine.Muted ? "Key FX Board — muted" : "Key FX Board";
            if (_muteItem is not null)
            {
                _muteItem.IsChecked = _runtime.Engine.Muted;
            }
        });
    }

    private void RebuildTrayMenu()
    {
        if (_tray is null || ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return;
        }

        Dispatcher.UIThread.Post(() => _tray.Menu = CreateTrayMenu(desktop));
    }

    private NativeMenu CreateTrayMenu(IClassicDesktopStyleApplicationLifetime desktop)
    {
        _muteItem = new NativeMenuItem("Mute")
        {
            ToggleType = NativeMenuItemToggleType.CheckBox,
            IsChecked = _runtime?.Engine.Muted == true
        };
        _muteItem.Click += (_, _) => _runtime?.SetMuted(!_runtime.Engine.Muted);

        var profiles = new NativeMenu();
        if (_runtime is not null)
        {
            foreach (var profile in _runtime.Profiles)
            {
                var item = new NativeMenuItem(profile.Name)
                {
                    ToggleType = NativeMenuItemToggleType.Radio,
                    IsChecked = profile.Id.Equals(_runtime.Settings.ActiveProfileId, StringComparison.OrdinalIgnoreCase)
                };
                var id = profile.Id;
                item.Click += async (_, _) =>
                {
                    if (desktop.MainWindow is MainWindow window &&
                        !await window.ConfirmLeaveUnsavedAsync())
                    {
                        return;
                    }

                    _runtime.ActivateProfile(id);
                };
                profiles.Items.Add(item);
            }
        }

        var pianoItem = new NativeMenuItem(_runtime?.PianoMode == true ? "Stop piano" : "Start piano");
        pianoItem.Click += (_, _) =>
        {
            if (_runtime is null)
            {
                return;
            }

            if (_runtime.PianoMode)
            {
                _runtime.StopPiano();
                return;
            }

            _runtime.StartPiano();
            if (desktop.MainWindow is MainWindow window)
            {
                window.ShowPianoMap();
            }
        };

        var openItem = new NativeMenuItem("Open Key FX Board");
        openItem.Click += (_, _) => ShowMain(desktop);
        var quitItem = new NativeMenuItem("Quit");
        quitItem.Click += (_, _) => RequestQuit();

        return
        [
            _muteItem,
            pianoItem,
            new NativeMenuItem("Profiles") { Menu = profiles },
            new NativeMenuItemSeparator(),
            openItem,
            quitItem
        ];
    }

    public async void RequestQuit()
    {
        if (_quitRequested)
        {
            return;
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (desktop.MainWindow is MainWindow window &&
                !await window.ConfirmQuitAsync())
            {
                return;
            }

            _quitRequested = true;
            if (desktop.MainWindow is MainWindow main)
            {
                main.AllowClose = true;
            }

            desktop.Shutdown();
        }
    }
}
