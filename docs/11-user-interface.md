# User interface

## Character

A **modern Windows 11** program: dark Fluent chrome, Mica on the shell, compact tray utility — not a DAW and not a gamer RGB explosion.

The app is used in two modes:

1. **Invisible** — tray, sounds as you type
2. **Workshop** — profiles, packs, FX when you want to tinker

## Surfaces

### Tray icon

- Default: app mark
- Muted: struck / dimmed mark
- Left click: toggle mute (fast) **or** open window — **lock: left click toggles mute, double-click opens**. Right click: menu

Tray menu:

- Mute / Unmute
- Start / Stop piano
- Profiles (radio list)
- Open Key FX Board
- Quit (asks “are you sure?”; does not open the unsaved-profile Save dialog)

### Main window

Single window, navigation pane (Avalonia `SplitView` / `NavigationView` style):

| Page | Content |
| --- | --- |
| Home | Active profile, mute, volume, **boost**, **octave**, output device (live plug-in refresh), piano-on banner, hook/audio status |
| Instruments | Piano Start/Stop, piano map |
| Profiles | System + user list, per-card Use / Duplicate / Rename / Reset / Delete, packs, overlay, **Custom sound** picker when Custom sample is assigned (frozen while piano is on) |
| Packs | Custom sample folder (arm / preview), library Enable / Disable, install, uninstall (user packs only) |
| Effects | Profile name, Save (user only) / Save as / Reset, virtual room, nested modules |
| Behavior | Repeat, play-on, modifiers, polyphony, velocity, **hold sustain / release** (piano), silence unmapped, silence groups (combinable), extra silent keys |
| Settings | Output device, autostart, tray options, **Check for updates**, **Reset app settings**, **Remove local data**, data folder, elevation note |
| About | Version, MIT, privacy one-liner, licenses of default pack |

### First run

Modal, one screen:

- What this app does
- Privacy: we do not record keys
- Start with Windows checkbox
- Choose a factory profile (cards)
- Done

### Pack install dialog

Progress + validation errors in plain language (“WAV too large”, “manifest missing id”).

### Uninstall pack

List of profiles that will break; confirm.

## Visual rules

- Dark default, Light supported via `theme` setting
- 8 px spacing grid, 32/40 px title
- Primary accent: one color (neutral teal or amber — pick at implementation, stay consistent)
- No animated RGB key background in v1
- FX knobs: sliders are enough; rotary knobs optional later
- Status color: green hook+audio, amber fallback device, red hook failed

## Copy (tone)

Short, direct, not cute-scary.

Good: “Sounds will not play in Administrator windows.”
Bad: “We failed to inject the capture driver!!!”

## Accessibility

- Keyboard-navigable settings
- Contrast-safe text on Mica
- Do not steal focus on key-repeat
- First-run must be closable with Esc

The app must never become a key-eater. We do not block input.

## Window lifetime

- Close button: hide to tray if `minimizeToTrayOnClose` (default true)
- Quit only from tray or Settings
- `startMinimized` after the user has completed first run

## Empty and error states

- No packs: “Install a .kfxpack or restore factory content”
- Hook failed: banner on every page until dismissed or fixed
- Mute: Home says “Mute” is on. There is no Silent profile.

## Out of scope for v1 UI

- Visualizer FFT candy (fun later)
- In-app pack store
- Per-key piano roll editor (a simple overlay key list is enough)
- Multi-window mixer
