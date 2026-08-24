# Pack format

## Role

A **pack** is a portable sound library: samples, a key map, metadata, and a license. It does not contain FX. FX live on profiles.

Users **install** and **uninstall** packs in the app, the same way a product installs content add-ons.

## File

| Item | Value |
| --- | --- |
| Extension | `.kfxpack` |
| Container | ZIP (stored or deflated) |
| Root | Files at archive root or a single top-level folder (both allowed; we strip one root folder if present) |

Suggested filename: `<id>-<version>.kfxpack` e.g. `piano-classic-1.0.0.kfxpack`.

## Layout

```text
manifest.json          required
icon.png               optional, PNG, ≤ 256×256 recommended
preview.wav            optional, short WAV
samples/               required if any samples exist
  a_down.wav
  a_up.wav
  enter_down.wav
  default_down.wav
  ...
LICENSE.txt            optional, human-readable
```

All sample paths in the manifest are **relative**, no `..`, no absolute paths, no drive letters.

## `manifest.json` (schema version 1)

```json
{
  "schemaVersion": 1,
  "id": "piano-classic",
  "name": "Piano Classic",
  "version": "1.0.0",
  "author": "Key FX Board",
  "license": "CC0-1.0",
  "homepage": "",
  "description": "Short one-shot piano hits mapped to the letter row.",
  "icon": "icon.png",
  "preview": "preview.wav",
  "defaults": {
    "playOn": "Down",
    "variantMode": "Random"
  },
  "keys": {
    "KeyA": {
      "down": ["samples/a_down.wav", "samples/a_down_b.wav"],
      "up": ["samples/a_up.wav"]
    },
    "Enter": {
      "down": ["samples/enter_down.wav"],
      "up": []
    }
  },
  "fallback": {
    "down": ["samples/default_down.wav"],
    "up": ["samples/default_up.wav"]
  },
  "notes": {
    "C4": "samples/C4.wav",
    "C#4": "samples/Cs4.wav"
  },
  "keyNotes": {
    "KeyQ": "C4",
    "D2": "C#4"
  },
  "octaveDown": "PageDown",
  "octaveUp": "PageUp",
  "octaveReset": ["Home", "End"]
}
```

Chromatic packs (`notes` + `keyNotes`) use the **piano-v2** map. Do **not** set `fallback` on those packs: unmapped keys stay silent. Page Down / Page Up shift ±12 semitones (sticky). Home / End reset to 0. Clamp MIDI C2–C6.

### Field rules

| Field | Rules |
| --- | --- |
| `schemaVersion` | Integer. v1 understands `1` only. Higher → reject with “update the app” |
| `id` | `[a-z0-9-]{2,64}`, unique in the local store |
| `name` | 1–80 chars, display |
| `version` | SemVer string |
| `license` | SPDX id or `"Proprietary"` or `"SEE-LICENSE-TXT"` |
| `keys` | Map of `KeyId` → sample lists |
| `fallback` | Used when a `KeyId` is missing or its list is empty. Omit on chromatic packs. |
| `notes` | Map of note name (`C4`, `C#4`) → WAV. Required for piano-v2 packs. |
| `keyNotes` | Map of `KeyId` → note name. Piano-v2 lives here. |
| `octaveDown` / `octaveUp` | `KeyId`s that transpose −12 / +12 (Page Down / Page Up on piano-v2) |
| `octaveReset` | `KeyId`s that restore shift 0 (Home / End) |
| empty `up` | Legal. Profile `Both` then only plays down for that key. On key-up, empty `up` falls back to that key’s down sample. |

Unknown JSON fields are ignored (forward compatible).

## Sample rules (v1)

- Container: **WAV** (PCM 16/24 or IEEE float)
- Sample rate: any common rate; we resample to 48 kHz on load
- Channels: 1 or 2
- Max file size per sample: **8 MB** (reject)
- Max decoded RAM per pack: **64 MB** (reject or warn + require confirm — **reject in v1** for safety)
- No nested `.kfxpack` inside a pack

