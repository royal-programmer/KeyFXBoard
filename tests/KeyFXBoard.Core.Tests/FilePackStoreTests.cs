using KeyFXBoard.Core.Audio;
using KeyFXBoard.Core.Packs;
using KeyFXBoard.Core.Storage;

namespace KeyFXBoard.Core.Tests;

public sealed class FilePackStoreTests
{
    [Fact]
    public void Install_then_uninstall_user_pack()
    {
        var root = Path.Combine(Path.GetTempPath(), "KeyFXBoard-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new AppPaths(root);
            FactoryPackSeeder.Ensure(paths);
            var store = new FilePackStore(paths);
            Assert.Contains(store.List(), p => p.Id == "factory-click");
            Assert.Contains(store.List(), p => p.Id == "piano-classic");
            Assert.True(Directory.Exists(paths.CustomSamplesDirectory));
            var seeded = store.List().Count;

            var installed = store.Install(Path.Combine(paths.ExamplesDirectory, "soft-tick-1.0.0.kfxpack"), false);
            Assert.Equal("soft-tick", installed.Id);
            Assert.Equal(seeded + 1, store.List().Count);

            Assert.Throws<PackException>(() => store.Uninstall("factory-click"));
            Assert.Throws<PackException>(() => store.Uninstall("piano-classic"));

            store.Uninstall("soft-tick");
            Assert.DoesNotContain(store.List(), p => p.Id == "soft-tick");
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void Zip_slip_is_rejected()
    {
        var root = Path.Combine(Path.GetTempPath(), "KeyFXBoard-tests", Guid.NewGuid().ToString("N"));
        var zip = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".kfxpack");
        try
        {
            var evil = Path.Combine(Path.GetTempPath(), "kfx-evil-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(evil);
            File.WriteAllText(Path.Combine(evil, "manifest.json"), """
                {
                  "schemaVersion": 1,
                  "id": "evil-pack",
                  "name": "Evil",
                  "version": "1.0.0",
                  "author": "x",
                  "license": "CC0-1.0",
                  "fallback": { "down": ["../escape.wav"] }
                }
                """);
            WavWriter.Write(Path.Combine(evil, "escape.wav"), ClickSampleFactory.Create("x"));
            PackArchive.ZipFolder(evil, zip);

            var store = new FilePackStore(new AppPaths(root));
            Assert.Throws<PackException>(() => store.Install(zip, false));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }

            if (File.Exists(zip))
            {
                File.Delete(zip);
            }
        }
    }
}
