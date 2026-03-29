# Tetris Dual-Version Repo

This repository now supports two launcher-visible versions:

- `HTML`
- `+E+RIS`

The PowerShell sprint build still remains in the repo as a development/reference fallback while the new versions continue to grow.

## Implementation Plan

- Full dual-version roadmap and execution guide: [IMPLEMENTATION_PLAN.md](./IMPLEMENTATION_PLAN.md)

## Launchers

- [Launcher.ps1](/C:/Users/grish/CODEX_Gen/Project_1/Launcher.ps1) is the Windows GUI launcher implementation.
- [Start-Tetris.cmd](/C:/Users/grish/CODEX_Gen/Project_1/Start-Tetris.cmd) is the Windows double-click entry point.
- [Start-Tetris.command](/C:/Users/grish/CODEX_Gen/Project_1/Start-Tetris.command) mirrors the same two-choice launcher flow on macOS as closely as practical.
- The target launch flow is: `HTML` opens the backend-free browser edition, while `+E+RIS` launches as a real desktop app window.
- The current PowerShell sprint build remains the gameplay reference/fallback while those two launch targets continue to mature.

Launcher behavior:

- On Windows, double-clicking [Start-Tetris.cmd](/C:/Users/grish/CODEX_Gen/Project_1/Start-Tetris.cmd) opens a small GUI launcher window.
- The launcher shows exactly two visible choices:
  - `HTML`
  - `+E+RIS`
- Clicking `Run` launches the selected version.
- Closing the window cancels cleanly.

## Repository Layout

- [classic-html](/C:/Users/grish/CODEX_Gen/Project_1/classic-html) is the backend-free HTML edition.
- [enhanced](/C:/Users/grish/CODEX_Gen/Project_1/enhanced) contains the `+E+RIS` scaffold and launchable preview build.
- [main.ps1](/C:/Users/grish/CODEX_Gen/Project_1/main.ps1), [src](/C:/Users/grish/CODEX_Gen/Project_1/src), and [web](/C:/Users/grish/CODEX_Gen/Project_1/web) remain the PowerShell reference build.

## HTML Edition

- Launch target: [classic-html/index.html](/C:/Users/grish/CODEX_Gen/Project_1/classic-html/index.html)
- Runs directly from local files under `file://`
- No backend, no Node, no npm, no build step
- Uses `localStorage` for sprint records, nickname persistence, and future-ready leaderboard structure
- Supports sprint gameplay, hold, next queue, ghost piece, retry, and local nickname leaderboard behavior

## +E+RIS Edition

- Target launch mode: packaged desktop app window
- Current repo preview: [enhanced/dist/index.html](/C:/Users/grish/CODEX_Gen/Project_1/enhanced/dist/index.html)
- Repo scaffold includes:
  - [enhanced/package.json](/C:/Users/grish/CODEX_Gen/Project_1/enhanced/package.json)
  - [enhanced/tsconfig.json](/C:/Users/grish/CODEX_Gen/Project_1/enhanced/tsconfig.json)
  - [enhanced/src/engine.ts](/C:/Users/grish/CODEX_Gen/Project_1/enhanced/src/engine.ts)
  - [enhanced/src/main.ts](/C:/Users/grish/CODEX_Gen/Project_1/enhanced/src/main.ts)
- The current dist build is a real launchable sprint preview and the base for the upgraded edition.
- `+E+RIS` now also has a first improved-control pass with client-side DAS, ARR, and lock delay settings stored locally.
- The implementation target is to replace browser-style launching with a packaged app-window launch flow.

## Current State

- `HTML` is now a real backend-free sprint build.
- `+E+RIS` now has a real scaffold and launchable preview build.
- The PowerShell build remains intact for rule-porting, comparison, and fallback testing.
- The PowerShell/browser reference build still includes the deepest handling/timing implementation at this point.

## Sprint Features

- 40 lines remaining counter
- stopwatch / elapsed time
- 7-bag piece randomizer
- visible next queue
- ghost piece
- hold
- pieces placed, total key inputs, keys per piece, and current-piece inputs
- local top-10 sprint times stored separately from the older score-based leaderboard
- customizable handling timings with DAS, ARR, DCD, SDF, gravity, and countdown values

## Controls

- `Left Arrow`: move left
- `Right Arrow`: move right
- `Down Arrow`: soft drop
- `Space`: hard drop
- `Z`: rotate counterclockwise with SRS kicks
- `X`: rotate clockwise with SRS kicks
- `C`: rotate 180 degrees without kicks
- `Shift`: hold
- `Q`: quit the current session

## Timing Settings

- `DAS`: delay before left/right auto-repeat starts
- `ARR`: repeat interval for left/right movement; `0` uses instant wall movement after DAS
- `DCD`: pause before horizontal repeat resumes after a new piece spawns or after rotation
- `SDF`: soft drop speed multiplier used while holding `Down Arrow`
- `Gravity`: automatic fall interval for pieces
- `Countdown`: pre-run countdown length before the sprint starts

These settings are adjusted in the in-game handling panel and saved locally in the browser.

## How To Run

### Standard Launcher Flow

1. Double-click [Start-Tetris.cmd](/C:/Users/grish/CODEX_Gen/Project_1/Start-Tetris.cmd) on Windows.
2. Choose `HTML` or `+E+RIS`.
3. Click `Run`.

### Direct File Launch

- Open [classic-html/index.html](/C:/Users/grish/CODEX_Gen/Project_1/classic-html/index.html) directly for the backend-free `HTML` edition.
- Open [enhanced/dist/index.html](/C:/Users/grish/CODEX_Gen/Project_1/enhanced/dist/index.html) directly only for the current `+E+RIS` preview during development; the target user-facing launch mode is a desktop app window.

### Direct PowerShell Fallback

1. Open Windows PowerShell 5.1.
2. Change into [Project_1](/C:/Users/grish/CODEX_Gen/Project_1).
3. If needed, use a one-time bypass:

```powershell
powershell -ExecutionPolicy Bypass -File .\main.ps1
```

4. Or, after your execution policy is set appropriately, run:

```powershell
.\main.ps1
```

5. The browser should open automatically. If it does not, open the printed `http://localhost:8080/` URL manually.

## Records

- Sprint times are stored in [sprint-times.json](/C:/Users/grish/CODEX_Gen/Project_1/data/sprint-times.json).
- The PowerShell reference build still keeps the older score leaderboard in [highscores.json](/C:/Users/grish/CODEX_Gen/Project_1/data/highscores.json).
- The `HTML` edition stores sprint leaderboard and nickname data in `localStorage`.
- The `+E+RIS` preview currently stores its local records in `localStorage`.

## Known Limitations

- Finesse faults and advanced finesse analysis are not implemented yet.
- The `HTML` edition is intentionally backend-free and simpler than the PowerShell reference build.
- `+E+RIS` has a first local handling pass, but it still needs the later visual, audio, and deeper polish passes from the roadmap.
- The PowerShell build remains available mainly as a development/reference fallback rather than the standard user-facing startup path.
