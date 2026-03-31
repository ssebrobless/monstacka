# MonStacka Next Implementation Plan

This is the concrete implementation plan for Codex. Each phase is ordered by dependency, specifies exact files and actions, and includes verification steps. Start from Phase 1 and work downward. Do not skip phases.

## Current State Summary

What is already working:
- Region map system (`regionMap.ts`) positions all interactive elements from pixel coordinates
- Clean base art (`monstacka-home-menu-clean.png`) has baked sample content removed
- Preview wheel renders Monstos into empty circles with `clip-path` containment
- Voice button plays per-Monstos audio preview via `audio.playMonstosPreview()`
- Lore toggle collapses/expands with CSS transitions
- Score tabs switch leaderboard data with `inset box-shadow` active indicator
- Monster animations (eyes, tongue, squish, blinking) exist in the rendering pipeline
- Tauri desktop scaffolding is fully configured (`src-tauri/`, `tauri.conf.json`, npm scripts)
- Vitest test suite exists for game engine logic
- All three game modes work (Arcade, 40 Lines, Training with finesse detection)
- Gravity curve implemented for Arcade mode

What still needs work:
- Game-state flow uses a 2-state (`menu`/`game`) model instead of an explicit phase machine — countdown, playing, game-over, and sprint-clear are inferred from booleans
- In-game mode select dropdown still exists, allowing confusing mid-game mode switching
- Lore mask element and its CSS pseudo-elements are still in the DOM/CSS despite the clean base art making them unnecessary
- Quit button calls `window.close()` with no fallback for browser context
- Home menu re-renders at 60fps even when nothing has changed
- Region map coordinates have not been formally verified against the source art — user reports alignment issues persist
- In-game layout composition has not been verified against the background art
- Monster animations have not been visually confirmed during stable gameplay
- No debug overlay mode exists for visual verification
- CHANGELOG has 3 stale "+E+RIS" references (lines 13, 15, 16)
- No loading indicator while the 764KB sprite sheet loads

---

## Phase 1: Region Map Calibration And Art Verification

**Objective:** Ensure every interactive element is precisely aligned to the drawn controls in both artboards. This is the single most impactful fix — if the controls don't line up, nothing else matters.

**Files:**
- `enhanced/src/ui/regionMap.ts`
- `enhanced/src/assets/ui/monstacka-home-menu-clean.png` (reference only — do not modify)
- `enhanced/src/assets/ui/monstacka-background.png` (reference only — do not modify)

**Actions:**

1. Open `monstacka-home-menu-clean.png` in an image viewer or canvas at native 1920x1080. For every region in `HOME_REGIONS`, verify that the pixel coordinates (`x`, `y`, `width`, `height`) precisely cover the drawn control they represent. Use the image's pixel grid — do not eyeball percentages.

2. The regions to audit (current coordinates shown for reference):

   **Home screen — top-right controls:**
   | Region | Current x,y,w,h | What it should cover |
   |--------|-----------------|---------------------|
   | `home-settings` | 1747, 18, 123, 97 | The gear/settings icon drawn in the top-right |
   | `home-quit` | 1747, 127, 123, 99 | The X/close icon below the gear |

   **Home screen — Monstos area:**
   | Region | Current x,y,w,h | What it should cover |
   |--------|-----------------|---------------------|
   | `home-name-fill` | 154, 434, 300, 62 | The name banner rectangle |
   | `home-voice` | 484, 443, 88, 66 | The speaker/voice icon near the name |
   | `home-lore` | 541, 477, 100, 82 | The lore toggle button |
   | `home-lore-fill` | 741, 353, 480, 149 | The interior of the drawn speech bubble |
   | `home-preview-left` | 42, 752, 315, 257 | The left preview circle outline |
   | `home-preview-center` | 230, 531, 522, 445 | The center preview circle outline |
   | `home-preview-right` | 609, 782, 353, 266 | The right preview circle outline |
   | `home-prev` | 123, 683, 261, 164 | The left arrow button |
   | `home-next` | 588, 684, 276, 164 | The right arrow button |

   **Home screen — scoreboard area:**
   | Region | Current x,y,w,h | What it should cover |
   |--------|-----------------|---------------------|
   | `home-score-sprint` | 975, 589, 123, 59 | The left score tab under the crown |
   | `home-score-arcade` | 1117, 589, 136, 59 | The right score tab under the crown |
   | `home-score-grid` | 985, 821, 250, 200 | The scoreboard display area |

   **Home screen — play buttons:**
   | Region | Current x,y,w,h | What it should cover |
   |--------|-----------------|---------------------|
   | `home-play-arcade` | 1384, 605, 422, 111 | The top play button (OG Bubba Mode) |
   | `home-play-sprint` | 1384, 762, 422, 113 | The middle play button (X(4)-LINES) |
   | `home-play-training` | 1384, 919, 422, 112 | The bottom play button (Training) |

