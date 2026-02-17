<#
.SYNOPSIS
    Duxel FBA 샘플을 로컬 프로젝트 참조로 실행합니다.
.DESCRIPTION
    FBA 파일의 #:package 지시문을 #:project로 치환한 임시 파일을 만들어 기본적으로 NativeAOT 게시를 수행합니다.
    원본 파일은 변경하지 않습니다.
.PARAMETER Path
    실행할 FBA .cs 파일 경로.
.PARAMETER NoBuild
    dotnet run/dotnet publish에 --no-build 플래그를 전달합니다.
.PARAMETER NoCache
    dotnet run/dotnet publish에 --no-cache 플래그를 전달합니다.
.PARAMETER NativeAot
    NativeAOT 게시를 강제합니다. (기본 동작)
.PARAMETER Managed
    NativeAOT 대신 dotnet run(Managed)으로 실행합니다.
.PARAMETER RuntimeIdentifier
    NativeAOT 게시 시 사용할 RID (기본값: win-x64).
.PARAMETER Configuration
    빌드 구성 (기본값: Release).
.PARAMETER Platform
    플랫폼 선택값(예: windows). `--platform windows` 형태로 전달 가능.
.PARAMETER ManagedTimeoutSeconds
    Managed 실행(`dotnet run`) 최대 대기 시간(초). 0 이하면 무제한.
.PARAMETER KillProcessTreeOnTimeout
    Managed 타임아웃 시 프로세스 트리를 강제 종료합니다.
.PARAMETER Launch
    (호환용) NativeAOT 게시 성공 후 산출 실행 파일(.exe)을 자동 실행합니다.
.PARAMETER NoLaunch
    NativeAOT 기본 자동 실행을 비활성화합니다.
.PARAMETER ExtraArgs
    dotnet run/dotnet publish에 전달할 추가 인수.
.EXAMPLE
    ./run-fba.ps1 samples/fba/all_features.cs
.EXAMPLE
    ./run-fba.ps1 samples/fba/all_features.cs -NoCache
.EXAMPLE
    ./run-fba.ps1 samples/fba/Duxel_perf_test_fba.cs
.EXAMPLE
    ./run-fba.ps1 samples/fba/Duxel_perf_test_fba.cs -Managed
.EXAMPLE
    ./run-fba.ps1 samples/fba/Duxel_perf_test_fba.cs -Launch
.EXAMPLE
    ./run-fba.ps1 samples/fba/Duxel_perf_test_fba.cs -Configuration Debug -Managed
#>
param(
    [Parameter(Mandatory, Position = 0)]
    [string]$Path,

    [switch]$NoBuild,
    [switch]$NoCache,
    [switch]$NativeAot,
    [switch]$Managed,
    [string]$RuntimeIdentifier = 'win-x64',
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$Platform,
    [int]$ManagedTimeoutSeconds = 0,
    [switch]$KillProcessTreeOnTimeout,
    [switch]$Launch,
    [switch]$NoLaunch,

    [Parameter(ValueFromRemainingArguments)]
    [string[]]$ExtraArgs
)

$ErrorActionPreference = 'Stop'

# 스크립트가 레포 루트에서 실행되는지 확인
$repoRoot = $PSScriptRoot
if (-not (Test-Path (Join-Path $repoRoot 'Duxel.slnx'))) {
    $repoRoot = Get-Location
    if (-not (Test-Path (Join-Path $repoRoot 'Duxel.slnx'))) {
        Write-Error "레포 루트를 찾을 수 없습니다. Duxel.slnx가 있는 디렉토리에서 실행하세요."
        exit 1
    }
}

# 원본 파일 경로 해석
$sourcePath = Resolve-Path $Path -ErrorAction Stop
$sourceContent = Get-Content $sourcePath -Raw -Encoding UTF8
$baseName = [System.IO.Path]::GetFileNameWithoutExtension($sourcePath)

