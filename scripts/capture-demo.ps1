[CmdletBinding()]
param(
    [ValidateNotNullOrEmpty()]
    [string]$BaseUrl = "http://127.0.0.1:3000",
    [ValidateNotNullOrEmpty()]
    [string]$Email = "demo@pulsepilot.ai"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$webRoot = Join-Path $repositoryRoot "src/PulsePilot.Web"
$artifactRoot = Join-Path $repositoryRoot "artifacts/demo"
$screenshotRoot = Join-Path $repositoryRoot "docs/assets/screenshots"

if ([string]::IsNullOrWhiteSpace($env:PULSEPILOT_DEMO_PASSWORD)) {
    throw "Set PULSEPILOT_DEMO_PASSWORD for the seeded demo account before running this script."
}

$origin = [Uri]$BaseUrl
if ($origin.Scheme -notin @("http", "https") -or $origin.AbsolutePath -ne "/" `
    -or -not [string]::IsNullOrEmpty($origin.Query) `
    -or -not [string]::IsNullOrEmpty($origin.Fragment)) {
    throw "BaseUrl must be an HTTP(S) origin without a path, query, or fragment."
}

New-Item -ItemType Directory -Path $artifactRoot -Force | Out-Null
New-Item -ItemType Directory -Path $screenshotRoot -Force | Out-Null

$env:PLAYWRIGHT_BASE_URL = $origin.GetLeftPart([UriPartial]::Authority)
$env:PULSEPILOT_DEMO_EMAIL = $Email

Write-Host "> npm --prefix $webRoot run demo:capture" -ForegroundColor DarkGray
& npm --prefix $webRoot run demo:capture | Out-Host
if ($LASTEXITCODE -ne 0) {
    throw "Demo capture failed with exit code $LASTEXITCODE."
}

$video = Get-ChildItem -LiteralPath (Join-Path $artifactRoot "test-results") `
    -Filter "*.webm" -File -Recurse |
    Sort-Object LastWriteTimeUtc -Descending |
    Select-Object -First 1
if ($null -eq $video) {
    throw "Playwright completed without producing a demo video."
}

$stableVideoPath = Join-Path $artifactRoot "pulsepilot-demo.webm"
Copy-Item -LiteralPath $video.FullName -Destination $stableVideoPath -Force

Write-Host "Demo screenshots: $screenshotRoot" -ForegroundColor Green
Write-Host "Demo video: $stableVideoPath" -ForegroundColor Green
