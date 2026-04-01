@echo off
setlocal
cd /d "%~dp0"
wscript.exe //nologo "%~dp0Start-MonStacka.vbs"
endlocal
