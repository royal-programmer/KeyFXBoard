# System design

This document describes runtime behavior: threads, queues, timing, and failure handling.

## Design goals

| Goal | Budget / rule |
| --- | --- |
| Press-to-sound | Target ≤ 20 ms, feel-broken > 40 ms |
| Hook callback | ≤ 1 ms; enqueue only, never decode or allocate |
| Audio callback | No UI, no disk, no pack unzip, no locks held across a mix |
| UI | 60 fps is enough; never on the audio path |
| RAM | Active profile only; rough cap documented in UI if a pack is huge |
| CPU | Polyphony cap + voice stealing; idle near zero when silent |

## Components

```
┌─────────────┐   KeyEvent    ┌─────────────┐  VoiceTrigger  ┌────────────┐
│ Keyboard    │──────────────►│ Filter +    │───────────────►│ Voice pool │
│ source      │               │ mapper      │                │            │
└─────────────┘               └─────────────┘                └─────┬──────┘
                                                                   │
                                                              mix (float)
                                                                   ▼
                                                          ┌────────────────┐
                                                          │ FX graph       │
                                                          └────────┬───────┘
                                                                   ▼
                                                          ┌────────────────┐
                                                          │ WASAPI output  │
                                                          └────────────────┘
```

Supporting components (off the realtime path):

- **Pack store** — install, uninstall, enumerate, load manifest
- **Profile store** — factory + user CRUD, active profile
- **Sample loader** — decode WAV → `float[]` (or interleaved stereo) on a worker thread
- **Settings store** — `settings.json`
- **Update host** — Velopack, idle time only

## Thread model

| Thread | Owner | Allowed work |
| --- | --- | --- |
| UI / Avalonia dispatcher | App | Bindings, dialogs, tray |
| Hook thread | Windows keyboard source | Translate `KBDLLHOOKSTRUCT` → `KeyEvent`, enqueue |
| Audio realtime | NAudio / WASAPI | Mix voices, run FX, write buffer |
| Loader / worker | Core | Decode samples, unzip packs, persist JSON |
| Velopack | App | Check / apply updates when idle |

### Communication

```
Hook thread  --(bounded channel, KeyEvent)-->  mapper (can be hook thread
                                                if mapping is O(1) dictionary)

Mapper       --(preallocated trigger struct)--> voice pool  (audio thread
                                                consumes; producer is wait-free)

UI           --(immutable Profile snapshot)-->  engine swap on audio barrier

Loader       --(PackRuntime ready)---------->  engine swap
```

**Mapping on the hook thread is allowed** if it is a dictionary lookup and a copy of a trigger descriptor. It is **not** allowed to touch `Conversion` / file I/O.

Preferred: hook thread only enqueues `KeyEvent`. A dedicated **engine thread** is *not* required if the audio callback pulls a lock-free SPSC/MPSC queue of triggers. That is the default design.

```
Hook  →  MPSC queue<KeyEvent>  →  audio callback:
                                    drain queue
                                    map (profile snapshot)
                                    start voices
                                    mix + FX
```

Mapping inside the audio callback is acceptable if it is pure CPU, no alloc, snapshot is immutable.

## Latency budget (48 kHz, shared WASAPI)

Assume a 10–15 ms output buffer (tunable). Remaining budget is software.

| Stage | Target |
| --- | --- |
| Hook → queue | < 0.5 ms |
| Queue wait | 0–one audio period (~5–15 ms) |
| Map + start voice | < 0.2 ms |
| Mix + FX | Must finish within the period with headroom |
| Device buffer | Dominant term |

**v1 uses WASAPI shared mode** for compatibility (other apps keep working). Exclusive mode is an advanced profile option later if we miss the feel target on a machine.

Document a Settings → Advanced control: **buffer size** (default / small / smallest). Default must be stable on cheap laptops, not just a gaming PC.

## Key event state machine

Per physical key (`KeyId`):

```
Idle --down--> Down --up--> Idle
         \        \
          \        +-- OS repeat --> Repeat (optional fire)
           +-- ignore injected if setting says so
```

Implementation: `HashSet<KeyId>` or a 256-slot bitset of currently down keys.

| Repeat mode | Down | OS repeat | Up |
| --- | --- | --- | --- |
| `Off` | Fire | Ignore | Fire if play-on includes Up |
| `On` | Fire | Fire | Fire if Up |
| `RateLimit` | Fire | Fire if token available | Fire if Up |

Rate limit is a simple token bucket on the **profile**, not per key, unless we later add per-key rates. Default bucket: `repeatRateLimitHz` (e.g. 8).

