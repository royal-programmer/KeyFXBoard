using Velopack;
using Velopack.Sources;

namespace KeyFXBoard.App.Services;

/// <summary>
/// App updates via Velopack. New Instruments and other app features ship as a new app version.
/// User AppData (profiles, packs, custom samples) stays across updates.
/// </summary>
public static class AppUpdateService
{
    public const string PackId = "KeyFXBoard";

    /// <summary>
    /// Set this to a GitHub repo URL (e.g. https://github.com/you/KeyFXBoard) when releases are published.
    /// Leave empty for local/dev builds — Check for updates explains that the feed is not configured.
    /// </summary>
    public static string UpdateFeedUrl { get; set; } = "https://github.com/royal-programmer/KeyFXBoard";

    public static string CurrentVersion =>
        typeof(AppUpdateService).Assembly.GetName().Version?.ToString(3) ?? "0.1.0";

    public static UpdateManager? TryCreateManager()
    {
        if (string.IsNullOrWhiteSpace(UpdateFeedUrl))
        {
            return null;
        }

        if (UpdateFeedUrl.Contains("github.com", StringComparison.OrdinalIgnoreCase))
        {
            return new UpdateManager(new GithubSource(UpdateFeedUrl.TrimEnd('/'), string.Empty, false));
        }

        return new UpdateManager(UpdateFeedUrl);
    }

    public static async Task<UpdateCheckResult> CheckAndOfferAsync(Func<string, string, string, Task<bool>> confirmAsync)
    {
        var mgr = TryCreateManager();
        if (mgr is null)
        {
            return new UpdateCheckResult(
                UpdateCheckStatus.NotConfigured,
                "Updates are not configured yet. When releases are published, set the feed URL and Check for updates will download new versions (including new Instruments).");
        }

        if (!mgr.IsInstalled)
        {
            return new UpdateCheckResult(
                UpdateCheckStatus.NotInstalled,
                "This copy was not installed with Setup.exe. Install from the Setup package to enable updates.");
        }

        try
        {
            var info = await mgr.CheckForUpdatesAsync();
            if (info is null)
            {
                return new UpdateCheckResult(
                    UpdateCheckStatus.UpToDate,
                    $"You are on the latest version ({CurrentVersion}).");
            }

            var target = info.TargetFullRelease.Version.ToString();
            var ok = await confirmAsync(
                "Update available",
                $"Version {target} is ready to download and install. The app will restart. Your profiles, packs, and custom samples stay.",
                "Update and restart");
            if (!ok)
            {
                return new UpdateCheckResult(UpdateCheckStatus.Cancelled, null);
            }

            await mgr.DownloadUpdatesAsync(info);
            mgr.ApplyUpdatesAndRestart(info);
            return new UpdateCheckResult(UpdateCheckStatus.Applied, null);
        }
        catch (Exception ex) when (ex.GetType().Name is "NotInstalledException")
        {
            return new UpdateCheckResult(
                UpdateCheckStatus.NotInstalled,
                "This copy was not installed with Setup.exe. Install from the Setup package to enable updates.");
        }
        catch (Exception ex)
        {
            return new UpdateCheckResult(UpdateCheckStatus.Failed, ex.Message);
        }
    }
}

public enum UpdateCheckStatus
{
    NotConfigured,
    NotInstalled,
    UpToDate,
    Cancelled,
    Applied,
    Failed
}

public sealed record UpdateCheckResult(UpdateCheckStatus Status, string? Message);
