# Project structure

## Repository (target)

```text
Key FX Board/
  docs/                          this folder
  src/
    KeyFXBoard.Core/             domain + engine
    KeyFXBoard.Windows/          hook, WASAPI, autostart
    KeyFXBoard.App/              Avalonia host
  tests/
    KeyFXBoard.Core.Tests/
  content/
    packs/factory-click/         source for the default pack
    profiles/                    factory JSON source
  packaging/
    velopack/                    pack scripts, icons
  LICENSE                        MIT (when published)
  README.md                      user-facing (later)
  KeyFXBoard.slnx                or .sln
```

Do not put build output or `%AppData%` copies in git.

## Projects

### `KeyFXBoard.Core` (`net10.0`)

No Avalonia, no NAudio, no Win32.

Suggested folders:

```text
Keys/           KeyId, KeyEvent, KeyMap
Filtering/      Repeat, modifiers, mute
Packs/          Manifest models, validation (pure)
Profiles/       Profile models, snapshot
Audio/          SampleBuffer, VoicePool, IFxModule (interfaces + CPU implementations)
Hosting/        Engine facade: Handle(KeyEvent), Swap(ProfileSnapshot)
Abstractions/   IKeyboardSource, IAudioOutput, IPackStore, IAppPaths
Storage/        JSON serialize (System.Text.Json) — OK in Core
```

FX algorithms that are pure float math **can** live in Core so tests do not need NAudio. `IAudioOutput` only writes the finished frames.

### `KeyFXBoard.Windows` (`net10.0-windows`)

```text
Hook/           LowLevelKeyboardSource
Audio/          WasapiOutput, WavDecoder
Autostart/      RunKeyAutostart
SingleInstance/ Mutex + IPC
```

References: Core, NAudio, CsWin32.

### `KeyFXBoard.App` (`net10.0`)

Avalonia desktop application.

```text
Views/
ViewModels/
Services/       Dialogs, pack file picker, Velopack host
Assets/         icon, tray images
App.axaml
Program.cs
```

`Program.cs`:

1. Single-instance gate
2. Velopack first-run / update hook (their documented startup pattern)
3. Build composition root (Core engine + Windows adapters)
4. Start Avalonia

## Composition root

Manual DI or `Microsoft.Extensions.DependencyInjection` in App only.

```text
AppPaths
  → JsonProfileStore, JsonSettingsStore, FilePackStore
  → Engine (Core)
  → LowLevelKeyboardSource.Start(engine.Handle)
  → WasapiOutput.Start(engine.FillBuffer)
```

No service locator inside Core.

## Naming

| User sees | Code |
| --- | --- |
| Key FX Board | `KeyFXBoard` namespaces |
| .kfxpack | `KfxPack` types |

## Tests

Core.Tests cover:

- Repeat state machine
- Modifier policy
- Overlay vs primary resolution
- Manifest validation (zip-slip paths as strings)
- Voice steal at polyphony cap (fake output)
- FX bypass does not NaN

No CI hook tests required for v1.

## Scaffold order

When implementation starts (not this docs task):

1. Solution + Core models + tests
2. Dummy `IAudioOutput` (null) + fake keys
3. Windows WASAPI + one WAV
4. Windows hook
5. Avalonia tray + mute
6. Pack install
7. Profiles + FX UI
8. Velopack

See [Roadmap](14-roadmap.md).
