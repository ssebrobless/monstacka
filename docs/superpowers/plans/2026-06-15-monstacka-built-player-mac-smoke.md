# MonStacka Built Player and Mac Smoke Harness Plan

> For agentic workers: use test-first verification for new scripts. Keep the remote Mac clean by staging in one generated cache folder and deleting that generated run folder by default.

Goal: extend the harness from editor/runtime method replay into built-player smoke checks, real keyboard input checks, screenshot evidence, and longer soak support. The macOS SSH path is parked for now and remains opt-in only.

Architecture:

```
Local repo
  |
  +-- Unity editor harness
  |     verifies deterministic runtime state
  |
  +-- Windows built player smoke
  |     launches MonStackaV2.exe
  |     sends OS keyboard input
  |     writes screenshot + smoke report + log
  |
  +-- Mac remote smoke
        copies MonStackaV2-macOS.zip to remote cache run folder
        runs .app executable with smoke args
        pulls screenshot + report + log back
        deletes only that generated run folder
```

Tasks:

- [x] Add smoke-tool verifier that fails when scripts are missing, unparsable, or unsafe.
- [x] Add Windows built-player keyboard smoke script.
- [x] Add Windows built-player suite for multi-mode capture and keyboard smoke.
- [x] Park Mac SSH smoke behind an opt-in verifier flag.
- [x] Update legacy smoke-capture helper to write reports/logs consistently.
- [x] Verify script parser checks.
- [x] Add runtime screenshot checkpoints for line clear, assist trigger, pause/settings, and game over.
- [x] Add longer runtime soak replays for queue drain, piece stop, and late modifier checks.
- [x] Rerun Unity harness if C# changes are present.
- [x] Verify Windows built-player suite across O.G.B.M., X(4)-LINES, Training, Story 1.1, Story 1.3, and keyboard smoke paths.
