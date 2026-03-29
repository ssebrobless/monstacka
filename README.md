# PowerShell Tetris Sprint

PowerShell Tetris Sprint is a local browser-based 40-line sprint game where the gameplay engine runs in Windows PowerShell 5.1 and the UI runs in a page served from `http://localhost:8080/`.

## Current State

- Primary mode is now a 40-line sprint inspired by the flow of modern clients such as TETR.IO.
- PowerShell remains the source of truth for board state, 7-bag generation, hold logic, ghost projection, sprint timing, and persistent records.
- Browser UI shows the sprint HUD, countdown, next queue, hold slot, quick retry flow, and local best times.
- SRS wall kicks are used for clockwise and counterclockwise rotation. `C` remains a non-standard 180 rotation without kicks.

## Sprint Features

- 40 lines remaining counter
- stopwatch / elapsed time
- 7-bag piece randomizer
- visible next queue
- ghost piece
- hold
- pieces placed, total key inputs, keys per piece, and current-piece inputs
- local top-10 sprint times stored separately from the older score-based leaderboard

## Controls

- `Left Arrow`: move left
- `Right Arrow`: move right
- `Down Arrow`: soft drop
- `Space`: hard drop
- `Z`: rotate counterclockwise with SRS kicks
- `X`: rotate clockwise with SRS kicks
- `C`: rotate 180 degrees without kicks
- `Shift`: hold
- `Q`: quit the current session

## How To Run

1. Open Windows PowerShell 5.1.
2. Change into [Project_1](/C:/Users/grish/CODEX_Gen/Project_1).
3. If needed, use a one-time bypass:

```powershell
powershell -ExecutionPolicy Bypass -File .\main.ps1
```

4. Or, after your execution policy is set appropriately, run:

```powershell
.\main.ps1
```

5. Open the printed `http://localhost:8080/` URL in your browser.

## Records

- Sprint times are stored in [sprint-times.json](/C:/Users/grish/CODEX_Gen/Project_1/data/sprint-times.json).
- The project still keeps the older score leaderboard in [highscores.json](/C:/Users/grish/CODEX_Gen/Project_1/data/highscores.json), but sprint mode is now the main experience.

## Known Limitations

- Finesse faults and advanced finesse analysis are not implemented yet.
- Handling tuning such as configurable DAS/ARR is not implemented yet.
- The current build is sprint-focused and does not yet include a separate polished score-attack mode selector.
- Quitting stops the PowerShell session, but because the browser is opened manually, you still need to close the browser tab yourself.