function Resolve-PlatformFromExtraArgs {
    param([string[]]$Args)

    if (-not $Args -or $Args.Count -eq 0) {
        return [pscustomobject]@{
            Platform = $null
            RemainingArgs = @()
        }
    }

    $remaining = New-Object System.Collections.Generic.List[string]
    $platform = $null
    for ($i = 0; $i -lt $Args.Count; $i++) {
        $arg = $Args[$i]
        if ([string]::IsNullOrWhiteSpace($arg)) {
            continue
        }

        if ($arg -ieq '--platform') {
            if ($i + 1 -ge $Args.Count) {
                throw "--platform 옵션에는 값이 필요합니다. 예: --platform windows"
            }

            $platform = $Args[$i + 1]
            $i++
            continue
        }

        if ($arg.StartsWith('--platform=', [System.StringComparison]::OrdinalIgnoreCase)) {
            $platform = $arg.Substring('--platform='.Length)
            continue
        }

        $remaining.Add($arg)
    }

    return [pscustomobject]@{
        Platform = $platform
        RemainingArgs = $remaining.ToArray()
    }
}

function Resolve-PlatformFromSourceProperty {
    param([string]$Content)

    $platformPropertyPattern = '(?im)^\s*#:property\s+platform\s*=\s*([^\r\n]+)\s*$'
    $platformPropertyMatch = [System.Text.RegularExpressions.Regex]::Match($Content, $platformPropertyPattern)
    if (-not $platformPropertyMatch.Success) {
        return $null
    }

    return $platformPropertyMatch.Groups[1].Value.Trim()
}

function Resolve-PlatformFromInvocationLine {
    param([string]$Line)

    if ([string]::IsNullOrWhiteSpace($Line)) {
        return $null
    }

    $match = [System.Text.RegularExpressions.Regex]::Match(
        $Line,
        '(?i)--platform(?:\s+|=)([^\s"'']+|"[^"]+")'
    )
    if (-not $match.Success) {
        return $null
    }

    return $match.Groups[1].Value.Trim('"', "'")
}

# #:package Duxel.App@... 또는 Duxel.Windows.App@... → #:project 치환

# FBA 파일이 samples/fba/ 외의 위치에 있을 수도 있으므로 상대 경로 계산
$sourceDir = Split-Path $sourcePath -Parent
$appCsprojAbs = (Join-Path $repoRoot 'src/Duxel.App/Duxel.App.csproj') -replace '\\', '/'
$windowsAppCsprojAbs = (Join-Path $repoRoot 'src/Duxel.Windows.App/Duxel.Windows.App.csproj') -replace '\\', '/'
$coreCsprojAbs = (Join-Path $repoRoot 'src/Duxel.Core/Duxel.Core.csproj') -replace '\\', '/'

$platformArgs = Resolve-PlatformFromExtraArgs -Args $ExtraArgs
$selectedPlatform = $platformArgs.Platform
$ExtraArgs = $platformArgs.RemainingArgs
if ([string]::IsNullOrWhiteSpace($selectedPlatform)) {
    $selectedPlatform = $Platform
}
if ([string]::IsNullOrWhiteSpace($selectedPlatform)) {
    $selectedPlatform = Resolve-PlatformFromInvocationLine -Line $MyInvocation.Line
}
if ([string]::IsNullOrWhiteSpace($selectedPlatform)) {
    $selectedPlatform = Resolve-PlatformFromSourceProperty -Content $sourceContent
}
if ([string]::IsNullOrWhiteSpace($selectedPlatform)) {
    $selectedPlatform = 'windows'
}

$selectedPlatformNormalized = if ([string]::IsNullOrWhiteSpace($selectedPlatform)) { $null } else { $selectedPlatform.Trim().ToLowerInvariant() }

$patternApp = '#:package\s+Duxel\.App@[^\r\n]+'
$patternWindowsApp = '#:package\s+Duxel\.Windows\.App@[^\r\n]+'
$patternTemplatedPlatformApp = '#:package\s+Duxel\.(?:\{platform\}|\$\(platform\))\.App@[^\r\n]+'

