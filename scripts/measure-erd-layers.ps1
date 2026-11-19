param(
    [ValidateSet('net10.0-windows7.0', 'net11.0-windows7.0')]
    [string]$TargetFramework = 'net11.0-windows7.0',
    [ValidateRange(1, 10)][int]$Runs = 3,
    [ValidateRange(4, 60)][int]$Seconds = 6,
    [ValidateRange(12, 5000)][int[]]$TableCounts = @(120, 1200),
    [ValidateSet('dense', 'explore')][string[]]$Views = @('dense', 'explore'),
    [ValidateNotNullOrEmpty()][ValidateSet('camera', 'edit', 'idle', 'motion-tour')][string[]]$Scenarios = @('camera', 'edit', 'idle'),
    [ValidateNotNullOrEmpty()][ValidateSet('full', 'resident')][string[]]$Modes = @('full', 'resident'),
    [string]$OutputDirectory,
    [string]$DotnetPath = 'dotnet',
    [string]$Executable,
    [string]$BaselineExecutable,
    [switch]$CompareCompiledLayers,
    [switch]$CompareSharedLayerBasis,
    [switch]$CompareSharedLayerCamera,
    [switch]$CompareDescriptorReuse,
    [switch]$CompareCommandSpans,
    [switch]$CompareLayerBoundsReuse,
    [switch]$CompareDirectLayerReplay,
    [switch]$CompareRenderPipeline,
    [switch]$ComparePlatformEventLoop,
    [switch]$CompareCpuTimingSampling,
    [switch]$CompareCompiledIndexCapacity,
    [switch]$CompareAxisAlignedCulling,
    [switch]$ControlledWorkloadClock,
    [switch]$RequireExclusive,
    [switch]$SummarizeOnly
)
$ErrorActionPreference = 'Stop'

function Get-ErDMedian([double[]]$Values) {
    if ($Values.Length -eq 0) { return $null }
    [Array]::Sort($Values)
    $middle = [int][Math]::Floor($Values.Length / 2)
    if ($Values.Length % 2 -eq 1) { return $Values[$middle] }
    return ($Values[$middle - 1] + $Values[$middle]) / 2
}

function Test-ErDFrameStatistics([string]$JsonPath, [object]$Result) {
    if ($Result.scenario -eq 'idle') { return }
    $reader = [IO.StreamReader]::new($JsonPath + '.frames.csv')
    $frames = [Collections.Generic.List[double]]::new()
    $sum = 0.0
    try {
        if ($reader.ReadLine() -ne 'frameSeconds') { throw 'ERD_RAW_FRAME_HEADER' }
        while ($null -ne ($line = $reader.ReadLine())) {
            $value = 0.0
            if (-not [double]::TryParse($line, [Globalization.NumberStyles]::Float, [cultureinfo]::InvariantCulture, [ref]$value) -or
                -not [double]::IsFinite($value) -or $value -le 0) { throw 'ERD_RAW_FRAME_VALUE' }
            $frames.Add($value)
            $sum += $value
        }
    } finally { $reader.Dispose() }
    if ($frames.Count -eq 0 -or $frames.Count -ne $Result.performance.samples) { throw 'ERD_RAW_FRAME_COUNT' }
    $sorted = $frames.ToArray()
    [Array]::Sort($sorted)
    $expected = @{ averageFps = $sorted.Length / $sum }
    foreach ($pair in @(@('medianMs', .5), @('p95Ms', .95), @('p99Ms', .99))) {
        $position = ($sorted.Length - 1) * $pair[1]
        $lower = [int][Math]::Floor($position)
        $upper = [Math]::Min($lower + 1, $sorted.Length - 1)
        $expected[$pair[0]] = ($sorted[$lower] + ($sorted[$upper] - $sorted[$lower]) * ($position - $lower)) * 1000
    }
    $slowCount = [Math]::Max(1, [int][Math]::Ceiling($sorted.Length * .01))
    $slowSum = 0.0
    for ($i = $sorted.Length - $slowCount; $i -lt $sorted.Length; $i++) { $slowSum += $sorted[$i] }
    $expected.low1PctFps = $slowCount / $slowSum
    foreach ($name in $expected.Keys) {
        $actual = [double]$Result.performance.$name
        if (-not [double]::IsFinite($actual) -or
            [Math]::Abs($actual - $expected[$name]) -gt [Math]::Max(1e-7, [Math]::Abs($expected[$name]) * 1e-8)) {
            throw "ERD_RAW_FRAME_STAT_MISMATCH: $name"
        }
    }
}

function Test-ErDPresentationLinks([string]$JsonPath, [long]$ExpectedSamples) {
    $reader = [IO.StreamReader]::new($JsonPath + '.presentations.csv')
    try {
        if ($reader.ReadLine() -ne 'presentationSequence,drawDataFrameId') { throw 'ERD_PRESENTATION_LINK_HEADER' }
        $count = 0L; $previousSequence = 0UL; $previousSource = 0UL
        while ($null -ne ($line = $reader.ReadLine())) {
            $parts = $line.Split(','); $sequence = 0UL; $source = 0UL
            if ($parts.Length -ne 2 -or -not [ulong]::TryParse($parts[0], [ref]$sequence) -or -not [ulong]::TryParse($parts[1], [ref]$source) -or
                $sequence -eq 0 -or $source -le $previousSource -or ($count -gt 0 -and $sequence -ne $previousSequence + 1)) {
                throw 'ERD_PRESENTATION_LINK_SEQUENCE'
            }
            $previousSequence = $sequence; $previousSource = $source; $count++
        }
        if ($count -ne $ExpectedSamples) { throw 'ERD_PRESENTATION_LINK_COUNT' }
        return $count
    }
    finally { $reader.Dispose() }
}

