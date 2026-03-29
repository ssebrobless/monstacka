@echo off
setlocal
cd /d "%~dp0"
mshta "vbscript:CreateObject(""WScript.Shell"").Run ""powershell -NoProfile -ExecutionPolicy Bypass -STA -WindowStyle Hidden -File """"%~dp0Launcher.ps1"""" "",0)(window.close)"
endlocal
