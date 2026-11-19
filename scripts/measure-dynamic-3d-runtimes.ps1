param(
    [Parameter(Mandatory)][string]$OutputDirectory,
    [Parameter(Mandatory)][string]$SourceManifestPath,
    [string]$DotnetPath = 'C:/Program Files/dotnet/dotnet.exe',
    [ValidateRange(3, 10)][int]$Runs = 3,
    [ValidateRange(.8, 30)][double]$PhaseSeconds = 1.5,
    [ValidateSet('managed', 'aot')][string[]]$Modes = @('managed', 'aot'),
    [ValidateSet('Balanced', 'Speed', 'Size')][string]$NativeOptimization = 'Balanced',
    [string]$Net10Framework = '.NET 10.0.14',
    [string]$Net11Framework = '.NET 11.0.0'
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw 'RUNTIME_OUTPUT_EXISTS: preserve existing results.' }
$sourceManifest = Get-Content -LiteralPath $SourceManifestPath -Raw | ConvertFrom-Json
$dotnet = (Get-Command $DotnetPath -ErrorAction Stop).Source
$sample = Join-Path $repo 'samples/fba/dynamic_3d_layers_bench.cs'
$native10 = Join-Path $repo 'samples/fba/artifacts/dynamic_3d_layers_bench/dynamic_3d_layers_bench.exe'
$native11 = Join-Path $repo 'samples/fba/artifacts/dynamic_3d_layers_bench/net11.0-windows7.0/dynamic_3d_layers_bench.exe'
$nativeHashes = @{}
foreach ($path in @($native10, $native11)) { $nativeHashes[$path] = (Get-FileHash -LiteralPath $path).Hash }
$sampleHash = (Get-FileHash -LiteralPath $sample).Hash
$total = 2 * $Modes.Count * $Runs
if (@($Modes | Sort-Object -Unique).Count -ne $Modes.Count) { throw 'RUNTIME_DUPLICATE_MODES' }

