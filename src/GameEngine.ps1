Set-StrictMode -Version 2.0

function New-Cell {
    param([int]$X, [int]$Y)
    [pscustomobject]@{ X = $X; Y = $Y }
}

function New-KickOffset {
    param([int]$X, [int]$Y)
    [pscustomobject]@{ X = $X; Y = $Y }
}

function Get-TetrominoDefinitions {
    $definitions = @{}
    $definitions.I = @(
        @((New-Cell 0 1), (New-Cell 1 1), (New-Cell 2 1), (New-Cell 3 1)),
        @((New-Cell 2 0), (New-Cell 2 1), (New-Cell 2 2), (New-Cell 2 3)),
        @((New-Cell 0 2), (New-Cell 1 2), (New-Cell 2 2), (New-Cell 3 2)),
        @((New-Cell 1 0), (New-Cell 1 1), (New-Cell 1 2), (New-Cell 1 3))
    )
    $definitions.O = @(
        @((New-Cell 1 0), (New-Cell 2 0), (New-Cell 1 1), (New-Cell 2 1)),
        @((New-Cell 1 0), (New-Cell 2 0), (New-Cell 1 1), (New-Cell 2 1)),
        @((New-Cell 1 0), (New-Cell 2 0), (New-Cell 1 1), (New-Cell 2 1)),
        @((New-Cell 1 0), (New-Cell 2 0), (New-Cell 1 1), (New-Cell 2 1))
    )
    $definitions.T = @(
        @((New-Cell 1 0), (New-Cell 0 1), (New-Cell 1 1), (New-Cell 2 1)),
        @((New-Cell 1 0), (New-Cell 1 1), (New-Cell 2 1), (New-Cell 1 2)),
        @((New-Cell 0 1), (New-Cell 1 1), (New-Cell 2 1), (New-Cell 1 2)),
        @((New-Cell 1 0), (New-Cell 0 1), (New-Cell 1 1), (New-Cell 1 2))
    )
    $definitions.S = @(
        @((New-Cell 1 0), (New-Cell 2 0), (New-Cell 0 1), (New-Cell 1 1)),
        @((New-Cell 1 0), (New-Cell 1 1), (New-Cell 2 1), (New-Cell 2 2)),
        @((New-Cell 1 1), (New-Cell 2 1), (New-Cell 0 2), (New-Cell 1 2)),
        @((New-Cell 0 0), (New-Cell 0 1), (New-Cell 1 1), (New-Cell 1 2))
    )
    $definitions.Z = @(
        @((New-Cell 0 0), (New-Cell 1 0), (New-Cell 1 1), (New-Cell 2 1)),
        @((New-Cell 2 0), (New-Cell 1 1), (New-Cell 2 1), (New-Cell 1 2)),
        @((New-Cell 0 1), (New-Cell 1 1), (New-Cell 1 2), (New-Cell 2 2)),
        @((New-Cell 1 0), (New-Cell 0 1), (New-Cell 1 1), (New-Cell 0 2))
    )
    $definitions.J = @(
        @((New-Cell 0 0), (New-Cell 0 1), (New-Cell 1 1), (New-Cell 2 1)),
        @((New-Cell 1 0), (New-Cell 2 0), (New-Cell 1 1), (New-Cell 1 2)),
        @((New-Cell 0 1), (New-Cell 1 1), (New-Cell 2 1), (New-Cell 2 2)),
        @((New-Cell 1 0), (New-Cell 1 1), (New-Cell 0 2), (New-Cell 1 2))
    )
    $definitions.L = @(
        @((New-Cell 2 0), (New-Cell 0 1), (New-Cell 1 1), (New-Cell 2 1)),
        @((New-Cell 1 0), (New-Cell 1 1), (New-Cell 1 2), (New-Cell 2 2)),
        @((New-Cell 0 1), (New-Cell 1 1), (New-Cell 2 1), (New-Cell 0 2)),
        @((New-Cell 0 0), (New-Cell 1 0), (New-Cell 1 1), (New-Cell 1 2))
    )
    return $definitions
}

