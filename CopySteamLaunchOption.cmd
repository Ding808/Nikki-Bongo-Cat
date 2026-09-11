@echo off
setlocal
set "ROOT=%~dp0"
title Nikki Bongo Cat - Copy Steam Launch Option
cd /d "%ROOT%"
set "STATS=%ROOT%PetStatsOverlay\PetStatsOverlay.exe"

if not exist "%STATS%" set "STATS=%ROOT%PetStatsOverlay\bin\Release\net9.0-windows\win-x64\publish\PetStatsOverlay.exe"
if not exist "%STATS%" goto MissingExecutable

for %%I in ("%STATS%") do set "STATS=%%~fI"
set "OPTION="%STATS%" --steam-launcher %%command%%"

if /i "%~1"=="--print" (
  echo %OPTION%
  exit /b 0
)

start "" "%STATS%" --copy-steam-option
exit /b 0

:MissingExecutable
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%ROOT%LauncherMessages.ps1"
exit /b 1
