namespace KeyFXBoard.Core.Abstractions;

public interface IAppPaths
{
    string Root { get; }
    string SettingsFile { get; }
    string PacksDirectory { get; }
    string CustomSamplesDirectory { get; }
    string ExamplesDirectory { get; }
    string PendingInstallFile { get; }
    string ProfilesDirectory { get; }
}
