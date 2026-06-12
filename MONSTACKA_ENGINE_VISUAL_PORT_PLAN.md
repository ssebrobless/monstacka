# MonStacka Engine Visual Port Plan

```text
Port strategy
+====================================================================+
| MonStacka v1 (current Tauri/web build)                             |
| +-- keep as gameplay/UI reference                                  |
| +-- finish final signoff items                                     |
| +-- freeze rules, flow, and content                                |
+====================================================================+
| MonStacka v2 (engine build)                                        |
| +-- keep strict Tetris grid gameplay                               |
| +-- rebuild visuals in Unity                                       |
| +-- add visual-only soft-body / seam-reaction systems              |
| +-- aim for the fleshy, reactive feel the current stack fights     |
+====================================================================+
```

## Purpose

This document is the plan for moving MonStacka from its current Tauri/web implementation into a Unity-based visual version once the remaining v1 signoff items are closed.

The reason for the engine move is not gameplay -- gameplay and UI flow are stable and act as the reference. The reason is visual ambition:

- block bodies should feel soft, alive, and reactive
- touching seams between stacked blocks should look like contact, not overlap
- the outer contour should pulse in a fluid, believable way
- localized facial animation should feel authored, not fake or glitched

## Product Intent

```text
Non-negotiable rules
+-- Tetris gameplay remains exact and grid-based
+-- engine physics must never decide board occupancy
+-- visual deformation is a presentation layer only
+-- MonStacka art direction stays creature-first and hand-drawn
+-- v1 remains the behavioral reference for v2
```

MonStacka v2 should feel like the same game, same modes, same menu logic, same controls/settings -- but with a much stronger animation and rendering stack.

## Engine Decision: Unity

Unity is the engine for this port. The recommendation is firm, not provisional, because the core visual target (border deformation, seam-coupled contact, layered sprite animation) maps directly onto Unity's existing 2D tooling.

### Why Unity fits this project

| Capability needed | Unity tooling |
|---|---|
| Per-vertex outline deformation | **Sprite Shape** or runtime mesh generation on `SpriteRenderer` quads |
| Authored border/seam shaders | **Shader Graph** (URP) -- visual node editor, no hand-written HLSL required for the first pass |
| Layered sprite parts (eyes, tongue) | **2D Animation** package with sprite rigging, or simple child-`SpriteRenderer` overlays |
| Frame-based body animation (1-2-3-2-1) | **Animator** with `AnimationClip` per piece, trivial state machine |
| Contact-response visual system | C# `MonoBehaviour` reading board state each frame; no physics engine involved |
| Build targets | Windows, macOS, WebGL (if web build is ever wanted again) |

### When to reconsider Godot

Only if one of these becomes true during the vertical slice:

- Unity's licensing terms change in a way that blocks distribution
- Sprite Shape proves too rigid for the border deformation and a custom mesh path in Godot's `MeshInstance2D` turns out simpler
- The project needs to ship on a platform Unity cannot target

Unless one of those triggers fires, do not split effort evaluating Godot. Pick Unity, commit, and iterate.

## What Must Be Finished in v1 Before Porting

```text
v1 freeze checklist
+-- gameplay/UI behavior confirmed final
+-- controller defaults / remapping / settings flow confirmed final
+-- release-facing content locked:
|   +-- mode names
|   +-- settings labels
|   +-- leaderboard behavior
|   +-- nickname entry rules
|   +-- audio toggle behavior
+-- reference capture complete:
    +-- screenshots of every screen state
    +-- short gameplay clips (each mode, menu, settings)
    +-- written behavior spec (controls, mode flow, save/resume, leaderboard logic)
```

Deliverable: **MonStacka v1 Reference Package** -- a folder containing the captures, the behavior spec, and a frozen build.

## The Visual Goal: What "Squishy" Means

The desired effect has five layers, listed from most fundamental to most situational. Each layer builds on the one above it. Do not skip ahead -- a broken layer 2 will make layers 3-5 look wrong.

```text
Layer stack (build in this order)
+====================================================================+
| 1. Base body animation                                             |
|    frame loop: 1 -> 2 -> 3 -> 2 -> 1                              |
|    this is the idle heartbeat of every piece                       |
+--------------------------------------------------------------------+
| 2. Connected-body rendering                                        |
|    multi-cell pieces render as one organism, not four tiles         |
|    no neighboring sprite remnants, no detached flicker              |
+--------------------------------------------------------------------+
| 3. Local facial animation                                          |
|    eyes, tongue, blinks -- isolated overlays on the body           |
|    must feel authored; remove anything that looks synthetic         |
+--------------------------------------------------------------------+
| 4. Ambient border life                                             |
|    tiny constant outline twitch/throb on EXPOSED borders only      |
|    many small protrusions, not large breathing chunks              |
+--------------------------------------------------------------------+
| 5. Seam-coupled contact response                                   |
|    touching edges push/pull in opposite phase at shared seams      |
|    this is the most important visual target                        |
+--------------------------------------------------------------------+
| 6. Impact settle                                                   |
|    brief squash/rebound when a piece locks into the stack          |
|    strongest at contact edge, fades quickly                        |
+====================================================================+
```

### Ambient border effect -- specifics

The border should look like many tiny, rapid, soft protrusions along the outer contour. A constant fleshy throb. Mostly outline-only motion. Subtle enough to keep the body reading as one connected shape.

It should NOT look like:

- random glitching
- large chunks breathing in and out
- the piece body breaking apart into four tiles

Must be visible in the preview viewer, hold box, next queue, and live gameplay.

### Contact effect -- specifics

This is the single most important visual in v2.

```text
Seam behavior
+-- Exposed edge: ambient ripple only
+-- Touching edge:
    +-- seam divided into N small segments (suggest 4-8 per cell edge)
    +-- each segment has a shared displacement value
    +-- block A pushes out where block B pulls in
    +-- block B pushes out where block A pulls in
    +-- both settle back toward neutral after impact

Example:
  Green block edge  :  +   -   +   +   -
  Yellow block edge :  -   +   -   -   +

  + = slight outward pressure on that segment
  - = slight inward compression on that segment
```

The goal: stacked blocks look like they are pressing into each other, deforming slightly at the boundary. Not merely overlapping sprites.

## Technical Architecture

### Rendering model

```text
Gameplay layer (deterministic, never deforms)
+====================================+
| exact 10x20 board                  |
| exact SRS rotations                |
| exact piece occupancy              |
| exact lock timing                  |
| game state is the single source    |
| of truth for all visual layers     |
+====================================+

Visual layer (reads game state, never writes it)
+====================================+
| connected creature body            |
| + authored frame animation         |
| + isolated facial sub-parts        |
| + border deformation               |
| + seam-coupled contact motion      |
| + impact settle                    |
+====================================+
```

Rule: visual nodes deform; gameplay transforms do not. Board state is always deterministic.

### Scene structure

```text
AppRoot
+-- Boot
+-- HomeMenuScene
+-- GameScene
|   +-- BoardLogic          (pure C#, no MonoBehaviour rendering)
|   +-- BoardView           (reads BoardLogic, positions piece GameObjects)
|   +-- ActivePieceView
|   +-- StackView
|   +-- SeamContactSystem   (reads BoardLogic neighbor data, writes displacement)
|   +-- HUD
|   +-- PauseOverlay
+-- SettingsScene / Modal
+-- SharedAssets
```

### Core systems

| System | Responsibility | Reads | Writes |
|---|---|---|---|
| GridGameplay | Tetris rules, lock delay, line clear | Input | Board state |
| PieceSkin | Builds visual body from art parts | Sprite data, board state | SpriteRenderers |
| BorderDeform | Ambient outline motion on exposed edges | Silhouette mesh, neighbor map | Vertex offsets |
| SeamContact | Opposite-phase displacement on touching edges | Board neighbor map, time | Shared seam displacement buffer |
| ImpactSettle | Landing squash and rebound | Lock events | Impulse into BorderDeform |
| LocalPartAnim | Eyes, tongue, blink timing | Per-piece anim data | Child sprite transforms |
| UINavigation | Menus, settings, controller remap | Input | UI state |