$projectRefs = $null
$packageName = $null
if ($sourceContent -match $patternWindowsApp) {
    $packageName = 'Duxel.Windows.App'
    $projectRefs = "#:project $windowsAppCsprojAbs`n#:project $coreCsprojAbs"
    $replaced = [System.Text.RegularExpressions.Regex]::Replace($sourceContent, $patternWindowsApp, [System.Text.RegularExpressions.MatchEvaluator]{ param($m) $projectRefs }, 1)
}
elseif ($sourceContent -match $patternApp) {
    $packageName = 'Duxel.App'
    $projectRefs = "#:project $appCsprojAbs`n#:project $coreCsprojAbs"
    $replaced = [System.Text.RegularExpressions.Regex]::Replace($sourceContent, $patternApp, [System.Text.RegularExpressions.MatchEvaluator]{ param($m) $projectRefs }, 1)
}
elseif ($sourceContent -match $patternTemplatedPlatformApp) {
    switch ($selectedPlatformNormalized) {
        'windows' {
            $packageName = 'Duxel.Windows.App'
            $projectRefs = "#:project $windowsAppCsprojAbs`n#:project $coreCsprojAbs"
        }
        $null {
            throw "#:package Duxel.{platform}.App 또는 Duxel.$(platform).App 사용 시 플랫폼 값이 필요합니다. 예: --platform windows 또는 파일에 #:property platform=windows"
        }
        default {
            throw "지원하지 않는 플랫폼 값입니다: '$selectedPlatform'. 현재 지원: windows"
        }
    }

    $replaced = [System.Text.RegularExpressions.Regex]::Replace($sourceContent, $patternTemplatedPlatformApp, [System.Text.RegularExpressions.MatchEvaluator]{ param($m) $projectRefs }, 1)
}

if ($null -eq $projectRefs) {
    Write-Host "[run-fba] #:package Duxel.App 또는 Duxel.Windows.App 지시문을 찾을 수 없습니다. 원본 그대로 실행합니다." -ForegroundColor Yellow
    $tempPath = $null
}
else {
    if ($packageName -eq 'Duxel.Windows.App') {
        if ($replaced -notmatch '(?m)^\s*using\s+Duxel\.Windows\.App\s*;\s*$') {
            $replaced = [System.Text.RegularExpressions.Regex]::Replace(
                $replaced,
                '(?m)^\s*using\s+Duxel\.App\s*;\s*$',
                "using Duxel.App;`nusing Duxel.Windows.App;",
                1
            )
        }

        $replaced = [System.Text.RegularExpressions.Regex]::Replace(
            $replaced,
            'DuxelApp\.Run\s*\(',
            'DuxelWindowsApp.Run(',
            [System.Text.RegularExpressions.RegexOptions]::None
        )
    }

    if ($packageName -eq 'Duxel.Windows.App') {
        Write-Host "[run-fba] 프로젝트 참조로 실행: src/Duxel.Windows.App + src/Duxel.Core" -ForegroundColor Cyan
    }
    else {
        Write-Host "[run-fba] 프로젝트 참조로 실행: src/Duxel.App + src/Duxel.Core" -ForegroundColor Cyan
    }

    # 임시 파일 생성 (격리된 임시 디렉토리 + 원본과 동일한 파일명)
    $tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("run-fba-{0}" -f ([guid]::NewGuid().ToString('N')))
    New-Item -ItemType Directory -Path $tempRoot -Force | Out-Null
    $tempPath = Join-Path $tempRoot ("$baseName.cs")
    Set-Content -Path $tempPath -Value $replaced -Encoding UTF8 -NoNewline
}

$runPath = if ($tempPath) { $tempPath } else { $sourcePath }
$runBaseName = [System.IO.Path]::GetFileNameWithoutExtension($runPath)
$useNativeAot = -not $Managed
if ($NativeAot) {
    $useNativeAot = $true
}

if ($Managed -and $NativeAot) {
    Write-Warning "-Managed 와 -NativeAot 가 함께 지정되었습니다. NativeAOT를 우선 적용합니다."
}

