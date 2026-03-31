# MonStacka Next Implementation Plan

This file is the current source of truth for MonStacka work on branch `codex/monstacka-next`.

Use it this way:

```text
1. Re-read this file first
2. Trust the status markers below over older docs
3. Work the remaining phases in order
4. Verify each phase before moving on
5. Do not redesign the user's drawings
```

## Current State

### Foundation Already Complete

These items are already in place in the current branch and should not be re-planned from scratch:

- `enhanced/src/assets/ui/monstacka-home-menu-clean.png` exists and is the home-menu base art
- `enhanced/src/assets/ui/monstacka-background.png` is the in-game artboard
- `enhanced/src/ui/regionMap.ts` is the single source of truth for art-hitbox coordinates
- `enhanced/src/types.ts` defines `AppPhase`
- `enhanced/src/main.ts` uses explicit app phases: `menu`, `countdown`, `playing`, `game-over`, `sprint-clear`
- the in-game mode dropdown has already been removed
- browser-safe quit fallback already exists
- debug mode via `?debug=1` already exists
- loading state for monster art already exists
- targeted active-preview refresh already exists on the home menu
- lore fit now uses DOM overflow checks with a final fallback tier
- record modal titles already switch between Arcade and Sprint
- Tauri packaging is already configured and buildable
- Arcade, 40 Lines, and Training all exist
- the monster sprite rendering pipeline already exists:
  - reactive eyes
  - selective blinking
  - tongue motion
  - visual-only squish

### Main Remaining Gaps

These are the only remaining work areas that matter right now:

```text
Remaining execution focus
├─ exact visual alignment against the user's drawings
├─ home-menu preview polish and continuous active animation
├─ speech-bubble text fit and readability under real content
├─ in-game layout fit against the gameplay artboard
├─ live proof that all run transitions work reliably
├─ live proof that creature animations are visible in actual play
└─ final release/readiness signoff
```

## Non-Negotiable Product Intent

- Keep the user's drawings as the UI source of truth.
- Do not replace the art with a generic menu or HUD.
- Fix alignment by adjusting `regionMap.ts`, not by layering random CSS percentages.
- Keep creature effects visual-only.
- `HTML` stays separate from MonStacka!.
- In game, `Home` abandons the current run and returns to the menu without saving the abandoned run.

## Calibration Model

This is the required structure for exact alignment plus squishy visuals:

```text
Art layer
├─ measured in native image pixels
├─ all hitboxes and content zones live in regionMap.ts
└─ no CSS nudging after the fact

Logic layer
├─ exact 10 x 20 board
├─ exact integer cell positions
├─ exact collision / rotation / lock logic
└─ never deformed

Skin layer
├─ monster sprites
├─ eyes / blink / tongue
├─ squish / settle / breathing
└─ rendered inside the logic cells only
```

Implementation rule:
- if a transform is meant to be visual-only, it belongs on an inner skin node, never on the outer grid cell that represents gameplay position

## Verification Matrix

Every remaining phase must use this matrix:

```text
Targets
├─ Browser preview
│  ├─ 1920x1080
│  ├─ 1280x720
│  └─ 960x540
├─ Tauri desktop
│  ├─ default launch size
│  └─ minimum supported size
└─ Modes
   ├─ normal
   └─ ?debug=1
```

## Issue Logging Format

Whenever runtime QA finds a problem, capture it in this format before fixing:

```text
Issue ID:
Screen:
Mode:
Window size:
Debug mode:
Repro steps:
Observed result:
Expected result:
Likely files:
Severity:
Screenshot path:
```

---

## Phase 1: Align The Plan To Reality

**Status:** complete

**Objective:** Keep this plan synchronized with the actual branch so implementation is not chasing already-completed tasks.

**Files:**
- `MONSTACKA_NEXT_IMPLEMENTATION_PLAN.md`
- `MONSTACKA_ISSUES_FOR_CLAUDE.md`

**Acceptance:**
- This plan reflects the live branch, not an older snapshot.
- No completed task still appears as a remaining blocker.

---

## Phase 2: Home Menu Calibration And Preview Loop

**Status:** in progress

**Objective:** Make the home menu feel like the drawing itself became interactive, with a clean preview wheel and obvious active Monstos behavior.

**Files:**
- `enhanced/src/ui/regionMap.ts`
- `enhanced/src/main.ts`
- `enhanced/src/ui/homeMenu.ts`
- `enhanced/src/ui/monsterDom.ts`
- `enhanced/src/styles.css`
- `enhanced/index.html`

**Actions:**

### 2A. Calibrate Home Regions

Audit and adjust these regions against `monstacka-home-menu-clean.png`:
- `home-settings`
- `home-quit`
- `home-name-fill`
- `home-voice`
- `home-lore`
- `home-lore-fill`
- `home-preview-left`
- `home-preview-center`
- `home-preview-right`
- `home-prev`
- `home-next`
- `home-score-sprint`
- `home-score-arcade`
- `home-score-grid`
- `home-play-arcade`
- `home-play-sprint`
- `home-play-training`

