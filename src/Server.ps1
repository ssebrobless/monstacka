Set-StrictMode -Version 2.0

function Initialize-TetrisContext {
    param([string]$RootPath)
    [pscustomobject]@{
        RootPath = $RootPath
        WebRoot = Join-Path $RootPath 'web'
        BaseUrl = 'http://localhost:8080/'
        ShutdownRequested = $false
        State = Initialize-GameState -HighScorePath (Join-Path $RootPath 'data\highscores.json')
    }
}

function Start-TetrisServer {
    param($Context)
    $listener = New-Object System.Net.HttpListener
    $listener.Prefixes.Add($Context.BaseUrl)
    $listener.Start()
    try {
        while ($listener.IsListening -and -not $Context.ShutdownRequested) {
            try {
                $httpContext = $listener.GetContext()
            }
            catch {
                break
            }

            try {
                Process-TetrisRequest -Context $Context -HttpContext $httpContext
            }
            catch {
                Write-JsonResponse -Response $httpContext.Response -StatusCode 500 -Body @{ error = 'Internal server error.' }
            }
        }
    }
    finally {
        Stop-TetrisServer -Listener $listener
    }
}

function Stop-TetrisServer {
    param($Listener)
    if ($Listener -and $Listener.IsListening) { $Listener.Stop() }
}

function Get-RequestBody {
    param($Request)
    $reader = New-Object System.IO.StreamReader($Request.InputStream, $Request.ContentEncoding)
    try { $reader.ReadToEnd() } finally { $reader.Dispose() }
}

function Write-JsonResponse {
    param($Response, [int]$StatusCode, $Body)
    $json = $Body | ConvertTo-Json -Depth 8
    $buffer = [System.Text.Encoding]::UTF8.GetBytes($json)
    $Response.StatusCode = $StatusCode
    $Response.ContentType = 'application/json; charset=utf-8'
    $Response.ContentLength64 = $buffer.Length
    $Response.OutputStream.Write($buffer, 0, $buffer.Length)
    $Response.OutputStream.Close()
}

function Write-FileResponse {
    param($Response, [string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) {
        Write-JsonResponse -Response $Response -StatusCode 404 -Body @{ error = 'Not found.' }
        return
    }
    $contentType = switch ([IO.Path]::GetExtension($Path).ToLowerInvariant()) {
        '.html' { 'text/html; charset=utf-8' }
        '.js' { 'application/javascript; charset=utf-8' }
        '.css' { 'text/css; charset=utf-8' }
        default { 'text/plain; charset=utf-8' }
    }
    $bytes = [IO.File]::ReadAllBytes($Path)
    $Response.StatusCode = 200
    $Response.ContentType = $contentType
    $Response.ContentLength64 = $bytes.Length
    $Response.OutputStream.Write($bytes, 0, $bytes.Length)
    $Response.OutputStream.Close()
}

function Process-TetrisRequest {
    param($Context, $HttpContext)
    $request = $HttpContext.Request
    $response = $HttpContext.Response
    switch ("$($request.HttpMethod) $($request.Url.AbsolutePath)") {
        'GET /' { Write-FileResponse -Response $response -Path (Join-Path $Context.WebRoot 'index.html'); return }
        'GET /app.js' { Write-FileResponse -Response $response -Path (Join-Path $Context.WebRoot 'app.js'); return }
        'GET /styles.css' { Write-FileResponse -Response $response -Path (Join-Path $Context.WebRoot 'styles.css'); return }
        'GET /api/state' { Write-JsonResponse -Response $response -StatusCode 200 -Body (Get-PublicGameState -State $Context.State); return }
        'POST /api/action' {
            $payload = ConvertFrom-Json -InputObject (Get-RequestBody -Request $request)
            Invoke-GameAction -State $Context.State -Action ([string]$payload.action)
            Write-JsonResponse -Response $response -StatusCode 200 -Body (Get-PublicGameState -State $Context.State)
            return
        }
        'POST /api/reset' {
            Reset-GameState -State $Context.State | Out-Null
            Write-JsonResponse -Response $response -StatusCode 200 -Body (Get-PublicGameState -State $Context.State)
            return
        }
        'POST /api/highscores' {
            $payload = ConvertFrom-Json -InputObject (Get-RequestBody -Request $request)
            if (-not (Submit-HighScore -State $Context.State -Initials ([string]$payload.initials))) {
                Write-JsonResponse -Response $response -StatusCode 400 -Body @{ error = 'High score entry rejected.' }
                return
            }
            Write-JsonResponse -Response $response -StatusCode 200 -Body (Get-PublicGameState -State $Context.State)
            return
        }
        'POST /api/quit' {
            Close-GameSession -State $Context.State
            $Context.ShutdownRequested = $true
            Write-JsonResponse -Response $response -StatusCode 200 -Body (Get-PublicGameState -State $Context.State)
            return
        }
        default { Write-JsonResponse -Response $response -StatusCode 404 -Body @{ error = 'Route not found.' } }
    }
}
