# Roadmap

Build for **feel first**, product chrome second, novelty modes last.

## Phase 0 — Docs and lock (done)

This folder. Decisions in [Vision](01-vision-and-decisions.md) stay unless we explicitly revise them.

## Phase 1 — Engine spike (MVP feel)

**Goal:** Type in Notepad and hear a tight click. No pretty UI required.

- Solution + Core + Windows
- One hardcoded WAV (or `content/packs/factory-click`)
- WASAPI shared output, voice pool, polyphony
- `WH_KEYBOARD_LL`, pressed-set, repeat `Off`
- Modifier `Ignore`
- Console or a blank Avalonia window with a Mute checkbox
- Latency sanity check on your laptop

**Exit:** Press-to-sound feels immediate. Hold Space does not machine-gun.

## Phase 2 — Tray product shell

- Avalonia Fluent/Mica window
- Tray icon, mute, quit, start minimized
- First-run copy + privacy sentence
- `settings.json` + autostart
- Single-instance
- Hook/audio status on Home
- Elevation limitation text

**Exit:** You can live with it all day as a tray app.

## Phase 3 — Packs as a product

- `.kfxpack` install / uninstall / validate
- Factory pack copy into AppData
- File association if easy
- Preview
- Reserved `factory-*` pack

**Exit:** You can add a second pack without touching code.

## Phase 4 — Profiles and factory experiences

- Factory profiles (Default / No Effect, Dry, Reverb, Bass, Surround)
- Virtual room dropdown; Effects page; explicit Save / Save as / dirty Reset
- Active profile switch from tray
- Behavior page (repeat, play-on, modifiers, polyphony, velocity)
- Overlay model in JSON; UI can stay simple (one extra “special keys pack”)

**Exit:** Switching profiles changes the room, not just the WAV.

## Phase 5 — FX rack UI

- All v1 modules wired
- Factory immersive tunings
- Safety limiter always on

**Exit:** Hall vs Dry is obvious with the same pack.

## Content pass — piano instrument + custom sample (this slice)

- Frozen **piano-v2** layout (Z C3, A C4, Q C5), Page Down/Up octave, Home/End reset, C2–C6 samples
- Piano is an **Instruments** mode (Start/Stop). Closing the map does not stop it. `piano-classic` stays on disk, hidden from Packs
- Custom sample folder + one armed file; primary = every key, overlay = Enter / Escape / Space
- Retired arcade guns/bombs packs
- Behavior: silence unmapped + combinable silence groups + extra silent key list + piano hold sustain
- Home: volume **boost**, live output-device list
- Settings: reset app settings (volume/boost/device/tray; not profiles/packs)
- Packs: Enable / Disable (hide from pickers); Uninstall for user packs; hover hints on parameters

**Exit:** Start piano from Instruments, play the map, Stop to return to the profile packs. Arm a custom file and assign it as primary or overlay.

## Phase 6 — Installer

- Self-contained publish (`scripts/pack-installer.ps1`)
- Velopack Setup.exe + `VelopackApp.Build().Run()`
- Manual **Check for updates** (feed URL optional until GitHub Releases exist)
- About + MIT + third-party notices
- Settings → Remove local data (AppData wipe; program uninstall stays in Windows Settings)

**Exit:** A friend can install and uninstall it like software. Later app versions (new Instruments, etc.) ship through Velopack updates without re-sending a full manual install story.

## Phase 7 — Harden

- Logging without keys
- Pack size / zip-slip tests
- Buffer preset
- Device fallback
- Crash hygiene
- Icon pass, copy pass

**Exit:** You would send someone the Setup.exe.

## Later (designed, not scheduled)

| Item | Depends on |
| --- | --- |
| Speak-key / phoneme mode | Phase 5 + privacy review |
| Pack catalog / URL install | Phase 3 + network policy |
| Per-app or fullscreen mute | Privacy review (no titles in logs) |
| Exclusive WASAPI | Phase 1 measurements |
| Chorus / extra `IFxModule`s | Phase 5 |
| Per-key play-on map | Phase 4 |
| OGG samples | Decoder + pack spec v1.1 |
| Code signing | Public/product release |
| macOS `KeyFXBoard.Mac` | Windows success + Core stability |
| Portable mode | `IAppPaths` |
| Paid packs | Legal + same `.kfxpack` |

## Explicitly not in the first public OSS drop unless Phases 1–6 are done

- Store listing
- RGB visualizer
- Plugin script host
- Windows Service rewrite
- Electron rewrite

## Suggested first implementation slice

When you say start coding: **Phase 1 only**. Do not scaffold every ViewModel first.

Definition of done for that slice is in Phase 1 **Exit**, not “all docs implemented.”
