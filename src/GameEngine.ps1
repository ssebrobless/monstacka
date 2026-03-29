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
    [pscustomobject]@{
        Type = $Type
        Rotation = $Rotation
        X = $X
        Y = $Y
        Id = $null
    }
}

function New-SpawnPiece {
    param([string]$Type)
    New-ActivePiece -Type $Type -Rotation 0 -X 3 -Y 0
}

function Get-PieceCells {
    param([hashtable]$Definitions, $Piece)
    $cells = @()
    foreach ($cell in $Definitions[$Piece.Type][$Piece.Rotation]) {
        $cells += [pscustomobject]@{
            X = $Piece.X + $cell.X
            Y = $Piece.Y + $cell.Y
        }
    }
    return $cells
}

function Test-PiecePosition {
    param($State, $Piece)
    foreach ($cell in (Get-PieceCells -Definitions $State.Definitions -Piece $Piece)) {
        if ($cell.X -lt 0 -or $cell.X -ge $State.Columns) { return $false }
        if ($cell.Y -ge $State.TotalRows) { return $false }
        if ($cell.Y -ge 0 -and $State.Board[$cell.Y, $cell.X]) { return $false }
    }
    return $true
}

function New-ScoreTable {
    @{ 1 = 100; 2 = 300; 3 = 500; 4 = 800 }
}

function Get-AllPieceTypes {
    @('I', 'O', 'T', 'S', 'Z', 'J', 'L')
}

function New-ShuffledBag {
    param([switch]$OpeningBag)
    $pieces = @()
    if ($OpeningBag) {
        $firstPool = @('I', 'J', 'L', 'T')
        $firstPick = Get-Random -InputObject $firstPool
        $pieces += $firstPick
        foreach ($piece in (Get-AllPieceTypes)) {
            if ($piece -ne $firstPick) {
                $pieces += $piece
            }
        }
    }
    else {
        $pieces = @(Get-AllPieceTypes)
    }

    $shuffled = @()
    foreach ($piece in ($pieces | Sort-Object { Get-Random })) {
        $shuffled += $piece
    }

    if ($OpeningBag) {
        $first = $shuffled | Where-Object { $_ -in @('I', 'J', 'L', 'T') } | Select-Object -First 1
        $rest = @($shuffled | Where-Object { $_ -ne $first })
        return @($first) + $rest
    }

    return @($shuffled)
}

function Ensure-Queue {
    param($State)
    while ($State.PieceQueue.Count -lt 7) {
        $opening = -not $State.HasSpawnedAnyPiece -and $State.PieceQueue.Count -eq 0
        foreach ($piece in (New-ShuffledBag -OpeningBag:$opening)) {
            [void]$State.PieceQueue.Add($piece)
        }
    }
}

function Dequeue-NextPieceType {
    param($State)
    Ensure-Queue -State $State
    $pieceType = $State.PieceQueue[0]
    $State.PieceQueue.RemoveAt(0)
    return $pieceType
}

function Load-EntriesFromJson {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) { return @() }
    $raw = Get-Content -LiteralPath $Path -Raw
    if ([string]::IsNullOrWhiteSpace($raw)) { return @() }
    return @((ConvertFrom-Json -InputObject $raw))
}

function Save-EntriesToJson {
    param([string]$Path, [object[]]$Entries)
    $folder = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $folder)) {
        New-Item -ItemType Directory -Path $folder | Out-Null
    }
    $Entries | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $Path -Encoding UTF8
}

function Load-HighScores {
    param([string]$Path)
    $entries = @()
    foreach ($entry in (Load-EntriesFromJson -Path $Path)) {
        $entries += [pscustomobject]@{
            initials = [string]$entry.initials
            score = [int]$entry.score
            lines = [int]$entry.lines
            timestamp = [string]$entry.timestamp
        }
    }
    return $entries
}

function Save-HighScores {
    param([string]$Path, [object[]]$Entries)
    Save-EntriesToJson -Path $Path -Entries $Entries
}

function Load-SprintTimes {
    param([string]$Path)
    $entries = @()
    foreach ($entry in (Load-EntriesFromJson -Path $Path)) {
        $entries += [pscustomobject]@{
            timeMs = [int]$entry.timeMs
            lines = [int]$entry.lines
            pieces = [int]$entry.pieces
            keys = [int]$entry.keys
            timestamp = [string]$entry.timestamp
        }
    }
    return $entries
}

