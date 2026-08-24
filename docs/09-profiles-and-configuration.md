# Profiles and configuration

## Idea

A **profile** is a saved setup: pack(s), overlay pack, behavior, **virtual room**, and the FX block.

A **virtual room** is an FX template (Default / Dry / Small / Hall / Surround) applied onto the profile. Changing the room stomps current FX; Effects page then fine-tunes. Behavior stays separate. Save on Effects writes the **whole** profile (packs, behavior, room, FX).

System profiles cannot be saved. Tweaks are live until **Save as** (name prompt; reserved system names blocked). The new user profile becomes active. User profiles **Save** a checkpoint; **Reset** restores last save (system Reset restores the shipped catalog).

## Profile kinds

| Kind | `isFactory` | Save | Delete | Reset |
| --- | --- | --- | --- | --- |
| System | true | No (Save as only) | No | Shipped catalog, when dirty |
| User | false | Yes — becomes the new Reset target | Yes | Last save, when dirty |

Startup creates a factory file only if it is **missing**. Existing live files are not overwritten. Catalog defaults live in code so Reset has a source that launch cannot clobber.

## Multi-pack

```text
Profile
  primaryPackId: "factory-click"
  overlays:
    - packId: "cinema-gun"
      keys: ["Enter", "Escape", "Space"]
```

- **Primary** covers the default keymap
- **Overlays** win on listed keys (first match in list order)
- MVP implementation may ship UI as “one pack” and still **serialize** empty `overlays` so we do not migrate later
- Immersive factory profile is the first overlay user

RAM = union of primary + overlay packs while the profile is active.

## Behavior block

All configurable, as locked:

| Key | Type | Default | Meaning |
| --- | --- | --- | --- |
| `repeat` | `Off` \| `On` \| `RateLimit` | `Off` | OS auto-repeat |
| `repeatRateLimitHz` | number | `8` | Used when `RateLimit` |
| `playOn` | `Down` \| `Up` \| `Both` | `Down` | Which edges make sound |
| `modifierPolicy` | `Ignore` \| `Play` | `Ignore` | Ctrl/Alt/Win mute |
| `ignoreInjected` | bool | `true` | Skip synthetic keys |
| `polyphony` | int | `24` | Voice cap |
| `velocityRandom` | 0–0.5 | `0.12` | Gain jitter |
| `holdSustain` | bool | `false` (forced on while piano instrument mode is running) | Chromatic packs keep the tone while the key is held; clicks ignore this |
| `releaseMs` | number | `280` | Fade after key-up when hold sustain is on |
| `forceSampleKey` | string? | `null` | Optional: lock one mapped sample onto every key |
| `variantMode` | `Random` \| `Cycle` \| `First` | `Random` | Multi-sample pick |
| `silenceUnmapped` | bool | `false` | Skip keys the active packs do not map (even if a fallback would have played) |
| `silentGroups` | string[] | `[]` | Any mix of `function`, `modifiers`, `numpad`, `navigation` |
| `silentKeys` | string[] | `[]` | Extra `KeyId` names to keep quiet (`Tab`, `Space`, …) |
| `perKeyPlayOn` | map | `{}` | Later; ignore unknown in v1 parser if present |

`playOn` is the profile default. Packs still store up samples so `Both` works without reinstalling packs.

## Output block

| Key | Default |
| --- | --- |
| `deviceId` | `"default"` |
| `masterVolume` | `0.7` (0–1) |

## FX block

See [FX rack](07-fx-rack.md). Stored inline as `fx`.

## Example `user` profile JSON

```json
{
  "schemaVersion": 1,
  "id": "user-piano-hall",
  "name": "My Hall",
  "isFactory": false,
  "basedOn": "factory-reverb",
  "primaryPackId": "factory-click",
  "overlays": [],
  "behavior": {
    "repeat": "Off",
    "repeatRateLimitHz": 8,
    "playOn": "Down",
    "modifierPolicy": "Ignore",
    "ignoreInjected": true,
    "polyphony": 24,
    "velocityRandom": 0.12,
    "variantMode": "Random"
  },
  "output": {
    "deviceId": "default",
    "masterVolume": 0.7
  },
  "fx": {
    "inputGainDb": 0,
    "eq": { "enabled": true, "bassDb": 1, "airDb": 2 },
    "compressor": { "enabled": true, "thresholdDb": -18, "ratio": 2.5, "attackMs": 8, "releaseMs": 80, "makeupDb": 2 },
    "saturation": { "enabled": false, "style": "Tape", "drive": 0.2, "mix": 0.3 },
    "delay": { "enabled": true, "timeMs": 180, "feedback": 0.25, "mix": 0.15 },
    "reverb": { "enabled": true, "decay": 0.55, "damping": 0.4, "mix": 0.35 },
    "limiter": { "ceilingDb": -0.3 }
  }
}
```

Ids: factory `factory-<slug>`, user `user-<guid or slug>`.

## Factory catalog (v1)

| Id | Name | Experience |
| --- | --- | --- |
| `factory-default` | Default / No Effect | No FX (limiter only). Effects page frozen. First-run default |
| `factory-dry` | Dry | Dry click, tiny tape sat, limiter |
| `factory-reverb` | Reverb | Hall virtual room |
| `factory-bass` | Bass | Dry room + dynamic bass |
| `factory-surround` | Surround | Width, crossfeed, short room |

Virtual rooms (FX templates, not profiles): `default`, `dry`, `small`, `hall`, `surround`.

Legacy ids (`factory-tight`, `factory-hall`, `factory-punch`, `factory-immersive`, and the older mechanical/piano/cinema names) migrate on startup.

Until extra packs exist, several factories may share `factory-click` and differ only in FX and behavior. Piano is an Instruments mode, not a profile pack. Custom sample is a virtual pack fed by one armed file.

## App `settings.json`

```json
{
  "schemaVersion": 1,
  "activeProfileId": "factory-default",
  "autostart": false,
  "theme": "Dark",
  "globalMute": false,
  "panicHotkey": "Ctrl+Shift+Alt+M",
  "openSettingsHotkey": "Ctrl+Shift+Alt+K",
  "minimizeToTrayOnClose": true,
  "startMinimized": true,
  "bufferPreset": "Stable",
  "checkForUpdates": true,
  "audioDeviceId": "default",
  "outputBoostDb": 0,
  "disabledPackIds": [],
  "armedSampleFile": null
}
```

Hotkeys are **app hotkeys** (register via Avalonia / Win32), not typed-key sounds. They must still go through the same privacy rules (do not log).

Panic / mute:

- Toggles `globalMute`
- Immediate (settings snapshot swap)
- Tray icon changes state

## Paths

```text
%AppData%\KeyFXBoard\
  settings.json
  profiles\
    factory-dry.json
    ...
    user-....json
  packs\
    factory-click\
    ...
  custom-samples\
  logs\
```

## Switching profiles

- Settings combo box
- Tray menu list (factory + user)
- Optional next/prev hotkey later

Switch = load packs if needed → publish snapshot → hard-stop voices on pack set change.

## First run

1. Create AppData tree
2. Install / copy factory pack
3. Write factory profiles
4. Write default `settings.json` with `factory-dry`
5. Show a short first-run window: “You’ll hear a click as you type. Mute from the tray.” + Open settings

## Validation

- Missing `primaryPackId` → fall back to `factory-click` + banner
- Overlay pack missing → ignore that overlay + banner
- Corrupt JSON → rename to `.bad` and restore factory default for that file only
