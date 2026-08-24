using KeyFXBoard.Core.Profiles;

namespace KeyFXBoard.Core.Audio;

/// <summary>Profile FX chain. Built off the audio thread; <see cref="Process"/> is realtime.</summary>
public sealed class FxGraph
{
    private readonly float _inputGain;
    private readonly Biquad _bassL = new();
    private readonly Biquad _bassR = new();
    private readonly Biquad _airL = new();
    private readonly Biquad _airR = new();
    private readonly bool _eq;
    private readonly bool _dynBass;
    private readonly float _dynMix;
    private float _dynEnv;
    private float _dynLpL;
    private float _dynLpR;
    private readonly bool _comp;
    private readonly float _compThreshold;
    private readonly float _compRatio;
    private readonly float _compAttack;
    private readonly float _compRelease;
    private readonly float _compMakeup;
    private float _env;
    private readonly bool _sat;
    private readonly bool _crush;
    private readonly float _drive;
    private readonly float _satMix;
    private readonly bool _chorus;
    private readonly float[] _chorusL;
    private readonly float[] _chorusR;
    private readonly float _chorusMix;
    private readonly float _chorusDepth;
    private readonly float _chorusInc;
    private float _chorusPhase;
    private int _chorusWrite;
    private readonly bool _flanger;
    private readonly float[] _flangerL;
    private readonly float[] _flangerR;
    private readonly float _flangerMix;
    private readonly float _flangerDepth;
    private readonly float _flangerFeedback;
    private readonly float _flangerInc;
    private float _flangerPhase;
    private int _flangerWrite;
    private readonly bool _phaser;
    private readonly float _phaserMix;
    private readonly float _phaserDepth;
    private readonly float _phaserInc;
    private float _phaserPhase;
    private readonly Allpass1 _phaserL1 = new();
    private readonly Allpass1 _phaserL2 = new();
    private readonly Allpass1 _phaserL3 = new();
    private readonly Allpass1 _phaserL4 = new();
    private readonly Allpass1 _phaserR1 = new();
    private readonly Allpass1 _phaserR2 = new();
    private readonly Allpass1 _phaserR3 = new();
    private readonly Allpass1 _phaserR4 = new();
    private readonly bool _delayOn;
    private readonly float[] _delayL;
    private readonly float[] _delayR;
    private readonly float _delayFeedback;
    private readonly float _delayMix;
    private int _delayIndex;
    private readonly bool _conv;
    private readonly float[] _ir;
    private readonly float[] _convHistL;
    private readonly float[] _convHistR;
    private int _convPos;
    private readonly float _convMix;
    private readonly bool _reverbOn;
    private readonly Comb[] _combs;
    private readonly Allpass[] _allpasses;
    private readonly float _reverbMix;
    private readonly bool _widthOn;
    private readonly float _width;
    private readonly bool _crossfeed;
    private readonly float[] _xfL;
    private readonly float[] _xfR;
    private int _xfIndex;
    private readonly float _xfMix;
    private readonly float _ceiling;