function Invoke-ManagedRunWithTimeout {
    param(
        [string[]]$DotnetArgs,
        [int]$TimeoutSeconds,
        [switch]$KillTree
    )

    $proc = Start-Process -FilePath 'dotnet' -ArgumentList $DotnetArgs -NoNewWindow -PassThru
    try {
        if ($TimeoutSeconds -le 0) {
            $proc.WaitForExit()
            return $proc.ExitCode
        }

        $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
        while (-not $proc.HasExited -and (Get-Date) -lt $deadline) {
            Start-Sleep -Milliseconds 500
            $proc.Refresh()
        }

        if (-not $proc.HasExited) {
            if ($KillTree) {
                & taskkill /PID $proc.Id /T /F 2>$null | Out-Null
            }
            else {
                try {
                    $proc.Kill()
                }
                catch {
                }
            }

            throw "Managed 실행이 타임아웃되었습니다. TimeoutSeconds=$TimeoutSeconds"
        }

        return $proc.ExitCode
    }
    finally {
        if (-not $proc.HasExited) {
            if ($KillTree) {
                & taskkill /PID $proc.Id /T /F 2>$null | Out-Null
            }
            else {
                try {
                    $proc.Kill()
                }
                catch {
                }
            }
        }
    }
}

function Wait-DetachedSampleProcessExit {
    param(
        [string]$ProcessName,
        [datetime]$StartedAfter,
        [int]$TimeoutSeconds,
        [switch]$KillTree
    )

    if ($TimeoutSeconds -le 0 -or [string]::IsNullOrWhiteSpace($ProcessName)) {
        return
    }

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $targets = Get-Process -Name $ProcessName -ErrorAction SilentlyContinue | Where-Object {
            try {
                $_.StartTime -ge $StartedAfter.AddSeconds(-1)
            }
            catch {
                $false
            }
        }

        if ($null -eq $targets -or $targets.Count -eq 0) {
            return
        }

        Start-Sleep -Milliseconds 500
    }

    $remainingTargets = Get-Process -Name $ProcessName -ErrorAction SilentlyContinue | Where-Object {
        try {
            $_.StartTime -ge $StartedAfter.AddSeconds(-1)
        }
        catch {
            $false
        }
    }

    if ($remainingTargets -and $remainingTargets.Count -gt 0) {
        foreach ($target in $remainingTargets) {
            if ($KillTree) {
                & taskkill /PID $target.Id /T /F 2>$null | Out-Null
            }
            else {
                try {
                    $target.Kill()
                }
                catch {
                }
            }
        }

        throw "분리 실행된 샘플 프로세스가 타임아웃 내 종료되지 않았습니다. ProcessName=$ProcessName TimeoutSeconds=$TimeoutSeconds"
    }
}

function Stop-RunningProcessForImagePath {
    param([string]$ImagePath)

    if ([string]::IsNullOrWhiteSpace($ImagePath) -or -not (Test-Path $ImagePath)) {
        return
    }

    $fullImagePath = [System.IO.Path]::GetFullPath($ImagePath)
    $imageName = [System.IO.Path]::GetFileName($fullImagePath)
    $targets = Get-CimInstance Win32_Process -Filter "Name='$imageName'" -ErrorAction SilentlyContinue | Where-Object {
        $exePath = $_.ExecutablePath
        -not [string]::IsNullOrWhiteSpace($exePath) -and [string]::Equals($exePath, $fullImagePath, [System.StringComparison]::OrdinalIgnoreCase)
    }

    foreach ($target in $targets) {
        & taskkill /PID $target.ProcessId /T /F 2>$null | Out-Null
    }
}

function Get-RunningProcessForImagePath {
    param([string]$ImagePath)

    if ([string]::IsNullOrWhiteSpace($ImagePath) -or -not (Test-Path $ImagePath)) {
        return @()
    }

    $fullImagePath = [System.IO.Path]::GetFullPath($ImagePath)
    $imageName = [System.IO.Path]::GetFileName($fullImagePath)
    return @(Get-CimInstance Win32_Process -Filter "Name='$imageName'" -ErrorAction SilentlyContinue | Where-Object {
        $exePath = $_.ExecutablePath
        -not [string]::IsNullOrWhiteSpace($exePath) -and [string]::Equals($exePath, $fullImagePath, [System.StringComparison]::OrdinalIgnoreCase)
    })
}

