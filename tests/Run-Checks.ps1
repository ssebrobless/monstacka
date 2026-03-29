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

$tempPath = Join-Path $PSScriptRoot 'temp-highscores.json'
if (Test-Path -LiteralPath $tempPath) { Remove-Item -LiteralPath $tempPath -Force }

$state = Initialize-GameState -HighScorePath $tempPath
Assert-True ($state.ActivePiece -ne $null) 'A new game should spawn an active piece.'

$startX = $state.ActivePiece.X
[void](Try-MovePiece -State $state -DeltaX -1 -DeltaY 0)
Assert-Equal $state.ActivePiece.X ($startX - 1) 'Moving left should update the piece position.'

$rotationBefore = $state.ActivePiece.Rotation
[void](Try-RotatePiece -State $state -Step 1 -UseKicks)
Assert-Equal $state.ActivePiece.Rotation (($rotationBefore + 1) % 4) 'Clockwise rotation should change rotation state.'

$state.Board = New-Board -Rows $state.TotalRows -Columns $state.Columns
for ($x = 0; $x -lt $state.Columns; $x++) { $state.Board[($state.TotalRows - 1), $x] = 'I' }
[void](Clear-CompletedLines -State $state)
Assert-Equal $state.LinesCleared 1 'Clearing a full row should increment cleared line count.'
Assert-Equal $state.Score 100 'Single line clear should award 100 points.'

$state = Initialize-GameState -HighScorePath $tempPath
$firstType = $state.ActivePiece.Type
[void](Hold-CurrentPiece -State $state)
Assert-Equal $state.HoldPieceType $firstType 'Holding should store the current piece.'
Assert-True $state.HoldUsed 'Holding should mark hold as used.'
[void](Hold-CurrentPiece -State $state)
Assert-Equal $state.HoldPieceType $firstType 'Holding twice before lock should not change the stored piece.'

$kickState = Initialize-GameState -HighScorePath $tempPath
$kickState.ActivePiece = New-ActivePiece -Type 'T' -Rotation 0 -X -1 -Y 0
Assert-True (Try-RotatePiece -State $kickState -Step 1 -UseKicks) 'SRS kicks should recover a valid rotation near the wall.'

$entries = @([pscustomobject]@{ initials = 'AAA'; score = 1000; lines = 10; timestamp = '2026-01-01T00:00:00.0000000Z' })
Save-HighScores -Path $tempPath -Entries $entries
$loaded = Load-HighScores -Path $tempPath
Assert-Equal $loaded[0].score 1000 'Saved high score should load back from disk.'
Assert-Equal (Normalize-Initials -Initials 'ab!') 'AB_' 'Initials should normalize and pad to 3 characters.'
Assert-True (Test-HighScoreQualification -Entries $loaded -Score 1001) 'Higher scores should qualify for the leaderboard.'

if (Test-Path -LiteralPath $tempPath) { Remove-Item -LiteralPath $tempPath -Force }
Write-Host 'All checks passed.' -ForegroundColor Green
