@echo off
setlocal
set "ROOT=%~dp0"
set "STATS=%ROOT%PetStatsOverlay\PetStatsOverlay.exe"

if not exist "%STATS%" (
  set "STATS=%ROOT%PetStatsOverlay\bin\Release\net9.0-windows\win-x64\publish\PetStatsOverlay.exe"
)

if not exist "%STATS%" (
  set "STATS=%ROOT%PetStatsOverlay\bin\Release\net9.0-windows\win-x64\PetStatsOverlay.exe"
)

if not exist "%STATS%" (
  set "STATS=%ROOT%PetStatsOverlay\bin\Debug\net9.0-windows\PetStatsOverlay.exe"
)

if not exist "%STATS%" (
  echo [Nikki Bongo Cat] Cannot find PetStatsOverlay.exe.
  echo Build the project first or use a packaged release.
  pause
  exit /b 1
)

for %%I in ("%STATS%") do set "STATSDIR=%%~dpI"

start "" /d "%STATSDIR%" "%STATS%" --launch-pet %*