function Save-SprintTimes {
    param([string]$Path, [object[]]$Entries)
    Save-EntriesToJson -Path $Path -Entries $Entries
}

function Normalize-Initials {
    param([string]$Initials)
    $clean = ([string]$Initials).ToUpperInvariant() -replace '[^A-Z0-9]', ''
    if ($clean.Length -gt 3) { $clean = $clean.Substring(0, 3) }
    return $clean.PadRight(3, '_')
}

function Test-HighScoreQualification {
    param([object[]]$Entries, [int]$Score)
    $entries = @($Entries)
    if ($entries.Count -lt 10) { return $true }
    return ($Score -gt $entries[-1].score)
}

function Get-ElapsedRunMilliseconds {
    param($State)
    if (-not $State.StartedAtUtc) { return 0 }
    if ($State.CompletedAtUtc) { return [int](($State.CompletedAtUtc - $State.StartedAtUtc).TotalMilliseconds) }
    return [int](([DateTime]::UtcNow - $State.StartedAtUtc).TotalMilliseconds)
}

function Get-LinesRemaining {
    param($State)
    return [Math]::Max(0, $State.TargetLines - $State.LinesCleared)
}

function Get-AverageKeysPerPiece {
    param($State)
    if ($State.PiecesPlaced -le 0) { return 0 }
    return [Math]::Round(($State.KeyInputs / [double]$State.PiecesPlaced), 2)
}

function Initialize-GameState {
    param(
        [string]$HighScorePath,
        [string]$SprintTimePath
    )

    $state = [pscustomobject]@{
        Columns = 10
        VisibleRows = 20
        HiddenRows = 4
        TotalRows = 24
        GravityMilliseconds = 700
        CountdownMilliseconds = 1200
        TargetLines = 40
        Definitions = $(Get-TetrominoDefinitions)
        KickData = $(Get-SrsKickData)
        ScoreTable = $(New-ScoreTable)
        HighScorePath = $HighScorePath
        SprintTimePath = $SprintTimePath
        HighScores = @()
        SprintTimes = @()
        Board = $null
        ActivePiece = $null
        HoldPieceType = $null
        HoldUsed = $false
        PieceQueue = (New-Object System.Collections.ArrayList)
        HasSpawnedAnyPiece = $false
        Score = 0
        LinesCleared = 0
        PiecesPlaced = 0
        KeyInputs = 0
        CurrentPieceInputs = 0
        LastTickUtc = [DateTime]::UtcNow
        CountdownEndsUtc = $null
        StartedAtUtc = $null
        CompletedAtUtc = $null
        GameOver = $false
        SprintComplete = $false
        GameClosed = $false
        CanSubmitHighScore = $false
        HighScoreSubmitted = $false
        FinalMessage = $null
        SessionId = [guid]::NewGuid().ToString()
        ActivePieceIdSeed = 0
    }

    $state.HighScores = Load-HighScores -Path $HighScorePath
    $state.SprintTimes = Load-SprintTimes -Path $SprintTimePath
    Reset-GameState -State $state | Out-Null
    return $state
}

function Reset-GameState {
    param($State)
    $State.Board = New-Board -Rows $State.TotalRows -Columns $State.Columns
    $State.ActivePiece = $null
    $State.HoldPieceType = $null
    $State.HoldUsed = $false
    $State.PieceQueue = New-Object System.Collections.ArrayList
    $State.HasSpawnedAnyPiece = $false
    $State.Score = 0
    $State.LinesCleared = 0
    $State.PiecesPlaced = 0
    $State.KeyInputs = 0
    $State.CurrentPieceInputs = 0
    $State.LastTickUtc = [DateTime]::UtcNow
    $State.CountdownEndsUtc = $State.LastTickUtc.AddMilliseconds($State.CountdownMilliseconds)
    $State.StartedAtUtc = $null
    $State.CompletedAtUtc = $null
    $State.GameOver = $false
    $State.SprintComplete = $false
    $State.GameClosed = $false
    $State.CanSubmitHighScore = $false
    $State.HighScoreSubmitted = $false
    $State.FinalMessage = $null
    $State.SessionId = [guid]::NewGuid().ToString()
    $State.HighScores = Load-HighScores -Path $State.HighScorePath
    $State.SprintTimes = Load-SprintTimes -Path $State.SprintTimePath
    Ensure-Queue -State $State
    [void](Spawn-NewPiece -State $State)
    return $State
}

