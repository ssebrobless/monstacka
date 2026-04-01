Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$ErrorActionPreference = 'Stop'
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path

function Get-EnhancedExecutable {
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

function Open-MonStacka {
    $target = Get-EnhancedExecutable

    if (-not $target -or -not (Test-Path -LiteralPath $target)) {
        Show-MonStackaUnavailable
        return
    }

    Start-Process -FilePath $target -WorkingDirectory (Split-Path -Parent $target) | Out-Null
}

function Open-Edition {
    param(
        [string]$Edition
    )

    switch ($Edition) {
        'HTML' {
            $target = Join-Path $scriptRoot 'classic-html\index.html'
        }
        'MonStacka!' {
            Open-MonStacka
            return
        }
        default {
            return
        }
    }

    if (-not (Test-Path -LiteralPath $target)) {
        [System.Windows.Forms.MessageBox]::Show(
            ("The selected edition is not available yet:`n{0}" -f $target),
            'Launch Tetris',
            [System.Windows.Forms.MessageBoxButtons]::OK,
            [System.Windows.Forms.MessageBoxIcon]::Warning
        ) | Out-Null
        return
    }
    Start-Process -FilePath $target | Out-Null
}

$form = New-Object System.Windows.Forms.Form
$form.Text = 'Launch Tetris'
$form.StartPosition = 'CenterScreen'
$form.FormBorderStyle = 'FixedDialog'
$form.MaximizeBox = $false
$form.MinimizeBox = $false
$form.ClientSize = New-Object System.Drawing.Size(320, 160)
$form.TopMost = $true

$label = New-Object System.Windows.Forms.Label
$label.Text = 'Version'
$label.Location = New-Object System.Drawing.Point(20, 20)
$label.AutoSize = $true

$combo = New-Object System.Windows.Forms.ComboBox
$combo.Location = New-Object System.Drawing.Point(20, 45)
$combo.Size = New-Object System.Drawing.Size(280, 28)
$combo.DropDownStyle = [System.Windows.Forms.ComboBoxStyle]::DropDownList
[void]$combo.Items.Add('HTML')
[void]$combo.Items.Add('MonStacka!')
$combo.SelectedIndex = 0

$runButton = New-Object System.Windows.Forms.Button
$runButton.Text = 'Run'
$runButton.Location = New-Object System.Drawing.Point(20, 88)
$runButton.Size = New-Object System.Drawing.Size(280, 32)
$runButton.Add_Click({
    Open-Edition -Edition ([string]$combo.SelectedItem)
    $form.Close()
})

$form.Controls.Add($label)
$form.Controls.Add($combo)
$form.Controls.Add($runButton)

[void]$form.ShowDialog()
