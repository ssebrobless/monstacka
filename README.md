# Tetris Dual-Version Repo

This repository is being evolved into a dual-version Tetris project with one low-friction browser build and one improved desktop-focused build.

## MonStacka v2 Playtest Download

The current friend-ready build is the Unity version in [MonStacka-v2/](./MonStacka-v2/).

Fastest way to play:

1. Open the latest GitHub Release.
2. Download `MonStackaV2-Windows-x64.zip`.
3. Extract the whole zip folder.
4. Double-click `Launch-MonStackaV2.cmd`.

Do not run `MonStackaV2.exe` from inside the zip preview. Windows needs the extracted folder because the executable depends on the nearby `MonStackaV2_Data/`, `MonoBleedingEdge/`, `D3D12/`, and `UnityPlayer.dll` files.

Current platform status:

- Windows: supported by the current downloadable build.
- macOS: build automation is wired through the `monstacka-v2-unity-macos` GitHub Actions workflow, but it needs Unity license secrets before it can produce a downloadable `.app` zip.

### Build MonStacka v2 for macOS

The Mac build is produced by `.github/workflows/monstacka-v2-unity-macos.yml`.

Required GitHub Actions secrets:

- `UNITY_LICENSE`
- `UNITY_EMAIL`
- `UNITY_PASSWORD`

After those secrets are configured:

1. Open the Actions tab.
2. Run `monstacka-v2-unity-macos`.
3. Download the `MonStackaV2-macOS` artifact.
4. Extract `MonStackaV2-macOS.zip`.
5. Open `MonStackaV2.app`.

This playtest app is not Apple-notarized yet. If macOS blocks it, Control-click the app, choose `Open`, then confirm.

```text
╔══════════════ Launch Flow ══════════════╗
║ Start-Tetris.cmd / Launcher.ps1         ║
╠════════════════╦════════════════════════╣
║ HTML           ║ classic-html/index.html║
║                ║ browser, file:// only  ║
╠════════════════╬════════════════════════╣
║ MonStacka!     ║ enhanced/              ║
║                ║ standalone Tauri app   ║
╚════════════════╩════════════════════════╝
```

The original PowerShell build is still kept in the repo as a reference and fallback while the new versions continue to grow.

## Implementation Plan

- Source of truth for the roadmap: [IMPLEMENTATION_PLAN.md](./IMPLEMENTATION_PLAN.md)

## Repo Layout

- [classic-html/](./classic-html/) is the backend-free browser edition.
- [enhanced/](./enhanced/) is the `MonStacka!` standalone desktop app source tree.
- [src/](./src/), [web/](./web/), and [main.ps1](./main.ps1) are the original PowerShell reference build.
- [Launcher.ps1](./Launcher.ps1), [Start-Tetris.cmd](./Start-Tetris.cmd), and [Start-Tetris.command](./Start-Tetris.command) provide the version chooser.
- [Launch-MonStacka.ps1](./Launch-MonStacka.ps1), [Start-MonStacka.cmd](./Start-MonStacka.cmd), and [Start-MonStacka.command](./Start-MonStacka.command) launch only the native `MonStacka!` desktop app.

## Versions

### `HTML`

- Launch target: [classic-html/index.html](./classic-html/index.html)
- Runs directly from local files under `file://`
- No backend, no Node, no npm, no build step
- Uses `localStorage` for local records and settings
- Intended for restricted/work computers

### `MonStacka!`

- Source root: [enhanced/](./enhanced/)
- Uses `TypeScript`, `Vite`, `Web Audio`, and a `Tauri` desktop shell
- Current playable modes:
  - `Arcade`
  - `40 Lines`
  - `Training`
- Local features already present:
  - top-10 Arcade leaderboard
  - top-10 40 Lines leaderboard
  - 5-character nickname entry
  - configurable DAS / ARR / lock delay
  - Training feedback modes: `Show`, `Redo`, `Off`
  - finesse fault counter and perfect-streak tracking
  - custom monster sprite-sheet rendering for `MonStacka!` only
  - reactive pupils, selective blinking, tongue motion, and soft squish-only visual effects
  - title screen, mini piece previews, lock flash, line-clear flash
  - audio controls for mute, SFX volume, and music volume
- Startup behavior:
  - the packaged desktop app can be launched directly on its own
  - the optional launcher offers `HTML` and `MonStacka!`
  - `MonStacka!` now launches only the native desktop executable
  - if the native executable is missing, the launcher shows build/download instructions instead of opening the browser preview

## Controls

- `Left Arrow`: move left
- `Right Arrow`: move right
- `Down Arrow`: soft drop
- `Space`: hard drop
- `Z`: rotate counterclockwise
- `X`: rotate clockwise
- `C`: rotate 180 degrees
- `Shift`: hold
- `R`: retry

## How To Run

### Standard launcher flow

1. On Windows, double-click [Start-Tetris.cmd](./Start-Tetris.cmd).
2. Choose `HTML` or `MonStacka!`.
3. Click `Run`.

### Launch `MonStacka!` directly

- On Windows, double-click [Start-MonStacka.cmd](./Start-MonStacka.cmd).
- On macOS, run [Start-MonStacka.command](./Start-MonStacka.command).
- If the native desktop build is missing, these scripts will show instructions instead of opening the browser preview.

### Run the `HTML` edition directly

- Open [classic-html/index.html](./classic-html/index.html) directly in a browser.

### Run the `MonStacka!` browser preview directly

- Build once from [enhanced/](./enhanced/):

```powershell
cd .\enhanced
npm install
npm run build
```

- Then open [enhanced/dist/index.html](./enhanced/dist/index.html) directly.
- For layout and animation verification, append `?debug=1` to the preview URL.
- This preview is for development only and is not the supported way to launch the desktop game.

### Run `MonStacka!` as a native desktop app window

From [enhanced/](./enhanced/):

```powershell
npm install
npm run tauri:dev
```

To build a Windows executable:

```powershell
npm run tauri:build
```

After a successful build, the launcher will prefer the native executable in `enhanced/src-tauri/target/release/` when `MonStacka!` is selected.

### Download `MonStacka!` from GitHub

- GitHub Actions now builds desktop artifacts for:
  - macOS Apple Silicon
  - macOS Intel
  - Windows
- On branch pushes, download the build from the workflow artifacts on the Actions tab.
- On `monstacka-v*` tags, the workflow publishes release assets so a MacBook can download just the standalone `MonStacka!` build without pulling the whole repo.

### Run the PowerShell fallback

```powershell
powershell -ExecutionPolicy Bypass -File .\main.ps1
```

The browser should open automatically. If it does not, open the printed `http://localhost:8080/` URL manually.

## Records

- PowerShell reference build:
  - sprint records: [data/sprint-times.json](./data/sprint-times.json)
  - score leaderboard: [data/highscores.json](./data/highscores.json)
- `HTML` edition:
  - sprint/local leaderboard data in `localStorage`
- `MonStacka!` edition:
  - Arcade and 40 Lines local records in `localStorage`

## Current Gaps

- A tagged GitHub release still needs to be cut for the first public Mac download.
- The PowerShell build remains the deepest reference implementation for comparison while the port continues.