### Border deformation implementation detail

The ambient border and seam contact effects both operate on the same underlying mechanism: **per-vertex displacement of the piece outline mesh**.

Recommended approach:

1. **Generate outline mesh at spawn.** When a piece enters play (or the stack updates), generate a mesh whose vertices trace the outer contour of the connected body. Use `Mesh` API in Unity -- create vertices along each exposed cell edge, subdivided into N segments (4-8 per cell edge).

2. **Tag each vertex.** Each vertex knows:
   - which cell edge it belongs to
   - whether that edge is exposed (no neighbor) or touching (has neighbor)
   - if touching, a reference to the neighbor piece's corresponding seam vertices

3. **Ambient pass (every frame).** For exposed-edge vertices, apply a small sine-based displacement perpendicular to the edge. Use per-vertex phase offsets (seeded from vertex index + piece ID) so the ripple looks organic, not uniform. Amplitude: ~0.5-1.5px at game resolution. Frequency: fast enough to read as alive, not breathing (~3-6 Hz base, with harmonics).

4. **Seam pass (every frame).** For touching-edge vertices, replace the ambient pass with the coupled displacement:
   - Compute a shared wave value per segment: `float wave = sin(time * freq + segmentPhase) * amplitude`
   - Block A vertex offset = `+wave * normal`
   - Block B vertex offset = `-wave * normal`
   - This produces the pressing-into-each-other look.

5. **Impact pass (on lock event).** When a piece locks, inject a one-shot impulse into the displacement of the newly touching seam vertices. This decays over ~150-300ms and blends back into the ambient/seam steady state.

6. **Render.** The outline mesh renders behind the sprite body (or as a masked overlay, depending on art direction). The sprite body itself does NOT deform -- only the border mesh moves. This keeps the interior visually stable.

Alternative if mesh generation proves too complex for the prototype: use a **Shader Graph material** on standard `SpriteRenderer` quads that displaces edge pixels in the fragment shader based on a UV-space noise texture. This is faster to prototype but harder to get the seam-coupling right. Start with the mesh approach; fall back to shader if vertex generation has major issues.

## Port Phases

Five phases. Each has a clear gate before the next one starts.

```text
Phase flow
1. Freeze v1 reference          -- gate: reference package exists
2. Vertical slice (proof)       -- gate: squishy contact visible on 2-3 pieces
3. Full gameplay port            -- gate: all modes playable, behavior matches v1
4. Menu and UI port              -- gate: full navigation parity with v1
5. Visual polish and release     -- gate: motion looks intentional and cohesive
```

### Phase 1: Freeze v1 reference

Tasks:
- Finalize remaining v1 signoff items
- Capture screenshots for: home menu, O.G.B.M., X(4)-LINES, You... Suck?, settings, controls, record modal
- Capture short gameplay clips for each mode

---

## Appendix B: Unity Recovery And Ripple Reimplementation Plan

This appendix supersedes any ad-hoc "tune it while broken" work. The Unity build is now in a **recovery-first** stage.

```text
Recovery logic
╔════════════════════════════════════════════════════════════════════╗
║ First:   restore functional parity with the working MonStacka v1 ║
║ Then:    restore clean sprite parity on the live board           ║
║ Then:    restore layout/resize/controller parity                 ║
║ Then:    reintroduce the new ripple system on top of that        ║
╚════════════════════════════════════════════════════════════════════╝
```

### B.1 Source Of Truth

For this recovery, the Unity port must match the working MonStacka v1 implementation in these areas before additional polish continues:

- gameplay rules
- spawn, gravity, collision, locking, line clear behavior
- O.G.B.M., X(4)-LINES, and You... Suck? mode behavior
- keyboard and mouse behavior
- Xbox controller gameplay controls
- Xbox controller UI navigation
- settings, controls, and remap behavior
- clean tetromino sprite presentation

Primary reference files:

- `enhanced/src/constants.ts`
- `enhanced/src/main.ts`
- `enhanced/src/storage.ts`
- `enhanced/src/input/keyboard.ts`
- `enhanced/src/input/bindings.ts`
- `enhanced/src/engine/board.ts`
- `enhanced/src/engine/pieces.ts`
- `enhanced/src/engine/gravity.ts`
- `enhanced/src/engine/bag.ts`
- `enhanced/src/monsterSkin.ts`
- `enhanced/src/ui/monsterDom.ts`

### B.2 Non-Negotiable Preservation Rules

```text
Preserve at all times
╔════════════════════════════════════════════════════════════════════╗
║ gameplay logic stays exact and deterministic                     ║
║ cyan (I) eyes keep independent motion timing                     ║
║ purple (T) eyes keep independent blink timing                    ║
║ ripple never removes cells or hides body art                     ║
║ ripple renders under the body sprite, not through it             ║
║ ripple never stretches eye overlays                              ║
║ preview and gameplay must use the same body-art truth            ║
╚════════════════════════════════════════════════════════════════════╝
```

If any change threatens one of those rules, the change must be rolled back or isolated behind a temporary debug flag.

### B.3 Execution Order

```text
R0  Freeze unsafe board-only visual hacks
 ▼
R1  Restore gameplay and control parity
 ▼
R2  Restore board sprite/body parity
 ▼
R3  Restore UI/layout/resize parity
 ▼
R4  Rebuild ripple as a silhouette underlay
 ▼
R5  Re-enable ripple in stages
 ▼
R6  Full regression audit
```

### B.4 Phase R0: Freeze Unsafe Visual Paths

Objective:
- stop introducing new gameplay regressions while repairing parity

Required action:
- keep the ripple system in the codebase
- if needed, disable only the unsafe gameplay ripple path temporarily
- keep home/preview behavior available as the visual reference if it remains stable

Do not do:
- do not delete the ripple implementation wholesale
- do not revert cyan/purple eyes to shared timing

Gate:
- the build can enter and exit a game without creating new runtime errors

### B.5 Phase R1: Restore Gameplay And Control Parity

Objective:
- make Unity behave like the working MonStacka v1 game again before further visual work

Tasks:

1. Restore exact keyboard gameplay controls from v1.
2. Restore exact mouse/menu behavior from v1.
3. Restore Xbox gameplay mapping from v1 parity.
4. Restore Xbox UI navigation and settings navigation parity.
5. Restore controller remapping parity.
6. Restore all three game modes:
   - O.G.B.M.
   - X(4)-LINES
   - You... Suck?
7. Fix board behavior regressions:
   - no overlap
   - no fall-through
   - no disappearing through floor
   - no invalid stack gaps caused by bad board state
   - proper hold/next behavior
   - proper queue updates
   - proper line clear and lock behavior

Implementation rule:
- all gameplay fixes happen in the gameplay path first; no visual workaround may "hide" gameplay bugs

Gate:

```text
Gameplay parity gate
├─ keyboard works
├─ controller works
├─ controller remap works
├─ all 3 modes start and play
├─ no piece overlap
├─ no piece fall-through
└─ no bad lock / line-clear behavior
```

### B.6 Phase R2: Restore Board Sprite And Animation Parity

Objective:
- make live gameplay sprites as clean and reliable as the preview path

Tasks:

1. Unify the body render path so gameplay and preview share the same source art truth.
2. Remove any gameplay-only crop/rotation shortcut that causes:
   - missing pink segments
   - missing orange segments
   - invisible cyan
   - accidental blank gaps
3. Keep the body sprite rendered as one connected organism, never as visibly separate cells.
4. Restore body frame animation in gameplay for all pieces.
5. Preserve the special animation cases:
   - cyan eyes remain independently timed
   - purple eyes remain independently timed
