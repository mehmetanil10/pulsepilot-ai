[CmdletBinding()]
param(
    [ValidateNotNullOrEmpty()]
    [string]$BaseUrl = "http://127.0.0.1:3000"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$webRoot = Join-Path $repositoryRoot "src/PulsePilot.Web"
$artifactRoot = Join-Path $repositoryRoot "artifacts/demo"
$resultRoot = Join-Path $artifactRoot "landing-test-results"

$origin = [Uri]$BaseUrl
if ($origin.Scheme -notin @("http", "https") -or $origin.AbsolutePath -ne "/" `
    -or -not [string]::IsNullOrEmpty($origin.Query) `
    -or -not [string]::IsNullOrEmpty($origin.Fragment)) {
    throw "BaseUrl must be an HTTP(S) origin without a path, query, or fragment."
}

New-Item -ItemType Directory -Path $artifactRoot -Force | Out-Null
$env:PLAYWRIGHT_BASE_URL = $origin.GetLeftPart([UriPartial]::Authority)

Write-Host "> npm --prefix $webRoot run demo:capture:landing" -ForegroundColor DarkGray
& npm --prefix $webRoot run demo:capture:landing | Out-Host
if ($LASTEXITCODE -ne 0) {
    throw "Landing demo capture failed with exit code $LASTEXITCODE."
}

$video = Get-ChildItem -LiteralPath $resultRoot -Filter "*.webm" -File -Recurse |
    Sort-Object LastWriteTimeUtc -Descending |
    Select-Object -First 1
if ($null -eq $video) {
    throw "Playwright completed without producing a landing demo video."
}

$stableVideoPath = Join-Path $artifactRoot "pulsepilot-landing-demo.webm"
Copy-Item -LiteralPath $video.FullName -Destination $stableVideoPath -Force

Write-Host "Landing demo video: $stableVideoPath" -ForegroundColor Green
