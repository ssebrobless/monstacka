$ErrorActionPreference = 'Stop'

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path

. (Join-Path $scriptRoot 'src\GameEngine.ps1')
. (Join-Path $scriptRoot 'src\Server.ps1')

try {
    $context = Initialize-TetrisContext -RootPath $scriptRoot
    Write-Host ''
    Write-Host 'PowerShell Tetris is running.' -ForegroundColor Green
    Write-Host ('Project path: {0}' -f $scriptRoot) -ForegroundColor Yellow
    Write-Host ('Open this URL in your browser: {0}' -f $context.BaseUrl) -ForegroundColor Cyan
    Write-Host 'Press Ctrl+C in this window to stop the server manually.' -ForegroundColor DarkGray
    Write-Host ''
    Start-TetrisServer -Context $context
}
finally {
}
