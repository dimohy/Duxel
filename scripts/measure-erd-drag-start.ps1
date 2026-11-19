param(
    [string]$Executable,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [ValidateRange(12,5000)][int]$Tables = 1200,
    [ValidateRange(0,2000)][int]$ZoomSettleMilliseconds = 100,
    [ValidateRange(3,10)][int]$DragSeconds = 6,
    [switch]$SummarizeOnly
)
$ErrorActionPreference = 'Stop'
function Get-DragPercentile([double[]]$Values,[double]$Percentile) {
    if ($Values.Length -eq 0) { throw 'Missing drag timing samples.' }
    [Array]::Sort($Values)
    $position=($Values.Length-1)*$Percentile
    $lower=[int][Math]::Floor($position); $upper=[int][Math]::Ceiling($position)
    return $Values[$lower]+($Values[$upper]-$Values[$lower])*($position-$lower)
}
function Write-DragStageSummary([string]$Directory) {
    $scenario=Get-Content -LiteralPath (Join-Path $Directory 'scenario.json') -Raw|ConvertFrom-Json
    if (-not $scenario.validated) { throw 'Cannot summarize an unvalidated input scenario.' }
    $markers=$scenario.markers
    $down=$markers|Where-Object phase -eq down
    $split=$markers|Where-Object phase -eq two-seconds
    $up=$markers|Where-Object phase -eq up
    $result=Get-Content -LiteralPath (Join-Path $Directory 'result.json') -Raw|ConvertFrom-Json
    $frameEvidence='external marker boundaries; legacy executable has no drag counters'
    if ($null -ne $result.drag) {
        $down.appLines = $result.drag.startFrame - 1; $down.vkLines=$down.appLines
        $split.appLines=$result.drag.twoSecondsFrame; $split.vkLines=$split.appLines
        $up.appLines=$result.drag.lastFrame; $up.vkLines=$up.appLines
        $frameEvidence='sample-consumed drag frame IDs; Vulkan frame indices may differ by swapchain recreation'
    }
    $rows=@(Get-Content -LiteralPath (Join-Path $Directory 'app.log')|ForEach-Object {
        if ($_ -match 'frame=(\d+).*render=([\d.]+).*renderer=([\d.]+) total=([\d.]+)') {
            [pscustomobject]@{frame=[int]$Matches[1];render=[double]$Matches[2];renderer=[double]$Matches[3];total=[double]$Matches[4]}
        }
    })
    $vk=Get-Content -LiteralPath (Join-Path $Directory 'vk.log')
    $summary=@(foreach($phase in @(@{name='first-two-seconds';start=$down;end=$split},@{name='later-drag';start=$split;end=$up})) {
        $subset=@($rows|Where-Object {$_.frame -gt $phase.start.appLines -and $_.frame -le $phase.end.appLines})
        $timings=@(foreach($metric in @('render','renderer','total')) {
            $values=[double[]]@($subset.$metric)
            [pscustomobject]@{metric=$metric;samples=$values.Count;medianUs=(Get-DragPercentile $values .5);
                p95Us=(Get-DragPercentile $values .95);p99Us=(Get-DragPercentile $values .99);maxUs=($values|Measure-Object -Maximum).Maximum;
                over16ms=@($values|Where-Object {$_ -gt 16000}).Count;over33ms=@($values|Where-Object {$_ -gt 33000}).Count}
        })
        $uploads=[long[]]@($vk|Select-Object -Skip $phase.start.vkLines -First ($phase.end.vkLines-$phase.start.vkLines)|ForEach-Object {
            if ($_ -match 'indexUpload=(\d+)') { [long]$Matches[1] }
        })
        if ($uploads.Count -eq 0) { throw 'Missing Vulkan drag upload samples.' }
        [pscustomobject]@{phase=$phase.name;wallSeconds=$phase.end.seconds-$phase.start.seconds;timings=$timings;
            compiledIndexUploadTotalBytes=($uploads|Measure-Object -Sum).Sum;compiledIndexUploadMaxFrameBytes=($uploads|Measure-Object -Maximum).Maximum}
    })
    [pscustomobject]@{kind='instrumented per-frame stage timings, excluding idle waits; not isolated FPS evidence';
        markerPrecision=$frameEvidence;phases=$summary}|
        ConvertTo-Json -Depth 7|Set-Content -LiteralPath (Join-Path $Directory 'stage-summary.json')
}
if ($SummarizeOnly) { Write-DragStageSummary ([IO.Path]::GetFullPath($OutputDirectory)); return }
if (-not $Executable) { throw '-Executable is required for an input run.' }
$exe = (Resolve-Path -LiteralPath $Executable).ProviderPath
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw 'Preserve existing drag evidence; choose a new output directory.' }
if (Get-Process -Name erd_layer_lab -ErrorAction SilentlyContinue) { throw 'DUXEL-ERD-CONCURRENT-PROCESS: preserve the running demo.' }
if (-not ('DuxelErdDragInput' -as [type])) {
    Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class DuxelErdDragInput {
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr h, ref POINT p);
    [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h, out RECT r);
    public struct POINT { public int X, Y; }
    public struct RECT { public int L, T, R, B; }
}
'@
}
New-Item -ItemType Directory -Path $output | Out-Null
$names = @('DUXEL_ERD_TABLES','DUXEL_ERD_SCENARIO','DUXEL_ERD_MODE','DUXEL_SAMPLE_AUTO_EXIT_SECONDS','DUXEL_ERD_OUT',
    'DUXEL_APP_PROFILE','DUXEL_APP_PROFILE_EVERY','DUXEL_APP_PROFILE_OUT','DUXEL_VK_PROFILE_EVERY','DUXEL_VK_PROFILE_OUT')
