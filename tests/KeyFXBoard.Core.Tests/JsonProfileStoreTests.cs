using KeyFXBoard.Core.Profiles;
using KeyFXBoard.Core.Storage;

namespace KeyFXBoard.Core.Tests;

public sealed class JsonProfileStoreTests
{
    [Fact]
    public void Duplicate_is_user_and_factory_cannot_delete()
    {
        WithStore((paths, store) =>
        {
            FactoryProfileSeeder.Ensure(paths, store);
            var factory = store.Get(FactoryProfileSeeder.DefaultId)!;
            var copy = store.Duplicate(factory, "My dry");
            Assert.False(copy.IsFactory);
            Assert.StartsWith("user-", copy.Id);
            Assert.Throws<InvalidOperationException>(() => store.Delete(factory.Id));
            store.Delete(copy.Id);
            Assert.Null(store.Get(copy.Id));
        });
    }

    [Fact]
    public void Factory_save_is_rejected_without_allow()
    {
        WithStore((paths, store) =>
        {
            FactoryProfileSeeder.Ensure(paths, store);
            var factory = store.Get(FactoryProfileSeeder.DefaultId)!;
            factory.Name = "Nope";
            Assert.Throws<InvalidOperationException>(() => store.Save(factory));
        });
    }

    [Fact]
    public void Ensure_does_not_overwrite_edited_factory()
    {
        WithStore((paths, store) =>
        {
            FactoryProfileSeeder.Ensure(paths, store);
            var live = store.Get(FactoryProfileSeeder.DefaultId)!;
            live.Fx.Reverb.Enabled = true;
            live.Fx.Reverb.Mix = 0.42f;
            live.Name = "My Dry";
            store.Save(live, allowFactory: true);

            FactoryProfileSeeder.Ensure(paths, store);
            var again = store.Get(FactoryProfileSeeder.DefaultId)!;
            Assert.Equal("My Dry", again.Name);
            Assert.True(again.Fx.Reverb.Enabled);
            Assert.Equal(0.42f, again.Fx.Reverb.Mix);
        });
    }

    [Fact]
    public void Ensure_migrates_legacy_factory_ids()
    {
        WithStore((paths, store) =>
        {
            Directory.CreateDirectory(paths.ProfilesDirectory);
            var legacy = FactoryProfileSeeder.TryCatalog(FactoryProfileSeeder.DefaultId)!;
            legacy.Id = "factory-mechanical-tight";
            legacy.Name = "Mechanical Tight";
            legacy.Fx.Reverb.Enabled = true;
            legacy.Fx.Reverb.Mix = 0.33f;
            store.Save(legacy, allowFactory: true);

            FactoryProfileSeeder.Ensure(paths, store);

            Assert.Null(store.Get("factory-mechanical-tight"));
            Assert.False(File.Exists(Path.Combine(paths.ProfilesDirectory, "factory-mechanical-tight.json")));
            var migrated = store.Get("factory-dry")!;
            Assert.Equal("Dry", migrated.Name);
            Assert.True(migrated.IsFactory);
            Assert.Equal(0.33f, migrated.Fx.Reverb.Mix);
        });
    }

    [Fact]
    public void MapId_rewrites_legacy_defaults()
    {
        Assert.Equal("factory-dry", FactoryProfileSeeder.MapId("factory-mechanical-tight"));
        Assert.Equal("factory-dry", FactoryProfileSeeder.MapId("factory-tight"));
        Assert.Equal("factory-reverb", FactoryProfileSeeder.MapId("factory-piano-hall"));
        Assert.Equal("factory-reverb", FactoryProfileSeeder.MapId("factory-hall"));
        Assert.Equal("factory-bass", FactoryProfileSeeder.MapId("factory-cinema-gun"));
        Assert.Equal("factory-surround", FactoryProfileSeeder.MapId("factory-immersive"));
        Assert.Equal("factory-default", FactoryProfileSeeder.MapId("factory-silent"));
        Assert.Equal("factory-default", FactoryProfileSeeder.MapId("factory-piano"));
        Assert.Equal("factory-default", FactoryProfileSeeder.MapId("factory-low-cpu"));
        Assert.Equal(FactoryProfileSeeder.DefaultId, FactoryProfileSeeder.MapId(null));
    }

    [Fact]
    public void Factory_catalog_is_default_plus_four_rooms()
    {
        var ids = FactoryProfileSeeder.Catalog().Select(p => p.Id).ToArray();
        Assert.Equal(
            ["factory-default", "factory-dry", "factory-reverb", "factory-bass", "factory-surround"],
            ids);
        var none = FactoryProfileSeeder.TryCatalog(FactoryProfileSeeder.DefaultId)!;
        Assert.Equal("Default / No Effect", none.Name);
        Assert.True(none.FxLocked);
        Assert.False(none.Silent);
        Assert.False(none.Fx.Eq.Enabled);
        Assert.False(none.Fx.Reverb.Enabled);
        Assert.Null(FactoryProfileSeeder.TryCatalog("factory-piano"));
        Assert.Null(FactoryProfileSeeder.TryCatalog("factory-silent"));
        Assert.Null(FactoryProfileSeeder.TryCatalog("factory-low-cpu"));
    }

    [Fact]
    public void User_profile_name_rejects_system_and_duplicates()
    {
        var existing = FactoryProfileSeeder.Catalog().ToList();
        existing.Add(new ProfileDocument { Id = "user-1", Name = "Studio" });
        Assert.Equal("Enter a name.", FactoryProfileSeeder.ValidateUserProfileName("  ", existing));
        Assert.Equal("That name is reserved for a system profile.", FactoryProfileSeeder.ValidateUserProfileName("Dry", existing));
        Assert.Equal("That name is reserved for a system profile.", FactoryProfileSeeder.ValidateUserProfileName("default / no effect", existing));
        Assert.Equal("A profile with that name already exists.", FactoryProfileSeeder.ValidateUserProfileName("Studio", existing));
        Assert.Null(FactoryProfileSeeder.ValidateUserProfileName("Studio", existing, "user-1"));
        Assert.Null(FactoryProfileSeeder.ValidateUserProfileName("My Hall", existing));
    }

    [Fact]
    public void Retired_silent_becomes_locked_default()
    {
        WithStore((paths, store) =>
        {
            Directory.CreateDirectory(paths.ProfilesDirectory);
            var silent = FactoryProfileSeeder.TryCatalog(FactoryProfileSeeder.DefaultId)!;
            silent.Id = "factory-silent";
            silent.Name = "Silent";
            silent.Silent = true;
            silent.FxLocked = false;
            silent.Fx.Reverb.Enabled = true;
            store.Save(silent, allowFactory: true);

            FactoryProfileSeeder.Ensure(paths, store);

            Assert.Null(store.Get("factory-silent"));
            var migrated = store.Get(FactoryProfileSeeder.DefaultId)!;
            Assert.Equal("Default / No Effect", migrated.Name);
            Assert.True(migrated.FxLocked);
            Assert.False(migrated.Silent);
            Assert.False(migrated.Fx.Reverb.Enabled);
        });
    }

    private static void WithStore(Action<AppPaths, JsonProfileStore> test)
    {
        var root = Path.Combine(Path.GetTempPath(), "KeyFXBoard-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new AppPaths(root);
            test(paths, new JsonProfileStore(paths));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
