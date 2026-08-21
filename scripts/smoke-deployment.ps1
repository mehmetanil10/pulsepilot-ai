[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$BaseUrl
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$origin = [Uri]$BaseUrl
if ($origin.Scheme -ne "https" -or $origin.AbsolutePath -ne "/" `
    -or -not [string]::IsNullOrEmpty($origin.Query) `
    -or -not [string]::IsNullOrEmpty($origin.Fragment)) {
    throw "BaseUrl must be an HTTPS origin without query or fragment."
}

$base = $origin.GetLeftPart([UriPartial]::Authority).TrimEnd('/')

function Invoke-SmokeRequest {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][int[]]$ExpectedStatusCodes
    )

    $response = Invoke-WebRequest `
        -Uri "$base$Path" `
        -Method Get `
        -MaximumRedirection 0 `
        -SkipHttpErrorCheck `
        -TimeoutSec 90

    if ($response.StatusCode -notin $ExpectedStatusCodes) {
        throw "Smoke request $Path returned $($response.StatusCode); expected $($ExpectedStatusCodes -join ', ')."
    }

    return $response
}

$health = Invoke-SmokeRequest -Path "/api/health" -ExpectedStatusCodes @(200)
$healthPayload = $health.Content | ConvertFrom-Json
if ($healthPayload.status -ne "healthy" -or $healthPayload.service -ne "pulsepilot-web") {
    throw "The deployment health payload is invalid."
}

$login = Invoke-SmokeRequest -Path "/login" -ExpectedStatusCodes @(200)
if ($login.Content -notmatch "PulsePilot") {
    throw "The login page did not contain the PulsePilot product marker."
}

foreach ($header in @("x-content-type-options", "x-frame-options", "referrer-policy")) {
    if (-not $login.Headers.ContainsKey($header)) {
        throw "The deployment response is missing the $header security header."
    }
}

Write-Host "Deployment smoke checks passed for $base." -ForegroundColor Green