$saved = @{}
foreach ($name in $names) { $saved[$name] = [Environment]::GetEnvironmentVariable($name,'Process') }
$markers = [Collections.Generic.List[object]]::new()
$clock = [Diagnostics.Stopwatch]::StartNew()
$process = $null
$hash = (Get-FileHash -LiteralPath $exe).Hash
function Get-CompletedLogLines([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) { return 0 }
    # A diagnostic reader must allow the renderer's concurrent append writer.
    $stream = [IO.FileStream]::new($Path,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::ReadWrite)
    $reader = [IO.StreamReader]::new($stream)
    try { return ($reader.ReadToEnd().Split("`n").Length - 1) }
    finally { $reader.Dispose() }
}
function Mark-Drag([string]$Phase) {
    $markers.Add([pscustomobject]@{phase=$Phase;seconds=$clock.Elapsed.TotalSeconds;
        appLines=(Get-CompletedLogLines $env:DUXEL_APP_PROFILE_OUT);vkLines=(Get-CompletedLogLines $env:DUXEL_VK_PROFILE_OUT)})
}
function Send-DragMouse([uint32]$Message,[int]$X,[int]$Y,[int]$Flags=0) {
    if ($process.HasExited) { throw 'Owned drag demo exited before the input scenario completed.' }
    # Queued input wakes the application's GetMessage/idle loop. SendMessage can
    # run inside that wait without publishing a new input snapshot to rendering.
    if (-not [DuxelErdDragInput]::PostMessage($window,$Message,[IntPtr]$Flags,[IntPtr](($Y -shl 16) -bor $X))) {
        throw 'Cannot queue pointer input to the owned test window.'
    }
}
function Click-DragControl([int]$X,[int]$Y) {
    Send-DragMouse 0x200 $X $Y
    Send-DragMouse 0x201 $X $Y 1
    Start-Sleep -Milliseconds 90
    Send-DragMouse 0x202 $X $Y
    Start-Sleep -Milliseconds 180
}
try {
    $env:DUXEL_ERD_TABLES = "$Tables"
    $env:DUXEL_ERD_SCENARIO = 'interactive'
    $env:DUXEL_ERD_MODE = 'resident'
    $env:DUXEL_SAMPLE_AUTO_EXIT_SECONDS = "$($DragSeconds + 12)"
    $env:DUXEL_ERD_OUT = Join-Path $output 'result.json'
    $env:DUXEL_APP_PROFILE = '1'; $env:DUXEL_APP_PROFILE_EVERY = '1'
    $env:DUXEL_APP_PROFILE_OUT = Join-Path $output 'app.log'
    $env:DUXEL_VK_PROFILE_EVERY = '1'; $env:DUXEL_VK_PROFILE_OUT = Join-Path $output 'vk.log'
    $process = Start-Process -FilePath $exe -WorkingDirectory (Split-Path $exe -Parent) -PassThru `
        -RedirectStandardOutput (Join-Path $output 'stdout.log') -RedirectStandardError (Join-Path $output 'stderr.log')
    Start-Sleep -Seconds 3
    $process.Refresh(); $window = $process.MainWindowHandle
    $client = New-Object DuxelErdDragInput+RECT
    if (-not [DuxelErdDragInput]::GetClientRect($window,[ref]$client) -or $client.R -ne 1440 -or $client.B -ne 940) {
        throw 'DUXEL-ERD-DRAG-GEOMETRY: this input fixture requires the unresized 1440x940 sample window.'
    }
    Click-DragControl 80 230
    Click-DragControl 80 315
    Click-DragControl 80 360
    Start-Sleep -Milliseconds 1000
    $columns = if ($Tables -eq 12) { 4 } else { [int][Math]::Ceiling([Math]::Sqrt($Tables * 1.4)) }
    $rows = [int][Math]::Ceiling($Tables / [double]$columns)
    $entity = [Math]::Min($Tables - 1, [int][Math]::Floor($rows / 2) * $columns + [int][Math]::Floor($columns / 2))
    $worldWidth = $columns * 340 - 80; $worldHeight = $rows * 270 - 68
    $zoom = [Math]::Min(1150 / $worldWidth, 816 / $worldHeight) * .9
    $anchorX = [int](849 + ($entity % $columns * 340 - $worldWidth / 2 + 130) * $zoom)
    $anchorY = [int](512 + ([int][Math]::Floor($entity / $columns) * 270 - $worldHeight / 2 + 101) * $zoom)
    $point = New-Object DuxelErdDragInput+POINT; $point.X=$anchorX; $point.Y=$anchorY
    if (-not [DuxelErdDragInput]::ClientToScreen($window,[ref]$point)) { throw 'Cannot map the owned window pointer.' }
    Send-DragMouse 0x200 $anchorX $anchorY
    Mark-Drag 'wheel'
    if (-not [DuxelErdDragInput]::PostMessage($window,0x20a,[IntPtr](2880 -shl 16),[IntPtr](($point.Y -shl 16) -bor $point.X))) { throw 'Cannot queue wheel input to the owned test window.' }
    Start-Sleep -Milliseconds $ZoomSettleMilliseconds
    Mark-Drag 'down'
    Send-DragMouse 0x201 $anchorX $anchorY 1
    $dragClock = [Diagnostics.Stopwatch]::StartNew(); $markedTwoSeconds=$false
    while ($dragClock.Elapsed.TotalSeconds -lt $DragSeconds) {
        $phase = $dragClock.Elapsed.TotalSeconds * [Math]::PI * 2 / $DragSeconds
        Send-DragMouse 0x200 ($anchorX + [int](100 * [Math]::Sin($phase))) ($anchorY + [int](50 * [Math]::Sin(2*$phase))) 1
        if (-not $markedTwoSeconds -and $dragClock.Elapsed.TotalSeconds -ge 2) { Mark-Drag 'two-seconds'; $markedTwoSeconds=$true }
        Start-Sleep -Milliseconds 33
    }
    Send-DragMouse 0x202 $anchorX $anchorY
    Mark-Drag 'up'
    # Finish this owned scenario before later desktop interaction can replace its
    # selected entity or diagnostic gesture. Auto-exit remains a timeout guard.
    Start-Sleep -Milliseconds 250
    if (-not $process.HasExited) { $process.CloseMainWindow() | Out-Null }
    if (-not $process.WaitForExit(($DragSeconds+120)*1000)) { throw 'Owned drag demo timed out.' }
    if ($process.ExitCode -ne 0) { throw "Owned drag demo failed with $($process.ExitCode)." }
    if ((Get-Content -LiteralPath (Join-Path $output 'stderr.log') -Raw) -match 'RenderThreadException:') { throw 'Drag renderer failed; preserve diagnostic logs.' }
    if ((Get-FileHash -LiteralPath $exe).Hash -ne $hash) { throw 'Drag executable changed during the scenario.' }
    $result = Get-Content -LiteralPath (Join-Path $output 'result.json') -Raw | ConvertFrom-Json
    if ($result.selection.entity -ne $entity -or $result.maximumZoom -lt 1.5) { throw 'Drag scenario did not select the intended zoomed entity.' }
    if ($null -ne $result.drag) {
        $down=$markers|Where-Object phase -eq down; $up=$markers|Where-Object phase -eq up
        if ($result.drag.entity -ne $entity -or $result.drag.motionFrames -lt 10 -or $result.drag.worldDistance -lt 1 -or
            $result.drag.startFrame -lt $down.appLines -or $result.drag.startFrame -gt $up.appLines -or
            ($result.drag.lastTime - $result.drag.startTime) -lt ($DragSeconds * .7)) {
            throw 'The sample did not consume a sustained drag during the intended input interval.'
        }
    }
    [pscustomobject]@{kind='instrumented interactive diagnostic; not isolated FPS evidence';executable=$exe;sha256=$hash;expectedEntity=$entity;
        input='queued window messages; no global pointer or focus manipulation';zoomSettleMilliseconds=$ZoomSettleMilliseconds;dragSeconds=$DragSeconds;markers=$markers.ToArray();validated=$true} |
        ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $output 'scenario.json')
    Write-DragStageSummary $output
    $markers | Format-Table -AutoSize
}
finally {
    $markers | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'markers.json')
    if ($null -ne $process -and -not $process.HasExited) {
        $process.CloseMainWindow() | Out-Null
        if (-not $process.WaitForExit(10000)) { $process.Kill(); $process.WaitForExit() }
    }
    if ($null -ne $process) { $process.Dispose() }
    foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name,$saved[$name],'Process') }
}
