# MonStacka Recovery Implementation Plan

Last updated: 2026-05-11

This plan turns `MONSTACKA_UNIFIED_RECOVERY_ISSUES.md` into a concrete order of
implementation. The goal is to recover a high-quality Unity version of MonStacka
that behaves like the working v1 game while preserving the new visual direction.

## Visual North Star

The recovery work must protect the original art concept while restoring stability.

```text
Visual stack
╔════════════════════════════════════════════════════════════════════╗
║ top layer:    cyan/purple independent eye overlays               ║
║ middle layer: original animated monster body sprite              ║
║ bottom layer: color-matched silhouette ripple underlay            ║
╚════════════════════════════════════════════════════════════════════╝
```

Rules:
- Keep the hand-drawn monster tetrominoes as the identity of the game.
- Never split a piece into visible cell chunks.
- Never hide, crop, or replace parts of the original drawings.
- Keep body frame animation alive on active and landed blocks.
- Preserve cyan independent eye motion and purple independent eye/blink motion.
- Keep ripple visual-only, underneath the body, sampled from the real sprite colors,
  and shaped like a traveling outline wave rather than square chunks.
- Do not package or publish Unity until it matches v1 behavior and passes the phase
  audits below.

```text
Execution ladder
╔════════════════════════════════════════════════════════════════════╗
║ 0. Stop the loop                                                  ║
║ 1. Establish v1 as executable truth                               ║
║ 2. Restore Unity gameplay correctness                             ║
║ 3. Restore controls, controller, and settings parity              ║
║ 4. Restore modes and run-state flow                               ║
║ 5. Fix live-board sprite rendering                                ║
║ 6. Restore animation identity                                     ║
║ 7. Stabilize UI, HUD, and resizing                                ║
║ 8. Remove lag and runtime churn                                   ║
║ 9. Rebuild ripple as silhouette underlay                          ║
║ 10. Reintroduce ripple in gates                                   ║
║ 11. Full audit and repair loop                                    ║
╚════════════════════════════════════════════════════════════════════╝
```

## Phase Structure

The numbered steps below are grouped into phases. A phase is not complete until its
build checkpoint and audit gate pass.

```text
Recovery phases
╔════════════════════════════════════════════════════════════════════╗
║ Phase A: Reference and safety gate                               ║
║ ├─ 0. Stop the loop                                              ║
║ └─ 1. Establish v1 as executable truth                           ║
╠════════════════════════════════════════════════════════════════════╣
║ Phase B: Playable game recovery                                  ║
║ ├─ 2. Restore Unity gameplay correctness                         ║
║ ├─ 3. Restore controls, controller, and settings parity          ║
║ └─ 4. Restore modes and run-state flow                           ║
╠════════════════════════════════════════════════════════════════════╣
║ Phase C: Visual parity recovery                                  ║
║ ├─ 5. Fix live-board sprite rendering                            ║
║ └─ 6. Restore animation identity                                 ║
╠════════════════════════════════════════════════════════════════════╣
║ Phase D: Shell quality recovery                                  ║
║ ├─ 7. Stabilize UI, HUD, and resizing                            ║
║ └─ 8. Remove lag and runtime churn                               ║
╠════════════════════════════════════════════════════════════════════╣
║ Phase E: Ripple reimplementation                                 ║
║ ├─ 9. Rebuild ripple as silhouette underlay                      ║
║ └─ 10. Reintroduce ripple in gates                               ║
╠════════════════════════════════════════════════════════════════════╣
║ Phase F: Candidate build and release audit                       ║
║ └─ 11. Full audit and repair loop                                ║
╚════════════════════════════════════════════════════════════════════╝
```

## Local Build Protocol

The local build must always reflect the latest passed phase, not the latest experiment.

