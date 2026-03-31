# MonStacka Issue Audit For Claude

This document is a concrete handoff of the current MonStacka problems.

Use it as a planning brief before more implementation work.

The goal is not another round of small CSS nudges. The goal is a stable implementation plan that makes the UI match the user's drawings, fixes the game-state flow, and verifies that the monster animations actually work both in the menu and in live gameplay.

## Current Shape

```text
+========================== Current MonStacka Stack ==========================+
| Home menu artboard      | monstacka-home-menu.png                          |
| In-game artboard        | monstacka-background.png                         |
| UI strategy today       | invisible hitboxes + replacement overlays        |
| Preview strategy today  | live canvases placed over baked sample art       |
| Game flow today         | menu > game shell > countdown/overlay logic      |
| Main problem            | visual layers and state flow are not reliable    |
+============================================================================+

+========================== Primary Failure Areas ============================+
| 1. Home menu fidelity   | layout does not faithfully match the drawing      |
| 2. Preview wheel        | Monstos art/animation behavior is not clean       |
| 3. In-game flow         | starting, restarting, and returning can feel stuck|
| 4. In-game visuals      | backdrop/buttons/animations not verified cleanly  |
| 5. QA gap               | build succeeds, but visual/runtime signoff is weak|
+============================================================================+
```

## Repo Paths To Inspect

- Home screen art: `enhanced/src/assets/ui/monstacka-home-menu.png`
- In-game art: `enhanced/src/assets/ui/monstacka-background.png`
- Home menu logic: `enhanced/src/ui/homeMenu.ts`
- Home/game shell markup: `enhanced/index.html`
- App flow: `enhanced/src/main.ts`
- In-game renderer: `enhanced/src/ui/render.ts`
- Monster preview/cell DOM helpers: `enhanced/src/ui/monsterDom.ts`
- Monster art/animation setup: `enhanced/src/monsterSkin.ts`
- Layout/hitbox styling: `enhanced/src/styles.css`

## Non-Negotiable Product Intent

```text
Home launch
  +-- open MonStacka! desktop app
      +-- first screen is the user's drawn home menu
          |-- use the drawing itself as the UI foundation
          |-- convert the drawn controls into real controls
          |-- do not "redesign" the menu
          +-- do not paint fake replacement shapes on top unless absolutely necessary

Gameplay
  +-- entering a mode should start a playable run
      |-- not a dead board
      |-- not a stuck overlay state
      |-- not a post-loss state
      +-- user must be able to retry or return home cleanly
```

## Concrete Issue List

### 1. Home Menu Is Not A Faithful Translation Of The Drawing

Source:
- user-reported
- visible in recent screenshots
- supported by the current implementation strategy in `enhanced/index.html` and `enhanced/src/styles.css`

Symptoms:
- Buttons do not consistently line up with the user's drawn buttons.
- The top-right home-menu controls are approximate, not precisely mapped to the art.
- The two leaderboard mode buttons under the crown are not aligned well enough with the drawn buttons.
- The three play-mode buttons are still driven by percentage-based hitboxes rather than an audited control map.
- The current approach feels like "shift things around, then find another issue."

Technical cause:
- The menu is currently implemented as an artboard with invisible absolutely positioned hitboxes.
- Those hitboxes are manually tuned percentages, not based on a stable coordinate map or measured regions from the source art.
- There is no formal layout calibration step for the user's 1920x1080 drawing.

Files involved:
- `enhanced/index.html`
- `enhanced/src/styles.css`

Done means:
- Every interactable element on the home menu has a clearly defined hitbox that matches the drawn control it belongs to.
- Hitboxes are verified against the source art at the native 1920x1080 layout and at scaled app sizes.
- No button appears clickable where the drawing does not imply a control.

### 2. The Monstos Preview Wheel Is Still Structurally Wrong

Source:
- user-reported repeatedly
- visible in recent screenshots
- supported by the current preview implementation in `enhanced/src/ui/homeMenu.ts` and `enhanced/src/ui/monsterDom.ts`

Symptoms:
- The preview area has behaved like live content is being stacked over example art instead of cleanly replacing it.
- Earlier attempts painted extra circles/orbs over the drawing.
- Even after removing some of those layers, the wheel still does not feel cleanly integrated.
- The user examples in the art are acting like baked sample art under live overlays.
- The names cycle, but the preview system has not yet achieved a clean "one active, two side previews" result that looks native to the drawing.

Technical cause:
- The current art contains baked sample Monstos in the preview circles.
- The implementation has been layering live preview canvases on top of an image that already contains example creatures.
- There is no clean asset strategy yet for removing or masking the baked sample previews in a controlled way.

Files involved:
- `enhanced/src/assets/ui/monstacka-home-menu.png`
- `enhanced/src/ui/homeMenu.ts`
- `enhanced/src/ui/monsterDom.ts`
- `enhanced/src/styles.css`

Done means:
- The preview wheel shows left, center, and right Monstos cleanly.
- Clicking the drawn arrows cycles through all Monstos in the intended direction.
- The active center Monstos animates continuously.
- The side previews are static or visibly less active.
- No duplicate circles, fake patches, or sample-art bleed remain.

