using Avalonia.Controls;
using Avalonia.Interactivity;
using KeyFXBoard.App.Services;

namespace KeyFXBoard.App;

public partial class PianoSettingsWindow : Window
{
    private readonly AppRuntime _runtime;
    private bool _syncing;

    public PianoSettingsWindow()
    {
        InitializeComponent();
        _runtime = null!;
    }

    public PianoSettingsWindow(AppRuntime runtime) : this()
    {
        _runtime = runtime;
        Load();
    }

    private void Load()
    {
        if (_runtime.ActiveProfile is not { } profile)
        {
            return;
        }

        _syncing = true;
        HoldSustainBox.IsChecked = profile.Behavior.HoldSustain;
        ReleaseSlider.Value = Math.Clamp(profile.Behavior.ReleaseMs, 40, 1200);
        ReleaseLabel.Text = $"{(int)ReleaseSlider.Value} ms";
        HoldSustainBox.IsCheckedChanged -= OnChanged;
        HoldSustainBox.IsCheckedChanged += OnChanged;
        ReleaseSlider.PropertyChanged -= OnSlider;
        ReleaseSlider.PropertyChanged += OnSlider;
        _syncing = false;
    }

    private void OnSlider(object? sender, Avalonia.AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != Slider.ValueProperty || _syncing)
        {
            return;
        }

        ReleaseLabel.Text = $"{(int)ReleaseSlider.Value} ms";
        Save();
    }

    private void OnChanged(object? sender, RoutedEventArgs e) => Save();

    private void Save()
    {
        if (_syncing || _runtime.ActiveProfile is not { } profile)
        {
            return;
        }

        profile.Behavior.HoldSustain = HoldSustainBox.IsChecked == true;
        profile.Behavior.ReleaseMs = Math.Clamp((float)ReleaseSlider.Value, 40, 1200);
        _runtime.NotifyWorkingChanged();
    }

    private void OnDone(object? sender, RoutedEventArgs e) => Close();
}