function Spawn-NewPiece {
    param($State, [string]$Type)
    if (-not $Type) {
        $Type = Dequeue-NextPieceType -State $State
    }

    $candidate = New-SpawnPiece -Type $Type
    $State.ActivePieceIdSeed += 1
    $candidate.Id = $State.ActivePieceIdSeed
    if (-not (Test-PiecePosition -State $State -Piece $candidate)) {
        $State.ActivePiece = $null
        return $false
    }

    $State.ActivePiece = $candidate
    $State.HoldUsed = $false
    $State.CurrentPieceInputs = 0
    $State.HasSpawnedAnyPiece = $true
    Ensure-Queue -State $State
    return $true
}

function Try-MovePiece {
    param($State, [int]$DeltaX, [int]$DeltaY)
    if (-not $State.ActivePiece) { return $false }
    $candidate = New-ActivePiece -Type $State.ActivePiece.Type -Rotation $State.ActivePiece.Rotation -X ($State.ActivePiece.X + $DeltaX) -Y ($State.ActivePiece.Y + $DeltaY)
    $candidate.Id = $State.ActivePiece.Id
    if (Test-PiecePosition -State $State -Piece $candidate) {
        $State.ActivePiece = $candidate
        return $true
    }
    return $false
}

function Try-RotatePiece {
    param($State, [int]$Step, [switch]$UseKicks)
    if (-not $State.ActivePiece) { return $false }
    $from = $State.ActivePiece.Rotation
    $to = ($from + $Step) % 4
    if ($to -lt 0) { $to += 4 }
    $candidate = New-ActivePiece -Type $State.ActivePiece.Type -Rotation $to -X $State.ActivePiece.X -Y $State.ActivePiece.Y
    $candidate.Id = $State.ActivePiece.Id

    if (-not $UseKicks) {
        if (Test-PiecePosition -State $State -Piece $candidate) {
            $State.ActivePiece = $candidate
            return $true
        }
        return $false
    }

    $kickTable = $State.KickData[$State.ActivePiece.Type]
    $kickKey = '{0}>{1}' -f $from, $to
    $tests = if ($kickTable.ContainsKey($kickKey)) { $kickTable[$kickKey] } else { @($(New-KickOffset 0 0)) }
    foreach ($offset in $tests) {
        $testPiece = New-ActivePiece -Type $candidate.Type -Rotation $candidate.Rotation -X ($candidate.X + $offset.X) -Y ($candidate.Y - $offset.Y)
        $testPiece.Id = $State.ActivePiece.Id
        if (Test-PiecePosition -State $State -Piece $testPiece) {
            $State.ActivePiece = $testPiece
            return $true
        }
    }

    return $false
}

function Complete-SprintRun {
    param($State)
    if ($State.SprintComplete) { return }
    $State.SprintComplete = $true
    $State.GameOver = $true
    $State.CompletedAtUtc = [DateTime]::UtcNow
    $State.FinalMessage = '40 lines cleared. Sprint complete.'

    $entry = [pscustomobject]@{
        timeMs = Get-ElapsedRunMilliseconds -State $State
        lines = $State.LinesCleared
        pieces = $State.PiecesPlaced
        keys = $State.KeyInputs
        timestamp = [DateTime]::UtcNow.ToString('o')
    }

    $entries = @($State.SprintTimes) + @($entry)
    $entries = @($entries | Sort-Object -Property @{ Expression = 'timeMs'; Descending = $false }, @{ Expression = 'timestamp'; Descending = $false })
    if ($entries.Count -gt 10) {
        $entries = @($entries[0..9])
    }

    $State.SprintTimes = @($entries)
    Save-SprintTimes -Path $State.SprintTimePath -Entries $State.SprintTimes
}

function Clear-CompletedLines {
    param($State)
    $cleared = 0
    $newBoard = New-Board -Rows $State.TotalRows -Columns $State.Columns
    $targetRow = $State.TotalRows - 1

    for ($y = $State.TotalRows - 1; $y -ge 0; $y--) {
        $complete = $true
        for ($x = 0; $x -lt $State.Columns; $x++) {
            if (-not $State.Board[$y, $x]) {
                $complete = $false
                break
            }
        }

        if ($complete) {
            $cleared++
            continue
        }

        for ($x = 0; $x -lt $State.Columns; $x++) {
            $newBoard[$targetRow, $x] = $State.Board[$y, $x]
        }
        $targetRow--
    }

    $State.Board = $newBoard
    if ($cleared -gt 0) {
        $State.LinesCleared += $cleared
        $State.Score += $State.ScoreTable[[Math]::Min($cleared, 4)]
    }

    if ($State.LinesCleared -ge $State.TargetLines) {
        Complete-SprintRun -State $State
    }

    return $cleared
}

