# Technology

## Locked stack

| Layer | Choice | Version policy |
| --- | --- | --- |
| Runtime | .NET | **.NET 10 LTS** |
| Language | C# | Latest C# shipped with that SDK |
| UI | Avalonia | Current stable 11.x (or 12.x if stable at implementation time) |
| Look | Fluent theme + Windows 11 Mica / acrylic where available | Dark default |
| Audio | NAudio + WASAPI | Current stable |
| DSP helpers | NAudio providers first; NWaves only if we need it | Optional |
| Keyboard | Win32 `WH_KEYBOARD_LL` via CsWin32 or thin P/Invoke | No extra hook framework required |
| Installer / update | Velopack | Current stable |
| Logging | Microsoft.Extensions.Logging + file sink | No key data |
| Tests | xUnit + FluentAssertions (or built-in assertions) | Core first |
| JSON | `System.Text.Json` | Source-generated context for manifests |

If Avalonia 12 is stable and recommended when we scaffold, use it. Do not sit on an old 0.10 line.

## Why this stack

### .NET 10 LTS + C#

- Long support window, one language for UI, engine, and installer hooks
- Easy for later open-source contributors
- High enough performance when the audio path is allocation-free
- Same Core can run on macOS later

### Avalonia, not WinUI 3, not WPF, not Electron

| Option | Verdict |
| --- | --- |
| **Avalonia** | Modern Fluent/Mica look, MIT, no MSIX requirement, real macOS path, XAML/MVVM |
| WinUI 3 | Native Windows 11, but Windows-only and a weaker long-term bet |
| WPF | Very stable, dated unless heavily restyled, no Mac |
| Electron | Easy UI, heavy RAM, weaker latency story for a tray utility |
| Tauri + Rust | Fast and small, but harder to maintain and a thinner audio FX ecosystem for this team goal |

Avalonia draws with Skia. On Windows 11 we enable Mica on the shell window so it still feels like a current Windows program.

### NAudio + WASAPI

- Battle-tested managed output on Windows
- `WasapiOut` (shared) for v1
- Mixing via `MixingSampleProvider` or a custom `ISampleProvider` we own
- Decode WAV with `WaveFileReader` / `AudioFileReader` on the worker thread, then convert to float PCM once

We do **not** use `SoundPlayer` or `PlaySound` APIs. They cannot do polyphony, FX, or low-latency mixing.

### Velopack

- `Setup.exe` (and optional MSI) with Add/Remove Programs
- GitHub Releases compatible later
- Delta updates
- No store cert required for v1
- Same family of tools can later pack macOS

MSIX / Store is a later channel, not the v1 path. Global hooks and AV are simpler with a classic user-mode exe.

## Libraries we will not take in v1 unless forced

- Full game engines (Unity) — absurd for a tray utility
- WebView UI host — we already have Avalonia
- Python interop
- Heavy plugin hosts (VST) — we are not a DAW; custom `IFxModule` is enough

## Platform APIs (Windows)

| Need | API |
| --- | --- |
| Global keys | `SetWindowsHookExW(WH_KEYBOARD_LL, …)` |
| Message pump | Hidden message window or hook thread `GetMessage` loop |
| Autostart | `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` |
| Default device | WASAPI enumerator via NAudio |
| Tray | Avalonia tray icon (Win32 notify icon under the hood) |

CsWin32 (`Microsoft.Windows.CsWin32`) is preferred over handwritten P/Invoke for hook structs.

## Target OS

- **Minimum:** Windows 10 1809+ x64 (WASAPI + Avalonia desktop)
- **Primary:** Windows 11 x64
- **ARM64:** Nice later; do not block v1. Design AnyCPU + `win-x64` publish first.

Self-contained `win-x64` publish so users do not need a separate .NET install. That matches a productized Setup.exe.

## Tooling

- Visual Studio 2022/2026 or Cursor + `dotnet` CLI
- `dotnet publish -c Release -r win-x64 --self-contained`
- `vpk pack` for Velopack
- Git for source; no remote required to start

## Coding standards (short)

- Nullable reference types enabled
- Async for I/O (pack install, updates), never for the audio callback
- Immutable snapshots on the realtime path
- No `lock` in the WASAPI callback; use atomics / queues
- MVVM for all windows; no business logic in code-behind beyond wiring

## Future Mac mapping (do not build)

| Windows | macOS later |
| --- | --- |
| `WH_KEYBOARD_LL` | CGEvent tap / `IKeyboardSource` |
| WASAPI / NAudio | Core Audio / a Mac audio adapter |
| Velopack Win | Velopack OSX |
| Avalonia Win | Same Avalonia project, Mac desktop target |

NAudio is Windows-centric. The `IAudioOutput` and decoder ports exist so Core does not care.
