# MonStacka Next Review Plan For Claude

This document is the next planning brief for Claude.

Its purpose is:
- review what still needs improvement
- organize the remaining work into the right planning buckets
- produce a concrete implementation plan we can hand back to Codex afterward

This is not the final implementation plan.
This is the planning scope for what Claude should think through next.

## North Star

```text
╔══════════════════════════ Final MonStacka Goal ══════════════════════════╗
║ Launch MonStacka! desktop app                                           ║
║   └─ opens into the user's hand-drawn home menu                         ║
║       ├─ art is preserved                                               ║
║       ├─ controls line up exactly with the drawing                      ║
║       ├─ Monstos preview wheel works cleanly                            ║
║       ├─ lore + voice preview buttons behave correctly                  ║
║       ├─ score tabs switch correctly                                    ║
║       └─ mode buttons start real playable runs                          ║
║                                                                         ║
║ Enter gameplay                                                          ║
║   └─ uses the user's in-game background art                             ║
║       ├─ settings / quit / home icons line up correctly                 ║
║       ├─ game flow is reliable                                          ║
║       ├─ retry and return-home never get stuck                          ║
║       ├─ monster sprites visibly animate in live play                   ║
║       └─ all visual creature effects remain gameplay-neutral            ║
╚═══════════════════════════════════════════════════════════════════════════╝
```

## Current Position

```text
What exists now
├─ custom MonStacka artboards are in the repo
├─ home screen and game screen both exist
├─ Monstos profiles, lore text, and preview names exist
├─ monster animation code exists
├─ local scoreboards exist
├─ the app builds
└─ some UI controls are wired

What is still not "down packed"
├─ UI alignment is not fully trustworthy
├─ home preview wheel still needs proper cleanup/signoff
├─ lore bubble/text treatment still needs proper final handling
├─ game start/retry/home flow is not fully robust
├─ in-game animation visibility has not been properly verified
├─ in-game composition/layout still needs polish
└─ final release/readiness flow is not settled
```

## Files Claude Should Treat As Primary Context

- [MONSTACKA_ISSUES_FOR_CLAUDE.md](/C:/Users/fishe/Documents/projects/Tetris/repo_review/MONSTACKA_ISSUES_FOR_CLAUDE.md)
- [enhanced/index.html](/C:/Users/fishe/Documents/projects/Tetris/repo_review/enhanced/index.html)
- [enhanced/src/main.ts](/C:/Users/fishe/Documents/projects/Tetris/repo_review/enhanced/src/main.ts)
- [enhanced/src/styles.css](/C:/Users/fishe/Documents/projects/Tetris/repo_review/enhanced/src/styles.css)
- [enhanced/src/ui/homeMenu.ts](/C:/Users/fishe/Documents/projects/Tetris/repo_review/enhanced/src/ui/homeMenu.ts)
- [enhanced/src/ui/monsterDom.ts](/C:/Users/fishe/Documents/projects/Tetris/repo_review/enhanced/src/ui/monsterDom.ts)
- [enhanced/src/ui/render.ts](/C:/Users/fishe/Documents/projects/Tetris/repo_review/enhanced/src/ui/render.ts)
- [enhanced/src/ui/regionMap.ts](/C:/Users/fishe/Documents/projects/Tetris/repo_review/enhanced/src/ui/regionMap.ts)
- [enhanced/src/monsterSkin.ts](/C:/Users/fishe/Documents/projects/Tetris/repo_review/enhanced/src/monsterSkin.ts)
- [enhanced/src/assets/ui/monstacka-home-menu.png](/C:/Users/fishe/Documents/projects/Tetris/repo_review/enhanced/src/assets/ui/monstacka-home-menu.png)
- [enhanced/src/assets/ui/monstacka-home-menu-clean.png](/C:/Users/fishe/Documents/projects/Tetris/repo_review/enhanced/src/assets/ui/monstacka-home-menu-clean.png)
- [enhanced/src/assets/ui/monstacka-background.png](/C:/Users/fishe/Documents/projects/Tetris/repo_review/enhanced/src/assets/ui/monstacka-background.png)

## What Claude Should Review Next

### 1. Art Integration Strategy

Claude should review:
- whether the current clean home menu asset is the right long-term base
- whether any more baked sample areas still need to be removed from the art
- whether the speech bubble, name field, and preview circles now have the right neutral surfaces
- whether the in-game art needs any companion "clean" version too

Claude should decide:
- what should stay in the PNG art
- what should be dynamic overlay content
- what should be masked, clipped, or rendered inside fixed regions

Output expected from Claude:
- a final asset strategy
- any additional source-art cleanup tasks
- a list of exact regions that must remain dynamic

### 2. Region Map Calibration

Claude should review:
- whether the current [regionMap.ts](/C:/Users/fishe/Documents/projects/Tetris/repo_review/enhanced/src/ui/regionMap.ts) coordinates are correct enough
- whether all home-menu controls are represented
- whether all in-game top-right controls are represented
- whether the preview slots, name box, speech bubble, leaderboard, and play buttons are aligned to the artwork

