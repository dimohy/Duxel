<#
.SYNOPSIS
    FBA Perf 샘플의 FPS 벤치를 자동 실행합니다.
.DESCRIPTION
    - NativeAOT 실행(기본)과 Managed 실행을 반복 수행하여 avgFps를 수집합니다.
    - 샘플은 DUXEL_PERF_BENCH_SECONDS/DUXEL_PERF_BENCH_OUT/DUXEL_PERF_INITIAL_POLYGONS 환경변수를 통해 자동 종료 및 초기 부하를 설정합니다.
.PARAMETER SamplePaths
    벤치 대상 FBA 파일 경로 목록. 기본값은 기존 샘플 + 신규 스트레스 샘플입니다.
.PARAMETER SamplePath
    하위 호환용 단일 샘플 경로(지정 시 SamplePaths 대신 이 값만 실행).
.PARAMETER Iterations
    모드별 반복 횟수.
.PARAMETER BenchSeconds
    1회 실행당 벤치 측정 시간(초).
.PARAMETER InitialPolygons
    시작 시 자동 추가할 폴리곤 수.
.PARAMETER Configuration
    벤치 실행 빌드 구성 (Debug/Release).
.PARAMETER WarmupIterations
    모드별 측정 전 워밍업 실행 횟수(결과 미집계).
.PARAMETER RuntimeIdentifier
    NativeAOT 게시 RID.
.PARAMETER NativeOnly
    NativeAOT만 수행합니다.
.PARAMETER EstimateNoPresent
    vk-prof 로그가 있을 때 present 시간을 제외한 추정 FPS를 계산합니다.
.PARAMETER IncludeLayerCacheBench
    Idle/Layer 캐시 벤치 샘플을 기본 목록에 추가합니다.
.PARAMETER LayerBenchParticles
    레이어 캐시 벤치에서 사용할 파티클 수 목록(CSV).
.EXAMPLE
    ./scripts/run-fba-bench.ps1
.EXAMPLE
    ./scripts/run-fba-bench.ps1 -Iterations 5 -BenchSeconds 8
.EXAMPLE
    ./scripts/run-fba-bench.ps1 -Iterations 5 -BenchSeconds 3 -InitialPolygons 10000
.EXAMPLE
    ./scripts/run-fba-bench.ps1 -NativeOnly
.EXAMPLE
    $env:DUXEL_VK_PROFILE='1'; ./scripts/run-fba-bench.ps1 -EstimateNoPresent
#>
param(
    [string[]]$SamplePaths = @('samples/fba/Duxel_perf_test_fba.cs', 'samples/fba/ui_mixed_stress.cs'),
    [string]$SamplePath,
    [ValidateRange(1, 100)]
    [int]$Iterations = 3,
    [ValidateRange(1, 300)]
    [int]$BenchSeconds = 6,
    [ValidateRange(0, 1000000)]
    [int]$InitialPolygons = 0,
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [ValidateRange(0, 20)]
    [int]$WarmupIterations = 1,
    [string]$RuntimeIdentifier = 'win-x64',
    [switch]$NativeOnly,
    [switch]$EstimateNoPresent,
    [switch]$IncludeLayerCacheBench,
    [string]$LayerBenchParticles = '3000,9000,18000'
)

$ErrorActionPreference = 'Stop'

if ($PSBoundParameters.ContainsKey('SamplePath')) {
    $SamplePaths = @($SamplePath)
}

if ($IncludeLayerCacheBench -and -not ($SamplePaths -contains 'samples/fba/idle_layer_validation.cs')) {
    $SamplePaths += 'samples/fba/idle_layer_validation.cs'
}

