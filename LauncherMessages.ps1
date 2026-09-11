param([string]$RootPath = $PSScriptRoot)

# Display launcher errors using the saved product language, even if the EXE is missing.
$language = 'en'
$settingsFiles = @(
    (Join-Path $RootPath 'PetStatsOverlay/pet-stats-settings.json'),
    (Join-Path $RootPath 'PetStatsOverlay/bin/Release/net9.0-windows/win-x64/publish/pet-stats-settings.json'),
    (Join-Path $RootPath 'PetStatsOverlay/bin/Release/net9.0-windows/win-x64/pet-stats-settings.json')
)
foreach ($settingsFile in $settingsFiles) {
    if (Test-Path -LiteralPath $settingsFile) {
        try {
            $saved = Get-Content -LiteralPath $settingsFile -Raw -Encoding UTF8 | ConvertFrom-Json
            if ($saved.CompanionUi.Language -like 'zh*') { $language = 'zh' }
            break
        } catch { }
    }
}

if ($language -eq 'zh') {
    Write-Host ''
    Write-Host '[Nikki Bongo Cat] 找不到 PetStatsOverlay.exe。'
    Write-Host '请下载完整的 Nikki-Bongo-Cat-win-x64.zip 成品包，并完整解压：'
    Write-Host 'https://github.com/Ding808/Nikki-Bongo-Cat/releases/latest'
    Write-Host '源码包不包含编译后的程序。成品包无需安装 .NET 或自行编译。'
    Write-Host ''
    Read-Host '按 Enter 关闭' | Out-Null
} else {
    Write-Host ''
    Write-Host '[Nikki Bongo Cat] Cannot find PetStatsOverlay.exe.'
    Write-Host 'Download Nikki-Bongo-Cat-win-x64.zip and extract the entire package:'
    Write-Host 'https://github.com/Ding808/Nikki-Bongo-Cat/releases/latest'
    Write-Host 'The source archive contains no built application. The release needs no .NET installation or build step.'
    Write-Host ''
    Read-Host 'Press Enter to close' | Out-Null
}
