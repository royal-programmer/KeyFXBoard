using KeyFXBoard.Core.Abstractions;

namespace KeyFXBoard.Core.Storage;

public sealed class AppPaths : IAppPaths
{
    public AppPaths(string? root = null)
    {
        Root = root ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "KeyFXBoard");
        Directory.CreateDirectory(Root);
    }

    public string Root { get; }
    public string SettingsFile => Path.Combine(Root, "settings.json");
    public string PacksDirectory => Path.Combine(Root, "packs");
    public string CustomSamplesDirectory => Path.Combine(Root, "custom-samples");
    public string ExamplesDirectory => Path.Combine(Root, "examples");
    public string PendingInstallFile => Path.Combine(Root, "pending-install.txt");
    public string ProfilesDirectory => Path.Combine(Root, "profiles");
}