Rules:
- all position fixes go through `regionMap.ts`
- do not add CSS percentage offsets as a workaround

### 2B. Restore Continuous Active Preview Animation

Current branch note:
- a lightweight active-preview refresh has been added
- this still requires live alignment and motion signoff

Fix strategy:
- keep full menu DOM rerenders event-driven
- add a lightweight menu animation loop that updates only the three preview figures while `appPhase === 'menu'`
- only the center preview gets `animate: true`
- side previews stay static

Implementation rules:
- do not rerender the whole menu every frame
- do not change names, lore, score tab state, or other DOM every frame
- update only preview visuals with current `now`

### 2C. Speech Bubble Fit

Current branch note:
- lore fit now uses DOM overflow checks with a final small fallback tier
- this still needs live content verification

Required cleanup:
- remove any unnecessary fake background patching behind the lore text
- keep the text inside the existing drawn bubble
- prefer container-driven fit and scrolling over brittle text-length buckets
- if heuristics remain, they must be minimal and justified by visible output

### 2D. Score Tabs And Home Buttons

Verify and tune:
- `X(4)-LINES` tab alignment
- `O.G.B.M.` tab alignment
- active-tab visibility without covering the art
- play-button hitboxes against the three drawn buttons
- top-right settings / quit button hitboxes
- lore and voice button hitboxes

**Verification:**
- run the app with `?debug=1`
- capture screenshots for:
  - top-right icons
  - Monstos name / lore / voice cluster
  - left/center/right preview wheel
  - score tabs and score grid
  - all three play buttons
- click through all 7 Monstos in both directions
- confirm:
  - names change
  - center preview changes
  - center preview animates continuously
  - side previews do not animate the same way
  - lore collapses and expands
  - voice button is clickable and safe
  - score tabs switch displayed records correctly

**Acceptance:**
- all home-menu controls line up with the drawing
- no sample-art bleed or fake replacement shapes remain
- center preview is visibly alive while selected
- the preview wheel cycles through all 7 Monstos cleanly

---

## Phase 3: Game Screen Fit And Run Reliability

**Status:** in progress

**Objective:** Make every mode start reliably, keep retry/home transitions safe, and fit the game shell to the gameplay art.

**Files:**
- `enhanced/src/ui/regionMap.ts`
- `enhanced/src/main.ts`
- `enhanced/src/ui/render.ts`
- `enhanced/src/ui/monsterDom.ts`
- `enhanced/src/styles.css`
- `enhanced/index.html`

**Actions:**

### 3A. Replace The Single Game Safe Area With Measured Zones

Create and calibrate explicit in-game regions:
- `game-settings`
- `game-quit`
- `game-home`
- `game-board-zone`
- `game-primary-zone`
- `game-secondary-zone`

Current branch note:
- initial measured zones are now in place
- these still need live calibration against the running artboards

Rules:
- do not rely on one broad `game-ui` rectangle plus CSS guessing
- each major content block should have its own measured zone
- all zone measurements live in `regionMap.ts`

### 3B. Anchor The Board To An Exact Safe Rect

Required behavior:
- the visible board must fit inside the measured board zone
- the board must stay a true `1 : 2` rectangle
- the board must stay centered within the board zone
- overlays and flashes must remain attached to the board itself

Current branch note:
- board-fit sizing logic is now present
- the remaining task is visual calibration, not missing structure

### 3C. Split Logic Cell From Squishy Skin

Required structure:
- outer cell = exact logical cell
- inner skin node = deformable visual node

Rules:
- no squish transform on the outer cell
- no shift/scale transform that can move the gameplay hitbox
- all monster visual transforms move to the inner skin node

Current branch note:
- the inner skin node split is now implemented
- remaining work is live proof and tuning

Files expected to change:
- `enhanced/src/ui/monsterDom.ts`
- `enhanced/src/styles.css`

### 3D. Verify Game Shell Composition

Check:
- board placement relative to the background art
- sidebar placement relative to the background art
- top-right buttons remain unobstructed
- no clipping at default Tauri size or minimum supported size

If needed:
- adjust zone rectangles in `regionMap.ts`
- reduce panel minimum widths only if the measured zones require it
- slightly reduce panel opacity only if it improves art coherence

### 3E. Run Flow Reliability

Verify this loop explicitly:

```text
Arcade
menu -> countdown -> playing -> top out -> retry/home

40 Lines
menu -> countdown -> playing -> clear -> record modal -> home

Training
menu -> countdown -> playing -> finesse feedback -> home
```

Confirm:
- entering a mode never shows a dead board
- retry always starts a fresh run
- Home always returns cleanly from:
  - countdown
  - active play
  - game-over
  - sprint-clear