## Voice and polyphony

- Voice = playhead + gain + pointer to immutable `SampleBuffer` + optional one-shot envelope.
- Pool size = profile `polyphony` (default 24, clamp 1–64).
- If the pool is full: **steal the oldest voice** (or the quietest, if we have a quick gain estimate). Never allocate a 25th voice on the audio thread.
- Key-up can start a *second* voice (the release sample). That counts toward polyphony.

Chords and smashed keys must stay bounded. This is a product requirement, not an optimization.

## Profile hot-swap

When the user changes the active profile or edits FX:

1. Worker builds `PackRuntime` + `FxGraph` + `BehaviorSnapshot`.
2. Publish with `Volatile.Write` / immutable swap (`Interlocked.Exchange` of a reference).
3. Audio thread sees the new snapshot on the next callback.
4. In-flight voices either:
   - **Finish on the old buffers** (buffers stay alive until voice end + 1 graph), or
   - **Hard-stop** on pack change (simpler, acceptable for v1 pack switches).

v1 rule:

- **FX / volume / behavior tweak:** keep ringing voices, new voices use new FX (or swap whole graph — new voices only is simpler; *document: moving a reverb knob may cut the tail*). Prefer **cross-swap graph** that still processes old voices through the *new* graph if buffers are compatible. Simplest correct v1: **stop all voices on pack change; keep voices on FX-only change** even if the tail quality is imperfect.

## Pack install pipeline

```
User picks .kfxpack
  → copy to temp
  → unzip
  → validate schema, id, paths (zip-slip check)
  → verify every referenced sample exists and decodes
  → move to packs\<id>\
  → index refresh
  → if a profile uses it and is active, offer “Load now”
```

Uninstall:

```
Confirm
  → if active, switch profiles / drop overlay first
  → unload buffers
  → delete directory
  → rewrite profiles that referenced it (warning + fallback)
```

Never unzip on the audio thread. Never leave a half-installed pack in the live index (write to `*.partial` then rename).

## Configuration files

All under `%AppData%\KeyFXBoard\` unless the installer is portable later.

| File | Role |
| --- | --- |
| `settings.json` | Autostart, theme, active profile id, mute, hotkeys, last device fallback |
| `profiles\factory-*.json` | Shipped copies or generated from embedded resources |
| `profiles\user-*.json` | User-created / duplicated |
| `packs\<id>\` | Installed pack trees |
| `logs\app-.log` | Serilog-style rolling logs — **no key names, no vk codes** |

Factory profile JSON is created in AppData only when the file is missing. Launch never overwrites a live factory file. Catalog defaults live in code; Reset writes that snapshot over the live file. User profiles are never overwritten by startup.

## Error handling

| Failure | User-visible | Engine |
| --- | --- | --- |
| Hook failed to install | Banner + “retry as normal user / check AV” | Silent app, no sounds |
| Device missing | Fall back to default device, toast | Keep running |
| Pack corrupt | Install rejected with reason | Old packs unchanged |
| Sample decode fail | Pack install fail or skip that key + fallback sample | Use pack fallback |
| Audio glitch / dropout | Optional diagnostics counter | Drop the period, do not crash |
| Unhandled | Crash log without key data | Process exit; tray gone — Velopack can restart if we enable it later |

## Diagnostics (safe)

Allowed in logs:

- Profile id, pack id, device name, buffer size, hook attached yes/no, exception type

Never:

- `vkCode`, `KeyId` of user presses, timestamps of individual keys, window titles of the foreground app (window title can leak passwords / URLs)

Foreground-app mute is a **later** feature and needs a privacy review before we read `GetForegroundWindow` titles.

## Clock

Use `Stopwatch` / `QPC` for rate limiting and diagnostics. Audio uses the device sample clock. Do not mix `DateTime.Now` into the audio path.

## Concurrency rules (normative)

1. `SampleBuffer` is immutable after load.
2. `ProfileSnapshot` is immutable after publish.
3. Voice pool slots are only mutated on the audio thread, except `Trigger` which only writes to a free slot via atomic claim.
4. UI holds no pointers into voice slots.
5. Dispose of old graphs only after `audioEpoch` has advanced past the swap.

## Testability

Core must be testable without a keyboard or sound card:

- `IKeyboardSource` can be a fake that pushes `KeyEvent`s
- `IAudioOutput` can be a `CollectingOutput` that records floats
- Golden test: “A down + Repeat Off + Down-only → exactly one voice start”

Windows hook tests are integration-only and optional in CI.
