# MonStacka Unified Recovery Issues

Last updated: 2026-05-11

This is the consolidated issue register for MonStacka after the recent Claude work.

Its purpose is to stop the project from looping through small point-fixes and instead
drive recovery by root cause, in a strict implementation order.

```text
MonStacka state
╔══════════════════════════════════════════════════════════════════════╗
║ v1 reference: enhanced/                                            ║
║ ├─ still the behavioral source of truth                            ║
║ ├─ contains the intended gameplay, UI flow, controls, and content  ║
║ └─ should define what Unity must match                             ║
╠══════════════════════════════════════════════════════════════════════╣
║ v2 port: MonStacka-v2/                                             ║
║ ├─ contains the new visual/ripple experimentation                  ║
║ ├─ currently mixes stable and unstable systems                     ║
║ └─ is not yet parity-clean or release-ready                        ║
╚══════════════════════════════════════════════════════════════════════╝
```

## Core Diagnosis

The project has not mainly been failing because of one bad ripple implementation.

It has been failing because four different categories of problems have been
interleaving:

```text
Failure stack
╔══════════════════════════════════════════════════════════════════════╗
║ 1. Gameplay parity regressions                                      ║
║ 2. Live board render-path regressions                               ║
║ 3. UI/layout/controller parity regressions                          ║
║ 4. Ripple experimentation layered on top of unstable foundations    ║
╚══════════════════════════════════════════════════════════════════════╝
```

That means some changes improved one visual symptom while making a more basic system
less trustworthy.

The elegant fix is therefore:

```text
Correct recovery order
1. Restore gameplay truth
2. Restore board sprite truth
3. Restore UI/layout/control truth
4. Reintroduce ripple on top of the stable systems
```

Not:

```text
Wrong order
1. Tune ripple
2. Patch another visual bug
3. Discover gameplay broke again
4. Patch around that
```

## Unified Issue List

### A. Gameplay / Rules Regressions

These are the highest-severity issues because they make the game unplayable even if
the visuals improve.

```text
Observed failures
├─ pieces overlap instead of locking cleanly
├─ pieces can fall through the bottom or through the stack
├─ stack sometimes shows impossible gaps or ghost spacing
├─ gameplay feels laggy / unresponsive
├─ mode behavior is not reliably matching v1
└─ run-state transitions are not trustworthy
```

Concrete symptoms reported:
- blocks falling through the board
- blocks stacking on top of each other incorrectly
- overlapping pieces during active play
- random invisible gaps between landed shapes
- game modes not feeling like the old version
- controls feeling delayed or bad

Likely root causes:
- Unity board truth and view truth are still drifting apart
- live board rendering has not been safely unified with the healthy preview logic
- update/rebuild work is happening at the wrong times
- gameplay verification is too weak to catch runtime parity breaks immediately

Non-negotiable requirement:
- `MonStacka-v2` must exactly match the v1 gameplay rules before further polish is trusted

### B. Live Board Sprite Integrity Regressions

These are the issues where the board pieces themselves are incomplete, clipped,
invisible, or assembled differently from the cleaner preview path.

```text
Observed failures
├─ cyan can disappear or fail to render on the live board
├─ pink can lose part of its body
├─ orange can lose segments
├─ live board pieces can show blank regions or invisible cells
├─ preview can look good while gameplay still looks broken
└─ some pieces appear differently in preview vs gameplay
```

Concrete symptoms reported:
- cyan not visible in gameplay
- pink partially broken
- orange partially broken
- invisible chunks missing from the user’s drawings
- preview viewer looking better than the live game board

Likely root causes:
- gameplay path and preview path are still not fully using the same connected-body truth
- rotated/full-box extraction and cropped-body extraction have diverged
- some runtime assembly decisions are still conditional in ways that the previewer does not share

Non-negotiable requirement:
- preview and gameplay must render from the same piece-body truth

### C. Special Animation Regressions

These are the issues specifically tied to animation identity rather than generic board rendering.

