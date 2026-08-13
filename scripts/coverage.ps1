#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Collects .NET code coverage and reports CRAP (Change Risk Anti-Patterns) scores.

.DESCRIPTION
    This repo runs Microsoft.Testing.Platform (Directory.Build.props sets
    TestingPlatformDotnetTestSupport=true; the test projects are OutputType=Exe
    referencing xunit.v3 only). VSTest-based coverage
    (`dotnet test --collect:"XPlat Code Coverage" --settings coverage.runsettings`)
    therefore collects nothing, and coverlet.msbuild never fires because it hooks
    the VSTest MSBuild target. Microsoft.Testing.Extensions.CodeCoverage works
    under MTP but emits no cyclomatic complexity, so it cannot produce CRAP
    scores.

    So we drive coverlet.console against the xUnit v3 test executables directly.

.EXAMPLE
    pwsh scripts/coverage.ps1
    pwsh scripts/coverage.ps1 -SkipIntegration -OpenReport
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Debug',
    [switch]$SkipIntegration,
    [switch]$OpenReport,
    [switch]$NoBuild,
    [double]$CrapThreshold = 30
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$targetFramework = 'net10.0'

function Invoke-Checked {
    param([Parameter(Mandatory)][string]$What, [Parameter(Mandatory)][scriptblock]$Action)
    Write-Host "==> $What" -ForegroundColor Cyan
    & $Action
    if ($LASTEXITCODE -ne 0) {
        throw "$What failed with exit code $LASTEXITCODE"
    }
}

# Canonical CRAP formula:
#   CRAP(m) = comp(m)^2 * (1 - cov(m))^3 + comp(m)
# NOT the simplified comp * (1 - cov)^2 that circulates in some write-ups.
# We must compute this ourselves: coverlet's OpenCover writer emits
# cyclomaticComplexity/nPathComplexity/sequenceCoverage but NOT crapScore, so
# ReportGenerator's Risk Hotspots table shows complexity columns only.
function Get-CrapScore {
    param([double]$Complexity, [double]$Coverage)
    return [math]::Round(($Complexity * $Complexity) * [math]::Pow(1 - $Coverage, 3) + $Complexity, 2)
}

function Get-MethodMetrics {
    param([string[]]$OpenCoverFiles)

    $merged = @{}

    foreach ($file in $OpenCoverFiles) {
        [xml]$xml = Get-Content -LiteralPath $file -Raw

        foreach ($module in $xml.SelectNodes('//CoverageSession/Modules/Module')) {
            $moduleName = $module.SelectSingleNode('./ModuleName')
            if (-not $moduleName) { continue }
            $moduleName = $moduleName.InnerText

            # uid -> source path, so hotspots can be reported as file:line
            $paths = @{}
            foreach ($f in $module.SelectNodes('./Files/File')) {
                $paths[$f.GetAttribute('uid')] = $f.GetAttribute('fullPath')
            }

            foreach ($method in $module.SelectNodes('./Classes/Class/Methods/Method')) {
                # NOTE: $method.Name is the XmlElement's tag name ("Method"), not
                # the <Name> child. Must go through SelectSingleNode.
                $nameNode = $method.SelectSingleNode('./Name')
                if (-not $nameNode) { continue }
                $signature = $nameNode.InnerText

                $complexity = [double]$method.GetAttribute('cyclomaticComplexity')
                if ($complexity -le 0) { continue }

                $sequenceCoverage = 0.0
                $rawCoverage = $method.GetAttribute('sequenceCoverage')
                if ($rawCoverage) {
                    $sequenceCoverage = [double]::Parse($rawCoverage, [cultureinfo]::InvariantCulture) / 100.0
                }

                $fileRef = $method.SelectSingleNode('./FileRef')
                $sourceFile = ''
                if ($fileRef) {
                    $uid = $fileRef.GetAttribute('uid')
                    if ($paths.ContainsKey($uid)) { $sourceFile = $paths[$uid] }
                }

                $line = 0
                $firstPoint = $method.SelectSingleNode('./SequencePoints/SequencePoint')
                if ($firstPoint) { $line = [int]$firstPoint.GetAttribute('sl') }

                $key = "$moduleName|$signature"
                $existing = $merged[$key]

                # A method can appear in both test projects' reports. Keep the
                # best coverage seen, which is what merging the runs means.
                if ($existing -and $existing.Coverage -ge $sequenceCoverage) { continue }

                $merged[$key] = [pscustomobject]@{
                    Module     = $moduleName
                    Signature  = $signature
                    Complexity = $complexity
                    Coverage   = $sequenceCoverage
                    SourceFile = $sourceFile
                    Line       = $line
                }
            }
        }
    }

    return $merged.Values | ForEach-Object {
        $_ | Add-Member -NotePropertyName Crap -NotePropertyValue (Get-CrapScore -Complexity $_.Complexity -Coverage $_.Coverage) -PassThru
    }
}

