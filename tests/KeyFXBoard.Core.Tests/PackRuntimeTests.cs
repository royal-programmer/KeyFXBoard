using KeyFXBoard.Core.Audio;
using KeyFXBoard.Core.Filtering;
using KeyFXBoard.Core.Hosting;
using KeyFXBoard.Core.Keys;
using KeyFXBoard.Core.Packs;

namespace KeyFXBoard.Core.Tests;

public sealed class PackRuntimeTests
{
    [Fact]
    public void Chromatic_resolve_uses_octave_shift()
    {
        var c4 = ClickSampleFactory.Create("C4");
        var c5 = ClickSampleFactory.Create("C5");
        var e4 = ClickSampleFactory.Create("E4");
        Assert.True(KeyCatalog.TryGetVirtualKey("KeyQ", out var q));
        Assert.True(KeyCatalog.TryGetVirtualKey("KeyE", out var e));
        Assert.True(KeyCatalog.TryGetVirtualKey("PageUp", out var pageUp));

        var pack = new PackRuntime(
            "piano-classic",
            "Piano",
            [],
            [],
            c4,
            new Dictionary<int, SampleBuffer[]>(),
            new Dictionary<int, SampleBuffer[]>(),
            new Dictionary<int, string> { [q] = "C4", [e] = "E4" },
            new Dictionary<string, SampleBuffer> { ["C4"] = c4, ["C5"] = c5, ["E4"] = e4 },
            octaveUpVk: pageUp);

        Assert.True(pack.IsChromatic);
        Assert.Same(e4, pack.Resolve(Down(e), VariantMode.First));
        Assert.Same(c5, pack.Resolve(Down(q), VariantMode.First, 12));
        Assert.True(pack.TryOctaveDelta(pageUp, out var delta));
        Assert.Equal(12, delta);
        Assert.False(pack.MapsVirtualKey(0x41));
        Assert.True(pack.MapsVirtualKey(e));
    }

    [Fact]
    public void Home_resets_octave()
    {
        Assert.True(KeyCatalog.TryGetVirtualKey("Home", out var home));
        var chromatic = new PackRuntime(
            "piano-classic",
            "Piano",
            [],
            [],
            ClickSampleFactory.Create("C4"),
            new Dictionary<int, SampleBuffer[]>(),
            new Dictionary<int, SampleBuffer[]>(),
            new Dictionary<int, string>(),
            new Dictionary<string, SampleBuffer>(),
            octaveDownVk: 0,
            octaveUpVk: 0,
            octaveReset: [home]);
        Assert.True(chromatic.TryOctaveReset(home));
        Assert.True(chromatic.MapsVirtualKey(home));
    }

    [Fact]
    public void Explicit_keymap_does_not_treat_fallback_as_mapped()
    {
        var mapped = ClickSampleFactory.Create("a");
        var fallback = ClickSampleFactory.Create("fb");
        var pack = new PackRuntime(
            "oneshot",
            "Guns",
            [fallback],
            [],
            mapped,
            new Dictionary<int, SampleBuffer[]> { [0x41] = [mapped] },
            new Dictionary<int, SampleBuffer[]>());

        Assert.True(pack.MapsVirtualKey(0x41));
        Assert.False(pack.MapsVirtualKey(0x42));
        Assert.Same(fallback, pack.Resolve(Down(0x42), VariantMode.First));
    }

    [Fact]
    public void Force_sample_key_uses_that_key_for_every_stroke()
    {
        var a = ClickSampleFactory.Create("a");
        var b = ClickSampleFactory.Create("b");
        var pack = new PackRuntime(
            "oneshot",
            "Guns",
            [],
            [],
            a,
            new Dictionary<int, SampleBuffer[]> { [0x41] = [a], [0x42] = [b] },
            new Dictionary<int, SampleBuffer[]>());

        Assert.Same(a, pack.Resolve(Down(0x42), VariantMode.First, forceSampleKey: "KeyA"));
        Assert.Same(b, pack.Resolve(Down(0x42), VariantMode.First));
    }

    [Fact]
    public void Engine_silence_unmapped_skips_fallback()
    {
        var mapped = ClickSampleFactory.Create("a");
        var fallback = ClickSampleFactory.Create("fb");
        var pack = new PackRuntime(
            "oneshot",
            "Guns",
            [fallback],
            [],
            mapped,
            new Dictionary<int, SampleBuffer[]> { [0x41] = [mapped] },
            new Dictionary<int, SampleBuffer[]>());

        var engine = new Engine();
        engine.SetPack(pack);
        engine.Volume = 1f;
        engine.VelocityRandom = 0;
        engine.Filter.Settings.IgnoreInjected = false;
        engine.Filter.Settings.SilenceUnmapped = true;

        engine.Handle(Down(0x42));
        var silent = new float[256];
        engine.FillBuffer(silent);
        Assert.All(silent, s => Assert.Equal(0, s));

        engine.Handle(Down(0x41));
        var heard = new float[256];
        engine.FillBuffer(heard);
        Assert.Contains(heard, s => s != 0);
    }

    [Fact]
    public void Pack_loader_does_not_invent_fallback_for_chromatic_packs()
    {
        var root = Path.Combine(Path.GetTempPath(), "KeyFXBoard-tests", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "samples"));
            WavWriter.Write(Path.Combine(root, "samples", "C4.wav"), ClickSampleFactory.Create("C4"));
            WavWriter.Write(Path.Combine(root, "preview.wav"), ClickSampleFactory.Create("preview"));
            PackArchive.WriteManifest(root, new PackManifest
            {
                SchemaVersion = 1,
                Id = "tiny-piano",
                Name = "Tiny Piano",
                Version = "1.0.0",
                Author = "t",
                License = "CC0-1.0",
                Preview = "preview.wav",
                Notes = new Dictionary<string, string> { ["C4"] = "samples/C4.wav" },
                KeyNotes = new Dictionary<string, string> { ["KeyQ"] = "C4" }
            });

            var runtime = PackLoader.Load(root, (_, id) => ClickSampleFactory.Create(id));
            Assert.True(runtime.IsChromatic);
            Assert.Null(runtime.Resolve(Down(0x41), VariantMode.First));
            Assert.NotNull(runtime.Resolve(Down(0x51), VariantMode.First));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static KeyEvent Down(int vk) =>
        new(KeyId.FromVirtualKey(vk), KeyKind.Down, false, false, false, false);
}
