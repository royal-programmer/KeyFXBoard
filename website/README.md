# Product website

Static marketing site for **Key FX Board**, published to GitHub Pages from this folder.

## Local preview

Open `index.html` in a browser, or from the repo root:

```powershell
npx --yes serve website
```

## Screenshots / video

Place captures in `assets/media/` using these names:

| File | Shot |
| --- | --- |
| `S1-home.png` | Home |
| `S3-effects.png` | Effects |
| `S4-packs.png` | Packs |
| `S2-profiles.png` | Profiles (optional) |
| `S5-instruments.png` | Piano (optional) |
| `S6-tray.png` | Tray menu (optional) |
| `R1-typing.mp4` | Typing demo (optional) |

Until a file exists, the home page shows a placeholder for that slot.

## URLs (after Pages is enabled)

- Home: https://royal-programmer.github.io/KeyFXBoard/
- Thank you: https://royal-programmer.github.io/KeyFXBoard/thank-you.html
- Uninstall: https://royal-programmer.github.io/KeyFXBoard/uninstall.html
- Getting started: https://royal-programmer.github.io/KeyFXBoard/getting-started.html

Enable **GitHub Pages** → Source: **GitHub Actions** (workflow `.github/workflows/pages.yml`).
