# Dual-Version Tetris Implementation Plan

## Purpose

This document is the source of truth for the full implementation of this repository as a dual-version Tetris project.

The project must support:

1. `HTML`
   A backend-free edition that can be launched directly in a browser from local files on restricted/work computers.

2. `+E+RIS`
   A newer improved edition with smoother controls, upgraded visuals, audio, and a more polished game flow that launches as a real desktop app window. Its target mode lineup is `Arcade`, `40 Lines`, and `Training`.

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
- When `+E+RIS` is selected and `Run` is clicked, the improved edition launches as a desktop app window, not in a browser tab.
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
Launcher GUI --> +E+RIS edition --> Tauri desktop app window

Reference fallback
PowerShell build remains available in repo for development use
```

## Continuous Execution Directive For Codex

Any Codex agent working from this file must follow these rules:

1. Read this file first and treat it as the source of truth.
2. Read the Status marker on each step and the Remaining Work Priority section to determine what to work on next.
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
11. Update the Status marker on each step as work is completed.

## Remaining Work Priority

The following items are incomplete and should be executed in this order:

1. Monster art and animation pass for `+E+RIS` only (see Outstanding User Requests section)
2. Final audit and rename (Step 12)

Codex agents: start from item 1 in this list and work downward.
Superseding note: Steps 6, 7, 9, 10, and 11 are also complete, so the remaining work list above takes precedence over any stale step references below.
Do not re-examine Steps 1, 2, 4, 5, or 8 — they are complete.

## Outstanding User Requests

The following user-requested work was agreed after the numbered steps were drafted and must be completed before the final audit/rename step:

- Integrate the user-supplied tetromino sprite PNG into `+E+RIS` only.
- Leave the `HTML` edition visually unchanged.
- Treat the art as a tetromino sprite sheet / themed render pass rather than changing gameplay logic.
- Add reactive eye/pupil motion where practical.
- Add occasional blinking for the red, pink, and orange pieces.
- Add subtle tongue motion for the purple `T` piece without breaking the block silhouette.
- Add soft "squish together" rendering so stacked monster pieces feel organic without changing collision or rules.
- Keep all of the above visual-only; no gameplay hitboxes or logic should change.

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
- `Tauri` desktop shell for Windows/macOS app packaging

Why:
- Best path for smoother controls, rendering, and audio
- Cross-platform friendly
- Natural evolution from the existing browser game
- Launches as an actual app window instead of a browser page

## Testing Strategy

### Unit tests for game engine (enhanced/src/engine/)

- Framework: Vitest (compatible with the Vite build)
- Test files: `enhanced/src/engine/__tests__/*.test.ts`
- Coverage targets:
  - `board.ts`: createBoard, clearLines (empty board, single line, multiple lines, Tetris)
  - `bag.ts`: ensureQueue (7-bag completeness, no repeats within bag)
  - `pieces.ts`: getCells, isValid, isGrounded, rotate with kicks
  - `state.ts`: spawn, move, rotate, lockPiece, hardDrop, hold, reset
  - `storage.ts`: normalizeNickname, qualifiesSprintRecord, qualifiesScoreRecord

### Integration tests

- Verify a full 40-line sprint completion flow (programmatic)
- Verify arcade mode game-over triggers leaderboard qualification check

### How to run

- Add `"test": "vitest run"` to `enhanced/package.json`
- Add `"test:watch": "vitest"` to `enhanced/package.json`

### When to add tests

- Add retroactively for existing engine logic before starting new feature work
- All new engine logic (gravity curves, finesse detection) must include tests
- UI/rendering code does not require automated tests

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
|  |- dist/
|  `- src-tauri/
`- tests/
```

## Step 1: Complete Launcher Foundation

Status: COMPLETE

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
- Make `HTML` launch in the default browser.
- Make `+E+RIS` launch the packaged desktop app window when available.
- Update the macOS launcher script to mirror the same two choices as closely as practical.

Acceptance criteria:
- Double-click launcher opens a small GUI window on Windows.
- The user can select `HTML` or `+E+RIS`.
- Clicking `Run` launches the selected version.
- `HTML` opens in the browser.
- `+E+RIS` opens as an app window instead of a browser tab.

## Step 2: Build the `HTML` Edition

Status: COMPLETE

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

Status: PARTIAL -- Vite/TypeScript scaffold exists and launches, but no Tauri packaging; currently opens in browser, not a desktop app window. Tauri packaging is split out to Step 10.

Goal:
- Replace placeholder behavior with a real improved-edition scaffold.

Required work:
- Create `enhanced/`
- Initialize the improved edition project structure
- Set up the entry point and basic app shell
- Wire the launcher `+E+RIS` option to open the built edition (browser fallback until Tauri is ready in Step 10)

Acceptance criteria:
- Selecting `+E+RIS` from the launcher opens the real improved edition
- The repo contains a clear foundation for ongoing improved-edition work
- Vite build produces a self-contained dist/ that works over `file://`

## Step 4: Port Core Gameplay Into `+E+RIS`

Status: COMPLETE

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

Status: COMPLETE

Required work:
- Add configurable DAS
- Add configurable ARR
- Add lock delay
- Add improved input buffering
- Improve restart speed and menu flow
- Reduce dependence on slow request/response loops by keeping gameplay local to the client

Acceptance criteria:
- `+E+RIS` clearly feels more responsive than the current PowerShell build

## Step 6: Visual Polish Pass

Status: COMPLETE

Goal:
- Make `+E+RIS` visually distinct and clearly upgraded from the `HTML` edition.

Required work:
- Add line-clear animation (brief flash or row-collapse effect via CSS transitions)
- Add lock feedback (brief highlight on piece lock)
- Improve hold and next-queue presentation (render mini piece grids instead of letter labels)
- Add a title/menu screen shown before first game start
- Polish the HUD layout for clearer visual hierarchy
- Ensure responsive layout works at common window sizes

Optional stretch (not required for acceptance):
- Migrate board rendering to Canvas for smoother animation
- If Canvas is adopted, keep all non-board UI (sidebar, modals, settings) as DOM

Acceptance criteria:
- Line clears have visible feedback animation
- Hold and next queue show piece shapes, not just letters
- A title screen exists
- The edition looks visually distinct from the HTML edition

## Step 7: Audio Pass

Status: COMPLETE

Required work:
- Sound effects for move, rotate, hold, hard drop, lock, line clear, top-out, countdown
- Mute toggle
- SFX/music volume controls

Acceptance criteria:
- Audio is real, optional, and cleanly integrated

## Step 8: Leaderboards And Nicknames Across Versions

Status: COMPLETE

Required work:
- Keep the `HTML` edition leaderboard local and simple
- Add the improved-edition leaderboard flow
- Support nickname entry after qualifying results
- Make qualification logic explicit and consistent

Acceptance criteria:
- Both visible versions support leaderboard entry in a clean way

## Step 9: Score-Focused Mode With Gravity Curve

Status: COMPLETE

Goal:
- Add a second major mode (`Arcade`) with scoring progression and increasing speed.

Required work:
- Add a `getGravityMs(linesCleared: number): number` function to the engine
- Replace constant `GRAVITY_MS` usage in the game loop with a call to this function using `state.lines`
- Sprint mode (`40 Lines`) continues to use flat 650ms gravity
- Implement the following gravity progression for Arcade mode:

  | Lines cleared | Gravity (ms) |
  |---------------|-------------|
  | 0-9           | 650         |
  | 10-19         | 500         |
  | 20-29         | 400         |
  | 30-39         | 300         |
  | 40-59         | 220         |
  | 60-79         | 150         |
  | 80-99         | 100         |
  | 100-119       | 70          |
  | 120-139       | 50          |
  | 140+          | 33          |

- Make score-based leaderboards meaningful

Acceptance criteria:
- The project supports both sprint and a score-focused mode
- Arcade gravity increases noticeably as lines are cleared
- Unit tests verify gravity returns correct values at each threshold

## Step 10: Tauri Desktop Packaging

Status: COMPLETE

Goal:
- Make `+E+RIS` launch as a real desktop app window instead of a browser tab.

Required work:
- Install Tauri CLI and prerequisites (Rust toolchain)
- Create `enhanced/src-tauri/` with:
  - `tauri.conf.json` (window title: "+E+RIS", default size 1200x800, no menu bar)
  - `Cargo.toml`
  - `src/main.rs` (standard Tauri bootstrap)
- Configure `tauri.conf.json` to use Vite's `dist/` output as the frontend
- Add npm scripts: `"tauri dev"` and `"tauri build"`
- Update `Launcher.ps1` to detect and launch the Tauri executable when available, falling back to browser launch if the binary is not built
- Test that the built `.exe` opens a native window with the game running inside

Acceptance criteria:
- Running `npm run tauri dev` from `enhanced/` opens a native window with the game
- Running `npm run tauri build` produces a distributable `.exe`
- The launcher detects and uses the Tauri binary when present
- The game renders and plays identically inside the Tauri window

## Step 11: Add `Training` Mode To `+E+RIS`

Status: COMPLETE

Goal:
- Add a third major mode named `Training` focused on teaching optimal piece placement (finesse).

Concept:
- Training mode teaches the player to place each piece using the minimum number of keystrokes. A piece placed with more inputs than necessary is a "finesse fault."

Required work:

Finesse fault detection:
- Maintain a lookup table mapping `(piece type, target column, target rotation)` to the optimal key sequence length.
- For standard SRS with DAS/ARR-0 movement, optimal placements use at most 2-3 inputs (one direction move + one rotation, or hard drop alone).
- After each piece lock, compare actual input count for that piece against the optimal count from the lookup table.
- If actual > optimal, flag a finesse fault.

Reference data:
- Use the commonly published SRS finesse charts.
- Each of the 7 pieces has up to 10 columns x 4 rotations = up to 40 placements.
- Many are unreachable; only valid final positions need entries.
- The optimal input count for each valid `(column, rotation)` pair is 0-3.

UI requirements for Training mode:
- Display a `Faults` counter in the HUD showing cumulative finesse faults.
- **"Show" mode** (mistake highlighting): when a fault occurs, briefly flash the locked piece red and display the optimal input count vs. actual count as a small overlay (e.g., "3 inputs used / 2 optimal").
- **"Redo" mode** (forced retry): when a fault occurs, undo the placement, restore the piece to spawn position, and force the player to retry the same piece. The player cannot advance until the piece is placed with optimal finesse.
- Add a toggle in Training settings: `Show` / `Redo` / `Off`.

Piece sequence:
- Use standard 7-bag randomizer (same as other modes).
- No fixed/scripted sequences for the initial implementation.

Tracking:
- Training mode does not track score or speed.
- Track and display: pieces placed, finesse faults, fault rate (faults / pieces).
- Show a "perfect streak" counter (consecutive pieces with 0 faults).

Training settings panel:
- Mode toggle: Show / Redo / Off
- Optional: limit training to specific piece types (e.g., practice only T-piece placements)

Scope boundary:
- Do NOT implement T-spin detection or combo training in this step.
- Do NOT implement opener/pattern drilling in this step.
- Those are potential future extensions.

Acceptance criteria:
- `+E+RIS` supports three distinct modes: `Arcade`, `40 Lines`, `Training`
- `Training` is clearly a practice mode rather than a score-attack mode
- Finesse fault detection works correctly for all 7 piece types
- Show and Redo modes both function as specified
- Unit tests verify finesse fault detection against the lookup table for all 7 piece types

## Step 12: Audit, Stabilize, And Conditionally Rename

Status: NOT STARTED

Required work:
- Run an end-to-end audit after all requested feature work is complete
- Verify launcher behavior
- Verify `HTML` still launches in-browser correctly
- Verify `+E+RIS` launches as a Tauri desktop window correctly and its implemented features behave properly
- Verify local leaderboards, nickname entry, and mode switching
- Verify arcade gravity curve progression
- Verify training mode finesse detection
- Run the full Vitest test suite and confirm all tests pass
- Fix any detected issues before final branding changes
- Only if the audit passes cleanly with no blocking issues, rename `+E+RIS` to `MonStacka!`

Rename scope when audit passes:
- launcher labels
- in-app titles
- visible branding text
- README / docs references where appropriate
- Tauri window title and packaging name
- packaging/app window name where appropriate

Acceptance criteria:
- The requested features are implemented and verified
- No blocking issues remain from the audit
- All Vitest tests pass
- The improved edition is renamed from `+E+RIS` to `MonStacka!`

## First Real Delivery Target

The first meaningful end-to-end delivery from this plan should produce:

- a working small GUI launcher on Windows
- the final visible launcher choices `HTML` and `+E+RIS`
- a real backend-free `HTML` edition that launches from the GUI
- a real `+E+RIS` desktop app scaffold that launches from the GUI into an app window

## Definition Of Done

This plan is complete when:

- double-clicking the launcher opens the small selection window
- the window presents `HTML` and `+E+RIS`
- clicking `Run` launches the selected version
- `HTML` runs directly from local files with no backend
- `+E+RIS` launches as a Tauri-packaged desktop app window and becomes the improved playable version
- `+E+RIS` includes `Arcade`, `40 Lines`, and `Training`
- Arcade mode has a working gravity curve that increases speed with lines cleared
- Training mode detects finesse faults and supports Show/Redo modes
- the PowerShell build remains available in the repo as a reference fallback
- nickname leaderboard flow exists where appropriate
- Vitest engine tests pass
- the final audit passes cleanly
- the improved edition is renamed to `MonStacka!` once the audit passes
- the README clearly explains how to run everything
