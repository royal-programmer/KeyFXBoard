# Installer, updates, and lifecycle

There are two products in one app: the **program** and **content packs**. They install and uninstall independently.

## App install (Velopack)

v1 ships a **self-contained** `win-x64` build packed with Velopack (`scripts/pack-installer.ps1`).

Artifacts:

- `KeyFXBoard-win-Setup.exe` — one-click (splash + progress). No path wizard. Per-user.
- `.msi` — Windows wizard: license, readme, Finish. **Per-user only** (`--instLocation PerUser`) — no admin UAC. Needs WiX via `vpk pack --msi`.
- Getting started HTML next to the app: `GettingStarted.html`

Default install location: `%LocalAppData%\KeyFXBoard`. Shortcuts: Desktop + Start menu. Profiles/packs stay in `%AppData%\KeyFXBoard`.

Installer should:

- Place binaries under Velopack’s per-user location (`%LocalAppData%\KeyFXBoard`)
- Create Start menu shortcut
- Register Add/Remove Programs
- Optionally register `.kfxpack` file association → `KeyFXBoard.exe --install-pack "%1"`
- Launch the app after setup

No admin required. That matches the hook (user session) and avoids UAC on every update. Custom install folders are not offered in the MSI wizard (Velopack OTA expects a stable layout); admins can still override MSI with `VELOPACK_INSTALLDIR` if needed.

## App uninstall

Windows Settings → Apps → Key FX Board.

Velopack removes binaries and shortcuts.

On first uninstall prompt (if we can hook it) or on next-start leftover cleaner:

- Ask: **Keep my packs and profiles in AppData?**
- Default: **Keep** (users hate losing custom packs)
- “Remove everything” deletes `%AppData%\KeyFXBoard\`

If Velopack cannot show a custom uninstall question, ship a **Settings → Remove local data** button and mention AppData in the docs / about box.

## Autostart

Settings toggle writes:

`HKCU\Software\Microsoft\Windows\CurrentVersion\Run` → `KeyFXBoard`

Value is the Velopack-updated exe path (use their helper so updates do not leave a stale path).

Default: **off** until the user opts in on first run (“Start with Windows”).

## Updates

- Velopack `UpdateManager`
- Source: GitHub Releases HTTPS (`AppUpdateService.UpdateFeedUrl` → `https://github.com/royal-programmer/KeyFXBoard`)
- Product site (GitHub Pages): https://royal-programmer.github.io/KeyFXBoard/ — download, thank-you, uninstall feedback, getting started
- v1 personal: updates are **manual** (Settings → Check for updates)
- When enabled: user-driven check only (never on the audio thread)
- Apply on restart via `ApplyUpdatesAndRestart`
- First-run **Done** opens the thank-you page in the browser; About can open the product site and uninstall feedback page

**What updates:** the installed app (exe + built-in piano, FX, UI). New Instruments later = bump `Directory.Build.props` Version, pack, publish releases. Friend’s installed copy checks and updates.

**What does not need an app update:** user packs and custom samples in AppData — they stay across Velopack updates.

Do not auto-download on metered networks if we can detect them; otherwise “check only.”

## Pack lifecycle

See [Pack format](08-pack-format.md).

CLI / protocol for file association:

```text
KeyFXBoard.exe --install-pack "C:\Users\...\piano.kfxpack"
```

If the app is already running, a second instance forwards the path via a named mutex + local pipe or `WM_COPYDATA`, then exits. Only one engine/hook host.

Single-instance is **required**. Two hooks + two WASAPI outputs is a bug.

## Portable mode (later)

A folder-next-to-exe `portable.txt` that redirects AppData to `.\data`. Do not build in v1; mention so we do not hard-code paths without an `IAppPaths`.

## Code signing

| Stage | Signing |
| --- | --- |
| Personal / MVP | None. Expect SmartScreen |
| OSS releases | Optional personal cert |
| Product | Authenticode, same publisher every release |

Unsigned + `WH_KEYBOARD_LL` will scare Defender. That is accepted for now.

## Crash and restart

- Unhandled exception → log (no keys) → exit
- Autostart will bring it back on next logon, not immediately (avoid crash loops)
- Optional later: Velopack / watchdog with a crash-count fuse

## Versioning the app

SemVer. Pack `schemaVersion` and profile `schemaVersion` increment independently.

App 1.x reads schema 1 only. When we add schema 2, 1.x must still open and say “this pack needs a newer Key FX Board.”
