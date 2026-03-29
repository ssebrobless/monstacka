# Dual-Version Tetris Implementation Plan

## Purpose

This document is the source of truth for the full implementation of this repository as a dual-version Tetris project.

The project must support:

1. `HTML`
   A backend-free edition that can be launched directly in a browser from local files on restricted/work computers.

2. `+E+RIS`
   A newer improved edition with smoother controls, upgraded visuals, audio, and a more polished game flow.

The existing PowerShell build must remain in the repository as a development/reference fallback while the new versions are being completed.

## Final User Experience

```text
+--------------------------------------------------+
| Launch Tetris                                    |
|                                                  |
| Version                                           |
| [ HTML        v ]                                 |
|                                                  |
| [ Run ]                                           |
+--------------------------------------------------+
```

Required launcher behavior:

- The normal Windows entry point must be a double-clickable launcher.
- Double-clicking the launcher opens a small GUI window, not a terminal prompt.
- The selection control must offer exactly two visible choices:
  - `HTML`
  - `+E+RIS`
- A `Run` button must appear directly below the selector.
- When `HTML` is selected and `Run` is clicked, the backend-free Classic HTML edition opens.
- When `+E+RIS` is selected and `Run` is clicked, the improved edition opens.
- Closing the window cancels without launching anything.

Non-user-facing fallback behavior:

- The old PowerShell sprint build stays in the repo for testing, comparison, and rule-porting.
- It does not need to remain a primary visible launcher option once the final two-choice launcher exists.

## Architecture Shape

```text
Current reference build
PowerShell engine --> local HTTP server --> browser

Target product
Launcher GUI --> HTML edition   --> browser (file-based, no backend)
Launcher GUI --> +E+RIS edition --> browser or packaged app

Reference fallback
PowerShell build remains available in repo for development use
```

## Continuous Execution Directive For Codex

Any Codex agent working from this file must follow these rules:

1. Read this file first and treat it as the source of truth.
2. Determine the highest completed step by inspecting the actual repository state.
3. Continue with the next unfinished step automatically.
4. Do not stop after a single step just because a milestone is reached.
5. Do not ask for permission between steps unless there is a real blocker or a risky architectural fork.
6. Keep the current PowerShell version intact while porting behavior.
7. Prefer small, real, testable implementations over placeholders.
8. Update `README.md` whenever launch behavior or project layout changes.
9. Commit progress in focused commits on a `codex/` branch.
10. After each major milestone, summarize:
   - what is complete
   - what remains
   - what the next unfinished step is

## Non-Negotiable Requirements

- Keep both new versions in the same repository.
- Keep the PowerShell gameplay reference build available during migration.
- Make the `HTML` edition playable directly from local files.
- Make the launcher the standard startup path on supported systems.
- Add local leaderboard support with nickname entry.
- Improve launch friction, visuals, and gameplay feel.

## Tech Direction

### `HTML` edition

- Plain `HTML + CSS + JavaScript`
- No backend
- No build step
- No package manager required to run
- Persistence via `localStorage`

Why:
- Best fit for work/restricted computers
- Can open directly from `file:///.../classic-html/index.html`
- Lowest friction

### `+E+RIS` edition

- `TypeScript`
- `HTML5 Canvas`
- `Web Audio API`
- `Vite` for development/build
- Optional later: `Tauri` packaging for Windows/macOS desktop builds

Why:
- Best path for smoother controls, rendering, and audio
- Cross-platform friendly
- Natural evolution from the existing browser game

## Target Repository Shape

```text
repo root
|- IMPLEMENTATION_PLAN.md
|- README.md
|- Launcher.ps1
|- Start-Tetris.cmd
|- Start-Tetris.command
|- main.ps1
|- src/                 current PowerShell engine + server
|- web/                 current PowerShell browser UI
|- data/                existing JSON persistence
|- classic-html/        backend-free HTML edition
|  |- index.html
|  |- styles.css
|  |- app.js
|  |- assets/
|  `- audio/
|- enhanced/            +E+RIS source
|  |- package.json
|  |- src/
|  |- public/
|  `- dist/
`- tests/
```

## Step 1: Complete Launcher Foundation

Status intent:
- This step is only complete when the launcher matches the final two-choice GUI UX.

Required work:
- Replace the current console-style launcher with a small Windows GUI launcher.
- Keep `Start-Tetris.cmd` as the Windows double-click entry point.
- Make the launcher show exactly:
  - `HTML`
  - `+E+RIS`
- Put a `Run` button below the selector.
- Make close/cancel exit cleanly.
- Update the macOS launcher script to mirror the same two choices as closely as practical.

Acceptance criteria:
- Double-click launcher opens a small GUI window on Windows.
- The user can select `HTML` or `+E+RIS`.
- Clicking `Run` launches the selected version.

## Step 2: Build the `HTML` Edition

Goal:
- Create the first real dual-version split by implementing the `HTML` version as a fully backend-free local-file build.

Product definition:
- `HTML` is the work-safe edition.
- It must run by opening `classic-html/index.html` directly in a browser.
- It must not require PowerShell, a local server, Node, npm, or any build step.
- It should preserve the current sprint-focused gameplay as closely as practical.
- It should support local persistence for records and settings.
- It should support nickname entry for qualifying leaderboard results.

Required files:
- `classic-html/index.html`
- `classic-html/styles.css`
- `classic-html/app.js`

Required gameplay scope:
- 10x20 visible board
- sprint mode first
- falling tetrominoes
- left/right movement
- soft drop
- hard drop
- rotate CCW
- rotate CW
- rotate 180
- hold
- next queue
- ghost piece
- line clears
- top-out detection
- restart/retry
- sprint timer

Persistence requirements:
- Use `localStorage`
- Save:
  - sprint leaderboard
  - future-ready score leaderboard structure
  - nickname if useful
  - local settings if added
- Do not use backend files or HTTP APIs

Leaderboard requirements:
- Add local nickname entry when the player achieves a qualifying result
- Initial qualification can be top-10 sprint result
- The leaderboard must update immediately after submission

UI requirements:
- It does not need to be heavily polished yet
- It must be clearly playable
- It must work under `file://`
- Avoid fetch calls, module loaders, or server-only assumptions

