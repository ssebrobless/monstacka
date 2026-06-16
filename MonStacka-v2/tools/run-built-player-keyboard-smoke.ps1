param(
    [string]$ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
    [string]$ExePath = '',
    [string]$Mode = 'story',
    [string]$Chapter = '1.3',
    [string]$ReportRoot = '',
    [string[]]$Inputs = @('Right', 'Right', 'Left', 'RotateCW', 'SoftDrop', 'HardDrop', 'Pause', 'Pause'),
    [int]$InitialWaitMilliseconds = 1200,
    [int]$BetweenInputMilliseconds = 180,
    [int]$WaitSeconds = 14,
    [int]$ScreenWidth = 1280,
    [int]$ScreenHeight = 720
)

$ErrorActionPreference = 'Stop'

function Convert-ToSendKeysToken {
    param([string]$InputName)

    switch ($InputName.ToLowerInvariant()) {
        'left' { return '{LEFT}' }
        'right' { return '{RIGHT}' }
        'softdrop' { return '{DOWN}' }
        'down' { return '{DOWN}' }
        'harddrop' { return ' ' }
        'space' { return ' ' }
        'hold' { return 'c' }
        'shift' { return 'c' }
        'rotatecw' { return 'x' }
        'rotateccw' { return 'z' }
        'rotate180' { return 'a' }
        'pause' { return 'p' }
        'escape' { return '{ESC}' }
        'restart' { return 'r' }
        'swap1' { return '1' }
        'swap2' { return '2' }
        'swap3' { return '3' }
        'enter' { return '{ENTER}' }
        default { throw "Unsupported keyboard smoke input: $InputName" }
    }
}

function Wait-ForMainWindow {
    param(
        [System.Diagnostics.Process]$Process,
        [int]$TimeoutMilliseconds
    )

    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMilliseconds)
    do {
        $Process.Refresh()
        if ($Process.HasExited) {
            throw "Player exited before a window was available."
        }

        if ($Process.MainWindowHandle -ne [IntPtr]::Zero) {
            return
        }

        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)

    throw "Timed out waiting for the built player window."
}

if ([string]::IsNullOrWhiteSpace($ExePath)) {
    $ExePath = Join-Path $ProjectPath 'Builds\Windows\MonStackaV2.exe'
}

if (-not (Test-Path -LiteralPath $ExePath)) {
    throw "Built player not found: $ExePath"
}

if ([string]::IsNullOrWhiteSpace($ReportRoot)) {
    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $ReportRoot = Join-Path $ProjectPath "Builds\Reports\BuiltPlayerKeyboardSmoke\$stamp"
}

New-Item -ItemType Directory -Force -Path $ReportRoot | Out-Null

$capturePath = Join-Path $ReportRoot 'capture.png'
$reportPath = Join-Path $ReportRoot 'smoke-report.txt'
$logPath = Join-Path $ReportRoot 'player.log'

foreach ($path in @($capturePath, $reportPath, $logPath)) {
    if (Test-Path -LiteralPath $path) {
        Remove-Item -LiteralPath $path -Force
    }
}

$playerArgs = @(
    '-monstacka-mode', $Mode,
    '-monstacka-capture', $capturePath,
    '-monstacka-smoke-report', $reportPath,
    '-monstacka-smoke-quit',
    '-screen-fullscreen', '0',
    '-screen-width', "$ScreenWidth",
    '-screen-height', "$ScreenHeight",
    '-logFile', $logPath
)

if (-not [string]::IsNullOrWhiteSpace($Chapter)) {
    $playerArgs += @('-monstacka-chapter', $Chapter, '-monstacka-skip-dialogue')
}

Write-Host "Launching built player keyboard smoke: $ExePath"
$process = Start-Process -FilePath $ExePath -ArgumentList $playerArgs -PassThru -WindowStyle Normal

try {
    Wait-ForMainWindow -Process $process -TimeoutMilliseconds 8000
    Start-Sleep -Milliseconds $InitialWaitMilliseconds

    $shell = New-Object -ComObject WScript.Shell
    $activated = $false
    for ($attempt = 0; $attempt -lt 8 -and -not $activated; $attempt += 1) {
        $activated = [bool]$shell.AppActivate($process.Id)
        if (-not $activated) {
            Start-Sleep -Milliseconds 250
        }
    }

    if (-not $activated) {
        throw "Could not focus the built player window for keyboard smoke input."
    }

    foreach ($inputName in $Inputs) {
        $token = Convert-ToSendKeysToken -InputName $inputName
        Write-Host "Sending input: $inputName"
        $shell.SendKeys($token)
        Start-Sleep -Milliseconds $BetweenInputMilliseconds
    }

    if (-not $process.WaitForExit($WaitSeconds * 1000)) {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        throw "Built player smoke timed out after $WaitSeconds seconds."
    }

    $process.Refresh()
    if ($process.ExitCode -ne 0) {
        throw "Built player smoke exited with code $($process.ExitCode). See $logPath"
    }
}
finally {
    if (-not $process.HasExited) {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
    }
}

if (-not (Test-Path -LiteralPath $capturePath) -or (Get-Item -LiteralPath $capturePath).Length -le 0) {
    throw "Smoke capture missing or empty: $capturePath"
}

if (-not (Test-Path -LiteralPath $reportPath)) {
    throw "Smoke report missing: $reportPath"
}

$report = Get-Content -Raw -LiteralPath $reportPath
if ($report -notmatch 'RESULT: PASS') {
    throw "Smoke report did not pass. See $reportPath"
}

Write-Host "Keyboard smoke passed."
Write-Host "Capture: $capturePath"
Write-Host "Report:  $reportPath"
Write-Host "Log:     $logPath"
