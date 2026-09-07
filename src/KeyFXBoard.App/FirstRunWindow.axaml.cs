using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using KeyFXBoard.App.Services;

namespace KeyFXBoard.App;

public partial class FirstRunWindow : Window
{
    private readonly AppRuntime _runtime;

    public FirstRunWindow()
    {
        InitializeComponent();
        _runtime = null!;
    }

    public FirstRunWindow(AppRuntime runtime) : this()
    {
        _runtime = runtime;
        AutostartBox.IsChecked = runtime.Settings.Autostart;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Finish();
        }
    }

    private void OnDone(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Finish();

    private void OnOpenGuide(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        OpenUrl(ProductUrls.GettingStarted);

    private void Finish()
    {
        _runtime.CompleteFirstRun(AutostartBox.IsChecked == true);
        OpenUrl(ProductUrls.ThankYou);
        Close();
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch
        {
            // Best-effort; first-run should not fail if the browser cannot open.
        }
    }
}