function Test-ErDPresentationWarmup([object]$Result) {
    if ($Result.fpsWarmupMethod -notin @('first-accepted-presentation-v1','first-accepted-presentation-source-epoch-v2')) { throw 'ERD_WARMUP_METHOD_REQUIRED' }
    if ($Result.warmupPresentationStartTicks -le 0 -or $Result.stopwatchFrequency -le 0 -or
        ([long]$Result.warmupPresentationDeadlineTicks - [long]$Result.warmupPresentationStartTicks) -ne [long]$Result.stopwatchFrequency) {
        throw 'ERD_PRESENTATION_WARMUP_DURATION'
    }
    if ($Result.scenario -ne 'idle' -and ($Result.firstMeasuredPresentationTimestampTicks -lt $Result.warmupPresentationDeadlineTicks -or
        $Result.framesAfterWarmup -ne $Result.performance.samples)) { throw 'ERD_PRESENTATION_WARMUP_SAMPLES' }
}

function Test-ErDProcessMetrics([object]$Result) {
    foreach ($name in @('processCpuMilliseconds','processWallMilliseconds','averageBusyCpuCores','processPeakWorkingSetBytes')) {
        $value = [double]$Result.$name
        if ($value -le 0 -or [double]::IsNaN($value) -or [double]::IsInfinity($value)) { throw 'PROCESS_METRICS_REQUIRED' }
    }
    if ([Math]::Abs($Result.averageBusyCpuCores - $Result.processCpuMilliseconds / $Result.processWallMilliseconds) -gt 0.000001) { throw 'PROCESS_CPU_INTERVAL_MISMATCH' }
}

function Test-ErDCpuTiming([object]$Result, [bool]$Sampled) {
    if ($null -eq $Result.cpuTimingCollectedFrames -or $Result.gpuTimingSamples -le 0 -or
        -not $Result.renderPipeline -or -not $Result.eventDrivenPlatformLoop -or
        $Result.renderedFrames -ge 1000000 -or $Result.performance.drawCalls -le 0) { throw 'CPU_TIMING_COMPARISON_CONTRACT' }
    $expected=if ($Sampled) {0} else {$Result.performance.samples}
    if ($Result.cpuTimingCollectedFrames -ne $expected) { throw 'CPU_TIMING_MODE_MISMATCH' }
}

function Test-ErDCompiledIndexCapacity([object]$Result, [int]$Capacity) {
    if ($Result.compiledIndexLimit -ne $Capacity -or $Result.compiledIndexCachePeakBytes -le 0 -or
        $Result.performance.compiledLayerDraws -le 0) { throw 'COMPILED_INDEX_CAPACITY_MISMATCH' }
    Test-ErDCpuTiming $Result $true
    Test-ErDProcessMetrics $Result
}

function Test-ErDAxisAlignedCulling([object]$Result, [bool]$Enabled) {
    Test-ErDCompiledIndexCapacity $Result 2097152
    if ($null -eq $Result.axisAlignedLayerCulling -or $Result.axisAlignedLayerCulling -ne $Enabled) {
        throw 'AXIS_CULLING_MODE_MISMATCH'
    }
    Test-ErDPresentationWarmup $Result
}

