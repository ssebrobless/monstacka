# MonStacka Playthrough Harness Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Expand the Unity regression harness so it verifies simulated playthroughs for all modes plus focused friendly ability, enemy ability, and core gameplay mechanics.

**Architecture:** Keep coverage in `Assets/Editor/MonStackaV2Harness.cs` so it runs in the existing batch harness/report flow. Add named scenarios that exercise mode playthroughs and ability/mechanic matrices using real `BoardState`, `GameManager`, `HUDController`, and story modifier objects.

**Tech Stack:** Unity 6000.3 editor batchmode, C# editor harness, existing MonStacka runtime systems.

---

### Task 1: Add Harness Scenario Entries

**Files:**
- Modify: `C:\Users\fishe\Documents\projects\Tetris\repo_review\MonStacka-v2\Assets\Editor\MonStackaV2Harness.cs`

- [ ] Add `mode simulated playthrough sweep` after the existing mode matrix.
- [ ] Add `friendly ability mechanic scenarios` after the playthrough sweep.
- [ ] Add `enemy ability focused trigger matrix` near the story modifier checks.
- [ ] Run the harness and expect compile failures until the new methods exist.

### Task 2: Add Mode Playthrough Sweep

**Files:**
- Modify: `C:\Users\fishe\Documents\projects\Tetris\repo_review\MonStacka-v2\Assets\Editor\MonStackaV2Harness.cs`

- [ ] Create a helper that loads a real `GameManager` for each mode/variant.
- [ ] Simulate several locks/drops per mode through private runtime methods.
- [ ] Verify active pieces recover, queue stays populated, score never decreases, pause/settings works, and rendered active/locked piece views remain present.
- [ ] Include O.G.B.M. classic, O.G.B.M. zany, X(4)-LINES classic, X(4)-LINES zany, Training classic, Training zany toggle, and Story 1.3.

### Task 3: Add Friendly Ability Matrix

**Files:**
- Modify: `C:\Users\fishe\Documents\projects\Tetris\repo_review\MonStacka-v2\Assets\Editor\MonStackaV2Harness.cs`

- [ ] Trigger all seven friendly assists by sending three held lock events per piece type.
- [ ] Verify each trigger maps to the expected ability label/type.
- [ ] Verify score events or state changes for scoring assists, timed-window assists, garbage cleanup, and stitch repair.

### Task 4: Add Enemy Ability Matrix

**Files:**
- Modify: `C:\Users\fishe\Documents\projects\Tetris\repo_review\MonStacka-v2\Assets\Editor\MonStackaV2Harness.cs`

- [ ] Trigger each enemy modifier in isolation where possible.
- [ ] Verify status text has state/progress tags.
- [ ] Verify board effects: seeded territory cells, rotation penalties, overhang penalties, hunger garbage, regrowth after clears, reduced preview, no hold, and signal relay status.

### Task 5: Verify and Rebuild

**Files:**
- Modify: `C:\Users\fishe\Documents\projects\Tetris\repo_review\MonStacka-v2\Builds\Reports\monstacka-harness-latest.txt`
- Modify: `C:\Users\fishe\Documents\projects\Tetris\repo_review\MonStacka-v2\Builds\Windows\build-stamp.txt`

- [ ] Run Unity batch harness and confirm new scenarios execute.
- [ ] Fix any failures caused by the new coverage.
- [ ] Rebuild Windows player if the build freshness gate fails.
- [ ] Rerun Unity batch harness and require PASS.
