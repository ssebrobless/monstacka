# MonStacka Gameplay Audit Plan

```text
Gameplay flow
├─ Arcade      ✓ stronger audit passes
├─ 40 Lines    ✓ stronger audit passes
└─ Training    ✓ stronger audit passes

Corrections applied
├─ end-state text moved off the board
├─ Retry hidden during live play
├─ voice button no longer changes lore state
├─ forced end-of-run audit hooks added
├─ leaderboard logic normalized to 8 entries
└─ save/resume summaries checked from real progress

Current audit state
├─ page errors                none
├─ canvas warnings            none
├─ leaderboard insert paths   verified
└─ remaining focus            sprite / visual polish
```

## Live Verification Status

The stronger automated audit now verifies:

- mode start from the home menu
- piece spawn / input response
- pause with `P`
- resume with `P`
- restart fresh with `O` while paused
- return Home
- per-mode Continue prompt when reopening that same mode
- forced `Arcade` top-out
- forced `40 Lines` completion
- qualifying nickname modal for score and sprint
- real leaderboard insertion after a qualifying result

Primary evidence:

- `enhanced/audit-artifacts/audit-report.json`
- `enhanced/audit-artifacts/animation-audit.json`

## Confirmed Improvements Applied

### 1. Voice preview no longer affects lore state

Current audit result:

- `preview.voiceButtonDidChangeLoreState` is now `false`

Meaning:

- only the lore button opens/closes lore
- the voice button only plays the preview beep

### 2. End-of-run flows are now explicitly verified

Current audit result:

- forced `Arcade` top-out opens the correct record modal
- forced `40 Lines` clear opens the correct record modal
- both end states render in the dedicated right-side end-state panel

### 3. Leaderboard writes are now verified against the actual visible board

Current audit result:

- `Arcade` forced score save inserts `RIP1 13337 pts` into visible rank 1
- `40 Lines` forced save inserts `ZIPPY 00:52.890` into visible rank 1

### 4. Leaderboard storage now matches the real UI

The home leaderboard has 8 boxes, so storage and qualification now also use 8 entries.

Definition now:

- only top 8 qualify
- only top 8 are persisted
- only top 8 are rendered

## Current Acceptance Status

Gameplay/UI flow is now clean enough to shift back into sprite work because:

- all three modes pass the stronger flow audit
- voice button remains lore-independent
- `Arcade` end-of-run and record flow pass
- `40 Lines` end-of-run and record flow pass
- leaderboard qualification, storage, and render all use the same 8-entry model
- save/resume summaries are verified with real progress
- the stronger audit currently reports no page errors and no canvas warnings