3. Open `monstacka-background.png` at native 1920x1080. Audit the `GAME_REGIONS`:

   | Region | Current x,y,w,h | What it should cover |
   |--------|-----------------|---------------------|
   | `game-settings` | 1780, 11, 100, 89 | The gear icon drawn in the top-right |
   | `game-quit` | 1780, 95, 102, 93 | The X/close icon |
   | `game-home` | 1784, 180, 96, 93 | The home icon |
   | `game-ui` | 154, 173, 1613, 799 | The main game area (board + sidebars) |

4. For each region where the coordinates are off, update the values in `regionMap.ts`. The fix is always: change the numbers in the `HOME_REGIONS` or `GAME_REGIONS` arrays. Never add position rules to CSS.

5. Check the `home-lore-mask` region — this element is no longer needed with the clean base art. It should be removed entirely in Phase 2. For now, verify that `home-lore-fill` covers the speech bubble interior correctly.

**Verification:**

1. Run `npm run dev` in `enhanced/`.
2. Open the app in a browser at roughly 1920x1080 (or use DevTools device emulation).
3. Open DevTools, hover over each `[data-region]` element, and visually confirm its bounding box aligns with the drawn control in the background art.
4. Resize the window to 1280x720 and 960x540. Confirm the hitboxes still align (they scale proportionally via the region map percentages within the `aspect-ratio: 16/9` artboard).
5. Click every button on both screens and confirm it activates in the expected area — not offset to the side.

**Risk:** The art may not be exactly 1920x1080 or may have slight padding/margins. If the PNG dimensions differ, update `ARTBOARD_WIDTH` and `ARTBOARD_HEIGHT` in `regionMap.ts` to match the actual image dimensions.

---

## Phase 2: DOM Cleanup — Remove Dead Overlay Hacks

**Objective:** Remove leftover elements and CSS from the pre-clean-art era that are no longer needed. This reduces visual artifacts and makes future changes safer.

**Files:**
- `enhanced/index.html`
- `enhanced/src/styles.css`
- `enhanced/src/ui/homeMenu.ts`
- `enhanced/src/ui/regionMap.ts`

**Actions:**

1. **Remove the lore mask element.** In `index.html` line 20, delete:
   ```html
   <div id="monstosLoreMask" class="home-lore-mask" data-region="home-lore-mask" aria-hidden="true"></div>
   ```

2. **Remove the lore mask CSS.** In `styles.css`, delete the `.home-lore-mask` block (lines 109-138) including the `::before` and `::after` pseudo-elements.

3. **Remove the lore mask from the region map.** In `regionMap.ts`, delete the `home-lore-mask` entry from `HOME_REGIONS`.

4. **Remove the lore mask ref and toggle.** In `homeMenu.ts`:
   - Remove `monstosLoreMask` from the `HomeMenuRefs` interface.
   - Remove the `refs.monstosLoreMask` line in `getHomeMenuRefs()`.
   - Remove the `refs.monstosLoreMask.classList.toggle(...)` line in `renderHomeMenu()`.

5. **Remove the white background patch from lore fill.** In `styles.css`, delete the `.home-lore-fill::before` rule (lines 152-159). The speech bubble outline is already in the clean base art — we just need text inside it, not a white rectangle.

6. **Verify the lore text still appears correctly** inside the speech bubble without the mask or white patch. If the text needs a subtle background for readability against the clean art, add a minimal `background: rgba(255, 255, 255, 0.85)` directly to `.home-lore-fill` instead of using a pseudo-element.

**Verification:**
- The speech bubble area shows lore text with no mask shapes or white patches visible.
- Toggling lore open/closed works smoothly.
- No console errors from missing DOM elements.

---

## Phase 3: Game State Machine

**Objective:** Replace the implicit 2-state model with an explicit `AppPhase` state machine so that game flow transitions are predictable and never leave the app in a stuck state.

