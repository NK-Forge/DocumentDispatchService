param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$OutputRoot = "publish",
    [switch]$Zip
)

$ErrorActionPreference = "Stop"

$projectRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $projectRoot

$outDir = Join-Path $OutputRoot $Runtime

Write-Host "Publishing self-contained build ($Runtime)..." -ForegroundColor Cyan

dotnet publish -c $Configuration -r $Runtime --self-contained true -p:PublishSingleFile=true -o $outDir

Write-Host "Publish output: $outDir" -ForegroundColor Green

if ($Zip) {
    $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $zipName = "DocumentDispatchService-$Runtime-$timestamp.zip"
    $zipPath = Join-Path $OutputRoot $zipName

    if (Test-Path $zipPath) {
        Remove-Item $zipPath -Force
    }

    Write-Host "Creating zip: $zipPath" -ForegroundColor Cyan
    Compress-Archive -Path (Join-Path $outDir "*") -DestinationPath $zipPath -Force
    Write-Host "Zip created: $zipPath" -ForegroundColor Green
}
