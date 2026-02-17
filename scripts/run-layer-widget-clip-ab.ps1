param(
    [string]$SamplePath = "samples/fba/layer_widget_mix_bench_fba.cs",
    [string]$LegacyOut = "artifacts/layer-widget-mix-legacy.json",
    [string]$OptimizedOut = "artifacts/layer-widget-mix-optimized.json",
    [double]$PhaseSeconds = 1.0,
    [switch]$Managed,
    [int]$RunFbaTimeoutSeconds = 300
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repoRoot
try {
    $sampleName = [System.IO.Path]::GetFileNameWithoutExtension($SamplePath)

    function Wait-ForFile {
        param(
            [string]$Path,
            [int]$TimeoutSeconds = 180
        )

        $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
        while ((Get-Date) -lt $deadline) {
            if (Test-Path $Path) {
                return
            }

            Start-Sleep -Milliseconds 500
        }

        throw "Output file not found within timeout: $Path"
    }

    function Stop-SampleProcessIfRunning {
        param([string]$ProcessName)

        $running = Get-Process -Name $ProcessName -ErrorAction SilentlyContinue
        if ($null -ne $running) {
            $running | Stop-Process -Force
            Start-Sleep -Milliseconds 300
        }

        $imageName = "$ProcessName.exe"
        & taskkill /IM $imageName /T /F 2>$null | Out-Null
    }

    function Invoke-RunFbaWithTimeout {
        param([string]$RunFbaPath)

        $args = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $RunFbaPath, '-Path', $SamplePath)
        if ($Managed) {
            $args += '-Managed'
        }

        $proc = Start-Process -FilePath 'pwsh' -ArgumentList $args -PassThru
        try {
            if ($RunFbaTimeoutSeconds -le 0) {
                $proc.WaitForExit()
            }
            else {
                $deadline = (Get-Date).AddSeconds($RunFbaTimeoutSeconds)
                while (-not $proc.HasExited -and (Get-Date) -lt $deadline) {
                    Start-Sleep -Milliseconds 500
                    $proc.Refresh()
                }

                if (-not $proc.HasExited) {
                    & taskkill /PID $proc.Id /T /F 2>$null | Out-Null
                    throw "run-fba timeout after ${RunFbaTimeoutSeconds}s"
                }
            }

            if ($proc.ExitCode -ne 0) {
                throw "run-fba failed with exit code: $($proc.ExitCode)"
            }
        }
        finally {
            if (-not $proc.HasExited) {
                & taskkill /PID $proc.Id /T /F 2>$null | Out-Null
            }
        }
    }

    function Invoke-BenchRun {
        param(
            [string]$LegacyFlag,
            [string]$OutPath
        )

        $fullOut = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $OutPath))
        $outDir = Split-Path -Parent $fullOut
        if (-not (Test-Path $outDir)) {
            New-Item -ItemType Directory -Path $outDir | Out-Null
        }
        if (Test-Path $fullOut) {
            Remove-Item $fullOut -Force
        }

        $prevLegacy = $env:DUXEL_VK_LEGACY_CLIP_CLAMP
        $prevPhase = $env:DUXEL_LAYER_WIDGET_PHASE_SECONDS
        $prevOut = $env:DUXEL_LAYER_WIDGET_BENCH_OUT

        try {
            $env:DUXEL_VK_LEGACY_CLIP_CLAMP = $LegacyFlag
            $env:DUXEL_LAYER_WIDGET_PHASE_SECONDS = [string]::Format([System.Globalization.CultureInfo]::InvariantCulture, "{0:0.###}", $PhaseSeconds)
            $env:DUXEL_LAYER_WIDGET_BENCH_OUT = $fullOut

            $runFba = Join-Path $repoRoot 'run-fba.ps1'
            Invoke-RunFbaWithTimeout -RunFbaPath $runFba
            Wait-ForFile -Path $fullOut
        }
        finally {
            Stop-SampleProcessIfRunning -ProcessName $sampleName

            if ($null -eq $prevLegacy) { Remove-Item Env:DUXEL_VK_LEGACY_CLIP_CLAMP -ErrorAction SilentlyContinue } else { $env:DUXEL_VK_LEGACY_CLIP_CLAMP = $prevLegacy }
            if ($null -eq $prevPhase) { Remove-Item Env:DUXEL_LAYER_WIDGET_PHASE_SECONDS -ErrorAction SilentlyContinue } else { $env:DUXEL_LAYER_WIDGET_PHASE_SECONDS = $prevPhase }
            if ($null -eq $prevOut) { Remove-Item Env:DUXEL_LAYER_WIDGET_BENCH_OUT -ErrorAction SilentlyContinue } else { $env:DUXEL_LAYER_WIDGET_BENCH_OUT = $prevOut }
        }
    }

    Write-Host "[ab] legacy run..."
    Invoke-BenchRun -LegacyFlag '1' -OutPath $LegacyOut

    Write-Host "[ab] optimized run..."
    Invoke-BenchRun -LegacyFlag '0' -OutPath $OptimizedOut

    $legacyPath = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $LegacyOut))
    $optimizedPath = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $OptimizedOut))

    $legacy = Get-Content $legacyPath -Raw | ConvertFrom-Json
    $optimized = Get-Content $optimizedPath -Raw | ConvertFrom-Json

    if ($legacy.records.Count -ne $optimized.records.Count) {
        throw "legacy/optimized records count mismatch"
    }

    $rows = for ($i = 0; $i -lt $legacy.records.Count; $i++) {
        $left = $legacy.records[$i]
        $right = $optimized.records[$i]
        $pct = if ($left.avgFps -ne 0) { (($right.avgFps - $left.avgFps) / $left.avgFps) * 100 } else { 0 }

        [PSCustomObject]@{
            phase = $left.name
            legacy = [Math]::Round([double]$left.avgFps, 3)
            optimized = [Math]::Round([double]$right.avgFps, 3)
            pct = [Math]::Round($pct, 3)
        }
    }

    $avgLegacy = ($legacy.records | Measure-Object avgFps -Average).Average
    $avgOptimized = ($optimized.records | Measure-Object avgFps -Average).Average
    $avgPct = if ($avgLegacy -ne 0) { (($avgOptimized - $avgLegacy) / $avgLegacy) * 100 } else { 0 }

    Write-Host "`n[ab] per-phase"
    $rows | Format-Table -AutoSize

    Write-Host "[ab] average: legacy=$([Math]::Round($avgLegacy,3)) optimized=$([Math]::Round($avgOptimized,3)) pct=$([Math]::Round($avgPct,3))%"
}
finally {
    Pop-Location
}