function Get-SrsKickData {
    $jlszt = @{
        '0>1' = @($(New-KickOffset 0 0), $(New-KickOffset -1 0), $(New-KickOffset -1 1), $(New-KickOffset 0 -2), $(New-KickOffset -1 -2))
        '1>0' = @($(New-KickOffset 0 0), $(New-KickOffset 1 0), $(New-KickOffset 1 -1), $(New-KickOffset 0 2), $(New-KickOffset 1 2))
        '1>2' = @($(New-KickOffset 0 0), $(New-KickOffset 1 0), $(New-KickOffset 1 -1), $(New-KickOffset 0 2), $(New-KickOffset 1 2))
        '2>1' = @($(New-KickOffset 0 0), $(New-KickOffset -1 0), $(New-KickOffset -1 1), $(New-KickOffset 0 -2), $(New-KickOffset -1 -2))
        '2>3' = @($(New-KickOffset 0 0), $(New-KickOffset 1 0), $(New-KickOffset 1 1), $(New-KickOffset 0 -2), $(New-KickOffset 1 -2))
        '3>2' = @($(New-KickOffset 0 0), $(New-KickOffset -1 0), $(New-KickOffset -1 -1), $(New-KickOffset 0 2), $(New-KickOffset -1 2))
        '3>0' = @($(New-KickOffset 0 0), $(New-KickOffset -1 0), $(New-KickOffset -1 -1), $(New-KickOffset 0 2), $(New-KickOffset -1 2))
        '0>3' = @($(New-KickOffset 0 0), $(New-KickOffset 1 0), $(New-KickOffset 1 1), $(New-KickOffset 0 -2), $(New-KickOffset 1 -2))
    }
    $i = @{
        '0>1' = @($(New-KickOffset 0 0), $(New-KickOffset -2 0), $(New-KickOffset 1 0), $(New-KickOffset -2 -1), $(New-KickOffset 1 2))
        '1>0' = @($(New-KickOffset 0 0), $(New-KickOffset 2 0), $(New-KickOffset -1 0), $(New-KickOffset 2 1), $(New-KickOffset -1 -2))
        '1>2' = @($(New-KickOffset 0 0), $(New-KickOffset -1 0), $(New-KickOffset 2 0), $(New-KickOffset -1 2), $(New-KickOffset 2 -1))
        '2>1' = @($(New-KickOffset 0 0), $(New-KickOffset 1 0), $(New-KickOffset -2 0), $(New-KickOffset 1 -2), $(New-KickOffset -2 1))
        '2>3' = @($(New-KickOffset 0 0), $(New-KickOffset 2 0), $(New-KickOffset -1 0), $(New-KickOffset 2 1), $(New-KickOffset -1 -2))
        '3>2' = @($(New-KickOffset 0 0), $(New-KickOffset -2 0), $(New-KickOffset 1 0), $(New-KickOffset -2 -1), $(New-KickOffset 1 2))
        '3>0' = @($(New-KickOffset 0 0), $(New-KickOffset 1 0), $(New-KickOffset -2 0), $(New-KickOffset 1 -2), $(New-KickOffset -2 1))
        '0>3' = @($(New-KickOffset 0 0), $(New-KickOffset -1 0), $(New-KickOffset 2 0), $(New-KickOffset -1 2), $(New-KickOffset 2 -1))
    }
    return @{ J = $jlszt; L = $jlszt; S = $jlszt; T = $jlszt; Z = $jlszt; I = $i; O = @{} }
}

function New-Board {
    param([int]$Rows, [int]$Columns)
    $board = New-Object 'object[,]' $Rows, $Columns
    for ($y = 0; $y -lt $Rows; $y++) {
        for ($x = 0; $x -lt $Columns; $x++) {
            $board[$y, $x] = $null
        }
    }
    return ,$board
}

function New-ActivePiece {
    param([string]$Type, [int]$Rotation, [int]$X, [int]$Y)
    [pscustomobject]@{ Type = $Type; Rotation = $Rotation; X = $X; Y = $Y }
}

function New-SpawnPiece {
    param([string]$Type)
    New-ActivePiece -Type $Type -Rotation 0 -X 3 -Y 0
}

function Get-PieceCells {
    param([hashtable]$Definitions, $Piece)
    $cells = @()
    foreach ($cell in $Definitions[$Piece.Type][$Piece.Rotation]) {
        $cells += [pscustomobject]@{ X = $Piece.X + $cell.X; Y = $Piece.Y + $cell.Y }
    }
    $cells
}