function Format-Signature {
    param([string]$Signature)
    # OpenCover signatures look like:
    #   System.Threading.Tasks.Task`1<T> Ns.Type::Method(System.String)
    # Collapse to Type.Method so the table stays readable.
    if ($Signature -match '([^ :]+)::([^(]+)\(') {
        $type = ($Matches[1] -split '\.')[-1]
        $member = $Matches[2]

        # Async/iterator bodies live in a compiler-generated state machine:
        #   Ns.Owner/<RealMethod>d__6::MoveNext  ->  Owner.RealMethod (async)
        if ($type -match '^(.+?)/<(.+?)>d__\d+$') {
            return "$($Matches[1]).$($Matches[2]) (async)"
        }
        return "$type.$member"
    }
    return $Signature
}

Push-Location $repoRoot
try {
    $projects = @(
        [pscustomobject]@{ Name = 'BaggageDelivery.UnitTests' }
    )
    if (-not $SkipIntegration) {
        $projects += [pscustomobject]@{ Name = 'BaggageDelivery.IntegrationTests' }
    }

    foreach ($stale in @('TestResults', 'coverage')) {
        if (Test-Path $stale) { Remove-Item $stale -Recurse -Force }
    }

    Invoke-Checked 'dotnet tool restore' { dotnet tool restore }

    # Build the test projects, NOT BaggageDelivery.slnx: the solution includes
    # BaggageDelivery.PaxPortal.esproj, whose build runs the Vite/npm build
    # (slow, and it fails outright without node_modules). The test projects pull
    # in Api + Core transitively. Building separately also means
    # TreatWarningsAsErrors failures surface as build errors rather than as a
    # confusing coverlet failure.
    # -NoBuild measures whatever is already in bin/. Useful when the Api is running
    # under a debugger, which locks BaggageDelivery.Core.dll in the Api output dir
    # and makes the build fail with MSB3027 through no fault of the code.
    if ($NoBuild) {
        Write-Host '==> skipping build (-NoBuild); measuring existing binaries' -ForegroundColor DarkYellow
    }
    else {
        foreach ($project in $projects) {
            $csproj = "tests/$($project.Name)/$($project.Name).csproj"
            Invoke-Checked "build $($project.Name)" {
                dotnet build $csproj -c $Configuration --nologo -v minimal
            }
        }
    }

    foreach ($project in $projects) {
        $outDir = Join-Path $repoRoot "tests/$($project.Name)/bin/$Configuration/$targetFramework"
        $resultDir = Join-Path $repoRoot "TestResults/$($project.Name)"
        New-Item -ItemType Directory -Path $resultDir -Force | Out-Null

        $testDll = Join-Path $outDir "$($project.Name).dll"
        $testExe = Join-Path $outDir "$($project.Name).exe"
        if (-not (Test-Path $testExe)) { throw "Test executable not found: $testExe" }

        # --include scopes results to our assemblies so the NuGet dependencies
        # sitting in the same output directory are not instrumented.
        # --skipautoprops matters a lot here: BaggageDeliveryContext (944 lines)
        # and the Tuc* entity models are mostly auto-properties and would
        # otherwise dilute every number.
        Invoke-Checked "coverage $($project.Name)" {
            dotnet coverlet $testDll `
                --target $testExe `
                --format opencover --format cobertura `
                --output "$resultDir$([IO.Path]::DirectorySeparatorChar)" `
                --include '[BaggageDelivery.Core]*' `
                --include '[BaggageDelivery.Api]*' `
                --exclude-by-attribute Obsolete `
                --exclude-by-attribute GeneratedCodeAttribute `
                --exclude-by-attribute CompilerGeneratedAttribute `
                --exclude-by-attribute ExcludeFromCodeCoverageAttribute `
                --exclude-by-file '**/obj/**/*' `
                --skipautoprops
        }
    }

    $openCoverFiles = Get-ChildItem -Path 'TestResults' -Recurse -Filter 'coverage.opencover.xml' |
        Select-Object -ExpandProperty FullName
    if (-not $openCoverFiles) { throw 'No coverage.opencover.xml produced.' }

    # ReportGenerator merges multiple reports natively, which is why each project
    # writes its own pair rather than threading coverlet's --merge-with between
    # runs (that needs the json format in a fixed order and fails silently).
    Invoke-Checked 'reportgenerator' {
        dotnet reportgenerator `
            "-reports:$($openCoverFiles -join ';')" `
            '-targetdir:coverage' `
            '-reporttypes:Html;TextSummary;MarkdownSummaryGithub' `
            '-title:BaggageDelivery' `
            '-verbosity:Warning'
    }

    Write-Host ''
    Get-Content 'coverage/Summary.txt' | Select-Object -First 12

    $metrics = Get-MethodMetrics -OpenCoverFiles $openCoverFiles
    $hotspots = $metrics | Where-Object { $_.Crap -gt $CrapThreshold } | Sort-Object Crap -Descending

    Write-Host ''
    Write-Host "CRAP hotspots (score > $CrapThreshold)" -ForegroundColor Yellow
    Write-Host ('-' * 100)

    if (-not $hotspots) {
        Write-Host "None. Every method scores at or below $CrapThreshold." -ForegroundColor Green
    }
    else {
        $hotspots | ForEach-Object {
            [pscustomobject]@{
                Crap     = $_.Crap
                Cx       = [int]$_.Complexity
                Cov      = '{0,5:P0}' -f $_.Coverage
                Method   = Format-Signature -Signature $_.Signature
                Location = if ($_.SourceFile) {
                    "$(Resolve-Path -LiteralPath $_.SourceFile -Relative -ErrorAction SilentlyContinue):$($_.Line)"
                } else { '' }
            }
        } | Format-Table -AutoSize

        Write-Host "$($hotspots.Count) method(s) above the CRAP threshold of $CrapThreshold." -ForegroundColor Yellow
    }

    # coverage/ is gitignored, so this is scratch output for tooling/agents.
    $metrics | Sort-Object Crap -Descending |
        Select-Object Crap, Complexity, Coverage, Module, Signature, SourceFile, Line |
        Export-Csv -Path 'coverage/crap.csv' -NoTypeInformation

    Write-Host ''
    Write-Host 'Reports:' -ForegroundColor Cyan
    Write-Host '  coverage/index.html   (Risk Hotspots: complexity + NPath only, see note below)'
    Write-Host '  coverage/Summary.txt'
    Write-Host '  coverage/crap.csv     (all methods, ranked by CRAP)'
    Write-Host ''
    Write-Host 'Note: coverlet''s OpenCover output has no crapScore attribute, so' -ForegroundColor DarkGray
    Write-Host 'ReportGenerator cannot render a Crap Score column. The CRAP numbers' -ForegroundColor DarkGray
    Write-Host 'above and in crap.csv are computed by this script.' -ForegroundColor DarkGray

    if ($OpenReport) { Invoke-Item 'coverage/index.html' }
}
finally {
    Pop-Location
}