### 3. The Speech Bubble/Lore Area Is Not Settled

Source:
- user-reported
- visible in screenshots
- supported by current DOM/CSS strategy

Symptoms:
- Lore text has been drawn on top of the art in a way that does not feel natural yet.
- The current implementation uses a white patch behind the text to cover the handwritten example text from the drawing.
- Text fit for longer descriptions is still fragile.
- The lore button behavior exists, but the visual result is not fully convincing yet.
- The voice button exists but is only a placeholder.

Technical cause:
- The speech bubble is baked into the image with handwritten sample text.
- The app is trying to overwrite that content at runtime using masks and overlay text blocks.
- The current fit logic is heuristic only: string-length thresholds toggle `is-long` and `is-huge`.

Files involved:
- `enhanced/src/ui/homeMenu.ts`
- `enhanced/src/styles.css`
- `enhanced/index.html`

Done means:
- The lore text appears inside the existing drawn speech bubble, not as a second fake bubble.
- The lore button cleanly collapses and expands the lore area.
- The voice button is clickable and safely does nothing for now without visual glitches.
- Long Monstos descriptions remain readable without breaking the art.

### 4. Home Menu Preview Animation Is Not Fully Verified

Source:
- user expectation
- partial implementation exists
- not fully signed off visually

Symptoms:
- The active center Monstos is supposed to animate while selected.
- The side previews should not animate the same way.
- This behavior has been implemented in pieces, but has not been fully verified as clean and obvious in the live app.

Technical cause:
- Animation behavior exists, but visual clarity depends on the preview wheel being laid out correctly first.
- A correct animation can still look broken if the preview is overlapping the background art or clipped badly.

Files involved:
- `enhanced/src/ui/homeMenu.ts`
- `enhanced/src/ui/monsterDom.ts`
- `enhanced/src/monsterSkin.ts`
- `enhanced/src/styles.css`

Done means:
- The active center Monstos loops its animation while selected.
- Cycling away stops that active emphasis on the old Monstos and transfers it to the newly selected one.
- The behavior is clearly visible in the home menu without layout artifacts.

### 5. Scoreboard Tabs Need Functional And Positional Verification

Source:
- user-reported
- partial implementation exists

Symptoms:
- The two scoreboard mode buttons under the crown do not line up well with the drawn buttons.
- The user needs each tab to switch the visible scores between OGBM and X(4)-LINES.
- The code may already be swapping data, but the UI alignment and clarity are not trustworthy yet.

Technical cause:
- Functional logic exists in `enhanced/src/ui/homeMenu.ts`, but the hitboxes remain rough.
- Selection-state feedback is weak because visible button outlines were removed during cleanup.

Files involved:
- `enhanced/index.html`
- `enhanced/src/ui/homeMenu.ts`
- `enhanced/src/styles.css`

Done means:
- Clicking the left score tab shows X(4)-LINES records.
- Clicking the right score tab shows OGBM records.
- The buttons are aligned to the drawn buttons.
- The active tab is obvious to the player without ruining the artwork.

### 6. Game Start / Retry / Return-Home Flow Is Not Trustworthy

Source:
- user-reported runtime failure
- supported by the complexity of current state transitions

Symptoms:
- The user entered a game mode and saw the board, but did not get a clear playable start.
- It behaved as if the run had already ended, or as if the game was stuck in a bad state.
- The user was not clearly given a path to actually start playing again.
- Returning home, restarting, and re-entering a mode do not feel robust yet.

Technical cause:
- Menu/game flow, reset logic, countdown logic, overlay logic, and retry logic are spread across:
  - `enhanced/src/main.ts`
  - `enhanced/src/ui/render.ts`
- The current runtime needs an explicit state audit covering:
  - entering a mode from the home screen
  - countdown start
  - active gameplay
  - top out
  - retry
  - return home
  - re-entering a different mode

Done means:
- Starting a mode always creates a playable run.
- Retry always starts a fresh run.
- Home always abandons the current run without saving it.
- Returning from game to menu and back again never leaves the app stuck in a dead board or post-loss state.

### 7. In-Game Backdrop Integration Is Not Signed Off

Source:
- user requirement
- partial implementation exists

Symptoms:
- The user supplied a dedicated in-game background drawing with gear, quit, and home buttons in the top-right.
- The app uses that artboard, but the user still does not trust that it is laid out correctly.
- The in-game top-right controls remain percentage-based overlays, not a verified final mapping.

Technical cause:
- Same core issue as the home menu: invisible absolute hitboxes over art, without a stable alignment audit.

Files involved:
- `enhanced/src/assets/ui/monstacka-background.png`
- `enhanced/index.html`
- `enhanced/src/styles.css`

Done means:
- The in-game backdrop is the actual user drawing.
- Gear opens settings.
- Quit exits the app.
- Home returns to the home menu and abandons the run.
- Each control is aligned to the drawn button it belongs to.