**Files:**
- `enhanced/src/types.ts`
- `enhanced/src/main.ts`
- `enhanced/src/ui/render.ts`
- `enhanced/index.html`

**Actions:**

### 3A: Define AppPhase type

In `types.ts`, add:
```typescript
export type AppPhase = 'menu' | 'countdown' | 'playing' | 'game-over' | 'sprint-clear';
```

### 3B: Refactor main.ts

Replace `appView: AppView` with `appPhase: AppPhase`. Create a `transitionTo(phase: AppPhase)` function that encapsulates all entry/exit logic:

```typescript
function transitionTo(nextPhase: AppPhase): void {
  // Exit current phase
  switch (appPhase) {
    case 'menu':
      homeScreen.classList.add('hidden');
      break;
    case 'countdown':
    case 'playing':
    case 'game-over':
    case 'sprint-clear':
      // no special exit needed for these
      break;
  }

  // Enter next phase
  switch (nextPhase) {
    case 'menu':
      closeRecordModal();
      closeSettingsModal();
      doReset(state.mode);
      gameShell.classList.add('hidden');
      homeScreen.classList.remove('hidden');
      break;
    case 'countdown':
      gameShell.classList.remove('hidden');
      doReset(state.mode);
      // countdownUntil is set by reset() via createGameState
      break;
    case 'playing':
      state.startTime = state.countdownUntil;
      state.lastGravity = state.startTime;
      break;
    case 'game-over':
      doRecordCheck();
      break;
    case 'sprint-clear':
      doRecordCheck();
      break;
  }

  appPhase = nextPhase;
}
```

Replace all direct state manipulations:
- `startMode(mode)`: calls `doReset(mode)` then `transitionTo('countdown')`
- `returnToMenu()`: calls `transitionTo('menu')`
- Retry button: calls `transitionTo('countdown')`
- In `tick()`: when countdown expires → `transitionTo('playing')`
- In `tick()`: when `state.gameOver && state.sprintComplete` → `transitionTo('sprint-clear')`
- In `tick()`: when `state.gameOver && !state.sprintComplete` → `transitionTo('game-over')`

The `tick()` function becomes a clean phase-based switch:
```typescript
function tick(now: number) {
  switch (appPhase) {
    case 'menu':
      renderCurrentView(now);
      break;
    case 'countdown':
      // countdown audio + timer
      if (now >= state.countdownUntil) transitionTo('playing');
      renderCurrentView(now);
      break;
    case 'playing':
      // gravity, lock deadline, sound effects
      if (state.gameOver) {
        transitionTo(state.sprintComplete ? 'sprint-clear' : 'game-over');
      }
      renderCurrentView(now);
      break;
    case 'game-over':
    case 'sprint-clear':
      renderCurrentView(now);
      break;
  }
  requestAnimationFrame(tick);
}
```

### 3C: Simplify render.ts overlay logic

Pass `appPhase` to the render function (or export it from main). Replace the nested boolean chains (lines 254-279 in `render.ts`) with a phase switch:

```typescript
switch (appPhase) {
  case 'countdown': {
    const count = Math.ceil(Math.max(0, state.countdownUntil - now) / 1000);
    refs.overlay.textContent = count > 0 ? String(count) : 'GO';
    refs.overlay.classList.remove('hidden');
    break;
  }
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

### 3D: Remove the in-game mode select dropdown

In `index.html`, remove the mode panel (lines 69-80):
```html
<div class="panel">
    <h2>Mode</h2>
    <label class="mode-label">
        <span>Select</span>
        <select id="modeSelect">
            <option value="arcade">Arcade</option>
            <option value="sprint40">40 Lines</option>
            <option value="training">Training</option>
        </select>
    </label>
    <p id="modeDescription" class="status-text">...</p>
</div>
```

Replace it with a static mode display:
```html
<div class="panel">
    <h2>Mode</h2>
    <p id="modeDescription" class="status-text">Play until you top out. Chase the highest score you can post.</p>
</div>
```

In `main.ts`:
- Remove the `modeSelect` variable and its `getElementById` line.
- Remove the `modeSelect.addEventListener('change', ...)` handler.
- Remove the `modeSelect.value = nextMode` line in `doReset()`.
- Remove the `refs.modeSelect.value = state.mode` line in `render.ts`.
- Remove `modeSelect` from `DomRefs` in `render.ts`.

### 3E: Optimize home menu rendering

The `tick()` function currently calls `renderCurrentView(now)` every frame even on the menu. The home menu only changes on user interaction, except for the center Monstos float animation which is CSS-only (the `monstos-preview-float` keyframe).

In the `'menu'` case of `tick()`, skip rendering entirely:
```typescript
case 'menu':
  // Home menu is static except CSS animations — no per-frame render needed.
  // Re-render is triggered by user interactions (cycle, toggle, tab click).
  break;
