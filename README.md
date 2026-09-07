# Key FX Board

Windows tray app for **typing sounds you can design** — sample packs, per-key mapping, profiles, and a built-in FX rack.

**Product page:** [royal-programmer.github.io/KeyFXBoard](https://royal-programmer.github.io/KeyFXBoard/)

## Download

| Installer | Link |
| --- | --- |
| MSI (wizard) | [KeyFXBoard-win.msi](https://github.com/royal-programmer/KeyFXBoard/releases/download/v0.1.1/KeyFXBoard-win.msi) |
| Setup (one-click) | [KeyFXBoard-win-Setup.exe](https://github.com/royal-programmer/KeyFXBoard/releases/download/v0.1.1/KeyFXBoard-win-Setup.exe) |

All assets: [Releases](https://github.com/royal-programmer/KeyFXBoard/releases)

Windows may show SmartScreen on first run until the build is code-signed — More info → Run anyway if you trust this GitHub release.

## Privacy

A global keyboard hook exists only to play a sound. **Nothing you type is stored or sent.**

## Build from source

```powershell
dotnet build KeyFXBoard.sln -c Release
dotnet run --project src/KeyFXBoard.App/KeyFXBoard.App.csproj
```

Pack installers (requires [vpk](https://github.com/velopack/velopack)):

```powershell
.\scripts\pack-installer.ps1
```

## Docs

Planning and design docs live in [`docs/`](docs/README.md). The public site is in [`website/`](website/README.md).

## License

MIT — see [LICENSE](LICENSE).
