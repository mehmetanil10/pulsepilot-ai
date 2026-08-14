[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [switch]$SkipRestore,
    [switch]$SkipBuild,
    [switch]$SkipIntegrationTests,
    [switch]$SkipQualityGate
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$solutionPath = Join-Path $repositoryRoot "PulsePilot.sln"
$runSettingsPath = Join-Path $repositoryRoot "quality.runsettings"
$qualityGatesPath = Join-Path $repositoryRoot "quality-gates.json"
$artifactRoot = Join-Path $repositoryRoot "artifacts/quality"
$dotnetArtifactRoot = Join-Path $artifactRoot "dotnet"
$webRoot = Join-Path $repositoryRoot "src/PulsePilot.Web"
$webTestReportPath = Join-Path $artifactRoot "web-tests.json"
$webCoverageSummaryPath = Join-Path $artifactRoot "web-coverage/coverage-summary.json"
$baselineReportPath = Join-Path $artifactRoot "quality-baseline.json"

function Invoke-CheckedCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,
        [Parameter(Mandatory = $true)]
        [string[]]$ArgumentList
    )

    Write-Host "> $FilePath $($ArgumentList -join ' ')" -ForegroundColor DarkGray
    & $FilePath @ArgumentList | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code ${LASTEXITCODE}: $FilePath $($ArgumentList -join ' ')"
    }
}

function Reset-ArtifactDirectory {
    if (Test-Path -LiteralPath $artifactRoot) {
        Remove-Item -LiteralPath $artifactRoot -Recurse -Force
    }

    New-Item -ItemType Directory -Path $dotnetArtifactRoot -Force | Out-Null
}

function Get-TrxMetrics {
    param([Parameter(Mandatory = $true)][string]$Path)

    [xml]$document = Get-Content -LiteralPath $Path -Raw
    $namespaceManager = New-Object System.Xml.XmlNamespaceManager($document.NameTable)
    $namespaceManager.AddNamespace("trx", "http://microsoft.com/schemas/VisualStudio/TeamTest/2010")
    $counters = $document.SelectSingleNode("/trx:TestRun/trx:ResultSummary/trx:Counters", $namespaceManager)

    if ($null -eq $counters) {
        throw "Could not read test counters from $Path."
    }

    return [ordered]@{
        total = [int]$counters.GetAttribute("total")
        passed = [int]$counters.GetAttribute("passed")
        failed = [int]$counters.GetAttribute("failed")
        skipped = [int]$counters.GetAttribute("notExecuted")
    }
}

function Get-CoberturaMetrics {
    param([Parameter(Mandatory = $true)][string]$ResultsDirectory)

    $coverageFiles = @(Get-ChildItem -LiteralPath $ResultsDirectory -Recurse -Filter "coverage.cobertura.xml")
    if ($coverageFiles.Count -eq 0) {
        throw "No Cobertura report was created below $ResultsDirectory."
    }

    $coverageHashes = @($coverageFiles | Get-FileHash -Algorithm SHA256 | Select-Object -ExpandProperty Hash -Unique)
    if ($coverageHashes.Count -ne 1) {
        throw "Found multiple different Cobertura reports below $ResultsDirectory."
    }

    [xml]$document = Get-Content -LiteralPath $coverageFiles[0].FullName -Raw
    $coverage = $document.DocumentElement
    $culture = [System.Globalization.CultureInfo]::InvariantCulture

    return [ordered]@{
        lineCoverage = [math]::Round(
            [double]::Parse($coverage.GetAttribute("line-rate"), $culture) * 100,
            2)
        branchCoverage = [math]::Round(
            [double]::Parse($coverage.GetAttribute("branch-rate"), $culture) * 100,
            2)
        linesCovered = [int]$coverage.GetAttribute("lines-covered")
        linesValid = [int]$coverage.GetAttribute("lines-valid")
        branchesCovered = [int]$coverage.GetAttribute("branches-covered")
        branchesValid = [int]$coverage.GetAttribute("branches-valid")
    }
}

