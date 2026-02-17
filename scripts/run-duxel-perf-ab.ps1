param(
    [string]$SamplePath = "samples/fba/Duxel_perf_test_fba.cs",
    [int]$Runs = 5,
    [double]$BenchSeconds = 2.0,
    [int]$InitialPolygons = 2200,
    [string]$BaselineName = "baseline",
    [string]$CandidateName = "candidate",
    [string]$BaselineProfile = "display",
    [string]$CandidateProfile = "display",
    [switch]$BaselineGlobalStaticCache,
    [switch]$CandidateGlobalStaticCache,
    [switch]$BaselineTaa,
    [switch]$CandidateTaa,
    [switch]$BaselineFxaa,
    [switch]$CandidateFxaa,
    [int]$ManagedTimeoutSeconds = 180,
    [string]$OutDir = "artifacts"
)

$ErrorActionPreference = 'Stop'

if ($Runs -lt 1) {
    throw "Runs must be >= 1"
}

$repoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repoRoot
try {
    function Invoke-PerfRuns {
        param(
            [string]$Label,
            [string]$Profile,
            [bool]$EnableGlobalStaticCache,
            [bool]$EnableTaa,
            [bool]$EnableFxaa,
            [int]$RunCount
        )

        $prevBenchOut = $env:DUXEL_PERF_BENCH_OUT
        $prevBenchSec = $env:DUXEL_PERF_BENCH_SECONDS
        $prevInitPoly = $env:DUXEL_PERF_INITIAL_POLYGONS
        $prevProfile = $env:DUXEL_APP_PROFILE
        $prevGlobalStaticCache = $env:DUXEL_PERF_GLOBAL_STATIC_CACHE
        $prevTaa = $env:DUXEL_TAA
        $prevFxaa = $env:DUXEL_FXAA

        $rows = @()
        try {
            $env:DUXEL_PERF_BENCH_SECONDS = [string]::Format([System.Globalization.CultureInfo]::InvariantCulture, "{0:0.###}", $BenchSeconds)
            $env:DUXEL_PERF_INITIAL_POLYGONS = [string]$InitialPolygons
            $env:DUXEL_APP_PROFILE = $Profile
            $env:DUXEL_PERF_GLOBAL_STATIC_CACHE = if ($EnableGlobalStaticCache) { '1' } else { '0' }
            $env:DUXEL_TAA = if ($EnableTaa) { '1' } else { '0' }
            $env:DUXEL_FXAA = if ($EnableFxaa) { '1' } else { '0' }

            for ($i = 1; $i -le $RunCount; $i++) {
                $outPath = Join-Path $OutDir ("duxel-perf-{0}-run{1}.json" -f $Label, $i)
                $fullOutPath = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $outPath))
                $outParent = Split-Path -Parent $fullOutPath
                if (-not (Test-Path $outParent)) {
                    New-Item -ItemType Directory -Path $outParent | Out-Null
                }

                if (Test-Path $fullOutPath) {
                    Remove-Item $fullOutPath -Force
                }

                $env:DUXEL_PERF_BENCH_OUT = $fullOutPath

                Write-Host "[perf:$Label] run $i/$RunCount profile=$Profile globalStaticCache=$EnableGlobalStaticCache taa=$EnableTaa fxaa=$EnableFxaa" -ForegroundColor Cyan
                ./run-fba.ps1 $SamplePath -Managed -ManagedTimeoutSeconds $ManagedTimeoutSeconds -KillProcessTreeOnTimeout

                if (-not (Test-Path $fullOutPath)) {
                    throw "Missing output file: $fullOutPath"
                }

                $json = Get-Content $fullOutPath -Raw | ConvertFrom-Json
                $rows += [PSCustomObject]@{
                    label = $Label
                    run = $i
                    path = $outPath
                    avgFps = [double]$json.avgFps
                    samples = [int]$json.samples
                    elapsedSeconds = [double]$json.elapsedSeconds
                    count = [int]$json.count
                    msaa = [int]$json.msaa
                    globalStaticCache = [bool]$json.globalStaticCache
                    taa = [bool]$json.taa
                    fxaa = [bool]$json.fxaa
                }
            }
        }
        finally {
            if ($null -eq $prevBenchOut) { Remove-Item Env:DUXEL_PERF_BENCH_OUT -ErrorAction SilentlyContinue } else { $env:DUXEL_PERF_BENCH_OUT = $prevBenchOut }
            if ($null -eq $prevBenchSec) { Remove-Item Env:DUXEL_PERF_BENCH_SECONDS -ErrorAction SilentlyContinue } else { $env:DUXEL_PERF_BENCH_SECONDS = $prevBenchSec }
            if ($null -eq $prevInitPoly) { Remove-Item Env:DUXEL_PERF_INITIAL_POLYGONS -ErrorAction SilentlyContinue } else { $env:DUXEL_PERF_INITIAL_POLYGONS = $prevInitPoly }
            if ($null -eq $prevProfile) { Remove-Item Env:DUXEL_APP_PROFILE -ErrorAction SilentlyContinue } else { $env:DUXEL_APP_PROFILE = $prevProfile }
            if ($null -eq $prevGlobalStaticCache) { Remove-Item Env:DUXEL_PERF_GLOBAL_STATIC_CACHE -ErrorAction SilentlyContinue } else { $env:DUXEL_PERF_GLOBAL_STATIC_CACHE = $prevGlobalStaticCache }
            if ($null -eq $prevTaa) { Remove-Item Env:DUXEL_TAA -ErrorAction SilentlyContinue } else { $env:DUXEL_TAA = $prevTaa }
            if ($null -eq $prevFxaa) { Remove-Item Env:DUXEL_FXAA -ErrorAction SilentlyContinue } else { $env:DUXEL_FXAA = $prevFxaa }
        }

        return $rows
    }

    function Get-StatSummary {
        param([object[]]$Rows)

        $avg = ($Rows | Measure-Object avgFps -Average).Average
        $min = ($Rows | Measure-Object avgFps -Minimum).Minimum
        $max = ($Rows | Measure-Object avgFps -Maximum).Maximum
        $variance = (($Rows | ForEach-Object { [Math]::Pow($_.avgFps - $avg, 2) } | Measure-Object -Sum).Sum / [Math]::Max(1, $Rows.Count))
        $std = [Math]::Sqrt($variance)

        return [PSCustomObject]@{
            avgFps = [Math]::Round($avg, 3)
            minFps = [Math]::Round($min, 3)
            maxFps = [Math]::Round($max, 3)
            stdFps = [Math]::Round($std, 3)
            runs = $Rows.Count
        }
    }

    $baselineRows = Invoke-PerfRuns -Label $BaselineName -Profile $BaselineProfile -EnableGlobalStaticCache:$BaselineGlobalStaticCache -EnableTaa:$BaselineTaa -EnableFxaa:$BaselineFxaa -RunCount $Runs
    $candidateRows = Invoke-PerfRuns -Label $CandidateName -Profile $CandidateProfile -EnableGlobalStaticCache:$CandidateGlobalStaticCache -EnableTaa:$CandidateTaa -EnableFxaa:$CandidateFxaa -RunCount $Runs

    $baselineStat = Get-StatSummary -Rows $baselineRows
    $candidateStat = Get-StatSummary -Rows $candidateRows

    $pct = if ($baselineStat.avgFps -ne 0) {
        (($candidateStat.avgFps - $baselineStat.avgFps) / $baselineStat.avgFps) * 100
    }
    else {
        0
    }

    Write-Host "`n[perf-ab] per-run" -ForegroundColor Green
    ($baselineRows + $candidateRows) |
        Select-Object label,run,avgFps,samples,elapsedSeconds,count,msaa,globalStaticCache,taa,fxaa,path |
        Sort-Object label,run |
        Format-Table -AutoSize

    Write-Host "`n[perf-ab] summary" -ForegroundColor Green
    [PSCustomObject]@{
        baseline = $BaselineName
        baselineAvg = $baselineStat.avgFps
        baselineStd = $baselineStat.stdFps
        candidate = $CandidateName
        candidateAvg = $candidateStat.avgFps
        candidateStd = $candidateStat.stdFps
        improvementPct = [Math]::Round($pct, 3)
    } | Format-Table -AutoSize

    $summaryPath = Join-Path $OutDir "duxel-perf-ab-summary.json"
    $summary = [PSCustomObject]@{
        samplePath = $SamplePath
        runs = $Runs
        benchSeconds = $BenchSeconds
        initialPolygons = $InitialPolygons
        baseline = [PSCustomObject]@{
            name = $BaselineName
            profile = $BaselineProfile
            globalStaticCache = [bool]$BaselineGlobalStaticCache
            taa = [bool]$BaselineTaa
            fxaa = [bool]$BaselineFxaa
            stats = $baselineStat
            records = $baselineRows
        }
        candidate = [PSCustomObject]@{
            name = $CandidateName
            profile = $CandidateProfile
            globalStaticCache = [bool]$CandidateGlobalStaticCache
            taa = [bool]$CandidateTaa
            fxaa = [bool]$CandidateFxaa
            stats = $candidateStat
            records = $candidateRows
        }
        improvementPct = [Math]::Round($pct, 3)
    }

    $summary | ConvertTo-Json -Depth 6 | Set-Content -Path $summaryPath -Encoding UTF8
    Write-Host "[perf-ab] summary file: $summaryPath" -ForegroundColor Yellow
}
finally {
    Pop-Location
}