### 8. In-Game Monster Animations Have Not Been Proved In Real Play

Source:
- user-reported
- code exists, but visual verification is incomplete

Symptoms:
- The user has not actually seen the creature animations clearly during gameplay.
- They have not confirmed blinking, reactive eye movement, tongue motion, or the fleshy squish effect while a live run is happening.
- The app may build successfully while still failing the real visual intent.

Technical cause:
- Animation code exists in the renderer and monster skin pipeline, but it has not been validated in a stable gameplay session with the final in-game UI.
- If game flow is unstable, visual verification in play is blocked.

Files involved:
- `enhanced/src/ui/render.ts`
- `enhanced/src/monsterSkin.ts`
- `enhanced/src/styles.css`

Done means:
- During a real playable run, the user can see:
  - wandering/reactive eyes on all intended eyes
  - blinking only on the intended pieces
  - purple tongue motion
  - fleshy visual squish on contact
- These effects remain visual-only and do not change gameplay logic.

### 9. The Current UI Strategy Is Too Fragile

Source:
- project-wide observation

Symptoms:
- Fixing one alignment problem often exposes another.
- Small changes produce regressions in nearby art overlays.
- The current work has felt like incremental patching rather than stable implementation.

Technical cause:
- The app is using hand-tuned absolute overlays over complex raster drawings.
- Some regions of the drawings already contain example content that the app is also trying to render dynamically.
- There is no clear separation yet between:
  - immutable decorative art
  - replaceable sample art
  - dynamic controls
  - dynamic text
  - dynamic preview content

Done means:
- Claude proposes a real UI-integration strategy instead of continued ad hoc percentage nudging.
- The strategy clearly defines what belongs in the base art, what is covered/replaced, and what is purely interactive overlay.

## Likely Root Cause Summary

```text
Root problem is not "one bad button".

It is:

user drawings
  +-- contain both decorative art and example content
      +-- app overlays live controls and live previews on top
          +-- without a stable region map
              |-- alignment drifts
              |-- previews overlap baked samples
              |-- text replacement feels hacked in
              +-- visual QA becomes unreliable
```

---

# Execution Plan

This section replaces the "Required Planning Questions" and "Recommended Planning Order" from the original brief with a concrete, phased implementation plan. Each phase addresses one or more of the issues above and includes the specific files to modify, what to do, and how to verify it.

## Answers To The Planning Questions

### Q1: Cleanest way to treat user's drawings as production UI without fighting baked sample content?

**Answer: Create "clean base" variants of each artboard.**

The home menu PNG (`monstacka-home-menu.png`) contains baked sample Monstos in the preview circles and handwritten text in the speech bubble. These regions must be made neutral in the base image so the app can render dynamic content into them cleanly.

Approach:
- Open the source art in an image editor.
- Identify every region that contains sample/placeholder content:
  - The three preview circles (left, center, right Monstos)
  - The speech bubble interior (handwritten lore text)
  - The name banner area (if it contains a baked name)
- Create a "clean" version where those regions are filled with the surrounding background color or pattern, preserving all decorative art (borders, circles, bubble outlines, arrows, crown, etc.).
- Save as `monstacka-home-menu-clean.png` and swap the CSS `background-image` reference.
- The in-game art (`monstacka-background.png`) likely does NOT have baked content that conflicts — verify and leave it as-is if clean.

This eliminates the "layering live content over baked samples" problem at the source.

### Q2: Should the art be split into layers or edited into a cleaner base?

**Answer: Edit into a cleaner base.** Layer splitting (multiple PNGs composited at runtime) adds complexity and z-index headaches. A single clean PNG per screen with dynamic content rendered into well-defined regions is simpler and more robust.

### Q3: What coordinate-mapping strategy for interactable regions?

**Answer: A TypeScript region map at native 1920x1080 with proportional scaling.**

Create a new file `enhanced/src/ui/regionMap.ts` that defines every interactive region as pixel coordinates at the native art resolution:

```typescript
interface HitRegion {
  id: string;
  x: number;      // pixels from left at 1920px width
  y: number;      // pixels from top at 1080px height
  width: number;   // pixels at 1920px width
  height: number;  // pixels at 1080px height
}
```

These pixel values are measured once from the source art. At runtime, each region is converted to percentage positions (`x / 1920 * 100%`, `y / 1080 * 100%`, etc.) and applied as inline styles or generated CSS. This replaces every hand-tuned `.hit-*` class with a single source of truth.

A helper function applies the map to the DOM on load and on resize:

```typescript
function applyRegionMap(artboard: HTMLElement, regions: HitRegion[]): void {
  regions.forEach(region => {
    const el = artboard.querySelector(`[data-region="${region.id}"]`);
    if (!el) return;
    (el as HTMLElement).style.left = `${(region.x / 1920) * 100}%`;
    (el as HTMLElement).style.top = `${(region.y / 1080) * 100}%`;
    (el as HTMLElement).style.width = `${(region.width / 1920) * 100}%`;
    (el as HTMLElement).style.height = `${(region.height / 1080) * 100}%`;
  });
}
```