```text
Build lanes
╔════════════════════════════════════════════════════════════════════╗
║ Working source                                                    ║
║ └─ MonStacka-v2 Unity project files under active development      ║
╠════════════════════════════════════════════════════════════════════╣
║ Internal candidate build                                          ║
║ └─ rebuilt after each phase for automated and visual audit        ║
╠════════════════════════════════════════════════════════════════════╣
║ User-test build                                                   ║
║ └─ only launched for the user after the phase audit passes        ║
╚════════════════════════════════════════════════════════════════════╝
```

Rules:
- After every phase, rebuild the Windows player at
  `MonStacka-v2/Builds/Windows/MonStackaV2.exe`.
- Do not ask the user to visually test a build that has not passed that phase's
  automated checks.
- If a phase fails, keep the last passed build as the user-test build and treat the
  new output as an internal candidate only.
- Every rebuild must include the Unity data folder beside the executable:
  `MonStackaV2_Data`.
- Every phase must update a short local status note in this document before moving on:
  phase name, build time, checks run, pass/fail result, and remaining defects.
- Only Phase F can produce a packaging/release candidate.

Recommended local verification commands:
- v1 reference: run `npm test` and the existing MonStacka audits from `enhanced`.
- Unity recovery: run the Unity verification method and build the Windows player.
- Visual audit: launch the Windows player from `MonStacka-v2/Builds/Windows`, not from
  the project root, so Unity can find `MonStackaV2_Data`.

## Phase Status Log

Update this log as phases are completed.

| Phase | Status | Local build status | Notes |
|---|---|---|---|
| Phase A: Reference and safety gate | In progress | No new user-test build | Safety switch implemented. v1 `npm test` and `npm run build` pass. Unity batch verifier blocked by local Editor license. |
| Phase B: Playable game recovery | Not started | Blocked by Phase A | Gameplay, controls, controller, settings, and modes. |
| Phase C: Visual parity recovery | Not started | Blocked by Phase B | Clean board sprites and cyan/purple identity animation. |
| Phase D: Shell quality recovery | Not started | Blocked by Phase C | HUD, resize, responsiveness. |
| Phase E: Ripple reimplementation | Not started | Blocked by Phase D | New silhouette-underlay ripple. |
| Phase F: Candidate build and release audit | Not started | Blocked by Phase E | Full audit and packaging decision. |

Phase A notes:
- Added a Unity `VisualExtrasEnabled` safety gate through app state and `PieceSkin`.
- Added `-monstacka-no-visual-extras` / `MONSTACKA_VISUAL_EXTRAS=off` support through `BootLoader`.
- Added verifier coverage that visual extras off keeps body art visible while disabling border pulse and facial overlays.
- Created `MONSTACKA_V1_REFERENCE_CHECKLIST.md`.
- v1 reference checks passed on 2026-05-11: `npm test` and `npm run build`.
- Unity verification attempted on 2026-05-11, but the local Unity Editor reported no valid license before running project verification.

## 0. Stop The Loop

Purpose:
- Freeze risky ripple and live-board visual experimentation until the foundation is
  trustworthy again.

Implementation:
- Add one debug/tuning switch in Unity that can disable all ripple and nonessential
  visual extras while leaving body sprites and authored frame animation on.
- Keep the ripple code, but gate it so gameplay can be tested without it.
- Do not delete the current ripple work yet.

Pass gate:
- Unity can run with `VisualExtras=Off` and still display all pieces as normal body art.

Fail means:
- Any body sprite depends on the ripple or border system to be visible.

## 1. Establish V1 As Executable Truth

Purpose:
- Make the Tauri/web build the measured reference instead of relying on memory.

Implementation:
- Run the v1 tests in `enhanced`.
- Capture or refresh a short reference checklist for:
  - O.G.B.M.
  - X(4)-LINES
  - You... Suck?
  - keyboard controls
  - Xbox controls
  - settings and remap flow
  - home menu block viewer
  - in-game HUD layout
- Record the exact control mapping Unity must match.

Pass gate:
- We have a written v1 behavior checklist and the v1 tests pass.

