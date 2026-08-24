using KeyFXBoard.Core.Storage;

namespace KeyFXBoard.Core.Tests;

public sealed class JsonSettingsStoreTests
{
    [Fact]
    public void Save_then_load_round_trips()
    {
        var root = Path.Combine(Path.GetTempPath(), "KeyFXBoard-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var store = new JsonSettingsStore(new AppPaths(root));
            store.Save(new AppSettings
            {
                Autostart = true,
                GlobalMute = true,
                Volume = 0.4f,
                FirstRunCompleted = true,
                AudioDeviceId = "speakers-1",
                OutputBoostDb = 6,
                DisabledPackIds = ["custom-sample"]
            });

            var loaded = store.Load();
            Assert.True(loaded.Autostart);
            Assert.True(loaded.GlobalMute);
            Assert.Equal(0.4f, loaded.Volume);
            Assert.True(loaded.FirstRunCompleted);
            Assert.Equal("speakers-1", loaded.AudioDeviceId);
            Assert.Equal(6f, loaded.OutputBoostDb);
            Assert.Equal(["custom-sample"], loaded.DisabledPackIds);
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
    public void Missing_file_returns_defaults()
    {
        var root = Path.Combine(Path.GetTempPath(), "KeyFXBoard-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var loaded = new JsonSettingsStore(new AppPaths(root)).Load();
            Assert.False(loaded.FirstRunCompleted);
            Assert.Equal(0.7f, loaded.Volume);
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
