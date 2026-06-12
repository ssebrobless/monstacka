# Launches the built player headlessly-friendly (no focus required), waits for the
# smoke pass + self-capture, then closes the player.
param(
    [string]$Mode = "training",
    [string]$Chapter = "",
    [string]$OutPath = "C:\Users\fishe\Documents\projects\Tetris\repo_review\MonStacka-v2\psd_layers\_capture.png",
    [int]$WaitSeconds = 16
)
$exe = "C:\Users\fishe\Documents\projects\Tetris\repo_review\MonStacka-v2\Builds\Windows\MonStackaV2.exe"
if (Test-Path $OutPath) { Remove-Item $OutPath -Force }
$args = @("-monstacka-mode", $Mode, "-monstacka-capture", $OutPath,
          "-screen-fullscreen", "0", "-screen-width", "1600", "-screen-height", "900")
if ($Chapter -ne "") { $args += @("-monstacka-chapter", $Chapter, "-monstacka-skip-dialogue") }
$proc = Start-Process -FilePath $exe -ArgumentList $args -PassThru -WindowStyle Minimized
Start-Sleep -Seconds $WaitSeconds
Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
if (Test-Path $OutPath) { Write-Output "captured: $OutPath" } else { Write-Output "CAPTURE MISSING" }
