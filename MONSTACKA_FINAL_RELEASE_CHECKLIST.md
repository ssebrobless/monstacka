# MonStacka Final Release Checklist

```text
MonStacka v1 Release Gate
╔════════════════════════════════════════════════════════════╗
║ A. Gameplay / UI signoff                                  ║
║ B. Visual / animation polish                              ║
║ C. Audio / controls polish                                ║
║ D. Packaging / release delivery                           ║
║ E. Final public-download verification                     ║
╚════════════════════════════════════════════════════════════╝
```

This checklist is the finish line for calling MonStacka a real downloadable release.

## Definition Of Done

```text
Release-ready means:
├─ all game modes work without flow bugs
├─ all important UI is clickable and behaves correctly
├─ sprite presentation is intentionally polished
├─ controls/settings/audio are stable and save correctly
├─ the packaged app installs/launches cleanly
└─ the public GitHub repo offers clean standalone downloads
```

## A. Gameplay / UI Signoff

- [ ] `Arcade` starts, plays, tops out, retries, and returns home without getting stuck.
- [ ] `40 Lines` starts, clears, retries, and returns home without getting stuck.
- [ ] `Training` starts, plays, retries, and returns home without getting stuck.
- [ ] `Continue / Start New / Cancel` save-flow works correctly for every supported mode.
- [ ] `Pause / Resume / Restart Paused` work correctly from both keyboard and controller.
- [ ] `Home` during an active run saves correctly when it should and does not create bad resume prompts.
- [ ] `Retry` only appears when it is actually supposed to appear.
- [ ] End-state text and status panels never render behind the board.
- [ ] Nickname-entry flow works cleanly for qualifying runs.
- [ ] Leaderboards update correctly and sort into the 8-slot home layout correctly.
- [ ] Settings open/close correctly from both home and in-game.
- [ ] No important UI hitbox is missing, dead, or accidentally overlapping another control.

## B. Visual / Animation Polish

- [ ] Home menu art is clean with no accidental paint-over damage to title, buttons, arrows, or labels.
- [ ] Old example speech bubble is fully removed from the background art.
- [ ] Old example preview sprites are fully removed from the background art.
- [ ] Lore bubble opens cleanly, closes cleanly, and the connector tail looks intentional.
- [ ] Lore text never clips awkwardly and always fits the bubble naturally.
- [ ] Preview wheel always shows the actual Monstos sprites, not fallback blocks or loading leftovers.
- [ ] Preview wheel scaling is correct for every piece in left, center, and right circles.
- [ ] Preview sprites and in-game sprites match stylistically; only scale should differ.
- [ ] No animation frame shows stray fragments from neighboring sprites.
- [ ] Tetromino bodies read as one connected creature, not as separate tiles.
- [ ] Border ripple stays outline-only and never causes the body to visually break apart.
- [ ] Ripple is clearly visible in both:
  - [ ] active preview block
  - [ ] live in-game pieces
- [ ] Frame animation pacing feels intentional, with loop cooldowns that are not too fast.
- [ ] Cyan eye behavior feels natural if kept.
- [ ] Purple eye/blink behavior feels natural if kept.
- [ ] Any eye animation that still looks fake or off-model is either fixed properly or removed.

## C. Audio / Controls Polish

- [ ] New looping BGM starts reliably and loops seamlessly.
- [ ] BGM default volume feels good relative to SFX.
- [ ] `Music On` and `Sound Effects On` toggles work independently and save correctly.
- [ ] Keyboard/mouse default controls feel familiar to Tetris players.
- [ ] Keyboard/mouse remapping works and persists.
- [ ] Controller default controls feel good in actual play.
- [ ] Controller remapping works and persists.
- [ ] Controller inputs do not cause mushy movement, repeat bugs, or double-input issues.
- [ ] A real live controller smoke test is completed on hardware.

## D. Packaging / Release Delivery

```text
Public delivery target
╔════════════════════════════════════════════════════════════╗
║ GitHub repo: ssebrobless/monstacka                        ║
║ Public download should be:                                ║
║ ├─ Windows installer (.exe setup)  ← primary             ║
║ ├─ optional portable monstacka.exe                       ║
║ └─ optional MSI                                          ║
╚════════════════════════════════════════════════════════════╝
```

- [ ] The standalone MonStacka repo is the public-facing download location.
- [ ] The standalone repo README is clean and only describes MonStacka.
- [ ] The standalone repo includes:
  - [ ] current screenshots
  - [ ] install instructions
  - [ ] run instructions
  - [ ] controls summary
  - [ ] known platform support
- [ ] Windows installer is the main download asset.
- [ ] Portable `.exe` is offered if we want a no-install option.
- [ ] Checksums are published for release assets.
- [ ] Release notes describe what players are downloading and what changed.
- [ ] The launcher confusion is gone; players should not be routed through dev chooser behavior for the standalone download.
- [ ] The packaged app icon is correct in:
  - [ ] file explorer
  - [ ] shortcuts
  - [ ] window titlebar/taskbar

## E. Final Public-Download Verification

- [ ] Download the release from GitHub like a real player would.
- [ ] Install/launch it on a clean-ish Windows machine or user profile.
- [ ] Confirm it launches directly as MonStacka.
- [ ] Confirm the packaged build matches the tested local build.
- [ ] Confirm save data works as expected in the packaged release.
- [ ] Confirm music, SFX, settings, and controls all work in the packaged release.
- [ ] Confirm no dev-only assets, debug overlays, or audit leftovers leak into the public build.

## Recommended Release Order

```text
1. Finish gameplay/controller signoff
2. Finish sprite / border-ripple polish
3. Run one complete end-to-end QA pass
4. Build the final installer and portable exe
5. Update standalone repo README / screenshots / notes
6. Publish release on GitHub
7. Do one real download-and-install verification
```

## Suggested Release Labels

- `Internal testing`
- `Release candidate`
- `v1.0.0`

## Nice-To-Have After v1

- [ ] macOS build
- [ ] code signing
- [ ] auto-updater
- [ ] itch.io page
- [ ] trailer / gameplay gif / prettier release page