function Test-ErDControlledWorkload([object]$Result, [string]$JsonPath, [double]$Seconds) {
    if ($Result.workloadTimingBasis -ne 'post-warmup-source-v1' -or
        $Result.fpsWarmupMethod -ne 'first-accepted-presentation-source-epoch-v2' -or
        $Result.workloadDurationSeconds -ne $Seconds -or $Result.workloadStartTicks -lt $Result.warmupPresentationDeadlineTicks -or
        ([long]$Result.workloadDeadlineTicks - [long]$Result.workloadStartTicks) -ne [long]($Seconds * $Result.stopwatchFrequency)) {
        throw 'ERD_CONTROLLED_WORKLOAD_EPOCH'
    }
    Test-ErDPresentationWarmup $Result
    if ($Result.firstMeasuredWorkloadTimestampTicks -lt $Result.workloadStartTicks -or
        $Result.lastMeasuredWorkloadTimestampTicks -ge $Result.workloadDeadlineTicks -or
        $Result.firstMeasuredPresentationTimestampTicks -lt $Result.firstMeasuredWorkloadTimestampTicks -or
        $Result.lastMeasuredPresentationTimestampTicks -lt $Result.lastMeasuredWorkloadTimestampTicks) {
        throw 'ERD_CONTROLLED_WORKLOAD_SOURCE_TIME'
    }
    $workloadReader=[IO.StreamReader]::new($JsonPath + '.workload.csv')
    $presentationReader=$null
    try {
        $presentationReader=[IO.StreamReader]::new($JsonPath + '.presentations.csv')
        if ($workloadReader.ReadLine() -ne 'drawDataFrameId,workloadTimestampTicks' -or
            $presentationReader.ReadLine() -ne 'presentationSequence,drawDataFrameId') { throw 'ERD_WORKLOAD_TRACE_HEADER' }
        $count=0L; $previousTicks=0L; $firstTicks=0L
        while ($null -ne ($line=$workloadReader.ReadLine())) {
            $source=0UL; $ticks=0L; $parts=$line.Split(','); $presentation=$presentationReader.ReadLine()
            if ($parts.Count -ne 2 -or -not [ulong]::TryParse($parts[0],[ref]$source) -or
                -not [long]::TryParse($parts[1],[ref]$ticks) -or $null -eq $presentation -or
                $source -ne [ulong]$presentation.Split(',')[1] -or $ticks -lt $Result.workloadStartTicks -or
                $ticks -ge $Result.workloadDeadlineTicks -or ($count -gt 0 -and $ticks -lt $previousTicks)) {
                throw 'ERD_WORKLOAD_TRACE_SOURCE'
            }
            if ($count -eq 0) { $firstTicks=$ticks }
            $previousTicks=$ticks; $count++
        }
        if ($count -ne $Result.performance.samples -or $null -ne $presentationReader.ReadLine() -or
            $firstTicks -ne $Result.firstMeasuredWorkloadTimestampTicks -or
            $previousTicks -ne $Result.lastMeasuredWorkloadTimestampTicks) { throw 'ERD_WORKLOAD_TRACE_COUNT' }
    }
    finally { $workloadReader.Dispose(); if ($null -ne $presentationReader) { $presentationReader.Dispose() } }
    $sampleSeconds=$Result.performance.samples / $Result.performance.averageFps
    if ([Math]::Abs($sampleSeconds - $Seconds) -gt .25) { throw 'ERD_WORKLOAD_SAMPLE_DURATION' }
}

