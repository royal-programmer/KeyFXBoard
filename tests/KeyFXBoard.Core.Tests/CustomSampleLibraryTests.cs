using KeyFXBoard.Core.Packs;
using KeyFXBoard.Core.Storage;

namespace KeyFXBoard.Core.Tests;

public sealed class CustomSampleLibraryTests
{
    [Fact]
    public void Ensure_creates_folder_and_catalog_entry()
    {
        var root = Path.Combine(Path.GetTempPath(), "KeyFXBoard-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new AppPaths(root);
            CustomSampleLibrary.Ensure(paths);
            Assert.True(Directory.Exists(paths.CustomSamplesDirectory));
            var entry = CustomSampleLibrary.CatalogEntry("hit.wav");
            Assert.Equal(CustomSampleLibrary.PackId, entry.Id);
            Assert.True(entry.IsFactory);
            Assert.Equal(CustomSampleLibrary.PackName, entry.Name);
            Assert.Contains("hit.wav", entry.Description, StringComparison.OrdinalIgnoreCase);
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