Claude should decide:
- whether the current region-map approach is the correct final layout system
- how to calibrate and verify positions across scaling

Output expected from Claude:
- a concrete calibration plan
- acceptance checks for each region
- any missing regions that should be added

### 3. Home Menu UX Completion

Claude should review:
- Monstos preview wheel behavior
- lore button collapse/expand behavior
- voice button placeholder behavior
- scoreboard tab behavior
- play-button behavior
- whether the active Monstos animation is obvious enough in the center slot

Claude should decide:
- how the home menu should behave frame-by-frame
- what should rerender every frame vs only on user interaction
- how to avoid visual stacking, ghosting, or leftover artifacts

Output expected from Claude:
- a specific implementation plan for the home screen
- a clean rendering strategy for left / center / right preview slots
- exact acceptance criteria for "menu is finished"

### 4. Game-State And Flow Reliability

Claude should review:
- start mode flow
- countdown flow
- transition into active gameplay
- top-out flow
- retry flow
- return-home flow
- behavior when changing modes or re-entering after a run

Claude should decide:
- whether the app needs a formal app phase/state machine
- how to separate menu state from match state cleanly
- where reset logic should live

Output expected from Claude:
- a concrete state-transition plan
- exact phase definitions
- exact files/functions to refactor
- how to verify that a run never starts in a dead or stuck state

### 5. In-Game Layout Composition

Claude should review:
- where the board, side panels, overlay, and controls sit relative to the user’s in-game drawing
- whether the current generic panel styling clashes too much with the art
- whether the board and HUD overlap parts of the background they should not

Claude should decide:
- how the gameplay layout should sit inside the artboard
- whether the current HUD should be lightly restyled or structurally repositioned

Output expected from Claude:
- a composition/layout plan for the game screen
- exact positioning or structural changes needed
- acceptance criteria for “game screen is visually coherent”

### 6. Monster Animation Verification

Claude should review:
- home menu animation behavior
- in-game eye movement visibility
- blinking behavior by piece
- tongue animation visibility on the purple block
- fleshy squish behavior when pieces stack
- whether these animations are currently provable in live gameplay

Claude should decide:
- what debug tooling or visual verification mode is needed
- how to prove each animation is actually active in menu and live play
- whether all intended eyes are mapped and behaving correctly

Output expected from Claude:
- a verification plan for all creature effects
- any remaining animation gaps
- exact acceptance criteria for animation signoff

### 7. Leaderboards, Modes, And Player Flow

Claude should review:
- OGBM local high scores
- X(4)-LINES local best times
- nickname entry flow
- how training mode fits into the full menu/game loop
- what users should see before, during, and after each mode

Claude should decide:
- whether the current record flow feels clean enough
- whether each mode has the right UI messaging
- whether any score/mode text should be renamed or clarified in the UI

Output expected from Claude:
- a player-flow plan from menu to run end for each mode
- any fixes required for leaderboard clarity or submission behavior

### 8. Packaging And Release Readiness

Claude should review:
- whether MonStacka is now cleanly separable from the HTML version
- whether the desktop app flow is ready for distribution
- whether anything still references old naming or old launcher behavior
- what is required for a Mac-downloadable build path

Claude should decide:
- what release-readiness steps remain
- what should be documented in README / changelog / release notes

Output expected from Claude:
- a release-readiness checklist
- what remains before the standalone app can be treated as polished enough to share

## What The Final Claude Plan Should Contain

Claude should return a concrete implementation plan that includes:

```text
1. Ordered phases
   └─ with dependencies between them

2. Exact file targets
   └─ what files should change in each phase

3. Exact behavioral goals
   └─ what should work after each phase

4. Verification steps
   └─ how to prove each phase is working

5. Acceptance criteria
   └─ what "done" means for each area

6. Risk notes
   └─ where art integration or state flow could still go wrong
```

## Constraints Claude Should Respect

- Keep the user's drawings as the design source of truth.
- Do not redesign the home menu into a generic UI.
- Use the art itself as the base, then place real controls into the drawn layout.
- Creature effects are visual only and must not alter gameplay.
- The Monstos preview wheel must cycle through all Monstos.
- The active center Monstos should be the one that animates in the home menu.
- Home button in game should abandon the run and return to menu without saving the abandoned run.
- The desktop app must remain separate from the HTML fallback version.

## Suggested Claude Deliverable Format

Ask Claude to return its implementation plan in this shape:

```text
╔═ Phase 1
║ objective
║ files
║ actions
║ verification
╠═ Phase 2
║ objective
║ files
║ actions
║ verification
╠═ Phase 3
║ ...
╚═ Final QA / release readiness
```

## End Goal For The Next Handoff

When Claude sends its write-up back, it should be detailed enough that Codex can:
- pick the first unfinished phase
- implement it directly
- verify it
- move to the next phase without guessing

If the returned plan still leaves major areas vague, it is not detailed enough yet.
