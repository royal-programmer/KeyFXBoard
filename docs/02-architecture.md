# Architecture

## Shape

Key FX Board is a single interactive user-mode process:

- A **tray-resident** Avalonia desktop app
- A **Windows keyboard source** running on a dedicated hook thread
- A **Core** engine (map → voices → FX → mix) that does not reference Avalonia or Win32 UI
- A **Windows audio backend** (NAudio / WASAPI)

```
┌─────────────────────────────────────────────────────────────┐
│                     KeyFXBoard.App                          │
│   Tray · Settings · Pack library · Profile editor · FX UI   │
└────────────▲────────────────────────────────────┬───────────┘
             │ commands / bindings                │ config I/O
┌────────────┴────────────────────────────────────▼───────────┐
│                     KeyFXBoard.Core                         │
│   Profiles · Pack store · Mapper · Voice pool · FX graph    │
└────────────▲────────────────────────────────────┬───────────┘
             │ KeyEvent                           │ Audio frames
┌────────────┴──────────┐              ┌──────────▼───────────┐
│  KeyFXBoard.Windows   │              │  KeyFXBoard.Windows  │
│  Low-level key hook   │              │  WASAPI output       │
└───────────────────────┘              └──────────────────────┘
```

UI never produces sound directly. The hook never touches the disk. The audio thread never waits on the UI thread.

## Why not a Windows Service

| Service | Tray app |
| --- | --- |
| Session 0 isolation — no user desktop | Same session as the user |
| Cannot play to the user’s device cleanly | WASAPI shared/exclusive in-session |
| Poor fit for a settings UI | Native window + tray |
| Extra IPC for no gain | One process |

Startup is a **user logon task** or Startup folder / registry run key, not `services.msc`.

## Logical layers

### 1. Presentation (`KeyFXBoard.App`)

- Avalonia windows and tray
- ViewModels over Core services
- File dialogs for `.kfxpack`
- First-run and update chrome (Velopack)

### 2. Domain (`KeyFXBoard.Core`)

Stable types and rules:

- `KeyId` — platform-neutral key names (`KeyA`, `Enter`, `Space`, `D1`)
- `KeyEvent` — down / up / repeat, timestamp, injected flag
- `Pack`, `PackManifest`, `SampleBuffer`
- `Profile`, `FxPreset`, `BehaviorSettings`
- `IPackStore`, `IProfileStore`, `IKeyMapper`, `IVoiceEngine`, `IFxGraph`

Core has **no** `HWND`, no Avalonia, no NAudio types leaking out of ports.

### 3. Platform (`KeyFXBoard.Windows`)

Implements:

- `IKeyboardSource` — `WH_KEYBOARD_LL`
- `IAudioOutput` — NAudio WASAPI
- `IAutostart` — current-user run key
- `IProcessElevation` — “we are / are not elevated” for the docs warning

A future `KeyFXBoard.Mac` implements the same interfaces.

## Primary data flow

```
OS keystroke
  → IKeyboardSource (hook thread)
  → filter (injected? repeat policy? modifiers? muted? panic?)
  → IKeyMapper (profile + packs → list of SampleBuffer + gain)
  → IVoiceEngine.Trigger(...)   // lock-free / preallocated
  → mixer → IFxGraph → IAudioOutput
```

Configuration writes (change profile, tweak reverb) go:

```
UI → ProfileStore.Save → Engine.HotReload(profile)
```

Hot reload **must not** glitch more than one buffer. Preferred approach: build a new FX graph, swap on a cycle boundary, then dispose the old graph. Pack switches unload old buffers after the new ones are ready (or after a short cross-fade of silence).

## Pack vs profile vs settings

```
┌─────────────┐     used by      ┌──────────────┐
│  .kfxpack   │◄─────────────────│   Profile    │
│  samples    │  primary+overlay │  behavior    │
│  keymap     │                  │  FX preset   │
└─────────────┘                  │  output      │
                                 └──────▲───────┘
                                        │ active
                                 ┌──────┴───────┐
                                 │ settings.json│
                                 │ mute, startup│
                                 │ last profile │
                                 └──────────────┘
```

- **Pack** = sounds + default key → sample map + license.
- **Profile** = which pack(s), how keys behave, FX, device, volume.
- **Settings** = app-level: autostart, theme, last profile, global mute hotkey.

A user can run the same piano pack through “dry studio” and “wet hall” as two profiles.

## Memory model

Installed packs live on disk:

```
%AppData%\KeyFXBoard\packs\<pack-id>\
```

**Resident** (decoded PCM in RAM): only packs referenced by the **active** profile (primary + overlays).

Uninstall pack = delete folder + drop from any profile that referenced it (fall back to factory default pack) + unload if it was resident.

This is how “many packs” and “preload for latency” coexist.

## Process and privilege

- Default: **current user**, medium integrity.
- Global low-level hook works for same-or-lower integrity processes.
- **Elevated apps will not produce sounds.** Do not request admin in v1. Show this in Settings → About / Diagnostics.
- No kernel driver. No filter driver. No accessibility API required for v1.

## Extension seams (build later, define now)

| Seam | Later use |
| --- | --- |
| `IKeyboardSource` | macOS CGEvent tap |
| `IAudioOutput` | Core Audio |
| `IFxModule` | Extra effects, third-party later |
| `IPackSource` | Catalog / URL install |
| `ITtsVoice` | Speak-key novelty mode |

Do not implement speak-key or catalog until the core feel is right.

## Dependency rule

```
App     → Core, Windows
Windows → Core
Core    → (contracts only)
Tests   → Core, and Windows with explicit flags
```

Nothing in Core may import `Avalonia.*`, `NAudio.*`, or `Windows.*` UI namespaces. NAudio stays inside the Windows (or a dedicated Audio) adapter. If we later extract `KeyFXBoard.Audio.NAudio`, Core still only sees `IAudioOutput` and `ISampleDecoder`.

Recommended practical split for v1 (fewer projects, same rule):

- `KeyFXBoard.Core` — domain + in-memory engine
- `KeyFXBoard.Windows` — hook + NAudio + autostart
- `KeyFXBoard.App` — Avalonia host

See [Project structure](13-project-structure.md).