function Get-DotnetSuiteMetrics {
    param(
        [Parameter(Mandatory = $true)][string]$ProjectPath,
        [Parameter(Mandatory = $true)][string]$SuiteName
    )

    $resultsDirectory = Join-Path $dotnetArtifactRoot $SuiteName
    New-Item -ItemType Directory -Path $resultsDirectory -Force | Out-Null

    Invoke-CheckedCommand "dotnet" @(
        "test",
        $ProjectPath,
        "--configuration", $Configuration,
        "--no-build",
        "--no-restore",
        "--settings", $runSettingsPath,
        "--collect", "XPlat Code Coverage",
        "--logger", "trx;LogFileName=$SuiteName.trx",
        "--results-directory", $resultsDirectory
    )

    $tests = Get-TrxMetrics (Join-Path $resultsDirectory "$SuiteName.trx")
    $coverage = Get-CoberturaMetrics $resultsDirectory

    return [ordered]@{
        tests = $tests
        coverage = $coverage
    }
}

function Get-WebMetrics {
    $testReport = Get-Content -LiteralPath $webTestReportPath -Raw | ConvertFrom-Json
    $coverageReport = Get-Content -LiteralPath $webCoverageSummaryPath -Raw | ConvertFrom-Json
    $totalCoverage = $coverageReport.total

    return [ordered]@{
        tests = [ordered]@{
            total = [int]$testReport.numTotalTests
            passed = [int]$testReport.numPassedTests
            failed = [int]$testReport.numFailedTests
            skipped = [int]$testReport.numPendingTests
        }
        coverage = [ordered]@{
            lineCoverage = [double]$totalCoverage.lines.pct
            branchCoverage = [double]$totalCoverage.branches.pct
            functionCoverage = [double]$totalCoverage.functions.pct
            statementCoverage = [double]$totalCoverage.statements.pct
            linesCovered = [int]$totalCoverage.lines.covered
            linesTotal = [int]$totalCoverage.lines.total
            branchesCovered = [int]$totalCoverage.branches.covered
            branchesTotal = [int]$totalCoverage.branches.total
            functionsCovered = [int]$totalCoverage.functions.covered
            functionsTotal = [int]$totalCoverage.functions.total
            statementsCovered = [int]$totalCoverage.statements.covered
            statementsTotal = [int]$totalCoverage.statements.total
        }
    }
}

function Add-MinimumFailure {
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [System.Collections.Generic.List[string]]$Failures,
        [Parameter(Mandatory = $true)][string]$Label,
        [Parameter(Mandatory = $true)][double]$Actual,
        [Parameter(Mandatory = $true)][double]$Minimum
    )

    if ($Actual -lt $Minimum) {
        $Failures.Add("$Label is $Actual; required minimum is $Minimum.")
    }
}

Reset-ArtifactDirectory

if (-not $SkipRestore) {
    Invoke-CheckedCommand "dotnet" @("restore", $solutionPath)
    Invoke-CheckedCommand "npm" @("--prefix", $webRoot, "ci")
}

if (-not $SkipBuild) {
    $buildArguments = @(
        "build", $solutionPath,
        "--configuration", $Configuration,
        "--no-restore"
    )
    Invoke-CheckedCommand "dotnet" $buildArguments
}

$unitMetrics = Get-DotnetSuiteMetrics `
    (Join-Path $repositoryRoot "tests/PulsePilot.UnitTests/PulsePilot.UnitTests.csproj") `
    "unit"

$integrationMetrics = $null
if (-not $SkipIntegrationTests) {
    $integrationMetrics = Get-DotnetSuiteMetrics `
        (Join-Path $repositoryRoot "tests/PulsePilot.IntegrationTests/PulsePilot.IntegrationTests.csproj") `
        "integration"
}