Fail means:
- The reference build itself is broken or ambiguous and must be stabilized first.

## 2. Restore Unity Gameplay Correctness

Purpose:
- Fix actual Tetris behavior before judging visuals.

Implementation:
- Audit Unity `BoardState`, `GameManager`, `PieceBag`, and `PieceDefinitions`
  against v1 `board.ts`, `pieces.ts`, `gravity.ts`, and `bag.ts`.
- Ensure BoardState is the only source of truth for:
  - spawn
  - movement
  - rotation
  - collision
  - gravity
  - locking
  - line clears
  - hold
  - queue/bag order
- Fix any path where view transforms or visual objects influence gameplay placement.
- Add deterministic Unity verification cases for:
  - floor lock
  - stack lock
  - wall collision
  - SRS kicks
  - line clear
  - hold once per spawn
  - top-out

Pass gate:
- No overlap, no fall-through, no impossible gaps in automated gameplay verification.

Fail means:
- Do not touch UI or ripple. Continue here until the board is correct.

## 3. Restore Controls, Controller, And Settings Parity

Purpose:
- Recover the interaction model that already worked in v1.

Implementation:
- Port the v1 action-map approach instead of hardcoding Unity-only controls.
- Restore keyboard/mouse mappings from v1.
- Restore Xbox gameplay defaults:
  - D-pad Left/Right: move
  - D-pad Down: soft drop
  - D-pad Up: hard drop
  - A: rotate CCW
  - B: rotate CW
  - Y: rotate 180
  - LB: hold
  - View/Select: pause
  - Start: retry where applicable
- Restore controller UI navigation:
  - home menu focus flow
  - pause menu focus flow
  - settings navigation
  - controls remap navigation
  - nickname entry behavior
- Add an in-game controls/settings route so the player can inspect or change mappings.

Pass gate:
- Keyboard and Xbox can both start a mode, play, pause, resume, restart, return home,
  open settings, and navigate controls.

Fail means:
- Do not proceed to visual polish. Missing input parity is a release blocker.

## 4. Restore Modes And Run-State Flow

Purpose:
- Make the Unity game feel like MonStacka again, not only a board test.

Implementation:
- Restore mode-specific behavior:
  - O.G.B.M. endless scoring
  - X(4)-LINES sprint completion
  - You... Suck? training behavior
- Restore run transitions:
  - home -> mode start
  - active -> pause
  - pause -> resume
  - pause -> restart
  - active/pause -> home
  - game over -> record/continue/retry
- Ensure retry creates a fresh run.
- Ensure home abandons the current run cleanly.

Pass gate:
- Each mode can be started, played, paused, restarted, ended, and exited without
  stale board state or stuck UI.

Fail means:
- Continue state-flow work before rendering polish.

## 5. Fix Live-Board Sprite Rendering

Purpose:
- Make gameplay pieces as clean as the home preview pieces.

Implementation:
- Define one canonical piece-body assembly pipeline.
- Use that same pipeline for:
  - home block viewer
  - hold box
  - next queue
  - active gameplay piece
  - locked board pieces
- Remove or isolate any alternate runtime path that crops or rotates pieces differently
  for gameplay.
- Verify all seven pieces in all rotations:
  - I/cyan visible
  - T/purple intact
  - J/pink intact
  - L/orange intact
  - O/yellow intact
  - S/red intact
  - Z/green intact

Pass gate:
- A screenshot audit shows no missing cells, invisible chunks, or gameplay-only sprite
  differences for all seven pieces.

Fail means:
- Fix the rendering path before re-enabling ripple on gameplay.

## 6. Restore Animation Identity

Purpose:
- Preserve what makes the blocks feel alive without breaking their body shape.

Implementation:
- Keep body frame animation for active and landed pieces.
- Keep cyan independent eye motion.
- Keep purple independent blink/eye timing.
- Ensure special eye overlays are positioned from the canonical body assembly data.
- Ensure eye overlays render above body art and above ripple underlay.