    private FxGraph(FxSettings fx)
    {
        _inputGain = Db(fx.InputGainDb);
        _eq = fx.Eq.Enabled && (Math.Abs(fx.Eq.BassDb) > 0.05f || Math.Abs(fx.Eq.AirDb) > 0.05f);
        if (_eq)
        {
            _bassL.LowShelf(120, fx.Eq.BassDb);
            _bassR.CopyFrom(_bassL);
            _airL.HighShelf(8000, fx.Eq.AirDb);
            _airR.CopyFrom(_airL);
        }

        _dynBass = fx.DynamicBass.Enabled && fx.DynamicBass.Mix > 0.01f;
        _dynMix = Math.Clamp(fx.DynamicBass.Mix, 0, 1) * 3.5f;

        _comp = fx.Compressor.Enabled;
        _compThreshold = Db(fx.Compressor.ThresholdDb);
        _compRatio = Math.Max(fx.Compressor.Ratio, 1f);
        _compAttack = 1f - MathF.Exp(-1f / Math.Max(fx.Compressor.AttackMs * 0.001f * SampleBuffer.SampleRate, 1));
        _compRelease = 1f - MathF.Exp(-1f / Math.Max(fx.Compressor.ReleaseMs * 0.001f * SampleBuffer.SampleRate, 1));
        _compMakeup = Db(fx.Compressor.MakeupDb);

        _sat = fx.Saturation.Enabled && fx.Saturation.Mix > 0;
        _crush = fx.Saturation.Style.Equals("Crush", StringComparison.OrdinalIgnoreCase);
        _drive = 1f + Math.Clamp(fx.Saturation.Drive, 0, 1) * 8f;
        _satMix = Math.Clamp(fx.Saturation.Mix, 0, 1);

        _chorus = fx.Chorus.Enabled && fx.Chorus.Mix > 0;
        _chorusL = new float[SampleBuffer.SampleRate / 20];
        _chorusR = new float[_chorusL.Length];
        _chorusMix = Math.Clamp(fx.Chorus.Mix, 0, 1);
        _chorusDepth = Math.Clamp(fx.Chorus.Depth, 0, 1);
        _chorusInc = MathF.Tau * Math.Clamp(fx.Chorus.RateHz, 0.1f, 5f) / SampleBuffer.SampleRate;

        _flanger = fx.Flanger.Enabled && fx.Flanger.Mix > 0;
        _flangerL = new float[SampleBuffer.SampleRate / 50];
        _flangerR = new float[_flangerL.Length];
        _flangerMix = Math.Clamp(fx.Flanger.Mix, 0, 1);
        _flangerDepth = Math.Clamp(fx.Flanger.Depth, 0, 1);
        _flangerFeedback = Math.Clamp(fx.Flanger.Feedback, 0, 0.7f);
        _flangerInc = MathF.Tau * Math.Clamp(fx.Flanger.RateHz, 0.05f, 4f) / SampleBuffer.SampleRate;

        _phaser = fx.Phaser.Enabled && fx.Phaser.Mix > 0;
        _phaserMix = Math.Clamp(fx.Phaser.Mix, 0, 1);
        _phaserDepth = Math.Clamp(fx.Phaser.Depth, 0, 1);
        _phaserInc = MathF.Tau * Math.Clamp(fx.Phaser.RateHz, 0.05f, 4f) / SampleBuffer.SampleRate;

        var delaySamples = (int)Math.Clamp(fx.Delay.TimeMs, 50, 600) * SampleBuffer.SampleRate / 1000;
        _delayOn = fx.Delay.Enabled && fx.Delay.Mix > 0;
        _delayL = new float[Math.Max(delaySamples, 1)];
        _delayR = new float[Math.Max(delaySamples, 1)];
        _delayFeedback = Math.Clamp(fx.Delay.Feedback, 0, 0.7f);
        _delayMix = Math.Clamp(fx.Delay.Mix, 0, 1);

        _ir = BuiltInIrs.Create(fx.Convolver.Ir);
        _conv = fx.Convolver.Enabled && fx.Convolver.Mix > 0;
        _convHistL = new float[_ir.Length];
        _convHistR = new float[_ir.Length];
        _convMix = Math.Clamp(fx.Convolver.Mix, 0, 1);

        _reverbOn = fx.Reverb.Enabled && fx.Reverb.Mix > 0;
        var decay = Math.Clamp(fx.Reverb.Decay, 0.05f, 0.95f);
        var damp = Math.Clamp(fx.Reverb.Damping, 0, 0.9f);
        _combs =
        [
            new Comb(1557, decay, damp),
            new Comb(1617, decay, damp),
            new Comb(1491, decay, damp),
            new Comb(1422, decay, damp)
        ];
        _allpasses =
        [
            new Allpass(225, 0.5f),
            new Allpass(556, 0.5f)
        ];
        _reverbMix = Math.Clamp(fx.Reverb.Mix, 0, 1);

        _widthOn = fx.Width.Enabled;
        _width = 0.15f + Math.Clamp(fx.Width.Mix, 0, 1) * 1.7f;

        _crossfeed = fx.Crossfeed.Enabled && fx.Crossfeed.Mix > 0;
        _xfL = new float[19];
        _xfR = new float[19];
        _xfMix = Math.Clamp(fx.Crossfeed.Mix, 0, 1);
        _ceiling = Db(fx.Limiter.CeilingDb);
    }

