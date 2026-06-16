# MonStacka Runtime Replay Harness Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add deterministic runtime replay coverage that drives real `GameManager` and `BoardState` actions through repeatable player-like scripts.

**Architecture:** Extend `C:\Users\fishe\Documents\projects\Tetris\repo_review\MonStacka-v2\Assets\Editor\MonStackaV2Harness.cs` with an in-editor replay driver. The driver loads real scenes, performs movement/rotate/hold/swap/drop/pause actions, and asserts board, HUD, visual, modifier, and ability state after checkpoints.

**Tech Stack:** Unity 6000.3 editor batchmode, C# editor harness, existing MonStacka runtime systems.

---

### Task 1: Add A Failing Replay Scenario

**Files:**
- Modify: `C:\Users\fishe\Documents\projects\Tetris\repo_review\MonStacka-v2\Assets\Editor\MonStackaV2Harness.cs`

- [x] Add `runtime replay driver sweep` to `BuildScenarios`.
- [x] Add a temporary failing `VerifyRuntimeReplayDriverSweep` method so the harness proves the new scenario is active before implementation.
- [x] Run Unity batch harness and expect the new scenario to fail.

### Task 2: Implement Replay Driver

**Files:**
- Modify: `C:\Users\fishe\Documents\projects\Tetris\repo_review\MonStacka-v2\Assets\Editor\MonStackaV2Harness.cs`

- [x] Add `ReplayActionKind`, `ReplayAction`, and `ReplayScenario` helper types.
- [x] Add `RunReplayScenario` to load a real `GameManager`, execute actions, update visuals, and assert post-action invariants.
- [x] Support player-like actions: move left/right, rotate, soft drop, hold, hold queue swap 1/2/3, hard drop, pause/resume, settings open/close, restart prompt/cancel, and force game over.

### Task 3: Add Mode Replays

**Files:**
- Modify: `C:\Users\fishe\Documents\projects\Tetris\repo_review\MonStacka-v2\Assets\Editor\MonStackaV2Harness.cs`

- [x] Add replay scripts for O.G.B.M. classic/zany, X(4)-LINES classic/zany, Training classic/zany, and Story 1.1/1.2/1.3.
- [x] Assert active piece recovery, non-empty next queue, reachable outer lanes, visible `PieceSkin` renderers, no game-over unless forced, and pause/settings/home safety.

### Task 4: Add Ability Replays

**Files:**
- Modify: `C:\Users\fishe\Documents\projects\Tetris\repo_review\MonStacka-v2\Assets\Editor\MonStackaV2Harness.cs`

- [x] Replay the live friendly ability trigger path using real held placements; the existing friendly ability matrix covers all seven specific effects.
- [x] Replay enemy modifiers and assert their status text/progress is populated after trigger events.
- [x] Assert score/mission HP updates are monotonic and friendly ability point events keep colored source metadata.

### Task 5: Verify

**Files:**
- Modify: `C:\Users\fishe\Documents\projects\Tetris\repo_review\MonStacka-v2\Builds\Reports\monstacka-harness-latest.txt`

- [x] Run Unity batch harness.
- [x] Fix failures until `MonStacka harness PASS`.
- [x] If source changes make the Windows build stale, rebuild and rerun.
