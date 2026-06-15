# MonStacka Built Player Visual Harness Plan

**Goal:** Add a real built-player smoke layer to the Unity harness so it can catch failures that only appear in the packaged game window: blank rendering, missing active pieces, stale builds, and gross layout/pixel regressions.

**Shape:**

```
Editor harness
  ├─ in-editor system/mechanic scenarios
  ├─ current build freshness gate
  └─ built player visual smoke
       ├─ launch Windows player with command-line mode
       ├─ runtime smoke writes PASS/FAIL report
       ├─ runtime smoke captures framebuffer PNG
       └─ editor harness checks screenshot dimensions, color variety, and scene palette
```

## Tasks

- [x] Extend `RuntimeRenderSmoke` with `-monstacka-smoke-report` and `-monstacka-smoke-quit`.
- [x] Add a harness scenario that launches the built player for O.G.B.M. zany and Story 1.3.
- [x] Validate each player report contains `RESULT: PASS`.
- [x] Validate each screenshot is nonblank and contains enough distinct/dark/blue/saturated pixels to prove the real window rendered game content.
- [x] Rebuild the Windows player after source changes.
- [x] Rerun the full harness and document what remains outside coverage.