6. Ensure eye overlays sit above the body and above any ripple underlay.

Implementation rule:
- preview may use different scale, but not a different art-truth or different crop logic

Gate:

```text
Sprite parity gate
├─ pink intact in gameplay
├─ orange intact in gameplay
├─ cyan visible in gameplay
├─ preview and gameplay agree on body shape
├─ body frames animate in gameplay
└─ cyan/purple special eye timing still works
```

### B.7 Phase R3: Restore UI And Resize Parity

Objective:
- make the game shell feel stable and readable at all window sizes

Tasks:

1. Keep the entire game on one shared artboard/viewport.
2. Prevent minimize/maximize/resize from shifting:
   - left text blocks
   - hold box
   - next queue
   - top-right controls
   - names in the home name box
3. Clean up the left in-game panel:
   - tighter next queue
   - readable spacing
   - no awkward gaps
   - no text drift
4. Keep the full game visible above the Windows taskbar.

Gate:

```text
Layout parity gate
├─ resize does not misalign left panel
├─ resize does not misalign names/home box
├─ next queue spacing is stable
├─ taskbar does not cover content
└─ maximize/restore preserves layout
```

### B.8 Phase R4: New Ripple Strategy

The old chunk-based ripple approach is not the target anymore.

The new strategy is:

```text
body sprite
└─ unchanged, clean, fully visible

eye overlays
└─ unchanged, independent, above everything

ripple underlay
└─ separate silhouette-border layer beneath the body
   ├─ fixed anchor points around the outer contour
   ├─ many smaller segments between anchors
   ├─ local pulls between anchors
   ├─ traveling wave timing
   └─ spring return to exact rest shape
```

#### B.8.1 Outline model

1. Build the final visible silhouette from the actual sprite alpha.
2. Resample the silhouette into evenly spaced anchor points around the contour.
3. Keep anchor rest positions fixed.
4. Create smaller interpolated border segments between anchors.
5. Move those in-between segments slightly outward/inward.
6. Always spring back to the original rest outline.

Important:
- the main body sprite does not move
- only the ripple underlay contour moves

#### B.8.2 Color model

Do not use generic palette approximations.

Use this color pipeline instead:

```text
Per-piece ripple color
├─ sample real non-transparent body pixels
├─ ignore eye / mouth / teeth / outline accent colors
├─ derive dominant body color from the actual sprite art
└─ cache that exact color per piece
```

That means the ripple underlay for each piece must visually match the real sprite body color from the user's art, not a guessed "close enough" color.

#### B.8.3 Motion model

The ripple should read like a traveling fleshy wave, not block chunks poking out.

Required behavior:

- many small segments
- close spacing
- sequential activation around the border
- neighboring segments influence each other
- no whole-edge bar popping at once
- stronger on active/contact states
- subtle but still visible on landed pieces

Recommended implementation:

```text
segment i
  reads from:
  - its own phase
  - previous neighbor
  - next neighbor
  - current contact impulse

wave travel
  phase(i) = basePhase + i * segmentOffset
```

Motion states:

- `idle landed`: subtle constant life
- `active falling`: more visible border life
- `impact`: short stronger pulse
- `contact seam`: future enhancement after parity recovery

#### B.8.4 Layering rule

```text
Top
├─ eye overlays
├─ main body sprite
└─ ripple underlay
Bottom
```

The underlay must never cover or stretch the body sprite or eyes.

### B.9 Phase R5: Ripple Reintroduction Stages

Do not turn the full ripple back on everywhere at once.

```text
Stage A
├─ home active preview only
└─ verify color, attachment, and wave feel

Stage B
├─ gameplay active piece only
└─ verify no gameplay sprite corruption

Stage C
├─ landed pieces subtle life
└─ verify stack remains readable

Stage D
├─ impact emphasis
└─ verify no chunking or detachment
```

Only move to the next stage if the current one passes.

### B.10 Phase R6: Full Audit

Final audit checklist:

```text
Gameplay
├─ no overlap
├─ no fall-through
├─ all 3 modes work
├─ keyboard works
├─ controller works
└─ controller remap works

Sprites
├─ preview clean
├─ gameplay clean
├─ pink intact
├─ orange intact
├─ cyan visible
├─ cyan eyes independent
└─ purple eyes independent

Layout
├─ home stable
├─ game stable
├─ resize stable
└─ taskbar-safe

Ripple
├─ correct color from real art
├─ underlay only
├─ wave-like, not chunky
├─ visible in gameplay
└─ returns to rest cleanly
```

### B.11 Packaging Rule

Do not package or publish the Unity build as the main downloadable game until:

1. phases R1 through R6 pass
2. the Unity build matches MonStacka v1 behaviorally
3. the ripple is present without breaking gameplay or body sprites

---

## Appendix C: Current Unity Recovery Audit

This section records the **current actual state** of the Unity port so the recovery plan is scoped against real failures, not guesses.

### C.1 Current State Summary

```text
Current Unity state
╔════════════════════════════════════════════════════════════════════╗
║ Home preview path                  partially healthy              ║
║ Live gameplay board                not parity-safe               ║
║ Gameplay rules/runtime behavior    not parity-safe               ║
║ Keyboard/controller/settings       not parity-complete           ║
║ Resize/layout behavior             partially improved, not fixed ║
║ Ripple system                      promising idea, unsafe base   ║
║ Verification/build tooling         too weak for runtime parity   ║
╚════════════════════════════════════════════════════════════════════╝
```

### C.2 Confirmed User-Facing Failures

These are currently observed, repeated failures in the Unity build:

#### Gameplay correctness failures

- pieces overlap visually and/or logically in ways that do not match Tetris rules
- pieces have fallen through the bottom of the board
- stack gaps/invisible missing regions appear after landing
- some modes are not behaving like the v1 reference
- controls feel laggy or incomplete

#### Board sprite failures

- gameplay board pieces can lose visible cells
- cyan can disappear entirely in gameplay
- pink and orange have shown missing segments in gameplay
- preview can look correct while the board still looks broken
- body animation and special eye behavior are inconsistent between contexts

#### UI/layout failures

- in-game left panel is awkward and unstable
- next queue spacing is poor or uneven
- text/panels drift on resize, maximize, or minimize
- name box alignment can shift
- taskbar/window handling has improved but not fully stabilized

#### Input/system parity failures

- original keyboard mapping is not fully restored
- Xbox gameplay controls are not fully restored
- Xbox UI navigation parity is not fully restored
- controller remapping parity is not fully restored
- settings/state parity from v1 is incomplete

#### Ripple failures

- earlier versions distorted body art or eye overlays
- ripple color did not consistently match the actual sprite art
- ripple often looked detached from the sprite
- motion read as chunks/bars instead of traveling fleshy waves

### C.3 Confirmed Technical Scope From The Current Codebase

The current source confirms several root-cause-level problems.

#### 1. Gameplay render path and preview render path are still split

In the current Unity code:

- preview uses a cropped/composited body path
- gameplay can use a different full-box/rotated render path when it thinks a piece matches a full definition

Files:
- `MonStacka-v2/Assets/MonStacka/Scripts/Visual/ConnectedBodyBuilder.cs`
- `MonStacka-v2/Assets/MonStacka/Scripts/Visual/PieceSkin.cs`

This directly explains why:
- previews can look good
- while the live board still loses cells, shows gaps, or hides cyan

Recovery implication:
- the board and preview must be unified onto one trusted body-render truth

#### 2. Controller and input parity are not ported yet

The current Unity gameplay input path is keyboard-only in practice.

Observed in code:
- `GameManager.cs` reads keyboard keys directly
- `MonStackaAppState.cs` currently stores only mode, gravity, lock delay, music, and SFX flags
- no full Unity-side equivalent yet exists for the v1 controller remap/navigation system

