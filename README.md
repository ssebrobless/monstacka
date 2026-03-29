# PowerShell Tetris

PowerShell Tetris is a local browser-based Tetris game where the gameplay engine runs in Windows PowerShell 5.1 and the UI runs in a page served from `http://localhost:8080/`.

## Current State

- Fully playable MVP with movement, soft drop, hard drop, hold, SRS wall kicks for clockwise and counterclockwise rotation, line clearing, scoring, and game over.
- PowerShell is the source of truth for board state, scoring, hold logic, and persistent high scores.
- Browser UI renders the board, HUD, controls, game-over flow, and top-10 leaderboard.
- Arcade-style 3-character initials can be saved when a finished game earns a top-10 score.

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
3. Run:

```powershell
.\main.ps1
```

4. Open the printed `http://localhost:8080/` URL in your browser.

## Leaderboard

- High scores are stored in [highscores.json](/C:/Users/grish/CODEX_Gen/Project_1/data/highscores.json).
- The game keeps the top 10 scores only.
- Entries are saved as uppercase 3-character initials, score, lines, and timestamp.

## Known Limitations

- `C` rotation is a non-standard convenience move and does not use kicks.
- There is no next-piece preview, ghost piece, pause system, or level progression yet.
- Quitting stops the PowerShell session, but because the browser is opened manually, you still need to close the browser tab yourself.
