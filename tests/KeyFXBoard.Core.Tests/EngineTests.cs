using KeyFXBoard.Core.Audio;
using KeyFXBoard.Core.Hosting;
using KeyFXBoard.Core.Keys;
using KeyFXBoard.Core.Packs;

namespace KeyFXBoard.Core.Tests;

public sealed class EngineTests
{
    [Fact]
    public void Handle_then_mix_writes_audio()
    {
        var engine = new Engine();
        engine.SetClick(ClickSampleFactory.Create());
        engine.Volume = 1f;
        engine.VelocityRandom = 0;

        engine.Handle(Down(0x41));

        var dest = new float[1024];
        engine.FillBuffer(dest);

        Assert.Contains(dest, s => s != 0);
    }

    [Fact]
    public void Mute_drops_triggers()
    {
        var engine = new Engine();
        engine.SetClick(ClickSampleFactory.Create());
        engine.Muted = true;

        engine.Handle(Down(0x41));

        var dest = new float[1024];
        engine.FillBuffer(dest);

        Assert.All(dest, s => Assert.Equal(0, s));
    }

    [Fact]
    public void Two_keys_start_two_voices()
    {
        var engine = new Engine();
        engine.SetClick(ClickSampleFactory.Create());
        engine.Volume = 1f;
        engine.VelocityRandom = 0;

        engine.Handle(Down(0x41));
        engine.Handle(Down(0x53));

        var dest = new float[64];
        engine.FillBuffer(dest);

        Assert.Equal(2, engine.ActiveVoices);
    }

    [Fact]
    public void Chord_is_louder_than_one_key()
    {
        var single = MixKeys(1);
        var chord = MixKeys(3);

        Assert.True(Peak(chord) > Peak(single));
    }

    [Fact]
    public void Output_boost_raises_peak()
    {
        var quiet = MixWithBoost(1f);
        var loud = MixWithBoost(4f);
        Assert.True(Peak(loud) > Peak(quiet) * 2f);
    }

    [Fact]
    public void Hold_sustain_keeps_chromatic_voice_until_up()
    {
        var c4 = ClickSampleFactory.Create("C4");
        Assert.True(KeyCatalog.TryGetVirtualKey("KeyQ", out var q));
        var pack = new PackRuntime(
            "piano-classic",
            "Piano",
            [],
            [],
            c4,
            new Dictionary<int, SampleBuffer[]>(),
            new Dictionary<int, SampleBuffer[]>(),
            new Dictionary<int, string> { [q] = "C4" },
            new Dictionary<string, SampleBuffer> { ["C4"] = Tone(12_000) });

        var engine = new Engine();
        engine.SetPack(pack);
        engine.Volume = 1f;
        engine.VelocityRandom = 0;
        engine.Filter.Settings.IgnoreInjected = false;
        engine.Filter.Settings.HoldSustain = true;
        engine.Filter.Settings.ReleaseMs = 40;

        engine.Handle(new KeyEvent(KeyId.FromVirtualKey(q), KeyKind.Down, false, false, false, false));
        var held = new float[16_000];
        engine.FillBuffer(held);
        Assert.Equal(1, engine.ActiveVoices);

        engine.Handle(new KeyEvent(KeyId.FromVirtualKey(q), KeyKind.Up, false, false, false, false));
        var released = new float[4096];
        engine.FillBuffer(released);
        Assert.Equal(0, engine.ActiveVoices);
    }

    private static float[] MixWithBoost(float boost)
    {
        var engine = new Engine();
        engine.SetClick(ClickSampleFactory.Create());
        engine.Volume = 0.25f;
        engine.VelocityRandom = 0;
        engine.OutputBoost = boost;
        engine.Handle(Down(0x41));
        var dest = new float[256];
        engine.FillBuffer(dest);
        return dest;
    }

    private static float[] MixKeys(int count)
    {
        var engine = new Engine();
        engine.SetClick(ClickSampleFactory.Create());
        engine.Volume = 1f;
        engine.VelocityRandom = 0;

        for (var i = 0; i < count; i++)
        {
            engine.Handle(Down(0x41 + i));
        }

        var dest = new float[256];
        engine.FillBuffer(dest);
        return dest;
    }

    private static float Peak(float[] samples)
    {
        var peak = 0f;
        foreach (var s in samples)
        {
            peak = Math.Max(peak, Math.Abs(s));
        }

        return peak;
    }

    private static SampleBuffer Tone(int frames)
    {
        var data = new float[frames * SampleBuffer.Channels];
        for (var i = 0; i < frames; i++)
        {
            data[i * 2] = 0.25f;
            data[i * 2 + 1] = 0.25f;
        }

        return new SampleBuffer("test", data);
    }

    private static KeyEvent Down(int vk) =>
        new(KeyId.FromVirtualKey(vk), KeyKind.Down, false, false, false, false);
}