The `.artboard` container already preserves 16:9 aspect ratio (`aspect-ratio: 16 / 9` in CSS), so percentage-based positioning within it scales correctly at any window size.

### Q4: What state machine should govern the app flow?

**Answer: An explicit `AppPhase` enum in `main.ts` with guarded transitions.**

```typescript
type AppPhase =
  | 'menu'           // home screen visible, no game state active
  | 'countdown'      // game screen visible, countdown timer running, input blocked
  | 'playing'        // game active, input accepted, gravity running
  | 'game-over'      // board frozen, overlay shown, retry/home available
  | 'sprint-clear';  // sprint complete, overlay shown, retry/home available

function transitionTo(phase: AppPhase): void {
  // exit logic for current phase
  // entry logic for new phase
  // update appPhase variable
}
```

Every user action (start mode, retry, return home, mode change) calls `transitionTo()` which handles cleanup and setup. The `tick()` loop checks `appPhase` to decide what to process. This replaces the current scattered boolean checks (`!state.startTime && !state.gameOver`, `state.sprintComplete`, etc.).

### Q5: Visual verification checklist?

**Answer: A debug overlay mode.** Add a `?debug=1` query parameter that:
- Draws colored borders on all hitbox elements (red for home, blue for in-game)
- Highlights animated elements (eyes, tongue, squish cells) with a subtle glow
- Logs animation frame counts to a corner HUD
- Shows the current `AppPhase` in the corner

This allows the user to visually confirm that controls are aligned and animations are firing without needing to read code.

---

## Phase 1: Asset Cleanup And Region Map

**Issues addressed:** #1 (home menu fidelity), #2 (preview wheel), #3 (speech bubble), #7 (in-game backdrop), #9 (fragile UI strategy)

**Priority:** HIGHEST — all other phases depend on this foundation.

### Step 1A: Create Clean Base Art

Files to create/modify:
- `enhanced/src/assets/ui/monstacka-home-menu-clean.png` (NEW — edited from original)
- `enhanced/src/styles.css` (swap background-image reference)

Work:
1. Open `monstacka-home-menu.png` in an image editor at native 1920x1080.
2. Measure the exact pixel boundaries of these baked-content regions:
   - Left preview circle
   - Center preview circle
   - Right preview circle
   - Speech bubble interior (inside the drawn outline)
   - Name banner rectangle
3. Fill each region with the surrounding background color/pattern, preserving the circle outlines, bubble border, and all decorative art.
4. Save as `monstacka-home-menu-clean.png`.
5. Update `.home-artboard` in `styles.css` to reference the clean version.
6. Verify `monstacka-background.png` — if it has no baked content that conflicts, leave it unchanged.

Acceptance:
- The home menu background shows empty preview circles and an empty speech bubble.
- All decorative art (borders, arrows, crown, title, play buttons) is preserved.
- The in-game background is verified clean or left unchanged.

### Step 1B: Build The Region Map

Files to create/modify:
- `enhanced/src/ui/regionMap.ts` (NEW)
- `enhanced/src/styles.css` (remove all `.hit-*` percentage rules)
- `enhanced/index.html` (add `data-region` attributes to hit elements)
- `enhanced/src/main.ts` (call `applyRegionMap` on init)

Work:
1. Open the source art at native resolution. Measure the pixel bounding box of every interactive element:

   **Home screen regions** (from `monstacka-home-menu.png` at 1920x1080):
   - `home-settings`: gear icon top-right
   - `home-quit`: X/close icon top-right
   - `home-voice`: speaker icon near name
   - `home-lore`: lore toggle button
   - `home-prev`: left arrow for Monstos carousel
   - `home-next`: right arrow for Monstos carousel
   - `home-score-sprint`: left score tab under crown
   - `home-score-arcade`: right score tab under crown
   - `home-play-arcade`: OG Bubba Mode play button
   - `home-play-sprint`: X(4)-LINES play button
   - `home-play-training`: Training play button
   - `home-preview-left`: left preview circle (for dynamic Monstos rendering)
   - `home-preview-center`: center preview circle
   - `home-preview-right`: right preview circle
   - `home-lore-bubble`: speech bubble interior
   - `home-name-banner`: name display area
   - `home-score-grid`: leaderboard display area

   **In-game screen regions** (from `monstacka-background.png` at 1920x1080):
   - `game-settings`: gear icon top-right
   - `game-quit`: X/close icon top-right
   - `game-home`: home icon top-right

2. Export all regions as a TypeScript constant:
   ```typescript
   export const HOME_REGIONS: HitRegion[] = [
     { id: 'home-settings', x: ..., y: ..., width: ..., height: ... },
     // ...
   ];
   export const GAME_REGIONS: HitRegion[] = [
     { id: 'game-settings', x: ..., y: ..., width: ..., height: ... },
     // ...
   ];
   ```

3. Write `applyRegionMap()` that converts pixel coords to percentages and applies them as inline styles.

