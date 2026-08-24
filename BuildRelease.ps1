param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot "artifacts")
)

$ErrorActionPreference = "Stop"

$repositoryRoot = [IO.Path]::GetFullPath($PSScriptRoot)
$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
$defaultArtifactsRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot "artifacts"))

$isArtifactsRoot = $outputRoot.Equals($defaultArtifactsRoot, [StringComparison]::OrdinalIgnoreCase)
$isInsideArtifacts = $outputRoot.StartsWith(
    $defaultArtifactsRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar,
    [StringComparison]::OrdinalIgnoreCase)
if (-not ($isArtifactsRoot -or $isInsideArtifacts)) {
    throw "OutputDirectory must stay inside $defaultArtifactsRoot"
}

$publishDirectory = Join-Path $outputRoot "publish"
$packageDirectory = Join-Path $outputRoot "Nikki-Bongo-Cat"
$archivePath = Join-Path $outputRoot "Nikki-Bongo-Cat-win-x64.zip"

foreach ($path in @($publishDirectory, $packageDirectory)) {
    if ([IO.Directory]::Exists($path)) {
        [IO.Directory]::Delete($path, $true)
    }
}

if ([IO.File]::Exists($archivePath)) {
    [IO.File]::Delete($archivePath)
}

New-Item -ItemType Directory -Path $publishDirectory, $packageDirectory -Force | Out-Null

dotnet publish (Join-Path $repositoryRoot "PetStatsOverlay\PetStatsOverlay.csproj") `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:DebugType=None `
    -o $publishDirectory

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

$rootFiles = @(
    "BongoCatMver.exe",
    "BongoCatMverUI.dll",
    "BongoCatUI.exe",
    "config.json",
    "CopySteamLaunchOption.cmd",
    "d3dx10_43.dll",
    "MaterialDesignColors.dll",
    "MaterialDesignThemes.Wpf.dll",
    "msvcp140.dll",
    "openal32.dll",
    "README.md",
    "sfml-audio-2.dll",
    "sfml-graphics-2.dll",
    "sfml-network-2.dll",
    "sfml-system-2.dll",
    "sfml-window-2.dll",
    "StartPetWithStats.cmd",
    "StartPetWithStats.Steam.cmd",
    "vcruntime140.dll",
    "vcruntime140_1.dll"
)

foreach ($relativePath in $rootFiles) {
    $source = Join-Path $repositoryRoot $relativePath
    if (-not [IO.File]::Exists($source)) {
        throw "Required release file is missing: $relativePath"
    }

    Copy-Item -LiteralPath $source -Destination $packageDirectory
}

foreach ($directoryName in @("img", "Resources")) {
    $source = Join-Path $repositoryRoot $directoryName
    if (-not [IO.Directory]::Exists($source)) {
        throw "Required release directory is missing: $directoryName"
    }

    Copy-Item -LiteralPath $source -Destination $packageDirectory -Recurse
}

$overlayDirectory = Join-Path $packageDirectory "PetStatsOverlay"
New-Item -ItemType Directory -Path $overlayDirectory -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $publishDirectory "PetStatsOverlay.exe") -Destination $overlayDirectory

Compress-Archive -LiteralPath $packageDirectory -DestinationPath $archivePath -CompressionLevel Optimal

$archive = Get-Item -LiteralPath $archivePath
$archiveSizeMb = [Math]::Round($archive.Length / 1MB, 2)
[IO.Directory]::Delete($publishDirectory, $true)
[IO.Directory]::Delete($packageDirectory, $true)

Write-Host "Release package created: $($archive.FullName)"
Write-Host "Size: $archiveSizeMb MB"
