# MonStacka V1 Reference Checklist

Last updated: 2026-05-11

This file is the behavior reference Unity must match before it becomes the
player-facing build.

## Build And Test Baseline

```text
v1 reference path
└─ C:\Users\fishe\Documents\projects\Tetris\repo_review\enhanced
```

Current baseline:
- `npm test`: passed on 2026-05-11
- test result: 9 files, 42 tests passing

## Modes

Unity must preserve these mode meanings:
- `O.G.B.M.` maps to v1 `arcade`: endless score chase until top-out.
- `X(4)-LINES` maps to v1 `sprint40`: clear 40 lines as fast as possible.
- `You... Suck?` maps to v1 `training`: training mode with feedback behavior.

## Keyboard Defaults

```text
Keyboard gameplay
├─ Left Arrow  -> move left
├─ Right Arrow -> move right
├─ Down Arrow  -> soft drop
├─ Space       -> hard drop
├─ Z           -> rotate CCW
├─ X           -> rotate CW
├─ A           -> rotate 180
├─ C           -> hold
├─ R           -> retry
├─ P           -> pause / resume
└─ O           -> restart while paused
```

## Xbox Defaults

```text
Xbox gameplay
├─ D-pad Left  -> move left
├─ D-pad Right -> move right
├─ D-pad Down  -> soft drop
├─ D-pad Up    -> hard drop
├─ A           -> rotate CCW
├─ B           -> rotate CW
├─ Y           -> rotate 180
├─ LB          -> hold
├─ Start       -> retry
├─ View/Back   -> pause / resume
└─ L3          -> restart while paused
```

## State Flow

Unity must preserve these flows:
- home menu -> mode start -> countdown/playable run
- active run -> pause -> resume
- paused run -> restart current mode
- active or paused run -> home
- top-out -> record/retry/home flow
- sprint completion -> sprint-clear/record flow
- settings changes and remaps persist through storage where v1 does

## Visual Identity Rules

Unity must preserve:
- all seven hand-drawn creature tetrominoes
- body frame animation
- cyan independent eye motion
- purple independent eye/blink timing
- clean preview and gameplay sprites with no missing chunks
- no plain fallback blocks in the final presentation