Invoke-CheckedCommand "npm" @("--prefix", $webRoot, "run", "test:coverage")
$webMetrics = Get-WebMetrics

$gateFailures = New-Object "System.Collections.Generic.List[string]"
$gateStatus = "skipped"

if (-not $SkipQualityGate) {
    $gates = Get-Content -LiteralPath $qualityGatesPath -Raw | ConvertFrom-Json
    Add-MinimumFailure $gateFailures "Unit test count" $unitMetrics.tests.total $gates.dotnetUnit.minimumTests
    Add-MinimumFailure $gateFailures "Unit line coverage" $unitMetrics.coverage.lineCoverage $gates.dotnetUnit.minimumLineCoverage
    Add-MinimumFailure $gateFailures "Unit branch coverage" $unitMetrics.coverage.branchCoverage $gates.dotnetUnit.minimumBranchCoverage

    if ($null -ne $integrationMetrics) {
        Add-MinimumFailure $gateFailures "Integration test count" $integrationMetrics.tests.total $gates.dotnetIntegration.minimumTests
        Add-MinimumFailure $gateFailures "Integration line coverage" $integrationMetrics.coverage.lineCoverage $gates.dotnetIntegration.minimumLineCoverage
        Add-MinimumFailure $gateFailures "Integration branch coverage" $integrationMetrics.coverage.branchCoverage $gates.dotnetIntegration.minimumBranchCoverage
    }

    Add-MinimumFailure $gateFailures "Web test count" $webMetrics.tests.total $gates.web.minimumTests
    Add-MinimumFailure $gateFailures "Web line coverage" $webMetrics.coverage.lineCoverage $gates.web.minimumLineCoverage
    Add-MinimumFailure $gateFailures "Web branch coverage" $webMetrics.coverage.branchCoverage $gates.web.minimumBranchCoverage
    Add-MinimumFailure $gateFailures "Web function coverage" $webMetrics.coverage.functionCoverage $gates.web.minimumFunctionCoverage
    Add-MinimumFailure $gateFailures "Web statement coverage" $webMetrics.coverage.statementCoverage $gates.web.minimumStatementCoverage

    if ($gateFailures.Count -eq 0) {
        if ($SkipIntegrationTests) {
            $gateStatus = "partial"
        }
        else {
            $gateStatus = "passed"
        }
    }
    else {
        $gateStatus = "failed"
    }
}

$report = [ordered]@{
    schemaVersion = 1
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    configuration = $Configuration
    integrationTestsIncluded = -not $SkipIntegrationTests
    dotnetUnit = $unitMetrics
    dotnetIntegration = $integrationMetrics
    web = $webMetrics
    qualityGate = [ordered]@{
        status = $gateStatus
        failures = @($gateFailures)
    }
}

$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $baselineReportPath -Encoding utf8

Write-Host ""
Write-Host "PulsePilot quality baseline" -ForegroundColor Cyan
Write-Host "  .NET unit:        $($unitMetrics.tests.total) tests, $($unitMetrics.coverage.lineCoverage)% lines, $($unitMetrics.coverage.branchCoverage)% branches"
if ($null -ne $integrationMetrics) {
    Write-Host "  .NET integration: $($integrationMetrics.tests.total) tests, $($integrationMetrics.coverage.lineCoverage)% lines, $($integrationMetrics.coverage.branchCoverage)% branches"
}
else {
    Write-Host "  .NET integration: skipped"
}
Write-Host "  Web:              $($webMetrics.tests.total) tests, $($webMetrics.coverage.lineCoverage)% lines, $($webMetrics.coverage.branchCoverage)% branches"
Write-Host "  Quality gate:     $gateStatus"
Write-Host "  Report:           $baselineReportPath"

if ($gateFailures.Count -gt 0) {
    foreach ($failure in $gateFailures) {
        Write-Host "  - $failure" -ForegroundColor Red
    }
    throw "Quality gate failed with $($gateFailures.Count) regression(s)."
}
