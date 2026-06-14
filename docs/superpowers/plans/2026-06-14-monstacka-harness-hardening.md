# MonStacka Harness Hardening Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the MonStacka harness catch story-mode gameplay, UI, visual, and build regressions before they reach live playtests.

**Architecture:** Keep `MonStackaV2Harness` as the canonical regression entrypoint, but make it stricter and more observant. First, fail on unexpected Unity warnings/errors. Then add targeted probes for board/render synchronization, story modifier progress, UI layout, and build freshness.

**Tech Stack:** Unity 6000.3.11f1, C# editor harness, MonStacka runtime scenes, PowerShell batch execution.

---

## Evidence From Current Harness Run

```text
╔════════════════════════════════════════════════════╗
║ Current Result                                     ║
╠════════════════════════════════════════════════════╣
║ Harness report                                    ║ PASS 12/12
║ Hidden log issue                                  ║ 8 edit-mode Destroy warnings
║ Affected flow                                     ║ runtime game flow smoke
║ Primary files                                     ║ GameManager.cs, NextQueueView.cs
║ Harness gap                                       ║ report can pass while log is dirty
╚════════════════════════════════════════════════════╝
```

Current command:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.3.11f1\Editor\Unity.exe' -batchmode -quit -projectPath 'C:\Users\fishe\Documents\projects\Tetris\repo_review\MonStacka-v2' -executeMethod MonStacka.Editor.MonStackaV2Harness.RunBatchMode -logFile 'C:\Users\fishe\Documents\projects\Tetris\repo_review\MonStacka-v2\monstacka_harness_investigation.log'
```

Confirmed warning:

```text
Destroy may not be called from edit mode! Use DestroyImmediate instead.
```

Observed call paths:

```text
GameManager.RebuildLockedPieceViews
  └─ Destroy(existing.gameObject)

NextQueueView.EnsureSlot
  └─ Destroy(previews[index].gameObject)
```

---

### Task 1: Make Harness Warnings Actionable

**Files:**
- Modify: `C:\Users\fishe\Documents\projects\Tetris\repo_review\MonStacka-v2\Assets\Editor\MonStackaV2Harness.cs`
- Modify: `C:\Users\fishe\Documents\projects\Tetris\repo_review\MonStacka-v2\Assets\MonStacka\Scripts\Core\GameManager.cs`
- Modify: `C:\Users\fishe\Documents\projects\Tetris\repo_review\MonStacka-v2\Assets\MonStacka\Scripts\UI\NextQueueView.cs`
- Modify: `C:\Users\fishe\Documents\projects\Tetris\repo_review\MonStacka-v2\Assets\MonStacka\Scripts\UI\HoldBoxView.cs`
- Modify: `C:\Users\fishe\Documents\projects\Tetris\repo_review\MonStacka-v2\Assets\MonStacka\Scripts\Visual\PieceSkin.cs`

- [ ] **Step 1: Add a harness log guard**

Add a `LogMessageReceived` collector around each scenario or the whole harness run. Treat unexpected `LogType.Warning`, `LogType.Error`, `LogType.Exception`, and `LogType.Assert` as failures.

Expected allowed noise:

```csharp
private static readonly string[] AllowedLogFragments =
{
    "Licensing",
    "Access token is unavailable",
    "Unsupported protocol version",
};
```

- [ ] **Step 2: Verify the new guard fails before cleanup**

Run:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.3.11f1\Editor\Unity.exe' -batchmode -quit -projectPath 'C:\Users\fishe\Documents\projects\Tetris\repo_review\MonStacka-v2' -executeMethod MonStacka.Editor.MonStackaV2Harness.RunBatchMode -logFile 'C:\Users\fishe\Documents\projects\Tetris\repo_review\MonStacka-v2\monstacka_harness_logguard_fail.log'
```

Expected: FAIL, naming the edit-mode `Destroy` warning.

- [ ] **Step 3: Replace runtime-only destroy calls with the repo pattern**

Use the same pattern already present in `BoardBackdropView` and `HomeMenuController`:

