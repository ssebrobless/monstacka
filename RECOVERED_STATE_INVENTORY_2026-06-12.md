# MonStacka Recovered State Inventory - 2026-06-12

Purpose: preserve the current recovered state before HUD/layout work continues.

## Git Baseline

- Workspace root: `C:\Users\fishe\Documents\projects\Tetris\repo_review`
- Branch: `codex/monstacka-next`
- Upstream: `origin/codex/monstacka-next`
- HEAD: `3c095eb Fix ripple alignment, auto ripple count, eye margin, lore typos`
- `main` / `origin/main`: `031e116 Fix MonStacka launcher to require desktop app`

## Working Tree Inventory

Current status buckets from `git status --porcelain=v1` before cleanup:

- modified tracked enhanced files: 6
- untracked enhanced entries: 45
- untracked root docs: 6
- untracked Unity project entry: `MonStacka-v2/`

Important note: `MonStacka-v2/` began as a full untracked Unity project tree, including source, generated assets, build outputs, logs, and Unity cache folders. Source-controlled cleanup should keep Unity source/project files and ignore cache/build artifacts.

## Verified Evidence

Current v1 reference checks in `enhanced`:

- `npm test`: passed, 9 files / 42 tests
- `npm run build`: passed

Current Unity recovery evidence:

- Unity version: `6000.3.11f1`
- Batch verification log: `MonStacka-v2/verify-fable-final.log`
- Verification result: `MonStacka v2 verification complete.`
- Windows player runtime smoke helper: `MonStacka-v2/tools/smoke-capture.ps1`
- Runtime smoke capture: `MonStacka-v2/runtime-smoke-2026-06-12.png`
- Player log result: `[MonStackaSmoke] RESULT: PASS - active piece is live and visibly rendered.`

## Highest-Value Recovered Unity Source Areas

Recent Unity source additions/changes are concentrated in:

- `MonStacka-v2/Assets/Editor/MonStackaV2Bootstrap.cs`
- `MonStacka-v2/Assets/Editor/MonStackaV2Verification.cs`
- `MonStacka-v2/Assets/MonStacka/Scripts/Core/RuntimeRenderSmoke.cs`
- `MonStacka-v2/Assets/MonStacka/Scripts/Core/GameManager.cs`
- `MonStacka-v2/Assets/MonStacka/Scripts/Core/AssistEffectSystem.cs`
- `MonStacka-v2/Assets/MonStacka/Scripts/Story/`
- `MonStacka-v2/Assets/MonStacka/Scripts/UI/`
- `MonStacka-v2/Assets/MonStacka/Scripts/Visual/`

## Current Next Risk

The recovered Unity player now renders live gameplay. After the HUD/layout pass, remaining risk shifts from "does the live player render?" to actual play-feel and mode parity:

- O.G.B.M. scoring and top-out loop
- X(4)-LINES completion/records
- Training feedback behavior
- Story 1.1 objective/progression and dialogue flow
- assist balance over real play sessions