if ($null -eq $SamplePaths -or $SamplePaths.Count -eq 0) {
    throw "벤치 대상 샘플이 비어 있습니다. -SamplePaths 또는 -SamplePath를 지정하세요."
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
Push-Location $repoRoot
try {
    $activeSamplePath = ''
    $artifactDir = ''

    function Get-NoPresentEstimate {
        param([string]$LogPath)

        if (-not (Test-Path $LogPath)) {
            return $null
        }

        $lines = Get-Content $LogPath -Encoding UTF8
        if ($null -eq $lines -or $lines.Count -eq 0) {
            return $null
        }

        $sumFps = 0.0
        $count = 0
        $regex = [regex]'upload=(?<upload>[0-9]+(?:\.[0-9]+)?)\s+record=(?<record>[0-9]+(?:\.[0-9]+)?)\([^\)]*\)\s+submit=(?<submit>[0-9]+(?:\.[0-9]+)?)\s+present=(?<present>[0-9]+(?:\.[0-9]+)?)'

        foreach ($line in $lines) {
            $m = $regex.Match($line)
            if (-not $m.Success) {
                continue
            }

            $uploadUs = [double]::Parse($m.Groups['upload'].Value, [System.Globalization.CultureInfo]::InvariantCulture)
            $recordUs = [double]::Parse($m.Groups['record'].Value, [System.Globalization.CultureInfo]::InvariantCulture)
            $submitUs = [double]::Parse($m.Groups['submit'].Value, [System.Globalization.CultureInfo]::InvariantCulture)
            $frameUsNoPresent = $uploadUs + $recordUs + $submitUs
            if ($frameUsNoPresent -le 0.0) {
                continue
            }

            $sumFps += 1000000.0 / $frameUsNoPresent
            $count++
        }

        if ($count -eq 0) {
            return $null
        }

        return $sumFps / $count
    }

    function Invoke-BenchRun {
        param(
            [string]$Mode,
            [int]$Index,
            [string]$ExecutablePath
        )

        $outJson = Join-Path $artifactDir ("bench-{0}-{1:00}.json" -f $Mode.ToLowerInvariant(), $Index)
        $outLog = Join-Path $artifactDir ("bench-{0}-{1:00}.log" -f $Mode.ToLowerInvariant(), $Index)
        if (Test-Path $outJson) {
            Remove-Item $outJson -Force -ErrorAction SilentlyContinue
        }
        if (Test-Path $outLog) {
            Remove-Item $outLog -Force -ErrorAction SilentlyContinue
        }

        $oldSeconds = $env:DUXEL_PERF_BENCH_SECONDS
        $oldOut = $env:DUXEL_PERF_BENCH_OUT
        $oldInitialPolygons = $env:DUXEL_PERF_INITIAL_POLYGONS
        $oldLayerOut = $env:DUXEL_LAYER_BENCH_OUT
        $oldLayerParticles = $env:DUXEL_LAYER_BENCH_PARTICLES
        $oldLayerPhaseSeconds = $env:DUXEL_LAYER_BENCH_PHASE_SECONDS
        try {
            $isLayerBenchSample = $activeSamplePath -like '*idle_layer_validation.cs'
            if ($isLayerBenchSample) {
                $env:DUXEL_LAYER_BENCH_OUT = $outJson
                $env:DUXEL_LAYER_BENCH_PARTICLES = $LayerBenchParticles
                $env:DUXEL_LAYER_BENCH_PHASE_SECONDS = $BenchSeconds.ToString([System.Globalization.CultureInfo]::InvariantCulture)
                Remove-Item Env:DUXEL_PERF_BENCH_SECONDS -ErrorAction SilentlyContinue
                Remove-Item Env:DUXEL_PERF_BENCH_OUT -ErrorAction SilentlyContinue
                Remove-Item Env:DUXEL_PERF_INITIAL_POLYGONS -ErrorAction SilentlyContinue
            }
            else {
                $env:DUXEL_PERF_BENCH_SECONDS = $BenchSeconds.ToString([System.Globalization.CultureInfo]::InvariantCulture)
                $env:DUXEL_PERF_BENCH_OUT = $outJson
                $env:DUXEL_PERF_INITIAL_POLYGONS = $InitialPolygons.ToString([System.Globalization.CultureInfo]::InvariantCulture)
                Remove-Item Env:DUXEL_LAYER_BENCH_OUT -ErrorAction SilentlyContinue
                Remove-Item Env:DUXEL_LAYER_BENCH_PARTICLES -ErrorAction SilentlyContinue
                Remove-Item Env:DUXEL_LAYER_BENCH_PHASE_SECONDS -ErrorAction SilentlyContinue
            }

            $sw = [System.Diagnostics.Stopwatch]::StartNew()
            if ($Mode -eq 'NativeAOT') {
                & $ExecutablePath *> $outLog
            }
            else {
                & ./run-fba.ps1 $activeSamplePath -Managed -Configuration $Configuration *> $outLog
            }
            $sw.Stop()

            if (-not (Test-Path $outJson)) {
                throw "벤치 출력 파일이 생성되지 않았습니다: $outJson"
            }

            $json = Get-Content $outJson -Raw -Encoding UTF8 | ConvertFrom-Json
            $isLayerResult = $null -ne $json.results
            $avgFps = 0.0
            $samples = 0
            $elapsedSeconds = 0.0
            $vsync = $false
            $count = 0
            if ($isLayerResult) {
                $phaseRows = @($json.results)
                if ($phaseRows.Count -gt 0) {
                    $avgFps = (($phaseRows | ForEach-Object { [double]$_.avgFps }) | Measure-Object -Average).Average
                    $samples = (($phaseRows | ForEach-Object { [int]$_.samples }) | Measure-Object -Sum).Sum
                    $elapsedSeconds = [double]$json.phaseSeconds * $phaseRows.Count

                    $phaseCsv = [System.IO.Path]::ChangeExtension($outJson, '.phases.csv')
                    $phaseRows | Export-Csv -Path $phaseCsv -NoTypeInformation -Encoding UTF8
                }
            }
            else {
                $avgFps = [double]$json.avgFps
                $samples = [int]$json.samples
                $elapsedSeconds = [double]$json.elapsedSeconds
                $vsync = [bool]$json.vsync
                $count = [int]$json.count
            }

            $avgFpsNoPresentEst = if ($EstimateNoPresent) { Get-NoPresentEstimate -LogPath $outLog } else { $null }
            return [pscustomobject]@{
                Mode = $Mode
                Iteration = $Index
                AvgFps = [double]$avgFps
                AvgFpsNoPresentEst = $avgFpsNoPresentEst
                Samples = [int]$samples
                ElapsedSeconds = [double]$elapsedSeconds
                VSync = [bool]$vsync
                PolygonCount = [int]$count
                IsLayerBench = [bool]$isLayerResult
                WallMs = [int]$sw.ElapsedMilliseconds
                OutputJson = $outJson
                OutputLog = $outLog
            }
        }
        finally {
            $env:DUXEL_PERF_BENCH_SECONDS = $oldSeconds
            $env:DUXEL_PERF_BENCH_OUT = $oldOut
            $env:DUXEL_PERF_INITIAL_POLYGONS = $oldInitialPolygons
            $env:DUXEL_LAYER_BENCH_OUT = $oldLayerOut
            $env:DUXEL_LAYER_BENCH_PARTICLES = $oldLayerParticles
            $env:DUXEL_LAYER_BENCH_PHASE_SECONDS = $oldLayerPhaseSeconds
        }
    }

    foreach ($sample in $SamplePaths) {
        $activeSamplePath = $sample
        $sampleAbs = Resolve-Path $activeSamplePath
        $sampleDir = Split-Path $sampleAbs -Parent
        $sampleBase = [System.IO.Path]::GetFileNameWithoutExtension($sampleAbs)
        $artifactDir = Join-Path $sampleDir ("artifacts/{0}" -f $sampleBase)
        New-Item -ItemType Directory -Path $artifactDir -Force | Out-Null

        Write-Host ""
        Write-Host ("=== Sample: {0} ===" -f $activeSamplePath) -ForegroundColor Green

        $results = New-Object System.Collections.Generic.List[object]

        Write-Host "[bench] NativeAOT 게시 준비..." -ForegroundColor Cyan
        & ./run-fba.ps1 $activeSamplePath -RuntimeIdentifier $RuntimeIdentifier -Configuration $Configuration

        $nativeExe = Get-ChildItem -Path $artifactDir -Filter '*.exe' -File -Recurse |
            Sort-Object LastWriteTime -Descending |
            Select-Object -First 1
        if ($null -eq $nativeExe) {
            throw "NativeAOT 실행 파일을 찾지 못했습니다: $artifactDir"
        }

        if ($WarmupIterations -gt 0) {
            for ($i = 1; $i -le $WarmupIterations; $i++) {
                Write-Host "[bench] NativeAOT warmup $i/$WarmupIterations" -ForegroundColor DarkCyan
                [void](Invoke-BenchRun -Mode 'NativeAOT' -Index (1000 + $i) -ExecutablePath $nativeExe.FullName)
            }
        }

        for ($i = 1; $i -le $Iterations; $i++) {
            Write-Host "[bench] NativeAOT run $i/$Iterations" -ForegroundColor Cyan
            $results.Add((Invoke-BenchRun -Mode 'NativeAOT' -Index $i -ExecutablePath $nativeExe.FullName))
        }

        if (-not $NativeOnly) {
            if ($WarmupIterations -gt 0) {
                for ($i = 1; $i -le $WarmupIterations; $i++) {
                    Write-Host "[bench] Managed warmup $i/$WarmupIterations" -ForegroundColor DarkCyan
                    [void](Invoke-BenchRun -Mode 'Managed' -Index (2000 + $i))
                }
            }

            for ($i = 1; $i -le $Iterations; $i++) {
                Write-Host "[bench] Managed run $i/$Iterations" -ForegroundColor Cyan
                $results.Add((Invoke-BenchRun -Mode 'Managed' -Index $i))
            }
        }

        $csvPath = Join-Path $artifactDir 'bench-summary.csv'
        $results | Export-Csv -Path $csvPath -NoTypeInformation -Encoding UTF8

        Write-Host "" 
        Write-Host "=== FBA Bench Summary ===" -ForegroundColor Green
        Write-Host ("설정: Sample={0}, Configuration={1}, BenchSeconds={2}, InitialPolygons={3}, WarmupIterations={4}, EstimateNoPresent={5}" -f $activeSamplePath, $Configuration, $BenchSeconds, $InitialPolygons, $WarmupIterations, $EstimateNoPresent.IsPresent) -ForegroundColor DarkCyan

        $hasNoPresentEstimate = $results | Where-Object { $null -ne $_.AvgFpsNoPresentEst } | Select-Object -First 1
        if ($hasNoPresentEstimate) {
            $results |
                Sort-Object Mode, Iteration |
                Format-Table Mode, Iteration, AvgFps, AvgFpsNoPresentEst, Samples, ElapsedSeconds, VSync, PolygonCount, WallMs -AutoSize
        }
        else {
            $results |
                Sort-Object Mode, Iteration |
                Format-Table Mode, Iteration, AvgFps, Samples, ElapsedSeconds, VSync, PolygonCount, WallMs -AutoSize
        }

        $grouped = $results | Group-Object Mode
        $avgByMode = @{}
        $avgNoPresentByMode = @{}
        foreach ($g in $grouped) {
            $values = @($g.Group | ForEach-Object { [double]$_.AvgFps })
            $avg = ($values | Measure-Object -Average).Average
            $best = ($values | Measure-Object -Maximum).Maximum
            $worst = ($values | Measure-Object -Minimum).Minimum
            $varianceSum = 0.0
            foreach ($v in $values) {
                $d = $v - $avg
                $varianceSum += ($d * $d)
            }
            $stddev = if ($values.Count -gt 0) { [Math]::Sqrt($varianceSum / $values.Count) } else { 0.0 }

            $avgByMode[$g.Name] = $avg
            Write-Host ("[{0}] 평균 FPS: {1:N2}" -f $g.Name, $avg) -ForegroundColor Yellow
            Write-Host ("[{0}] FPS best/avg/worst/stddev: {1:N2} / {2:N2} / {3:N2} / {4:N2}" -f $g.Name, $best, $avg, $worst, $stddev) -ForegroundColor DarkYellow

            if ($EstimateNoPresent) {
                $valid = $g.Group | Where-Object { $null -ne $_.AvgFpsNoPresentEst }
                if ($valid.Count -gt 0) {
                    $noPresentValues = @($valid | ForEach-Object { [double]$_.AvgFpsNoPresentEst })
                    $avgNoPresent = ($noPresentValues | Measure-Object -Average).Average
                    $bestNoPresent = ($noPresentValues | Measure-Object -Maximum).Maximum
                    $worstNoPresent = ($noPresentValues | Measure-Object -Minimum).Minimum
                    $varianceNoPresent = 0.0
                    foreach ($v in $noPresentValues) {
                        $d = $v - $avgNoPresent
                        $varianceNoPresent += ($d * $d)
                    }
                    $stddevNoPresent = if ($noPresentValues.Count -gt 0) { [Math]::Sqrt($varianceNoPresent / $noPresentValues.Count) } else { 0.0 }

                    $avgNoPresentByMode[$g.Name] = $avgNoPresent
                    Write-Host ("[{0}] present 제외 추정 FPS: {1:N2}" -f $g.Name, $avgNoPresent) -ForegroundColor DarkYellow
                    Write-Host ("[{0}] present 제외 best/avg/worst/stddev: {1:N2} / {2:N2} / {3:N2} / {4:N2}" -f $g.Name, $bestNoPresent, $avgNoPresent, $worstNoPresent, $stddevNoPresent) -ForegroundColor DarkCyan
                }
            }
        }

        if ($avgByMode.ContainsKey('Managed') -and $avgByMode.ContainsKey('NativeAOT') -and $avgByMode['Managed'] -gt 0) {
            $delta = (($avgByMode['NativeAOT'] - $avgByMode['Managed']) / $avgByMode['Managed']) * 100.0
            Write-Host ("[delta] NativeAOT vs Managed (표시 포함 FPS): {0:+0.00;-0.00;0.00}%" -f $delta) -ForegroundColor Cyan
        }

        if ($EstimateNoPresent -and $avgNoPresentByMode.ContainsKey('Managed') -and $avgNoPresentByMode.ContainsKey('NativeAOT') -and $avgNoPresentByMode['Managed'] -gt 0) {
            $deltaNoPresent = (($avgNoPresentByMode['NativeAOT'] - $avgNoPresentByMode['Managed']) / $avgNoPresentByMode['Managed']) * 100.0
            Write-Host ("[delta] NativeAOT vs Managed (present 제외 추정 FPS): {0:+0.00;-0.00;0.00}%" -f $deltaNoPresent) -ForegroundColor DarkCyan
        }

        Write-Host "[bench] CSV: $csvPath" -ForegroundColor Cyan
    }
}
finally {
    Pop-Location
}