4. In `index.html`, add `data-region="home-settings"` (etc.) to each button/div that maps to a region. Remove the `.hit-home-settings` (etc.) class-based positioning.

5. In `styles.css`, delete all `.hit-*` position/size rules. Keep only the shared `.art-hit` base style (transparent background, cursor pointer, border-radius).

6. Call `applyRegionMap()` in `main.ts` `init()` after DOM is ready.

Acceptance:
- All interactive elements are positioned from the region map, not from hand-tuned CSS.
- Changing a button's position requires editing one line in `regionMap.ts`, not hunting through CSS.
- The layout scales correctly when the window is resized (test at 1920x1080, 1280x720, and 960x540).

---

## Phase 2: Preview Wheel Rebuild

**Issues addressed:** #2 (preview wheel), #4 (preview animation)

**Depends on:** Phase 1A (clean base art with empty preview circles)

### Step 2A: Clean Preview Rendering

Files to modify:
- `enhanced/src/ui/homeMenu.ts`
- `enhanced/src/ui/monsterDom.ts`
- `enhanced/src/styles.css`

Work:
1. With the clean base art in place, the preview circles are now empty background regions.
2. The `.preview-slot-*` containers are already positioned by the region map (Phase 1B).
3. Refactor `renderMonstosStage()` to:
   - Clear the container fully before rendering.
   - Call `populateMonsterPreviewFigure()` to render the Monstos sprite into the empty circle.
   - Apply a circular `clip-path: circle(50%)` to each preview slot so the sprite is cleanly contained within the drawn circle outline.
   - Do NOT add background colors, fake circles, or patching elements.
4. The center slot gets the `is-active` class (enables float animation + eye/blink).
5. The left and right slots get static rendering (no `is-active`, no float animation).

### Step 2B: Arrow Cycling Polish

Work:
1. Verify that clicking the left arrow cycles backward through `MONSTOS_ORDER` and the right arrow cycles forward.
2. Each cycle triggers a full re-render of all three preview slots.
3. The name, lore text, and voice hint update simultaneously.
4. Add a brief CSS transition (`opacity 0 -> 1` over 120ms) on the preview stage content to smooth the swap.

Acceptance:
- Three Monstos render cleanly inside the drawn circles with no bleed or overlap.
- Center Monstos floats/animates continuously.
- Side Monstos are static.
- Arrow clicks cycle smoothly in both directions.
- No baked sample art is visible.

---

## Phase 3: Speech Bubble And Lore Fix

**Issues addressed:** #3 (speech bubble/lore)

**Depends on:** Phase 1A (clean base art with empty speech bubble)

Files to modify:
- `enhanced/src/styles.css`
- `enhanced/src/ui/homeMenu.ts`

Work:
1. With the clean base art, the speech bubble is an empty outlined region.
2. The `.home-lore-fill` container is positioned by the region map to sit exactly inside the drawn bubble outline.
3. Remove the `.home-lore-mask` element entirely — it was a hack to cover baked text. With clean art, no mask is needed.
4. Remove the `.home-lore-mask` CSS rules and the `::before`/`::after` pseudo-elements that drew a fake bubble shape.
5. Remove the `::before` white background patch from `.home-lore-fill` — the bubble outline is already in the art; we just need text inside it.
6. Keep the `.home-lore-text` styling but improve the sizing:
   - Replace the `is-long` / `is-huge` string-length heuristic with CSS `overflow-y: auto` and a fixed `max-height` derived from the region map height.
   - Use `font-size: clamp(...)` based on the container size, not on text length.
   - Let the text scroll if it exceeds the bubble bounds, with the hidden scrollbar already in place.
7. The lore button toggles `.is-collapsed` which scales and fades the text.
8. The voice button remains clickable. Wire it to call `audio.playMonstosPreview()` with the active Monstos piece type (this function already exists in `audio.ts` but is not connected — see Additional Issue A1 below).

Acceptance:
- Lore text appears inside the drawn speech bubble outline.
- No fake white patches or mask shapes are visible.
- Long descriptions scroll within the bubble bounds.
- Collapse/expand is smooth and artifact-free.
- The voice button plays the Monstos preview sound.

---

## Phase 4: Scoreboard Tabs

**Issues addressed:** #5 (scoreboard tabs)

**Depends on:** Phase 1B (region map positions the tab buttons)

Files to modify:
- `enhanced/src/styles.css`
- `enhanced/src/ui/homeMenu.ts`

Work:
1. The region map positions the two tab buttons precisely over the drawn buttons.
2. Add a visible active-tab indicator that works over the art:
   - When `.is-selected`, apply a subtle bottom-border highlight or a semi-transparent underline glow.
   - Use a color that complements the art (e.g., `rgba(255, 255, 255, 0.6)` underline).
   - Do NOT use opaque backgrounds that would cover the art.
3. Verify the leaderboard grid (`home-score-grid`) is positioned by the region map to align with the drawn scoreboard area.
4. Verify clicking the left tab shows X(4)-LINES (sprint) records and the right tab shows OGBM (arcade) records.

