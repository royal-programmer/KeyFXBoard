using System.Text.Json;
using KeyFXBoard.Core.Abstractions;
using KeyFXBoard.Core.Packs;
using KeyFXBoard.Core.Storage;

namespace KeyFXBoard.Core.Profiles;

public static class FactoryProfileSeeder
{
    public const string DefaultId = "factory-default";

    public static readonly IReadOnlyDictionary<string, string> IdMigrations =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["factory-mechanical-tight"] = "factory-dry",
            ["factory-tight"] = "factory-dry",
            ["factory-piano-hall"] = "factory-reverb",
            ["factory-hall"] = "factory-reverb",
            ["factory-cinema-gun"] = "factory-bass",
            ["factory-punch"] = "factory-bass",
            ["factory-mixed-immersive"] = "factory-surround",
            ["factory-immersive"] = "factory-surround",
            ["factory-piano"] = DefaultId,
            ["factory-silent"] = DefaultId,
            ["factory-low-cpu"] = DefaultId
        };

    private static readonly HashSet<string> RetiredIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "factory-piano", "factory-silent", "factory-low-cpu"
    };

    public static string MapId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return DefaultId;
        }

        return IdMigrations.TryGetValue(id, out var mapped) ? mapped : id;
    }

    public static void Ensure(IAppPaths paths, JsonProfileStore store)
    {
        Directory.CreateDirectory(paths.ProfilesDirectory);
        MigrateLegacyIds(paths, store);
        RemoveRetired(paths);
        foreach (var profile in Catalog())
        {
            var path = Path.Combine(paths.ProfilesDirectory, profile.Id + ".json");
            if (!File.Exists(path))
            {
                store.Save(profile, allowFactory: true);
            }
        }
    }

    public static IEnumerable<ProfileDocument> Catalog()
    {
        var none = Base(DefaultId, "Default / No Effect", VirtualRoomCatalog.Default, silent: false, fx: VirtualRoomCatalog.CreateFx(VirtualRoomCatalog.Default));
        none.FxLocked = true;
        yield return none;

        yield return Base("factory-dry", "Dry", VirtualRoomCatalog.Dry, silent: false, fx: VirtualRoomCatalog.CreateFx(VirtualRoomCatalog.Dry));
        yield return Base("factory-reverb", "Reverb", VirtualRoomCatalog.Hall, silent: false, fx: VirtualRoomCatalog.CreateFx(VirtualRoomCatalog.Hall));

        var bass = Base("factory-bass", "Bass", VirtualRoomCatalog.Dry, silent: false, fx: VirtualRoomCatalog.CreateFx(VirtualRoomCatalog.Dry));
        bass.Fx.Eq.Enabled = true;
        bass.Fx.Eq.BassDb = 4;
        bass.Fx.Eq.AirDb = -0.5f;
        bass.Fx.DynamicBass.Enabled = true;
        bass.Fx.DynamicBass.Mix = 0.7f;
        yield return bass;

        yield return Base("factory-surround", "Surround", VirtualRoomCatalog.Surround, silent: false, fx: VirtualRoomCatalog.CreateFx(VirtualRoomCatalog.Surround));
    }

    public static ProfileDocument? TryCatalog(string id) =>
        Catalog().FirstOrDefault(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    public static bool IsReservedName(string? name) =>
        !string.IsNullOrWhiteSpace(name) &&
        Catalog().Any(p => p.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));

    public static string? ValidateUserProfileName(string? name, IEnumerable<ProfileDocument> existing, string? ignoreId = null)
    {
        var trimmed = name?.Trim() ?? "";
        if (trimmed.Length == 0)
        {
            return "Enter a name.";
        }

        if (IsReservedName(trimmed))
        {
            return "That name is reserved for a system profile.";
        }

        if (existing.Any(p =>
                !p.Id.Equals(ignoreId, StringComparison.OrdinalIgnoreCase) &&
                p.Name.Equals(trimmed, StringComparison.OrdinalIgnoreCase)))
        {
            return "A profile with that name already exists.";
        }

        return null;
    }

    private static void RemoveRetired(IAppPaths paths)
    {
        foreach (var id in RetiredIds)
        {
            var path = Path.Combine(paths.ProfilesDirectory, id + ".json");
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static void MigrateLegacyIds(IAppPaths paths, JsonProfileStore store)
    {
        foreach (var (oldId, newId) in IdMigrations)
        {
            var oldPath = Path.Combine(paths.ProfilesDirectory, oldId + ".json");
            var newPath = Path.Combine(paths.ProfilesDirectory, newId + ".json");
            if (!File.Exists(oldPath))
            {
                continue;
            }

            if (File.Exists(newPath) || oldId.Equals(newId, StringComparison.OrdinalIgnoreCase))
            {
                if (!oldId.Equals(newId, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(oldPath);
                }

                continue;
            }

            if (RetiredIds.Contains(oldId))
            {
                var retired = TryCatalog(newId);
                if (retired is not null)
                {
                    store.Save(retired, allowFactory: true);
                }

                File.Delete(oldPath);
                continue;
            }

            try
            {
                var doc = JsonSerializer.Deserialize<ProfileDocument>(File.ReadAllText(oldPath), JsonOptions.File);
                if (doc is null)
                {
                    File.Delete(oldPath);
                    continue;
                }

                var catalog = TryCatalog(newId);
                doc.Id = newId;
                doc.IsFactory = true;
                if (catalog is not null)
                {
                    doc.Name = catalog.Name;
                    doc.VirtualRoomId = catalog.VirtualRoomId;
                    doc.FxLocked = catalog.FxLocked;
                }

                store.Save(doc, allowFactory: true);
            }
            catch (Exception)
            {
                File.Copy(oldPath, oldPath + ".bad", overwrite: true);
            }

            if (File.Exists(oldPath))
            {
                File.Delete(oldPath);
            }
        }

        foreach (var profile in store.List())
        {
            if (profile.BasedOn is { } based &&
                IdMigrations.TryGetValue(based, out var mapped) &&
                !based.Equals(mapped, StringComparison.OrdinalIgnoreCase))
            {
                profile.BasedOn = mapped;
                if (!profile.IsFactory)
                {
                    store.Save(profile);
                }
            }
        }
    }

    private static ProfileDocument Base(
        string id,
        string name,
        string roomId,
        bool silent,
        FxSettings fx,
        string? primaryPackId = null) =>
        new()
        {
            SchemaVersion = 1,
            Id = id,
            Name = name,
            IsFactory = true,
            VirtualRoomId = roomId,
            Silent = silent,
            FxLocked = false,
            PrimaryPackId = primaryPackId ?? FactoryPackSeeder.FactoryId,
            Overlays = [],
            Behavior = new ProfileBehavior(),
            Output = new ProfileOutput { MasterVolume = silent ? 0 : 0.7f },
            Fx = fx
        };
}
