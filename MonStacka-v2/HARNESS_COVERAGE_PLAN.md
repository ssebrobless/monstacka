# MonStacka Harness Coverage Plan

Purpose: make bugs cheaper to find before live playtests. The harness should stay deterministic, fast enough to run often, and broad enough to catch story-mode regressions across data, mechanics, UI, and visuals.

## Current Coverage Shape

```text
MonStackaV2Harness
|-- Core verifier
|   |-- piece definitions
|   |-- scoring basics
|   |-- line clear source-cell preservation
|
|-- Mode and controls matrix
|   |-- classic vs zany ability enablement
|   |-- default audio/visual settings
|   |-- keyboard hard drop stays Space only
|   |-- all piece types can reach outer lanes
|   |-- assist arm/trigger/reset lifecycle
|
|-- Story modifier scenarios
|   |-- every declared enemy modifier has HUD status text
|   |-- story chapters all have boss health
|   |-- modifier trigger/status labels stay present
|
|-- Story deterministic simulation sweep
|   |-- every chapter can run deterministic lock/drop steps
|   |-- no alive run can stay without an active piece
|   |-- next queue remains populated
|   |-- score never decreases
|   |-- boss HP percent remains clamped
|
|-- Story runtime HUD and visual sweep
|   |-- mission HP fill tracks score
|   |-- story HUD text is present/readable
|   |-- enemy status renders state tags
|   |-- 1.3 territory cells render as red enemy cells
|   |-- settings suppresses the pause banner
|
|-- Scene and layout smoke
|   |-- required scene controllers exist
|   |-- dither overlay covers canvas
|   |-- known button/preview overlaps stay fixed
|
|-- Runtime flow smoke
    |-- pause/resume
    |-- restart confirmation
    |-- training zany toggle reset
    |-- story restart pause behavior
    |-- Home button transition guard
```

## Next High-Value Expansions

1. Deterministic input playback
   - Feed scripted left/right/rotate/hold/drop sequences through `GameManager` instead of only `BoardState`.
   - Catch UI/gameplay desyncs where board logic is correct but the live view falls behind.

2. Render-state assertions
   - Count active piece, locked pieces, preview pieces, hold piece, and garbage renderers after each simulated event.
   - Fail if board state and visible objects disagree.

3. Story chapter snapshots
   - Launch every story chapter in editor mode and assert the correct HUD, objective, modifiers, preview count, and hold rules.
   - This is slower than pure simulation but should run before builds/pushes.

4. Screenshot pixel smoke
   - Capture fixed-size screenshots for home, story 1.1, story 1.3, settings, pause, and game-over.
   - Use coarse pixel checks: nonblank board, readable HUD regions, no giant unexpected gray blocks, no panel overlap.

5. Long-run fuzz pass
   - Seeded random input over all modes for hundreds of locks.
   - Fail on exceptions, inactive-alive board state, impossible score/line values, or queue starvation.

6. Build artifact validation
   - After Windows and macOS builds, run a lightweight player smoke with command-line flags.
   - Confirm the build includes the latest scenes, app icon, audio assets, and expected save/record keys.

## Rule of Thumb

When a player reports a bug, add the smallest harness check that would have caught it first. If the check needs human visual judgment, add a coarse automated sentinel anyway and keep the human taste pass for final polish.
