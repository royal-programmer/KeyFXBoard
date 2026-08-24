using Avalonia;
using KeyFXBoard.Core.Storage;
using KeyFXBoard.Windows.SingleInstance;
using Velopack;

namespace KeyFXBoard.App;

internal static class Program
{
    public static SingleInstanceGuard? Instance { get; private set; }
    public static string? StartupPackPath { get; set; }

    [STAThread]
    public static void Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            CrashLog.Write("UnhandledException", e.ExceptionObject as Exception ?? new Exception(e.ExceptionObject?.ToString()));
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            CrashLog.Write("UnobservedTask", e.Exception);
            e.SetObserved();
        };

        try
        {
            // Must be first: Velopack hooks for install / update / uninstall exit inside Run().
            VelopackApp.Build().Run();

            var packPath = ParsePackPath(args);
            if (!SingleInstanceGuard.TryStartPrimary(out var guard))
            {
                if (packPath is not null)
                {
                    var paths = new AppPaths();
                    File.WriteAllText(paths.PendingInstallFile, packPath);
                }

                return;
            }

            StartupPackPath = packPath;
            using (Instance = guard)
            {
                BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            }

            Instance = null;
        }
        catch (Exception ex)
        {
            CrashLog.Write("Main", ex);
            throw;
        }
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    public static string? ParsePackPath(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] is "--install-pack" && i + 1 < args.Length)
            {
                return Path.GetFullPath(args[i + 1]);
            }

            if (args[i].EndsWith(".kfxpack", StringComparison.OrdinalIgnoreCase) && File.Exists(args[i]))
            {
                return Path.GetFullPath(args[i]);
            }
        }

        return null;
    }
}
