[CmdletBinding()]
param(
    [switch]$SkipImageBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$webRoot = Join-Path $repositoryRoot "src/PulsePilot.Web"
$projectName = "pulsepilot-task35-e2e"

function Invoke-CheckedCommand {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $true)][string[]]$ArgumentList
    )

    Write-Host "> $FilePath $($ArgumentList -join ' ')" -ForegroundColor DarkGray
    & $FilePath @ArgumentList | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code ${LASTEXITCODE}: $FilePath $($ArgumentList -join ' ')"
    }
}

$env:IMAGE_TAG = "task35-e2e"
$env:API_PORT = "18035"
$env:WEB_PORT = "13035"
$env:POSTGRES_PORT = "15435"
$env:POSTGRES_DB = "pulsepilot_e2e"
$env:POSTGRES_USER = "pulsepilot_e2e"
$env:POSTGRES_PASSWORD = "e2e-$([Guid]::NewGuid().ToString('N'))"
$env:JWT_ISSUER = "PulsePilot.E2E"
$env:JWT_AUDIENCE = "PulsePilot.E2E.Client"
$jwtSecretBytes = New-Object byte[] 48
$randomNumberGenerator = [Security.Cryptography.RandomNumberGenerator]::Create()
try {
    $randomNumberGenerator.GetBytes($jwtSecretBytes)
}
finally {
    $randomNumberGenerator.Dispose()
}
$env:JWT_SECRET = [Convert]::ToBase64String($jwtSecretBytes)
$env:ASPNETCORE_ENVIRONMENT = "Production"
$env:OPENAI_ENABLED = "false"
$env:OPENAI_API_KEY = ""
$env:FEEDBACK_PROCESSING_ENABLED = "false"
$env:SEED_DEMO_DATA = "false"
$env:PLAYWRIGHT_BASE_URL = "http://127.0.0.1:13035"

$composeArguments = @(
    "compose",
    "--project-name", $projectName,
    "up",
    "--detach",
    "--wait"
)
if (-not $SkipImageBuild) {
    $composeArguments += "--build"
}

try {
    Invoke-CheckedCommand "docker" $composeArguments
    Invoke-CheckedCommand "npm" @("--prefix", $webRoot, "run", "test:e2e")
}
finally {
    Write-Host "> docker compose --project-name $projectName down --volumes --remove-orphans" -ForegroundColor DarkGray
    & docker compose --project-name $projectName down --volumes --remove-orphans | Out-Host
}
