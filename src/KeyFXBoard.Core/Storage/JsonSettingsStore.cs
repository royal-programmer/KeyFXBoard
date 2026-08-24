using System.Text.Json;
using KeyFXBoard.Core.Abstractions;

namespace KeyFXBoard.Core.Storage;

public sealed class JsonSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly IAppPaths _paths;

    public JsonSettingsStore(IAppPaths paths)
    {
        _paths = paths;
    }

    public AppSettings Load()
    {
        if (!File.Exists(_paths.SettingsFile))
        {
            return new AppSettings();
        }

        try
        {
            var json = File.ReadAllText(_paths.SettingsFile);
            var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            loaded.DisabledPackIds ??= [];
            return loaded;
        }
        catch (JsonException)
        {
            var bad = _paths.SettingsFile + ".bad";
            File.Copy(_paths.SettingsFile, bad, overwrite: true);
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(_paths.Root);
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        var temp = _paths.SettingsFile + ".tmp";
        File.WriteAllText(temp, json);
        File.Copy(temp, _paths.SettingsFile, overwrite: true);
        File.Delete(temp);
    }
}