function Test-PiecePosition {
    param($State, $Piece)
    foreach ($cell in (Get-PieceCells -Definitions $State.Definitions -Piece $Piece)) {
        if ($cell.X -lt 0 -or $cell.X -ge $State.Columns) { return $false }
        if ($cell.Y -ge $State.TotalRows) { return $false }
        if ($cell.Y -ge 0 -and $State.Board[$cell.Y, $cell.X]) { return $false }
    }
    $true
}

function Get-RandomPieceType {
    param([hashtable]$Definitions)
    Get-Random -InputObject @($Definitions.Keys)
}

function New-ScoreTable {
    @{ 1 = 100; 2 = 300; 3 = 500; 4 = 800 }
}

function Load-HighScores {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) { return @() }
    $raw = Get-Content -LiteralPath $Path -Raw
    if ([string]::IsNullOrWhiteSpace($raw)) { return @() }
    @((ConvertFrom-Json -InputObject $raw) | ForEach-Object {
        [pscustomobject]@{
            initials = [string]$_.initials
            score = [int]$_.score
            lines = [int]$_.lines
            timestamp = [string]$_.timestamp
        }
    })
}

function Save-HighScores {
    param([string]$Path, [object[]]$Entries)
    $folder = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $folder)) { New-Item -ItemType Directory -Path $folder | Out-Null }
    $Entries | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $Path -Encoding UTF8
}

function Normalize-Initials {
    param([string]$Initials)
    $clean = ([string]$Initials).ToUpperInvariant() -replace '[^A-Z0-9]', ''
    if ($clean.Length -gt 3) { $clean = $clean.Substring(0, 3) }
    $clean.PadRight(3, '_')
}

function Test-HighScoreQualification {
    param([object[]]$Entries, [int]$Score)
    $entries = @($Entries)
    if ($entries.Count -lt 10) { return $true }
    ($Score -gt $entries[-1].score)
}

function Initialize-GameState {
    param([string]$HighScorePath)
    $state = [pscustomobject]@{
        Columns = 10
        VisibleRows = 20
        HiddenRows = 4
        TotalRows = 24
        GravityMilliseconds = 700
        Definitions = $(Get-TetrominoDefinitions)
        KickData = $(Get-SrsKickData)
        ScoreTable = $(New-ScoreTable)
        HighScorePath = $HighScorePath
        HighScores = @()
        Board = $null
        ActivePiece = $null
        HoldPieceType = $null
        HoldUsed = $false
        Score = 0
        LinesCleared = 0
        LastTickUtc = [DateTime]::UtcNow
        GameOver = $false
        GameClosed = $false
        CanSubmitHighScore = $false
        HighScoreSubmitted = $false
        FinalMessage = $null
        SessionId = [guid]::NewGuid().ToString()
    }
    $state.HighScores = Load-HighScores -Path $HighScorePath
    Reset-GameState -State $state | Out-Null
    $state
}

function Update-ScoreQualification {
    param($State)
    $State.CanSubmitHighScore = $State.GameOver -and -not $State.HighScoreSubmitted -and (Test-HighScoreQualification -Entries $State.HighScores -Score $State.Score)
}

function Spawn-NewPiece {
    param($State, [string]$Type)
    if (-not $Type) { $Type = Get-RandomPieceType -Definitions $State.Definitions }
    $candidate = New-SpawnPiece -Type $Type
    if (-not (Test-PiecePosition -State $State -Piece $candidate)) {
        $State.ActivePiece = $null
        return $false
    }
    $State.ActivePiece = $candidate
    $State.HoldUsed = $false
    $true
}

function Reset-GameState {
    param($State)
    $State.Board = New-Board -Rows $State.TotalRows -Columns $State.Columns
    $State.ActivePiece = $null
    $State.HoldPieceType = $null
    $State.HoldUsed = $false
    $State.Score = 0
    $State.LinesCleared = 0
    $State.LastTickUtc = [DateTime]::UtcNow
    $State.GameOver = $false
    $State.GameClosed = $false
    $State.CanSubmitHighScore = $false
    $State.HighScoreSubmitted = $false
    $State.FinalMessage = $null
    $State.SessionId = [guid]::NewGuid().ToString()
    $State.HighScores = Load-HighScores -Path $State.HighScorePath
    [void](Spawn-NewPiece -State $State)
    $State
}

