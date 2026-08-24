using KeyFXBoard.Core.Audio;

namespace KeyFXBoard.Core.Tests;

public sealed class VoicePoolTests
{
    [Fact]
    public void Polyphony_steals_instead_of_growing()
    {
        var pool = new VoicePool(8);
        var buffer = Tone(frames: 4_000);

        for (var i = 0; i < 20; i++)
        {
            pool.Trigger(buffer, 0.5f);
        }

        Assert.Equal(8, pool.ActiveVoices);
    }

    [Fact]
    public void Mix_releases_finished_voices()
    {
        var pool = new VoicePool(4);
        pool.Trigger(Tone(frames: 2), 1f);

        var dest = new float[16];
        pool.Mix(dest);

        Assert.Equal(0, pool.ActiveVoices);
        Assert.NotEqual(0, dest.Count(s => s != 0));
    }

    [Fact]
    public void Sustain_keeps_voice_until_release()
    {
        var pool = new VoicePool(2);
        pool.Trigger(Tone(frames: 12_000), 1f, holdKey: 0x51, sustain: true);

        var dest = new float[16_000];
        pool.Mix(dest);

        Assert.Equal(1, pool.ActiveVoices);

        pool.ReleaseHold(0x51, 0.002f);
        var fade = new float[512];
        pool.Mix(fade);
        Assert.Equal(0, pool.ActiveVoices);
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
}