Pass gate:
- Cyan and purple animate correctly in home preview and gameplay.
- Landed pieces continue their authored body animation.

Fail means:
- Do not tune ripple until identity animation is stable.

## 7. Stabilize UI, HUD, And Resizing

Purpose:
- Stop text and panels from drifting when the window changes.

Implementation:
- Treat the whole game as one fixed 16:9 artboard that scales as a unit.
- Put world art, board, HUD, buttons, and text under the same viewport calculation.
- Remove remaining mixed coordinate-space layout for runtime UI.
- Redesign the left in-game HUD as a compact stable panel:
  - score/goal/lines/time grouped clearly
  - hold and next areas sized consistently
  - next queue evenly spaced and not hugging
- Fix home name box alignment so resize/minimize/maximize does not shift it.
- Keep bottom content above the Windows taskbar in windowed mode.

Pass gate:
- Resize, maximize, restore, and taskbar-visible tests show no misaligned text or panels.

Fail means:
- Continue layout work before performance/ripple polish.

## 8. Remove Lag And Runtime Churn

Purpose:
- Make the game feel responsive and elegant.

Implementation:
- Replace repeated destroy/recreate paths with persistent view objects where practical.
- Rebuild only changed pieces or UI regions.
- Cache body sprites, ripple meshes, sampled colors, and generated preview objects.
- Profile the obvious hot paths:
  - piece spawn
  - movement repeat
  - lock/update stack
  - next queue refresh
  - home block viewer cycling
- Keep input polling independent from expensive visual refreshes.

Pass gate:
- Movement feels responsive and no visible hitch occurs when pieces spawn or lock.

Fail means:
- Continue optimization before adding richer ripple behavior.

## 9. Rebuild Ripple As Silhouette Underlay

Purpose:
- Keep the lively ripple concept while preventing it from breaking the actual sprite.

Implementation:
- Body sprite remains untouched.
- Eye overlays remain untouched.
- Ripple renders as a separate solid-color underlay below the body sprite.
- Derive the underlay color from the real body art:
  - sample non-transparent body pixels
  - ignore dark outlines, eyes, teeth, mouth, and highlights
  - cache one dominant body color per piece
- Trace the final silhouette of the assembled body.
- Place dense anchor points around the entire outer contour.
- Animate the contour between anchors with a traveling wave:
  - small local offsets
  - sequential timing
  - spring back to rest
  - no large bar-like motion
- Keep the ripple close enough to read as part of the body surface.

Pass gate:
- Ripple is visible, color-matched, attached to the sprite, and wave-like in preview
  without stretching facial details.

Fail means:
- Keep ripple disabled for gameplay and continue underlay work.

## 10. Reintroduce Ripple In Gates

Purpose:
- Avoid breaking gameplay again while restoring the visual ambition.

Implementation:
- Gate A: home preview active piece only.
- Gate B: all home preview pieces, with side previews calmer.
- Gate C: active gameplay piece only.
- Gate D: landed gameplay pieces with subtle constant motion.
- Gate E: impact/contact emphasis.

Pass gate:
- Each gate passes visual audit and gameplay audit before enabling the next gate.

Fail means:
- Disable only the failing gate and repair that layer.

## 11. Full Audit And Repair Loop

Purpose:
- Finish with evidence instead of vibes.

Audit checklist:
- gameplay: no overlap, no fall-through, no stale state
- modes: all three modes work
- controls: keyboard and Xbox pass
- UI: home/game/settings/record screens align
- resize: window changes do not drift layout
- sprites: all seven pieces clean in preview and gameplay
- animation: body frames plus cyan/purple special cases work
- ripple: visible, attached, color-matched, wave-like
- performance: no obvious lag on spawn, move, lock, or menu navigation

Final outcome:
- If issues remain, record them as a new repair list and repeat from the earliest
  affected stage.
- If no issues remain, the Unity build can become the candidate for packaging.
