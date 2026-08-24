@echo off
setlocal
set "ROOT=%~dp0"
set "STATS=%ROOT%PetStatsOverlay\PetStatsOverlay.exe"

if not exist "%STATS%" set "STATS=%ROOT%PetStatsOverlay\bin\Release\net9.0-windows\win-x64\publish\PetStatsOverlay.exe"
if not exist "%STATS%" goto MissingExecutable

for %%I in ("%STATS%") do set "STATS=%%~fI"
set "OPTION="%STATS%" --steam-launcher %%command%%"

if /i "%~1"=="--print" (
  echo %OPTION%
  exit /b 0
)

echo %OPTION%| clip
echo.
echo Steam launch option copied to the clipboard:
echo %OPTION%
echo.
echo Paste it into Steam: Bongo Cat - Properties - General - Launch Options
pause
exit /b 0

:MissingExecutable
echo.
echo [Nikki Bongo Cat] Cannot find PetStatsOverlay.exe.
echo Run StartPetWithStats.cmd once to build it, or download the packaged release:
echo https://github.com/Ding808/Nikki-Bongo-Cat/releases/latest
pause
exit /b 1