Acceptance:
- Tabs are aligned to the drawn buttons.
- The active tab has a clear visual indicator.
- Switching tabs updates the leaderboard data immediately.

---

## Phase 5: Game State Machine

**Issues addressed:** #6 (game start/retry/return-home flow)

**Priority:** HIGH — blocking visual verification (Phase 7)

Files to modify:
- `enhanced/src/main.ts` (major refactor of flow logic)
- `enhanced/src/types.ts` (add `AppPhase` type)
- `enhanced/src/ui/render.ts` (simplify overlay logic to use phase)

### Step 5A: Define The State Machine

Add to `types.ts`:
```typescript
export type AppPhase = 'menu' | 'countdown' | 'playing' | 'game-over' | 'sprint-clear';
```

### Step 5B: Refactor main.ts Transitions

Replace the current implicit state tracking (`appView`, boolean checks on `state.startTime`, `state.gameOver`, `state.sprintComplete`) with an explicit `appPhase` variable and a `transitionTo(phase)` function.

Transition rules:
```
menu -> countdown        // user clicks a play button
countdown -> playing     // countdown timer expires
playing -> game-over     // top-out detected
playing -> sprint-clear  // 40 lines cleared in sprint mode
game-over -> countdown   // user clicks Retry
sprint-clear -> countdown // user clicks Retry
game-over -> menu        // user clicks Home
sprint-clear -> menu     // user clicks Home
countdown -> menu        // user clicks Home during countdown
playing -> menu          // user clicks Home during play
```

Entry logic for each phase:
- `menu`: hide game shell, show home screen, reset game state, clear input timers.
- `countdown`: show game shell, hide home screen, call `reset()`, set `countdownUntil = now + COUNTDOWN_MS`, show countdown overlay.
- `playing`: set `startTime = now`, hide overlay, start gravity.
- `game-over`: freeze board, show "TOP OUT" overlay, check for record.
- `sprint-clear`: freeze board, show "40 CLEAR" overlay, check for record.

### Step 5C: Simplify render.ts Overlay Logic

The current overlay logic in `render.ts` lines 254-279 uses nested if/else chains checking `state.startTime`, `state.gameOver`, `state.sprintComplete`. Replace with a phase-based switch:

```typescript
switch (appPhase) {
  case 'countdown':
    refs.overlay.textContent = countdownText;
    refs.overlay.classList.remove('hidden');
    break;
  case 'playing':
    refs.overlay.classList.add('hidden');
    break;
  case 'game-over':
    refs.overlay.textContent = 'TOP OUT';
    refs.overlay.classList.remove('hidden');
    break;
  case 'sprint-clear':
    refs.overlay.textContent = '40 CLEAR';
    refs.overlay.classList.remove('hidden');
    break;
}
```

### Step 5D: Remove The In-Game Mode Select Dropdown

The mode select dropdown in the game sidebar (`<select id="modeSelect">` at `index.html` line 73) is redundant. Mode selection happens from the home menu via the three play buttons. Having a dropdown mid-game that resets the run is confusing and error-prone.

- Remove the `<select id="modeSelect">` element and its containing `.panel` from `index.html`.
- Remove the `modeSelect.addEventListener('change', ...)` handler from `main.ts`.
- Keep the mode label as a static display (`refs.modeDescription` shows the current mode name).

Acceptance:
- Starting any mode from the home menu always produces a countdown followed by a playable run.
- Retry from game-over or sprint-clear always produces a fresh countdown.
- Home from any game phase returns to the menu cleanly.
- Re-entering a different mode from the menu works without stuck state.
- No mode-select dropdown exists in the game screen.

---

## Phase 6: In-Game Backdrop And Controls

**Issues addressed:** #7 (in-game backdrop)

**Depends on:** Phase 1B (region map for in-game screen)

Files to modify:
- `enhanced/src/styles.css`
- `enhanced/index.html`

Work:
1. The in-game background is already `monstacka-background.png` via `.game-artboard`.
2. The three top-right buttons (settings, quit, home) are now positioned by the region map from Phase 1B.
3. Verify the `game-ui` container (the board, sidebar, and secondary sidebar) is positioned to avoid overlapping the drawn decorative elements. The current `inset: 16% 8% 10% 8%` may need adjustment after the region map is in place.
4. The quit button currently calls `window.close()` which only works in Tauri. Add a fallback: if `window.close()` doesn't work (browser context), navigate to a "thanks for playing" message or simply return to menu.

Acceptance:
- The in-game background is the user's drawing.
- Settings, quit, and home buttons are precisely aligned to the drawn icons.
- The game board and sidebar do not overlap decorative art elements.
- Quit works in both Tauri and browser contexts.

---

## Phase 7: Animation Verification

**Issues addressed:** #4 (preview animation), #8 (in-game monster animations)

**Depends on:** Phase 2 (preview wheel working), Phase 5 (game flow stable)

Files to modify:
- `enhanced/src/main.ts` (add debug mode)
- `enhanced/src/ui/render.ts` (verify animation code paths)
- `enhanced/src/ui/monsterDom.ts` (verify eye/tongue/squish logic)

