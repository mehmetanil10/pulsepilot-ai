[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("migration", "api", "worker", "web")]
    [string]$Service
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$PSNativeCommandUseErrorActionPreference = $false

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$artifactDirectory = Join-Path $repositoryRoot "artifacts/container-build"
$logPath = Join-Path $artifactDirectory "$Service.log"
New-Item -ItemType Directory -Path $artifactDirectory -Force | Out-Null

Push-Location $repositoryRoot
try {
    Write-Host "> docker compose build --progress plain $Service" -ForegroundColor DarkGray
    & docker compose build --progress plain $Service 2>&1 |
        Tee-Object -FilePath $logPath
    $exitCode = $LASTEXITCODE

    if ($exitCode -ne 0) {
        $tail = (Get-Content -LiteralPath $logPath -Tail 20) -join [Environment]::NewLine
        $annotation = $tail.Replace("%", "%25").Replace("`r", "%0D").Replace("`n", "%0A")
        Write-Host "::error title=$Service image build failed::$annotation"
        throw "$Service image build failed with exit code $exitCode."
    }

    Write-Host "$Service image build passed. Log: $logPath" -ForegroundColor Green
}
finally {
    Pop-Location
}
