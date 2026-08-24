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
echo.
echo [Nikki Bongo Cat] Cannot find PetStatsOverlay.exe.
echo Launcher folder: %ROOT%
echo.
echo This is not a C: or D: drive problem.
echo C:\Windows\System32\cmd.exe is the normal Windows command shell.
echo.
echo This folder is incomplete or is the GitHub source package.
echo Download the ready-to-use Nikki-Bongo-Cat-win-x64.zip from:
echo https://github.com/Ding808/Nikki-Bongo-Cat/releases/latest
echo.
echo Do not download the "Source code" or "Code - Download ZIP" package.
echo The ready-to-use package needs no .NET installation or build step.
pause
exit /b 1
