using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using KeyFXBoard.App.Services;
using KeyFXBoard.Core.Abstractions;
using KeyFXBoard.Core.Filtering;
using KeyFXBoard.Core.Packs;
using KeyFXBoard.Core.Profiles;

namespace KeyFXBoard.App;

public partial class MainWindow : Window
{
    private readonly AppRuntime? _runtime;
    private readonly Action? _quit;
    private bool _syncing;
    private int _uiLoadDepth;
    private PianoMapWindow? _pianoMap;

    public MainWindow()
    {
        InitializeComponent();
    }

    public MainWindow(AppRuntime runtime, Action quit) : this()
    {
        _runtime = runtime;
        _quit = quit;
        runtime.MuteChanged += () => Avalonia.Threading.Dispatcher.UIThread.Post(LoadFromRuntime);
        runtime.PacksChanged += () => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            RefreshPacks();
            RefreshProfiles();
            LoadBehavior();
            LoadFx();
        });
        runtime.InstrumentChanged += () => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            RefreshInstrumentUi();
            RefreshProfiles();
            LoadFromRuntime();
        });
        runtime.ProfilesChanged += () => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            RefreshProfiles();
            LoadFromRuntime();
            LoadBehavior();
            LoadFx();
            RefreshSaveButtons();
        });
        runtime.WorkingChanged += () => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            RefreshSaveButtons();
            OctaveLabel.Text = PianoLayout.OctaveLabel(runtime.Engine.OctaveShift);
        });
        runtime.Engine.OctaveShiftChanged += _ => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            OctaveLabel.Text = PianoLayout.OctaveLabel(runtime.Engine.OctaveShift);
            _pianoMap?.Rebuild(runtime.Engine.OctaveShift);
        });
        runtime.AudioChanged += () => Avalonia.Threading.Dispatcher.UIThread.Post(LoadFromRuntime);
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
        LoadFromRuntime();
        RefreshPacks();
        RefreshProfiles();
        LoadBehavior();
        LoadFx();
        RefreshSaveButtons();
    }

    public bool AllowClose { get; set; }

    private void FillOutputCombo(ComboBox box)
    {
        if (_runtime is null)
        {
            return;
        }

        box.SelectionChanged -= OnOutputChanged;
        box.Items.Clear();
        ComboBoxItem? selected = null;
        var wanted = _runtime.Settings.AudioDeviceId;
        var devices = _runtime.ListAudioDevices();
        if (!devices.Any(d => d.Id.Equals(wanted, StringComparison.OrdinalIgnoreCase)))
        {
            wanted = AudioDeviceInfo.DefaultId;
        }

        foreach (var device in devices)
        {
            var item = new ComboBoxItem { Content = device.Name, Tag = device.Id };
            box.Items.Add(item);
            if (device.Id.Equals(wanted, StringComparison.OrdinalIgnoreCase))
            {
                selected = item;
            }
        }

        box.SelectedItem = selected ?? (box.Items.Count > 0 ? box.Items[0] : null);
        box.SelectionChanged += OnOutputChanged;
    }

    private void OnOutputChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncing || _runtime is null || sender is not ComboBox box)
        {
            return;
        }

        if (box.SelectedItem is ComboBoxItem { Tag: string id })
        {
            _runtime.SetAudioDevice(id);
        }
    }

    public async Task<bool> ConfirmQuitAsync()
    {
        ShowFromTray();
        var body = _runtime?.HasUnsavedChanges == true
            ? "Are you sure you want to quit Key FX Board? Unsaved profile tweaks will be lost."
            : "Are you sure you want to quit Key FX Board? Sounds will stop.";
        return await MessageDialog.Confirm(this, "Quit Key FX Board?", body, "Quit");
    }

    public async Task<bool> ConfirmLeaveUnsavedAsync()
    {
        if (_runtime is null || !_runtime.HasUnsavedChanges)
        {
            return true;
        }

        ShowFromTray();
        var choice = await UnsavedChangesDialog.Ask(this, _runtime.ActiveProfile?.IsFactory == true);
        switch (choice)
        {
            case UnsavedChoice.Cancel:
                return false;
            case UnsavedChoice.Save:
                _runtime.SaveWorking();
                return true;
            case UnsavedChoice.SaveAs:
                return await PromptSaveAsAsync();
            default:
                return true;
        }
    }

    public void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void BeginUiLoad()
    {
        _uiLoadDepth++;
        _syncing = true;
    }

    private void EndUiLoad()
    {
        Dispatcher.UIThread.Post(() =>
        {
            _uiLoadDepth = Math.Max(0, _uiLoadDepth - 1);
            if (_uiLoadDepth == 0)
            {
                _syncing = false;
            }
        }, DispatcherPriority.Loaded);
    }

    private void LoadFromRuntime()
    {
        if (_runtime is null)
        {
            return;
        }

        BeginUiLoad();
        MuteBox.IsChecked = _runtime.Engine.Muted;
        VolumeSlider.Value = _runtime.Engine.AppTrim * 100;
        VolumeLabel.Text = $"{(int)VolumeSlider.Value}%";
        BoostSlider.Value = _runtime.Settings.OutputBoostDb;
        BoostLabel.Text = $"+{(int)BoostSlider.Value} dB";
        VelocitySlider.Value = _runtime.Engine.VelocityRandom * 100;
        VelocityLabel.Text = $"{(int)VelocitySlider.Value}%";
        AutostartBox.IsChecked = _runtime.Settings.Autostart;
        CloseToTrayBox.IsChecked = _runtime.Settings.MinimizeToTrayOnClose;
        StartMinimizedBox.IsChecked = _runtime.Settings.StartMinimized;
        DataPath.Text = _runtime.Paths.Root;
        VersionText.Text = $"Version {AppUpdateService.CurrentVersion}";
        UpdateStatusText.Text = string.IsNullOrWhiteSpace(AppUpdateService.UpdateFeedUrl)
            ? "Updates: feed not configured yet (OK for first Setup). Later releases use Check for updates."
            : "Updates: Check for updates when you want a new version.";
        ElevationNote.Text = _runtime.IsElevated
            ? "This process is elevated. You will hear keys in Administrator windows."
            : "Sounds will not play in Administrator windows.";
        var profileName = _runtime.Engine.ActiveProfileName;
        var packs = _runtime.Engine.ResidentPacks;
        ActivePackLabel.Text = profileName is null
            ? "Active profile: none"
            : $"Active profile: {profileName}" + (string.IsNullOrWhiteSpace(packs) ? "" : $"\nIn memory: {packs}");
        OctaveLabel.Text = PianoLayout.OctaveLabel(_runtime.Engine.OctaveShift);
        RefreshInstrumentUi();

        SetStatus(HookStatus, _runtime.HookError, _runtime.Keyboard.IsAttached, "Hook");
        SetStatus(AudioStatus, _runtime.AudioError, _runtime.Audio.IsRunning, "Audio", _runtime.Audio.DeviceName);
        if (_runtime.AudioError is null && _runtime.AudioWarning is not null)
        {
            AudioStatus.Text += "\n" + _runtime.AudioWarning;
            AudioStatus.Foreground = Brushes.Orange;
        }

        FillOutputCombo(HomeOutputBox);
        FillOutputCombo(SettingsOutputBox);

        var failed = _runtime.HookError ?? _runtime.AudioError ?? _runtime.PackError;
        ErrorBanner.IsVisible = failed is not null;
        ErrorBannerText.Text = failed is null ? "" : failed;

        MuteBox.IsCheckedChanged -= OnMuteChanged;
        MuteBox.IsCheckedChanged += OnMuteChanged;
        VolumeSlider.PropertyChanged -= OnVolumeChanged;
        VolumeSlider.PropertyChanged += OnVolumeChanged;
        BoostSlider.PropertyChanged -= OnBoostChanged;
        BoostSlider.PropertyChanged += OnBoostChanged;
        VelocitySlider.PropertyChanged -= OnVelocityChanged;
        VelocitySlider.PropertyChanged += OnVelocityChanged;
        AutostartBox.IsCheckedChanged -= OnAutostartChanged;
        AutostartBox.IsCheckedChanged += OnAutostartChanged;
        CloseToTrayBox.IsCheckedChanged -= OnCloseToTrayChanged;
        CloseToTrayBox.IsCheckedChanged += OnCloseToTrayChanged;
        StartMinimizedBox.IsCheckedChanged -= OnStartMinimizedChanged;
        StartMinimizedBox.IsCheckedChanged += OnStartMinimizedChanged;
        EndUiLoad();
    }

    private void OnMuteChanged(object? sender, RoutedEventArgs e)
    {
        if (!_syncing)
        {
            _runtime?.SetMuted(MuteBox.IsChecked == true);
        }
    }

    private void OnVolumeChanged(object? sender, Avalonia.AvaloniaPropertyChangedEventArgs e)
    {
        if (_syncing || e.Property != Slider.ValueProperty || _runtime is null)
        {
            return;
        }

        VolumeLabel.Text = $"{(int)VolumeSlider.Value}%";
        _runtime.SetVolume((float)(VolumeSlider.Value / 100.0));
    }

    private void OnBoostChanged(object? sender, Avalonia.AvaloniaPropertyChangedEventArgs e)
    {
        if (_syncing || e.Property != Slider.ValueProperty || _runtime is null)
        {
            return;
        }

        BoostLabel.Text = $"+{(int)BoostSlider.Value} dB";
        _runtime.SetOutputBoostDb((float)BoostSlider.Value);
    }

    private void OnVelocityChanged(object? sender, Avalonia.AvaloniaPropertyChangedEventArgs e)
    {
        if (_syncing || e.Property != Slider.ValueProperty || _runtime is null)
        {
            return;
        }

        VelocityLabel.Text = $"{(int)VelocitySlider.Value}%";
        _runtime.SetVelocityRandom((float)(VelocitySlider.Value / 100.0));
    }

    private void OnAutostartChanged(object? sender, RoutedEventArgs e)
    {
        if (!_syncing)
        {
            _runtime?.SetAutostart(AutostartBox.IsChecked == true);
        }
    }

    private void OnCloseToTrayChanged(object? sender, RoutedEventArgs e)
    {
        if (!_syncing)
        {
            _runtime?.SetMinimizeToTrayOnClose(CloseToTrayBox.IsChecked == true);
        }
    }

    private void OnStartMinimizedChanged(object? sender, RoutedEventArgs e)
    {
        if (!_syncing)
        {
            _runtime?.SetStartMinimized(StartMinimizedBox.IsChecked == true);
        }
    }

    private void OnNavChanged(object? sender, SelectionChangedEventArgs e)
    {
        var tag = (Nav.SelectedItem as ListBoxItem)?.Tag as string;
        HomePage.IsVisible = tag is null or "home";
        InstrumentsPage.IsVisible = tag == "instruments";
        ProfilesPage.IsVisible = tag == "profiles";
        FxPage.IsVisible = tag == "fx";
        BehaviorPage.IsVisible = tag == "behavior";
        PacksPage.IsVisible = tag == "packs";
        SettingsPage.IsVisible = tag == "settings";
        AboutPage.IsVisible = tag == "about";
    }

    private void OnOpenDataFolder(object? sender, RoutedEventArgs e)
    {
        if (_runtime is null)
        {
            return;
        }

        Directory.CreateDirectory(_runtime.Paths.Root);
        Process.Start(new ProcessStartInfo
        {
            FileName = _runtime.Paths.Root,
            UseShellExecute = true
        });
    }

    public async Task InstallPackFromPathAsync(string path)
    {
        if (_runtime is null)
        {
            return;
        }

        try
        {
            try
            {
                _runtime.InstallPack(path, replaceExisting: false);
            }
            catch (PackException ex) when (ex.Message.Contains("already installed", StringComparison.OrdinalIgnoreCase))
            {
                var replace = await MessageDialog.Confirm(
                    this,
                    "Replace pack?",
                    $"{ex.Message} Replace the installed copy?");
                if (!replace)
                {
                    return;
                }

                _runtime.InstallPack(path, replaceExisting: true);
            }

            RefreshPacks();
            LoadFromRuntime();
            await MessageDialog.Alert(this, "Pack installed", "The pack is on disk. Assign it from Profiles — only the active profile is preloaded.");
        }
        catch (Exception ex)
        {
            await MessageDialog.Alert(this, "Could not install pack", ex.Message);
        }
    }

    private async void OnInstallPack(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Install Key FX pack",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Key FX Pack") { Patterns = ["*.kfxpack"] }
            ]
        });

        if (files.Count == 0)
        {
            return;
        }

        await InstallPackFromPathAsync(files[0].Path.LocalPath);
    }

    private void OnOpenExamples(object? sender, RoutedEventArgs e)
    {
        if (_runtime is null)
        {
            return;
        }

        Directory.CreateDirectory(_runtime.Paths.ExamplesDirectory);
        Process.Start(new ProcessStartInfo
        {
            FileName = _runtime.Paths.ExamplesDirectory,
            UseShellExecute = true
        });
    }

    private void RefreshPacks()
    {
        if (_runtime is null)
        {
            return;
        }

        ExamplesHint.Text = $"Example packs are in {_runtime.Paths.ExamplesDirectory}";
        RefreshCustomSamples();
        PackRows.Children.Clear();
        foreach (var pack in _runtime.Packs)
        {
            if (pack.Id.Equals(CustomSampleLibrary.PackId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            PackRows.Children.Add(CreatePackRow(pack));
        }
    }

    private void RefreshCustomSamples()
    {
        if (_runtime is null)
        {
            return;
        }

        CustomSampleRows.Children.Clear();
        var files = CustomSampleLibrary.ListFiles(_runtime.Paths);
        if (files.Count == 0)
        {
            CustomSampleRows.Children.Add(new TextBlock
            {
                Text = "No files yet. Open the folder and drop a WAV, MP3, or other Windows-decodable audio file.",
                Opacity = 0.7,
                TextWrapping = TextWrapping.Wrap
            });
            return;
        }

        var armed = Path.GetFileName(CustomSampleLibrary.ResolveArmed(_runtime.Paths, _runtime.Settings.ArmedSampleFile) ?? "");
        foreach (var path in files)
        {
            CustomSampleRows.Children.Add(CreateCustomSampleRow(path, Path.GetFileName(path).Equals(armed, StringComparison.OrdinalIgnoreCase)));
        }
    }

    private Control CreateCustomSampleRow(string path, bool armed)
    {
        var name = Path.GetFileName(path);
        var title = new TextBlock
        {
            Text = name + (armed ? "  ·  armed" : ""),
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };
        var arm = new Button { Content = armed ? "Armed" : "Arm", IsEnabled = !armed };
        arm.Click += (_, _) =>
        {
            _runtime!.ArmSample(name);
            RefreshPacks();
            RefreshProfiles();
        };
        var preview = new Button { Content = "Preview" };
        preview.Click += async (_, _) =>
        {
            try
            {
                _runtime!.PreviewCustomSample(name);
            }
            catch (Exception ex)
            {
                await MessageDialog.Alert(this, "Could not preview", ex.Message);
            }
        };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        buttons.Children.Add(arm);
        buttons.Children.Add(preview);
        var row = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(buttons, Dock.Right);
        row.Children.Add(buttons);
        row.Children.Add(title);
        return row;
    }

    private void OnOpenCustomSamples(object? sender, RoutedEventArgs e)
    {
        if (_runtime is null)
        {
            return;
        }

        Directory.CreateDirectory(_runtime.Paths.CustomSamplesDirectory);
        Process.Start(new ProcessStartInfo
        {
            FileName = _runtime.Paths.CustomSamplesDirectory,
            UseShellExecute = true
        });
    }

    private Control CreatePackRow(InstalledPack pack)
    {
        var inUse = pack.Id.Equals(_runtime!.ActivePackId, StringComparison.OrdinalIgnoreCase);
        var enabled = _runtime.IsPackEnabled(pack.Id);
        var title = new TextBlock
        {
            Text = pack.Name + (inUse ? "  ·  in use" : "") + (enabled ? "" : "  ·  hidden"),
            FontWeight = FontWeight.SemiBold
        };
        var meta = new TextBlock
        {
            Text = $"{pack.Version}  ·  {pack.Author}  ·  {pack.License}" +
                   (string.IsNullOrWhiteSpace(pack.Description) ? "" : $"\n{pack.Description}"),
            Opacity = 0.75,
            TextWrapping = TextWrapping.Wrap
        };

        var toggle = new Button { Content = enabled ? "Disable" : "Enable" };
        ToolTip.SetTip(toggle, enabled
            ? "Hide this pack when picking a pack on a profile. It stays installed."
            : "Show this pack again in the profile pack list.");
        toggle.Click += (_, _) => _runtime.SetPackEnabled(pack.Id, !enabled);

        var uninstall = new Button { Content = "Uninstall", IsEnabled = !pack.IsFactory };
        uninstall.Click += async (_, _) =>
        {
            var ok = await MessageDialog.Confirm(
                this,
                "Uninstall pack?",
                pack.Id.Equals(_runtime.ActivePackId, StringComparison.OrdinalIgnoreCase)
                    ? $"Uninstall {pack.Name}? Profiles that used it will fall back to Factory Click."
                    : $"Uninstall {pack.Name}?");
            if (!ok)
            {
                return;
            }

            try
            {
                _runtime.UninstallPack(pack.Id);
                RefreshPacks();
                LoadFromRuntime();
            }
            catch (Exception ex)
            {
                await MessageDialog.Alert(this, "Could not uninstall", ex.Message);
            }
        };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Avalonia.Thickness(0, 8, 0, 0)
        };
        buttons.Children.Add(toggle);
        buttons.Children.Add(uninstall);

        var body = new StackPanel { Spacing = 4 };
        body.Children.Add(title);
        body.Children.Add(meta);
        body.Children.Add(buttons);

        return new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
            BorderThickness = new Avalonia.Thickness(1),
            CornerRadius = new Avalonia.CornerRadius(8),
            Padding = new Avalonia.Thickness(12),
            Child = body
        };
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = HasPackFile(e) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        var path = FirstPackPath(e);
        if (path is null)
        {
            return;
        }

        await InstallPackFromPathAsync(path);
    }

    private static bool HasPackFile(DragEventArgs e) => FirstPackPath(e) is not null;

    private static string? FirstPackPath(DragEventArgs e)
    {
        if (e.DataTransfer is null || !e.DataTransfer.Contains(DataFormat.File))
        {
            return null;
        }

        var file = e.DataTransfer.TryGetFiles()?.FirstOrDefault(f =>
            f.Name.EndsWith(".kfxpack", StringComparison.OrdinalIgnoreCase));
        return file?.TryGetLocalPath();
    }

    private void OnSaveWorking(object? sender, RoutedEventArgs e)
    {
        if (_runtime?.CanSave != true)
        {
            return;
        }

        _runtime.SaveWorking();
        RefreshSaveButtons();
        RefreshProfiles();
        LoadFromRuntime();
    }

    private async void OnSaveWorkingAs(object? sender, RoutedEventArgs e) =>
        await PromptSaveAsAsync();

    private async Task<bool> PromptSaveAsAsync()
    {
        if (_runtime?.ActiveProfile is not { } profile)
        {
            return false;
        }

        var suggested = profile.IsFactory ? $"My {profile.Name}" : $"{profile.Name} copy";
        var name = await PromptDialog.Ask(
            this,
            "Save as",
            "Name this profile. System names cannot be reused.",
            suggested,
            value => FactoryProfileSeeder.ValidateUserProfileName(value, _runtime.Profiles));
        if (name is null)
        {
            return false;
        }

        try
        {
            _runtime.SaveWorkingAs(name);
            RefreshProfiles();
            LoadFromRuntime();
            LoadBehavior();
            LoadFx();
            RefreshSaveButtons();
            return true;
        }
        catch (Exception ex)
        {
            await MessageDialog.Alert(this, "Could not save profile", ex.Message);
            return false;
        }
    }

    private void OnDuplicateActive(object? sender, RoutedEventArgs e)
    {
        _runtime?.DuplicateActive();
        RefreshProfiles();
        LoadFromRuntime();
        LoadBehavior();
        LoadFx();
    }

    private async void OnResetActive(object? sender, RoutedEventArgs e)
    {
        if (_runtime?.ActiveProfile is not { } profile)
        {
            return;
        }

        if (_runtime?.CanReset != true)
        {
            return;
        }

        var target = _runtime.ActiveProfile?.IsFactory == true ? "the system default" : "the last save";
        if (!await MessageDialog.Confirm(this, "Reset profile?", $"Restore {profile.Name} to {target}?"))
        {
            return;
        }

        _runtime.ResetWorking();
        RefreshProfiles();
        LoadFromRuntime();
        LoadBehavior();
        LoadFx();
        RefreshSaveButtons();
    }

    private void RefreshProfiles()
    {
        if (_runtime is null)
        {
            return;
        }

        var active = _runtime.ActiveProfile;
        ProfileEditHint.Text = active?.IsFactory == true
            ? "System profile. Tweaks are live until you Save as a new profile. Reset restores the shipped default."
            : "User profile. Save writes a checkpoint. Reset restores the last save. Overlay pack is for Enter, Escape, and Space.";

        ProfileRows.Children.Clear();
        foreach (var profile in _runtime.Profiles)
        {
            ProfileRows.Children.Add(CreateProfileRow(profile));
        }

        BeginUiLoad();
        FillPackCombo(PrimaryPackBox, active?.PrimaryPackId, includeNone: false);
        FillPackCombo(OverlayPackBox, active?.Overlays.FirstOrDefault()?.PackId, includeNone: true);
        FillCustomSoundCombo(active);
        PrimaryPackBox.IsEnabled = !_runtime.PianoMode;
        OverlayPackBox.IsEnabled = !_runtime.PianoMode;
        CustomSoundBox.IsEnabled = !_runtime.PianoMode;
        PrimaryPackBox.SelectionChanged -= OnPrimaryPackChanged;
        OverlayPackBox.SelectionChanged -= OnOverlayPackChanged;
        CustomSoundBox.SelectionChanged -= OnCustomSoundChanged;
        PrimaryPackBox.SelectionChanged += OnPrimaryPackChanged;
        OverlayPackBox.SelectionChanged += OnOverlayPackChanged;
        CustomSoundBox.SelectionChanged += OnCustomSoundChanged;
        EndUiLoad();
        RefreshSaveButtons();
    }

    private void RefreshSaveButtons()
    {
        if (_runtime is null)
        {
            return;
        }

        SaveButton.IsVisible = _runtime.ActiveProfile is { IsFactory: false };
        SaveButton.IsEnabled = _runtime.CanSave;
        SaveAsButton.IsEnabled = _runtime.CanSaveAs;
        ResetButton.IsEnabled = _runtime.CanReset;
    }

    private Control CreateProfileRow(ProfileDocument profile)
    {
        var active = profile.Id.Equals(_runtime!.Settings.ActiveProfileId, StringComparison.OrdinalIgnoreCase);
        var title = new TextBlock
        {
            Text = active ? $"{profile.Name}  ·  active" : profile.Name,
            FontWeight = FontWeight.SemiBold
        };
        var meta = new TextBlock
        {
            Text = profile.IsFactory ? "System" : "Yours",
            Opacity = 0.7
        };

        var use = new Button { Content = "Use", IsEnabled = !active };
        use.Click += async (_, _) =>
        {
            if (await ConfirmLeaveUnsavedAsync())
            {
                _runtime.ActivateProfile(profile.Id);
            }
        };

        var dup = new Button { Content = "Duplicate" };
        dup.Click += (_, _) =>
        {
            _runtime.Duplicate(profile.Id);
            RefreshProfiles();
        };

        var rename = new Button { Content = "Rename", IsEnabled = !profile.IsFactory };
        rename.Click += async (_, _) =>
        {
            var name = await PromptDialog.Ask(
                this,
                "Rename profile",
                "Name this profile. System names cannot be reused.",
                profile.Name,
                value => FactoryProfileSeeder.ValidateUserProfileName(value, _runtime.Profiles, profile.Id));
            if (name is null)
            {
                return;
            }

            try
            {
                _runtime.RenameProfile(profile.Id, name);
                RefreshProfiles();
                LoadFromRuntime();
                LoadFx();
                RefreshSaveButtons();
            }
            catch (Exception ex)
            {
                await MessageDialog.Alert(this, "Could not rename", ex.Message);
            }
        };

        var reset = new Button { Content = "Reset", IsEnabled = active && _runtime.CanReset };
        reset.Click += (_, _) =>
        {
            if (active)
            {
                OnResetActive(null, new RoutedEventArgs());
            }
        };

        var delete = new Button { Content = "Delete", IsEnabled = !profile.IsFactory };
        delete.Click += async (_, _) =>
        {
            if (!await MessageDialog.Confirm(this, "Delete profile?", $"Delete {profile.Name}?"))
            {
                return;
            }

            _runtime.DeleteProfile(profile.Id);
            RefreshProfiles();
            LoadFromRuntime();
            LoadBehavior();
            LoadFx();
        };

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Avalonia.Thickness(0, 8, 0, 0) };
        buttons.Children.Add(use);
        buttons.Children.Add(dup);
        buttons.Children.Add(rename);
        buttons.Children.Add(reset);
        buttons.Children.Add(delete);
        var body = new StackPanel { Spacing = 4 };
        body.Children.Add(title);
        body.Children.Add(meta);
        body.Children.Add(buttons);
        return new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
            BorderThickness = new Avalonia.Thickness(1),
            CornerRadius = new Avalonia.CornerRadius(8),
            Padding = new Avalonia.Thickness(12),
            Child = body
        };
    }

    private void FillPackCombo(ComboBox box, string? selectedId, bool includeNone)
    {
        box.Items.Clear();
        if (includeNone)
        {
            box.Items.Add(new ComboBoxItem { Content = "(none)", Tag = "" });
        }

        ComboBoxItem? selected = null;
        foreach (var pack in _runtime!.PacksForPicker(selectedId))
        {
            var item = new ComboBoxItem { Content = pack.Name, Tag = pack.Id };
            box.Items.Add(item);
            if (pack.Id.Equals(selectedId, StringComparison.OrdinalIgnoreCase))
            {
                selected = item;
            }
        }

        box.SelectedItem = selected ?? (includeNone ? box.Items[0] : box.Items.Count > 0 ? box.Items[0] : null);
    }

    private static bool UsesCustomSample(ProfileDocument? profile)
    {
        if (profile is null)
        {
            return false;
        }

        if (profile.PrimaryPackId.Equals(CustomSampleLibrary.PackId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return profile.Overlays.Any(o => o.PackId.Equals(CustomSampleLibrary.PackId, StringComparison.OrdinalIgnoreCase));
    }

    private void FillCustomSoundCombo(ProfileDocument? profile)
    {
        if (_runtime is null)
        {
            return;
        }

        var show = UsesCustomSample(profile);
        CustomSoundPanel.IsVisible = show;
        CustomSoundBox.Items.Clear();
        if (!show)
        {
            return;
        }

        var files = CustomSampleLibrary.ListFiles(_runtime.Paths);
        if (files.Count == 0)
        {
            CustomSoundBox.Items.Add(new ComboBoxItem { Content = "(no files in the custom folder)", Tag = "" });
            CustomSoundBox.SelectedIndex = 0;
            CustomSoundBox.IsEnabled = false;
            return;
        }

        CustomSoundBox.IsEnabled = !_runtime.PianoMode;
        var armed = Path.GetFileName(CustomSampleLibrary.ResolveArmed(_runtime.Paths, _runtime.Settings.ArmedSampleFile) ?? "");
        ComboBoxItem? selected = null;
        foreach (var path in files)
        {
            var name = Path.GetFileName(path);
            var item = new ComboBoxItem { Content = name, Tag = name };
            CustomSoundBox.Items.Add(item);
            if (name.Equals(armed, StringComparison.OrdinalIgnoreCase))
            {
                selected = item;
            }
        }

        CustomSoundBox.SelectedItem = selected ?? CustomSoundBox.Items[0];
    }

    private void OnCustomSoundChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncing || _runtime is null)
        {
            return;
        }

        if ((CustomSoundBox.SelectedItem as ComboBoxItem)?.Tag is string file && file.Length > 0)
        {
            _runtime.ArmSample(file);
        }
    }

    private void OnPrimaryPackChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncing || _runtime is null)
        {
            return;
        }

        if ((PrimaryPackBox.SelectedItem as ComboBoxItem)?.Tag is string id && id.Length > 0)
        {
            _runtime.SetPrimaryPack(id);
            FillCustomSoundCombo(_runtime.ActiveProfile);
            LoadBehavior();
        }
    }

    private void OnVirtualRoomChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncing || _runtime is null)
        {
            return;
        }

        var id = ((sender as ComboBox)?.SelectedItem as ComboBoxItem)?.Tag as string;
        if (string.IsNullOrWhiteSpace(id) ||
            id.Equals(_runtime.ActiveProfile?.VirtualRoomId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            _runtime.ApplyVirtualRoom(id);
        }
        catch (Exception ex)
        {
            _ = MessageDialog.Alert(this, "Could not change virtual room", ex.Message);
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            LoadFx();
            RefreshSaveButtons();
        });
    }

    private void FillRoomCombo(ComboBox box, string? selectedId)
    {
        box.SelectionChanged -= OnVirtualRoomChanged;
        if (box.Items.Count != VirtualRoomCatalog.Rooms.Length)
        {
            box.Items.Clear();
            foreach (var (id, name) in VirtualRoomCatalog.Rooms)
            {
                box.Items.Add(new ComboBoxItem { Content = name, Tag = id });
            }
        }

        ComboBoxItem? selected = null;
        var wanted = VirtualRoomCatalog.MapId(selectedId);
        foreach (var item in box.Items)
        {
            if (item is ComboBoxItem combo && combo.Tag as string == wanted)
            {
                selected = combo;
                break;
            }
        }

        box.SelectedItem = selected ?? (box.Items.Count > 0 ? box.Items[0] : null);
        box.SelectionChanged += OnVirtualRoomChanged;
    }

    private void OnOverlayPackChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncing || _runtime?.ActiveProfile is not { } profile)
        {
            return;
        }

        var id = (OverlayPackBox.SelectedItem as ComboBoxItem)?.Tag as string;
        profile.Overlays = string.IsNullOrWhiteSpace(id)
            ? []
            :
            [
                new ProfileOverlay { PackId = id, Keys = ["Enter", "Escape", "Space"] }
            ];
        _runtime.NotifyWorkingChanged();
        FillCustomSoundCombo(profile);
    }

    private void LoadBehavior()
    {
        if (_runtime?.ActiveProfile is not { } profile)
        {
            return;
        }

        BeginUiLoad();
        BehaviorHint.Text = "When keys fire. Silence groups can be combined. Changes are live until you Save.";
        SelectTag(RepeatBox, profile.Behavior.Repeat.ToString());
        SelectTag(PlayOnBox, profile.Behavior.PlayOn.ToString());
        SelectTag(ModifierBox, profile.Behavior.ModifierPolicy.ToString());
        PolyphonySlider.Value = profile.Behavior.Polyphony;
        PolyphonyLabel.Text = profile.Behavior.Polyphony.ToString();
        BehaviorVelocitySlider.Value = profile.Behavior.VelocityRandom * 100;
        BehaviorVelocityLabel.Text = $"{(int)BehaviorVelocitySlider.Value}%";
        HoldSustainBox.IsChecked = profile.Behavior.HoldSustain;
        ReleaseSlider.Value = Math.Clamp(profile.Behavior.ReleaseMs, 40, 1200);
        ReleaseLabel.Text = $"{(int)ReleaseSlider.Value} ms";
        SilenceUnmappedBox.IsChecked = profile.Behavior.SilenceUnmapped;
        SilentFunctionBox.IsChecked = HasSilentGroup(profile, "function");
        SilentModifiersBox.IsChecked = HasSilentGroup(profile, "modifiers");
        SilentNumpadBox.IsChecked = HasSilentGroup(profile, "numpad");
        SilentNavigationBox.IsChecked = HasSilentGroup(profile, "navigation");
        SilentKeysBox.Text = string.Join(", ", profile.Behavior.SilentKeys);
        RepeatBox.SelectionChanged -= OnBehaviorChanged;
        PlayOnBox.SelectionChanged -= OnBehaviorChanged;
        ModifierBox.SelectionChanged -= OnBehaviorChanged;
        RepeatBox.SelectionChanged += OnBehaviorChanged;
        PlayOnBox.SelectionChanged += OnBehaviorChanged;
        ModifierBox.SelectionChanged += OnBehaviorChanged;
        PolyphonySlider.PropertyChanged -= OnBehaviorSlider;
        BehaviorVelocitySlider.PropertyChanged -= OnBehaviorSlider;
        PolyphonySlider.PropertyChanged += OnBehaviorSlider;
        BehaviorVelocitySlider.PropertyChanged += OnBehaviorSlider;
        ReleaseSlider.PropertyChanged -= OnBehaviorSlider;
        ReleaseSlider.PropertyChanged += OnBehaviorSlider;
        HoldSustainBox.IsCheckedChanged -= OnBehaviorChangedRouted;
        HoldSustainBox.IsCheckedChanged += OnBehaviorChangedRouted;
        SilenceUnmappedBox.IsCheckedChanged -= OnBehaviorChangedRouted;
        SilentFunctionBox.IsCheckedChanged -= OnBehaviorChangedRouted;
        SilentModifiersBox.IsCheckedChanged -= OnBehaviorChangedRouted;
        SilentNumpadBox.IsCheckedChanged -= OnBehaviorChangedRouted;
        SilentNavigationBox.IsCheckedChanged -= OnBehaviorChangedRouted;
        SilenceUnmappedBox.IsCheckedChanged += OnBehaviorChangedRouted;
        SilentFunctionBox.IsCheckedChanged += OnBehaviorChangedRouted;
        SilentModifiersBox.IsCheckedChanged += OnBehaviorChangedRouted;
        SilentNumpadBox.IsCheckedChanged += OnBehaviorChangedRouted;
        SilentNavigationBox.IsCheckedChanged += OnBehaviorChangedRouted;
        EndUiLoad();
    }

    private static void SelectTag(ComboBox box, string tag)
    {
        foreach (var item in box.Items)
        {
            if (item is ComboBoxItem combo && combo.Tag as string == tag)
            {
                box.SelectedItem = combo;
                return;
            }
        }
    }

    private void OnBehaviorChanged(object? sender, SelectionChangedEventArgs e) => SaveBehavior();

    private void OnBehaviorChangedRouted(object? sender, RoutedEventArgs e) => SaveBehavior();

    private void OnSilentKeysLostFocus(object? sender, RoutedEventArgs e) => SaveBehavior();

    private void OnSilentKeysKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            SaveBehavior();
        }
    }

    private void OnBehaviorSlider(object? sender, Avalonia.AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Slider.ValueProperty)
        {
            PolyphonyLabel.Text = ((int)PolyphonySlider.Value).ToString();
            BehaviorVelocityLabel.Text = $"{(int)BehaviorVelocitySlider.Value}%";
            ReleaseLabel.Text = $"{(int)ReleaseSlider.Value} ms";
            SaveBehavior();
        }
    }

    private void SaveBehavior()
    {
        if (_syncing || _runtime?.ActiveProfile is not { } profile)
        {
            return;
        }

        profile.Behavior.Repeat = ParseEnum((RepeatBox.SelectedItem as ComboBoxItem)?.Tag as string, RepeatMode.Off);
        profile.Behavior.PlayOn = ParseEnum((PlayOnBox.SelectedItem as ComboBoxItem)?.Tag as string, PlayOn.Down);
        profile.Behavior.ModifierPolicy = ParseEnum((ModifierBox.SelectedItem as ComboBoxItem)?.Tag as string, ModifierPolicy.Ignore);
        profile.Behavior.Polyphony = Math.Clamp((int)PolyphonySlider.Value, 1, 64);
        profile.Behavior.VelocityRandom = (float)(BehaviorVelocitySlider.Value / 100.0);
        profile.Behavior.HoldSustain = HoldSustainBox.IsChecked == true;
        profile.Behavior.ReleaseMs = Math.Clamp((float)ReleaseSlider.Value, 40, 1200);
        profile.Behavior.SilenceUnmapped = SilenceUnmappedBox.IsChecked == true;
        profile.Behavior.SilentGroups = CollectSilentGroups();
        profile.Behavior.SilentKeys = ParseSilentKeys(SilentKeysBox.Text);
        _runtime.NotifyWorkingChanged();
        VelocitySlider.Value = profile.Behavior.VelocityRandom * 100;
        VelocityLabel.Text = $"{(int)VelocitySlider.Value}%";
    }

    private void LoadFx()
    {
        if (_runtime?.ActiveProfile is not { } profile)
        {
            return;
        }

        BeginUiLoad();
        FxProfileName.Text = profile.Name;
        FxHint.Text = profile.FxLocked
            ? $"{profile.Name} has no effects. Save as to make an editable copy. The limiter stays on."
            : profile.IsFactory
            ? $"Editing {profile.Name} (system). Save as to keep tweaks. This template cannot be overwritten."
            : $"Editing {profile.Name}. Save writes packs, behavior, and effects. Reset restores the last save.";
        FillRoomCombo(FxRoomBox, profile.VirtualRoomId);
        var fx = profile.Fx;
        RoomVolumeSlider.Value = profile.Output.MasterVolume * 100;
        InputGainSlider.Value = fx.InputGainDb;
        EqBox.IsChecked = fx.Eq.Enabled;
        BassSlider.Value = fx.Eq.BassDb;
        AirSlider.Value = fx.Eq.AirDb;
        DynBassBox.IsChecked = fx.DynamicBass.Enabled;
        DynBassSlider.Value = fx.DynamicBass.Mix * 100;
        CompBox.IsChecked = fx.Compressor.Enabled;
        ThresholdSlider.Value = fx.Compressor.ThresholdDb;
        RatioSlider.Value = fx.Compressor.Ratio;
        MakeupSlider.Value = fx.Compressor.MakeupDb;
        SatBox.IsChecked = fx.Saturation.Enabled;
        SelectTag(SatStyleBox, fx.Saturation.Style is "Crush" ? "Crush" : "Tape");
        DriveSlider.Value = fx.Saturation.Drive * 100;
        SatMixSlider.Value = fx.Saturation.Mix * 100;
        ChorusBox.IsChecked = fx.Chorus.Enabled;
        ChorusMixSlider.Value = fx.Chorus.Mix * 100;
        ChorusRateSlider.Value = fx.Chorus.RateHz * 100;
        ChorusDepthSlider.Value = fx.Chorus.Depth * 100;
        FlangerBox.IsChecked = fx.Flanger.Enabled;
        FlangerMixSlider.Value = fx.Flanger.Mix * 100;
        FlangerRateSlider.Value = fx.Flanger.RateHz * 100;
        FlangerDepthSlider.Value = fx.Flanger.Depth * 100;
        FlangerFeedbackSlider.Value = fx.Flanger.Feedback * 100;
        PhaserBox.IsChecked = fx.Phaser.Enabled;
        PhaserMixSlider.Value = fx.Phaser.Mix * 100;
        PhaserRateSlider.Value = fx.Phaser.RateHz * 100;
        PhaserDepthSlider.Value = fx.Phaser.Depth * 100;
        DelayBox.IsChecked = fx.Delay.Enabled;
        DelayTimeSlider.Value = fx.Delay.TimeMs;
        DelayFeedbackSlider.Value = fx.Delay.Feedback * 100;
        DelayMixSlider.Value = fx.Delay.Mix * 100;
        ConvBox.IsChecked = fx.Convolver.Enabled;
        SelectTag(ConvIrBox, fx.Convolver.Ir is "Medium" ? "Medium" : "Short");
        ConvMixSlider.Value = fx.Convolver.Mix * 100;
        ReverbBox.IsChecked = fx.Reverb.Enabled;
        DecaySlider.Value = fx.Reverb.Decay * 100;
        DampingSlider.Value = fx.Reverb.Damping * 100;
        ReverbMixSlider.Value = fx.Reverb.Mix * 100;
        WidthBox.IsChecked = fx.Width.Enabled;
        WidthSlider.Value = fx.Width.Mix * 100;
        CrossfeedBox.IsChecked = fx.Crossfeed.Enabled;
        CrossfeedSlider.Value = fx.Crossfeed.Mix * 100;
        UpdateFxLabels();
        HookFxEvents();
        ApplyFxEnablement();
        EndUiLoad();
    }

    private void HookFxEvents()
    {
        EqBox.IsCheckedChanged -= OnFxChanged;
        DynBassBox.IsCheckedChanged -= OnFxChanged;
        CompBox.IsCheckedChanged -= OnFxChanged;
        SatBox.IsCheckedChanged -= OnFxChanged;
        ChorusBox.IsCheckedChanged -= OnFxChanged;
        FlangerBox.IsCheckedChanged -= OnFxChanged;
        PhaserBox.IsCheckedChanged -= OnFxChanged;
        DelayBox.IsCheckedChanged -= OnFxChanged;
        ConvBox.IsCheckedChanged -= OnFxChanged;
        ReverbBox.IsCheckedChanged -= OnFxChanged;
        WidthBox.IsCheckedChanged -= OnFxChanged;
        CrossfeedBox.IsCheckedChanged -= OnFxChanged;
        EqBox.IsCheckedChanged += OnFxChanged;
        DynBassBox.IsCheckedChanged += OnFxChanged;
        CompBox.IsCheckedChanged += OnFxChanged;
        SatBox.IsCheckedChanged += OnFxChanged;
        ChorusBox.IsCheckedChanged += OnFxChanged;
        FlangerBox.IsCheckedChanged += OnFxChanged;
        PhaserBox.IsCheckedChanged += OnFxChanged;
        DelayBox.IsCheckedChanged += OnFxChanged;
        ConvBox.IsCheckedChanged += OnFxChanged;
        ReverbBox.IsCheckedChanged += OnFxChanged;
        WidthBox.IsCheckedChanged += OnFxChanged;
        CrossfeedBox.IsCheckedChanged += OnFxChanged;
        SatStyleBox.SelectionChanged -= OnFxSelectionChanged;
        SatStyleBox.SelectionChanged += OnFxSelectionChanged;
        ConvIrBox.SelectionChanged -= OnFxSelectionChanged;
        ConvIrBox.SelectionChanged += OnFxSelectionChanged;
        foreach (var slider in FxSliders())
        {
            slider.PropertyChanged -= OnFxSlider;
            slider.PropertyChanged += OnFxSlider;
        }
    }

    private void ApplyFxEnablement()
    {
        var locked = _runtime?.ActiveProfile?.FxLocked == true;
        var allow = !locked;
        RoomVolumeSlider.IsEnabled = true;
        FxRoomBox.IsEnabled = allow;
        InputGainSlider.IsEnabled = allow;
        EqBox.IsEnabled = allow;
        EqChildren.IsEnabled = allow && EqBox.IsChecked == true;
        DynBassBox.IsEnabled = allow;
        DynBassChildren.IsEnabled = allow && DynBassBox.IsChecked == true;
        CompBox.IsEnabled = allow;
        CompChildren.IsEnabled = allow && CompBox.IsChecked == true;
        SatBox.IsEnabled = allow;
        SatChildren.IsEnabled = allow && SatBox.IsChecked == true;
        ChorusBox.IsEnabled = allow;
        ChorusChildren.IsEnabled = allow && ChorusBox.IsChecked == true;
        FlangerBox.IsEnabled = allow;
        FlangerChildren.IsEnabled = allow && FlangerBox.IsChecked == true;
        PhaserBox.IsEnabled = allow;
        PhaserChildren.IsEnabled = allow && PhaserBox.IsChecked == true;
        DelayBox.IsEnabled = allow;
        DelayChildren.IsEnabled = allow && DelayBox.IsChecked == true;
        ConvBox.IsEnabled = allow;
        ConvChildren.IsEnabled = allow && ConvBox.IsChecked == true;
        ReverbBox.IsEnabled = allow;
        ReverbChildren.IsEnabled = allow && ReverbBox.IsChecked == true;
        WidthBox.IsEnabled = allow;
        WidthChildren.IsEnabled = allow && WidthBox.IsChecked == true;
        CrossfeedBox.IsEnabled = allow;
        CrossfeedChildren.IsEnabled = allow && CrossfeedBox.IsChecked == true;
    }

    private IEnumerable<Slider> FxSliders()
    {
        yield return RoomVolumeSlider;
        yield return InputGainSlider;
        yield return BassSlider;
        yield return AirSlider;
        yield return ThresholdSlider;
        yield return RatioSlider;
        yield return MakeupSlider;
        yield return DriveSlider;
        yield return SatMixSlider;
        yield return DynBassSlider;
        yield return ChorusMixSlider;
        yield return ChorusRateSlider;
        yield return ChorusDepthSlider;
        yield return FlangerMixSlider;
        yield return FlangerRateSlider;
        yield return FlangerDepthSlider;
        yield return FlangerFeedbackSlider;
        yield return PhaserMixSlider;
        yield return PhaserRateSlider;
        yield return PhaserDepthSlider;
        yield return DelayTimeSlider;
        yield return DelayFeedbackSlider;
        yield return DelayMixSlider;
        yield return ConvMixSlider;
        yield return DecaySlider;
        yield return DampingSlider;
        yield return ReverbMixSlider;
        yield return WidthSlider;
        yield return CrossfeedSlider;
    }

    private void OnFxChanged(object? sender, RoutedEventArgs e)
    {
        ApplyFxEnablement();
        SaveFx();
    }

    private void OnFxSelectionChanged(object? sender, SelectionChangedEventArgs e) => SaveFx();

    private void OnFxSlider(object? sender, Avalonia.AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Slider.ValueProperty)
        {
            UpdateFxLabels();
            SaveFx();
        }
    }

    private void UpdateFxLabels()
    {
        RoomVolumeLabel.Text = $"{(int)RoomVolumeSlider.Value}%";
        InputGainLabel.Text = $"{InputGainSlider.Value:0.0} dB";
        BassLabel.Text = $"{BassSlider.Value:0.0} dB";
        AirLabel.Text = $"{AirSlider.Value:0.0} dB";
        ThresholdLabel.Text = $"{ThresholdSlider.Value:0} dB";
        RatioLabel.Text = $"{RatioSlider.Value:0.0}:1";
        MakeupLabel.Text = $"{MakeupSlider.Value:0.0} dB";
        DriveLabel.Text = $"{(int)DriveSlider.Value}%";
        SatMixLabel.Text = $"{(int)SatMixSlider.Value}%";
        DynBassLabel.Text = $"{(int)DynBassSlider.Value}%";
        ChorusMixLabel.Text = $"{(int)ChorusMixSlider.Value}%";
        ChorusRateLabel.Text = $"{ChorusRateSlider.Value / 100:0.00} Hz";
        ChorusDepthLabel.Text = $"{(int)ChorusDepthSlider.Value}%";
        FlangerMixLabel.Text = $"{(int)FlangerMixSlider.Value}%";
        FlangerRateLabel.Text = $"{FlangerRateSlider.Value / 100:0.00} Hz";
        FlangerDepthLabel.Text = $"{(int)FlangerDepthSlider.Value}%";
        FlangerFeedbackLabel.Text = $"{(int)FlangerFeedbackSlider.Value}%";
        PhaserMixLabel.Text = $"{(int)PhaserMixSlider.Value}%";
        PhaserRateLabel.Text = $"{PhaserRateSlider.Value / 100:0.00} Hz";
        PhaserDepthLabel.Text = $"{(int)PhaserDepthSlider.Value}%";
        DelayTimeLabel.Text = $"{(int)DelayTimeSlider.Value} ms";
        DelayFeedbackLabel.Text = $"{(int)DelayFeedbackSlider.Value}%";
        DelayMixLabel.Text = $"{(int)DelayMixSlider.Value}%";
        ConvMixLabel.Text = $"{(int)ConvMixSlider.Value}%";
        DecayLabel.Text = $"{(int)DecaySlider.Value}%";
        DampingLabel.Text = $"{(int)DampingSlider.Value}%";
        ReverbMixLabel.Text = $"{(int)ReverbMixSlider.Value}%";
        WidthLabel.Text = $"{(int)WidthSlider.Value}%";
        CrossfeedLabel.Text = $"{(int)CrossfeedSlider.Value}%";
    }

    private void SaveFx()
    {
        if (_syncing || _runtime?.ActiveProfile is not { } profile)
        {
            return;
        }

        profile.Output.MasterVolume = (float)(RoomVolumeSlider.Value / 100.0);
        if (profile.FxLocked)
        {
            _runtime.NotifyWorkingChanged();
            return;
        }

        profile.Silent = false;
        profile.Output.MasterVolume = (float)(RoomVolumeSlider.Value / 100.0);
        profile.Fx.InputGainDb = (float)InputGainSlider.Value;
        profile.Fx.Eq.Enabled = EqBox.IsChecked == true;
        profile.Fx.Eq.BassDb = (float)BassSlider.Value;
        profile.Fx.Eq.AirDb = (float)AirSlider.Value;
        profile.Fx.Compressor.Enabled = CompBox.IsChecked == true;
        profile.Fx.Compressor.ThresholdDb = (float)ThresholdSlider.Value;
        profile.Fx.Compressor.Ratio = (float)RatioSlider.Value;
        profile.Fx.Compressor.MakeupDb = (float)MakeupSlider.Value;
        profile.Fx.Saturation.Enabled = SatBox.IsChecked == true;
        profile.Fx.Saturation.Style = (SatStyleBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "Tape";
        profile.Fx.Saturation.Drive = (float)(DriveSlider.Value / 100.0);
        profile.Fx.Saturation.Mix = (float)(SatMixSlider.Value / 100.0);
        profile.Fx.DynamicBass.Enabled = DynBassBox.IsChecked == true;
        profile.Fx.DynamicBass.Mix = (float)(DynBassSlider.Value / 100.0);
        profile.Fx.Chorus.Enabled = ChorusBox.IsChecked == true;
        profile.Fx.Chorus.Mix = (float)(ChorusMixSlider.Value / 100.0);
        profile.Fx.Chorus.RateHz = (float)(ChorusRateSlider.Value / 100.0);
        profile.Fx.Chorus.Depth = (float)(ChorusDepthSlider.Value / 100.0);
        profile.Fx.Flanger.Enabled = FlangerBox.IsChecked == true;
        profile.Fx.Flanger.Mix = (float)(FlangerMixSlider.Value / 100.0);
        profile.Fx.Flanger.RateHz = (float)(FlangerRateSlider.Value / 100.0);
        profile.Fx.Flanger.Depth = (float)(FlangerDepthSlider.Value / 100.0);
        profile.Fx.Flanger.Feedback = (float)(FlangerFeedbackSlider.Value / 100.0);
        profile.Fx.Phaser.Enabled = PhaserBox.IsChecked == true;
        profile.Fx.Phaser.Mix = (float)(PhaserMixSlider.Value / 100.0);
        profile.Fx.Phaser.RateHz = (float)(PhaserRateSlider.Value / 100.0);
        profile.Fx.Phaser.Depth = (float)(PhaserDepthSlider.Value / 100.0);
        profile.Fx.Delay.Enabled = DelayBox.IsChecked == true;
        profile.Fx.Delay.TimeMs = (float)DelayTimeSlider.Value;
        profile.Fx.Delay.Feedback = (float)(DelayFeedbackSlider.Value / 100.0);
        profile.Fx.Delay.Mix = (float)(DelayMixSlider.Value / 100.0);
        profile.Fx.Convolver.Enabled = ConvBox.IsChecked == true;
        profile.Fx.Convolver.Ir = (ConvIrBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "Short";
        profile.Fx.Convolver.Mix = (float)(ConvMixSlider.Value / 100.0);
        profile.Fx.Reverb.Enabled = ReverbBox.IsChecked == true;
        profile.Fx.Reverb.Decay = (float)(DecaySlider.Value / 100.0);
        profile.Fx.Reverb.Damping = (float)(DampingSlider.Value / 100.0);
        profile.Fx.Reverb.Mix = (float)(ReverbMixSlider.Value / 100.0);
        profile.Fx.Width.Enabled = WidthBox.IsChecked == true;
        profile.Fx.Width.Mix = (float)(WidthSlider.Value / 100.0);
        profile.Fx.Crossfeed.Enabled = CrossfeedBox.IsChecked == true;
        profile.Fx.Crossfeed.Mix = (float)(CrossfeedSlider.Value / 100.0);
        _runtime.NotifyWorkingChanged();
    }

    private static bool HasSilentGroup(ProfileDocument profile, string group) =>
        profile.Behavior.SilentGroups.Any(g => g.Equals(group, StringComparison.OrdinalIgnoreCase));

    private List<string> CollectSilentGroups()
    {
        var groups = new List<string>();
        if (SilentFunctionBox.IsChecked == true)
        {
            groups.Add("function");
        }

        if (SilentModifiersBox.IsChecked == true)
        {
            groups.Add("modifiers");
        }

        if (SilentNumpadBox.IsChecked == true)
        {
            groups.Add("numpad");
        }

        if (SilentNavigationBox.IsChecked == true)
        {
            groups.Add("navigation");
        }

        return groups;
    }

    private static List<string> ParseSilentKeys(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        return text
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void RefreshInstrumentUi()
    {
        if (_runtime is null)
        {
            return;
        }

        var on = _runtime.PianoMode;
        PianoOnBanner.IsVisible = on;
        InstrumentsToggleButton.Content = on ? "Stop piano" : "Start piano";
        InstrumentsStatus.Text = on
            ? "Piano is on. Closing the map does not stop it — only Stop does."
            : "Start piano to play the computer keyboard as an instrument. Your profile packs stay until you Stop. Closing the map does not stop piano.";
        PrimaryPackBox.IsEnabled = !on;
        OverlayPackBox.IsEnabled = !on;
        if (CustomSoundPanel.IsVisible)
        {
            CustomSoundBox.IsEnabled = !on && CustomSampleLibrary.ListFiles(_runtime.Paths).Count > 0;
        }
    }

    private void OnTogglePiano(object? sender, RoutedEventArgs e)
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
        ShowPianoMap();
    }

    private void OnStopPiano(object? sender, RoutedEventArgs e) => _runtime?.StopPiano();

    public void ShowPianoMap()
    {
        if (_runtime is null)
        {
            return;
        }

        if (_pianoMap is not null)
        {
            _pianoMap.Rebuild(_runtime.Engine.OctaveShift);
            _pianoMap.Show();
            _pianoMap.Activate();
            return;
        }

        _pianoMap = new PianoMapWindow(_runtime);
        _pianoMap.Closed += (_, _) => _pianoMap = null;
        _pianoMap.Show();
    }

    private void OnOpenPianoMap(object? sender, RoutedEventArgs e) => ShowPianoMap();

    private async void OnCheckForUpdates(object? sender, RoutedEventArgs e)
    {
        UpdateStatusText.Text = "Checking for updates…";
        var result = await AppUpdateService.CheckAndOfferAsync(async (title, body, confirm) =>
            await MessageDialog.Confirm(this, title, body, confirm));

        if (result.Status == UpdateCheckStatus.Applied)
        {
            return;
        }

        UpdateStatusText.Text = result.Message ?? "";
        if (result.Status is UpdateCheckStatus.Failed or UpdateCheckStatus.NotInstalled or UpdateCheckStatus.NotConfigured)
        {
            await MessageDialog.Alert(this, "Updates", result.Message ?? "Could not check for updates.");
        }
        else if (result.Status == UpdateCheckStatus.UpToDate)
        {
            await MessageDialog.Alert(this, "Updates", result.Message ?? "You are up to date.");
        }
    }

    private async void OnRemoveLocalData(object? sender, RoutedEventArgs e)
    {
        if (_runtime is null)
        {
            return;
        }

        if (!await MessageDialog.Confirm(
                this,
                "Remove local data?",
                "This deletes profiles, packs, custom samples, and settings in AppData. The installed program stays until you uninstall Key FX Board from Windows Settings. The app will quit.",
                "Remove and quit"))
        {
            return;
        }

        try
        {
            _runtime.RemoveLocalData();
        }
        catch (Exception ex)
        {
            await MessageDialog.Alert(this, "Could not remove data", ex.Message);
            return;
        }

        AllowClose = true;
        _quit?.Invoke();
    }

    private void OnOpenGettingStarted(object? sender, RoutedEventArgs e) =>
        OpenBesideExe("GettingStarted.html", "Getting started");

    private void OnOpenLicense(object? sender, RoutedEventArgs e) =>
        OpenBesideExe("LICENSE.txt", "LICENSE");

    private void OnOpenThirdParty(object? sender, RoutedEventArgs e) =>
        OpenBesideExe("THIRD_PARTY_NOTICES.txt", "THIRD_PARTY_NOTICES");

    private void OpenBesideExe(string fileName, string fallbackTitle)
    {
        var path = Path.Combine(AppContext.BaseDirectory, fileName);
        if (!File.Exists(path))
        {
            _ = MessageDialog.Alert(this, fallbackTitle, $"{fileName} was not found next to the app.");
            return;
        }

        Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
    }

    private async void OnResetAppSettings(object? sender, RoutedEventArgs e)
    {
        if (_runtime is null)
        {
            return;
        }

        if (!await MessageDialog.Confirm(
                this,
                "Reset app settings?",
                "Restore volume, boost, mute, output device, autostart, and tray options to defaults. Profiles and packs stay as they are."))
        {
            return;
        }

        _runtime.ResetAppSettings();
        LoadFromRuntime();
    }

    private static T ParseEnum<T>(string? value, T fallback) where T : struct, Enum =>
        Enum.TryParse<T>(value, out var parsed) ? parsed : fallback;

    private void OnQuit(object? sender, RoutedEventArgs e) => _quit?.Invoke();

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (AllowClose || _runtime?.Settings.MinimizeToTrayOnClose == false)
        {
            return;
        }

        e.Cancel = true;
        Hide();
    }

    private static void SetStatus(TextBlock block, string? error, bool ok, string label, string? extra = null)
    {
        if (error is not null)
        {
            block.Text = $"{label}: failed — {error}";
            block.Foreground = Brushes.IndianRed;
            return;
        }

        block.Text = extra is null
            ? $"{label}: {(ok ? "ok" : "not running")}"
            : $"{label}: {(ok ? "ok" : "not running")} · {extra}";
        block.Foreground = ok ? Brushes.LightGreen : Brushes.Orange;
    }
}
