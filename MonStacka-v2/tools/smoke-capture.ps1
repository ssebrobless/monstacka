# Launches the built player, waits for the runtime smoke pass and self-capture,
# then lets the player close itself through -monstacka-smoke-quit.
param(
    [string]$ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
    [string]$Mode = 'training',
    [string]$Chapter = '',
    [string]$OutPath = '',
    [string]$ReportPath = '',
    [string]$LogPath = '',
    [int]$WaitSeconds = 16,
    [int]$ScreenWidth = 1600,
    [int]$ScreenHeight = 900
)

$ErrorActionPreference = 'Stop'

$exe = Join-Path $ProjectPath 'Builds\Windows\MonStackaV2.exe'
if (-not (Test-Path -LiteralPath $exe)) {
    throw "Built player not found: $exe"
}

$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$defaultRoot = Join-Path $ProjectPath "Builds\Reports\SmokeCapture\$stamp"
New-Item -ItemType Directory -Force -Path $defaultRoot | Out-Null

if ([string]::IsNullOrWhiteSpace($OutPath)) {
    $OutPath = Join-Path $defaultRoot 'capture.png'
}
if ([string]::IsNullOrWhiteSpace($ReportPath)) {
    $ReportPath = Join-Path $defaultRoot 'smoke-report.txt'
}
if ([string]::IsNullOrWhiteSpace($LogPath)) {
    $LogPath = Join-Path $defaultRoot 'player.log'
}

foreach ($path in @($OutPath, $ReportPath, $LogPath)) {
    if (Test-Path -LiteralPath $path) {
        Remove-Item -LiteralPath $path -Force
    }
}

$args = @(
    '-monstacka-mode', $Mode,
    '-monstacka-capture', $OutPath,
    '-monstacka-smoke-report', $ReportPath,
    '-monstacka-smoke-quit',
    '-screen-fullscreen', '0',
    '-screen-width', "$ScreenWidth",
    '-screen-height', "$ScreenHeight",
    '-logFile', $LogPath
)
if ($Chapter -ne '') {
    $args += @('-monstacka-chapter', $Chapter, '-monstacka-skip-dialogue')
}

$proc = Start-Process -FilePath $exe -ArgumentList $args -PassThru -WindowStyle Minimized
if (-not $proc.WaitForExit($WaitSeconds * 1000)) {
    Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
    throw "Smoke capture timed out after $WaitSeconds seconds."
}

$proc.Refresh()
if ($proc.ExitCode -ne 0) {
    throw "Smoke capture player exited with code $($proc.ExitCode). See $LogPath"
}

if (-not (Test-Path -LiteralPath $OutPath) -or (Get-Item -LiteralPath $OutPath).Length -le 0) {
    throw "Capture missing or empty: $OutPath"
}
if (-not (Test-Path -LiteralPath $ReportPath)) {
    throw "Smoke report missing: $ReportPath"
}

$report = Get-Content -Raw -LiteralPath $ReportPath
if ($report -notmatch 'RESULT: PASS') {
    throw "Smoke report did not pass. See $ReportPath"
}

Write-Output "captured: $OutPath"
Write-Output "report:   $ReportPath"
Write-Output "log:      $LogPath"