Files:
- `MonStacka-v2/Assets/MonStacka/Scripts/Core/GameManager.cs`
- `MonStacka-v2/Assets/MonStacka/Scripts/Core/MonStackaAppState.cs`

Recovery implication:
- input/controller parity is not a minor bug fix; it is an unported feature set

#### 3. UI/layout parity is only partially solved

`ArtboardViewportController.cs` improves camera/canvas viewport consistency, but it does not by itself guarantee that every world object, text object, and UI panel shares the exact same stable anchoring behavior.

Files:
- `MonStacka-v2/Assets/MonStacka/Scripts/UI/ArtboardViewportController.cs`
- `MonStacka-v2/Assets/MonStacka/Scripts/UI/HUDController.cs`
- `MonStacka-v2/Assets/MonStacka/Scripts/UI/HomeMenuController.cs`
- `MonStacka-v2/Assets/MonStacka/Scripts/UI/NextQueueView.cs`

Recovery implication:
- layout parity needs an explicit anchoring and artboard-root pass, not just viewport math

#### 4. Current preview/queue rebuild patterns contribute to instability and lag

The current code still does too much destroy/recreate work in presentation paths:

- `RebuildActivePieceView()` destroys and recreates the active piece object
- `RebuildLockedPieceViews()` destroys and recreates all locked views
- `NextQueueView.Render()` destroys and recreates every next preview object
- `HomeMenuController.RebuildPreview()` destroys and recreates all three previews

Files:
- `MonStacka-v2/Assets/MonStacka/Scripts/Core/GameManager.cs`
- `MonStacka-v2/Assets/MonStacka/Scripts/UI/NextQueueView.cs`
- `MonStacka-v2/Assets/MonStacka/Scripts/UI/HomeMenuController.cs`

Recovery implication:
- parity recovery must include a render-lifecycle cleanup pass, not only visual tuning

#### 5. Current verification is too shallow

The Unity verification script currently proves:
- basic board-state logic
- asset presence
- scene wiring

It does **not** prove:
- that the built player launches cleanly every time
- that board pieces render intact in gameplay
- that resize/maximize/minimize preserve layout
- that controller/UI navigation works
- that gameplay modes behave like v1

Files:
- `MonStacka-v2/Assets/Editor/MonStackaV2Verification.cs`
- `MonStacka-v2/Assets/Editor/MonStackaV2Build.cs`

Recovery implication:
- the audit harness itself must be upgraded as part of the work

### C.4 Refined Execution Scope

Based on the current state, the plan is refined as follows:

```text
Priority 1  Restore gameplay/input parity
Priority 2  Unify live board and preview sprite rendering
Priority 3  Stabilize layout and resize behavior
Priority 4  Upgrade verification/build confidence
Priority 5  Reintroduce ripple safely on the stabilized board
```

This means:

- no more gameplay ripple tuning before priorities 1 through 4 are under control
- no more preview-only success criteria if the board is still broken
- no packaging/publishing of Unity until the runtime audit passes

### C.5 Concrete Fix Buckets

#### Bucket A: Gameplay and controls parity

Deliverables:
- port v1 keyboard mapping
- port v1 Xbox gameplay mapping
- port v1 Xbox UI navigation
- port controller remap/settings behavior
- restore mode behavior parity
- eliminate overlap/fall-through/lock bugs

#### Bucket B: Board sprite parity

Deliverables:
- one trusted body-render source for preview and gameplay
- no missing segments on pink/orange
- cyan visible on the board
- no hidden blank cells/gaps from bad assembly
- body animation active in gameplay
- cyan/purple special eye timing preserved

#### Bucket C: UI/layout parity

Deliverables:
- stable left panel
- stable next queue spacing
- no text drift on resize/maximize/minimize
- home name box remains aligned
- game content remains visible above taskbar

#### Bucket D: Ripple reimplementation

Deliverables:
- silhouette-based underlay ripple
- exact color derived from the real sprite body
- no body/eye distortion
- traveling wave feel, not bar/chunk motion
- active piece and landed piece states tuned separately

#### Bucket E: Audit/tooling

Deliverables:
- build integrity check verifies `_Data` completeness
- runtime launch check after build
- gameplay parity checklist
- resize/layout checklist
- sprite parity checklist
- controller parity checklist

### C.6 Done Criteria For "High-Quality Functional State"

The Unity version is not considered recovered until all of the following are true:

```text
Recovered state
├─ game modes behave like v1
├─ controls/controller parity restored
├─ board plays like proper Tetris
├─ preview and gameplay sprites both look clean
├─ cyan/purple special animation cases preserved
├─ resize/minimize/maximize do not misalign UI
├─ performance feels responsive
└─ ripple reads as alive without breaking the body art
```

### C.7 Live Audit Update (2026-04-02)

This audit reflects the current Windows player after the latest recovery pass.

```text
Live audit snapshot
╔════════════════════════════════════════════════════════════════════╗
║ Home menu preview path      mostly healthy                        ║
║ Home controls/help text     now driven from shared control defs   ║
║ Gameplay input path         healthier, still not parity-complete  ║
║ Gameplay board render       improved, still needs more repair     ║
║ Gameplay ripple             still intentionally minimized         ║
║ Resize/layout               improved, not signed off yet          ║
║ Controller/settings parity  partial only                          ║
╚════════════════════════════════════════════════════════════════════╝
```

Observed from the latest live captures:

- home/menu preview sprites are currently the healthiest part of the Unity build
- the shared control mapping/source-of-truth pass is in progress:
  - gameplay
  - home menu
  - pause/settings help text
- the live gameplay board is materially healthier than earlier catastrophic states
- however, Unity is still not parity-safe in gameplay because:
  - live board pieces still need deeper correctness verification
  - full controller/settings parity is not restored yet
  - resize/maximize/minimize still need explicit live signoff
- gameplay ripple remains suppressed/minimal on purpose until the board is stable enough to support reintroduction safely

Implication:

- continue executing D3 and D4 first
- keep gameplay ripple minimized while D5-D9 restore stable behavior
- do not treat home-preview success as a proxy for gameplay parity

---

## Appendix D: Exact Implementation Order

This is the exact order of implementation to follow from this point forward.

```text
Execution order
╔════════════════════════════════════════════════════════════════════╗
║ D1  Freeze unstable board visuals                                ║
║ D2  Restore pure gameplay correctness                            ║
║ D3  Restore keyboard/controller/settings parity                  ║
║ D4  Restore mode parity                                          ║
║ D5  Unify gameplay/preview body rendering                        ║
║ D6  Restore special animation cases (cyan/purple)                ║
║ D7  Stabilize HUD/layout/resize                                  ║
║ D8  Reduce lag and rebuild churn                                 ║
║ D9  Upgrade verification and launch checks                       ║
║ D10 Rebuild ripple as silhouette underlay                        ║
║ D11 Reintroduce ripple in controlled stages                      ║
║ D12 Final regression audit                                       ║
╚════════════════════════════════════════════════════════════════════╝
```

### D1. Freeze unstable board visuals

Goal:
- stop the live board from getting worse while core fixes are underway

Actions:
- keep the ripple code in the project
- disable or minimize only the unsafe gameplay ripple path if it is corrupting the board
- keep preview/home ripple behavior as reference if it remains stable
- do not remove cyan/purple independent eye timing

Exit criteria:
- build launches
- no new board corruption is introduced by visual tuning during recovery

### D2. Restore pure gameplay correctness

Goal:
- get the Unity board back to behaving like proper Tetris before any more presentation work

Actions:
1. audit `BoardState.cs` against:
   - `enhanced/src/engine/board.ts`
   - `enhanced/src/engine/pieces.ts`
   - `enhanced/src/engine/gravity.ts`
   - `enhanced/src/engine/bag.ts`
