[CmdletBinding()]
param(
    [switch]$NoRestore
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$solutionPath = Join-Path $repositoryRoot "PulsePilot.sln"
$reportDirectory = Join-Path $repositoryRoot "artifacts/security"
$reportPath = Join-Path $reportDirectory "dotnet-vulnerabilities.json"

New-Item -ItemType Directory -Path $reportDirectory -Force | Out-Null

$arguments = @(
    "package",
    "list",
    "--project", $solutionPath,
    "--vulnerable",
    "--include-transitive",
    "--format", "json",
    "--output-version", "1"
)
if ($NoRestore) {
    $arguments += "--no-restore"
}

Write-Host "> dotnet $($arguments -join ' ')" -ForegroundColor DarkGray
$output = & dotnet @arguments
if ($LASTEXITCODE -ne 0) {
    throw "dotnet package list failed with exit code $LASTEXITCODE."
}

$json = $output -join [Environment]::NewLine
$json | Set-Content -LiteralPath $reportPath -Encoding utf8
$report = $json | ConvertFrom-Json
$findings = New-Object "System.Collections.Generic.List[string]"

foreach ($project in @($report.projects)) {
    if ($null -eq $project.PSObject.Properties["frameworks"]) {
        continue
    }

    foreach ($framework in @($project.frameworks)) {
        foreach ($collectionName in @("topLevelPackages", "transitivePackages")) {
            if ($null -eq $framework.PSObject.Properties[$collectionName]) {
                continue
            }

            foreach ($package in @($framework.$collectionName)) {
                if ($null -eq $package.PSObject.Properties["vulnerabilities"]) {
                    continue
                }

                $vulnerabilities = @($package.vulnerabilities)
                if ($vulnerabilities.Count -eq 0) {
                    continue
                }

                foreach ($vulnerability in $vulnerabilities) {
                    $findings.Add(
                        "$($project.path): $($package.id) $($package.resolvedVersion) " +
                        "[$($vulnerability.severity)] $($vulnerability.advisoryurl)")
                }
            }
        }
    }
}

if ($findings.Count -gt 0) {
    foreach ($finding in $findings) {
        Write-Host "  - $finding" -ForegroundColor Red
    }

    throw "Found $($findings.Count) vulnerable .NET package advisory result(s)."
}

Write-Host "No vulnerable .NET packages were reported." -ForegroundColor Green
Write-Host "Report: $reportPath"
