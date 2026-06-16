param(
    [string]$ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
    [string]$ExePath = '',
    [string]$ReportRoot = '',
    [int]$WaitSeconds = 20
)

$ErrorActionPreference = 'Stop'

function Assert-RenderedScreenshot {
    param(
        [string]$Path,
        [string]$Name
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "$Name screenshot missing: $Path"
    }

    $file = Get-Item -LiteralPath $Path
    if ($file.Length -le 4096) {
        throw "$Name screenshot is too small: $($file.Length) bytes"
    }

    Add-Type -AssemblyName System.Drawing
    $bitmap = [System.Drawing.Bitmap]::FromFile($Path)
    try {
        if ($bitmap.Width -lt 800 -or $bitmap.Height -lt 450) {
            throw "$Name screenshot dimensions are too small: $($bitmap.Width)x$($bitmap.Height)"
        }

        $sampled = 0
        $blueish = 0
        $dark = 0
        $saturated = 0
        $buckets = New-Object 'System.Collections.Generic.HashSet[int]'
        $stepX = [Math]::Max(1, [int]($bitmap.Width / 120))
        $stepY = [Math]::Max(1, [int]($bitmap.Height / 80))

        for ($y = 0; $y -lt $bitmap.Height; $y += $stepY) {
            for ($x = 0; $x -lt $bitmap.Width; $x += $stepX) {
                $pixel = $bitmap.GetPixel($x, $y)
                $sampled += 1
                $max = [Math]::Max($pixel.R, [Math]::Max($pixel.G, $pixel.B))
                $min = [Math]::Min($pixel.R, [Math]::Min($pixel.G, $pixel.B))
                if ($pixel.B -gt ($pixel.R + 12) -and $pixel.B -gt ($pixel.G + 4)) {
                    $blueish += 1
                }
                if ($max -lt 55) {
                    $dark += 1
                }
                if (($max - $min) -gt 35) {
                    $saturated += 1
                }

                [void]$buckets.Add((([int]($pixel.R / 16)) -shl 8) -bor (([int]($pixel.G / 16)) -shl 4) -bor ([int]($pixel.B / 16)))
            }
        }

        if ($sampled -le 0) {
            throw "$Name screenshot sampler did not inspect pixels."
        }
        if ($buckets.Count -lt 18) {
            throw "$Name screenshot has too few color buckets: $($buckets.Count)"
        }
        if ($blueish -lt ($sampled * 0.10)) {
            throw "$Name screenshot does not contain enough blue game background pixels."
        }
        if ($dark -lt ($sampled * 0.02)) {
            throw "$Name screenshot does not contain enough dark panel/depth pixels."
        }
        if ($saturated -lt ($sampled * 0.05)) {
            throw "$Name screenshot does not contain enough saturated block/UI pixels."
        }
    }
    finally {
        $bitmap.Dispose()
    }
}

if ([string]::IsNullOrWhiteSpace($ExePath)) {
    $ExePath = Join-Path $ProjectPath 'Builds\Windows\MonStackaV2.exe'
}

if (-not (Test-Path -LiteralPath $ExePath)) {
    throw "Built player not found: $ExePath"
}

if ([string]::IsNullOrWhiteSpace($ReportRoot)) {
    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $ReportRoot = Join-Path $ProjectPath "Builds\Reports\BuiltPlayerSmokeSuite\$stamp"
}

New-Item -ItemType Directory -Force -Path $ReportRoot | Out-Null

$captureScript = Join-Path $PSScriptRoot 'smoke-capture.ps1'
$keyboardScript = Join-Path $PSScriptRoot 'run-built-player-keyboard-smoke.ps1'

$captures = @(
    @{ Name = 'ogbm'; Mode = 'ogbm'; Chapter = '' },
    @{ Name = 'x4lines'; Mode = 'x4lines'; Chapter = '' },
    @{ Name = 'training'; Mode = 'training'; Chapter = '' },
    @{ Name = 'story-1-1'; Mode = 'story'; Chapter = '1.1' },
    @{ Name = 'story-1-3'; Mode = 'story'; Chapter = '1.3' }
)

foreach ($scenario in $captures) {
    $scenarioName = [string]($scenario['Name'])
    $scenarioMode = [string]($scenario['Mode'])
    $scenarioChapter = [string]($scenario['Chapter'])
    $scenarioRoot = Join-Path $ReportRoot $scenarioName
    New-Item -ItemType Directory -Force -Path $scenarioRoot | Out-Null
    $capturePath = Join-Path $scenarioRoot 'capture.png'
    $reportPath = Join-Path $scenarioRoot 'smoke-report.txt'
    $logPath = Join-Path $scenarioRoot 'player.log'

    Write-Host "Running capture smoke: $scenarioName"
    if ([string]::IsNullOrWhiteSpace($scenarioChapter)) {
        & $captureScript `
            -ProjectPath $ProjectPath `
            -Mode $scenarioMode `
            -OutPath $capturePath `
            -ReportPath $reportPath `
            -LogPath $logPath `
            -WaitSeconds $WaitSeconds
    }
    else {
        & $captureScript `
            -ProjectPath $ProjectPath `
            -Mode $scenarioMode `
            -Chapter $scenarioChapter `
            -OutPath $capturePath `
            -ReportPath $reportPath `
            -LogPath $logPath `
            -WaitSeconds $WaitSeconds
    }
    Assert-RenderedScreenshot -Path $capturePath -Name $scenarioName
}

$keyboardScenarios = @(
    @{ Name = 'keyboard-ogbm'; Mode = 'ogbm'; Chapter = ''; Inputs = @('Right', 'Right', 'Left', 'RotateCW', 'HardDrop', 'Pause', 'Pause') },
    @{ Name = 'keyboard-x4lines'; Mode = 'x4lines'; Chapter = ''; Inputs = @('Left', 'Left', 'RotateCCW', 'SoftDrop', 'HardDrop') },
    @{ Name = 'keyboard-story-1-3'; Mode = 'story'; Chapter = '1.3'; Inputs = @('Hold', 'HardDrop', 'Swap1', 'Right', 'HardDrop', 'Pause', 'Pause') }
)

foreach ($scenario in $keyboardScenarios) {
    $scenarioName = [string]($scenario['Name'])
    $scenarioMode = [string]($scenario['Mode'])
    $scenarioChapter = [string]($scenario['Chapter'])
    $scenarioInputs = [string[]]($scenario['Inputs'])
    $scenarioRoot = Join-Path $ReportRoot $scenarioName
    Write-Host "Running keyboard smoke: $scenarioName"
    & $keyboardScript `
        -ProjectPath $ProjectPath `
        -ExePath $ExePath `
        -Mode $scenarioMode `
        -Chapter $scenarioChapter `
        -ReportRoot $scenarioRoot `
        -Inputs $scenarioInputs `
        -WaitSeconds $WaitSeconds
    Assert-RenderedScreenshot -Path (Join-Path $scenarioRoot 'capture.png') -Name $scenarioName
}

Write-Host "Built-player smoke suite passed."
Write-Host "Report root: $ReportRoot"