function Write-ErDComparisonSummary([object[]]$RunResults, [string]$Directory) {
    $rows = [Collections.Generic.List[object]]::new()
    $fpsTarget = 1000.0
    $fpsTargets = [Collections.Generic.List[object]]::new()
    $groups = $RunResults | Group-Object -Property { "$($_.result.tables)|$($_.result.view)|$($_.scenario)|$($_.mode)" }
    foreach ($group in $groups) {
        $first = $group.Group[0]
        $timingBases = @($group.Group | ForEach-Object {
            if ($_.result.fpsTimingBasis) { $_.result.fpsTimingBasis } else { 'legacy-ui-loop' }
        } | Sort-Object -Unique)
        if ($timingBases.Count -ne 1) { throw 'MIXED_FPS_TIMING_BASIS: compare runs with the same FPS clock.' }
        $samplingMethods = @($group.Group | ForEach-Object {
            if ($_.result.fpsSamplingMethod) { $_.result.fpsSamplingMethod } else { 'legacy-latest-snapshot' }
        } | Sort-Object -Unique)
        $warmupMethods = @($group.Group | ForEach-Object {
            if ($_.result.fpsWarmupMethod) { $_.result.fpsWarmupMethod } else { 'unspecified' }
        } | Sort-Object -Unique)
        $rootMetrics = @('compiledIndexCachePeakBytes','processCpuMilliseconds','processWallMilliseconds','averageBusyCpuCores','processPeakWorkingSetBytes')
        $metrics = if ($first.scenario -eq 'idle') { @('framesAfterWarmup') + $rootMetrics } else {
            @('averageFps','medianMs','p95Ms','p99Ms','low1PctFps','sceneAllocatedBytesPerFrame','rendererAllocatedBytesPerFrame','frameUploadBytes','layerStateUploadBytes','compiledIndexUploadBytes','compiledLayerDraws','sharedBasisLayerDraws','sharedCameraLayerDraws','compiledLayerCommands','compiledDescriptorBuilds','compiledCommandVisits','layerBoundsCommandVisits','drawCalls','gpuMicroseconds') + $rootMetrics
        }
        foreach ($metric in $metrics) {
            $values = @{}
            foreach ($variant in @('baseline','current')) {
                $values[$variant] = [double[]]@($group.Group | Where-Object variant -eq $variant | ForEach-Object {
                    $value = if ($first.scenario -eq 'idle' -or $metric -in $rootMetrics) { $_.result.$metric } else { $_.result.performance.$metric }
                    if ($null -ne $value -and -not [double]::IsNaN([double]$value) -and -not [double]::IsInfinity([double]$value)) { [double]$value }
                })
            }
            $before = $values.baseline; $after = $values.current
            $beforeMedian = Get-ErDMedian $before; $afterMedian = Get-ErDMedian $after
            $beforeRange = $before | Measure-Object -Minimum -Maximum
            $afterRange = $after | Measure-Object -Minimum -Maximum
            if ($metric -eq 'averageFps') {
                $fpsTargets.Add([pscustomobject]@{
                    tables=$first.result.tables;view=$first.result.view;scenario=$first.scenario;mode=$first.mode
                    targetFps=$fpsTarget;currentMedianFps=$afterMedian;currentRuns=$after.Length
                    timingBasis=$timingBases[0]
                    samplingMethod=$samplingMethods -join ','
                    warmupMethod=$warmupMethods -join ','
                    currentMinFps=$afterRange.Minimum;currentMaxFps=$afterRange.Maximum
                    targetAttainmentPercent=if ($null -ne $afterMedian) {[Math]::Round($afterMedian / $fpsTarget * 100, 2)} else {$null}
                    targetStatus=if ($after.Length -lt 3) {'INSUFFICIENT_REPETITIONS'} elseif ($afterRange.Minimum -ge $fpsTarget) {'MET_ACROSS_RUNS'} else {'NOT_MET'}
                })
            }
            $status = 'INSUFFICIENT_REPETITIONS'; $change = $null
            if ($before.Length -ge 3 -and $after.Length -ge 3) {
                if ($beforeRange.Minimum -eq $beforeRange.Maximum -and $afterRange.Minimum -eq $afterRange.Maximum -and $beforeMedian -eq $afterMedian) {
                    $status = 'UNCHANGED'
                    if ($beforeMedian -ne 0) { $change = 0 }
                }
                elseif ($beforeRange.Minimum -le $afterRange.Maximum -and $afterRange.Minimum -le $beforeRange.Maximum) { $status = 'INVALID: overlapping run ranges' }
                elseif ($beforeMedian -eq 0) { $status = 'ZERO_BASELINE' }
                else { $status = 'disjoint-run-ranges'; $change = ($afterMedian / $beforeMedian - 1) * 100 }
            }
            if ($metric -eq 'frameUploadBytes' -and @($group.Group | Where-Object { $null -ne $_.compiledLayers } | Select-Object -ExpandProperty compiledLayers -Unique).Count -gt 1) {
                $status = 'NONCOMPARABLE: direct push constants are not counted as upload bytes'
                $change = $null
            }
            if ($samplingMethods.Count -ne 1) {
                $status = 'NONCOMPARABLE: different presentation sampling methods'
                $change = $null
            }
            if ($warmupMethods.Count -ne 1) {
                $status = 'NONCOMPARABLE: different presentation warmup methods'
                $change = $null
            }
            $rows.Add([pscustomobject]@{tables=$first.result.tables;view=$first.result.view;scenario=$first.scenario;mode=$first.mode;metric=$metric;
                baselineRuns=$before.Length;currentRuns=$after.Length;beforeMedian=$beforeMedian;afterMedian=$afterMedian;changePercent=$change;
                beforeMin=$beforeRange.Minimum;beforeMax=$beforeRange.Maximum;afterMin=$afterRange.Minimum;afterMax=$afterRange.Maximum;quantitativeStatus=$status})
        }
    }
    [pscustomobject]@{method='Median of independent runs; overlapping ranges invalidate percentage claims';minimumRepetitions=3;fpsTarget=$fpsTarget;fpsTargets=$fpsTargets.ToArray();rows=$rows.ToArray()} |
        ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $Directory 'summary.json') -Encoding utf8
    $targetReport = [Collections.Generic.List[string]]::new()
    $targetReport.Add('Target: 1000 FPS. Current values are medians of independent run averages, not instantaneous peaks.')
    $targetReport.Add('')
    $targetReport.Add('| Tables | View | Scenario | Mode | Timing basis | Sampling method | Warmup method | Current FPS | Target FPS | Attainment | Runs | Status |')
    $targetReport.Add('| ---: | --- | --- | --- | --- | --- | --- | ---: | ---: | ---: | ---: | --- |')
    foreach ($target in $fpsTargets) {
        $currentText = if ($null -ne $target.currentMedianFps) { $target.currentMedianFps.ToString('F2', [Globalization.CultureInfo]::InvariantCulture) } else { '?' }
        $attainmentText = if ($null -ne $target.targetAttainmentPercent) { $target.targetAttainmentPercent.ToString('F2', [Globalization.CultureInfo]::InvariantCulture) + '%' } else { '?' }
        $targetReport.Add("| $($target.tables) | $($target.view) | $($target.scenario) | $($target.mode) | $($target.timingBasis) | $($target.samplingMethod) | $($target.warmupMethod) | $currentText | 1000 | $attainmentText | $($target.currentRuns) | $($target.targetStatus) |")
        Write-Host "[ERD FPS] $($target.tables) tables / $($target.scenario): $currentText / 1000 FPS ($attainmentText), $($target.currentRuns) runs, $($target.targetStatus)"
    }
    $targetReport | Set-Content -LiteralPath (Join-Path $Directory 'fps-target.md') -Encoding utf8
}

