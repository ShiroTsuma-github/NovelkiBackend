[CmdletBinding()]
param(
    [string]$OutputDir = "",
    [int]$LeastCoveredCount = 3,
    [int]$MinimumLines = 5,
    [string]$Configuration = "Debug",
    [switch]$NoRestore
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $repoRoot "artifacts/coverage"
}
elseif (-not [System.IO.Path]::IsPathRooted($OutputDir)) {
    $OutputDir = Join-Path $repoRoot $OutputDir
}

$outputDir = [System.IO.Path]::GetFullPath($OutputDir)
$artifactsRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot "artifacts"))
if (-not $outputDir.StartsWith($artifactsRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Coverage OutputDir must stay under $artifactsRoot."
}

$testResultsDir = Join-Path $outputDir "test-results"
$htmlDir = Join-Path $outputDir "html"
$leastCoveredPath = Join-Path $outputDir "least-covered-files.txt"
$summaryPath = Join-Path $outputDir "summary.txt"
$htmlIndexPath = Join-Path $htmlDir "index.html"
$baseOutputPath = Join-Path ([System.IO.Path]::GetTempPath()) "NovelkiBackendCoverageBin"
$runSettingsPath = Join-Path $repoRoot "coverage.runsettings"

if (Test-Path -LiteralPath $outputDir) {
    Remove-Item -LiteralPath $outputDir -Recurse -Force
}
New-Item -ItemType Directory -Path $testResultsDir, $htmlDir | Out-Null

Push-Location $repoRoot
try {
    if (-not $NoRestore) {
        dotnet tool restore
        if ($LASTEXITCODE -ne 0) {
            exit $LASTEXITCODE
        }
    }

    $projects = @(
        "Application.UnitTests/Application.UnitTests.csproj",
        "Infrastructure.IntegrationTests/Infrastructure.IntegrationTests.csproj"
    )

    foreach ($project in $projects) {
        $projectName = [System.IO.Path]::GetFileNameWithoutExtension($project)
        $projectResultsDir = Join-Path $testResultsDir $projectName
        New-Item -ItemType Directory -Path $projectResultsDir | Out-Null

        $testArgs = @(
            "test", $project,
            "--configuration", $Configuration,
            "--collect:XPlat Code Coverage",
            "--settings", $runSettingsPath,
            "--results-directory", $projectResultsDir,
            "-p:BaseOutputPath=$baseOutputPath/"
        )
        if ($NoRestore) {
            $testArgs += "--no-restore"
        }

        & dotnet @testArgs
        if ($LASTEXITCODE -ne 0) {
            exit $LASTEXITCODE
        }
    }

    $coverageFiles = @(Get-ChildItem -LiteralPath $testResultsDir -Recurse -Filter "coverage.cobertura.xml")
    if ($coverageFiles.Count -eq 0) {
        throw "No coverage.cobertura.xml files were generated under $testResultsDir."
    }

    $reportsArgument = ($coverageFiles | ForEach-Object { $_.FullName }) -join ";"
    dotnet tool run reportgenerator -- `
        "-reports:$reportsArgument" `
        "-targetdir:$htmlDir" `
        "-reporttypes:Html;TextSummary;Cobertura" `
        "-assemblyfilters:-Application.UnitTests;-Infrastructure.IntegrationTests"
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }

    $lineHitsByFile = @{}
    foreach ($coverageFile in $coverageFiles) {
        [xml]$coverage = Get-Content -LiteralPath $coverageFile.FullName
        foreach ($class in $coverage.coverage.packages.package.classes.class) {
            $fileName = [string]$class.filename
            if ([string]::IsNullOrWhiteSpace($fileName)) {
                continue
            }

            if (-not $lineHitsByFile.ContainsKey($fileName)) {
                $lineHitsByFile[$fileName] = @{}
            }

            foreach ($line in $class.lines.line) {
                $lineNumber = [int]$line.number
                $hits = [int]$line.hits
                if (-not $lineHitsByFile[$fileName].ContainsKey($lineNumber) -or $hits -gt $lineHitsByFile[$fileName][$lineNumber]) {
                    $lineHitsByFile[$fileName][$lineNumber] = $hits
                }
            }
        }
    }

    $fileStats = foreach ($fileName in $lineHitsByFile.Keys) {
        $lineHits = $lineHitsByFile[$fileName]
        $totalLines = $lineHits.Count
        if ($totalLines -lt $MinimumLines) {
            continue
        }

        $coveredLines = @($lineHits.Values | Where-Object { $_ -gt 0 }).Count
        [pscustomobject]@{
            File = $fileName
            CoveredLines = $coveredLines
            TotalLines = $totalLines
            UncoveredLines = $totalLines - $coveredLines
            LineRate = if ($totalLines -eq 0) { 0 } else { $coveredLines / $totalLines }
        }
    }

    $worstFiles = @($fileStats |
        Sort-Object @{ Expression = "LineRate"; Ascending = $true }, @{ Expression = "UncoveredLines"; Ascending = $false }, @{ Expression = "File"; Ascending = $true } |
        Select-Object -First $LeastCoveredCount)

    $leastCoveredLines = @(
        "Least-covered files"
        "Generated: $([DateTimeOffset]::Now.ToString("u"))"
        "Minimum instrumented lines: $MinimumLines"
        ""
    )
    foreach ($file in $worstFiles) {
        $leastCoveredLines += "{0,6:P2}  {1,4}/{2,-4} lines  uncovered={3,-4}  {4}" -f $file.LineRate, $file.CoveredLines, $file.TotalLines, $file.UncoveredLines, $file.File
    }
    Set-Content -LiteralPath $leastCoveredPath -Value $leastCoveredLines -Encoding UTF8

    $totalCovered = ($fileStats | Measure-Object -Property CoveredLines -Sum).Sum
    $totalLines = ($fileStats | Measure-Object -Property TotalLines -Sum).Sum
    $lineRate = if ($totalLines -eq 0) { 0 } else { $totalCovered / $totalLines }
    Set-Content -LiteralPath $summaryPath -Encoding UTF8 -Value @(
        "Coverage summary"
        "Line coverage: $($lineRate.ToString("P2")) ($totalCovered/$totalLines)"
        "HTML report: $htmlIndexPath"
        "Least-covered files: $leastCoveredPath"
    )

    Write-Host "Coverage HTML report: $htmlIndexPath"
    Write-Host "Least-covered files: $leastCoveredPath"
    Write-Host "Summary: $summaryPath"
}
finally {
    Pop-Location
}