- re-entering a different mode after returning home works

**Verification:**
- browser run
- Tauri run
- normal mode and `?debug=1`
- at least one full pass through each mode

**Acceptance:**
- no stuck transitions remain
- board and HUD fit the artboard
- Home and Retry are reliable from every reachable game phase
- visual squish no longer risks shifting the gameplay cell positions

---

## Phase 4: Creature Animation Proof And Tuning

**Status:** pending

**Objective:** Prove the creature effects are visible in real play and correct any mismatch to the intended spec.

**Files:**
- `enhanced/src/ui/render.ts`
- `enhanced/src/ui/monsterDom.ts`
- `enhanced/src/monsterSkin.ts`
- `enhanced/src/styles.css`

**Actions:**

### 4A. Home Menu Animation Proof

Confirm in the running app:
- center Monstos floats
- center Monstos has wandering eyes where applicable
- blink behavior is visible on intended preview pieces
- cycling transfers active behavior to the new center

### 4B. In-Game Animation Proof

Confirm during actual gameplay:
- eyes visibly track active-piece motion
- blinking only appears on:
  - `S` / SORRISOL
  - `J` / DOUSEMA
  - `L` / GALIFFAMBOS
- no blinking on:
  - `I`
  - `Z`
  - `T`
  - `O`
- T-piece tongue is visible and moving
- squish appears only where pieces contact neighbors
- gameplay is unchanged

### 4C. If Any Effect Is Too Subtle

Tune only visibility, not behavior:
- increase pupil travel slightly
- increase blink duration slightly
- increase tongue sway slightly
- increase squish contrast slightly

Do not:
- add new piece behaviors
- alter collision, timing, or rotation logic

**Verification:**
- use deliberate board setups to lock multiple piece families
- move the active piece far left and right to confirm pupil response
- leave stable pieces on the board long enough to observe blink cycles
- create stacked and side-adjacent placements to observe squish

**Acceptance:**
- creature effects are clearly visible in live play
- behavior matches the intended family rules
- all effects remain visual-only

---

## Phase 5: Final QA, Docs, And Release Gate

**Status:** pending

**Objective:** Finish with evidence, not assumptions.

**Files:**
- `README.md`
- `CHANGELOG.md`
- `Launcher.ps1`
- `Start-Tetris.cmd`
- `enhanced/src-tauri/tauri.conf.json`

**Actions:**
1. Run:
   - `npm test`
   - `npm run build`
   - `npm run tauri:build`
2. Confirm launcher still prefers the native MonStacka! executable when present.
3. Confirm browser preview still works.
4. Update docs only if behavior changed during Phases 2-4.
5. Capture final QA results using the issue logging format above.

**Final Acceptance Gate:**

```text
MonStacka is ready for signoff only if:
├─ all major home-menu controls align to the art
├─ all major in-game controls align to the art
├─ all three modes start reliably
├─ retry and home never get stuck
├─ leaderboard flows are correct
├─ center preview animation is obvious
├─ in-game creature animations are visibly confirmed
├─ browser preview still works
├─ Tauri build still works
└─ no Priority 1 issue remains
```

---

## Final QA Checklist

### Home Menu
- [ ] settings button aligned and clickable
- [ ] quit button aligned and clickable
- [ ] Monstos name banner aligned
- [ ] voice button aligned and clickable
- [ ] lore button aligned and clickable
- [ ] lore text fits inside the speech bubble
- [ ] left and right arrows align with the drawn arrows
- [ ] preview wheel cycles through all 7 Monstos
- [ ] center preview animates continuously
- [ ] side previews stay visually quieter than center
- [ ] score tabs align correctly
- [ ] score tabs switch data correctly
- [ ] three play buttons align and launch correct modes

### Gameplay
- [ ] Arcade starts with a real countdown and playable run
- [ ] 40 Lines starts with a real countdown and playable run
- [ ] Training starts with a real countdown and playable run
- [ ] Retry works from game-over and sprint-clear
- [ ] Home works from countdown and active play
- [ ] top-out overlay appears when expected
- [ ] sprint-clear overlay appears when expected
- [ ] nickname entry appears only where appropriate

### Creature Effects
- [ ] home-menu active preview is visibly animated
- [ ] in-game eyes track motion
- [ ] only S/J/L blink
- [ ] T tongue is visible and moving
- [ ] squish is visible on contact

### Release
- [ ] `npm test` passes
- [ ] `npm run build` passes
- [ ] `npm run tauri:build` passes
- [ ] launcher still works

---

## Execution Order Summary

```text
Phase 1  Plan alignment
Phase 2  Home menu calibration and preview loop
Phase 3  Game screen fit and run reliability
Phase 4  Creature animation proof and tuning
Phase 5  Final QA, docs, and release gate
```

Codex: start with Phase 2 if Phase 1 is already reflected in the file you are reading.
