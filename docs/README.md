# Key FX Board — Documentation

Product documentation for **Key FX Board**, a Windows tray app that plays configurable sound packs and an FX rack on every keystroke.

These docs are the source of truth for planning and implementation. If code and docs disagree later, update both.

## How to read this

| Order | Doc | What it answers |
| --- | --- | --- |
| 1 | [Vision and decisions](01-vision-and-decisions.md) | What we are building, and what is locked |
| 2 | [Architecture](02-architecture.md) | Layers, boundaries, what is *not* a Windows service |
| 3 | [System design](03-system-design.md) | Threads, data flow, latency budget, failure modes |
| 4 | [Technology](04-technology.md) | Stack, libraries, why these choices |
| 5 | [Keyboard hooks](05-keyboard-hooks.md) | How keystrokes are captured and filtered |
| 6 | [Audio engine](06-audio-engine.md) | Preload, voices, devices, polyphony |
| 7 | [FX rack](07-fx-rack.md) | Effect chain, presets, what ships in v1 |
| 8 | [Pack format](08-pack-format.md) | `.kfxpack` spec, install / uninstall |
| 9 | [Profiles and configuration](09-profiles-and-configuration.md) | Experiences, factory profiles, settings |
| 10 | [Installer, updates, and lifecycle](10-installer-updates-and-lifecycle.md) | Velopack, app vs pack install |
| 11 | [User interface](11-user-interface.md) | Tray, windows, first-run |
| 12 | [Privacy, security, and licensing](12-privacy-security-and-licensing.md) | No keylogging, MIT, sample licenses |
| 13 | [Project structure](13-project-structure.md) | Solution layout, projects, Mac-ready seams |
| 14 | [Roadmap](14-roadmap.md) | Phased build, MVP through later product |

## Locked identity

| Item | Value |
| --- | --- |
| Product name | Key FX Board |
| App id / exe | `KeyFXBoard` |
| Pack extension | `.kfxpack` |
| Code license | MIT |
| First platform | Windows 10 / 11 (x64) |
| Later platform | macOS, behind the same Core interfaces |

## One-sentence product

A modern Windows tray program that maps keystrokes to preloaded samples, runs them through a per-profile FX rack, and treats sound packs and profiles as first-class, installable product pieces.