function Lock-ActivePiece {
    param($State)
    foreach ($cell in (Get-PieceCells -Definitions $State.Definitions -Piece $State.ActivePiece)) {
        if ($cell.Y -ge 0 -and $cell.Y -lt $State.TotalRows) {
            $State.Board[$cell.Y, $cell.X] = $State.ActivePiece.Type
        }
    }

    $State.ActivePiece = $null
    $State.PiecesPlaced += 1
    [void](Clear-CompletedLines -State $State)

    if ($State.SprintComplete) {
        return
    }

    if (-not (Spawn-NewPiece -State $State)) {
        $State.GameOver = $true
        $State.FinalMessage = 'Run ended by top-out.'
    }
}

function Drop-OneRow {
    param($State, [bool]$AwardSoftDrop)
    if (Try-MovePiece -State $State -DeltaX 0 -DeltaY 1) {
        if ($AwardSoftDrop) {
            $State.Score += 1
        }
        return $true
    }

    Lock-ActivePiece -State $State
    return $false
}

function Hard-DropPiece {
    param($State)
    if (-not $State.ActivePiece) { return }
    while (Try-MovePiece -State $State -DeltaX 0 -DeltaY 1) {
    }
    Lock-ActivePiece -State $State
}

function Move-PieceToWall {
    param(
        $State,
        [int]$DeltaX
    )

    if (-not $State.ActivePiece) { return }
    while (Try-MovePiece -State $State -DeltaX $DeltaX -DeltaY 0) {
    }
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
            $State.FinalMessage = 'Run ended by top-out.'
            return $false
        }
        $State.ActivePiece = $candidate
    }
    else {
        $State.HoldPieceType = $currentType
        $State.ActivePiece = $null
        if (-not (Spawn-NewPiece -State $State)) {
            $State.GameOver = $true
            $State.FinalMessage = 'Run ended by top-out.'
            return $false
        }
    }

    $State.HoldUsed = $true
    $State.CurrentPieceInputs = 0
    return $true
}

function Update-GameClock {
    param($State)
    if ($State.GameClosed) { return }

    $now = [DateTime]::UtcNow

    if (-not $State.StartedAtUtc -and $now -ge $State.CountdownEndsUtc) {
        $State.StartedAtUtc = $State.CountdownEndsUtc
        $State.LastTickUtc = $State.StartedAtUtc
    }

    if (-not $State.StartedAtUtc -or $State.GameOver) {
        return
    }

    $elapsed = ($now - $State.LastTickUtc).TotalMilliseconds
    while ($elapsed -ge $State.GravityMilliseconds -and -not $State.GameOver) {
        [void](Drop-OneRow -State $State -AwardSoftDrop:$false)
        $State.LastTickUtc = $State.LastTickUtc.AddMilliseconds($State.GravityMilliseconds)
        $elapsed = ($now - $State.LastTickUtc).TotalMilliseconds
    }
}

function Update-GameSettings {
    param(
        $State,
        $Settings
    )

    if ($null -eq $Settings) { return }

    if ($null -ne $Settings.gravityMs) {
        $State.GravityMilliseconds = [Math]::Max(16, [int]$Settings.gravityMs)
    }

    if ($null -ne $Settings.countdownMs) {
        $State.CountdownMilliseconds = [Math]::Max(0, [int]$Settings.countdownMs)
    }
}

function Register-Input {
    param($State)
    $State.KeyInputs += 1
    $State.CurrentPieceInputs += 1
}

