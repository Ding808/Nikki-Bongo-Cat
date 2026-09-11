param([switch]$IncludeUi)
$ErrorActionPreference = 'Stop'
$projects = @('DesktopTests', 'UsageTests', 'PetHitTesting')
if ($IncludeUi) { $projects += 'UiSmoke' }
foreach ($project in $projects) {
    dotnet run --project (Join-Path $PSScriptRoot "Tests/$project/$project.csproj") -c Release
    if ($LASTEXITCODE -ne 0) { throw "$project failed with exit code $LASTEXITCODE" }
}