2. fix collision, spawn, lock, clear, and hold behavior
3. fix the specific runtime regressions:
   - overlap
   - fall-through
   - bad stack gaps caused by logic/state mismatch
   - disappearing below the board
4. ensure ghost, hard drop, and grounded logic match v1 expectations

Exit criteria:
- no overlap
- no fall-through
- no invalid stacking behavior
- proper line clear and hold behavior

### D3. Restore keyboard/controller/settings parity

Goal:
- restore the original MonStacka input experience

Actions:
1. port keyboard gameplay controls from v1
2. port home/menu mouse behavior from v1
3. port Xbox gameplay controls from v1
4. port Xbox UI navigation from v1
5. port controller remapping/settings behavior from v1
6. restore settings data needed in Unity app state

Primary reference files:
- `enhanced/src/input/keyboard.ts`
- `enhanced/src/input/bindings.ts`
- `enhanced/src/constants.ts`
- `enhanced/src/storage.ts`
- `enhanced/src/main.ts`

Exit criteria:
- keyboard gameplay works
- controller gameplay works
- controller UI navigation works
- controller remap works
- settings inputs behave like v1

### D4. Restore mode parity

Goal:
- make all shipped game modes behave like the old working version

Actions:
1. verify and fix:
   - `O.G.B.M.`
   - `X(4)-LINES`
   - `You... Suck?`
2. ensure mode-specific goals, end conditions, and HUD text are correct
3. ensure restart/home/quit behavior matches v1 intent

Exit criteria:
- all 3 modes start, play, and finish correctly

### D5. Unify gameplay and preview body rendering

Goal:
- eliminate the split render truth that makes preview look good while gameplay breaks

Actions:
1. remove gameplay-only body shortcuts that differ from preview truth
2. choose one trusted connected-body assembly path for both preview and gameplay
3. fix missing-segment cases:
   - pink
   - orange
   - cyan invisible
4. eliminate blank/gap artifacts caused by incorrect rotated assembly

Primary files:
- `MonStacka-v2/Assets/MonStacka/Scripts/Visual/ConnectedBodyBuilder.cs`
- `MonStacka-v2/Assets/MonStacka/Scripts/Visual/PieceSkin.cs`

Exit criteria:
- preview and gameplay agree on full body shape
- cyan is visible in gameplay
- pink/orange no longer lose cells

### D6. Restore special animation cases cleanly

Goal:
- keep the monster personality details intact once the body path is stable

Actions:
1. restore body frame animation everywhere it should appear
2. preserve cyan eye independence
3. preserve purple eye independence
4. ensure those overlays never clip, drift, or distort
5. ensure gameplay and preview use the same animation truth where appropriate

Exit criteria:
- all pieces animate
- cyan eyes remain independently timed
- purple eyes remain independently timed

### D7. Stabilize HUD, layout, and resize

Goal:
- make the Unity shell feel like a stable game screen rather than a drifting prototype

Actions:
1. fix left panel alignment and spacing
2. fix next queue layout
3. fix text drift on resize/minimize/maximize
4. fix home name box alignment under resize
5. ensure content remains visible above the taskbar
6. confirm home and game scenes share the same artboard behavior

Primary files:
- `MonStacka-v2/Assets/MonStacka/Scripts/UI/ArtboardViewportController.cs`
- `MonStacka-v2/Assets/MonStacka/Scripts/UI/HUDController.cs`
- `MonStacka-v2/Assets/MonStacka/Scripts/UI/NextQueueView.cs`
- `MonStacka-v2/Assets/MonStacka/Scripts/UI/HomeMenuController.cs`

Exit criteria:
- resize does not misalign UI
- left panel is readable and stable
- name box remains aligned

### D8. Reduce lag and render churn

Goal:
- make the game feel responsive and elegant, not rebuild-heavy

Actions:
1. identify destroy/recreate hot spots in:
   - active piece updates
   - locked stack updates
   - next queue renders
   - home preview renders
2. replace unnecessary rebuilds with reuse/update paths where safe
3. ensure visual updates are incremental rather than full object recreation when possible

Exit criteria:
- input feels responsive
- runtime no longer stutters from unnecessary rebuilds

### D9. Upgrade verification and launch checks

Goal:
- stop shipping builds that "verify complete" but still fail at runtime

Actions:
1. strengthen `MonStackaV2Verification.cs` to assert:
   - gameplay parity basics
   - sprite parity basics
   - scene wiring for input/UI systems
2. add build integrity checks:
   - executable exists
   - `_Data` folder is complete
3. add post-build launch smoke check
4. fail verification if runtime-critical assets or systems are missing

Exit criteria:
- build artifacts are complete
- verification catches runtime-blocking issues before launch

### D10. Rebuild ripple as silhouette underlay

Goal:
- replace chunky cell-edge motion with the new anchor/segment wave approach

Actions:
1. derive ripple from the final sprite silhouette, not the cell perimeter
2. create fixed rest anchors around the contour
3. create smaller segments between anchors
4. move only the in-between border contour
5. spring all deformed segments back to rest
6. keep the underlay beneath the body sprite
7. keep eyes above both body and ripple
8. derive ripple color from the real sprite art body color

Exit criteria:
- no body distortion
- no eye distortion
- exact or near-exact body-color match from sprite art

### D11. Reintroduce ripple in controlled stages

Goal:
- add the new ripple back without rebreaking the game

Actions:
1. stage A: home active preview only
2. stage B: gameplay active falling piece only
3. stage C: subtle landed-piece constant life
4. stage D: impact/contact emphasis

Rules:
- do not move to the next stage until the current one is clean
- if gameplay breaks, roll back to the last stable stage

Exit criteria:
- ripple is visible, attached, wave-like, and safe

### D12. Final regression audit

Goal:
- prove the game is back to a high-quality functional state

Checklist:
1. gameplay
2. controls and controller parity
3. mode parity
4. preview/gameplay sprite parity
5. cyan/purple special animation preservation
6. resize/minimize/maximize stability
7. performance feel
8. ripple quality

Ship gate:
- Unity is not package-ready until every section above passes
- Write behavior spec: controls, controller navigation, mode flow, save/resume rules, leaderboard logic, nickname entry rules

**Gate:** v1 reference package folder exists with all captures and spec.

### Phase 2: Vertical slice

This is the critical proof-of-concept. Scope is deliberately small.

Scope:
- One board, one playable mode (O.G.B.M.)
- 2-3 monster pieces (suggest I, T, and one asymmetric like S or L)
- Hold box, next queue, preview panel
- Pause overlay (minimal)

Build order within this phase (matches the visual layer stack):
1. Import sprite sheets, define crop bounds and piece-skin data
2. Connected-body rendering -- pieces display correctly as one organism
3. Base frame animation (1-2-3-2-1 loop)
4. Local facial animation on the 2-3 prototype pieces
5. Ambient border deformation on exposed edges
6. Seam-coupled contact response on touching edges
7. Landing impact settle

**Gate:** The vertical slice must demonstrate that stacked blocks visibly press into each other and that border motion reads as fleshy, not glitchy. If it does not, stop and reassess the rendering approach before proceeding to Phase 3.

### Phase 3: Full gameplay port

Tasks:
- Add all seven pieces with full skin data
- Port complete Tetris gameplay logic (rotations, wall kicks, lock delay, gravity, line clear)
- Port O.G.B.M., X(4)-LINES, You... Suck?
- Port leaderboards, save/resume, settings, controller remapping

**Gate:** All modes playable with no behavior regressions against v1 reference.

### Phase 4: Menu and UI port

Tasks:
- Rebuild home screen art layout
- Rebuild lore bubble behavior
- Rebuild settings UI
- Rebuild controls remap UI
- Rebuild nickname-entry modal

**Gate:** Full navigation parity with v1. Controller navigation remains strong.