function Invoke-GameAction {
    param($State, [string]$Action)
    Update-GameClock -State $State

    if ($State.GameOver -or $State.GameClosed) {
        return
    }

    if ($Action -in @('moveLeft', 'moveRight', 'softDrop', 'hardDrop', 'rotateCcw', 'rotateCw', 'rotate180', 'hold')) {
        Register-Input -State $State
    }

    if (-not $State.StartedAtUtc) {
        if ($Action -eq 'hardDrop') {
            return
        }
    }

    switch ($Action) {
        'moveLeft' { [void](Try-MovePiece -State $State -DeltaX -1 -DeltaY 0) }
        'moveRight' { [void](Try-MovePiece -State $State -DeltaX 1 -DeltaY 0) }
        'moveLeftMax' { Move-PieceToWall -State $State -DeltaX -1 }
        'moveRightMax' { Move-PieceToWall -State $State -DeltaX 1 }
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
    $entries = @($entries | Sort-Object -Property @{ Expression = 'score'; Descending = $true }, @{ Expression = 'timestamp'; Descending = $false })
    if ($entries.Count -gt 10) {
        $entries = @($entries[0..9])
    }
    $State.HighScores = @($entries)
    Save-HighScores -Path $State.HighScorePath -Entries $State.HighScores
    $State.CanSubmitHighScore = $false
    $State.HighScoreSubmitted = $true
    return $true
}

function Close-GameSession {
    param($State)
    $State.GameClosed = $true
    $State.FinalMessage = 'Game session ended. You can close this browser tab.'
}

function Get-GhostCells {
    param($State)
    if (-not $State.ActivePiece) { return @() }
    $ghost = New-ActivePiece -Type $State.ActivePiece.Type -Rotation $State.ActivePiece.Rotation -X $State.ActivePiece.X -Y $State.ActivePiece.Y
    while (Test-PiecePosition -State $State -Piece (New-ActivePiece -Type $ghost.Type -Rotation $ghost.Rotation -X $ghost.X -Y ($ghost.Y + 1))) {
        $ghost = New-ActivePiece -Type $ghost.Type -Rotation $ghost.Rotation -X $ghost.X -Y ($ghost.Y + 1)
    }
    return Get-PieceCells -Definitions $State.Definitions -Piece $ghost
}

function Get-BoardSnapshot {
    param($State)
    $rows = @()
    for ($y = $State.HiddenRows; $y -lt $State.TotalRows; $y++) {
        $row = @()
        for ($x = 0; $x -lt $State.Columns; $x++) {
            $row += $State.Board[$y, $x]
        }
        $rows += ,@($row)
    }

    foreach ($cell in (Get-GhostCells -State $State)) {
        if ($cell.Y -ge $State.HiddenRows -and $cell.Y -lt $State.TotalRows) {
            $rowIndex = $cell.Y - $State.HiddenRows
            if (-not $rows[$rowIndex][$cell.X]) {
                $rows[$rowIndex][$cell.X] = ('ghost-{0}' -f $State.ActivePiece.Type)
            }
        }
    }

    if ($State.ActivePiece) {
        foreach ($cell in (Get-PieceCells -Definitions $State.Definitions -Piece $State.ActivePiece)) {
            if ($cell.Y -ge $State.HiddenRows -and $cell.Y -lt $State.TotalRows) {
                $rows[($cell.Y - $State.HiddenRows)][$cell.X] = $State.ActivePiece.Type
            }
        }
    }

    return $rows
}

function Get-CountdownRemainingMs {
    param($State)
    if ($State.StartedAtUtc) { return 0 }
    return [Math]::Max(0, [int](($State.CountdownEndsUtc - [DateTime]::UtcNow).TotalMilliseconds))
}

function Get-PublicGameState {
    param($State)
    Update-GameClock -State $State

    [pscustomobject]@{
        sessionId = $State.SessionId
        mode = '40L Sprint'
        rows = Get-BoardSnapshot -State $State
        score = $State.Score
        lines = $State.LinesCleared
        linesRemaining = Get-LinesRemaining -State $State
        hold = $State.HoldPieceType
        holdUsed = $State.HoldUsed
        nextQueue = @($State.PieceQueue | Select-Object -First 5)
        elapsedMs = Get-ElapsedRunMilliseconds -State $State
        countdownRemainingMs = Get-CountdownRemainingMs -State $State
        runStarted = [bool]$State.StartedAtUtc
        piecesPlaced = $State.PiecesPlaced
        keyInputs = $State.KeyInputs
        currentPieceInputs = $State.CurrentPieceInputs
        activePieceId = if ($State.ActivePiece) { $State.ActivePiece.Id } else { 0 }
        inputsPerPiece = Get-AverageKeysPerPiece -State $State
        sprintComplete = $State.SprintComplete
        gameOver = $State.GameOver
        gameClosed = $State.GameClosed
        finalMessage = $State.FinalMessage
        timing = [pscustomobject]@{
            gravityMs = $State.GravityMilliseconds
            countdownMs = $State.CountdownMilliseconds
        }
        sprintLeaderboard = @($State.SprintTimes)
        arcadeLeaderboard = @($State.HighScores)
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