## Install location

```text
%AppData%\KeyFXBoard\packs\<id>\
  manifest.json
  ...
```

The incoming zip is **not** kept after a successful install (unless we later add “keep installer file”). Reinstall = overwrite same `id` after confirmation, comparing `version`.

### Zip-slip

Every extracted path must resolve under `packs\<id>\`. Reject `..`, absolute paths, and symlink tricks.

## Install / uninstall UX

**Install**

- Settings → Packs → Install pack… (`.kfxpack`)
- Drag-and-drop onto the Packs page
- Double-click `.kfxpack` if we register a file association (v1 yes if Velopack / installer can; otherwise in-app only)

Validation failures are full-stop: nothing committed to `packs\<id>\`.

**Uninstall**

- Confirm dialog lists profiles that reference the pack
- If active: switch to a factory profile first, then delete
- Remove directory
- Offer to open profile editor for broken overlays

Uninstall never deletes the running exe. That is app uninstall.

## Default pack

The app seeds **factory-click** into AppData on first run. **piano-classic** is also generated on disk for the Instruments piano, but it is hidden from the Packs library. **custom-sample** is a virtual pack (armed file in `%AppData%\KeyFXBoard\custom-samples`). Users cannot uninstall factory or reserved ids; they can hide library packs with Enable / Disable.

Reserved ids: `factory-*`, `piano-classic`, `custom-sample`. Retired arcade ids (`guns-arcade`, `bombs-arcade`) are deleted on launch and rewritten to Factory Click.

## Mapping resolution

For a `KeyEvent` and active profile:

1. If an overlay lists this `KeyId`, use that overlay pack’s samples for the key (octave shift applies only if that overlay pack is chromatic)
2. Else if the primary pack has `keyNotes` for this key, play `notes[transposed]`
3. Else use primary pack `keys[KeyId]`
4. Else use that same pack’s `fallback`
5. Else silence (do not crash)

`playOn` from the **profile** decides whether down/up lists are consulted. Pack `defaults.playOn` is only a hint for profile editors (“this pack is release-sample rich”).

## piano-v2

Home → **Instruments** starts piano. **Open piano map** draws C2–C6 with the computer key that plays each note **at the current octave**. Closing the map does not stop piano. Unreachable notes at this shift are unlabeled. Page Down / Page Up move the whole span; Home / End reset to A-row = C4.

| Region | Keys | Notes (shift 0) |
| --- | --- | --- |
| Lower whites | Z X C V B N M | C3–B3 |
| Home whites | A S D F G H J | C4–B4 |
| Home blacks | 2 3 5 6 7 | C#4 D#4 F#4 G#4 A#4 |
| Upper whites | Q W E R T Y U | C5–B5 |
| Upper blacks | 9 0 - = [ | C#5 D#5 F#5 G#5 A#5 |
| Octave | Page Down / Page Up | −12 / +12 sticky, clamp MIDI C2–C6 |
| Reset | Home / End | Shift 0 |

Shipped on disk: `factory-click` (library) and `piano-classic` (instrument, hidden from Packs). Virtual picker pack: `custom-sample`. Packs page Enable / Disable hides a pack from profile pickers without deleting it. Custom files are WAV plus whatever Windows `AudioFileReader` can decode; pack `.kfxpack` samples stay WAV-only.

## Versioning and updates

- Same `id`, higher `version`: “Update pack?”
- Same `id`, same or lower: “Already installed” / “Replace anyway?”
- Different `id`: new library slot

No automatic network pack updates in v1.

## Authoring

v1: authors build a folder and zip it. A later `KeyFXBoard.PackTool` can validate and pack. Document the spec so OSS contributors can ship packs without our UI.

A `docs/pack-authoring.md` can be split out later; this file is the spec.

## Open source + product

- Spec is public
- Official packs can be CC0
- Third-party packs carry their own `license`
- Paid packs later are still `.kfxpack` files; the engine does not care about money
- Do not encrypt packs in v1 (it is theater and hostile to OSS)
