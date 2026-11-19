param(
    [Parameter(Mandatory)][string]$BaselineExecutable,
    [Parameter(Mandatory)][string]$CandidateExecutable,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [ValidateRange(1, 8)][int]$FrameSlots = 3
)

$ErrorActionPreference = 'Stop'
$resultRoot = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $resultRoot) {
    throw 'Use a fresh comparison output directory.'
}
$executables = @{
    baseline = [IO.Path]::GetFullPath($BaselineExecutable)
    candidate = [IO.Path]::GetFullPath($CandidateExecutable)
}
$inputs = foreach ($kind in @('baseline', 'candidate')) {
    $executable = $executables[$kind]
    $folder = [IO.Path]::GetDirectoryName($executable)
    $stem = [IO.Path]::GetFileNameWithoutExtension($executable)
    $paths = @(
        $executable,
        (Join-Path $folder ($stem + '.dll')),
        (Join-Path $folder ($stem + '.runtimeconfig.json')),
        (Join-Path $folder 'Duxel.Core.dll'),
        (Join-Path $folder 'Duxel.Vulkan.dll'),
        (Join-Path $folder 'Duxel.Svg.dll'),
        (Join-Path $folder 'Assets/Svg/Browser/Red_Gallardo.svg'),
        (Join-Path $folder 'Assets/Svg/Browser/TheStructorr_Lamborghini_Gallardo.svg')
    )
    foreach ($path in $paths) {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Missing comparison input : $path"
        }
        [pscustomobject]@{ kind = $kind; path = $path; sha256 = (Get-FileHash -LiteralPath $path).Hash }
    }
}
[IO.Directory]::CreateDirectory($resultRoot) | Out-Null
[pscustomobject]@{
    rounds = 3
    order = 'baseline,candidate repeated three times'
    mode = 'gpu-svg-camera-present-bench'
    frameSlots = $FrameSlots
    timeoutSecondsPerProcess = 60
    inputs = $inputs
    memoryScope = 'Whole process including preparation, warmup, normal presentation and post-block readback; host private/working set, not VRAM.'
} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $resultRoot 'inputs.json') -Encoding utf8

foreach ($round in 1..3) {
    foreach ($kind in @('baseline', 'candidate')) {
        $name = "$kind-round-$round"
        $runPath = Join-Path $resultRoot $name
        $launch = @{
            FilePath = $executables[$kind]
            ArgumentList = [string[]]@(('"{0}"' -f $runPath), 'gpu-svg-camera-present-bench', "$FrameSlots")
            WindowStyle = 'Hidden'
            PassThru = $true
            RedirectStandardOutput = ($runPath + '.stdout.log')
            RedirectStandardError = ($runPath + '.stderr.log')
        }
        $owned = Start-Process @launch
        $clock = [Diagnostics.Stopwatch]::StartNew()
        [pscustomobject]@{ event = 'started'; name = $name; pid = $owned.Id; atUtc = [DateTime]::UtcNow.ToString('o') } |
            ConvertTo-Json -Compress | Add-Content -LiteralPath (Join-Path $resultRoot 'runs.jsonl') -Encoding utf8
        $samples = [Collections.Generic.List[object]]::new()
        while (-not $owned.WaitForExit(100)) {
            $owned.Refresh()
            if (-not $owned.HasExited) {
                $samples.Add([pscustomobject]@{
                    elapsedMs = $clock.ElapsedMilliseconds
                    privateBytes = $owned.PrivateMemorySize64
                    workingSetBytes = $owned.WorkingSet64
                    cpuMs = $owned.TotalProcessorTime.TotalMilliseconds
                })
            }
            if ($clock.Elapsed.TotalSeconds -gt 60) {
                Stop-Process -Id $owned.Id
                [pscustomobject]@{ event = 'terminated'; name = $name; pid = $owned.Id; reason = 'owned-process-timeout' } |
                    ConvertTo-Json -Compress | Add-Content -LiteralPath (Join-Path $resultRoot 'runs.jsonl') -Encoding utf8
                throw "Owned comparison process exceeded its60 second budget : $name"
            }
        }
        $owned.WaitForExit()
        $samples | Export-Csv -LiteralPath ($runPath + '.process.csv') -NoTypeInformation -Encoding utf8
        [pscustomobject]@{
            event = 'completed'; name = $name; pid = $owned.Id; exitCode = $owned.ExitCode
            elapsedMs = $clock.ElapsedMilliseconds; sampleCount = $samples.Count; atUtc = [DateTime]::UtcNow.ToString('o')
        } | ConvertTo-Json -Compress | Add-Content -LiteralPath (Join-Path $resultRoot 'runs.jsonl') -Encoding utf8
        if ($owned.ExitCode -ne 0) {
            throw "Camera comparison process failed; inspect its independent stdout/stderr : $name"
        }
        foreach ($inputFile in $inputs) {
            if ((Get-FileHash -LiteralPath $inputFile.path).Hash -ne $inputFile.sha256) {
                throw "Comparison input changed during execution : $($inputFile.path)"
            }
        }
        Write-Output "$name : passed"
    }
}
