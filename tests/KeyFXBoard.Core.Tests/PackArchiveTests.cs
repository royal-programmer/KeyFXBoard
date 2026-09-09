using System.IO.Compression;
using KeyFXBoard.Core.Packs;

namespace KeyFXBoard.Core.Tests;

public sealed class PackArchiveTests
{
    [Fact]
    public void Extract_rejects_zip_slip_relative()
    {
        var root = Path.Combine(Path.GetTempPath(), "kfx-slip-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var zipPath = Path.Combine(root, "evil.kfxpack");
            var dest = Path.Combine(root, "out");
            using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                var entry = zip.CreateEntry("../outside.txt");
                using var writer = new StreamWriter(entry.Open());
                writer.Write("nope");
            }

            Assert.Throws<PackException>(() => PackArchive.Extract(zipPath, dest));
            Assert.False(File.Exists(Path.Combine(root, "outside.txt")));
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
    public void Extract_writes_safe_nested_file()
    {
        var root = Path.Combine(Path.GetTempPath(), "kfx-ok-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var zipPath = Path.Combine(root, "ok.kfxpack");
            var dest = Path.Combine(root, "out");
            using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                using (var manifest = zip.CreateEntry("manifest.json").Open())
                using (var writer = new StreamWriter(manifest))
                {
                    writer.Write("{}");
                }

                var entry = zip.CreateEntry("samples/click.wav");
                using var stream = entry.Open();
                stream.Write(new byte[] { 1, 2, 3, 4 });
            }

            PackArchive.Extract(zipPath, dest);
            Assert.True(File.Exists(Path.Combine(dest, "samples", "click.wav")));
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
    public void Extract_rejects_missing_file()
    {
        Assert.Throws<PackException>(() =>
            PackArchive.Extract(Path.Combine(Path.GetTempPath(), "missing-" + Guid.NewGuid() + ".kfxpack"), Path.GetTempPath()));
    }
}
