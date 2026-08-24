namespace KeyFXBoard.Core.Audio;

/// <summary>Single-threaded mixer. Only the audio callback may call <see cref="Trigger"/> and <see cref="Mix"/>.</summary>
public sealed class VoicePool
{
    private readonly Voice[] _voices;
    private ulong _epoch;

    public VoicePool(int polyphony)
    {
        if (polyphony is < 1 or > 64)
        {
            throw new ArgumentOutOfRangeException(nameof(polyphony), "Polyphony must be 1–64.");
        }

        _voices = new Voice[polyphony];
        for (var i = 0; i < _voices.Length; i++)
        {
            _voices[i] = new Voice();
        }
    }

    public int Polyphony => _voices.Length;

    public int ActiveVoices
    {
        get
        {
            var n = 0;
            foreach (var voice in _voices)
            {
                if (voice.InUse)
                {
                    n++;
                }
            }

            return n;
        }
    }

    public void Trigger(SampleBuffer buffer, float gain, int holdKey = 0, bool sustain = false)
    {
        if (holdKey != 0)
        {
            foreach (var existing in _voices)
            {
                if (existing.InUse && existing.HoldKey == holdKey)
                {
                    existing.InUse = false;
                    existing.Buffer = null;
                }
            }
        }

        Voice? free = null;
        Voice? oldest = _voices[0];

        foreach (var voice in _voices)
        {
            if (!voice.InUse)
            {
                free = voice;
                break;
            }

            if (voice.StartedAt < oldest.StartedAt)
            {
                oldest = voice;
            }
        }

        var target = free ?? oldest;
        var frames = buffer.Frames;
        var loopStart = (int)(frames * 0.12);
        var loopEnd = (int)(frames * 0.38);
        var canLoop = sustain && holdKey != 0 && loopEnd - loopStart > SampleBuffer.SampleRate / 20;

        target.Buffer = buffer;
        target.Frame = 0;
        target.Gain = gain;
        target.BaseGain = gain;
        target.HoldKey = holdKey;
        target.Sustain = canLoop;
        target.Releasing = false;
        target.LoopStart = loopStart;
        target.LoopEnd = loopEnd;
        target.ReleaseStep = 0;
        target.StartedAt = ++_epoch;
        target.InUse = true;
    }

    public void ReleaseHold(int holdKey, float releaseSeconds)
    {
        if (holdKey == 0)
        {
            return;
        }

        var frames = Math.Max(1, (int)(releaseSeconds * SampleBuffer.SampleRate));
        foreach (var voice in _voices)
        {
            if (!voice.InUse || voice.HoldKey != holdKey)
            {
                continue;
            }

            voice.Sustain = false;
            voice.Releasing = true;
            voice.HoldKey = 0;
            voice.ReleaseStep = voice.BaseGain / frames;
        }
    }

    public void Mix(Span<float> interleavedStereo)
    {
        interleavedStereo.Clear();

        foreach (var voice in _voices)
        {
            if (!voice.InUse || voice.Buffer is null)
            {
                continue;
            }

            var data = voice.Buffer.Data;
            var frames = voice.Buffer.Frames;
            var destFrames = interleavedStereo.Length / SampleBuffer.Channels;

            for (var i = 0; i < destFrames; i++)
            {
                if (voice.Frame >= frames)
                {
                    if (voice.Sustain && voice.LoopEnd > voice.LoopStart)
                    {
                        voice.Frame = voice.LoopStart;
                    }
                    else
                    {
                        voice.InUse = false;
                        voice.Buffer = null;
                        break;
                    }
                }

                if (voice.Releasing)
                {
                    voice.Gain -= voice.ReleaseStep;
                    if (voice.Gain <= 0.0008f)
                    {
                        voice.InUse = false;
                        voice.Buffer = null;
                        break;
                    }
                }

                var src = voice.Frame * SampleBuffer.Channels;
                var dst = i * SampleBuffer.Channels;
                var left = data[src] * voice.Gain;
                var right = data[src + 1] * voice.Gain;
                if (voice.Sustain && !voice.Releasing && voice.LoopEnd > voice.LoopStart)
                {
                    var cross = Math.Min(128, (voice.LoopEnd - voice.LoopStart) / 4);
                    var fadeAt = voice.LoopEnd - cross;
                    if (cross > 0 && voice.Frame >= fadeAt)
                    {
                        var t = (voice.Frame - fadeAt) / (float)cross;
                        var other = (voice.LoopStart + (voice.Frame - fadeAt)) * SampleBuffer.Channels;
                        left = (data[src] * (1f - t) + data[other] * t) * voice.Gain;
                        right = (data[src + 1] * (1f - t) + data[other + 1] * t) * voice.Gain;
                    }
                }

                interleavedStereo[dst] += left;
                interleavedStereo[dst + 1] += right;
                voice.Frame++;

                if (voice.Sustain && !voice.Releasing && voice.Frame >= voice.LoopEnd)
                {
                    voice.Frame = voice.LoopStart;
                }
            }
        }
    }

    public void StopAll()
    {
        foreach (var voice in _voices)
        {
            voice.InUse = false;
            voice.Buffer = null;
            voice.HoldKey = 0;
        }
    }

    private sealed class Voice
    {
        public bool InUse;
        public SampleBuffer? Buffer;
        public int Frame;
        public float Gain;
        public float BaseGain;
        public ulong StartedAt;
        public int HoldKey;
        public bool Sustain;
        public bool Releasing;
        public int LoopStart;
        public int LoopEnd;
        public float ReleaseStep;
    }
}