### Phase 5: Visual polish and release

Tasks:
- Tune border amplitude, frequency, and harmonic mix
- Tune seam response strength and decay timing
- Tune landing settle timing and amplitude
- Tune eye/tongue/local animation timing
- Verify stylistic consistency across preview, hold, next, and gameplay
- Final build packaging

**Gate:** Visual motion looks intentional and cohesive. Touching blocks feel physically reactive. Nothing reads as broken sprite overlap.

## Risks and Mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| Outline mesh generation is too slow for 200+ stacked cells | Seam effect stutters or is cut | Profile early in Phase 2. Batch outline meshes per row or use GPU compute if needed. Worst case: shader-only fallback. |
| Seam coupling looks mechanical (uniform sine) | Effect reads as programmatic, not organic | Layer 2-3 sine harmonics with per-segment phase jitter. Tune in Phase 5 but prototype the jitter in Phase 2. |
| Connected-body rendering has edge artifacts at cell boundaries | Pieces look segmented | This was already a problem in v1. Solve it first in Phase 2 step 2 before adding deformation. |
| Unity 2D Animation package adds unwanted complexity | Over-engineered rig for simple overlay parts | Start with plain child SpriteRenderers for facial parts. Only adopt 2D Animation if overlay approach proves insufficient. |
| Scope creep into new gameplay features during port | Port stalls, v2 never ships | Hard rule: v2 adds zero gameplay features. Visual layer only. Gameplay changes go back to v1 first, then propagate. |
| v1 reference is incomplete or ambiguous | v2 behavior drifts from intended design | Phase 1 gate must be strict. Do not start Phase 2 until the reference package is thorough enough to answer "how does v1 handle X?" for any X. |

## What Not To Do

```text
Avoid
+-- using rigidbody physics for Tetris rules
+-- letting deformation move actual board occupancy
+-- reintroducing fake eye overlays that drift off-model
+-- global wobble that makes bodies look segmented
+-- rewriting everything at once before the vertical slice proves out
+-- evaluating Godot in parallel (decision is made; revisit only on trigger)
+-- adding new gameplay features during the port
```

## Delivery Artifacts

```text
By phase
Phase 1: v1 reference package (captures, behavior spec, frozen build)
Phase 2: vertical slice build, tech spike notes, piece-skin data spec
Phase 3: full gameplay build, seam response tuning sheet
Phase 4: complete UI build, controller mapping sheet
Phase 5: release candidate, release checklist
```

## Immediate Next Steps

```text
Right now
+-- finish remaining v1 signoff
+-- freeze the current build as reference
+-- create Unity project (2D URP template)
+-- start vertical slice, not full port
```

### First five executable tasks

1. Finish the current MonStacka v1 signoff items
2. Capture final v1 reference screenshots and video
3. Create a new Unity 2D URP project for MonStacka v2
4. Import sprite sheets, define crop bounds, build piece-skin data for I, T, and S pieces
5. Prototype in order: connected-body render -> ambient border deformation -> seam-coupled contact response

---

## Appendix A: Phase 2 Implementation Spec (Vertical Slice)

This appendix is the build-it-now spec for Phase 2. It names every file, class, and data structure. Codex or any implementation agent should be able to read this section alone and produce a working vertical slice.

### A.1 Project setup

- Create a Unity 2D URP project named `MonStacka-v2`
- Unity version: 2022.3 LTS (or latest 2022.3.x)
- Template: 2D (URP)
- Packages to install via Package Manager:
  - `com.unity.2d.sprite` (included by default)
  - `com.unity.shadergraph` (included with URP)
  - No 2D Animation package yet -- use plain child SpriteRenderers for facial parts

### A.2 Folder structure

```text
Assets/
+-- MonStacka/
    +-- Scripts/
    |   +-- Core/
    |   |   +-- BoardState.cs
    |   |   +-- PieceDefinitions.cs
    |   |   +-- PieceBag.cs
    |   |   +-- GameManager.cs
    |   +-- Visual/
    |   |   +-- PieceSkin.cs
    |   |   +-- PieceSkinData.cs        (ScriptableObject)
    |   |   +-- ConnectedBodyBuilder.cs
    |   |   +-- OutlineMeshBuilder.cs
    |   |   +-- BorderDeformSystem.cs
    |   |   +-- SeamContactSystem.cs
    |   |   +-- ImpactSettleSystem.cs
    |   |   +-- FacialPartAnimator.cs
    |   +-- UI/
    |       +-- HUDController.cs
    |       +-- PauseOverlay.cs
    |       +-- HoldBoxView.cs
    |       +-- NextQueueView.cs
    +-- Art/
    |   +-- SpriteSheets/
    |   |   +-- monster-sheet.png         (import from v1)
    |   |   +-- monster-sheet-frame2.png  (import from v1)
    |   |   +-- monster-sheet-frame3.png  (import from v1)
    |   +-- Materials/
    |       +-- OutlineMeshMat.mat        (URP unlit, vertex-color enabled)
    +-- Data/
    |   +-- PieceSkins/                   (one ScriptableObject per piece)
    |   |   +-- Skin_I.asset
    |   |   +-- Skin_T.asset
    |   |   +-- Skin_S.asset
    |   +-- PieceDefinitions.asset        (ScriptableObject: all 7 piece rotation tables)
    +-- Scenes/
    |   +-- Boot.unity
    |   +-- Game.unity
    +-- Prefabs/
        +-- PieceView.prefab
        +-- CellView.prefab
        +-- OutlineMesh.prefab
```

### A.3 v1 source reference for porting

The v1 codebase lives at `repo_review/enhanced/src/`. Key files to port from:

| v1 file | What to extract | v2 destination |
|---|---|---|
| `constants.ts` | Piece DEFINITIONS (cell coords, 4 rotations), PIECE_COLORS, SRS kick tables | `PieceDefinitions.cs` |
| `engine/board.ts` | Board collision, placement, line clear | `BoardState.cs` |
| `engine/pieces.ts` | Piece rotation, wall kick validation, ghost piece | `BoardState.cs` |
| `engine/gravity.ts` | Gravity ticks, soft/hard drop | `GameManager.cs` |
| `engine/bag.ts` | 7-bag randomizer | `PieceBag.cs` |
| `monsterSkin.ts` | Per-piece crop bounds from sprite sheet, frame mapping | `PieceSkinData.cs` (ScriptableObject) |
| `ui/monsterDom.ts` | How connected body is assembled from crop regions | `ConnectedBodyBuilder.cs` |

### A.4 Core classes

#### BoardState.cs

Pure C# (no MonoBehaviour). Single source of truth for game state.

```text
public class BoardState
    int[,] grid                        // 10 wide x 20 tall (0 = empty, 1-7 = piece type)
    PieceInstance activePiece          // type, rotation, position
    PieceInstance? holdPiece
    Queue<PieceType> nextQueue         // 5+ lookahead from bag

    bool TryMove(int dx, int dy)
    bool TryRotate(int dir)            // SRS wall kicks
    bool LockPiece()                   // returns true, fires OnPieceLocked event
    int ClearLines()                   // returns count of lines cleared
    bool IsGameOver()

    event Action<PieceLockEvent> OnPieceLocked
    event Action<int> OnLinesCleared

    NeighborMap GetNeighborMap()        // for each occupied cell, which edges touch another piece
```

`NeighborMap` is the key data structure that the visual layer reads:

```text
public struct CellEdgeInfo
    bool topExposed, bottomExposed, leftExposed, rightExposed
    int topNeighborPieceId, bottomNeighborPieceId  // -1 if exposed
    int leftNeighborPieceId, rightNeighborPieceId

public class NeighborMap
    Dictionary<Vector2Int, CellEdgeInfo> cells
```

#### PieceDefinitions.cs

Port directly from v1 `constants.ts`. Static data:

```text
public static class PieceDefinitions
    // 7 pieces, 4 rotations each, cell offsets
    // I: [[0,0],[1,0],[2,0],[3,0]], ...
    // SRS kick tables (I-piece separate from JLSTZ)
    // Piece colors: I=cyan, O=yellow, T=purple, S=green, Z=red, J=blue, L=orange
```

#### PieceSkinData.cs (ScriptableObject)

One asset per piece. Defines how to build the visual from sprite sheet crops:

```text
[CreateAssetMenu(menuName = "MonStacka/PieceSkinData")]
public class PieceSkinData : ScriptableObject
    PieceType pieceType
    Sprite[] bodyFrames                // 3 frames (from the 3 sprite sheets)
    Rect cropBounds                    // pixel rect in the sprite sheet
    FacialPart[] facialParts           // optional eye/tongue overlays

public struct FacialPart
    string name                        // "leftEye", "rightEye", "tongue"
    Sprite[] frames                    // per-frame sprites for this part
    Vector2 localOffset                // offset from piece pivot
```

#### OutlineMeshBuilder.cs

Generates the deformable border mesh for a piece or stack region.

```text
public class OutlineMeshBuilder
    // Input: set of occupied cells + NeighborMap
    // Output: Mesh with vertices along exposed/touching edges

    Mesh BuildOutline(HashSet<Vector2Int> cells, NeighborMap neighbors, int subdivisions = 6)
        // 1. Walk the outer contour of the cell group
        // 2. For each cell edge on the contour:
        //    a. Subdivide into `subdivisions` segments
        //    b. Create vertices at each subdivision point
        //    c. Tag each vertex: exposed vs touching, neighbor piece ID, edge normal
        // 3. Build triangle strip (thin outline band, ~2px wide at game scale)
        // 4. Store vertex metadata in UV2/UV3 channels or parallel arrays
        // Returns Mesh ready for MeshRenderer with OutlineMeshMat

    VertexMeta[] GetVertexMetadata()
        // parallel array: for each vertex, its edge type, neighbor ID, normal, segment phase seed
```

#### BorderDeformSystem.cs (MonoBehaviour)

Runs every frame. Reads vertex metadata, writes vertex positions.

```text
public class BorderDeformSystem : MonoBehaviour
    [Header("Ambient")]
    float ambientAmplitude = 0.015f     // ~1px at 64px/unit
    float ambientBaseFreq = 4.0f        // Hz
    float ambientHarmonicFreq = 11.0f   // second harmonic for organic feel
    float harmonicWeight = 0.3f         // blend of second harmonic

    [Header("Seam")]
    float seamAmplitude = 0.025f        // slightly stronger than ambient
    float seamFreq = 3.0f

    void LateUpdate()
        for each vertex in outlineMesh:
            if vertex.isExposed:
                offset = AmbientDisplacement(vertex, time)
            else if vertex.isTouching:
                offset = SeamDisplacement(vertex, time)
            vertex.position = vertex.restPosition + offset

    Vector3 AmbientDisplacement(VertexMeta v, float t)
        phase = v.phaseSeed   // unique per vertex, seeded from index + piece ID
        wave1 = sin(t * ambientBaseFreq * TAU + phase) * ambientAmplitude
        wave2 = sin(t * ambientHarmonicFreq * TAU + phase * 1.7) * ambientAmplitude * harmonicWeight
        return v.normal * (wave1 + wave2)

    Vector3 SeamDisplacement(VertexMeta v, float t)
        segPhase = v.segmentPhaseSeed
        wave = sin(t * seamFreq * TAU + segPhase) * seamAmplitude
        sign = (v.ownerPieceId < v.neighborPieceId) ? +1 : -1   // deterministic opposite phase
        return v.normal * wave * sign
```

The `sign` flip is the core of the contact effect: same wave value, opposite direction for the two blocks sharing a seam.

#### ImpactSettleSystem.cs

Triggered by `BoardState.OnPieceLocked`. Injects a decaying impulse.

```text
public class ImpactSettleSystem : MonoBehaviour
    float impactAmplitude = 0.04f
    float decayTime = 0.2f              // seconds

    void OnPieceLocked(PieceLockEvent e)
        // For each seam vertex on the newly locked piece's bottom edge:
        // Inject an impulse: amplitude * exp(-elapsed / decayTime)
        // BorderDeformSystem adds this impulse to its per-frame offset
        // After decayTime * 5, remove the impulse (effectively zero)
```

### A.5 Scene hierarchy (Game.unity)

```text
Game (Scene Root)
+-- GameManager              [GameManager.cs]
+-- Board                    [Transform at screen center]
|   +-- BoardLogic           [BoardState.cs — not a renderer]
|   +-- BoardView            [reads BoardState, instantiates CellViews]
|   |   +-- CellView(0,0)   [SpriteRenderer, child of BoardView]
|   |   +-- CellView(0,1)
|   |   +-- ...
|   +-- StackOutline         [MeshFilter + MeshRenderer + BorderDeformSystem + SeamContactSystem]
|   +-- ActivePieceView      [reads activePiece, renders ghost + piece]
+-- HoldBox                  [HoldBoxView.cs, SpriteRenderer]
+-- NextQueue                [NextQueueView.cs, 5 preview slots]
+-- HUD                      [HUDController.cs — score, lines, time]
+-- PauseOverlay             [PauseOverlay.cs — hidden by default]
+-- Camera                   [orthographic, sized to fit board + UI]
```

### A.6 Build order (what to implement first)

This is the exact order to write code. Each step has a test you can run before moving on.

**Step 1: Static board + piece drop (no visuals yet)**
- Implement `PieceDefinitions.cs` (port piece shapes and SRS kicks from v1 `constants.ts`)
- Implement `BoardState.cs` (grid, move, rotate, lock, clear)
- Implement `PieceBag.cs` (7-bag randomizer)
- Implement `GameManager.cs` (spawn piece, gravity tick, input -> move/rotate/drop, lock, game over)
- Test: play a text-only or colored-square Tetris game in the Unity editor. Pieces fall, rotate with kicks, lock, lines clear. No monsters yet.

**Step 2: Import sprites and build PieceSkinData**
- Import the 3 sprite sheets into `Art/SpriteSheets/`
- Set texture import: Sprite Mode = Multiple, Filter = Point, Compression = None
- Use Sprite Editor to slice crop regions matching v1's `monsterSkin.ts` crop bounds
- Create `PieceSkinData` ScriptableObjects for I, T, S pieces (3 body frames each, crop rects, facial part offsets)
- Test: preview sprites in the Unity inspector. Frames match v1 appearance.

**Step 3: Connected-body rendering**
- Implement `ConnectedBodyBuilder.cs` -- given a set of cells and a PieceSkinData, composites the body sprite as one connected image (not four separate tiles)
- Wire into `BoardView` so locked pieces display as connected creatures
- Test: lock several pieces. Each reads as one organism. No tile seams, no neighbor bleed.

**Step 4: Frame animation**
- Add `Animator` component to piece views, create AnimationClips for the 1-2-3-2-1 body loop
- Test: pieces pulse through their 3 frames smoothly in the stack.

**Step 5: Facial overlays**
- Implement `FacialPartAnimator.cs` -- positions child SpriteRenderers for eyes/tongue using PieceSkinData offsets
- Each eye animates on its own random blink timer
- Test: eyes blink independently. Overlays track the body correctly. Nothing drifts.

**Step 6: Ambient border deformation**
- Implement `OutlineMeshBuilder.cs` -- generate outline mesh for the visible stack
- Implement `BorderDeformSystem.cs` -- ambient sine displacement on exposed vertices
- Apply `OutlineMeshMat` (URP unlit, vertex-color or solid-color matching piece border)
- Test: the stack outline ripples with a fleshy throb. Interior is stable. Motion reads as organic.

