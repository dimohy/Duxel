param(
    [Parameter(Mandatory)][string]$Executable,
    [Parameter(Mandatory)][ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$ExpectedSha256,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [ValidateSet('motion-tour','idle')][string]$Scenario = 'motion-tour',
    [ValidateRange(30,1800)][int]$Seconds = 180,
    [ValidateRange(10,60)][int]$WarmupSeconds = 30,
    [ValidateRange(12,5000)][int]$Tables = 1200,
    [switch]$IncludeGcMetrics,
    [switch]$IncludeVirtualMemory,
    [switch]$IncludePrivateAllocations,
    [Nullable[int]]$MaximizeAfterSeconds,
    [ValidatePattern('^[1-9][0-9]*x[1-9][0-9]*$')][string]$ExpectedVulkanExtent,
    [switch]$IncludeMappedAllocations
)
$ErrorActionPreference = 'Stop'
if ($ExpectedVulkanExtent -and $PSBoundParameters.ContainsKey('MaximizeAfterSeconds')) {
    throw 'ERD_RESOURCE_FIXED_EXTENT_CONFLICTS_WITH_MAXIMIZE'
}
$exe = (Resolve-Path -LiteralPath $Executable).ProviderPath
$output = [IO.Path]::GetFullPath($OutputDirectory)
if ((Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash -ne $ExpectedSha256) { throw 'ERD_RESOURCE_IMAGE_MISMATCH' }
if (Test-Path -LiteralPath $output) { throw 'ERD_RESOURCE_OUTPUT_EXISTS' }
if (@(Get-Process -Name ([IO.Path]::GetFileNameWithoutExtension($exe)) -ErrorAction SilentlyContinue).Count -ne 0) {
    throw 'ERD_RESOURCE_CONCURRENT_PROCESS'
}
$observeVirtualMemory = $IncludeVirtualMemory -or $IncludePrivateAllocations -or $IncludeMappedAllocations
if ($PSBoundParameters.ContainsKey('MaximizeAfterSeconds') -and
    ($null -eq $MaximizeAfterSeconds -or $MaximizeAfterSeconds -lt 1 -or $MaximizeAfterSeconds -gt $Seconds - 5 -or $Scenario -ne 'motion-tour')) {
    throw 'ERD_RESOURCE_MAXIMIZE_OUTSIDE_MOTION_OBSERVATION'
}
if ($observeVirtualMemory -or $null -ne $MaximizeAfterSeconds) {
    if (!$IsWindows -or [Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture -ne 'X64') {
        throw 'ERD_RESOURCE_VIRTUAL_MEMORY_REQUIRES_WINDOWS_X64'
    }
    $virtualMemorySource = Join-Path $PSScriptRoot 'ErdVirtualMemorySnapshot.cs'
    Add-Type -LiteralPath $virtualMemorySource
}
[IO.Directory]::CreateDirectory($output) | Out-Null
$settings = [ordered]@{
    DUXEL_ERD_SCENARIO=$Scenario; DUXEL_ERD_TABLES="$Tables"; DUXEL_ERD_VIEW='dense'; DUXEL_ERD_MODE='resident'
    DUXEL_ERD_OUT=''; DUXEL_ERD_WORKLOAD_CLOCK='wall-total'; DUXEL_ERD_PIPELINE='1'; DUXEL_ERD_EVENT_LOOP='1'
    DUXEL_VK_COMPILED_LAYERS='1'; DUXEL_VK_SHARED_LAYER_BASIS='1'; DUXEL_VK_SHARED_LAYER_CAMERA='1'
    DUXEL_VK_DESCRIPTOR_REUSE='1'; DUXEL_VK_COMMAND_SPANS='1'; DUXEL_VK_LAYER_BOUNDS_REUSE='1'
    DUXEL_VK_AXIS_ALIGNED_CULLING='1'; DUXEL_VK_COMPILED_INDEX_LIMIT='2097152'
    DUXEL_APP_PROFILE='1'; DUXEL_APP_PROFILE_EVERY=$(if ($Scenario -eq 'idle') {'1'} else {'1000'})
    DUXEL_APP_PROFILE_MEMORY=$(if ($IncludeGcMetrics) {'1'} else {'0'})
    DUXEL_APP_PROFILE_OUT=(Join-Path $output 'app.log'); DUXEL_VK_PROFILE_EVERY='10000'
    DUXEL_VK_PROFILE_TIMING_SAMPLING='1'; DUXEL_VK_PROFILE_OUT=(Join-Path $output 'vk.log')
    DUXEL_SAMPLE_AUTO_EXIT_SECONDS="$($Seconds + $WarmupSeconds + 15)"
}
if ($ExpectedVulkanExtent) { $settings['DUXEL_VK_PROFILE'] = '1' }
$saved = @{}
foreach ($name in $settings.Keys) { $saved[$name] = [Environment]::GetEnvironmentVariable($name,'Process') }
$rows = [Collections.Generic.List[object]]::new()
$virtualMemoryRows = [Collections.Generic.List[object]]::new()
$process = $null
$preserveObservedWindow = $false
$result = [ordered]@{
    state='running'; scope='live no-output process resource observation;not isolated FPS or VRAM'
    executable=$exe; sha256=$ExpectedSha256; scenario=$Scenario; tables=$Tables
    requestedObservationSeconds=$Seconds; warmupSeconds=$WarmupSeconds; settings=$settings
    processId=$null; exitCode=$null; termination='not-started'; failure=$null
    observerSourceSha256=(Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash
    emergencyKill=$false; at=[DateTimeOffset]::Now.ToString('o')
}
if ($observeVirtualMemory -or $null -ne $MaximizeAfterSeconds) {
    $result.virtualMemorySourceSha256 = (Get-FileHash -LiteralPath $virtualMemorySource).Hash
}
$result.maximizeAfterSeconds = $MaximizeAfterSeconds
$result.capturePrivateAllocations = [bool]$IncludePrivateAllocations
if ($IncludeMappedAllocations) { $result.captureMappedAllocations = $true }
if ($ExpectedVulkanExtent) { $result.expectedVulkanExtent = $ExpectedVulkanExtent }
function Get-ErDCompletedProfileLines([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { return @() }
    $stream = [IO.FileStream]::new($Path,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::ReadWrite)
    $reader = [IO.StreamReader]::new($stream)
    try {
        $text = $reader.ReadToEnd()
        $end = $text.LastIndexOf("`n")
        if ($end -lt 0) { return @() }
        return $text.Substring(0,$end).Split("`n")
    }
    finally { $reader.Dispose() }
}
function Get-LoggedFrame([string]$Path) {
    $last = $null
    foreach ($line in Get-ErDCompletedProfileLines $Path) {
        if ($line -match '\bframe=(\d+)') { $last = [long]$Matches[1] }
    }
    return $last
}
function Read-ErDVulkanSurface([string]$Path) {
    $surfaceEvents = @(Get-ErDCompletedProfileLines $Path | Where-Object { $_ -match '^\[duxel-vk-(wsi|resize)\]' })
    $extents = @(foreach ($line in $surfaceEvents) {
        if ($line -match '^\[duxel-vk-wsi\].*\bextent=(\d+x\d+)\s*$') { $Matches[1] }
    })
    return [ordered]@{ extents = $extents; events = $surfaceEvents }
}
function Get-ResourceMedian([object[]]$Items,[string]$Property) {
    $values = [double[]]@($Items | ForEach-Object { $_.$Property })
    [Array]::Sort($values)
    $middle = [int][Math]::Floor($values.Length / 2)
    if ($values.Length % 2 -eq 1) { return $values[$middle] }
    return ($values[$middle-1] + $values[$middle]) / 2
}
function Read-ErDGcSnapshots([string]$Path,[long]$FirstFrame,[long]$LastFrame) {
    $records = @(foreach ($line in Get-ErDCompletedProfileLines $Path) {
        if ($line -match '\bframe=(\d+)') {
            $frame = [long]$Matches[1]
            if ($frame -lt $FirstFrame -or $frame -gt $LastFrame) { continue }
            $record = [ordered]@{frame=$frame}
            foreach ($field in @('gcHeap','gcCommitted','gcLastHeap','gcGen0','gcGen1','gcGen2','gcAllocated')) {
                if ($line -notmatch "\b$field=(\d+)") { throw 'ERD_RESOURCE_GC_METRICS_MISSING' }
                $record[$field] = [long]$Matches[1]
            }
            [pscustomobject]$record
        }
    })
    if ($records.Count -eq 0) { throw 'ERD_RESOURCE_GC_METRICS_MISSING' }
    return $records
}
try {
    $result | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $output 'input.json') -Encoding utf8
    foreach ($name in $settings.Keys) { [Environment]::SetEnvironmentVariable($name,$settings[$name],'Process') }
    # The GUI stays visible to exercise its live presentation path. No focus,
    # pointer or other application's window is manipulated by this observer.
    $process = Start-Process -FilePath $exe -WorkingDirectory (Split-Path $exe -Parent) -PassThru `
        -RedirectStandardOutput (Join-Path $output 'stdout.log') -RedirectStandardError (Join-Path $output 'stderr.log')
    $result.processId = $process.Id
    $result.termination = 'running'
    [ordered]@{processId=$process.Id;startTimeUtc=$process.StartTime.ToUniversalTime().ToString('o');sha256=$ExpectedSha256} |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'process.json') -Encoding utf8
    $clock = [Diagnostics.Stopwatch]::StartNew()
    $nextVirtualMemory = $WarmupSeconds
    $ownedWindow = [IntPtr]::Zero
    $gcCapabilityConfirmed = $false
    do {
        $process.Refresh()
        if ($process.HasExited) { throw 'ERD_RESOURCE_EARLY_EXIT' }
        $elapsed = $clock.Elapsed.TotalSeconds
        $row = [pscustomobject]@{
            seconds=$elapsed; steady=($elapsed -ge $WarmupSeconds)
            privateBytes=$process.PrivateMemorySize64; workingSetBytes=$process.WorkingSet64
            peakWorkingSetBytes=$process.PeakWorkingSet64; cpuMilliseconds=$process.TotalProcessorTime.TotalMilliseconds
            handles=$process.HandleCount; threads=$process.Threads.Count
            loggedFrame=(Get-LoggedFrame $settings.DUXEL_APP_PROFILE_OUT)
        }
        $rows.Add($row)
        $row | Export-Csv -LiteralPath (Join-Path $output 'samples.csv') -NoTypeInformation -Append
        $allocationTail = $IncludePrivateAllocations -and $elapsed -ge $WarmupSeconds + $Seconds - 15
        if ($observeVirtualMemory -and ($allocationTail -or $elapsed -ge $nextVirtualMemory -or $elapsed -ge $WarmupSeconds + $Seconds)) {
            $queryStarted = $clock.Elapsed.TotalSeconds
            $snapshot = [Duxel.Diagnostics.ErdVirtualMemorySnapshot]::Read($process.Handle, $IncludePrivateAllocations, $IncludeMappedAllocations)
            $virtualRow = [pscustomobject]@{
                processSampleSeconds=$elapsed; queryStartedSeconds=$queryStarted; loggedFrame=$row.loggedFrame
                privateBytes=$row.privateBytes; privateCommitBytes=$snapshot.PrivateCommitBytes
                mappedCommitBytes=$snapshot.MappedCommitBytes; imageCommitBytes=$snapshot.ImageCommitBytes
                reservedBytes=$snapshot.ReservedBytes; regions=$snapshot.Regions; queryMilliseconds=$snapshot.QueryMilliseconds
            }
            $virtualMemoryRows.Add($virtualRow)
            $virtualRow | Export-Csv -LiteralPath (Join-Path $output 'virtual-memory.csv') -NoTypeInformation -Append
            if ($elapsed -ge $nextVirtualMemory) { $nextVirtualMemory += 10 }
            if ($IncludePrivateAllocations) {
                [ordered]@{processSampleSeconds=$elapsed;queryStartedSeconds=$queryStarted;loggedFrame=$row.loggedFrame;allocations=$snapshot.PrivateAllocations} |
                    ConvertTo-Json -Compress -Depth 4 | Add-Content -LiteralPath (Join-Path $output 'private-allocations.jsonl') -Encoding utf8
            }
            if ($IncludeMappedAllocations) {
                [ordered]@{processSampleSeconds=$elapsed;queryStartedSeconds=$queryStarted;loggedFrame=$row.loggedFrame;mappings=$snapshot.MappedAllocations} |
                    ConvertTo-Json -Compress -Depth 4 | Add-Content -LiteralPath (Join-Path $output 'mapped-allocations.jsonl') -Encoding utf8
            }
        }
        if ($null -ne $MaximizeAfterSeconds -and $ownedWindow -eq [IntPtr]::Zero -and $elapsed -ge $WarmupSeconds + $MaximizeAfterSeconds) {
            $ownedWindow = $process.MainWindowHandle
            if ($ownedWindow -eq [IntPtr]::Zero) {
                throw 'ERD_RESOURCE_MAXIMIZE_WINDOW_BASELINE_INVALID'
            }
            if ([Duxel.Diagnostics.ErdObservedWindow]::IsMaximized($ownedWindow)) {
                $preserveObservedWindow = $true
                $result.termination = 'left-open-after-external-maximize'
                throw 'ERD_RESOURCE_WINDOW_ALREADY_MAXIMIZED'
            }
            $result.maximizeRequestedAtSeconds = $clock.Elapsed.TotalSeconds
            if (![Duxel.Diagnostics.ErdObservedWindow]::RequestMaximize($ownedWindow)) { throw 'ERD_RESOURCE_MAXIMIZE_REQUEST_FAILED' }
        }
        if ($ownedWindow -ne [IntPtr]::Zero -and !$result.Contains('maximizeObservedAtSeconds') -and [Duxel.Diagnostics.ErdObservedWindow]::IsMaximized($ownedWindow)) {
            $result.maximizeObservedAtSeconds = $clock.Elapsed.TotalSeconds
        }
        if ($IncludeGcMetrics -and -not $gcCapabilityConfirmed -and $null -ne $row.loggedFrame) {
            # Probe the first emitted profile before spending the observation
            # interval on an executable that lacks the requested diagnostics.
            [void]@(Read-ErDGcSnapshots $settings.DUXEL_APP_PROFILE_OUT 0 $row.loggedFrame)
            $gcCapabilityConfirmed = $true
        }
        if ($elapsed -ge $WarmupSeconds + $Seconds) { break }
        # Schedule against the monotonic clock, rather than adding the work of
        # each sample to every subsequent interval.
        $next = [Math]::Floor($clock.Elapsed.TotalSeconds) + 1
        $delay = [int][Math]::Ceiling(($next - $clock.Elapsed.TotalSeconds) * 1000)
        if ($delay -gt 0) { Start-Sleep -Milliseconds $delay }
    } while ($true)
    $steady = @($rows | Where-Object steady)
    if ($steady.Count -lt 2 -or $null -eq $steady[0].loggedFrame -or $steady[0].loggedFrame -le 0) {
        throw 'ERD_RESOURCE_OBSERVATION_INCOMPLETE'
    }
    $span = $steady[-1].seconds - $steady[0].seconds
    if ($span -lt $Seconds - 2) { throw 'ERD_RESOURCE_OBSERVATION_TOO_SHORT' }
    $window = [Math]::Max(1,[int][Math]::Floor($steady.Count / 3))
    $first = @($steady | Select-Object -First $window)
    $last = @($steady | Select-Object -Last $window)
    $result.observedSeconds = $span
    $result.samples = $rows.Count
    $result.steadySamples = $steady.Count
    $result.firstPrivateMedianBytes = Get-ResourceMedian $first 'privateBytes'
    $result.lastPrivateMedianBytes = Get-ResourceMedian $last 'privateBytes'
    $result.privateMedianChangeBytes = $result.lastPrivateMedianBytes - $result.firstPrivateMedianBytes
    $result.firstWorkingSetMedianBytes = Get-ResourceMedian $first 'workingSetBytes'
    $result.lastWorkingSetMedianBytes = Get-ResourceMedian $last 'workingSetBytes'
    $result.workingSetMedianChangeBytes = $result.lastWorkingSetMedianBytes - $result.firstWorkingSetMedianBytes
    $result.privateMinimumBytes = ($steady.privateBytes | Measure-Object -Minimum).Minimum
    $result.privateMaximumBytes = ($steady.privateBytes | Measure-Object -Maximum).Maximum
    $result.handleMinimum = ($steady.handles | Measure-Object -Minimum).Minimum
    $result.handleMaximum = ($steady.handles | Measure-Object -Maximum).Maximum
    $result.threadMinimum = ($steady.threads | Measure-Object -Minimum).Minimum
    $result.threadMaximum = ($steady.threads | Measure-Object -Maximum).Maximum
    $result.observedCpuMilliseconds = $steady[-1].cpuMilliseconds - $steady[0].cpuMilliseconds
    $result.averageBusyCpuCores = $result.observedCpuMilliseconds / ($span * 1000)
    $result.loggedFrameDelta = $steady[-1].loggedFrame - $steady[0].loggedFrame
    $maximumGap = 0.0
    for ($i = 1; $i -lt $steady.Count; $i++) { $maximumGap = [Math]::Max($maximumGap,$steady[$i].seconds - $steady[$i-1].seconds) }
    $result.maximumSampleGapSeconds = $maximumGap
    if ($maximumGap -gt 2.5) { throw 'ERD_RESOURCE_SAMPLE_GAP' }
    if ($Scenario -eq 'motion-tour' -and $result.loggedFrameDelta -le 0) { throw 'ERD_RESOURCE_NO_ACTIVE_FRAMES' }
    if ($Scenario -eq 'idle' -and $result.loggedFrameDelta -ne 0) { throw 'ERD_RESOURCE_IDLE_FRAMES_CONTINUE' }
    if ($null -ne $MaximizeAfterSeconds -and !$result.Contains('maximizeObservedAtSeconds')) { throw 'ERD_RESOURCE_MAXIMIZE_UNCONFIRMED' }
    if ($observeVirtualMemory) {
        if ($virtualMemoryRows.Count -lt 2) { throw 'ERD_RESOURCE_VIRTUAL_MEMORY_INCOMPLETE' }
        $result.virtualMemory = [ordered]@{
            samples=$virtualMemoryRows.Count
            first=$virtualMemoryRows[0]
            last=$virtualMemoryRows[-1]
            scope='non-atomic Windows committed virtual regions by type; not working set, heap ownership or VRAM'
        }
    }
    if (-not $process.CloseMainWindow()) { throw 'ERD_RESOURCE_CLOSE_REQUEST_FAILED' }
    if (-not $process.WaitForExit(30000)) { throw 'ERD_RESOURCE_CLOSE_TIMEOUT' }
    $result.exitCode = $process.ExitCode
    $result.termination = 'normal-window-close'
    if ($process.ExitCode -ne 0) { throw 'ERD_RESOURCE_NONZERO_EXIT' }
    if ($IncludeGcMetrics) {
        $gcRows = @(Read-ErDGcSnapshots $settings.DUXEL_APP_PROFILE_OUT $steady[0].loggedFrame $steady[-1].loggedFrame)
        $gcRows | Export-Csv -LiteralPath (Join-Path $output 'gc-samples.csv') -NoTypeInformation
        $result.gc = [ordered]@{samples=$gcRows.Count;first=$gcRows[0];last=$gcRows[-1];scope='heap estimate and last-collection snapshot;no forced GC'}
    }
    if ((Get-Item -LiteralPath (Join-Path $output 'stderr.log')).Length -ne 0) { throw 'ERD_RESOURCE_STDERR' }
    if ((Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash -ne $ExpectedSha256) { throw 'ERD_RESOURCE_INPUT_CHANGED' }
    if ($ExpectedVulkanExtent) {
        $surface = Read-ErDVulkanSurface $settings.DUXEL_VK_PROFILE_OUT
        $result.vulkanSurface = $surface
        if (!$surface.events.Count) { throw 'ERD_RESOURCE_VULKAN_SURFACE_UNOBSERVED' }
        if ($surface.events.Count -ne 1 -or $surface.extents.Count -ne 1 -or $surface.extents[0] -ne $ExpectedVulkanExtent) {
            throw 'ERD_RESOURCE_VULKAN_SURFACE_CHANGED_OR_UNEXPECTED'
        }
    }
    $result.state = 'complete'
}
catch {
    $result.state = 'failed'
    $result.failure = $_.Exception.Message
    throw
}
finally {
    if ($null -ne $process) {
        try {
            if (-not $process.HasExited -and !$preserveObservedWindow) {
                $result.emergencyKill = $true
                $process.Kill()
                if (-not $process.WaitForExit(10000)) { throw 'ERD_RESOURCE_KILL_UNCONFIRMED' }
                $result.termination = 'emergency-kill'
                if ($result.state -eq 'running') { $result.state='canceled' }
            }
            $result.processTerminationConfirmed = $process.HasExited
        }
        catch {
            $result.state = 'failed'
            $result.cleanupFailure = $_.Exception.Message
        }
        finally { $process.Dispose() }
    }
    foreach ($name in $settings.Keys) { [Environment]::SetEnvironmentVariable($name,$saved[$name],'Process') }
    $result | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $output 'result.json') -Encoding utf8
    if ($result.Contains('cleanupFailure')) { throw $result.cleanupFailure }
}
Write-Output "[ERD resources] $Scenario $($result.state): $($result.observedSeconds) seconds,private median change $($result.privateMedianChangeBytes) bytes"
