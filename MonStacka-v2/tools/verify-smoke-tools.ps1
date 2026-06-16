param(
    [switch]$IncludeMac
)

$ErrorActionPreference = 'Stop'

$toolRoot = Split-Path -Parent $PSCommandPath
$scripts = @(
    'run-built-player-keyboard-smoke.ps1',
    'run-built-player-smoke-suite.ps1',
    'smoke-capture.ps1'
)

if ($IncludeMac) {
    $scripts += 'run-mac-remote-smoke.ps1'
}

foreach ($script in $scripts) {
    $path = Join-Path $toolRoot $script
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Missing smoke tool: $path"
    }

    $tokens = $null
    $errors = $null
    [System.Management.Automation.Language.Parser]::ParseFile($path, [ref]$tokens, [ref]$errors) | Out-Null
    if ($errors.Count -gt 0) {
        $message = ($errors | ForEach-Object { "$($_.Extent.StartLineNumber): $($_.Message)" }) -join '; '
        throw "PowerShell parse failed for $script`: $message"
    }
}

if ($IncludeMac) {
    $macSmokePath = Join-Path $toolRoot 'run-mac-remote-smoke.ps1'
    $macSmoke = Get-Content -Raw -LiteralPath $macSmokePath
    foreach ($required in @('Assert-SafeRemoteRunDirectory', '~/Library/Caches/MonStackaCodexSmoke', 'RemoveRemoteAfterRun', 'MonStackaV2.app/Contents/MacOS/MonStackaV2')) {
        if ($macSmoke -notmatch [regex]::Escape($required)) {
            throw "Mac smoke tool is missing required cleanup/run marker: $required"
        }
    }
}

$keyboardSmokePath = Join-Path $toolRoot 'run-built-player-keyboard-smoke.ps1'
$keyboardSmoke = Get-Content -Raw -LiteralPath $keyboardSmokePath
foreach ($required in @('WScript.Shell', 'SendKeys', '-monstacka-smoke-report', '-monstacka-capture')) {
    if ($keyboardSmoke -notmatch [regex]::Escape($required)) {
        throw "Keyboard smoke tool is missing required input/report marker: $required"
    }
}

$suiteSmokePath = Join-Path $toolRoot 'run-built-player-smoke-suite.ps1'
$suiteSmoke = Get-Content -Raw -LiteralPath $suiteSmokePath
foreach ($required in @('keyboard-ogbm', 'keyboard-x4lines', 'keyboard-story-1-3', 'Assert-RenderedScreenshot')) {
    if ($suiteSmoke -notmatch [regex]::Escape($required)) {
        throw "Built-player suite is missing required marker: $required"
    }
}

Write-Host 'Smoke tool verification passed.'