    public static FxGraph Create(FxSettings settings) => new(settings);

    public void Process(Span<float> interleaved)
    {
        for (var i = 0; i < interleaved.Length; i += 2)
        {
            var l = interleaved[i] * _inputGain;
            var r = interleaved[i + 1] * _inputGain;

            if (_eq)
            {
                l = _airL.Process(_bassL.Process(l));
                r = _airR.Process(_bassR.Process(r));
            }

            if (_dynBass)
            {
                var level = MathF.Max(MathF.Abs(l), MathF.Abs(r));
                _dynEnv += (level - _dynEnv) * (level > _dynEnv ? 0.12f : 0.02f);
                _dynLpL += 0.04f * (l - _dynLpL);
                _dynLpR += 0.04f * (r - _dynLpR);
                var boost = 1f + _dynMix * _dynEnv;
                l += _dynLpL * (boost - 1f);
                r += _dynLpR * (boost - 1f);
            }

            if (_comp)
            {
                var level = MathF.Max(MathF.Abs(l), MathF.Abs(r));
                var coeff = level > _env ? _compAttack : _compRelease;
                _env += (level - _env) * coeff;
                var gain = 1f;
                if (_env > _compThreshold)
                {
                    var over = _env / _compThreshold;
                    gain = (1f / over) + ((1f - 1f / over) / _compRatio);
                    gain = Math.Clamp(gain, 0.05f, 1f);
                }

                l *= gain * _compMakeup;
                r *= gain * _compMakeup;
            }

            if (_sat)
            {
                l = Saturate(l);
                r = Saturate(r);
            }

            if (_chorus)
            {
                (l, r) = ModDelay(l, r, _chorusL, _chorusR, ref _chorusWrite, ref _chorusPhase, _chorusInc, 0.012f, 0.006f, _chorusDepth, 0, _chorusMix);
            }

            if (_flanger)
            {
                (l, r) = ModDelay(l, r, _flangerL, _flangerR, ref _flangerWrite, ref _flangerPhase, _flangerInc, 0.0015f, 0.0035f, _flangerDepth, _flangerFeedback, _flangerMix);
            }

            if (_phaser)
            {
                _phaserPhase += _phaserInc;
                if (_phaserPhase > MathF.Tau)
                {
                    _phaserPhase -= MathF.Tau;
                }

                var coeff = 0.15f + (0.55f + 0.25f * MathF.Sin(_phaserPhase)) * _phaserDepth;
                var wetL = _phaserL4.Process(_phaserL3.Process(_phaserL2.Process(_phaserL1.Process(l, coeff), coeff), coeff), coeff);
                var wetR = _phaserR4.Process(_phaserR3.Process(_phaserR2.Process(_phaserR1.Process(r, coeff), coeff), coeff), coeff);
                l += (wetL - l) * _phaserMix;
                r += (wetR - r) * _phaserMix;
            }

            if (_delayOn)
            {
                var dl = _delayL[_delayIndex];
                var dr = _delayR[_delayIndex];
                _delayL[_delayIndex] = l + dl * _delayFeedback;
                _delayR[_delayIndex] = r + dr * _delayFeedback;
                _delayIndex++;
                if (_delayIndex >= _delayL.Length)
                {
                    _delayIndex = 0;
                }

                l += dl * _delayMix;
                r += dr * _delayMix;
            }

            if (_conv)
            {
                _convHistL[_convPos] = l;
                _convHistR[_convPos] = r;
                float cl = 0, cr = 0;
                var pos = _convPos;
                for (var k = 0; k < _ir.Length; k++)
                {
                    cl += _convHistL[pos] * _ir[k];
                    cr += _convHistR[pos] * _ir[k];
                    if (--pos < 0)
                    {
                        pos = _ir.Length - 1;
                    }
                }

                _convPos++;
                if (_convPos >= _ir.Length)
                {
                    _convPos = 0;
                }

                l += (cl - l) * _convMix;
                r += (cr - r) * _convMix;
            }

            if (_reverbOn)
            {
                var send = (l + r) * 0.5f;
                var wet = 0f;
                foreach (var comb in _combs)
                {
                    wet += comb.Process(send);
                }

                wet *= 0.25f;
                foreach (var allpass in _allpasses)
                {
                    wet = allpass.Process(wet);
                }

                l += wet * _reverbMix;
                r += wet * _reverbMix * 0.92f;
            }

            if (_widthOn)
            {
                var mid = (l + r) * 0.5f;
                var side = (l - r) * 0.5f * _width;
                l = mid + side;
                r = mid - side;
            }

            if (_crossfeed)
            {
                var delayedL = _xfL[_xfIndex];
                var delayedR = _xfR[_xfIndex];
                _xfL[_xfIndex] = l;
                _xfR[_xfIndex] = r;
                _xfIndex++;
                if (_xfIndex >= _xfL.Length)
                {
                    _xfIndex = 0;
                }

                var dry = 1f - _xfMix * 0.55f;
                l = l * dry + delayedR * _xfMix;
                r = r * dry + delayedL * _xfMix;
            }

            interleaved[i] = Limit(l);
            interleaved[i + 1] = Limit(r);
        }
    }

