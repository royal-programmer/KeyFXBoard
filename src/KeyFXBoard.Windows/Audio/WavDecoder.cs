using KeyFXBoard.Core.Audio;
using KeyFXBoard.Core.Packs;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace KeyFXBoard.Windows.Audio;

public static class WavDecoder
{
    public static SampleBuffer Decode(string path, string id) =>
        Decode(path, id, wavOnly: true);

    public static SampleBuffer DecodeAny(string path, string id) =>
        Decode(path, id, wavOnly: false);

    private static SampleBuffer Decode(string path, string id, bool wavOnly)
    {
        if (wavOnly && !path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
        {
            throw new PackException($"Only WAV samples are supported: {Path.GetFileName(path)}");
        }

        if (new FileInfo(path).Length > PackPathRules.MaxSampleBytes)
        {
            throw new PackException($"Audio file too large (over 8 MB): {Path.GetFileName(path)}");
        }

        using var reader = new AudioFileReader(path);
        if (reader.WaveFormat.Channels is < 1 or > 2)
        {
            throw new PackException($"Audio must be mono or stereo: {Path.GetFileName(path)}");
        }

        ISampleProvider provider = reader.ToSampleProvider();
        if (reader.WaveFormat.Channels == 1)
        {
            provider = new MonoToStereoSampleProvider(provider);
        }

        if (reader.WaveFormat.SampleRate != SampleBuffer.SampleRate)
        {
            provider = new WdlResamplingSampleProvider(provider, SampleBuffer.SampleRate);
        }

        var data = new List<float>(reader.WaveFormat.SampleRate);
        var buffer = new float[4096];
        int read;
        while ((read = provider.Read(buffer, 0, buffer.Length)) > 0)
        {
            data.AddRange(buffer.AsSpan(0, read).ToArray());
        }

        if (data.Count == 0 || data.Count % SampleBuffer.Channels != 0)
        {
            throw new PackException($"Could not decode audio: {Path.GetFileName(path)}");
        }

        return new SampleBuffer(id, data.ToArray());
    }
}
