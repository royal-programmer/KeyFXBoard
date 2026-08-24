# Pack and install Key FX Board (Velopack)
#
# Prerequisites:
#   dotnet tool install -g vpk
#
# Output:
#   artifacts\publish\     self-contained win-x64 app
#   artifacts\releases\    Setup.exe, optional .msi, update packages

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
if (-not (Test-Path (Join-Path $Root "KeyFXBoard.sln"))) {
  $Root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
}

$Version = "0.1.1"
$props = Join-Path $Root "Directory.Build.props"
if (Test-Path $props) {
  $match = Select-String -Path $props -Pattern "<Version>([^<]+)</Version>" | Select-Object -First 1
  if ($match) { $Version = $match.Matches[0].Groups[1].Value }
}

$Publish = Join-Path $Root "artifacts\publish"
$Releases = Join-Path $Root "artifacts\releases"
$Installer = Join-Path $Root "installer"
$Project = Join-Path $Root "src\KeyFXBoard.App\KeyFXBoard.App.csproj"
$Icon = Join-Path $Root "src\KeyFXBoard.App\Assets\app.ico"
$Splash = Join-Path $Root "src\KeyFXBoard.App\Assets\app-icon.png"

Write-Host "Publishing KeyFXBoard $Version (win-x64 self-contained)..."
if (Test-Path $Publish) { Remove-Item $Publish -Recurse -Force }
dotnet publish $Project -c Release -r win-x64 --self-contained true -o $Publish
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Copy-Item (Join-Path $Root "LICENSE.txt") (Join-Path $Publish "LICENSE.txt") -Force
Copy-Item (Join-Path $Root "THIRD_PARTY_NOTICES.txt") (Join-Path $Publish "THIRD_PARTY_NOTICES.txt") -Force
Copy-Item (Join-Path $Installer "GettingStarted.html") (Join-Path $Publish "GettingStarted.html") -Force
New-Item -ItemType Directory -Force -Path (Join-Path $Publish "Assets") | Out-Null
Copy-Item $Splash (Join-Path $Publish "Assets\app-icon.png") -Force

Write-Host "Packing with Velopack (vpk)..."
New-Item -ItemType Directory -Force -Path $Releases | Out-Null

$packArgs = @(
  "pack",
  "--packId", "KeyFXBoard",
  "--packVersion", $Version,
  "--packDir", $Publish,
  "--mainExe", "KeyFXBoard.exe",
  "--packTitle", "Key FX Board",
  "--packAuthors", "Key FX Board",
  "--icon", $Icon,
  "--splashImage", $Splash,
  "--shortcuts", "Desktop,StartMenuRoot",
  "--outputDir", $Releases,
  "--instWelcome", (Join-Path $Installer "welcome.md"),
  "--instLicense", (Join-Path $Root "LICENSE.txt"),
  "--instReadme", (Join-Path $Installer "readme.md"),
  "--instConclusion", (Join-Path $Installer "conclusion.md"),
  "--msi",
  "--instLocation", "Either"
)

& vpk @packArgs
if ($LASTEXITCODE -ne 0) {
  Write-Host "MSI pack failed (WiX may be missing). Packing Setup.exe only..."
  $packArgs = $packArgs | Where-Object { $_ -notin @("--msi", "--instLocation", "Either") }
  & vpk @packArgs
  if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

Write-Host ""
Write-Host "Done. Prefer the .msi if present (path picker, license, readme, finish)."
Write-Host "  Folder: $Releases"
Write-Host "  Default per-user location is %LocalAppData%\KeyFXBoard (not Program Files)."
