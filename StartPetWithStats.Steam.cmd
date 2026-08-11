@echo off
setlocal
set "ROOT=%~dp0"
set "STATS=%ROOT%PetStatsOverlay\PetStatsOverlay.exe"
set "BONGO_CAT_STEAM_APPID=3419430"

if not exist "%STATS%" (
  set "STATS=%ROOT%PetStatsOverlay\bin\Release\net9.0-windows\win-x64\PetStatsOverlay.exe"
)

if not exist "%STATS%" (
  set "STATS=%ROOT%PetStatsOverlay\bin\Release\net9.0-windows\win-x64\publish\PetStatsOverlay.exe"
)

if not exist "%STATS%" (
  set "STATS=%ROOT%PetStatsOverlay\bin\Debug\net9.0-windows\PetStatsOverlay.exe"
)

if not exist "%STATS%" (
  echo Cannot find PetStatsOverlay.exe
  echo Please make sure the PetStatsOverlay folder is next to this launcher.
  pause
  exit /b 1
)

powershell -NoProfile -ExecutionPolicy Bypass -File "%ROOT%SyncLive2DModel.ps1"

start "" "steam://rungameid/%BONGO_CAT_STEAM_APPID%"

for %%I in ("%STATS%") do set "STATSDIR=%%~dpI"
cd /d "%STATSDIR%"
"%STATS%" --attach-steam-bongo-cat
