Option Explicit

Dim shell
Dim fileSystem
Dim scriptRoot
Dim command

Set shell = CreateObject("WScript.Shell")
Set fileSystem = CreateObject("Scripting.FileSystemObject")

scriptRoot = fileSystem.GetParentFolderName(WScript.ScriptFullName)
command = "powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -WindowStyle Hidden -File " & Chr(34) & scriptRoot & "\Launcher.ps1" & Chr(34)

shell.Run command, 0, False
