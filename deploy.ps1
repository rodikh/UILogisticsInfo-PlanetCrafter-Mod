# LogisticsInfo Deploy Script
# Builds the mod and copies the DLL to the game's BepInEx plugins folder.

$ErrorActionPreference = "Stop"
$ProjectDir = $PSScriptRoot
$GameDir = "C:\Program Files (x86)\Steam\steamapps\common\The Planet Crafter"
$PluginPath = Join-Path $GameDir "BepInEx\plugins\LogisticsInfo.dll"
$SourcePath = Join-Path $ProjectDir "bin\Release\netstandard2.1\LogisticsInfo.dll"

Write-Host "LogisticsInfo Deploy" -ForegroundColor Cyan
Write-Host "===================" -ForegroundColor Cyan
Write-Host ""

# Build
Write-Host "Building..." -ForegroundColor Yellow
Push-Location $ProjectDir
try {
    dotnet build -c Release
    if ($LASTEXITCODE -ne 0) {
        Write-Host ""
        Write-Host "Build failed." -ForegroundColor Red
        exit 1
    }
} finally {
    Pop-Location
}

Write-Host ""
Write-Host "Deploying to: $PluginPath" -ForegroundColor Yellow

# Copy with overwrite; catch file-in-use errors
try {
    Copy-Item -Path $SourcePath -Destination $PluginPath -Force
    Write-Host ""
    Write-Host "Deploy successful." -ForegroundColor Green
    Write-Host "Restart the game if it is running to load the update." -ForegroundColor Gray
    exit 0
} catch [System.IO.IOException] {
    Write-Host ""
    Write-Host "ERROR: Could not overwrite the plugin file." -ForegroundColor Red
    Write-Host "The file may be locked because The Planet Crafter is running." -ForegroundColor Red
    Write-Host ""
    Write-Host "Please close the game and run deploy again." -ForegroundColor Yellow
    exit 1
} catch {
    Write-Host ""
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