    private static (float l, float r) ModDelay(
        float l,
        float r,
        float[] bufL,
        float[] bufR,
        ref int write,
        ref float phase,
        float inc,
        float baseSec,
        float depthSec,
        float depth,
        float feedback,
        float mix)
    {
        bufL[write] = l;
        bufR[write] = r;
        phase += inc;
        if (phase > MathF.Tau)
        {
            phase -= MathF.Tau;
        }

        var delay = (baseSec + depthSec * depth * (0.5f + 0.5f * MathF.Sin(phase))) * SampleBuffer.SampleRate;
        var wetL = Read(bufL, write, delay);
        var wetR = Read(bufR, write, delay);
        bufL[write] += wetL * feedback;
        bufR[write] += wetR * feedback;
        write++;
        if (write >= bufL.Length)
        {
            write = 0;
        }

        return (l + (wetL - l) * mix, r + (wetR - r) * mix);
    }

    private static float Read(float[] buf, int write, float delaySamples)
    {
        var index = write - delaySamples;
        while (index < 0)
        {
            index += buf.Length;
        }

        var i = (int)index;
        var frac = index - i;
        var a = buf[i % buf.Length];
        var b = buf[(i + 1) % buf.Length];
        return a + (b - a) * frac;
    }

    private float Saturate(float x)
    {
        var wet = _crush
            ? MathF.CopySign(MathF.Min(MathF.Abs(x * _drive), 1f), x)
            : MathF.Tanh(x * _drive);
        return x + (wet - x) * _satMix;
    }

    private float Limit(float x) => Math.Clamp(x, -_ceiling, _ceiling);

    private static float Db(float db) => MathF.Pow(10f, db / 20f);

    private static class BuiltInIrs
    {
        public static float[] Create(string? ir)
        {
            var medium = ir is not null && ir.Equals("Medium", StringComparison.OrdinalIgnoreCase);
            var n = medium ? 256 : 128;
            var decay = medium ? 70f : 36f;
            var rng = new Random(medium ? 11 : 5);
            var h = new float[n];
            var sum = 0f;
            for (var i = 0; i < n; i++)
            {
                h[i] = (float)(rng.NextDouble() * 2 - 1) * MathF.Exp(-i / decay);
                sum += MathF.Abs(h[i]);
            }

            h[0] = 0.85f;
            if (sum < 0.001f)
            {
                return h;
            }

            for (var i = 0; i < n; i++)
            {
                h[i] /= sum;
            }

            h[0] += 0.35f;
            return h;
        }
    }

