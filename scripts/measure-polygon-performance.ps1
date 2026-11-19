param(
    [Parameter(Mandatory)][string]$Executable,
    [Parameter(Mandatory)][string]$OutDir,
    [string]$Label = 'candidate',
    [int]$Runs = 3,
    [int]$Count = 5000,
    [ValidateRange(6,80)][double]$Size = 6,
    [double]$Seconds = 6,
    [ValidateRange(640,16384)][int]$Width = 1920,
    [ValidateRange(480,16384)][int]$Height = 1080,
    [ValidateSet('cpu','cpu-jacobi','gpu')][string]$Simulation = 'gpu',
    [ValidateRange(1,2147483647)][int]$ProfileEvery = 1000000,
    [ValidateRange(1,8)][int]$CollisionIterations = 2,
    [switch]$DisableRenderPipeline,
    [string[]]$Cases = @('render-cache', 'render-uncached', 'display-cache')
)
$ErrorActionPreference = 'Stop'
function Test-PolygonFrameStatistics($Result, [double[]]$Frames) {
    if ($Frames.Length -eq 0 -or @($Frames | Where-Object { -not [double]::IsFinite($_) -or $_ -le 0 }).Count -ne 0) { throw 'POLYGON_BENCH_INVALID_FRAMES' }
    $orderedFrames = [double[]]$Frames.Clone()
    [Array]::Sort($orderedFrames)
    foreach ($spec in @(@('medianFrameMs',.5),@('p95FrameMs',.95),@('p99FrameMs',.99))) {
        $position = ($orderedFrames.Length - 1) * $spec[1]
        $lower = [int][Math]::Floor($position)
        $upper = [Math]::Min($lower + 1,$orderedFrames.Length - 1)
        $expected = 1000 * ($orderedFrames[$lower] + ($orderedFrames[$upper] - $orderedFrames[$lower]) * ($position - $lower))
        if ([Math]::Abs($Result.($spec[0]) - $expected) -gt .000001) { throw "POLYGON_BENCH_PERCENTILE_MISMATCH $($spec[0])" }
    }
    $slowCount = [Math]::Max(1,[int][Math]::Ceiling($orderedFrames.Length * .01))
    $slowSeconds = 0.0
    for ($index = $orderedFrames.Length - $slowCount; $index -lt $orderedFrames.Length; $index++) { $slowSeconds += $orderedFrames[$index] }
    if ([Math]::Abs($Result.low1PctFps - $slowCount / $slowSeconds) -gt .000001) { throw 'POLYGON_BENCH_LOW1_MISMATCH' }
}
if ($Runs -lt 1 -or $Count -lt 1 -or -not [double]::IsFinite($Seconds) -or $Seconds -le 0 -or $Cases.Count -eq 0) { throw 'POLYGON_BENCH_INVALID_INPUT' }
foreach ($case in $Cases) {
    if ($case -notin @('render-cache','render-uncached','display-cache','render-direct','render-paused')) { throw 'POLYGON_BENCH_UNKNOWN_CASE' }
}
$Executable = (Resolve-Path -LiteralPath $Executable).Path
$OutDir = [IO.Path]::GetFullPath($OutDir)
New-Item -ItemType Directory -Path $OutDir -Force | Out-Null
foreach ($name in @('results.json','execution-record.json')) {
    if (Test-Path -LiteralPath (Join-Path $OutDir $name)) { throw 'POLYGON_BENCH_OUTPUT_EXISTS' }
}
for ($run = 1; $run -le $Runs; $run++) {
    foreach ($case in $Cases) {
        foreach ($extension in @('.json','.json.frames.csv','.stdout.log','.stderr.log','.gpu.log','.resources.csv')) {
            if (Test-Path -LiteralPath (Join-Path $OutDir "$Label-$case-$run$extension")) { throw 'POLYGON_BENCH_OUTPUT_EXISTS' }
        }
    }
}
if (Get-Process -Name 'Duxel_perf_test_fba' -ErrorAction SilentlyContinue) { throw 'POLYGON_BENCH_REQUIRES_EXCLUSIVE_SAMPLE' }
$variables = @('DUXEL_PERF_BENCH_OUT','DUXEL_PERF_BENCH_SECONDS','DUXEL_PERF_INITIAL_POLYGONS','DUXEL_PERF_INITIAL_SIZE','DUXEL_PERF_PROFILE','DUXEL_PERF_GLOBAL_STATIC_CACHE','DUXEL_PERF_WARMUP_SECONDS','DUXEL_PERF_DYNAMIC_CANVAS','DUXEL_PERF_PAUSED','DUXEL_PERF_SIMULATION','DUXEL_PERF_GPU_ITERATIONS','DUXEL_PERF_PIPELINE','DUXEL_PERF_WINDOW_WIDTH','DUXEL_PERF_WINDOW_HEIGHT','DUXEL_VK_PROFILE','DUXEL_VK_GPU_PROFILE','DUXEL_VK_PROFILE_OUT','DUXEL_VK_PROFILE_EVERY')
$saved = @{}
foreach ($name in $variables) { $saved[$name] = [Environment]::GetEnvironmentVariable($name) }
$rows = [Collections.Generic.List[object]]::new()
$executableHash = (Get-FileHash -LiteralPath $Executable -Algorithm SHA256).Hash
try {
    $env:DUXEL_PERF_INITIAL_POLYGONS = [string]$Count
    $env:DUXEL_PERF_WINDOW_WIDTH = [string]$Width
    $env:DUXEL_PERF_WINDOW_HEIGHT = [string]$Height
    $env:DUXEL_PERF_SIMULATION = $Simulation
    $env:DUXEL_PERF_GPU_ITERATIONS = [string]$CollisionIterations
    $env:DUXEL_PERF_PIPELINE = if($DisableRenderPipeline){'0'}else{'1'}
    $env:DUXEL_PERF_INITIAL_SIZE = $Size.ToString('R', [Globalization.CultureInfo]::InvariantCulture)
    $env:DUXEL_PERF_BENCH_SECONDS = $Seconds.ToString('R', [Globalization.CultureInfo]::InvariantCulture)
    $env:DUXEL_PERF_WARMUP_SECONDS = '1'
    $env:DUXEL_VK_GPU_PROFILE = '1'
    $env:DUXEL_VK_PROFILE = '1'
    $env:DUXEL_VK_PROFILE_EVERY = [string]$ProfileEvery
    for ($run = 1; $run -le $Runs; $run++) {
        $order = @($Cases)
        if ($run % 2 -eq 0) { [Array]::Reverse($order) }
        foreach ($case in $order) {
            if ($case -notin @('render-cache','render-uncached','display-cache','render-direct','render-paused')) { throw 'POLYGON_BENCH_UNKNOWN_CASE' }
            $stem = Join-Path $OutDir "$Label-$case-$run"
            $env:DUXEL_PERF_BENCH_OUT = "$stem.json"
            $env:DUXEL_PERF_PROFILE = if ($case -eq 'display-cache') { 'display' } else { 'render' }
            $env:DUXEL_PERF_GLOBAL_STATIC_CACHE = if ($case -eq 'render-uncached') { '0' } else { '1' }
            $env:DUXEL_PERF_DYNAMIC_CANVAS = if ($case -eq 'render-direct') { '0' } else { '1' }
            $env:DUXEL_PERF_PAUSED = if ($case -eq 'render-paused') { '1' } else { '0' }
            $env:DUXEL_VK_PROFILE_OUT = "$stem.gpu.log"
            $launch = @{ FilePath=$Executable; WorkingDirectory=(Split-Path -Parent $Executable); WindowStyle='Hidden'; PassThru=$true; RedirectStandardOutput="$stem.stdout.log"; RedirectStandardError="$stem.stderr.log" }
            $process = Start-Process @launch
            $windowHandleObserved = $false
            $resources = [Collections.Generic.List[object]]::new()
            $nextSampleSeconds = 0.0
            $watch = [Diagnostics.Stopwatch]::StartNew()
            while (-not $process.HasExited) {
                $process.Refresh()
                if ($process.MainWindowHandle -ne 0) { $windowHandleObserved = $true }
                if ($watch.Elapsed.TotalSeconds -ge $nextSampleSeconds) {
                    $sample = [pscustomobject]@{
                        elapsedSeconds=$watch.Elapsed.TotalSeconds
                        privateBytes=$process.PrivateMemorySize64
                        workingSetBytes=$process.WorkingSet64
                        peakWorkingSetBytes=$process.PeakWorkingSet64
                        cpuMilliseconds=$process.TotalProcessorTime.TotalMilliseconds
                        handles=$process.HandleCount
                        threads=$process.Threads.Count
                    }
                    $resources.Add($sample)
                    $sample | Export-Csv -LiteralPath "$stem.resources.csv" -NoTypeInformation -Append
                    $nextSampleSeconds = [Math]::Floor($watch.Elapsed.TotalSeconds) + 1
                }
                if ($watch.Elapsed.TotalSeconds -gt $Seconds + 30) {
                    $null = $process.CloseMainWindow()
                    throw "POLYGON_BENCH_TIMEOUT pid=$($process.Id)"
                }
                Start-Sleep -Milliseconds 100
            }
            $process.WaitForExit()
            if ($process.ExitCode -ne 0) { throw "POLYGON_BENCH_PROCESS_FAILED exit=$($process.ExitCode)" }
            $result = Get-Content -LiteralPath "$stem.json" -Raw | ConvertFrom-Json
            $frames = @(Get-Content -LiteralPath "$stem.json.frames.csv" | Select-Object -Skip 1 | ForEach-Object { [double]::Parse($_,[Globalization.CultureInfo]::InvariantCulture) })
            $frameSum = ($frames | Measure-Object -Sum).Sum
            if ($result.count -ne $Count -or $result.size -ne $Size -or $result.samples -ne $frames.Count -or [Math]::Abs($result.avgFps - $frames.Count / $frameSum) -gt 0.000001 -or $result.vsync) { throw 'POLYGON_BENCH_RAW_CONTRACT' }
            if ($result.canvasWidth -le 0 -or $result.canvasHeight -le 0 -or $result.framebufferWidth -le 0 -or $result.framebufferHeight -le 0 -or $result.drawCalls -le 0 -or -not $result.profilingEnabled) { throw 'POLYGON_BENCH_TELEMETRY_REQUIRED' }
            Test-PolygonFrameStatistics $result $frames
            $rows.Add([pscustomobject]@{label=$Label;run=$run;case=$case;windowHandleObserved=$windowHandleObserved;visualAcceptance=$false;exitCode=$process.ExitCode;cpuSeconds=$process.TotalProcessorTime.TotalSeconds;resourceSamples=$resources.Count;resourcesPath="$stem.resources.csv";result=$result;path="$stem.json"})
            ConvertTo-Json -InputObject @($rows.ToArray()) -Depth 8 | Set-Content -LiteralPath (Join-Path $OutDir 'results.json')
            Write-Host "$Label $case $run : $([Math]::Round($result.avgFps,2)) FPS; collision $([Math]::Round($result.collisionMs,3)) ms; geometry $([Math]::Round($result.geometryMs,3)) ms"
        }
    }
    if ((Get-FileHash -LiteralPath $Executable -Algorithm SHA256).Hash -ne $executableHash) { throw 'POLYGON_BENCH_INPUT_CHANGED' }
    [pscustomobject]@{ executable=$Executable;sha256=$executableHash;count=$Count;runs=$Runs;seconds=$Seconds;requestedWidth=$Width;requestedHeight=$Height;cases=$Cases;resourceScope='Process-lifetime samples; not synchronized to UI warmup and no forced GC';visualAcceptance=$false;complete=$true;completedUtc=[DateTime]::UtcNow.ToString('o') } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $OutDir 'execution-record.json')
} finally {
    foreach ($name in $variables) { [Environment]::SetEnvironmentVariable($name, $saved[$name]) }
}
