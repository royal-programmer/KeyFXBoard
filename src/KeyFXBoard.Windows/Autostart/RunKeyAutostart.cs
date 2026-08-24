using KeyFXBoard.Core.Abstractions;
using Microsoft.Win32;

namespace KeyFXBoard.Windows.Autostart;

public sealed class RunKeyAutostart : IAutostart
{
    private const string RunPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "KeyFXBoard";

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunPath, writable: false);
        return key?.GetValue(ValueName) is string;
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunPath, writable: true)
                        ?? Registry.CurrentUser.CreateSubKey(RunPath);
        if (key is null)
        {
            throw new InvalidOperationException("Could not open the current-user Run key.");
        }

        if (!enabled)
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
            return;
        }

        var exe = Environment.ProcessPath
                  ?? Path.Combine(AppContext.BaseDirectory, "KeyFXBoard.exe");
        key.SetValue(ValueName, $"\"{exe}\"");
    }
}