**Step 7: Seam-coupled contact response**
- Extend `BorderDeformSystem.cs` with seam displacement logic
- `SeamContactSystem.cs` reads `NeighborMap` from `BoardState`, feeds touching-edge data to `BorderDeformSystem`
- Test: stack two pieces. The shared edge pulses in opposite phase -- one pushes where the other pulls. It looks like pressure, not overlap.

**Step 8: Impact settle**
- Implement `ImpactSettleSystem.cs` -- on lock, inject decaying impulse into seam vertices
- Test: drop a piece. The contact edge compresses briefly, then settles. Feels tactile.

**Step 9: Hold/Next/Preview**
- Implement `HoldBoxView.cs` and `NextQueueView.cs` using the same PieceSkin + connected-body rendering
- Border deformation should be visible here too (ambient only, no seam)
- Test: hold and next queue show monsters with the same fleshy border pulse.

### A.7 Tuning reference values (starting points)

These are initial values to type in. All should be exposed as `[SerializeField]` fields for live tweaking in the Unity inspector.

| Parameter | Starting value | Unit | Notes |
|---|---|---|---|
| `ambientAmplitude` | 0.015 | world units | ~1px at 64 pixels-per-unit |
| `ambientBaseFreq` | 4.0 | Hz | Visible pulse, not frantic |
| `ambientHarmonicFreq` | 11.0 | Hz | Breaks up uniformity |
| `harmonicWeight` | 0.3 | ratio | Keep secondary subtle |
| `seamAmplitude` | 0.025 | world units | Slightly stronger than ambient |
| `seamFreq` | 3.0 | Hz | Slower than ambient for contrast |
| `impactAmplitude` | 0.04 | world units | Brief punch |
| `impactDecayTime` | 0.2 | seconds | Fast settle |
| `outlineSubdivisions` | 6 | per cell edge | 4-8 range; more = smoother, costlier |
| `outlineBandWidth` | 0.03 | world units | ~2px visual thickness |
| `blinkIntervalMin` | 2.0 | seconds | Eye blink randomization |
| `blinkIntervalMax` | 6.0 | seconds | Eye blink randomization |
| `frameAnimSpeed` | 0.35 | seconds/frame | Body 1-2-3-2-1 loop speed |

## Definition of Success

```text
This plan succeeds when
+-- v1 is frozen as a trustworthy reference
+-- v2 preserves exact gameplay while improving presentation
+-- the squishy effect reads as contact, not overlap
+-- border motion reads as flesh, not glitch
+-- the vertical slice proves the effect before full port effort begins
+-- MonStacka looks like the game it was always meant to be
```

## Current Unity Recovery Audit (2026-04-02)

```text
Current state
+-- home preview path
|   +-- materially improved
|   +-- current viewer sprites are mostly intact
|   +-- still needs final polish on scale / ripple feel
+-- live gameplay path
|   +-- still not parity-safe
|   +-- board visuals and board behavior are not yet trustworthy
+-- shell / settings / controls
    +-- partly restored
    +-- not yet at v1 feature parity
```

### Confirmed improvements

- Home/block-viewer creatures now look significantly healthier than the earlier broken Unity builds.
- Resize fitting is better than the earliest Unity versions and the app now behaves more like a single scaled stage.
- The ripple concept is moving in the right direction, but it is still not final quality.
- The build itself launches reliably from the Unity `Builds/Windows` folder when the data folder sits beside the executable.

### Confirmed remaining failures

```text
Remaining failures
+-- gameplay correctness
|   +-- live board still not reliable enough
|   +-- user has observed overlap / fall-through / wrong stacking
|   +-- controls feel laggy or incomplete
+-- gameplay rendering
|   +-- board pieces still diverge from the preview quality
|   +-- missing / cut segments still show up in live play
|   +-- cyan visibility is not consistently trustworthy in gameplay
+-- UI/layout parity
|   +-- left panel / next queue still not elegant
|   +-- text and shell can still drift on some window-state changes
+-- ripple quality
    +-- still not fluid enough
    +-- still not fully color-matched to the sprite art
    +-- still not yet reintroduced safely for all board states
```

### Latest live audit notes

The most important recent observation is:

- **preview path is healthier than gameplay path**

That means the next work should continue prioritizing:

1. restoring a single trustworthy gameplay render path
2. restoring gameplay parity and control parity
3. only then finishing the ripple

Do not try to "polish through" broken gameplay again.

## Exact Repair Order From Here

```text
R1  Restore gameplay correctness first
R2  Restore control parity (keyboard / mouse / Xbox)
R3  Restore game-mode parity
R4  Unify live board rendering with the preview-quality path
R5  Stabilize left HUD / next queue / hold layout
R6  Lock resize behavior so minimize / maximize / drag never drift UI
R7  Cut runtime churn / lag sources
R8  Rebuild ripple using silhouette underlay anchors and sprite-sampled color
R9  Re-enable ripple in stages on the stable board
R10 Run a new live audit and produce the next defect list
```

### R1. Restore gameplay correctness first

Scope:
- verify gravity, lock timing, move/rotate legality, hold, queue, spawn positions, line clears
- verify pieces never overlap, never fall through the floor, and never stack into illegal states
- treat visual code as suspect if gameplay and rendering disagree

Deliverable:
- Unity gameplay behaves like v1 even if ripple must be limited while stabilizing

### R2. Restore control parity

Scope:
- restore keyboard gameplay controls to v1 defaults
- restore Xbox gameplay controls to v1 defaults
- restore pause shell behavior
- restore readable control mapping inside settings or an equivalent shell

Non-negotiable:
- cyan `I` and purple `T` independent eye timing must remain when gameplay rendering is stabilized

### R3. Restore game-mode parity

Scope:
- O.G.B.M.
- X(4)-LINES
- You... Suck?
- correct per-mode goal, target-lines behavior, and shell copy

### R4. Unify live board rendering with the preview-quality path

Scope:
- remove remaining live-board-only sprite corruption
- ensure pink / orange / cyan / purple are complete and visible
- verify locked pieces and active pieces use the same trustworthy body assembly rules
- keep partial-piece / line-clear fallback safe

Success condition:
- if the preview looks right, the same piece should also look right in gameplay

### R5. Stabilize left HUD / next queue / hold layout

Scope:
- tighten queue spacing
- remove awkward gaps
- restore more of the v1 left-side visual hierarchy
- prevent text and labels from colliding or floating oddly

### R6. Lock resize behavior

Scope:
- window drag
- maximize / restore
- taskbar-visible desktop
- no drift between world board, background, and UI text panels

### R7. Cut lag sources

Scope:
- reduce unnecessary destroy/recreate work
- reduce preview churn
- avoid rebuilding locked-piece views except when required
- keep gameplay responsive before adding more visual cost

### R8. Rebuild ripple the right way

```text
New ripple rules
+-- ripple is an underlay, not a body deformation
+-- body sprite stays intact and returns to its original position
+-- cyan/purple eyes remain above the ripple
+-- tint comes from the real sprite art, not a generic piece palette
+-- segments are placed sequentially around the silhouette
+-- motion must travel in a wave around the perimeter
```

Implementation notes:
- use many close anchor/segment points around the full outer contour
- pull the spaces between anchors slightly, then spring them back
- render ripple below the body sprite
- never let ripple remove cells or carve into the body art

### R9. Re-enable ripple in stages

```text
Stage A  home / preview reference only
Stage B  active falling piece only
Stage C  landed stack with subtle constant life
Stage D  stronger contact / impact emphasis
```

Do not advance to the next stage until the previous one is stable in live audit.

### R10. Audit gate

For every repair batch, collect:
- visual bugs
- gameplay bugs
- UI/layout bugs
- control/controller bugs
- performance/lag notes

If any remain, add them back into this plan before the next batch.
