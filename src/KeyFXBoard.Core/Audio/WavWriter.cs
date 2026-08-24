namespace KeyFXBoard.Core.Audio;

public static class WavWriter
{
    public static void Write(string path, SampleBuffer buffer)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var frames = buffer.Frames;
        var dataBytes = frames * SampleBuffer.Channels * 2;
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);

        writer.Write("RIFF"u8.ToArray());
        writer.Write(36 + dataBytes);
        writer.Write("WAVE"u8.ToArray());
        writer.Write("fmt "u8.ToArray());
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)SampleBuffer.Channels);
        writer.Write(SampleBuffer.SampleRate);
        writer.Write(SampleBuffer.SampleRate * SampleBuffer.Channels * 2);
        writer.Write((short)(SampleBuffer.Channels * 2));
        writer.Write((short)16);
        writer.Write("data"u8.ToArray());
        writer.Write(dataBytes);

        for (var i = 0; i < buffer.Data.Length; i++)
        {
            var sample = Math.Clamp(buffer.Data[i], -1f, 1f);
            writer.Write((short)Math.Round(sample * short.MaxValue));
        }
    }
}
