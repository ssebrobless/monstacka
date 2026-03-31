# Changelog

## 2026-03-30

- Added a region-map layout system so MonStacka! artboard controls, preview slots, and gameplay shell can be positioned from native 1920x1080 coordinates instead of ad hoc CSS percentages.
- Added a clean home-menu art variant for dynamic Monstos previews and lore rendering without fighting baked sample content.
- Rebuilt the Monstos home preview path around live monster-cell rendering and preview audio triggers.
- Removed the old lore-mask overlay path and simplified the speech-bubble rendering stack.
- Replaced the implicit menu/game flow with an explicit app-phase flow for menu, countdown, playing, game-over, and sprint-clear transitions.
- Removed the in-game mode dropdown so mode selection happens from the hand-drawn home menu only.
- Added a browser-safe quit fallback, a debug overlay mode via `?debug=1`, and a preview loading state while the monster sprite sheet is being prepared.
- Polished record entry flow with mode-aware modal titles for Arcade and Sprint runs.

## 2026-03-29

- Created the initial PowerShell Tetris MVP with a local `HttpListener` server and browser UI.
- Added core gameplay for movement, dropping, hold, SRS wall kicks for `Z` and `X`, non-kicked `C` rotation, line clears, scoring, and game-over handling.
- Added persistent top-10 high scores stored in JSON with arcade-style initials entry.
- Added lightweight PowerShell checks for engine logic.
- Added README and changelog documentation so project state stays current as the game evolves.
- Refined the project into a 40-line sprint-focused build with a 7-bag queue, ghost piece, countdown start, quick retry flow, sprint counters, and separate sprint-time persistence.
- Added customizable timing settings for DAS, ARR, DCD, soft-drop speed, gravity, and countdown, with a browser settings panel and local persistence.
- Added Phase 1 launcher groundwork with browser auto-open, a Windows launcher script, a macOS launcher placeholder, and updated startup instructions.
- Replaced the placeholder launcher flow with the final two-choice launcher for `HTML` and `MonStacka!`.
- Added a real backend-free `classic-html/` sprint edition that runs directly from local files and uses `localStorage` for nickname leaderboard persistence.
- Added an `enhanced/` scaffold with TypeScript/Vite project structure and a launchable `MonStacka!` preview build.
- Added the first `MonStacka!` control-feel pass with client-side DAS, ARR, and lock delay settings stored in localStorage.
