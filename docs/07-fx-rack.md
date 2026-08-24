# FX rack

## Ownership

The FX rack belongs to the **profile**, not the pack.

Same piano pack can be “dry and close” or “hall immersive.” Factory profiles are curated combinations of pack + behavior + FX.

## v1 chain (fixed order)

Order is product-normative. Users can bypass a slot (wet/dry = 0 or enable = false). They cannot reorder in v1.

```
Input gain
  → EQ (low shelf “bass” + optional high shelf)
  → Dynamic bass
  → Compressor
  → Saturation / distortion
  → Chorus
  → Flanger
  → Phaser
  → Delay / echo
  → Convolver (built-in short IR)
  → Reverb
  → Stereo width
  → Crossfeed
  → Limiter (always last, cannot be removed)
```

Each module except the limiter has **enabled** plus an **intensity** (mix/amount) slider. Inner knobs stay where they change character. Users cannot reorder in v1.

## Module specs

Values below are starting UI ranges, not sacred DSP numbers. Implement, then tune by ear.

### Input gain

- Range: −12 dB to +12 dB
- Default: 0 dB

### EQ

v1 is a simple musical EQ, not a 10-band graphic.

| Band | Type | Freq | Gain range |
| --- | --- | --- | --- |
| Bass | Low shelf | ~120 Hz | −12 to +12 dB |
| Air | High shelf | ~8 kHz | −12 to +12 dB |

Optional later: mid peak. Do not ship a 31-band EQ.

### Compressor

- Threshold, ratio, attack, release, makeup
- Default: gentle glue (or bypassed on Dry)

### Saturation / distortion

- Drive + mix (dry/wet)
- Soft sat default; a harder “gun crush” curve as a style enum: `Off`, `Tape`, `Crush`

### Delay / echo (“retention” lives here)

The user’s “retention” maps to **delay feedback + time** and/or **reverb decay**, not a mysterious third engine.

| Control | Meaning |
| --- | --- |
| Time | 50–600 ms |
| Feedback | 0–70% (hard cap so it cannot run away) |
| Mix | 0–100% |
| Sync | Off in v1 (no BPM) |

### Reverb

- Size / decay
- Damping
- Mix
- Default factory Hall room is wet; Dry is almost dry

### Dynamic bass

- Enable + intensity
- Envelope-followed low-band boost, not a second compressor

### Chorus / flanger / phaser

- Enable + intensity, plus rate/depth (flanger also has feedback)
- Modulation before delay/reverb

### Convolver

- Enable + intensity
- Built-in **Short** / **Medium** IRs only. User WAV IRs later.

### Stereo width and crossfeed

- Width: mid/side amount
- Crossfeed: short opposite-channel delay (headphone speaker-blend)

### Limiter

- Ceiling about −0.3 dBFS
- Always on
- Not exposed as a creative toy in v1 beyond “safety on”

## Bypass and presets

Each module except the limiter has `enabled`.

A profile stores the full parameter block. Factory profiles ship named FX as part of the profile, not as a separate file — **unless** we extract `fx` to a reusable preset id later.

v1 file shape: FX inline on the profile. v1.1 may add `fxPresets\hall.json` and a `fxPresetId` reference. Design the JSON so extraction is easy (`fx` is one object).

## Implementation notes

- All modules are `IFxModule : ISampleProvider` or a single graph that processes an interleaved float span in-place / out-of-place.
- Coefficients update from UI via atomics or a parameter snapshot swapped with the graph.
- Changing a knob must not allocate on the audio thread (precompute biquad coeffs on the UI/worker, publish).
- Reverb: start with a well-known Schroeder / Dattorro-ish or a small convolution **only if** IR size is tiny. Prefer algorithmic in v1 so packs stay small and CPU is predictable.
- Delay: delay line sized to max time at 48 kHz, allocated at graph build.

NAudio extras may cover some of this. If quality is poor, write our own biquad + delay + cheap reverb. Do not take a VST host.

## CPU

Worst case: delay + reverb + full polyphony.

Budget: FX should stay well under half of the audio period on a 4-core laptop. If not, we drop saturation first, then shorten reverb.

**Default / No Effect** is limiter only (FX frozen). For a lighter look, use Dry.

## Out of scope (document, do not build)

- Convolution halls with 2-second IRs / user IR load (built-in short IRs ship now)
- VST host, 31-band graphic EQ, live system-audio sidechain
- Named headphone/device DDC curve library
- Bitcrush as a second distortion mode is cheap — allowed as the `Crush` style, not a sixth module
- Sidechain from system audio
- Per-key FX

## Factory immersive intent

These are **profile** goals, not extra engines:

| Factory profile | FX intent |
| --- | --- |
| Default / No Effect | Limiter only; Effects frozen |
| Dry | Dry, tiny sat, limiter only |
| Reverb | Hall template, long reverb, light delay |
| Bass | Dry room + dynamic bass + low shelf |
| Surround | Width, crossfeed, short room, light chorus |