function Assert-RuntimeInputs {
    foreach ($entry in $sourceManifest) {
        if ((Get-FileHash -LiteralPath (Join-Path $repo $entry.path)).Hash -ne $entry.sha256) {
            throw "RUNTIME_SOURCE_CHANGED: $($entry.path)"
        }
    }
    foreach ($path in $nativeHashes.Keys) {
        if ((Get-FileHash -LiteralPath $path).Hash -ne $nativeHashes[$path]) { throw 'RUNTIME_NATIVE_CHANGED' }
    }
    if ((Get-FileHash -LiteralPath $sample).Hash -ne $sampleHash) { throw 'RUNTIME_SAMPLE_CHANGED' }
}
function Assert-NoRuntimeProcess {
    if (Get-Process -Name 'dynamic_3d_layers_bench', 'erd_layer_lab', 'Duxel.Rendering.Tests' -ErrorAction SilentlyContinue) {
        throw 'RUNTIME_CONCURRENT_PROCESS: existing windows were preserved.'
    }
}
function Assert-Close([double]$Actual, [double]$Expected) {
    if (-not [double]::IsFinite($Actual) -or [Math]::Abs($Actual - $Expected) -gt [Math]::Max(1e-7, [Math]::Abs($Expected) * 1e-8)) {
        throw "RUNTIME_RAW_STAT_MISMATCH: $Actual / $Expected"
    }
}
function Get-Percentile([double[]]$Sorted, [double]$Fraction) {
    $position = ($Sorted.Length - 1) * $Fraction
    $lower = [int][Math]::Floor($position)
    $upper = [Math]::Min($lower + 1, $Sorted.Length - 1)
    return $Sorted[$lower] + ($Sorted[$upper] - $Sorted[$lower]) * ($position - $lower)
}
function Test-RuntimeResult([string]$Prefix, [object]$Result, [bool]$Aot, [string]$Framework, [bool]$Reverse) {
    if ($Result.nativeAot -ne $Aot -or $Result.framework -ne $Framework -or $Result.staticCache -ne $false -or
        $Result.records.Count -ne 6 -or $Result.warmupSeconds -ne .25) { throw 'RUNTIME_MODE_CONTRACT' }
    $expected = @('1|1000|False', '1|1000|True', '8|1000|False', '8|1000|True', '8|4000|False', '8|4000|True')
    if ($Reverse) { [Array]::Reverse($expected) }
    foreach ($phase in $Result.records) {
        $index = [int]$phase.phase
        if ("$($phase.layers)|$($phase.shapes)|$($phase.scene3D)" -ne $expected[$index]) { throw 'RUNTIME_PHASE_ORDER' }
        [double[]]$raw = @(Import-Csv -LiteralPath "$Prefix.json.phase-$index.frames.csv" | ForEach-Object {
            [double]::Parse($_.frameSeconds, [cultureinfo]::InvariantCulture)
        })
        if ($raw.Length -ne $phase.samples -or $raw.Length -lt 2) { throw 'RUNTIME_RAW_FRAME_COUNT' }
        $sum = 0.0
        foreach ($value in $raw) {
            if (-not [double]::IsFinite($value) -or $value -le 0) { throw 'RUNTIME_RAW_FRAME_VALUE' }
            $sum += $value
        }
        if ([Math]::Abs($sum - $PhaseSeconds) -gt .15) { throw 'RUNTIME_PHASE_DURATION' }
        [Array]::Sort($raw)
        Assert-Close $phase.avgFps ($raw.Length / $sum)
        Assert-Close $phase.medianFrameMs ((Get-Percentile $raw .5) * 1000)
        Assert-Close $phase.p95FrameMs ((Get-Percentile $raw .95) * 1000)
        Assert-Close $phase.p99FrameMs ((Get-Percentile $raw .99) * 1000)
        $slowCount = [Math]::Max(1, [int][Math]::Ceiling($raw.Length * .01))
        $slowSeconds = 0.0
        for ($i = $raw.Length - $slowCount; $i -lt $raw.Length; $i++) { $slowSeconds += $raw[$i] }
        Assert-Close $phase.low1PctFps ($slowCount / $slowSeconds)
    }
}
function Read-RuntimeGpuPhases([string]$Path) {
    $phases = @{}
    $index = -1
    foreach ($line in [IO.File]::ReadLines($Path)) {
        if ($line -match '^\[bench-phase\] index=(\d+) ') {
            $index = [int]$Matches[1]
            if ($phases.ContainsKey($index)) { throw 'RUNTIME_DUPLICATE_GPU_PHASE' }
            $phases[$index] = [Collections.Generic.List[object]]::new()
        }
        elseif ($line.StartsWith('[duxel-vk-prof]')) {
            if ($index -lt 0 -or $line -notmatch 'gpuTs=1\)') { throw 'RUNTIME_GPU_TIMESTAMPS_REQUIRED' }
            $metrics = @{}
            foreach ($pair in @(@('gpuMicroseconds',' gpuRender=([0-9.]+)'), @('drawCalls',' draws=(\d+) '),
                @('dynamicUploadBytes',' dynamicUpload\(bytes=(\d+)\)'), @('depthBytes',' depthMem\(images=\d+ bytes=(\d+)\)'))) {
                if ($line -notmatch $pair[1]) { throw "RUNTIME_GPU_FIELD_REQUIRED: $($pair[0])" }
                $metrics[$pair[0]] = [double]::Parse($Matches[1], [cultureinfo]::InvariantCulture)
            }
            $phases[$index].Add([pscustomobject]$metrics)
        }
    }
    if ($phases.Count -ne 6) { throw 'RUNTIME_GPU_PHASE_COUNT' }
    foreach ($key in ($phases.Keys | Sort-Object)) {
        $rows = @($phases[$key] | Select-Object -Skip 1)
        if ($rows.Length -eq 0) { throw 'RUNTIME_GPU_PHASE_SAMPLES' }
        [pscustomobject]@{phase=$key;samples=$rows.Length;gpuMicroseconds=($rows|Measure-Object gpuMicroseconds -Average).Average
            drawCalls=($rows|Measure-Object drawCalls -Average).Average;dynamicUploadBytes=($rows|Measure-Object dynamicUploadBytes -Average).Average
            depthBytes=($rows|Measure-Object depthBytes -Average).Average}
    }
}

Assert-RuntimeInputs
Assert-NoRuntimeProcess
$sdkVersions = @{}
foreach ($major in @(10, 11)) {
    $sdkDir = if ($major -eq 10) { 'tests/runtime-net10-current' } else { 'tests/runtime-net11' }
    Push-Location -LiteralPath (Join-Path $repo $sdkDir)
    try {
        $sdkVersions[$major.ToString()] = (& $dotnet --version | Out-String).Trim()
        if ($LASTEXITCODE -ne 0 -or $sdkVersions[$major.ToString()] -ne (Get-Content -LiteralPath 'global.json' -Raw|ConvertFrom-Json).sdk.version) {
            throw 'RUNTIME_SDK_SELECTION'
        }
    } finally { Pop-Location }
}
New-Item -ItemType Directory -Path $output | Out-Null
$environmentNames = @('DUXEL_3D_BENCH_OUT','DUXEL_3D_BENCH_SECONDS','DUXEL_3D_BENCH_REVERSE','DUXEL_SAMPLE_AUTO_EXIT_SECONDS',
    'DUXEL_VK_PROFILE','DUXEL_VK_PROFILE_EVERY','DUXEL_VK_PROFILE_OUT','DUXEL_VK_GPU_PROFILE','DUXEL_APP_PROFILE','VK_INSTANCE_LAYERS')
