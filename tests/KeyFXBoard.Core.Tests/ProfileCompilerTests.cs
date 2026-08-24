using KeyFXBoard.Core.Audio;
using KeyFXBoard.Core.Filtering;
using KeyFXBoard.Core.Keys;
using KeyFXBoard.Core.Packs;
using KeyFXBoard.Core.Profiles;

namespace KeyFXBoard.Core.Tests;

public sealed class ProfileCompilerTests
{
    [Fact]
    public void Overlay_wins_on_listed_keys()
    {
        var primary = PackRuntime.SingleSample("factory-click", "Click", ClickSampleFactory.Create("p"));
        var overlay = PackRuntime.SingleSample("soft-tick", "Soft", ClickSampleFactory.CreateSoft("o"));
        var doc = new ProfileDocument
        {
            Id = "user-test",
            Name = "Test",
            PrimaryPackId = "factory-click",
            Overlays =
            [
                new ProfileOverlay { PackId = "soft-tick", Keys = ["Enter"] }
            ]
        };

        var snapshot = ProfileCompiler.Compile(
            doc,
            id => id == "soft-tick" ? overlay : primary,
            primary,
            out _);

        var enter = snapshot.Resolve(new KeyEvent(KeyId.FromVirtualKey(0x0D), KeyKind.Down, false, false, false, false));
        var letter = snapshot.Resolve(new KeyEvent(KeyId.FromVirtualKey(0x41), KeyKind.Down, false, false, false, false));
        Assert.Same(overlay.Preview, enter);
        Assert.Same(primary.Preview, letter);
    }

    [Fact]
    public void Up_falls_back_to_down_sample()
    {
        var sample = ClickSampleFactory.Create("p");
        var pack = new PackRuntime(
            "click",
            "Click",
            [sample],
            [],
            sample,
            new Dictionary<int, SampleBuffer[]>(),
            new Dictionary<int, SampleBuffer[]>());

        var up = pack.Resolve(
            new KeyEvent(KeyId.FromVirtualKey(0x41), KeyKind.Up, false, false, false, false),
            VariantMode.First);
        Assert.Same(sample, up);
    }

    [Fact]
    public void Missing_overlay_is_skipped()
    {
        var primary = PackRuntime.SingleSample("factory-click", "Click", ClickSampleFactory.Create());
        var doc = new ProfileDocument
        {
            Id = "factory-mixed-immersive",
            Name = "Mixed",
            PrimaryPackId = "factory-click",
            Overlays = [new ProfileOverlay { PackId = "missing", Keys = ["Enter"] }]
        };

        var snapshot = ProfileCompiler.Compile(
            doc,
            id => id == "factory-click" ? primary : null,
            primary,
            out var warning);
        Assert.Empty(snapshot.Overlays);
        Assert.Contains("missing", warning);
    }
}
