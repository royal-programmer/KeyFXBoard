# Vision and decisions

## Problem

Typing is silent unless you own a mechanical keyboard. People already fake that feeling with apps like Mechvibes or Bucklespring. Those tools are usually one sound set and almost no processing.

Key FX Board is the next step: **themes, per-key mapping, multiple packs in one profile, and a real FX rack**, shipped as a productized Windows app you can install, update, and uninstall cleanly.

## Who it is for

| Stage | Audience |
| --- | --- |
| Now | You — a daily driver on Windows |
| Next | Open-source users who want a pack format and a real installer |
| Later | A marketable product (packs, pro features) on the same core |

Design for the later stages now. Do not build the store, paid packs, or marketing site in v1.

## What it is

A **system-tray desktop application** that:

1. Listens to keyboard input globally on the current Windows session.
2. Maps a key event to one or more preloaded samples from the **active profile**.
3. Mixes those voices through a **profile-owned FX chain**.
4. Plays to a chosen output device with low latency.
5. Lets the user install / uninstall **packs**, switch **profiles**, and configure behavior and FX.

It is **not** a Windows Service. Services run in Session 0, cannot easily hook the interactive desktop, and are a poor fit for audio and UI.

“Always on” is achieved by:

- Start with Windows (user-level startup)
- Live in the tray
- Optional mute / panic hotkey

## What it is not (v1)

- A keylogger, macro tool, or text expander
- Live TTS on every key (novelty mode, later)
- A DAW
- A cross-platform 1.0 (Windows first; macOS when Windows is a success)
- An Electron app
- An audio engine that loads WAVs from disk on each keypress

## Locked product decisions

1. **Windows only for v1.** macOS later. Core stays platform-agnostic.
2. **Stack:** .NET 10 LTS + Avalonia (Fluent / Mica) + NAudio + Velopack. See [Technology](04-technology.md).
3. **Start with one pack**, format designed for many. Packs are installed and uninstalled like a product feature. Only the **active profile’s** packs are preloaded into RAM.
4. **Personal first**, architecture fit for open source and later commercial packs.
5. **Productized distribution:** installer, Add/Remove Programs, pack files, profile files. Open source does not mean “unzip a folder and hope.”

## Locked behavior decisions

| Topic | Decision |
| --- | --- |
| Key-repeat | Configurable per profile: `Off` (default), `On`, `RateLimit` |
| Key-down / key-up | Configurable per profile: `Down`, `Up`, `Both`. Packs store both from day one |
| Variants / velocity | Small randomness so one WAV does not sound robotic |
| FX | Owned by the **profile**, not the pack |
| Profiles | Factory (immersive presets) + user profiles. A profile may use multiple packs |
| Privacy | Never persist keystrokes. No key history |
| Antivirus / signing | Unsigned hooks will get flagged. Accept for local use; sign at product stage |
| Samples in MVP | Placeholder / original / CC0. Real licensed libraries are a content problem |
| Solution split | `Core` / `Windows` / `App` so Mac replaces only the Windows project |

## Locked identity

| Item | Value |
| --- | --- |
| Display name | Key FX Board |
| Process / exe / pack id | `KeyFXBoard` |
| Pack extension | `.kfxpack` |
| Code license | MIT |
| Default sample license | CC0 or original work, declared per pack |

## Success criteria (feel, not features)

The product is working when:

- A keypress produces sound **before** the eye finishes registering the key travel (target: under ~20 ms press-to-sound).
- Holding Space does **not** machine-gun unless repeat is enabled.
- Switching a profile swaps the experience without restarting the app.
- Installing and uninstalling a pack is obvious and clean.
- Uninstalling the app from Windows Settings is clean.
- The app never writes what you typed to disk.

## Non-goals that people will ask for

Document these so they do not sneak into MVP:

- Speak the key name
- Per-application mute lists (except a later “mute in fullscreen” if easy)
- Cloud pack store
- MIDI out / streaming overlay
- Linux
- Running elevated to catch admin windows

## Design principles

1. **Latency over features.** A late click feels broken. Cut features before missing the budget.
2. **Profiles are the experience.** Packs are raw material. FX and behavior sit on the profile.
3. **Preload the active set only.** Many installed packs, few resident packs.
4. **Configurable, with good defaults.** Factory profiles should sound great with zero setup.
5. **Privacy is a feature.** Global hooks are enough of a trust problem without logs.
6. **Seams over speculation.** Interfaces for Mac and extra FX; implementations only when needed.
