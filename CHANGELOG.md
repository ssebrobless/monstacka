# Changelog

## 2026-03-29

- Created the initial PowerShell Tetris MVP with a local `HttpListener` server and browser UI.
- Added core gameplay for movement, dropping, hold, SRS wall kicks for `Z` and `X`, non-kicked `C` rotation, line clears, scoring, and game-over handling.
- Added persistent top-10 high scores stored in JSON with arcade-style initials entry.
- Added lightweight PowerShell checks for engine logic.
- Added README and changelog documentation so project state stays current as the game evolves.
- Refined the project into a 40-line sprint-focused build with a 7-bag queue, ghost piece, countdown start, quick retry flow, sprint counters, and separate sprint-time persistence.
- Added customizable timing settings for DAS, ARR, DCD, soft-drop speed, gravity, and countdown, with a browser settings panel and local persistence.
- Added Phase 1 launcher groundwork with browser auto-open, a Windows launcher script, a macOS launcher placeholder, and updated startup instructions.
- Replaced the placeholder launcher flow with the final two-choice launcher for `HTML` and `+E+RIS`.
- Added a real backend-free `classic-html/` sprint edition that runs directly from local files and uses `localStorage` for nickname leaderboard persistence.
- Added an `enhanced/` scaffold with TypeScript/Vite project structure and a launchable `+E+RIS` preview build.
