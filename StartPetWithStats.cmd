@echo off
setlocal
set "ROOT=%~dp0"

call :FindStats
if defined STATS goto Launch

where dotnet.exe >nul 2>nul
if errorlevel 1 goto MissingRuntime

if not exist "%ROOT%PetStatsOverlay\PetStatsOverlay.csproj" goto MissingRuntime

echo [Nikki Bongo Cat] PetStatsOverlay.exe is missing.
echo This looks like a source download. Building a portable copy now...
dotnet publish "%ROOT%PetStatsOverlay\PetStatsOverlay.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
if errorlevel 1 goto BuildFailed

call :FindStats
if not defined STATS goto BuildFailed

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

:MissingRuntime
echo.
echo [Nikki Bongo Cat] Cannot find PetStatsOverlay.exe.
echo You downloaded the source code instead of the packaged release.
echo Download Nikki-Bongo-Cat-win-x64.zip from:
echo https://github.com/Ding808/Nikki-Bongo-Cat/releases/latest
echo.
echo Developers can also install the .NET 9 SDK and run this file again.
pause
exit /b 1

:BuildFailed
echo.
echo [Nikki Bongo Cat] Automatic build failed.
echo Download Nikki-Bongo-Cat-win-x64.zip from:
echo https://github.com/Ding808/Nikki-Bongo-Cat/releases/latest
pause
exit /b 1