```csharp
private static void DestroyGameObject(GameObject go)
{
    if (!go)
    {
        return;
    }

    if (Application.isPlaying)
    {
        UnityEngine.Object.Destroy(go);
    }
    else
    {
        UnityEngine.Object.DestroyImmediate(go);
    }
}
```

Use it for visual rebuild paths in `GameManager`, `NextQueueView`, `HoldBoxView`, and `PieceSkin`.

- [ ] **Step 4: Verify the harness is both green and clean**

Run:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.3.11f1\Editor\Unity.exe' -batchmode -quit -projectPath 'C:\Users\fishe\Documents\projects\Tetris\repo_review\MonStacka-v2' -executeMethod MonStacka.Editor.MonStackaV2Harness.RunBatchMode -logFile 'C:\Users\fishe\Documents\projects\Tetris\repo_review\MonStacka-v2\monstacka_harness_logguard_clean.log'
```

Expected: PASS 12/12, no unexpected warning/error/exception records.

---

### Task 2: Add Board-to-Renderer Consistency Checks

**Files:**
- Modify: `C:\Users\fishe\Documents\projects\Tetris\repo_review\MonStacka-v2\Assets\Editor\MonStackaV2Harness.cs`
- Inspect: `C:\Users\fishe\Documents\projects\Tetris\repo_review\MonStacka-v2\Assets\MonStacka\Scripts\Core\GameManager.cs`
- Inspect: `C:\Users\fishe\Documents\projects\Tetris\repo_review\MonStacka-v2\Assets\MonStacka\Scripts\Visual\PieceSkin.cs`

- [ ] **Step 1: Add a failing check for line-clear render mismatch**

Create a scenario named `story render state consistency sweep`.

Minimum assertions:

```text
locked board cells count == visible locked sprite cells count
active piece exists when BoardState.HasActivePiece is true
no orphan gray territory placeholder renderers remain
partial pieces retain their source-cell skin coordinates after line clear
```

- [ ] **Step 2: Simulate the exact bug family**

Use a deterministic board setup that clears one row, leaves partial source cells, calls the runtime visual sync path, and checks rendered cell count and sprite source identity.

- [ ] **Step 3: Run harness**

Expected before implementation, if current visual sync regresses again: FAIL with a cell-count/source-cell mismatch. Expected after current fixes: PASS.

---

### Task 3: Add Story Enemy Ability Progress Assertions

**Files:**
- Modify: `C:\Users\fishe\Documents\projects\Tetris\repo_review\MonStacka-v2\Assets\Editor\MonStackaV2Harness.cs`
- Inspect: `C:\Users\fishe\Documents\projects\Tetris\repo_review\MonStacka-v2\Assets\MonStacka\Scripts\Story\StoryModifierSystem.cs`

- [ ] **Step 1: Extend enemy modifier scenarios to assert progress, not only text**

Current harness checks status labels. Add state assertions for each modifier:

```text
Guard Pressure       -> lock delay multiplier active
Territory Cells      -> seeded cell count increases on match start
Calculated Planning  -> rotation counter advances and punishment fires
Precision Pressure   -> unsupported overhang punishment fires
Hunger Meter         -> timer progress advances with Tick()
Adrenaline Monitor   -> gravity multiplier changes when stack height is high
```

- [ ] **Step 2: Verify right-side HUD mirrors real state**

For each story chapter, compare `BuildEnemyAbilityStatus()` against actual modifier state. Fail when HUD says idle or empty while the system has active progress.

- [ ] **Step 3: Run harness**

Expected: PASS, with failures becoming specific enough to say which modifier drifted.

---

### Task 4: Add Story Input Playback

**Files:**
- Modify: `C:\Users\fishe\Documents\projects\Tetris\repo_review\MonStacka-v2\Assets\Editor\MonStackaV2Harness.cs`
- Inspect: `C:\Users\fishe\Documents\projects\Tetris\repo_review\MonStacka-v2\Assets\MonStacka\Scripts\Core\GameManager.cs`
- Inspect: `C:\Users\fishe\Documents\projects\Tetris\repo_review\MonStacka-v2\Assets\MonStacka\Scripts\Core\MonStackaControls.cs`

- [ ] **Step 1: Add a deterministic input script runner**

Feed actions through `GameManager` where possible:

```text
Left, Left, RotateCW, SoftDrop, Hold, SwapHoldQueue1, HardDrop
Right, Right, SwapHoldQueue2, HardDrop
Pause, Settings, CloseSettings, Resume
```

- [ ] **Step 2: Assert invariants after every action**

```text
alive run has active piece or has explicit game over
next queue is populated
piece can still reach left and right lanes
Space is the only keyboard hard drop
pause does not open while story dialogue advances
```

- [ ] **Step 3: Run against Story 1.1, 1.2, and 1.3**

Expected: PASS without active-piece starvation, input conflicts, or side-lane restriction.

---

### Task 5: Add Coarse Screenshot and Layout Sentinels

**Files:**
- Modify: `C:\Users\fishe\Documents\projects\Tetris\repo_review\MonStacka-v2\Assets\Editor\MonStackaV2Harness.cs`
- Optional create: `C:\Users\fishe\Documents\projects\Tetris\repo_review\MonStacka-v2\Builds\Reports\Screenshots\`

- [ ] **Step 1: Capture fixed-size screenshots**

Capture:

```text
Home
Story 1.1 gameplay
Story 1.3 gameplay
Pause + settings
Game over
```

- [ ] **Step 2: Add coarse pixel checks**

Do not attempt taste judgments. Only sentinel checks:

```text
right HUD panel is nonblank
mission HP area is inside right HUD panel
settings panel does not overlap preview buttons
no large gray enemy-cell placeholders
home info text panel has readable high-contrast region
dither overlay covers the whole frame
```

- [ ] **Step 3: Save screenshots with the report**

Expected: harness report includes screenshot paths for quick human review.

---

### Task 6: Add Build Freshness and Cross-Platform Checks

**Files:**
- Modify: `C:\Users\fishe\Documents\projects\Tetris\repo_review\MonStacka-v2\Assets\Editor\MonStackaV2Harness.cs`
- Inspect: `C:\Users\fishe\Documents\projects\Tetris\repo_review\MonStacka-v2\Builds\Windows\build-stamp.txt`

- [ ] **Step 1: Fail stale builds**

Compare build-stamp timestamp against latest modified runtime/script asset timestamp. Fail if the player build is older than gameplay/UI/script changes.

- [ ] **Step 2: Check required release files**

Assert:

```text
Windows exe exists
Windows data folder exists
launch script exists
build stamp exists
app icon asset exists
README or download instructions exist
```

- [ ] **Step 3: Add macOS artifact placeholders**

If macOS build artifacts are absent, report `SKIP macOS build artifacts` rather than silently passing. Once a macOS build exists, change to strict checks.

---

## Priority Order

```text
1. Log guard + edit-mode cleanup
   └─ Makes PASS mean clean enough to trust.

2. Board-to-renderer consistency
   └─ Targets disappearing/chunked block bugs after line clears.

3. Story enemy state progress
   └─ Targets enemy tracker not running or not explaining trigger progress.

4. Story input playback
   └─ Targets side-lane movement, hard-drop conflicts, pause/dialogue conflicts.

5. Screenshot sentinels
   └─ Targets HUD misalignment and obvious visual placeholder regressions.

6. Build freshness/cross-platform checks
   └─ Prevents friends from downloading old or incomplete builds.
```

## Done Definition

```text
╔════════════════════════════════════════════════════╗
║ Harness Hardening Done When                       ║
╠════════════════════════════════════════════════════╣
║ Batch harness                                     ║ PASS
║ Unexpected Unity warnings/errors                  ║ 0
║ Story render-state sweep                          ║ PASS
║ Story enemy progress sweep                        ║ PASS
║ Story input playback                              ║ PASS
║ Screenshot/layout sentinel report                 ║ generated
║ Build freshness check                             ║ strict for Windows
╚════════════════════════════════════════════════════╝
```
