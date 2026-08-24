namespace KeyFXBoard.Core.Audio;

/// <summary>Original placeholder click so Phase 1 has no copyrighted samples.</summary>
public static class ClickSampleFactory
{
    public static SampleBuffer Create(string id = "factory-click")
    {
        const int frames = (int)(0.055 * SampleBuffer.SampleRate);
        var data = new float[frames * SampleBuffer.Channels];
        var rng = new Random(20260823);

        for (var i = 0; i < frames; i++)
        {
            var t = i / (double)SampleBuffer.SampleRate;
            var noise = (rng.NextDouble() * 2.0 - 1.0) * Math.Exp(-t * 220.0);
            var tick = Math.Sin(2.0 * Math.PI * 1950.0 * t) * Math.Exp(-t * 90.0);
            var body = Math.Sin(2.0 * Math.PI * 420.0 * t) * Math.Exp(-t * 55.0);
            var sample = (float)((noise * 0.22) + (tick * 0.38) + (body * 0.18));
            sample = Math.Clamp(sample, -0.85f, 0.85f);
            data[i * 2] = sample;
            data[i * 2 + 1] = sample;
        }

        return new SampleBuffer(id, data);
    }

    public static SampleBuffer CreateSoft(string id = "soft-tick")
    {
        const int frames = (int)(0.08 * SampleBuffer.SampleRate);
        var data = new float[frames * SampleBuffer.Channels];
        var rng = new Random(77);

        for (var i = 0; i < frames; i++)
        {
            var t = i / (double)SampleBuffer.SampleRate;
            var noise = (rng.NextDouble() * 2.0 - 1.0) * Math.Exp(-t * 140.0);
            var tick = Math.Sin(2.0 * Math.PI * 980.0 * t) * Math.Exp(-t * 42.0);
            var sample = (float)((noise * 0.10) + (tick * 0.32));
            sample = Math.Clamp(sample, -0.7f, 0.7f);
            data[i * 2] = sample;
            data[i * 2 + 1] = sample;
        }

        return new SampleBuffer(id, data);
    }
}