```

Remove the `renderCurrentView(now)` call from the menu branch. The home menu is already rendered on every user interaction (every click handler calls `renderCurrentView()`). The CSS `monstos-preview-float` animation runs independently of JS rendering.

**Verification:**
- Start Arcade from home menu → countdown → pieces fall → top out → "TOP OUT" overlay → click Retry → fresh countdown → pieces fall again.
- Start 40 Lines → clear 40 lines → "40 CLEAR" overlay → record modal if qualifying → click Home → home menu shows.
- Start Training → play a few pieces → click Home → home menu shows, no stuck state.
- Re-enter a different mode → fresh countdown, no leftover state from previous mode.
- No mode select dropdown visible in the game screen.
- Home menu does not re-render at 60fps (check with DevTools Performance tab).

---

## Phase 4: Quit Button Browser Fallback

**Objective:** Make the quit button work in both Tauri and browser contexts.

**Files:**
- `enhanced/src/main.ts`

**Actions:**

Replace the `quitGame` function (line 226-228):
```typescript
const quitGame = () => {
  try { window.close(); } catch { /* ignored */ }
  // If window.close() was blocked (browser context), return to menu
  setTimeout(() => {
    if (!document.hidden) returnToMenu();
  }, 100);
};
```

**Verification:**
- In Tauri: quit button closes the window.
- In browser: quit button returns to the home menu instead of silently failing.

---

## Phase 5: In-Game Layout Composition

**Objective:** Verify that the game board, sidebars, and HUD sit correctly within the in-game artboard without overlapping decorative elements.

**Files:**
- `enhanced/src/ui/regionMap.ts`
- `enhanced/src/styles.css`

**Actions:**

1. The `game-ui` region is currently `{ x: 154, y: 173, width: 1613, height: 799 }`. This positions the entire game area (board + two sidebars) via the region map. Open the in-game background art and verify:
   - The board column does not overlap any drawn decorative elements (borders, character art, etc.).
   - The sidebar panels sit in an area that is visually clear in the art (likely a darker or plain region meant for game HUD).
   - The top-right control buttons (gear, quit, home) are not overlapped by sidebar content.

2. If the `game-ui` region needs adjustment, update its coordinates in `regionMap.ts`.

3. The `.game-ui` CSS currently uses a `grid-template-columns: minmax(280px, 360px) minmax(260px, 300px) minmax(260px, 300px)`. This 3-column layout must fit within the `game-ui` region at the artboard's aspect ratio. If the region is too narrow for 3 columns, either:
   - Widen the `game-ui` region.
   - OR reduce the sidebar minimum widths.
   - OR collapse to 2 columns by default with the secondary sidebar below.

4. The `.game-ui` CSS no longer has an `inset` property (it was removed when the region map was adopted). Verify that the region map's inline styles (`left`, `top`, `width`, `height`) are actually taking effect. If `.game-ui` has a conflicting `position` or layout rule, fix it.

5. Consider reducing the panel background opacity from `rgba(8, 14, 30, 0.72)` to `rgba(8, 14, 30, 0.56)` so the background art shows through slightly. This is a minor polish item — only do it if it improves visual coherence with the art.

**Verification:**
- The board is fully visible and not clipped by the artboard edges.
- Sidebars don't overlap the decorative border art.
- Top-right control buttons are accessible (not covered by sidebar content).
- At 1280x860 (the Tauri default window size), all game elements fit without horizontal scrollbar.

---

## Phase 6: Debug Overlay Mode

**Objective:** Add a developer verification mode that makes hitboxes and animation state visible, allowing the user to confirm alignment and animation behavior without reading code.

**Files:**
- `enhanced/src/main.ts`
- `enhanced/src/styles.css`

**Actions:**

1. In `main.ts` `init()`, add:
   ```typescript
   const debugMode = new URLSearchParams(window.location.search).has('debug');
   if (debugMode) document.body.classList.add('debug-mode');
   ```

2. In `styles.css`, add:
   ```css
   .debug-mode .art-hit {
       outline: 2px solid rgba(255, 50, 50, 0.7) !important;
       background: rgba(255, 50, 50, 0.08) !important;
   }

   .debug-mode .preview-slot {
       outline: 2px dashed rgba(50, 255, 50, 0.6);
   }

   .debug-mode .home-lore-fill {
       outline: 2px dashed rgba(50, 50, 255, 0.6);
   }

   .debug-mode .home-name-fill {
       outline: 2px dashed rgba(255, 255, 50, 0.6);
   }

   .debug-mode .home-score-grid {
       outline: 2px dashed rgba(255, 50, 255, 0.6);
   }

   .debug-mode [data-region="game-ui"] {
       outline: 2px dashed rgba(50, 255, 255, 0.5);
   }
   ```

3. Optionally, add a small corner label showing the current `appPhase`:
   ```typescript
   if (debugMode) {
     const debugLabel = document.createElement('div');
     debugLabel.id = 'debugLabel';
     debugLabel.style.cssText = 'position:fixed;top:4px;left:4px;z-index:999;background:rgba(0,0,0,0.7);color:#0f0;font:12px monospace;padding:4px 8px;border-radius:4px;pointer-events:none';
     document.body.appendChild(debugLabel);
     // Update in tick():
     // debugLabel.textContent = `phase: ${appPhase}`;
   }
   ```

**Verification:**
- Open the app with `?debug=1` in the URL.
- All hitboxes are outlined in red on the home screen and game screen.
- Preview slots are outlined in green.
- The lore bubble, name banner, and score grid are outlined in distinct colors.
- The current `appPhase` is displayed in the corner.
- Opening the app without `?debug=1` shows no debug outlines.

---

## Phase 7: Monster Animation Verification

**Objective:** Using the debug mode from Phase 6 and the stable game flow from Phase 3, systematically verify that all monster animations fire correctly during real gameplay.

**Files:** No code changes expected — this is a verification phase. If issues are found, fix them in the relevant files.

**Verification checklist:**

### Home menu animations:
- [ ] Center Monstos floats (bobbing up/down via `monstos-preview-float` CSS animation).
- [ ] Center Monstos has `is-active` class applied.
- [ ] Side Monstos do NOT float or have `is-active`.
- [ ] Cycling left/right transfers the float animation to the new center.
- [ ] Preview sprites are fully contained within the circles (no bleed past `clip-path`).

### In-game animations — eyes:
- [ ] Locked pieces on the board show eyes (white circles with dark pupils).
- [ ] Pupils track the active falling piece position (shift left/right/up/down as the piece moves).
- [ ] Eye tracking uses the `--look-x` and `--look-y` CSS variables.
- [ ] Confirm by moving a piece far left, then far right — pupils should visibly shift.

### In-game animations — blinking:
- [ ] S-piece (red/SORRISOL) locked on board → eyes blink periodically.
- [ ] J-piece (pink/DOUSEMA) → eyes blink.
- [ ] L-piece (orange/GALIFFAMBOS) → eyes blink.
- [ ] I-piece (cyan/BLYNDOOLIE) → eyes do NOT blink (not in blink family).
- [ ] T-piece (purple/LYSERGICADA) → eyes do NOT blink.
- [ ] Z-piece (green/AGGRASO) → eyes do NOT blink.
- [ ] O-piece (yellow/MUWERDE) → no eyes at all.
- [ ] Blinking uses `--blink` CSS variable with `scaleY` transform on the eye.

### In-game animations — tongue:
- [ ] T-piece locked on board shows a subtle purple tongue element.
- [ ] Tongue sways side-to-side via `--tongue-sway` CSS variable.
- [ ] No other piece type shows a tongue.

### In-game animations — squish:
- [ ] Two pieces stacked vertically → slight vertical compression where they touch.
- [ ] Two pieces side by side → slight horizontal compression at the shared edge.
- [ ] Isolated pieces (no neighbors) → no squish deformation.
- [ ] Squish uses `--squish-scale-x`, `--squish-scale-y`, `--squish-shift-x`, `--squish-shift-y` CSS variables.

### If any animation fails:
- Check the relevant code in `monsterDom.ts` (`populateMonsterCell`, `blinkAmount`, squish calculation).
- Check `monsterSkin.ts` (`MONSTER_SPECS`) for correct eye/tongue definitions per piece type.
- Check `render.ts` for correct `lookX`/`lookY` calculation and passing to `populateMonsterCell`.
- Fix and re-verify.

---

## Phase 8: Player Flow Polish

**Objective:** Ensure the end-to-end player experience is clean for all three modes.

**Files:**
- `enhanced/src/ui/render.ts`
- `enhanced/src/main.ts`

**Actions:**

1. **Verify record modal titles.** The record modal in `index.html` has a hardcoded `<h2>Arcade Entry</h2>` (line 156). This should dynamically update based on the mode:
   - Arcade: "Arcade Entry"
   - Sprint: "Sprint Entry"
   - Training: should never show (already guarded by `if (state.mode === 'training') return` in `doRecordCheck`)

   In `openRecordModal()`, update the modal `<h2>` text based on `record.mode`:
   ```typescript
   const modalTitle = recordModal.querySelector('h2')!;
   modalTitle.textContent = record.mode === 'sprint40' ? 'Sprint Entry' : 'Arcade Entry';
   ```

2. **Verify training mode messaging.** In `render.ts`, the training status text shows fault rate and feedback mode. Confirm:
   - Faults counter increments when a finesse fault occurs.
   - Perfect streak resets on a fault.
   - "Show" mode flashes the fault toast with input count comparison.
   - "Redo" mode restores the piece to spawn.

3. **Verify arcade gravity progression.** During an Arcade run:
   - Start: pieces fall at 650ms.
   - After 10 lines: pieces fall noticeably faster (500ms).
   - After 40 lines: 220ms.
   - After 140 lines: near-instant (33ms).

4. **Verify sprint completion flow.** Clear 40 lines in sprint mode:
   - "40 CLEAR" overlay appears.
   - Timer stops.
   - If qualifying time, record modal appears with "Sprint Entry" title.
   - Nickname submission updates the home screen leaderboard.

**Verification:**
- Play each mode to completion or game-over. Confirm the overlay, record modal, and return-to-menu flow work without stuck states.

---

## Phase 9: CHANGELOG And Documentation Cleanup

**Objective:** Fix stale references and update documentation to match the current state.

**Files:**
- `CHANGELOG.md`
- `README.md`

**Actions:**

1. **Fix CHANGELOG stale references.** Replace "+E+RIS" with "MonStacka!" on lines 13, 15, and 16:
   - Line 13: "...final two-choice launcher for `HTML` and `MonStacka!`."
   - Line 15: "Added an `enhanced/` scaffold with TypeScript/Vite project structure and a launchable `MonStacka!` preview build."
   - Line 16: "Added the first `MonStacka!` control-feel pass..."

2. **Add a new CHANGELOG entry** for all the work done since 2026-03-29:
   - Region map system for coordinate-based layout
   - Clean base art asset
   - Monster sprite integration (eyes, tongue, squish, blinking)
   - Monstos preview wheel with carousel cycling
   - Lore toggle and voice preview audio
   - Score tab switching with active indicator
   - Training mode with finesse detection (Show/Redo)
   - Arcade gravity curve
   - Tauri desktop packaging
   - Vitest engine test suite
   - AppPhase state machine (if implemented in Phase 3)
   - Debug overlay mode (if implemented in Phase 6)

3. **Verify README accuracy.** Confirm:
   - Launch instructions still work for both HTML and MonStacka!.
   - The repo layout section matches the actual directory structure.
   - Tauri build instructions (`npm run tauri:dev`, `npm run tauri:build`) are documented.
   - The mode list includes all three modes (Arcade, 40 Lines, Training).

**Verification:**
- `grep -r "+E+RIS" .` returns no results in active code files (CHANGELOG historical entries are OK if updated).
- README instructions can be followed step-by-step to launch both editions.

---

## Phase 10: Release Readiness

**Objective:** Confirm the app is ready for distribution as a standalone desktop application.

**Files:**
- `enhanced/src-tauri/tauri.conf.json`
- `enhanced/src-tauri/icons/`
- `Launcher.ps1`

**Actions:**

1. **Verify Tauri build.** Run `npm run tauri:build` from `enhanced/`. Confirm:
   - The build completes without errors.
   - A `.exe` is produced in `enhanced/src-tauri/target/release/` (or `target/release/bundle/`).
   - The `.exe` opens a native window with the MonStacka! home menu.
   - All gameplay works identically in the Tauri window as in the browser.

2. **Verify launcher detection.** Run `Start-Tetris.cmd`:
   - Select "MonStacka!" and click Run.
   - Confirm the launcher finds and opens the Tauri `.exe` if it exists.
   - If the `.exe` doesn't exist, confirm it falls back to opening `enhanced/dist/index.html` in the browser.

3. **Verify app icons.** Check that `enhanced/src-tauri/icons/` contains valid icon files:
   - `icon.ico` (Windows)
   - `icon.icns` (macOS)
   - `32x32.png`, `128x128.png`, `128x128@2x.png`
   - If these are placeholder Tauri default icons, note this as a future task (custom MonStacka! icon).

4. **Verify Tauri window configuration.** In `tauri.conf.json`:
   - Title: "MonStacka!" (correct)
   - Size: 1280x860 (reasonable for 16:9 art)
   - Min size: 1100x780 (confirm the layout doesn't break at this size)
   - Resizable: true
   - Center: true

5. **Mac build readiness.** If a Mac build is intended:
   - Confirm `npm run tauri:build` produces a `.app` or `.dmg` on macOS.
   - Confirm `Start-Tetris.command` can detect and launch the Mac binary.
   - If Mac builds are not yet tested, note it as a known gap.

**Verification:**
- MonStacka! launches as a native desktop window from the launcher.
- The window title says "MonStacka!".
- All gameplay, menus, animations, and audio work in the Tauri window.
- Closing the Tauri window exits the app cleanly.

---

## Final QA Checklist

Run through this checklist after all phases are complete:

**Home menu:**
- [ ] All buttons aligned to drawn controls (verified with `?debug=1`).
- [ ] 7 Monstos cycle correctly with name, lore, and preview.
- [ ] Center Monstos animates (float); sides do not.
- [ ] Voice button plays per-Monstos sound.
- [ ] Lore toggle opens/closes smoothly.
- [ ] Score tabs switch between Arcade and Sprint leaderboards.
- [ ] Active tab has visible indicator.
- [ ] Three play buttons start the correct modes.
- [ ] Settings gear opens the settings modal.
- [ ] Quit button works (Tauri: closes; browser: returns to menu).

**Gameplay:**
- [ ] Countdown fires, then pieces start falling.
- [ ] All controls work (move, rotate, hold, hard drop, soft drop).
- [ ] Monster sprites show on locked pieces with eyes, tongue, and squish.
- [ ] Line clears have flash animation.
- [ ] Arcade gravity increases with lines cleared.
- [ ] Top-out shows "TOP OUT" overlay.
- [ ] Sprint: 40-line clear shows "40 CLEAR" overlay.
- [ ] Training: finesse faults show toast; redo mode restores piece.
- [ ] Retry works from any game-over state.
- [ ] Home button returns to menu cleanly from any state.
- [ ] No mode select dropdown exists in the game screen.

**Settings:**
- [ ] DAS/ARR/lock delay changes apply to gameplay.
- [ ] Training feedback mode (Show/Redo/Off) changes apply.
- [ ] Audio mute toggle works.
- [ ] SFX and music volume sliders work.
- [ ] Settings persist across sessions (localStorage).

**Records:**
- [ ] Arcade: top-10 score saves with nickname.
- [ ] Sprint: top-10 time saves with nickname.
- [ ] Training: no record modal appears.
- [ ] Records show on home screen leaderboard.

**Scaling:**
- [ ] Layout correct at 1920x1080.
- [ ] Layout correct at 1280x720.
- [ ] Layout correct at 960x540.

**Desktop:**
- [ ] Tauri build produces working .exe.
- [ ] Launcher detects and opens .exe.
- [ ] Window title: "MonStacka!".

---

## Execution Order Summary

```text
Phase 1:  Region Map Calibration         (alignment — highest priority)
Phase 2:  DOM Cleanup                     (remove dead mask/patch elements)
Phase 3:  Game State Machine              (AppPhase + remove mode dropdown + optimize menu render)
Phase 4:  Quit Button Fallback            (small fix, no dependencies)
Phase 5:  In-Game Layout Composition      (verify board/sidebar positioning)
Phase 6:  Debug Overlay Mode              (enables visual verification)
Phase 7:  Monster Animation Verification  (prove all effects work in live play)
Phase 8:  Player Flow Polish              (record modal titles, mode messaging)
Phase 9:  CHANGELOG + Docs Cleanup        (fix stale refs, add new entry)
Phase 10: Release Readiness               (Tauri build, launcher, icons)
```

Codex: start from Phase 1. Complete each phase before moving to the next. Update the Status markers as you go.
