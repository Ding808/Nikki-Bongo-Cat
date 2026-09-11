@echo off
setlocal
set "ROOT=%~dp0"
title Nikki Bongo Cat Launcher
cd /d "%ROOT%"

call :FindStats
if not defined STATS goto MissingExecutable

:Launch
for %%I in ("%STATS%") do set "STATSDIR=%%~dpI"
start "" /d "%STATSDIR%" "%STATS%" --launch-pet %*
exit /b 0

:FindStats
set "STATS="
if exist "%ROOT%PetStatsOverlay\PetStatsOverlay.exe" set "STATS=%ROOT%PetStatsOverlay\PetStatsOverlay.exe"
if not defined STATS if exist "%ROOT%PetStatsOverlay\bin\Release\net9.0-windows\win-x64\publish\PetStatsOverlay.exe" set "STATS=%ROOT%PetStatsOverlay\bin\Release\net9.0-windows\win-x64\publish\PetStatsOverlay.exe"
if not defined STATS if exist "%ROOT%PetStatsOverlay\bin\Release\net9.0-windows\win-x64\PetStatsOverlay.exe" set "STATS=%ROOT%PetStatsOverlay\bin\Release\net9.0-windows\win-x64\PetStatsOverlay.exe"
if not defined STATS if exist "%ROOT%PetStatsOverlay\bin\Debug\net9.0-windows\PetStatsOverlay.exe" set "STATS=%ROOT%PetStatsOverlay\bin\Debug\net9.0-windows\PetStatsOverlay.exe"
exit /b 0

:MissingExecutable
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%ROOT%LauncherMessages.ps1"
exit /b 1
