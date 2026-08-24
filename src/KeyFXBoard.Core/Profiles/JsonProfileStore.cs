using System.Text.Json;
using KeyFXBoard.Core.Abstractions;
using KeyFXBoard.Core.Packs;
using KeyFXBoard.Core.Storage;

namespace KeyFXBoard.Core.Profiles;

public sealed class JsonProfileStore
{
    private readonly IAppPaths _paths;

    public JsonProfileStore(IAppPaths paths)
    {
        _paths = paths;
        Directory.CreateDirectory(_paths.ProfilesDirectory);
    }

    public IReadOnlyList<ProfileDocument> List()
    {
        var list = new List<ProfileDocument>();
        if (!Directory.Exists(_paths.ProfilesDirectory))
        {
            return list;
        }

        foreach (var file in Directory.GetFiles(_paths.ProfilesDirectory, "*.json"))
        {
            try
            {
                list.Add(Read(file));
            }
            catch (Exception)
            {
                var bad = file + ".bad";
                File.Copy(file, bad, overwrite: true);
            }
        }

        return list
            .OrderByDescending(p => p.IsFactory)
            .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public ProfileDocument? Get(string id) =>
        List().FirstOrDefault(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    public void Save(ProfileDocument profile, bool allowFactory = false)
    {
        if (profile.IsFactory && !allowFactory)
        {
            throw new InvalidOperationException("System profiles cannot be saved. Save as a new profile to keep changes.");
        }

        Directory.CreateDirectory(_paths.ProfilesDirectory);
        var path = Path.Combine(_paths.ProfilesDirectory, profile.Id + ".json");
        var json = JsonSerializer.Serialize(profile, JsonOptions.File);
        var temp = path + ".tmp";
        File.WriteAllText(temp, json);
        File.Copy(temp, path, overwrite: true);
        File.Delete(temp);
    }

    public void Delete(string id)
    {
        var profile = Get(id);
        if (profile is null)
        {
            return;
        }

        if (profile.IsFactory)
        {
            throw new InvalidOperationException("Factory profiles cannot be deleted.");
        }

        var path = Path.Combine(_paths.ProfilesDirectory, id + ".json");
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public ProfileDocument Duplicate(ProfileDocument source, string name)
    {
        var copy = JsonSerializer.Deserialize<ProfileDocument>(
            JsonSerializer.Serialize(source, JsonOptions.File), JsonOptions.File)
            ?? throw new InvalidOperationException("Could not duplicate the profile.");
        copy.Id = "user-" + Guid.NewGuid().ToString("N")[..12];
        copy.Name = name;
        copy.IsFactory = false;
        copy.FxLocked = false;
        copy.BasedOn = source.Id;
        Save(copy);
        return copy;
    }

    public void RewritePackReferences(string removedPackId, string fallbackPackId)
    {
        foreach (var profile in List())
        {
            var changed = false;
            if (profile.PrimaryPackId.Equals(removedPackId, StringComparison.OrdinalIgnoreCase))
            {
                profile.PrimaryPackId = fallbackPackId;
                changed = true;
            }

            var remaining = profile.Overlays
                .Where(o => !o.PackId.Equals(removedPackId, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (remaining.Count != profile.Overlays.Count)
            {
                profile.Overlays = remaining;
                changed = true;
            }

            if (changed && !profile.IsFactory)
            {
                Save(profile);
            }
        }
    }

    private static ProfileDocument Read(string file)
    {
        var json = File.ReadAllText(file);
        return JsonSerializer.Deserialize<ProfileDocument>(json, JsonOptions.File)
               ?? throw new JsonException("Empty profile.");
    }
}
