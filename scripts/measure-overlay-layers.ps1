[CmdletBinding(DefaultParameterSetName='Measure')]
param(
    [Parameter(Mandatory,ParameterSetName='Measure')][string]$BeforeExecutable,
    [Parameter(Mandatory,ParameterSetName='Measure')][string]$AfterExecutable,
    [Parameter(Mandatory)][string]$OutDir,
    [Parameter(Mandatory,ParameterSetName='Summary')][switch]$SummarizeOnly,
    [int]$Runs = 3,
    [double]$Seconds = 4,
    [int[]]$Counts = @(48, 128),
    [ValidateRange(0,512)][int]$Swatches = 64,
    [switch]$VisibleWindow,
    [ValidateSet('idle','motion')][string[]]$Scenarios = @('idle','motion')
)
$ErrorActionPreference = 'Stop'
function Get-OverlayMedian([double[]]$Numbers) {
    $sorted = [double[]]$Numbers.Clone()
    [Array]::Sort($sorted)
    $middle = [int][Math]::Floor($sorted.Length / 2)
    if($sorted.Length % 2 -eq 0){return ($sorted[$middle - 1] + $sorted[$middle]) / 2}
    return $sorted[$middle]
}
function Export-OverlaySummary([string]$Directory) {
    $records = @(Get-Content -LiteralPath (Join-Path $Directory 'runs.json') -Raw | ConvertFrom-Json)
    $audits = [Collections.Generic.List[object]]::new()
    $observations = foreach($record in $records){
        $result = Get-Content -LiteralPath $record.result -Raw | ConvertFrom-Json
        if(-not $result.complete){throw 'OVERLAY_SUMMARY_INCOMPLETE_RESULT'}
        $frames = [Collections.Generic.List[double]]::new()
        foreach($line in [IO.File]::ReadLines($record.result + '.frames.csv')){
            if($line -eq 'frameSeconds'){continue}
            $frames.Add([double]::Parse($line,[Globalization.CultureInfo]::InvariantCulture))
        }
        $sum = 0.0
        foreach($seconds in $frames){if(-not [double]::IsFinite($seconds) -or $seconds -le 0){throw 'OVERLAY_SUMMARY_INVALID_FRAME'};$sum += $seconds}
        if($frames.Count -ne $result.samples -or [Math]::Abs($frames.Count/$sum - $result.averageFps) -gt .000001){throw 'OVERLAY_SUMMARY_FPS_MISMATCH'}
        $sorted = $frames.ToArray();[Array]::Sort($sorted)
        foreach($entry in @(@{field='medianFrameMs';q=.5},@{field='p95FrameMs';q=.95},@{field='p99FrameMs';q=.99})){
            $position=($sorted.Length-1)*$entry.q;$lower=[int][Math]::Floor($position);$upper=[int][Math]::Ceiling($position)
            $value=($sorted[$lower]+($sorted[$upper]-$sorted[$lower])*($position-$lower))*1000
            if([Math]::Abs($value-$result.($entry.field)) -gt .000000001){throw 'OVERLAY_SUMMARY_PERCENTILE_MISMATCH'}
        }
        $slow=[Math]::Max(1,[int][Math]::Ceiling($sorted.Length*.01));$slowSeconds=0.0
        for($index=$sorted.Length-$slow;$index -lt $sorted.Length;$index++){$slowSeconds+=$sorted[$index]}
        if([Math]::Abs($slow/$slowSeconds-$result.low1PctFps) -gt .000001){throw 'OVERLAY_SUMMARY_LOW1_MISMATCH'}
        $audits.Add([pscustomobject]@{result=$record.result;frames=$frames.Count;recordedSeconds=$sum;verified=$true})
        [pscustomobject]@{variant=$record.variant;count=$record.count;scenario=$record.scenario;run=$record.run;result=$result}
    }
    $cases=foreach($group in @($observations|Group-Object count,scenario)){
        $before=@($group.Group|Where-Object variant -EQ 'before');$after=@($group.Group|Where-Object variant -EQ 'after')
        if($before.Count -lt 3 -or $before.Count -ne $after.Count){throw 'OVERLAY_SUMMARY_INCOMPLETE_REPETITIONS'}
        $metrics=[ordered]@{}
        foreach($name in @('averageFps','medianFrameMs','p95FrameMs','p99FrameMs','low1PctFps','drawCalls','dynamicUploadBytes','gpuMicroseconds','uiRenderAllocatedBytes','privateBytes','workingSetBytes','reusedOverlayLists','rebuiltOverlayLists')){
            $left=[double[]]@($before|ForEach-Object {$_.result.$name});$right=[double[]]@($after|ForEach-Object {$_.result.$name})
            $leftMedian=Get-OverlayMedian $left;$rightMedian=Get-OverlayMedian $right
            $leftRange=@(($left|Measure-Object -Minimum).Minimum,($left|Measure-Object -Maximum).Maximum)
            $rightRange=@(($right|Measure-Object -Minimum).Minimum,($right|Measure-Object -Maximum).Maximum)
            $status=if($rightRange[0] -gt $leftRange[1]){'higher-nonoverlapping'}elseif($rightRange[1] -lt $leftRange[0]){'lower-nonoverlapping'}else{'inconclusive-overlapping-ranges'}
            $metrics[$name]=[ordered]@{before=$leftMedian;after=$rightMedian;observedChangePercent=$(if($leftMedian -ne 0){100*($rightMedian-$leftMedian)/$leftMedian}else{$null});beforeRange=$leftRange;afterRange=$rightRange;comparison=$status}
        }
        [pscustomobject]@{count=$group.Group[0].count;scenario=$group.Group[0].scenario;runs=$before.Count;swatchesPerPalette=$before[0].result.swatchesPerPalette;framebuffer="$($before[0].result.framebufferWidth)x$($before[0].result.framebufferHeight)";metrics=$metrics}
    }
    [ordered]@{rawFrameAudit=$audits;cases=@($cases);scope='independent-run medians; overlapping ranges are inconclusive; memory sampled with active window'}|ConvertTo-Json -Depth 10|Set-Content -LiteralPath (Join-Path $Directory 'summary.json')
}
if($SummarizeOnly){Export-OverlaySummary ([IO.Path]::GetFullPath($OutDir));return}
if ($Runs -lt 3 -or $Seconds -le 0 -or $Counts.Count -eq 0) { throw 'OVERLAY_BENCH_INVALID_INPUT' }
$BeforeExecutable = (Resolve-Path -LiteralPath $BeforeExecutable).Path
$AfterExecutable = (Resolve-Path -LiteralPath $AfterExecutable).Path
$OutDir = [IO.Path]::GetFullPath($OutDir)
if (Test-Path -LiteralPath $OutDir) { throw 'OVERLAY_BENCH_OUTPUT_EXISTS' }
[IO.Directory]::CreateDirectory($OutDir) | Out-Null
[ordered]@{counts=$Counts;swatches=$Swatches;scenarios=$Scenarios;runs=$Runs;seconds=$Seconds;visibleWindow=[bool]$VisibleWindow;before=$BeforeExecutable;after=$AfterExecutable}|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $OutDir 'plan.json')
$manifest = [Collections.Generic.List[object]]::new()
$referenceFramebuffer = $null
foreach ($count in $Counts) {
    if ($count -lt 1 -or $count -gt 512) { throw 'OVERLAY_BENCH_INVALID_COUNT' }
    foreach ($scenario in $Scenarios) {
        for ($run = 1; $run -le $Runs; $run++) {
            $order = if ($run % 2 -eq 1) { @('before','after') } else { @('after','before') }
            foreach ($variant in $order) {
                $executable = if ($variant -eq 'before') { $BeforeExecutable } else { $AfterExecutable }
                $stem = Join-Path $OutDir "$variant-$count-$scenario-$run"
                $start = [Diagnostics.ProcessStartInfo]::new($executable)
                $start.WorkingDirectory = [IO.Path]::GetDirectoryName($executable)
                $start.UseShellExecute = $false
                $start.WindowStyle = if ($VisibleWindow) { [Diagnostics.ProcessWindowStyle]::Normal } else { [Diagnostics.ProcessWindowStyle]::Hidden }
                $start.RedirectStandardOutput = $true
                $start.RedirectStandardError = $true
                $start.Environment['DUXEL_OVERLAY_COUNT'] = [string]$count
                $start.Environment['DUXEL_OVERLAY_SWATCHES'] = [string]$Swatches
                $start.Environment['DUXEL_OVERLAY_SCENARIO'] = $scenario
                $start.Environment['DUXEL_OVERLAY_SECONDS'] = $Seconds.ToString('R',[Globalization.CultureInfo]::InvariantCulture)
                $start.Environment['DUXEL_OVERLAY_OUT'] = "$stem.json"
                $process = [Diagnostics.Process]::Start($start)
                $stdout = $process.StandardOutput.ReadToEndAsync()
                $stderr = $process.StandardError.ReadToEndAsync()
                if (-not $process.WaitForExit([int](($Seconds + 35) * 1000))) {
                    $null = $process.CloseMainWindow()
                    if (-not $process.WaitForExit(5000)) { $process.Kill($true); $process.WaitForExit() }
                    throw "OVERLAY_BENCH_TIMEOUT pid=$($process.Id)"
                }
                [IO.File]::WriteAllText("$stem.stdout.log",$stdout.GetAwaiter().GetResult())
                $errorText = $stderr.GetAwaiter().GetResult()
                [IO.File]::WriteAllText("$stem.stderr.log",$errorText)
                if ($process.ExitCode -ne 0) { throw "OVERLAY_BENCH_PROCESS_FAILED exit=$($process.ExitCode) stem=$stem" }
                if ($errorText.Contains('RenderThreadException') -or $errorText.Contains('Unhandled exception')) { throw "OVERLAY_BENCH_RENDER_FAILED stem=$stem" }
                $result = Get-Content -LiteralPath "$stem.json" -Raw | ConvertFrom-Json
                if (-not $result.complete -or $result.presentationHistoryOverwrites -ne 0 -or $result.samples -lt 96 -or $result.palettes -ne $count -or $result.swatchesPerPalette -ne $Swatches) { throw 'OVERLAY_BENCH_INCOMPLETE_SAMPLES' }
                if ($result.memorySamples -lt 3 -or $result.framebufferWidth -le 0 -or $result.framebufferHeight -le 0) { throw 'OVERLAY_BENCH_INCOMPLETE_MEMORY_OR_FRAMEBUFFER' }
                $framebuffer = "$($result.framebufferWidth)x$($result.framebufferHeight)"
                if ($null -eq $referenceFramebuffer) { $referenceFramebuffer = $framebuffer }
                elseif ($referenceFramebuffer -ne $framebuffer) { throw 'OVERLAY_BENCH_FRAMEBUFFER_MISMATCH' }
                $manifest.Add([pscustomobject]@{ variant=$variant; count=$count; scenario=$scenario; run=$run; executable=$executable;
                    requestedWindowVisibility=$(if($VisibleWindow){'visible'}else{'hidden'});
                    sha256=(Get-FileHash -LiteralPath $executable -Algorithm SHA256).Hash;
                    managedPayloadHashes=@(Get-ChildItem -LiteralPath ([IO.Path]::GetDirectoryName($executable)) -File | Where-Object Extension -EQ '.dll' | ForEach-Object { @{ name=$_.Name; sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash } });
                    result="$stem.json"; exitCode=$process.ExitCode })
                $manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $OutDir 'runs.json')
                Write-Output "$variant $count $scenario run=$run FPS=$([Math]::Round($result.averageFps,2))"
                $process.Dispose()
            }
        }
    }
}
Export-OverlaySummary $OutDir
