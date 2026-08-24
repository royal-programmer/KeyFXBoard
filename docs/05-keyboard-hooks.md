# Keyboard hooks

## Goal

Capture key-down, key-up, and OS auto-repeat from the interactive Windows session, convert them to a platform-neutral `KeyEvent`, and apply profile filters — without ever recording the keys.

## Mechanism (v1)

**Low-level keyboard hook:** `WH_KEYBOARD_LL` via `SetWindowsHookExW`.

Why this, not alternatives:

| Approach | Why not for v1 |
| --- | --- |
| Raw Input (`RIDEV_INPUTSINK`) | Good, more code, foreground-aware extras we do not need yet |
| `GetAsyncKeyState` polling | Misses events, wastes CPU, worse latency jitter |
| Accessibility / UI Automation | Heavy, invasive, not needed to play sounds |
| Kernel filter driver | Product-death for a fun app (signing, Secure Boot, trust) |
| Windows Service hook | Session 0 — will not see the user’s keys cleanly |

`WH_KEYBOARD_LL` is installed **in-process** on a thread that pumps messages. The hook procedure runs in our process (Windows delivers LL hooks this way), which is what we want: we never inject a DLL into other apps.

## Hook thread

1. Start a dedicated STA or standard thread with a Win32 message loop.
2. Register the hook on **that** thread.
3. Keep the thread alive for the app lifetime.
4. On shutdown: `UnhookWindowsHookEx`, then exit the loop.

Avalonia’s UI thread should not own the hook. A busy UI must not delay `CallNextHookEx`.

## Event translation

From `KBDLLHOOKSTRUCT`:

| Field | Use |
| --- | --- |
| `vkCode` | Map to `KeyId` |
| `scanCode` | Disambiguate later if needed (rare) |
| `flags` | `LLKHF_UP`, `LLKHF_EXTENDED`, `LLKHF_INJECTED` |
| `time` | Diagnostics only if needed; do not log |

`KeyEvent`:

```text
KeyId      : KeyA | Enter | Space | LeftCtrl | ...
Kind       : Down | Repeat | Up
Injected   : bool
Timestamp  : long (QPC ticks)
```

**Repeat detection:** LL hook does not always give a clean “repeat bit” like `lParam` bit 30 on `WM_KEYDOWN`. Maintain a `pressed` set:

- Down while not pressed → `Down`, add to set
- Down while pressed → `Repeat`
- Up → `Up`, remove from set

## KeyId catalog

Stable strings, independent of culture and keyboard layout **for v1**.

We map **virtual keys**, not characters. `KeyA` is the A-key position in the current VK table (US-centric names). On AZERTY the physical key still fires `KeyA` if VK is `A` — this is acceptable for v1. Layout-accurate “character packs” are a later enhancement (`ToUnicode` is a privacy and IME minefield; do not call it in v1).

Minimum catalog:

- `KeyA`–`KeyZ`
- `D0`–`D9`
- `F1`–`F12`
- `Space`, `Enter`, `Tab`, `Escape`, `Backspace`
- `LeftShift`, `RightShift`, `LeftCtrl`, `RightCtrl`, `LeftAlt`, `RightAlt`
- `LWin`, `RWin`
- Arrows, `Home`, `End`, `PageUp`, `PageDown`, `Insert`, `Delete`
- `OemMinus`, `OemPlus`, `OemLeftBracket`, `OemRightBracket`, `OemSemicolon`, `OemQuotes`, `OemComma`, `OemPeriod`, `OemQuestion`, `OemBackslash`, `OemTilde`
- Numpad keys

Unknown VKs map to `Vk{n}` and use the pack **fallback** sample.

## Filters (profile + settings)

Applied in this order:

1. **Global mute / panic** — drop everything
2. **Engine not ready** — drop (pack still loading)
3. **Injected** — drop if `ignoreInjected` (default **true**) so we do not play sounds for our own or macro-tool synthetic keys unless the user wants that
4. **Modifier policy**
   - `Ignore` (default): if any Ctrl/Alt/Win is held, drop (Shift is **not** a mute — users type capitals)
   - `Play`: always map
   - `ModifiersOnly`: only play when a modifier is held (niche; optional later)
5. **Repeat policy** — see [System design](03-system-design.md)
6. **Play-on** — `Down` / `Up` / `Both`

Shift is allowed under `Ignore` so normal typing still makes sound. Ctrl+C should be quiet by default.

## Always call the next hook

The procedure **must** call `CallNextHookEx` and return that value (or 0 as documented). We never swallow keys. We are a listener, not a blocker.

If we hang in the hook, Windows will skip us and the user’s keyboard will feel broken. This is why the hook only enqueues.

## Integrity and elevated windows

User Interface Privilege Isolation (UIPI):

- Medium-integrity Key FX Board **will not** see keys typed into an elevated window (Admin CMD, some installers).
- v1: document this. Do not add an “always run as admin” checkbox as the default.
- Optional later: a clearly warned “Run elevated” shortcut.

## Antivirus and reputation

Unsigned binaries that install `WH_KEYBOARD_LL` are a classic AV heuristic.

v1 (personal):

- Expect Defender SmartScreen on first run
- Do not pack a disable-AV guide into the product
- Keep the hook code small and obvious

Product stage:

- Authenticode certificate
- Consistent publisher name
- Velopack updates from HTTPS

## Privacy rules (normative)

The hook path may **not**:

- Write `KeyEvent` to disk
- Send `KeyEvent` over the network
- Keep an unbounded in-memory history (the pressed-set is current state only)
- Include `KeyId` in log statements

A debug build may have a **manual, off-by-default** “event counter” (counts only, no identities) for latency tests.

## Testing

- Unit: filters and repeat state machine with fake events
- Manual: type in Notepad, VS Code, a browser, a game (best effort), an elevated Notepad (expect silence)
- Manual: hold Space in each repeat mode
- Manual: Ctrl+C default quiet, Shift+A still plays

## Failure to attach

Common causes: another hook storm, AV, running in a context without a desktop (we should never).

UX: persistent but calm banner, Retry button, link to the hooks / privacy doc in-app later. App still opens Settings so the user can quit or disable autostart.