if ($SummarizeOnly) {
    if (-not $OutputDirectory) { throw '-SummarizeOnly requires -OutputDirectory.' }
    $existingResults = Get-Content -LiteralPath (Join-Path $OutputDirectory 'results.json') -Raw | ConvertFrom-Json
    if ($existingResults.completed -ne $existingResults.total) { throw 'DUXEL-ERD-MEASUREMENT-INCOMPLETE: preserve unfinished measurement results.' }
    foreach ($entry in $existingResults.results) { Test-ErDFrameStatistics $entry.resultPath $entry.result }
    Write-ErDComparisonSummary -RunResults $existingResults.results -Directory $OutputDirectory
    return
}
$repoRoot = Split-Path $PSScriptRoot -Parent
if (-not $OutputDirectory) {
    $resultRoot = if ($Executable) { Join-Path $PSScriptRoot 'results' } else { Join-Path $repoRoot 'artifacts/erd-layer-lab' }
    $OutputDirectory = Join-Path $resultRoot (Get-Date -Format 'yyyyMMdd-HHmmss')
}
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $OutputDirectory) { throw "Use a new output directory to protect existing results: $OutputDirectory" }
if ($Executable) {
    $sample = (Resolve-Path -LiteralPath $Executable -ErrorAction Stop).ProviderPath
    $workingDirectory = Split-Path $sample -Parent
}
else {
    $dotnetCommand = (Get-Command $DotnetPath -ErrorAction Stop).Source
    $sample = Join-Path $repoRoot 'samples/fba/erd_layer_lab.cs'
    $workingDirectory = if ($TargetFramework.StartsWith('net11.')) { Join-Path $repoRoot 'tests/runtime-net11' } else { $repoRoot }
}
$candidates = @{ current = $sample }
$comparisonModes = @(
    if ($CompareCompiledLayers) { 'compiled-layers' }
    if ($CompareSharedLayerBasis) { 'shared-basis' }
    if ($CompareSharedLayerCamera) { 'shared-camera' }
    if ($CompareDescriptorReuse) { 'descriptor-reuse' }
    if ($CompareCommandSpans) { 'command-spans' }
    if ($CompareLayerBoundsReuse) { 'layer-bounds' }
    if ($CompareDirectLayerReplay) { 'direct-layer-replay' }
    if ($CompareRenderPipeline) { 'render-pipeline' }
    if ($ComparePlatformEventLoop) { 'platform-event-loop' }
    if ($CompareCpuTimingSampling) { 'cpu-timing-sampling' }
    if ($CompareCompiledIndexCapacity) { 'compiled-index-capacity' }
    if ($CompareAxisAlignedCulling) { 'axis-aligned-culling' }
)
if ($comparisonModes.Count -gt 1) { throw 'COMPARISON_MODE_CONFLICT: select one comparison mode.' }
if ($CompareCpuTimingSampling -and $Scenarios -contains 'idle') { throw 'CPU_TIMING_ACTIVE_SCENARIO_REQUIRED' }
if ($CompareCompiledIndexCapacity -and $Scenarios -contains 'idle') { throw 'COMPILED_INDEX_ACTIVE_SCENARIO_REQUIRED' }
if ($CompareAxisAlignedCulling -and $Scenarios -contains 'idle') { throw 'AXIS_CULLING_ACTIVE_SCENARIO_REQUIRED' }
if ($ControlledWorkloadClock -and ($Scenarios -contains 'idle' -or $BaselineExecutable)) { throw 'CONTROLLED_WORKLOAD_CURRENT_ACTIVE_REQUIRED' }
if ($comparisonModes.Count -gt 0) {
    if ($BaselineExecutable -or -not $Executable) { throw 'COMPARISON_EXECUTABLE_REQUIRED: comparison modes require one -Executable and no -BaselineExecutable.' }
    $candidates.baseline = $sample
}
if ($BaselineExecutable) {
    if (-not $Executable) { throw '-BaselineExecutable requires -Executable.' }
    $candidates.baseline = (Resolve-Path -LiteralPath $BaselineExecutable -ErrorAction Stop).ProviderPath
    if ($candidates.baseline -eq $sample) { throw 'Baseline and current executable paths must differ.' }
}
$hasComparison = $BaselineExecutable -or $comparisonModes.Count -gt 0
$environmentNames = @('PATH', 'DUXEL_ERD_SCENARIO', 'DUXEL_ERD_MODE', 'DUXEL_ERD_OUT', 'DUXEL_ERD_TABLES', 'DUXEL_ERD_VIEW', 'DUXEL_SAMPLE_AUTO_EXIT_SECONDS', 'DUXEL_VK_COMPILED_LAYERS', 'DUXEL_VK_SHARED_LAYER_BASIS', 'DUXEL_VK_SHARED_LAYER_CAMERA','DUXEL_VK_DESCRIPTOR_REUSE','DUXEL_VK_COMMAND_SPANS','DUXEL_VK_LAYER_BOUNDS_REUSE','DUXEL_ERD_DIRECT_REPLAY','DUXEL_ERD_PIPELINE','DUXEL_ERD_EVENT_LOOP','DUXEL_VK_PROFILE_EVERY','DUXEL_VK_PROFILE_TIMING_SAMPLING','DUXEL_VK_COMPILED_INDEX_LIMIT','DUXEL_VK_AXIS_ALIGNED_CULLING','DUXEL_ERD_WORKLOAD_CLOCK','DUXEL_ERD_MEASURE_SECONDS')
$originalEnvironment = @{}
foreach ($name in $environmentNames) { $originalEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
$results = [Collections.Generic.List[object]]::new()
Push-Location -LiteralPath $workingDirectory
try {
    $sdk = 'prebuilt executable'
    if (-not $Executable) {
        $env:PATH = (Split-Path $dotnetCommand -Parent) + ';' + $env:PATH
        $sdk = (& $dotnetCommand --version | Out-String).Trim()
        if ($LASTEXITCODE -ne 0) { throw 'Selected SDK is unavailable.' }
    }
    New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
    $sampleHash = (Get-FileHash -LiteralPath $sample -Algorithm SHA256).Hash
    $candidateHashes = @{}
    foreach ($candidate in $candidates.GetEnumerator()) {
        $candidateHashes[$candidate.Key] = (Get-FileHash -LiteralPath $candidate.Value -Algorithm SHA256).Hash
    }
    $completed = 0
    $total = $Runs * $Scenarios.Length * $Modes.Length * $candidates.Count * $TableCounts.Length * $Views.Length
    foreach ($tableCount in $TableCounts) {
    foreach ($view in $Views) {
    $env:DUXEL_ERD_TABLES = "$tableCount"
    $env:DUXEL_ERD_VIEW = $view
    for ($run = 1; $run -le $Runs; $run++) {
        foreach ($scenario in $Scenarios) {
            $modeOrder = @($Modes)
            if ($run % 2 -eq 0) { [Array]::Reverse($modeOrder) }
            foreach ($mode in $modeOrder) {
            $variantOrder = if ($hasComparison) { @('baseline', 'current') } else { @('current') }
            if ($run % 2 -eq 0) { [Array]::Reverse($variantOrder) }
            foreach ($variant in $variantOrder) {
                foreach ($candidate in $candidates.GetEnumerator()) {
                    if ((Get-FileHash -LiteralPath $candidate.Value -Algorithm SHA256).Hash -ne $candidateHashes[$candidate.Key]) { throw 'ERD comparison input changed during measurement.' }
                }
                if ($RequireExclusive) {
                    $processNames = @($candidates.Values | ForEach-Object { [IO.Path]::GetFileNameWithoutExtension($_) }) + @('erd_layer_lab', 'Duxel.ErdLayerLab')
                    $existing = @(Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.ProcessName -in $processNames })
                    if ($existing.Count -gt 0) { throw 'DUXEL-ERD-CONCURRENT-PROCESS: a comparison window is already running; it was preserved.' }
                }
                if ((Get-FileHash -LiteralPath $sample -Algorithm SHA256).Hash -ne $sampleHash) { throw 'ERD source changed during the measurement.' }
                $variantSuffix = if ($hasComparison) { "-$variant" } else { '' }
                $prefix = Join-Path $OutputDirectory "tables-$tableCount-$view-$scenario-$mode$variantSuffix-$run"
                $env:DUXEL_ERD_SCENARIO = $scenario
                $env:DUXEL_ERD_MODE = $mode
                $env:DUXEL_ERD_OUT = "$prefix.json"
                $env:DUXEL_SAMPLE_AUTO_EXIT_SECONDS = "$Seconds"
                $env:DUXEL_ERD_WORKLOAD_CLOCK = if ($ControlledWorkloadClock) {'post-warmup'} else {'wall-total'}
                $env:DUXEL_ERD_MEASURE_SECONDS = "$Seconds"
                if ($ControlledWorkloadClock) { $env:DUXEL_SAMPLE_AUTO_EXIT_SECONDS = "$($Seconds + 120)" }
                if ($CompareRenderPipeline) { $env:DUXEL_ERD_PIPELINE = if ($variant -eq 'baseline') { '0' } else { '1' } }
                if ($ComparePlatformEventLoop) {
                    $env:DUXEL_ERD_PIPELINE = '1'
                    $env:DUXEL_ERD_EVENT_LOOP = if ($variant -eq 'baseline') { '0' } else { '1' }
                }
                if ($CompareCpuTimingSampling) {
                    $env:DUXEL_ERD_PIPELINE='1'
                    $env:DUXEL_ERD_EVENT_LOOP='1'
                    $env:DUXEL_VK_PROFILE_EVERY='1000000'
                    $env:DUXEL_VK_PROFILE_TIMING_SAMPLING=if ($variant -eq 'baseline') {'0'} else {'1'}
                }
                if ($CompareCompiledIndexCapacity -or $CompareAxisAlignedCulling) {
                    $env:DUXEL_ERD_PIPELINE='1'
                    $env:DUXEL_ERD_EVENT_LOOP='1'
                    $env:DUXEL_VK_COMPILED_LAYERS='1'
                    $env:DUXEL_VK_SHARED_LAYER_BASIS='1'
                    $env:DUXEL_VK_SHARED_LAYER_CAMERA='1'
                    $env:DUXEL_VK_DESCRIPTOR_REUSE='1'
                    $env:DUXEL_VK_COMMAND_SPANS='1'
                    $env:DUXEL_VK_LAYER_BOUNDS_REUSE='1'
                    $env:DUXEL_VK_PROFILE_EVERY='1000000'
                    $env:DUXEL_VK_PROFILE_TIMING_SAMPLING='1'
                    $env:DUXEL_VK_COMPILED_INDEX_LIMIT=if ($CompareCompiledIndexCapacity -and $variant -eq 'baseline') {'1048576'} else {'2097152'}
                    $env:DUXEL_VK_AXIS_ALIGNED_CULLING=if ($CompareAxisAlignedCulling -and $variant -eq 'baseline') {'0'} else {'1'}
                }
                if ($CompareCompiledLayers) { $env:DUXEL_VK_COMPILED_LAYERS = if ($variant -eq 'baseline') { '0' } else { '1' } }
                if ($CompareSharedLayerBasis) { $env:DUXEL_VK_COMPILED_LAYERS = '1'; $env:DUXEL_VK_SHARED_LAYER_CAMERA = '0'; $env:DUXEL_VK_SHARED_LAYER_BASIS = if ($variant -eq 'baseline') { '0' } else { '1' } }
                if ($CompareSharedLayerCamera) {
                    $env:DUXEL_VK_COMPILED_LAYERS = '1'
                    $env:DUXEL_VK_SHARED_LAYER_BASIS = '1'
                    $env:DUXEL_VK_SHARED_LAYER_CAMERA = if ($variant -eq 'baseline') { '0' } else { '1' }
                }
                if ($CompareDirectLayerReplay) {
                    $env:DUXEL_VK_COMPILED_LAYERS = '1'
                    $env:DUXEL_VK_SHARED_LAYER_BASIS = '1'
                    $env:DUXEL_VK_SHARED_LAYER_CAMERA = '1'
                    $env:DUXEL_VK_DESCRIPTOR_REUSE = '1'
                    $env:DUXEL_VK_COMMAND_SPANS = '1'
                    $env:DUXEL_VK_LAYER_BOUNDS_REUSE = '1'
                    $env:DUXEL_ERD_DIRECT_REPLAY = if ($variant -eq 'baseline') { '0' } else { '1' }
                }
                if ($CompareLayerBoundsReuse) {
                    $env:DUXEL_VK_COMPILED_LAYERS = '1'
                    $env:DUXEL_VK_SHARED_LAYER_BASIS = '1'
                    $env:DUXEL_VK_SHARED_LAYER_CAMERA = '1'
                    $env:DUXEL_VK_DESCRIPTOR_REUSE = '1'
                    $env:DUXEL_VK_COMMAND_SPANS = '1'
                    $env:DUXEL_VK_LAYER_BOUNDS_REUSE = if ($variant -eq 'baseline') { '0' } else { '1' }
                }
                if ($CompareCommandSpans) {
                    $env:DUXEL_VK_COMPILED_LAYERS = '1'
                    $env:DUXEL_VK_SHARED_LAYER_BASIS = '1'
                    $env:DUXEL_VK_SHARED_LAYER_CAMERA = '1'
                    $env:DUXEL_VK_DESCRIPTOR_REUSE = '1'
                    $env:DUXEL_VK_COMMAND_SPANS = if ($variant -eq 'baseline') { '0' } else { '1' }
                }
                if ($CompareDescriptorReuse) {
                    $env:DUXEL_VK_COMPILED_LAYERS = '1'
                    $env:DUXEL_VK_SHARED_LAYER_BASIS = '1'
                    $env:DUXEL_VK_SHARED_LAYER_CAMERA = '1'
                    $env:DUXEL_VK_DESCRIPTOR_REUSE = if ($variant -eq 'baseline') { '0' } else { '1' }
                }
                if ($Executable) {
                    $candidatePath = $candidates[$variant]
                    $process = Start-Process -FilePath $candidatePath -PassThru -WorkingDirectory (Split-Path $candidatePath -Parent) -RedirectStandardOutput "$prefix.child.stdout.log" -RedirectStandardError "$prefix.child.stderr.log"
                    if (-not $process.WaitForExit(($Seconds + 120) * 1000)) { $process.Kill(); throw "ERD executable timed out: $prefix" }
                    $executionExitCode = $process.ExitCode
                    $process.Dispose()
                }
                else {
                    $runner = @{ Path = $sample; DotnetPath = $dotnetCommand; TargetFramework = $TargetFramework; Managed = $true; NoCache = $true; ManagedTimeoutSeconds = $Seconds + 120; ManagedLogPrefix = "$prefix.child"; KillProcessTreeOnTimeout = $true }
                    & (Join-Path $repoRoot 'run-fba.ps1') @runner 1> "$prefix.stdout.log" 2> "$prefix.stderr.log"
                    $executionExitCode = $LASTEXITCODE
                }
                if ($executionExitCode -ne 0) { throw "ERD $scenario/$mode/$run failed with $executionExitCode" }
                if ((Get-Content -LiteralPath "$prefix.child.stderr.log" -Raw) -match 'RenderThreadException:') { throw "ERD render thread failed: $prefix" }
                if (-not (Test-Path -LiteralPath "$prefix.json" -PathType Leaf)) { throw "ERD result is missing: $prefix.json" }
                $result = Get-Content -LiteralPath "$prefix.json" -Raw | ConvertFrom-Json
                Test-ErDFrameStatistics "$prefix.json" $result
                foreach ($candidate in $candidates.GetEnumerator()) {
                    if ((Get-FileHash -LiteralPath $candidate.Value -Algorithm SHA256).Hash -ne $candidateHashes[$candidate.Key]) { throw 'ERD comparison input changed during measurement.' }
                }
                if ((Get-FileHash -LiteralPath $sample -Algorithm SHA256).Hash -ne $sampleHash) { throw 'ERD source changed during the measurement.' }
                if ($result.scenario -ne $scenario -or $result.mode -ne $mode -or $result.tables -ne $tableCount -or $result.view -ne $view) { throw 'ERD scenario changed during the measurement.' }
                if ($scenario -ne 'idle' -and $result.performance.samples -le 0) { throw 'ERD produced no measured frame samples.' }
                $presentationLinkRows = $null
                if ($result.fpsSamplingMethod -eq 'presentation-history-v1') {
                    if ($result.presentationHistoryOverwrites -ne 0) { throw 'ERD_PRESENTATION_HISTORY_OVERFLOW' }
                    if ($scenario -ne 'idle' -and ($null -eq $result.warmupPresentationDeadlineTicks -or $result.firstMeasuredPresentationTimestampTicks -le 0)) { throw 'ERD_PRESENTATION_WARMUP_METADATA_MISSING' }
                    if ($scenario -ne 'idle' -and $result.firstMeasuredPresentationTimestampTicks -lt $result.warmupPresentationDeadlineTicks) { throw 'ERD_PRESENTATION_WARMUP_BOUNDARY' }
                    $presentationLinkRows = Test-ErDPresentationLinks -JsonPath "$prefix.json" -ExpectedSamples ([long]$result.performance.samples)
                }
                if ($CompareRenderPipeline -or $ComparePlatformEventLoop -or $CompareCpuTimingSampling -or $CompareCompiledIndexCapacity) { Test-ErDPresentationWarmup $result }
                if (($CompareCommandSpans -or $CompareLayerBoundsReuse -or $CompareDirectLayerReplay) -and $result.fpsTimingBasis -ne 'accepted-presentation') { throw 'FPS_TIMING_BASIS_REQUIRED: command span comparison requires accepted-presentation samples.' }
                if ($CompareDirectLayerReplay -and $result.directLayerReplay -ne ($variant -eq 'current')) { throw 'DIRECT_LAYER_REPLAY_MODE_MISMATCH' }
                if ($CompareRenderPipeline -and $result.renderPipeline -ne ($variant -eq 'current')) { throw 'RENDER_PIPELINE_MODE_MISMATCH' }
                if ($ComparePlatformEventLoop -and ($result.eventDrivenPlatformLoop -ne ($variant -eq 'current') -or $result.renderPipeline -ne $true)) { throw 'PLATFORM_EVENT_LOOP_MODE_MISMATCH' }
                if ($ComparePlatformEventLoop) { Test-ErDProcessMetrics $result }
                if ($CompareCpuTimingSampling) {
                    Test-ErDProcessMetrics $result
                    Test-ErDCpuTiming $result ($variant -eq 'current')
                }
                if ($CompareCompiledIndexCapacity) {
                    Test-ErDCompiledIndexCapacity $result $(if ($variant -eq 'baseline') {1048576} else {2097152})
                }
                if ($CompareAxisAlignedCulling) {
                    Test-ErDAxisAlignedCulling $result ($variant -eq 'current')
                }
                if ($ControlledWorkloadClock) { Test-ErDControlledWorkload $result "$prefix.json" $Seconds }
                $results.Add([pscustomobject]@{run=$run;scenario=$scenario;mode=$mode;variant=$variant;presentationLinkRows=$presentationLinkRows;directLayerReplay=if ($CompareDirectLayerReplay) {$variant -eq 'current'} else {$null};layerBoundsReuse=if ($CompareLayerBoundsReuse) {$variant -eq 'current'} elseif ($CompareDirectLayerReplay) {$true} else {$null};commandSpans=if ($CompareCommandSpans) {$variant -eq 'current'} elseif ($CompareLayerBoundsReuse -or $CompareDirectLayerReplay) {$true} else {$null};compiledLayers=if ($CompareCompiledLayers) {$variant -eq 'current'} elseif ($CompareSharedLayerBasis -or $CompareSharedLayerCamera -or $CompareDescriptorReuse -or $CompareCommandSpans -or $CompareLayerBoundsReuse -or $CompareDirectLayerReplay) {$true} else {$null};sharedBasis=if ($CompareSharedLayerBasis) {$variant -eq 'current'} elseif ($CompareSharedLayerCamera -or $CompareDescriptorReuse -or $CompareCommandSpans -or $CompareLayerBoundsReuse -or $CompareDirectLayerReplay) {$true} else {$null};sharedCamera=if ($CompareSharedLayerCamera) {$variant -eq 'current'} elseif ($CompareSharedLayerBasis) {$false} elseif ($CompareDescriptorReuse -or $CompareCommandSpans -or $CompareLayerBoundsReuse -or $CompareDirectLayerReplay) {$true} else {$null};descriptorReuse=if ($CompareDescriptorReuse) {$variant -eq 'current'} elseif ($CompareCommandSpans -or $CompareLayerBoundsReuse -or $CompareDirectLayerReplay) {$true} else {$null};resultPath="$prefix.json";result=$result})
                $completed++
                $progress = [pscustomobject]@{sdk=$sdk;targetFramework=$TargetFramework;sampleSha256=$sampleHash;candidates=$candidates;candidateHashes=$candidateHashes;completed=$completed;total=$total;results=$results.ToArray()}
                $progress | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'results.json') -Encoding utf8
                Write-Host "[ERD] $completed/$total ($([Math]::Round($completed * 100.0 / $total, 2))%) $tableCount tables / $view / $scenario/$mode/$variant run $run"
            }
            }
        }
    }
    }
    }
    Write-ErDComparisonSummary -RunResults $results.ToArray() -Directory $OutputDirectory
    Write-Host "[ERD] Results: $OutputDirectory"
}
finally {
    foreach ($name in $environmentNames) { [Environment]::SetEnvironmentVariable($name, $originalEnvironment[$name], 'Process') }
    Pop-Location
}
