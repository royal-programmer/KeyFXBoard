# Audio engine

## Goal

Play preloaded samples with polyphony and an FX graph, on a dedicated output path, fast enough that the sound is part of the key feel.

## Principles

1. **Decode once, play many.** Disk is off the realtime path.
2. **No allocations in the callback.** Voice pool is pre-sized.
3. **One mixer, one device.** v1 is not a multi-device router.
4. **Shared WASAPI default.** Compatible with YouTube, Discord, games.
5. **Cap voices.** Fun must not become a fan-spinning synth.

## Internal format

After load, every sample becomes:

```text
SampleBuffer
  Id            : packId + relative path
  SampleRate    : 48000
  Channels      : 2 (stereo)
  Frames        : int
  Data          : float[]   // interleaved L,R, length = Frames * 2
```

Load pipeline (worker thread):

1. Read file bytes from the already-unzipped pack folder
2. Decode with NAudio (`WaveFileReader` / `AudioFileReader`)
3. Resample to 48 kHz if needed (NAudio resampler)
4. Upmix mono → stereo, downmix 5.1 → stereo (v1: reject > 2 ch with a clear error, or downmix)
5. Convert to float32 −1..1
6. Freeze as immutable `SampleBuffer`

**Preferred ship format for official packs:** 48 kHz, 16-bit or 24-bit WAV, mono or stereo, short one-shots (10–400 ms). Longer tails are allowed but cost RAM.

v1 pack install **rejects** MP3/OGG if we have not taken a decoder dependency. WAV only in v1 keeps the engine honest. We can add OGG later for pack size.

## Preload rules

When a profile becomes active:

1. Collect pack ids: `primaryPackId` + each overlay `packId`
2. Load each pack’s referenced samples (and fallbacks)
3. Optionally decode `preview.wav` only when the UI plays a preview (not required in the voice pool)
4. Publish `PackRuntime`
5. Unload buffers from the previous profile only after in-flight voices are gone or on hard-stop

**Do not** preload every installed pack.

If two overlays share the same pack id, load it once.

## Voice pool

```text
Voice
  InUse       : int (0/1, atomic claim)
  Buffer      : SampleBuffer
  Frame       : int playhead
  Gain        : float
  StartedAt   : ulong (optional, for steal)
```

`Trigger(buffer, gain)`:

1. Find a free voice (`Interlocked.CompareExchange`)
2. If none, steal index `oldest`
3. Set buffer, frame = 0, gain, InUse = 1

Audio callback, per voice:

- Mix `gain * buffer[frame..]` into the accumulation buffer
- Advance playhead
- If playhead ≥ frames → InUse = 0

No per-voice FX in v1. All voices hit **one** shared FX graph after the mix. That matches “the room,” not “20 different reverbs.”

If we ever need per-pack EQ, add a cheap pre-gain / 3-band on the voice before the mix — not a second reverb per voice.

## Mixer and output

```
voice mix (float stereo)
  → app volume × profile room volume
  → output boost (0–18 dB, settings-level, after mix)
  → FX graph (input gain first)
  → limiter (last, always on at a safety ceiling)
  → WASAPI shared (NAudio WasapiOut)
```

Device selection:

- `default` follows the Windows **Default Device** (console role), and only if that endpoint is **Active**
- Unplugged jacks are omitted from the picker
- Or a persisted device id from the enumerator (Home and Settings)
- If the device disappears, fall back to default, warn once, keep playing

Master volume is on the profile. Global mute is a settings-level gain of 0, applied after the profile volume (mute must win).

## Polyphony

| Setting | Default | Clamp |
| --- | --- | --- |
| `polyphony` | 24 | 1–64 |

UI copy: “How many sounds can overlap. Lower is safer on quiet laptops.”

## Velocity and variants

On trigger (mapper, before voice start):

- **Variant:** if the key has `down: [a.wav, b.wav, c.wav]`, pick `Random` or `Cycle` per profile
- **Velocity random:** `gain = baseGain * (1 ± velocityRandom)` with `velocityRandom` default `0.12`

There is no real keyboard velocity from a normal laptop. This is fake dynamics so it does not sound like a sampler demo.

Hold sustain (chromatic packs only): while the key is down the voice loops a stable portion of the sample; key-up fades over `releaseMs`. One-shots never loop.

## Preview / Select

The Packs page **Select** button assigns the pack as the active profile’s primary. The engine can still one-shot a representative key through the same output path.

Do not spin a second `WasapiOut`.

## Buffer size (advanced)

Expose three presets, not a raw exclusive-mode panel in v1:

| Preset | Intent |
| --- | --- |
| Stable | Default, shared, safer period |
| Tight | Smaller period, may glitch on weak machines |
| Exclusive | Later, not v1 unless we need it to hit 20 ms |

Measure on a typical laptop before locking the default period in code.

## RAM expectations

Rough: `seconds * 48000 * 2 ch * 4 bytes`.

A 100 ms stereo one-shot ≈ 38 KB. A 100-key pack with down+up and 2 variants ≈ a few MB. A sloppy pack of 5-second WAVs will be obvious; the installer UI should show decoded-size estimate after validation.

## What we will not do in v1

- Stream from disk
- Per-key pitch shift (piano-as-chromatic-map can be **pre-rendered samples**, not live resampler)
- Spatial / HRTF
- ASIO
- Recording the mix to a file (that becomes a key-timed recording — privacy trap)

## Failure

If WASAPI fails to start: show a blocking-but-simple error, keep the hook attached or detach (prefer **detach** so we do not queue forever). Retry from Settings.
