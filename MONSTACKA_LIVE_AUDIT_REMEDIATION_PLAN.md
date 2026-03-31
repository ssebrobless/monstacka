# MonStacka Live Audit Remediation Plan

## Audit Snapshot

Current audit artifacts:
- `enhanced/audit-artifacts/audit-report.json`
- `enhanced/audit-artifacts/home-initial.png`
- `enhanced/audit-artifacts/arcade-start.png`
- `enhanced/audit-artifacts/arcade-resume.png`

## Confirmed Failures

1. Preview renderer crash
- Symptom: home preview wheel is blank, menu rerenders log errors, game shell renders stale content.
- Root cause: `filled monster-preview` is passed as a single token to `classList.add(...)`.
- Files:
  - `enhanced/src/ui/monsterDom.ts`
  - `enhanced/src/ui/render.ts`

2. Game shell is entering with stale initial HTML
- Symptom: after picking a mode, the board appears but status still says “Choose a mode...” and gameplay never visibly starts.
- Root cause: renderer throws during hold/next preview population before the active game frame finishes rendering.
- Files:
  - `enhanced/src/ui/render.ts`
  - `enhanced/src/ui/monsterDom.ts`

3. Home preview wheel is text-only in practice
- Symptom: name/lore update, but left/center/right Monstos visuals do not show.
- Root cause: same preview renderer failure as item 1.
- Files:
  - `enhanced/src/ui/homeMenu.ts`
  - `enhanced/src/ui/monsterDom.ts`

4. Lore bubble behavior does not match intended UX
- Symptom: collapse is rough, old bubble treatment still feels baked-in, text is not typed in after bubble expansion.
- Required fix:
  - bubble and text are dependent
  - opening animates bubble first, then types text
  - closing clears text and retracts bubble cleanly
  - old example bubble appearance is fully painted over by the live bubble
- Files:
  - `enhanced/index.html`
  - `enhanced/src/ui/homeMenu.ts`
  - `enhanced/src/styles.css`

5. Voice button behavior is wrong for current placeholder stage
- Symptom: voice button is not a simple test-beep placeholder.
- Required fix:
  - clicking voice button plays a short beep only
  - clicking it must not toggle or affect lore state
- Files:
  - `enhanced/src/audio.ts`
  - `enhanced/src/main.ts`

6. Leaderboard switching lacks meaningful visible data
- Symptom: home leaderboard tabs switch to empty arrays, making verification weak.
- Required fix:
  - provide visible demo fallback records when storage is empty
  - keep real saved records authoritative when they exist
- Files:
  - `enhanced/src/ui/homeMenu.ts`
  - `enhanced/src/ui/render.ts`

7. In-game leaderboard is too large for the layout
- Symptom: active game screen still reserves too much space for records.
- Required fix:
  - show top 3 only in active game view
  - keep nickname visible
- Files:
  - `enhanced/src/ui/render.ts`

8. Missing pause/restart-live controls
- Symptom: `P` pause/resume and paused `O` restart are not implemented.
- Required fix:
  - `P` toggles pause during a live run
  - paused overlay/status explains controls
  - `O` while paused starts a fresh run in the current mode
  - restarting clears any saved continuation for that mode
- Files:
  - `enhanced/src/types.ts`
  - `enhanced/src/main.ts`
  - `enhanced/src/constants.ts`
  - `enhanced/src/input/keyboard.ts`
  - `enhanced/src/ui/render.ts`

9. Gameplay board placement/scale still needs tuning
- Symptom: board is smaller and left-heavy relative to the background composition.
- Required fix:
  - center the live board
  - increase practical board size by about 40%
  - keep sprites scaled to the true grid
- Files:
  - `enhanced/src/ui/regionMap.ts`
  - `enhanced/src/main.ts`
  - `enhanced/src/styles.css`

10. Second audit required after fixes
- Required verification:
  - preview wheel visibly cycles all Monstos
  - active center Monstos animates
  - lore bubble opens, types, closes cleanly
  - voice button beeps only
  - all three modes render playable runs
  - home save → resume modal works
  - pause/resume/restart-live works
  - in-game top 3 leaderboard renders correctly
  - no renderer/page errors remain

## Execution Order

1. Fix the renderer crash and restore playable rendering.
2. Rebuild the home preview wheel and lore/voice interactions on top of the now-stable renderer.
3. Improve leaderboard visibility with demo fallback and top-3 in-game layout.
4. Implement pause/resume/restart-live.
5. Recenter and enlarge the board layout.
6. Re-run the live audit and capture updated findings.
