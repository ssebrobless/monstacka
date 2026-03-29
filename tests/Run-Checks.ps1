$ErrorActionPreference = 'Stop'

. (Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) '..\src\GameEngine.ps1')

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw "Assertion failed: $Message" }
}

function Assert-Equal {
    param($Actual, $Expected, [string]$Message)
    if ($Actual -ne $Expected) { throw "Assertion failed: $Message. Expected '$Expected' but got '$Actual'." }
}

$tempHighscores = Join-Path $PSScriptRoot 'temp-highscores.json'
$tempSprint = Join-Path $PSScriptRoot 'temp-sprint-times.json'
foreach ($path in @($tempHighscores, $tempSprint)) {
    if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Force }
}

$state = Initialize-GameState -HighScorePath $tempHighscores -SprintTimePath $tempSprint
Assert-True ($state.ActivePiece -ne $null) 'A new sprint run should spawn an active piece.'
Assert-Equal $state.LinesCleared 0 'A new sprint run should start at zero cleared lines.'
Assert-True ($state.PieceQueue.Count -ge 5) 'The next queue should be populated.'
Assert-True ($state.ActivePiece.Type -in @('I', 'J', 'L', 'T')) 'The opening piece should follow the stride-style restriction.'

$allPieces = @($state.ActivePiece.Type) + @($state.PieceQueue | Select-Object -First 6)
Assert-Equal (@($allPieces | Sort-Object -Unique).Count) 7 'The opening bag plus active piece should cover all seven tetrominoes.'

$startX = $state.ActivePiece.X
[void](Try-MovePiece -State $state -DeltaX -1 -DeltaY 0)
Assert-Equal $state.ActivePiece.X ($startX - 1) 'Moving left should update the piece position.'

$rotationBefore = $state.ActivePiece.Rotation
[void](Try-RotatePiece -State $state -Step 1 -UseKicks)
Assert-Equal $state.ActivePiece.Rotation (($rotationBefore + 1) % 4) 'Clockwise rotation should change rotation state.'

$kickState = Initialize-GameState -HighScorePath $tempHighscores -SprintTimePath $tempSprint
$kickState.ActivePiece = New-ActivePiece -Type 'T' -Rotation 0 -X -1 -Y 0
Assert-True (Try-RotatePiece -State $kickState -Step 1 -UseKicks) 'SRS kicks should recover a valid rotation near the wall.'

$ghostState = Initialize-GameState -HighScorePath $tempHighscores -SprintTimePath $tempSprint
$ghostCells = @(Get-GhostCells -State $ghostState)
Assert-True ($ghostCells.Count -gt 0) 'A visible piece should produce a ghost projection.'
Assert-True (($ghostCells | Measure-Object -Property Y -Maximum).Maximum -ge ($ghostState.ActivePiece.Y + 1)) 'Ghost cells should project below the active piece.'

$lineState = Initialize-GameState -HighScorePath $tempHighscores -SprintTimePath $tempSprint
$lineState.Board = New-Board -Rows $lineState.TotalRows -Columns $lineState.Columns
for ($x = 0; $x -lt $lineState.Columns; $x++) { $lineState.Board[($lineState.TotalRows - 1), $x] = 'I' }
[void](Clear-CompletedLines -State $lineState)
Assert-Equal $lineState.LinesCleared 1 'Clearing a full row should increment cleared line count.'
Assert-Equal (Get-LinesRemaining -State $lineState) 39 'Sprint mode should track lines remaining from 40.'

$holdState = Initialize-GameState -HighScorePath $tempHighscores -SprintTimePath $tempSprint
$firstType = $holdState.ActivePiece.Type
[void](Hold-CurrentPiece -State $holdState)
Assert-Equal $holdState.HoldPieceType $firstType 'Holding should store the current piece.'
Assert-True $holdState.HoldUsed 'Holding should mark hold as used.'
[void](Hold-CurrentPiece -State $holdState)
Assert-Equal $holdState.HoldPieceType $firstType 'Holding twice before lock should not change the stored piece.'

$completeState = Initialize-GameState -HighScorePath $tempHighscores -SprintTimePath $tempSprint
$completeState.StartedAtUtc = [DateTime]::UtcNow.AddSeconds(-30)
$completeState.CountdownEndsUtc = $completeState.StartedAtUtc
$completeState.LinesCleared = 39
$completeState.Board = New-Board -Rows $completeState.TotalRows -Columns $completeState.Columns
for ($x = 0; $x -lt $completeState.Columns; $x++) { $completeState.Board[($completeState.TotalRows - 1), $x] = 'I' }
[void](Clear-CompletedLines -State $completeState)
Assert-True $completeState.SprintComplete 'Clearing the 40th line should complete the sprint.'
Assert-True (Test-Path -LiteralPath $tempSprint) 'Completing a sprint should persist sprint times.'
$sprintEntries = @(Load-SprintTimes -Path $tempSprint)
Assert-True ($sprintEntries.Count -ge 1) 'Saved sprint runs should load back from disk.'

foreach ($path in @($tempHighscores, $tempSprint)) {
    if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Force }
}

Write-Host 'All checks passed.' -ForegroundColor Green