Launcher integration requirements:
- The launcher `HTML` option must open `classic-html/index.html`
- If `+E+RIS` is not ready yet, `HTML` must still be fully real and launchable

Acceptance criteria:
- `classic-html/index.html` is playable directly from disk
- No backend process is required
- Sprint mode works
- Local leaderboard works
- Nickname entry works
- Existing PowerShell version remains intact

## Step 3: Create the `+E+RIS` Foundation

Goal:
- Replace placeholder behavior with a real improved-edition scaffold.

Required work:
- Create `enhanced/`
- Initialize the improved edition project structure
- Set up the entry point and basic app shell
- Add the first real launch path for `+E+RIS`
- Wire the launcher `+E+RIS` option to this edition

Acceptance criteria:
- Selecting `+E+RIS` from the launcher opens a real scaffold, not a placeholder message
- The repo contains a clear foundation for ongoing improved-edition work

## Step 4: Port Core Gameplay Into `+E+RIS`

Required work:
- Port sprint gameplay rules into TypeScript modules
- Match the current reference behavior for:
  - spawn
  - queue
  - hold
  - ghost
  - movement
  - rotation
  - line clear
  - top-out
  - sprint completion

Acceptance criteria:
- `+E+RIS` becomes a real playable sprint build
- The PowerShell version is still available as the reference comparison

## Step 5: Improve Fluidity And Control Feel

Required work:
- Add configurable DAS
- Add configurable ARR
- Add lock delay
- Add improved input buffering
- Improve restart speed and menu flow
- Reduce dependence on slow request/response loops by keeping gameplay local to the client

Acceptance criteria:
- `+E+RIS` clearly feels more responsive than the current PowerShell build

## Step 6: Visual Upgrade Pass

Required work:
- Canvas-based board rendering
- Better piece styling
- Better ghost styling
- line-clear and lock feedback
- stronger hold/next presentation
- title/menu screen
- more cohesive HUD

Acceptance criteria:
- `+E+RIS` is visually distinct and clearly upgraded

## Step 7: Audio Pass

Required work:
- Sound effects for move, rotate, hold, hard drop, lock, line clear, top-out, countdown
- Mute toggle
- SFX/music volume controls

Acceptance criteria:
- Audio is real, optional, and cleanly integrated

## Step 8: Leaderboards And Nicknames Across Versions

Required work:
- Keep the `HTML` edition leaderboard local and simple
- Add the improved-edition leaderboard flow
- Support nickname entry after qualifying results
- Make qualification logic explicit and consistent

Acceptance criteria:
- Both visible versions support leaderboard entry in a clean way

## Step 9: Score-Focused Mode

Required work:
- Add a second major mode such as `Marathon` or `Arcade`
- Add scoring progression and speed curve
- Make score-based leaderboards meaningful

Acceptance criteria:
- The project supports both sprint and a score-focused mode

## First Real Delivery Target

The first meaningful end-to-end delivery from this plan should produce:

- a working small GUI launcher on Windows
- the final visible launcher choices `HTML` and `+E+RIS`
- a real backend-free `HTML` edition that launches from the GUI
- a real `+E+RIS` scaffold that launches from the GUI

## Definition Of Done

This plan is complete when:

- double-clicking the launcher opens the small selection window
- the window presents `HTML` and `+E+RIS`
- clicking `Run` launches the selected version
- `HTML` runs directly from local files with no backend
- `+E+RIS` becomes the improved playable version
- the PowerShell build remains available in the repo as a reference fallback
- nickname leaderboard flow exists where appropriate
- the README clearly explains how to run everything