### Step 7A: Add Debug Overlay Mode

Add a check in `init()`:
```typescript
const debugMode = new URLSearchParams(window.location.search).has('debug');
```

When `debugMode` is true:
- Add `outline: 2px solid red` to all `.art-hit` elements on the home screen.
- Add `outline: 2px solid blue` to all `.art-hit` elements on the game screen.
- Add a small corner HUD showing: `appPhase`, FPS, and active piece type.
- Highlight cells that have active eye animations with a faint green glow.
- Highlight cells that have active tongue animations with a faint purple glow.

### Step 7B: Verify Animation Code Paths

Walk through the animation pipeline and verify each effect fires during gameplay:

1. **Eye motion** (`render.ts` lines 94-95):
   - `lookX` and `lookY` are calculated from `sin/cos(now)` and piece position.
   - These are passed to `populateMonsterCell()` which sets `--look-x` and `--look-y` CSS variables.
   - Verify: eyes should visibly track the active falling piece.

2. **Blinking** (`monsterDom.ts` `blinkAmount()`):
   - Uses seed-based timing with 3200-4100ms periods.
   - Only applies to pieces in the `BLINK_FAMILIES` set (S=red, J=pink, L=orange).
   - Verify: those three piece colors blink periodically; other pieces never blink.

3. **Tongue** (`monsterDom.ts`):
   - Only the T-piece has a tongue spec in `MONSTER_SPECS`.
   - Sway animation at 520ms period via `--tongue-sway` CSS variable.
   - Verify: purple T-pieces show a subtle tongue motion; no other pieces show tongues.

4. **Squish** (`monsterDom.ts`):
   - Applied based on `occupiedNeighbors` (left/right/up/down).
   - Sets `--squish-scale-x`, `--squish-scale-y`, `--squish-shift-x`, `--squish-shift-y`.
   - Verify: stacked pieces visually compress slightly where they touch neighbors.

### Step 7C: Home Menu Animation Verification

With the preview wheel rebuilt (Phase 2):
1. Center Monstos plays the `monstos-preview-float` keyframe animation (bobbing up/down).
2. Center Monstos eyes blink (if in blink family) and look around.
3. Side Monstos are static — no float, no eye motion.
4. Cycling to a new Monstos transfers the animation to the new center.

Acceptance:
- Debug mode visually confirms all hitboxes and animations.
- During a real playable run, eye tracking, blinking, tongue, and squish are all visually confirmed.
- Home menu center Monstos animates clearly; sides do not.

---

## Phase 8: Polish And QA

**Issues addressed:** #9 (fragile UI strategy — final verification)

**Depends on:** All previous phases.

Work:
1. Remove all dead code from the old hitbox system (any remaining `.hit-*` classes, unused CSS rules).
2. Remove the `home-lore-mask` element and all related CSS.
3. Run the full Vitest test suite (`npm run test` in `enhanced/`).
4. Manual QA checklist:
   - [ ] Launch from `Start-Tetris.cmd` — both options work.
   - [ ] Home menu: all 7 Monstos cycle correctly with name, lore, and preview.
   - [ ] Home menu: score tabs switch leaderboard data.
   - [ ] Home menu: play buttons start the correct mode.
   - [ ] Home menu: settings gear opens the settings modal.
   - [ ] Home menu: quit button closes the app (or returns to menu in browser).
   - [ ] Gameplay: countdown fires, then pieces start falling.
   - [ ] Gameplay: all controls work (move, rotate, hold, hard drop, soft drop).
   - [ ] Gameplay: monster sprites show on locked pieces with eyes, tongue, and squish.
   - [ ] Gameplay: line clears have flash animation.
   - [ ] Gameplay: top-out shows overlay, retry works, home works.
   - [ ] Gameplay (sprint): 40-line clear shows overlay, record modal appears if qualifying.
   - [ ] Gameplay (training): finesse faults show toast, redo mode restores piece.
   - [ ] Gameplay (arcade): gravity increases with lines cleared.
   - [ ] Settings: DAS/ARR/lock delay changes apply.
   - [ ] Settings: audio controls work (mute, SFX volume, music volume).
   - [ ] Resize: layout scales correctly at 1920x1080, 1280x720, and 960x540.
   - [ ] Debug mode (`?debug=1`): all hitboxes and animations are highlighted.
5. Fix any issues found during QA.
6. Update `README.md` if any launch behavior or controls changed.
7. Update `CHANGELOG.md` — replace the 3 stale "+E+RIS" references with "MonStacka!".

Acceptance:
- All items on the QA checklist pass.
- No visual regressions from the old layout.
- The region map is the single source of truth for all interactive element positions.
- The state machine governs all app flow transitions.

---

## Additional Issues Found During Review

These were not in the original user-reported list but were discovered by inspecting the codebase. They should be addressed during the relevant phase.

### A1: Voice Button Is A Dead Click (fix in Phase 3)

**File:** `enhanced/src/main.ts` line 237-239

