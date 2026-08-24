using KeyFXBoard.Core.Packs;

namespace KeyFXBoard.Core.Audio;

public static class InstrumentSampleFactory
{
    public static SampleBuffer Piano(string note, string id)
    {
        var freq = Frequency(note);
        var frames = (int)(2.4 * SampleBuffer.SampleRate);
        var data = new float[frames * SampleBuffer.Channels];
        const double sustain = 0.4;
        for (var i = 0; i < frames; i++)
        {
            var t = i / (double)SampleBuffer.SampleRate;
            var attack = 1.0 - Math.Exp(-t * 90.0);
            double env;
            if (t < 0.18)
            {
                var decay = 1.0 - ((1.0 - sustain) * (1.0 - Math.Exp(-t * 14.0)));
                env = attack * decay;
            }
            else if (t < 2.05)
            {
                env = sustain * Math.Exp(-(t - 0.18) * 0.07);
            }
            else
            {
                env = sustain * Math.Exp(-0.13) * Math.Exp(-(t - 2.05) * 5.0);
            }

            var sample = (float)((Math.Sin(Tau * freq * t)
                         + 0.38 * Math.Sin(Tau * freq * 2 * t)
                         + 0.16 * Math.Sin(Tau * freq * 3 * t)
                         + 0.07 * Math.Sin(Tau * freq * 4 * t)
                         + 0.03 * Math.Sin(Tau * freq * 5 * t)
                         + 0.012 * Math.Sin(Tau * freq * 6 * t)) * env * 0.58);
            data[i * 2] = sample;
            data[i * 2 + 1] = sample * 0.97f;
        }

        return new SampleBuffer(id, data);
    }

    private static double Frequency(string note) =>
        440.0 * Math.Pow(2.0, (PianoLayout.MidiOf(note) - 69) / 12.0);

    private const double Tau = Math.PI * 2;
}
