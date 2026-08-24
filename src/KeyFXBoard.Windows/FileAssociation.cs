using Microsoft.Win32;

namespace KeyFXBoard.Windows;

public static class FileAssociation
{
    public static void RegisterCurrentUser()
    {
        var exe = Environment.ProcessPath
                  ?? Path.Combine(AppContext.BaseDirectory, "KeyFXBoard.exe");

        using var ext = Registry.CurrentUser.CreateSubKey(@"Software\Classes\.kfxpack");
        ext.SetValue(null, "KeyFXBoard.Pack");

        using var type = Registry.CurrentUser.CreateSubKey(@"Software\Classes\KeyFXBoard.Pack");
        type.SetValue(null, "Key FX Board Pack");

        using var command = Registry.CurrentUser.CreateSubKey(@"Software\Classes\KeyFXBoard.Pack\shell\open\command");
        command.SetValue(null, $"\"{exe}\" --install-pack \"%1\"");
    }
}
