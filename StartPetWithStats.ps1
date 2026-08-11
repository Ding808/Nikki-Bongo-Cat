$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$pet = Join-Path $root "BongoCatMver.exe"
$stats = Join-Path $root "PetStatsOverlay\PetStatsOverlay.exe"

if (-not (Test-Path $stats)) {
    $stats = Join-Path $root "PetStatsOverlay\bin\Release\net9.0-windows\win-x64\PetStatsOverlay.exe"
}

if (-not (Test-Path $stats)) {
    $stats = Join-Path $root "PetStatsOverlay\bin\Release\net9.0-windows\win-x64\publish\PetStatsOverlay.exe"
}

if (-not (Test-Path $stats)) {
    $stats = Join-Path $root "PetStatsOverlay\bin\Debug\net9.0-windows\PetStatsOverlay.exe"
}

if (-not (Test-Path $pet)) {
    throw "Cannot find BongoCatMver.exe"
}

if (-not (Test-Path $stats)) {
    throw "Cannot find PetStatsOverlay.exe. Please make sure the PetStatsOverlay folder is next to this launcher."
}

& (Join-Path $root "SyncLive2DModel.ps1")

Start-Process -FilePath $pet -WorkingDirectory $root
Start-Process -FilePath $stats -WorkingDirectory (Split-Path -Parent $stats)