try {
    if ($useNativeAot) {
        $publishDir = Join-Path $sourceDir ("artifacts/{0}/" -f $baseName)
        $targetExePath = Join-Path $publishDir ("{0}.exe" -f $runBaseName)

        Stop-RunningProcessForImagePath -ImagePath $targetExePath
        $remainingLocks = Get-RunningProcessForImagePath -ImagePath $targetExePath
        if ($remainingLocks.Count -gt 0) {
            $timestamp = (Get-Date).ToString('yyyyMMdd-HHmmssfff')
            $publishDir = Join-Path $sourceDir ("artifacts/{0}/runs/{1}/" -f $baseName, $timestamp)
            $targetExePath = Join-Path $publishDir ("{0}.exe" -f $runBaseName)
            Write-Host "[run-fba] 출력 파일 잠금 감지로 고유 publish 디렉터리로 우회합니다: $publishDir" -ForegroundColor Yellow
        }

        # dotnet publish 인수 구성 (NativeAOT)
        $dotnetArgs = @(
            'publish',
            $runPath,
            '-c', $Configuration,
            '-r', $RuntimeIdentifier,
            '-p:PublishAot=true',
            '-p:SelfContained=true',
            "-p:PublishDir=$publishDir"
        )

        if ($NoCache) { $dotnetArgs += '--no-cache' }
        if ($NoBuild) { $dotnetArgs += '--no-build' }
        if ($ExtraArgs) { $dotnetArgs += $ExtraArgs }

        Write-Host "[run-fba] NativeAOT 게시: RID=$RuntimeIdentifier" -ForegroundColor Cyan
        Write-Host "[run-fba] 출력 경로: $publishDir" -ForegroundColor Cyan
        & dotnet @dotnetArgs
        if ($LASTEXITCODE -ne 0) {
            throw "NativeAOT 게시가 실패했습니다. 종료 코드: $LASTEXITCODE"
        }

        $shouldLaunch = -not $NoLaunch
        if ($shouldLaunch) {
            $exePath = Join-Path $publishDir ("{0}.exe" -f $runBaseName)
            if (-not (Test-Path $exePath)) {
                $fallbackExe = Get-ChildItem -Path $publishDir -Filter '*.exe' -File | Select-Object -First 1
                if ($null -eq $fallbackExe) {
                    throw "게시 결과 실행 파일을 찾을 수 없습니다: $exePath"
                }

                $exePath = $fallbackExe.FullName
            }

            Write-Host "[run-fba] 실행: $exePath" -ForegroundColor Cyan
            Start-Process -FilePath $exePath | Out-Null
        }
    }
    else {
        # dotnet run 인수 구성
        $dotnetArgs = @('run', '-c', $Configuration)
        if ($NoCache) { $dotnetArgs += '--no-cache' }
        if ($NoBuild) { $dotnetArgs += '--no-build' }
        $dotnetArgs += $runPath
        if ($ExtraArgs) {
            $dotnetArgs += '--'
            $dotnetArgs += $ExtraArgs
        }

        if ($ManagedTimeoutSeconds -gt 0) {
            $managedRunStart = Get-Date
            $exitCode = Invoke-ManagedRunWithTimeout -DotnetArgs $dotnetArgs -TimeoutSeconds $ManagedTimeoutSeconds -KillTree:$KillProcessTreeOnTimeout
            if ($exitCode -ne 0) {
                throw "Managed 실행이 실패했습니다. 종료 코드: $exitCode"
            }

            $elapsedSeconds = [int]([Math]::Floor(((Get-Date) - $managedRunStart).TotalSeconds))
            $remainingTimeout = [Math]::Max(1, $ManagedTimeoutSeconds - $elapsedSeconds)
            Wait-DetachedSampleProcessExit -ProcessName $runBaseName -StartedAfter $managedRunStart -TimeoutSeconds $remainingTimeout -KillTree:$KillProcessTreeOnTimeout
        }
        else {
            & dotnet @dotnetArgs
            if ($LASTEXITCODE -ne 0) {
                throw "Managed 실행이 실패했습니다. 종료 코드: $LASTEXITCODE"
            }
        }
    }
}
finally {
    # 임시 파일 정리
    if ($tempPath -and (Test-Path $tempPath)) {
        Remove-Item $tempPath -Force -ErrorAction SilentlyContinue
    }

    if ($tempRoot -and (Test-Path $tempRoot)) {
        Remove-Item $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}
