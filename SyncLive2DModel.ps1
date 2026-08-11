$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$configPath = Join-Path $root "config.json"
$standardDir = Join-Path $root "img\standard"
$runtimeModelDir = Join-Path $standardDir "cat_model"
$libraryDir = Join-Path $standardDir "live2d_models"

function Test-Live2DModelRoot {
    param([string]$Directory)

    if (-not (Test-Path -LiteralPath $Directory -PathType Container)) {
        return $false
    }

    $modelFiles = @(Get-ChildItem -LiteralPath $Directory -Filter "*.model3.json" -File)
    if ($modelFiles.Count -ne 1) {
        return $false
    }

    try {
        $modelJson = Get-Content -LiteralPath $modelFiles[0].FullName -Raw | ConvertFrom-Json
        $mocPath = Join-Path $Directory $modelJson.FileReferences.Moc
        if (-not (Test-Path -LiteralPath $mocPath -PathType Leaf)) {
            return $false
        }

        foreach ($texture in $modelJson.FileReferences.Textures) {
            if (-not (Test-Path -LiteralPath (Join-Path $Directory $texture) -PathType Leaf)) {
                return $false
            }
        }

        return $true
    }
    catch {
        return $false
    }
}

function Resolve-Live2DModelRoot {
    param([string]$Directory)

    if (Test-Live2DModelRoot -Directory $Directory) {
        return $Directory
    }

    $candidates = @(Get-ChildItem -LiteralPath $Directory -Filter "*.model3.json" -File -Recurse |
        ForEach-Object { $_.DirectoryName } |
        Select-Object -Unique |
        Where-Object { Test-Live2DModelRoot -Directory $_ } |
        Sort-Object @{ Expression = { Test-Path -LiteralPath (Join-Path $_ "petstats-live2d-profile.json") }; Descending = $true }, Length)

    if ($candidates.Count -eq 0) {
        return ""
    }

    return $candidates[0]
}

if (-not (Test-Path -LiteralPath $configPath)) {
    exit 0
}

$config = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
$modelId = $config.standard.live2d_model
if ([string]::IsNullOrWhiteSpace($modelId)) {
    exit 0
}

$sourceDir = Join-Path $libraryDir $modelId
if (-not (Test-Path -LiteralPath $sourceDir -PathType Container)) {
    exit 0
}

$modelRoot = Resolve-Live2DModelRoot -Directory $sourceDir
if ([string]::IsNullOrWhiteSpace($modelRoot)) {
    exit 0
}

$tempDir = Join-Path $standardDir ("cat_model_startup_tmp_" + (Get-Date -Format "yyyyMMdd_HHmmssfff"))
Copy-Item -LiteralPath $modelRoot -Destination $tempDir -Recurse

$tempModelFiles = Get-ChildItem -LiteralPath $tempDir -Filter "*.model3.json" -File
if ($tempModelFiles.Count -eq 1 -and $tempModelFiles[0].Name -ne "cat.model3.json") {
    Move-Item -LiteralPath $tempModelFiles[0].FullName -Destination (Join-Path $tempDir "cat.model3.json") -Force
}

$standardFull = [System.IO.Path]::GetFullPath($standardDir).TrimEnd('\', '/')
$runtimeFull = [System.IO.Path]::GetFullPath($runtimeModelDir).TrimEnd('\', '/')
if (-not ($runtimeFull.StartsWith($standardFull + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase))) {
    throw "Runtime Live2D path is outside img\standard."
}

if (Test-Path -LiteralPath $runtimeModelDir) {
    Remove-Item -LiteralPath $runtimeModelDir -Recurse -Force
}
Move-Item -LiteralPath $tempDir -Destination $runtimeModelDir