```text
Observed failures
├─ cyan independent eyes became buggy or disappeared in some paths
├─ purple independent eyes became buggy or got stretched
├─ landed blocks sometimes stopped animating
└─ active-only animation made the stack feel visually dead
```

Concrete symptoms reported:
- cyan and purple looking bad in preview at times
- cyan disappearing in live gameplay
- independent-eye cases behaving differently than intended
- blocks not animating unless actively falling

Non-negotiable requirement:
- cyan (`I`) keeps independent eye timing
- purple (`T`) keeps independent blink/eye timing
- these overlays must never be stretched by the ripple layer

### D. Ripple / Border Effect Problems

This is the main visual ambition, but it has also been the area with the most false starts.

```text
Observed failures
├─ ripple has looked detached from the body
├─ ripple has moved in chunks instead of waves
├─ ripple has stretched facial details / eyes
├─ ripple colors have not always matched the real sprite body colors
├─ ripple has not been noticeable enough in some builds
└─ ripple has not looked fluid enough to feel alive
```

Concrete symptoms reported:
- “little squares being lifted”
- “looks like one whole bar gets moved up and then moved down”
- generic rather than exact sprite-matched colors
- detached underlay
- not enough visible constant life around the border

Root cause:
- several attempts have been based on cell edges or coarse segments rather than a stable
  silhouette-underlay system

Correct target model:

```text
Final ripple model
╔══════════════════════════════════════════════════════════════════════╗
║ top layer     = eye overlays (cyan/purple special cases)           ║
║ middle layer  = untouched monster body sprite                      ║
║ bottom layer  = solid-color silhouette underlay                    ║
║                with dense anchor points around the border          ║
║                and sequential wave-like deformation between them   ║
╚══════════════════════════════════════════════════════════════════════╝
```

Rules:
- the body art never gets dragged
- the eye overlays never get dragged
- the ripple uses the actual sampled body color from each sprite
- every displacement returns to its original rest position

### E. UI / Layout / Resize Problems

These are the issues where the game shell moves around, misaligns, or feels badly composed.

```text
Observed failures
├─ left in-game UI panel feels ugly or awkward
├─ next queue is spaced badly or hugs itself strangely
├─ text and UI drift when resizing / minimizing / maximizing
├─ home name box can misalign when window state changes
├─ the bottom of the game can end up too close to the taskbar
└─ home and game artboards do not always feel rigidly locked
```

Concrete symptoms reported:
- in-game left UI looks messy
- next list weirdly organized
- text moves around when making the screen bigger/smaller
- minimize/maximize causing misalignment

Likely root causes:
- some world-space and overlay-space layout logic are still not fully unified
- viewport fitting is not yet treated as a strict artboard system everywhere
- some UI positioning may still be recalculated from unstable window-relative values

Non-negotiable requirement:
- the whole composition must behave like one fixed artboard that scales cleanly

### F. Controls / Controller / Settings Parity Regressions

These are the issues where the game stopped feeling like the working MonStacka version in terms of interaction.

```text
Observed failures
├─ original keyboard mapping parity is not fully restored
├─ original Xbox/controller parity is not fully restored
├─ controller navigation/settings/remap flow is missing or partial
├─ control discoverability is poor in the current Unity build
└─ settings/controls parity with v1 is incomplete
```

Concrete symptoms reported:
- user cannot tell what the controls are
- controller mapping work from earlier is not reliably present
- the game lost interaction parity it used to have

Non-negotiable requirement:
- Unity must inherit the control model from v1, not invent a new one

### G. Performance / Elegance Problems

These are the issues that make the game feel rough even when a given frame looks better.

```text
Observed failures
├─ laggy feeling during active play
├─ lag when blocks appear
├─ lag navigating certain interactions
├─ repeated destroy/recreate behavior in UI/view paths
└─ too much runtime churn for visual experimentation
```

Likely causes:
- unnecessary rebuilds of board/preview/hud content
- too much per-frame creation/destruction
- visual systems running on unstable or overly broad update paths

