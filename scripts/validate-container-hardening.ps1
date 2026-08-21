[CmdletBinding()]
param(
    [switch]$SkipImageInspect
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
Push-Location $repositoryRoot

function Assert-Condition {
    param(
        [Parameter(Mandatory = $true)][bool]$Condition,
        [Parameter(Mandatory = $true)][string]$Message
    )

    if (-not $Condition) {
        throw "Container hardening validation failed: $Message"
    }
}

function Invoke-JsonCommand {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $true)][string[]]$ArgumentList
    )

    $output = & $FilePath @ArgumentList
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code ${LASTEXITCODE}: $FilePath $($ArgumentList -join ' ')"
    }

    return $output | ConvertFrom-Json
}

try {
    $config = Invoke-JsonCommand "docker" @("compose", "config", "--format", "json")
    $appServices = @("migration", "api", "web", "worker")

    Assert-Condition ($config.networks.data.internal -eq $true) `
        "the data network must be internal"
    Assert-Condition ($config.services.database.networks.PSObject.Properties.Name -contains "data") `
        "database must join the isolated data network"
    Assert-Condition (-not ($config.services.database.networks.PSObject.Properties.Name -contains "app")) `
        "database must not join the application network"

    foreach ($serviceName in $appServices) {
        $service = $config.services.$serviceName
        Assert-Condition ($service.read_only -eq $true) "$serviceName root filesystem must be read-only"
        Assert-Condition ($service.init -eq $true) "$serviceName must use an init process"
        Assert-Condition ($service.cap_drop -contains "ALL") "$serviceName must drop all capabilities"
        Assert-Condition ($service.security_opt -contains "no-new-privileges:true") `
            "$serviceName must prevent privilege escalation"
        Assert-Condition ($service.pids_limit -gt 0 -and $service.pids_limit -le 512) `
            "$serviceName must have a bounded PID limit"
    }

    foreach ($serviceName in @("database", "api", "web")) {
        $service = $config.services.$serviceName
        foreach ($publishedPort in $service.ports) {
            Assert-Condition ($publishedPort.host_ip -eq "127.0.0.1") `
                "$serviceName published ports must default to loopback"
        }
    }

    Assert-Condition `
        ($config.services.api.environment.ASPNETCORE_ENVIRONMENT -eq "Production") `
        "ASP.NET Core must default to Production"

    if (-not $SkipImageInspect) {
        foreach ($serviceName in $appServices) {
            $service = $config.services.$serviceName
            $inspection = @(Invoke-JsonCommand "docker" @("image", "inspect", $service.image))[0]
            $user = [string]$inspection.Config.User
            Assert-Condition `
                (-not [string]::IsNullOrWhiteSpace($user) -and $user -notin @("0", "root", "0:0")) `
                "$serviceName image must declare a non-root user"
            Assert-Condition `
                ($inspection.Config.Labels.'org.opencontainers.image.source' -eq `
                    "https://github.com/mehmetanil10/pulsepilot-ai") `
                "$serviceName image must contain OCI source metadata"

            $embeddedEnvironment = @($inspection.Config.Env)
            foreach ($sensitivePrefix in @(
                "JWT__SECRET=",
                "OPENAI__APIKEY=",
                "POSTGRES_PASSWORD=",
                "CONNECTIONSTRINGS__DATABASE="
            )) {
                Assert-Condition `
                    (-not ($embeddedEnvironment | Where-Object {
                        $_.ToUpperInvariant().StartsWith($sensitivePrefix)
                    })) `
                    "$serviceName image must not embed $sensitivePrefix"
            }
        }

        foreach ($serviceName in @("api", "web")) {
            $service = $config.services.$serviceName
            $inspection = @(Invoke-JsonCommand "docker" @("image", "inspect", $service.image))[0]
            Assert-Condition ($null -ne $inspection.Config.Healthcheck) `
                "$serviceName image must define a health check"
            Assert-Condition ($inspection.Config.Healthcheck.Test[0] -eq "CMD") `
                "$serviceName health check must use exec form"
        }
    }

    Write-Host "Container hardening validation passed." -ForegroundColor Green
}
finally {
    Pop-Location
}
