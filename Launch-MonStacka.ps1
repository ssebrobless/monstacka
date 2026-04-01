Add-Type -AssemblyName System.Windows.Forms

$ErrorActionPreference = 'Stop'
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path

function Get-MonStackaExecutable {
    $releaseDir = Join-Path $scriptRoot 'enhanced\src-tauri\target\release'
    $preferredExecutables = @(
        'monstacka.exe',
        'MonStacka!.exe',
        'MonStacka.exe',
        'eris_tetris.exe',
        'ERIS Tetris.exe'
    )

    foreach ($name in $preferredExecutables) {
        $candidate = Join-Path $releaseDir $name
        if (Test-Path -LiteralPath $candidate) {
            return $candidate
        }
    }

    if (Test-Path -LiteralPath $releaseDir) {
        $fallbackExe = Get-ChildItem -LiteralPath $releaseDir -Filter '*.exe' -File -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -notmatch 'build-script|crash_reporter' } |
            Select-Object -First 1

        if ($fallbackExe) {
            return $fallbackExe.FullName
        }
    }

    return $null
}

function Show-MonStackaUnavailable {
    $message = @(
        'MonStacka! desktop app was not found.',
        '',
        'This launcher no longer falls back to the browser preview, because that can launch a broken development build.',
        '',
        'To run MonStacka!, either:',
        '1. Download the built desktop artifact/release from GitHub, or',
        '2. Build it locally from enhanced\ with:',
        '   npm install',
        '   npm run tauri:build',
        '',
        'Expected executable location:',
        (Join-Path $scriptRoot 'enhanced\src-tauri\target\release\monstacka.exe')
    ) -join [Environment]::NewLine

    [System.Windows.Forms.MessageBox]::Show(
        $message,
        'MonStacka! Not Built',
        [System.Windows.Forms.MessageBoxButtons]::OK,
        [System.Windows.Forms.MessageBoxIcon]::Information
    ) | Out-Null
}

$target = Get-MonStackaExecutable

if (-not $target -or -not (Test-Path -LiteralPath $target)) {
    Show-MonStackaUnavailable
    exit 1
}

Start-Process -FilePath $target -WorkingDirectory (Split-Path -Parent $target) | Out-Null