function Try-MovePiece {
    param($State, [int]$DeltaX, [int]$DeltaY)
    if (-not $State.ActivePiece) { return $false }
    $candidate = New-ActivePiece -Type $State.ActivePiece.Type -Rotation $State.ActivePiece.Rotation -X ($State.ActivePiece.X + $DeltaX) -Y ($State.ActivePiece.Y + $DeltaY)
    if (Test-PiecePosition -State $State -Piece $candidate) {
        $State.ActivePiece = $candidate
        return $true
    }
    $false
}

function Try-RotatePiece {
    param($State, [int]$Step, [switch]$UseKicks)
    if (-not $State.ActivePiece) { return $false }
    $from = $State.ActivePiece.Rotation
    $to = ($from + $Step) % 4
    if ($to -lt 0) { $to += 4 }
    $candidate = New-ActivePiece -Type $State.ActivePiece.Type -Rotation $to -X $State.ActivePiece.X -Y $State.ActivePiece.Y
    if (-not $UseKicks) {
        if (Test-PiecePosition -State $State -Piece $candidate) {
            $State.ActivePiece = $candidate
            return $true
        }
        return $false
    }
    $kickTable = $State.KickData[$State.ActivePiece.Type]
    $kickKey = '{0}>{1}' -f $from, $to
    $tests = if ($kickTable.ContainsKey($kickKey)) { $kickTable[$kickKey] } else { @(@(0,0)) }
    foreach ($offset in $tests) {
        $testPiece = New-ActivePiece -Type $candidate.Type -Rotation $candidate.Rotation -X ($candidate.X + $offset.X) -Y ($candidate.Y - $offset.Y)
        if (Test-PiecePosition -State $State -Piece $testPiece) {
            $State.ActivePiece = $testPiece
            return $true
        }
    }
    $false
}

function Clear-CompletedLines {
    param($State)
    $cleared = 0
    $newBoard = New-Board -Rows $State.TotalRows -Columns $State.Columns
    $targetRow = $State.TotalRows - 1
    for ($y = $State.TotalRows - 1; $y -ge 0; $y--) {
        $complete = $true
        for ($x = 0; $x -lt $State.Columns; $x++) {
            if (-not $State.Board[$y, $x]) { $complete = $false; break }
        }
        if ($complete) { $cleared++; continue }
        for ($x = 0; $x -lt $State.Columns; $x++) { $newBoard[$targetRow, $x] = $State.Board[$y, $x] }
        $targetRow--
    }
    $State.Board = $newBoard
    if ($cleared -gt 0) {
        $State.LinesCleared += $cleared
        $State.Score += $State.ScoreTable[$cleared]
    }
    $cleared
}

function Lock-ActivePiece {
    param($State)
    foreach ($cell in (Get-PieceCells -Definitions $State.Definitions -Piece $State.ActivePiece)) {
        if ($cell.Y -ge 0 -and $cell.Y -lt $State.TotalRows) { $State.Board[$cell.Y, $cell.X] = $State.ActivePiece.Type }
    }
    $State.ActivePiece = $null
    [void](Clear-CompletedLines -State $State)
    if (-not (Spawn-NewPiece -State $State)) {
        $State.GameOver = $true
        Update-ScoreQualification -State $State
    }
}

function Drop-OneRow {
    param($State, [bool]$AwardSoftDrop)
    if (Try-MovePiece -State $State -DeltaX 0 -DeltaY 1) {
        if ($AwardSoftDrop) { $State.Score += 1 }
        return $true
    }
    Lock-ActivePiece -State $State
    $false
}

function Hard-DropPiece {
    param($State)
    if (-not $State.ActivePiece) { return }
    while (Try-MovePiece -State $State -DeltaX 0 -DeltaY 1) { }
    Lock-ActivePiece -State $State
}

function Hold-CurrentPiece {
    param($State)
    if (-not $State.ActivePiece -or $State.HoldUsed -or $State.GameOver) { return $false }
    $currentType = $State.ActivePiece.Type
    if ($State.HoldPieceType) {
        $swapType = $State.HoldPieceType
        $State.HoldPieceType = $currentType
        $candidate = New-SpawnPiece -Type $swapType
        if (-not (Test-PiecePosition -State $State -Piece $candidate)) {
            $State.GameOver = $true
            Update-ScoreQualification -State $State
            return $false
        }
        $State.ActivePiece = $candidate
    } else {
        $State.HoldPieceType = $currentType
        $State.ActivePiece = $null
        if (-not (Spawn-NewPiece -State $State)) {
            $State.GameOver = $true
            Update-ScoreQualification -State $State
            return $false
        }
    }
    $State.HoldUsed = $true
    $true
}

