$ErrorActionPreference = 'Stop'

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path

function Show-LauncherMenu {
    Write-Host ''
    Write-Host 'PowerShell Tetris Launcher' -ForegroundColor Cyan
    Write-Host '1. Classic HTML Edition'
    Write-Host '2. Current PowerShell Sprint'
    Write-Host '3. Enhanced Edition'
    Write-Host '4. Cancel'
    Write-Host ''
}

function Invoke-CurrentPowerShellSprint {
    & (Join-Path $scriptRoot 'main.ps1')
}

function Show-Placeholder {
    param(
        [string]$EditionName
    )

    Write-Host ''
    Write-Host ("{0} is not available yet in this phase." -f $EditionName) -ForegroundColor Yellow
    Write-Host 'Current safe fallback: Current PowerShell Sprint' -ForegroundColor DarkGray
    Write-Host ''
    Read-Host 'Press Enter to return to the launcher'
}

while ($true) {
    Show-LauncherMenu
    $selection = Read-Host 'Choose an option'

    switch ($selection) {
        '1' { Show-Placeholder -EditionName 'Classic HTML Edition' }
        '2' { Invoke-CurrentPowerShellSprint; break }
        '3' { Show-Placeholder -EditionName 'Enhanced Edition' }
        '4' { break }
        default {
            Write-Host 'Please choose 1, 2, 3, or 4.' -ForegroundColor Yellow
        }
    }
}
