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

    $mergedCoveragePath = Join-Path $htmlDir "Cobertura.xml"
    [xml]$coverage = Get-Content -LiteralPath $mergedCoveragePath
    $coverageByFile = @{}
    foreach ($class in $coverage.coverage.packages.package.classes.class) {
        $fileName = [string]$class.filename
        if ([string]::IsNullOrWhiteSpace($fileName)) {
            continue
        }

        if (-not $coverageByFile.ContainsKey($fileName)) {
            $coverageByFile[$fileName] = @{ Lines = @{}; Branches = @{} }
        }

        foreach ($line in $class.lines.line) {
            $lineNumber = [int]$line.number
            $hits = [int]$line.hits
            if (-not $coverageByFile[$fileName].Lines.ContainsKey($lineNumber) -or
                $hits -gt $coverageByFile[$fileName].Lines[$lineNumber]) {
                $coverageByFile[$fileName].Lines[$lineNumber] = $hits
            }

            if ([string]$line.branch -eq "true" -and
                [string]$line.'condition-coverage' -match '\((\d+)/(\d+)\)') {
                $coveredBranches = [int]$Matches[1]
                $totalBranches = [int]$Matches[2]
                if (-not $coverageByFile[$fileName].Branches.ContainsKey($lineNumber) -or
                    $coveredBranches -gt $coverageByFile[$fileName].Branches[$lineNumber].Covered) {
                    $coverageByFile[$fileName].Branches[$lineNumber] = @{
                        Covered = $coveredBranches
                        Total = $totalBranches
                    }
                }
            }
        }
    }

    $fileStats = foreach ($fileName in $coverageByFile.Keys) {
        $lineHits = $coverageByFile[$fileName].Lines
        $totalLines = $lineHits.Count
        if ($totalLines -lt $MinimumLines) {
            continue
        }

        $coveredLines = @($lineHits.Values | Where-Object { $_ -gt 0 }).Count
        $coveredBranches = ($coverageByFile[$fileName].Branches.Values |
            Measure-Object -Property Covered -Sum).Sum
        $totalBranches = ($coverageByFile[$fileName].Branches.Values |
            Measure-Object -Property Total -Sum).Sum
        [pscustomobject]@{
            File = $fileName
            CoveredLines = $coveredLines
            TotalLines = $totalLines
            UncoveredLines = $totalLines - $coveredLines
            LineRate = if ($totalLines -eq 0) { 0 } else { $coveredLines / $totalLines }
            CoveredBranches = if ($null -eq $coveredBranches) { 0 } else { $coveredBranches }
            TotalBranches = if ($null -eq $totalBranches) { 0 } else { $totalBranches }
            BranchRate = if ($null -eq $totalBranches -or $totalBranches -eq 0) { 1 } else { $coveredBranches / $totalBranches }
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
        $leastCoveredLines += "{0,6:P2} lines {1,4}/{2,-4}  {3,6:P2} branches {4,3}/{5,-3}  uncovered={6,-4}  {7}" -f `
            $file.LineRate, $file.CoveredLines, $file.TotalLines, $file.BranchRate,
            $file.CoveredBranches, $file.TotalBranches, $file.UncoveredLines, $file.File
    }
    Set-Content -LiteralPath $leastCoveredPath -Value $leastCoveredLines -Encoding UTF8

    $totalCovered = ($fileStats | Measure-Object -Property CoveredLines -Sum).Sum
    $totalLines = ($fileStats | Measure-Object -Property TotalLines -Sum).Sum
    $lineRate = if ($totalLines -eq 0) { 0 } else { $totalCovered / $totalLines }
    $totalCoveredBranches = ($fileStats | Measure-Object -Property CoveredBranches -Sum).Sum
    $totalBranches = ($fileStats | Measure-Object -Property TotalBranches -Sum).Sum
    $branchRate = if ($totalBranches -eq 0) { 1 } else { $totalCoveredBranches / $totalBranches }

    $gates = @(
        [pscustomobject]@{ Name = "Infrastructure.Services.PublicBookService"; Lines = 0.95; Branches = 0.85 },
        [pscustomobject]@{ Name = "Api.Controllers.PublicBookController"; Lines = 1.00; Branches = $null },
        [pscustomobject]@{ Name = "Application.Features.BookFeatures.Commands.DeleteBookHandler"; Lines = 0.90; Branches = 0.80 },
        [pscustomobject]@{ Name = "Infrastructure.Services.AdminLibraryService"; Lines = 0.95; Branches = 0.85 }
    )
    $gateLines = @("", "Public books coverage gates")
    $uncoveredLinesSection = @("", "Public books uncovered lines")
    $gateFailures = [System.Collections.Generic.List[string]]::new()
    foreach ($gate in $gates) {
        $class = @($coverage.coverage.packages.package.classes.class |
            Where-Object { [string]$_.name -eq $gate.Name }) | Select-Object -First 1
        if ($null -eq $class) {
            $message = "$($gate.Name): MISSING"
            $gateLines += $message
            $gateFailures.Add($message)
            continue
        }

        $classLines = @($class.lines.line)
        $classCoveredLines = @($classLines | Where-Object { [int]$_.hits -gt 0 }).Count
        $classLineRate = if ($classLines.Count -eq 0) { 0 } else { $classCoveredLines / $classLines.Count }
        $classCoveredBranches = 0
        $classTotalBranches = 0
        foreach ($line in $classLines) {
            if ([string]$line.branch -eq "true" -and
                [string]$line.'condition-coverage' -match '\((\d+)/(\d+)\)') {
                $classCoveredBranches += [int]$Matches[1]
                $classTotalBranches += [int]$Matches[2]
            }
        }
        $classBranchRate = if ($classTotalBranches -eq 0) { 1 } else { $classCoveredBranches / $classTotalBranches }
        $passes = $classLineRate -ge $gate.Lines -and
            ($null -eq $gate.Branches -or $classBranchRate -ge $gate.Branches)
        $status = if ($passes) { "PASS" } else { "FAIL" }
        $branchTarget = if ($null -eq $gate.Branches) { "n/a" } else { $gate.Branches.ToString("P0") }
        $message = "{0}: {1} lines={2:P2} (target {3:P0}) branches={4:P2} (target {5})" -f `
            $gate.Name, $status, $classLineRate, $gate.Lines, $classBranchRate, $branchTarget
        $gateLines += $message
        if (-not $passes) {
            $gateFailures.Add($message)
        }

        $uncovered = @($classLines | Where-Object { [int]$_.hits -eq 0 } |
            ForEach-Object { [int]$_.number } | Sort-Object -Unique)
        $uncoveredLinesSection += if ($uncovered.Count -eq 0) {
            "$($gate.Name): none"
        } else {
            "$($gate.Name): $($uncovered -join ', ')"
        }
    }

    $summaryLines = @(
        "Coverage summary"
        "Line coverage: $($lineRate.ToString("P2")) ($totalCovered/$totalLines)"
        "Branch coverage: $($branchRate.ToString("P2")) ($totalCoveredBranches/$totalBranches)"
        "HTML report: $htmlIndexPath"
        "Least-covered files: $leastCoveredPath"
    ) + $gateLines + $uncoveredLinesSection
    Set-Content -LiteralPath $summaryPath -Encoding UTF8 -Value $summaryLines

    Write-Host "Coverage HTML report: $htmlIndexPath"
    Write-Host "Least-covered files: $leastCoveredPath"
    Write-Host "Summary: $summaryPath"
    $gateLines | ForEach-Object { Write-Host $_ }
    if ($gateFailures.Count -gt 0) {
        throw "Coverage gate failed:`n$($gateFailures -join "`n")"
    }
}
finally {
    Pop-Location
}