Desired outcome:
- maintain elegant, persistent view objects
- only update the pieces and UI regions that truly changed

## Elegant Solution Strategy

This is the actual fix strategy that avoids more loops.

```text
Recovery program
╔══════════════════════════════════════════════════════════════════════╗
║ R1. Lock the behavioral reference                                  ║
║ R2. Restore gameplay correctness in Unity                          ║
║ R3. Restore controls / controller / settings parity                ║
║ R4. Restore all game modes and run-state transitions               ║
║ R5. Unify live board and preview body rendering                    ║
║ R6. Restore cyan/purple special animation cases safely             ║
║ R7. Stabilize HUD / next queue / hold / resize artboard behavior   ║
║ R8. Reduce churn and input lag                                     ║
║ R9. Rebuild ripple using the silhouette-underlay model             ║
║ R10. Reintroduce ripple in phases                                  ║
║ R11. Run a full live audit and record remaining defects            ║
╚══════════════════════════════════════════════════════════════════════╝
```

### R1. Lock the behavioral reference

Use the working v1 Tauri/web implementation as the source of truth for:
- gameplay
- controls
- controller
- settings/remapping
- modes
- UI flow

### R2. Restore gameplay correctness in Unity

Do not touch ripple here.

Success means:
- no overlap bugs
- no fall-through
- no impossible gaps from the gameplay layer
- correct spawn, move, rotate, lock, clear behavior

### R3. Restore controls parity

Bring back the exact keyboard/mouse/Xbox behavior from v1:
- gameplay controls
- pause/restart/home/settings flows
- controller navigation
- controller remapping

### R4. Restore mode parity

All intended modes must behave as before:
- O.G.B.M.
- X(4)-LINES
- You... Suck?

### R5. Unify live board and preview body rendering

This is the key board-sprite repair:
- gameplay and preview must derive from the same body assembly truth
- if preview is clean and gameplay is broken, the architecture is still wrong

### R6. Restore special animation cases

Keep:
- cyan independent eye timing
- purple independent eye timing

Ensure:
- they work in both preview and gameplay
- they stay above the ripple layer

### R7. Stabilize HUD / layout / resize

The whole app should act like a single scaling artboard.

Success means:
- text does not drift
- next queue spacing is stable
- left panel stays composed
- minimize/maximize does not break alignment

### R8. Reduce performance churn

Before final ripple polish:
- persistent view objects
- less destroy/recreate work
- tighter update scope
- improved responsiveness

### R9. Rebuild ripple with the correct model

Correct implementation:

```text
Ripple implementation
1. trace final piece silhouette
2. place dense anchor points around the border
3. keep anchor rest positions stable
4. deform the contour between anchors slightly
5. phase-offset motion so it travels as a wave
6. render as solid-color underlay sampled from the true sprite body
7. always spring back to original border shape
```

### R10. Reintroduce ripple in phases

```text
Ripple rollout
├─ Phase A: preview/home only
├─ Phase B: active falling piece only
├─ Phase C: landed pieces with subtle constant life
└─ Phase D: stronger contact / impact emphasis
```

### R11. Audit everything again

Audit categories:
- gameplay bugs
- live board sprite bugs
- preview bugs
- ripple quality bugs
- UI/layout bugs
- control/controller issues
- performance issues

## Decision Rules Going Forward

To stop the loop, these rules should govern every future change:

```text
Decision rules
├─ Never polish ripple while gameplay parity is broken
├─ Never accept preview-only success if gameplay still differs
├─ Never accept “mostly fixed” resize behavior
├─ Never change control behavior without checking against v1
├─ Never let the ripple touch the body sprite or eye overlays
└─ Audit after every recovery phase, not just after the end
```

## Current Priority Order

If work resumes immediately, the next order should be:

1. restore gameplay correctness
2. restore controls/controller/settings parity
3. restore all game modes
4. unify gameplay and preview body rendering
5. stabilize HUD/next/resize behavior
6. optimize responsiveness
7. finish the new ripple implementation