    private sealed class Biquad
    {
        private float _b0 = 1, _b1, _b2, _a1, _a2, _z1, _z2;

        public void CopyFrom(Biquad other)
        {
            _b0 = other._b0;
            _b1 = other._b1;
            _b2 = other._b2;
            _a1 = other._a1;
            _a2 = other._a2;
        }

        public void LowShelf(float freq, float gainDb) => DesignShelf(freq, gainDb, low: true);

        public void HighShelf(float freq, float gainDb) => DesignShelf(freq, gainDb, low: false);

        public float Process(float x)
        {
            var y = _b0 * x + _z1;
            _z1 = _b1 * x - _a1 * y + _z2;
            _z2 = _b2 * x - _a2 * y;
            return y;
        }

        private void DesignShelf(float freq, float gainDb, bool low)
        {
            var a = MathF.Pow(10f, gainDb / 40f);
            var w0 = 2f * MathF.PI * freq / SampleBuffer.SampleRate;
            var cos = MathF.Cos(w0);
            var sin = MathF.Sin(w0);
            var alpha = sin / 2f * MathF.Sqrt((a + 1 / a) * (1 / 0.707f - 1) + 2);
            float b0, b1, b2, a0, a1, a2;
            if (low)
            {
                b0 = a * ((a + 1) - (a - 1) * cos + 2 * MathF.Sqrt(a) * alpha);
                b1 = 2 * a * ((a - 1) - (a + 1) * cos);
                b2 = a * ((a + 1) - (a - 1) * cos - 2 * MathF.Sqrt(a) * alpha);
                a0 = (a + 1) + (a - 1) * cos + 2 * MathF.Sqrt(a) * alpha;
                a1 = -2 * ((a - 1) + (a + 1) * cos);
                a2 = (a + 1) + (a - 1) * cos - 2 * MathF.Sqrt(a) * alpha;
            }
            else
            {
                b0 = a * ((a + 1) + (a - 1) * cos + 2 * MathF.Sqrt(a) * alpha);
                b1 = -2 * a * ((a - 1) + (a + 1) * cos);
                b2 = a * ((a + 1) + (a - 1) * cos - 2 * MathF.Sqrt(a) * alpha);
                a0 = (a + 1) - (a - 1) * cos + 2 * MathF.Sqrt(a) * alpha;
                a1 = 2 * ((a - 1) - (a + 1) * cos);
                a2 = (a + 1) - (a - 1) * cos - 2 * MathF.Sqrt(a) * alpha;
            }

            _b0 = b0 / a0;
            _b1 = b1 / a0;
            _b2 = b2 / a0;
            _a1 = a1 / a0;
            _a2 = a2 / a0;
        }
    }

    private sealed class Comb
    {
        private readonly float[] _buf;
        private readonly float _feedback;
        private readonly float _damp;
        private float _filter;
        private int _i;

        public Comb(int length, float decay, float damp)
        {
            _buf = new float[length];
            _feedback = decay;
            _damp = damp;
        }

        public float Process(float x)
        {
            var y = _buf[_i];
            _filter = y * (1 - _damp) + _filter * _damp;
            _buf[_i] = x + _filter * _feedback;
            _i++;
            if (_i >= _buf.Length)
            {
                _i = 0;
            }

            return y;
        }
    }

    private sealed class Allpass
    {
        private readonly float[] _buf;
        private readonly float _feedback;
        private int _i;

        public Allpass(int length, float feedback)
        {
            _buf = new float[length];
            _feedback = feedback;
        }

        public float Process(float x)
        {
            var buf = _buf[_i];
            var y = -x + buf;
            _buf[_i] = x + buf * _feedback;
            _i++;
            if (_i >= _buf.Length)
            {
                _i = 0;
            }

            return y;
        }
    }

    private sealed class Allpass1
    {
        private float _z;

        public float Process(float x, float a)
        {
            var y = -x * a + _z;
            _z = x + y * a;
            return y;
        }
    }
}