$saved = @{}
foreach ($name in $environmentNames) { $saved[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
$results = [Collections.Generic.List[object]]::new()
$contract = [ordered]@{maximumExecutions=$total;phasesPerRun=6;phaseSeconds=$PhaseSeconds;warmupSeconds=.25;sampleSha256=$sampleHash
    sdkVersions=$sdkVersions;nativeHashes=$nativeHashes;sourceManifest=[IO.Path]::GetFullPath($SourceManifestPath)
    modes=$Modes;nativeOptimization=$NativeOptimization
    runnerSha256=(Get-FileHash -LiteralPath $PSCommandPath).Hash;msaa=1;vSync=$false
    windowCondition='Normal interactive sample window for every mode;independent visible-window observer required'
    clock='serial UI frame-start intervals;not ERD accepted-presentation FPS';order='normal on odd runs;reverse variants and phases on even runs'
    gpuSampling='every64frames;skip first per-phase sample for delayed query;includes warmup;not aligned with CPU sample set'}
$contract | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $output 'contract.json') -Encoding utf8
$observer = Start-Job -ArgumentList $output -ScriptBlock {
    param($directory)
    $ErrorActionPreference = 'Stop'
    $writer = [IO.StreamWriter]::new((Join-Path $directory 'window-samples.csv'))
    try {
        $writer.AutoFlush = $true
        $writer.WriteLine('utcTicks,case,pid,visible')
        [IO.File]::WriteAllText((Join-Path $directory 'window-observer.ready'), 'ready')
        while (-not (Test-Path -LiteralPath (Join-Path $directory 'window-observer.stop'))) {
            $caseFile = Join-Path $directory 'current-case.txt'
            if (Test-Path -LiteralPath $caseFile) {
                $stream = [IO.FileStream]::new($caseFile, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::ReadWrite)
                $reader = [IO.StreamReader]::new($stream)
                try { $case = $reader.ReadToEnd() }
                finally { $reader.Dispose() }
                if ($case.Length -gt 0) {
                    foreach ($process in @(Get-Process -Name 'dynamic_3d_layers_bench' -ErrorAction SilentlyContinue)) {
                        try {
                            $writer.WriteLine("$([DateTime]::UtcNow.Ticks),$case,$($process.Id),$([int]($process.MainWindowHandle -ne 0))")
                        } finally { $process.Dispose() }
                    }
                }
            }
            Start-Sleep -Milliseconds 100
        }
    } finally { $writer.Dispose() }
}
try {
    $observerStartup = [Diagnostics.Stopwatch]::StartNew()
    while (-not (Test-Path -LiteralPath (Join-Path $output 'window-observer.ready'))) {
        if ($observer.State -ne 'Running' -or $observerStartup.Elapsed.TotalSeconds -gt 10) { throw 'RUNTIME_WINDOW_OBSERVER_START' }
        Start-Sleep -Milliseconds 50
    }
    $env:DUXEL_3D_BENCH_SECONDS = $PhaseSeconds.ToString('R', [cultureinfo]::InvariantCulture)
    $env:DUXEL_SAMPLE_AUTO_EXIT_SECONDS = '45'
    $env:DUXEL_VK_PROFILE = '1'; $env:DUXEL_VK_PROFILE_EVERY = '64'; $env:DUXEL_VK_GPU_PROFILE = '1'
    $env:DUXEL_APP_PROFILE = '0'; $env:VK_INSTANCE_LAYERS = $null
    for ($run = 1; $run -le $Runs; $run++) {
        $variants = @(foreach ($mode in $Modes) { "net10-$mode"; "net11-$mode" })
        $reverse = $run % 2 -eq 0
        if ($reverse) { [Array]::Reverse($variants) }
        foreach ($variant in $variants) {
            Assert-RuntimeInputs
            Assert-NoRuntimeProcess
            $major = if ($variant.StartsWith('net10-')) { 10 } else { 11 }
            $aot = $variant.EndsWith('-aot')
            $prefix = Join-Path $output "$variant-$run"
            [IO.File]::WriteAllText((Join-Path $output 'current-case.txt'), "$variant-$run")
            $env:DUXEL_3D_BENCH_OUT = "$prefix.json"
            $env:DUXEL_3D_BENCH_REVERSE = if ($reverse) { '1' } else { '0' }
            $env:DUXEL_VK_PROFILE_OUT = "$prefix.vk.log"
            if ($aot) {
                $image = if ($major -eq 10) { $native10 } else { $native11 }
                # This is the interactive graphics sample, not a background helper.
                # Hidden startup suppresses its first ShowWindow and changes present cost.
                $process = Start-Process -FilePath $image -WorkingDirectory (Split-Path $image -Parent) -PassThru -WindowStyle Normal -RedirectStandardOutput "$prefix.child.stdout.log" -RedirectStandardError "$prefix.child.stderr.log"
                try {
                    if (-not $process.WaitForExit(90000)) { $process.Kill(); throw 'RUNTIME_NATIVE_TIMEOUT' }
                    if ($process.ExitCode -ne 0) { throw "RUNTIME_NATIVE_EXIT: $($process.ExitCode)" }
                } finally { $process.Dispose() }
            }
            else {
                $sdkDir = if ($major -eq 10) { 'tests/runtime-net10-current' } else { 'tests/runtime-net11' }
                Push-Location -LiteralPath (Join-Path $repo $sdkDir)
                try {
                    $parameters = @{Path=$sample;DotnetPath=$dotnet;TargetFramework="net$major.0-windows7.0";Managed=$true;NoCache=$true
                        ManagedTimeoutSeconds=150;ManagedLogPrefix="$prefix.child";KillProcessTreeOnTimeout=$true}
                    & (Join-Path $repo 'run-fba.ps1') @parameters 1> "$prefix.runner.stdout.log" 2> "$prefix.runner.stderr.log"
                } finally { Pop-Location }
            }
            Assert-RuntimeInputs
            Assert-NoRuntimeProcess
            if ($observer.State -ne 'Running') { throw 'RUNTIME_WINDOW_OBSERVER_FAILED' }
            $windowSamples = @(Import-Csv -LiteralPath (Join-Path $output 'window-samples.csv') |
                Where-Object { $_.case -eq "$variant-$run" -and $_.visible -eq '1' })
            if ($windowSamples.Count -eq 0) { throw 'RUNTIME_VISIBLE_WINDOW_REQUIRED' }
            $result = Get-Content -LiteralPath "$prefix.json" -Raw | ConvertFrom-Json
            $framework = if ($major -eq 10) { $Net10Framework } else { $Net11Framework }
            Test-RuntimeResult $prefix $result $aot $framework $reverse
            if ((Get-Item -LiteralPath "$prefix.child.stderr.log").Length -ne 0) { throw 'RUNTIME_CHILD_STDERR' }
            $gpu = @(Read-RuntimeGpuPhases "$prefix.vk.log")
            $results.Add([pscustomobject]@{variant=$variant;run=$run;sdk=$sdkVersions[$major.ToString()];phaseOrder=if($reverse){'reverse'}else{'normal'}
                resultPath="$prefix.json";result=$result;gpu=$gpu;childExit=0;visibleWindowSamples=$windowSamples.Count})
            [pscustomobject]@{state='running';completed=$results.Count;total=$total;results=$results.ToArray()} |
                ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $output 'results.json') -Encoding utf8
            Write-Host "[Runtime] $($results.Count)/$total $variant run$run passed raw/GPU audit"
        }
    }
}
catch {
    [pscustomobject]@{state='failed';completed=$results.Count;total=$total;failure=$_.Exception.Message;results=$results.ToArray()} |
        ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $output 'results.json') -Encoding utf8
    throw
}
finally {
    try {
        [IO.File]::WriteAllText((Join-Path $output 'window-observer.stop'), 'stop')
        if ($null -eq (Wait-Job -Job $observer -Timeout 10)) { Stop-Job -Job $observer }
        $observerState = $observer.State
        try { Receive-Job -Job $observer -ErrorAction Stop | Out-Null }
        finally { Remove-Job -Job $observer }
        if ($observerState -ne 'Completed') { throw 'RUNTIME_WINDOW_OBSERVER_SHUTDOWN' }
    }
    finally {
        foreach ($name in $saved.Keys) { [Environment]::SetEnvironmentVariable($name, $saved[$name], 'Process') }
    }
}
[pscustomobject]@{state='complete-audited';completed=$results.Count;total=$total;phaseResults=$results.Count*6;rawFrameFiles=$results.Count*6
    gpuPhaseResults=$results.Count*6;sourceStable=$true;childExit0=$results.Count;childProcessesRemaining=0;windowObserverState=$observerState;results=$results.ToArray()} |
    ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $output 'results.json') -Encoding utf8