function Update-GameClock {
    param($State)
    if ($State.GameOver -or $State.GameClosed) { return }
    $now = [DateTime]::UtcNow
    $elapsed = ($now - $State.LastTickUtc).TotalMilliseconds
    while ($elapsed -ge $State.GravityMilliseconds -and -not $State.GameOver) {
        [void](Drop-OneRow -State $State -AwardSoftDrop:$false)
        $State.LastTickUtc = $State.LastTickUtc.AddMilliseconds($State.GravityMilliseconds)
        $elapsed = ($now - $State.LastTickUtc).TotalMilliseconds
    }
}

function Invoke-GameAction {
    param($State, [string]$Action)
    Update-GameClock -State $State
    if ($State.GameOver -or $State.GameClosed) { return }
    switch ($Action) {
        'moveLeft' { [void](Try-MovePiece -State $State -DeltaX -1 -DeltaY 0) }
        'moveRight' { [void](Try-MovePiece -State $State -DeltaX 1 -DeltaY 0) }
        'softDrop' { [void](Drop-OneRow -State $State -AwardSoftDrop:$true) }
        'hardDrop' { Hard-DropPiece -State $State }
        'rotateCcw' { [void](Try-RotatePiece -State $State -Step -1 -UseKicks) }
        'rotateCw' { [void](Try-RotatePiece -State $State -Step 1 -UseKicks) }
        'rotate180' { [void](Try-RotatePiece -State $State -Step 2) }
        'hold' { [void](Hold-CurrentPiece -State $State) }
    }
}

function Submit-HighScore {
    param($State, [string]$Initials)
    if (-not $State.GameOver -or -not $State.CanSubmitHighScore) { return $false }
    $entry = [pscustomobject]@{
        initials = Normalize-Initials -Initials $Initials
        score = $State.Score
        lines = $State.LinesCleared
        timestamp = [DateTime]::UtcNow.ToString('o')
    }
    $entries = @($State.HighScores) + @($entry)
    $entries = $entries | Sort-Object -Property @{ Expression = 'score'; Descending = $true }, @{ Expression = 'timestamp'; Descending = $false }
    if ($entries.Count -gt 10) { $entries = @($entries[0..9]) }
    $State.HighScores = @($entries)
    Save-HighScores -Path $State.HighScorePath -Entries $State.HighScores
    $State.CanSubmitHighScore = $false
    $State.HighScoreSubmitted = $true
    $true
}

function Close-GameSession {
    param($State)
    $State.GameClosed = $true
    $State.FinalMessage = 'Game session ended. You can close this browser tab.'
}

function Get-BoardSnapshot {
    param($State)
    $rows = @()
    for ($y = $State.HiddenRows; $y -lt $State.TotalRows; $y++) {
        $row = @()
        for ($x = 0; $x -lt $State.Columns; $x++) { $row += $State.Board[$y, $x] }
        $rows += ,@($row)
    }
    if ($State.ActivePiece) {
        foreach ($cell in (Get-PieceCells -Definitions $State.Definitions -Piece $State.ActivePiece)) {
            if ($cell.Y -ge $State.HiddenRows -and $cell.Y -lt $State.TotalRows) { $rows[($cell.Y - $State.HiddenRows)][$cell.X] = $State.ActivePiece.Type }
        }
    }
    $rows
}

function Get-PublicGameState {
    param($State)
    Update-GameClock -State $State
    Update-ScoreQualification -State $State
    [pscustomobject]@{
        sessionId = $State.SessionId
        rows = Get-BoardSnapshot -State $State
        score = $State.Score
        lines = $State.LinesCleared
        hold = $State.HoldPieceType
        holdUsed = $State.HoldUsed
        gameOver = $State.GameOver
        gameClosed = $State.GameClosed
        canSubmitHighScore = $State.CanSubmitHighScore
        highScoreSubmitted = $State.HighScoreSubmitted
        finalMessage = $State.FinalMessage
        leaderboard = @($State.HighScores)
        controls = [pscustomobject]@{
            left = 'Left Arrow'
            right = 'Right Arrow'
            softDrop = 'Down Arrow'
            hardDrop = 'Space'
            rotateCcw = 'Z'
            rotateCw = 'X'
            rotate180 = 'C'
            hold = 'Shift'
            quit = 'Q'
        }
    }
}