The voice button handler currently does nothing but re-render:
```typescript
homeRefs.monstosVoiceButton.addEventListener('click', () => {
  renderCurrentView();
});
```

But `audio.ts` already has a `playMonstosPreview(pieceType, settings)` method that generates a unique sound pattern for each Monstos. Wire it up:
```typescript
homeRefs.monstosVoiceButton.addEventListener('click', () => {
  const active = getActiveMonstos(homeState);
  audio.playMonstosPreview(active.pieceType, settings);
  renderCurrentView();
});
```

### A2: No Visual Indicator For Active Score Tab (fix in Phase 4)

**File:** `enhanced/src/styles.css` lines 406-410

The `.is-selected` class currently removes all visual feedback:
```css
.hit-home-score-sprint.is-selected,
.hit-home-score-arcade.is-selected {
    outline: none;
    box-shadow: none;
}
```

Replace with a subtle underline glow or brightness shift that works over the art:
```css
.hit-home-score-sprint.is-selected,
.hit-home-score-arcade.is-selected {
    box-shadow: 0 3px 0 0 rgba(255, 255, 255, 0.7);
}
```

### A3: Quit Button Fails In Browser Context (fix in Phase 6)

**File:** `enhanced/src/main.ts` lines 215-217

`window.close()` is blocked by browsers for security. It only works in Tauri or when the window was opened by script. Add a fallback:
```typescript
const quitGame = () => {
  try { window.close(); } catch {}
  // If still here, window.close() was blocked — return to menu instead
  setTimeout(() => returnToMenu(), 100);
};
```

### A4: Home Menu Re-renders At 60fps Even When Nothing Changes (fix in Phase 5)

**File:** `enhanced/src/main.ts` lines 307-312

The `tick()` function calls `renderCurrentView(now)` on every animation frame, including when `appView === 'menu'`. The home menu only needs to re-render when:
- The user cycles Monstos
- The user toggles lore
- The user switches score tabs

Fix: track a `menuDirty` flag. Set it to `true` on user interaction. In `tick()`, skip menu rendering if `menuDirty === false`. Exception: the center Monstos float animation needs `requestAnimationFrame` — extract just the animation update to a lightweight function that only touches the preview canvas, not the full DOM.

### A5: CHANGELOG Has 3 Stale "+E+RIS" References (fix in Phase 8)

**File:** `CHANGELOG.md` lines 13, 15, 16

Replace "+E+RIS" with "MonStacka!" in these historical entries.

### A6: No Loading State For Monster Sprite Asset (fix in Phase 2 or 8)

**File:** `enhanced/src/main.ts` line 186

`prepareMonsterSkin()` loads the 764KB sprite sheet asynchronously. Until it finishes, any Monstos rendering will show empty containers. There is no loading indicator or fallback.

Fix: show a brief "Loading..." text in the center preview slot until the sprite promise resolves. Then trigger the first full render.

### A7: Game Sidebar Visual Style Clashes With Art (consider for future)

The in-game sidebar (mode panel, score panel, hold/next panel, leaderboard panel) uses generic translucent dark `.panel` styling that does not match the hand-drawn art style. This is a lower-priority polish issue — the panels are functional and readable. But for full art integration, consider:
- Reducing panel opacity to let the background art show through more.
- Using softer border-radius to match the drawn style.
- Or accepting the current panel style as the "game HUD" aesthetic distinct from the art.

This is NOT blocking and should not be addressed until all other issues are resolved.

---

## Execution Order Summary

```text
Phase 1: Asset Cleanup + Region Map     (Issues #1, #2, #3, #7, #9)
  |-- 1A: Create clean base art
  +-- 1B: Build region map, replace CSS hitboxes

Phase 2: Preview Wheel Rebuild          (Issues #2, #4)
  |-- 2A: Clean preview rendering
  +-- 2B: Arrow cycling polish

Phase 3: Speech Bubble + Lore Fix       (Issue #3, A1)
  +-- Wire voice button, remove masks, fix text sizing

Phase 4: Scoreboard Tabs                (Issue #5, A2)
  +-- Position from region map, add active indicator

Phase 5: Game State Machine             (Issue #6, A4)
  |-- 5A: Define AppPhase type
  |-- 5B: Refactor main.ts transitions
  |-- 5C: Simplify render.ts overlay logic
  +-- 5D: Remove in-game mode dropdown

Phase 6: In-Game Backdrop + Controls    (Issue #7, A3)
  +-- Verify alignment, fix quit fallback

Phase 7: Animation Verification         (Issues #4, #8)
  |-- 7A: Debug overlay mode
  |-- 7B: In-game animation verification
  +-- 7C: Home menu animation verification

Phase 8: Polish + QA                    (Issue #9, A5, A6)
  +-- Dead code removal, test suite, manual QA checklist
```

## Important Constraint

Do not continue with ad-hoc percentage nudging. Every position change must go through `regionMap.ts`. If a button is misaligned, the fix is updating its pixel coordinates in the region map — not editing CSS percentages.
