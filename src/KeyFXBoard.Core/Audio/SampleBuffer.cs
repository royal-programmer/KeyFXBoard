namespace KeyFXBoard.Core.Audio;

/// <summary>Immutable 48 kHz stereo float PCM. Safe to share across threads after construction.</summary>
public sealed class SampleBuffer
{
    public const int SampleRate = 48_000;
    public const int Channels = 2;

    public SampleBuffer(string id, float[] interleavedStereo)
    {
        if (interleavedStereo.Length % Channels != 0)
        {
            throw new ArgumentException("Stereo buffer length must be even.", nameof(interleavedStereo));
        }

        Id = id;
        Data = interleavedStereo;
        Frames = interleavedStereo.Length / Channels;
    }

    public string Id { get; }
    public float[] Data { get; }
    public int Frames { get; }
}
