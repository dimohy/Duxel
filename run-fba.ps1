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
.PARAMETER ArtifactRootDirectory
    검증 출력을 저장할 저장소 artifacts 하위 전용 루트. 기본 샘플 출력과 분리하며 동일한 소유권·보관 한도를 적용합니다.
.PARAMETER Wait
    NativeAOT 게시 후 산출 실행 파일(.exe)이 종료될 때까지 기다립니다.
.PARAMETER ExtraArgs
    dotnet run/dotnet publish에 전달할 추가 인수.
.EXAMPLE
    ./run-fba.ps1 samples/fba/duxel_gallery.cs
.EXAMPLE
    ./run-fba.ps1 samples/fba/duxel_gallery.cs -NoCache
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
    [ValidatePattern('^net[1-9][0-9]\.0-windows7\.0$')]
    [string]$TargetFramework = 'net10.0-windows7.0',
    [string]$DotnetPath = 'dotnet',
    [string]$ArtifactRootDirectory,
    [int]$ManagedTimeoutSeconds = 0,
    [string]$ManagedLogPrefix,
    [switch]$KillProcessTreeOnTimeout,
    [switch]$Launch,
    [switch]$NoLaunch,
    [switch]$Wait,

    [Parameter(ValueFromRemainingArguments)]
    [string[]]$ExtraArgs
)

$ErrorActionPreference = 'Stop'

# 스크립트가 레포 루트에서 실행되는지 확인
$repoRoot = $PSScriptRoot
if (-not (Test-Path -LiteralPath (Join-Path $repoRoot 'Duxel.slnx'))) {
    $repoRoot = Get-Location
    if (-not (Test-Path -LiteralPath (Join-Path $repoRoot 'Duxel.slnx'))) {
        Write-Error "레포 루트를 찾을 수 없습니다. Duxel.slnx가 있는 디렉토리에서 실행하세요."
        exit 1
    }
}

if ($ArtifactRootDirectory) {
    if ($Managed -and -not $NativeAot) { throw 'BUILD_ARTIFACT_ROOT_REQUIRES_NATIVEAOT' }
    . (Join-Path $repoRoot 'scripts/BuildArtifactLifetime.ps1')
    $validationRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts'))
    $resolvedArtifactRootDirectory = Resolve-DuxelArtifactDirectory -Root $validationRoot -Directory ([IO.Path]::GetFullPath($ArtifactRootDirectory))
    if ($resolvedArtifactRootDirectory -eq $validationRoot) { throw 'BUILD_ARTIFACT_DEDICATED_ROOT_REQUIRED' }
}

# Resolve and probe the same CLI used by publish and managed child processes.
# Do this before allocating temporary inputs or initializing the native toolchain.
$dotnetExecutable = (Get-Command $DotnetPath -CommandType Application -ErrorAction Stop).Source
$sdkProbe = & $dotnetExecutable --version 2>&1
if ($LASTEXITCODE -ne 0) {
    throw "DUXEL-FBA-SDK-UNAVAILABLE: $dotnetExecutable in $((Get-Location).ProviderPath). $($sdkProbe -join [Environment]::NewLine)"
}
$sdkVersion = ($sdkProbe | Out-String).Trim()
$frameworkMajor = [int]([regex]::Match($TargetFramework, '^net(\d+)').Groups[1].Value)
if ($sdkVersion -notmatch '^(\d+)\.' -or [int]$Matches[1] -lt $frameworkMajor) {
    throw "DUXEL-FBA-SDK-TARGET-MISMATCH: SDK $sdkVersion cannot build $TargetFramework. Select a matching SDK directory and -DotnetPath."
}
Write-Host "[run-fba] .NET SDK: $sdkVersion ($dotnetExecutable)"

# 원본 파일 경로 해석
$sourcePath = Resolve-Path -LiteralPath $Path -ErrorAction Stop
$sourceContent = Get-Content -LiteralPath $sourcePath -Raw -Encoding UTF8
$baseName = [System.IO.Path]::GetFileNameWithoutExtension($sourcePath)

function Resolve-PlatformFromExtraArgs {
    param([string[]]$InputArguments)

    if (-not $InputArguments -or $InputArguments.Count -eq 0) {
        return [pscustomobject]@{
            Platform = $null
            RemainingArgs = @()
        }
    }

    $remaining = New-Object System.Collections.Generic.List[string]
    $platform = $null
    for ($i = 0; $i -lt $InputArguments.Count; $i++) {
        $arg = $InputArguments[$i]
        if ([string]::IsNullOrWhiteSpace($arg)) {
            continue
        }

        if ($arg -ieq '--platform') {
            if ($i + 1 -ge $InputArguments.Count) {
                throw "--platform 옵션에는 값이 필요합니다. 예: --platform windows"
            }

            $platform = $InputArguments[$i + 1]
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

$platformArgs = Resolve-PlatformFromExtraArgs -InputArguments $ExtraArgs
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

        $targetFrameworkLine = "#:property TargetFramework=$TargetFramework"
        if ($replaced -match '(?im)^\s*#:property\s+TargetFramework\s*=\s*([^\r\n]+)\s*$') {
            $replaced = [System.Text.RegularExpressions.Regex]::Replace(
                $replaced,
                '(?im)^\s*#:property\s+TargetFramework\s*=\s*([^\r\n]+)\s*$',
                $targetFrameworkLine,
                1
            )
        }
        else {
            $replaced = "$targetFrameworkLine`n$replaced"
        }

        $outputTypeLine = '#:property OutputType=WinExe'
        if ($replaced -notmatch '(?im)^\s*#:property\s+OutputType\s*=\s*WinExe\s*$') {
            if ($replaced -match '(?im)^\s*#:property\s+OutputType\s*=\s*[^\r\n]+\s*$') {
                $replaced = [System.Text.RegularExpressions.Regex]::Replace(
                    $replaced,
                    '(?im)^\s*#:property\s+OutputType\s*=\s*[^\r\n]+\s*$',
                    $outputTypeLine,
                    1
                )
            }
            else {
                $replaced = "$outputTypeLine`n$replaced"
            }
        }

    }

    if ($packageName -eq 'Duxel.Windows.App') {
        Write-Host "[run-fba] 프로젝트 참조로 실행: src/Duxel.Windows.App + src/Duxel.Core" -ForegroundColor Cyan
    }
    else {
        Write-Host "[run-fba] 프로젝트 참조로 실행: src/Duxel.App + src/Duxel.Core" -ForegroundColor Cyan
    }

    # Local FBA sources live outside the repository; explicitly import its consumer analyzer contract.
    if ($replaced -notmatch '(?im)^\s*#:property\s+DirectoryBuildTargetsPath\s*=') {
        $consumerTargets = (Join-Path $repoRoot 'Directory.Build.targets') -replace '\\', '/'
        if (-not (Test-Path -LiteralPath $consumerTargets)) { throw 'DUXEL-FBA-CONSUMER-TARGETS-MISSING' }
        $replaced = "#:property DirectoryBuildTargetsPath=$consumerTargets`n$replaced"
    }

    # 임시 파일 생성 (격리된 임시 디렉토리 + 원본과 동일한 파일명)
    $tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("run-fba-{0}" -f ([guid]::NewGuid().ToString('N')))
    New-Item -ItemType Directory -Path $tempRoot -Force | Out-Null
    $tempPath = Join-Path $tempRoot ("$baseName.cs")
    Set-Content -LiteralPath $tempPath -Value $replaced -Encoding UTF8 -NoNewline
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
        [string]$DotnetExecutable,
        [int]$TimeoutSeconds,
        [string]$LogPrefix,
        [switch]$KillTree
    )

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $DotnetExecutable
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.WorkingDirectory = (Get-Location).ProviderPath
    $startInfo.RedirectStandardOutput = -not [string]::IsNullOrWhiteSpace($LogPrefix)
    $startInfo.RedirectStandardError = $startInfo.RedirectStandardOutput
    foreach ($argument in $DotnetArgs) {
        $startInfo.ArgumentList.Add($argument)
    }

    $proc = [System.Diagnostics.Process]::Start($startInfo)
    if ($null -eq $proc) {
        throw 'dotnet managed run process could not be started.'
    }

    $outputTask = if ($startInfo.RedirectStandardOutput) { $proc.StandardOutput.ReadToEndAsync() } else { $null }
    $errorTask = if ($startInfo.RedirectStandardError) { $proc.StandardError.ReadToEndAsync() } else { $null }

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
        if ($outputTask) { [IO.File]::WriteAllText($LogPrefix + '.stdout.log', $outputTask.GetAwaiter().GetResult()) }
        if ($errorTask) { [IO.File]::WriteAllText($LogPrefix + '.stderr.log', $errorTask.GetAwaiter().GetResult()) }
        $proc.Dispose()
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

function Get-RunningProcessForImagePath {
    param([string]$ImagePath)

    if ([string]::IsNullOrWhiteSpace($ImagePath) -or -not (Test-Path -LiteralPath $ImagePath)) {
        return @()
    }

    $fullImagePath = [System.IO.Path]::GetFullPath($ImagePath)
    $imageName = [System.IO.Path]::GetFileName($fullImagePath)
    return @(Get-CimInstance Win32_Process -Filter "Name='$imageName'" -ErrorAction SilentlyContinue | Where-Object {
        $exePath = $_.ExecutablePath
        -not [string]::IsNullOrWhiteSpace($exePath) -and [string]::Equals($exePath, $fullImagePath, [System.StringComparison]::OrdinalIgnoreCase)
    })
}

function Initialize-DuxelNativeToolchain {
    param([string]$Runtime)
    $architecture = switch ($Runtime) {
        'win-x64' { 'amd64' }
        'win-x86' { 'amd64_x86' }
        'win-arm64' { 'amd64_arm64' }
        default { throw "Unsupported Windows NativeAOT runtime: $Runtime" }
    }
    $installer = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer'
    $vswhere = Join-Path $installer 'vswhere.exe'
    if (-not (Test-Path -LiteralPath $vswhere -PathType Leaf)) { throw "NativeAOT tool discovery missing: $vswhere" }
    $component = if ($Runtime -eq 'win-arm64') { 'Microsoft.VisualStudio.Component.VC.Tools.ARM64' } else { 'Microsoft.VisualStudio.Component.VC.Tools.x86.x64' }
    $probeArgs = @('-latest', '-prerelease', '-products', '*', '-requires', $component, '-property', 'installationPath')
    $installation = (& $vswhere @probeArgs | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or -not $installation) { throw 'NativeAOT C++ installation probe failed.' }
    $vcvars = Join-Path $installation 'VC/Auxiliary/Build/vcvarsall.bat'
    if (-not (Test-Path -LiteralPath $vcvars -PathType Leaf)) { throw "NativeAOT environment missing: $vcvars" }
    $start = [System.Diagnostics.ProcessStartInfo]::new($env:ComSpec)
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.Environment['PATH'] = $installer + ';' + $env:PATH
    # cmd.exe uses its own quote rules; ArgumentList's C-runtime escaping would
    # turn embedded quotes into literal backslashes for this batch invocation.
    $start.Arguments = '/d /s /c ""{0}" {1} >nul && set PATH && set LIB"' -f $vcvars, $architecture
    $probe = [System.Diagnostics.Process]::Start($start)
    $stdout = $probe.StandardOutput.ReadToEndAsync()
    $stderr = $probe.StandardError.ReadToEndAsync()
    if (-not $probe.WaitForExit(30000)) { $probe.Kill($true); throw 'NativeAOT toolchain probe timed out.' }
    $probeOutput = $stdout.GetAwaiter().GetResult()
    $probeError = $stderr.GetAwaiter().GetResult()
    if ($probe.ExitCode -ne 0 -or $probeError.Trim()) { throw "NativeAOT toolchain probe failed: $probeError" }
    $probe.Dispose()
    $nativePath = $null
    $nativeLib = $null
    foreach ($line in ($probeOutput -split '\r?\n')) {
        if ($line.StartsWith('PATH=', [StringComparison]::OrdinalIgnoreCase)) { $nativePath = $line.Substring(5) }
        if ($line.StartsWith('LIB=', [StringComparison]::OrdinalIgnoreCase)) { $nativeLib = $line.Substring(4) }
    }
    if (-not $nativePath -or -not $nativeLib) { throw 'NativeAOT toolchain probe did not produce PATH and LIB.' }
    $linker = $nativePath.Split(';') | ForEach-Object { if ($_ -and (Test-Path -LiteralPath (Join-Path $_ 'link.exe') -PathType Leaf)) { Join-Path $_ 'link.exe' } } | Select-Object -First 1
    if (-not $linker -or -not $linker.StartsWith($installation, [StringComparison]::OrdinalIgnoreCase)) { throw 'NativeAOT resolved an unexpected linker.' }
    $kernelLib = $nativeLib.Split(';') | Where-Object { $_ -and (Test-Path -LiteralPath (Join-Path $_ 'kernel32.lib') -PathType Leaf) }
    if (-not $kernelLib) { throw 'NativeAOT Windows SDK library probe failed.' }
    Write-Host "[run-fba] NativeAOT linker: $linker ($((Get-Item -LiteralPath $linker).VersionInfo.FileVersion))"
    return @{ PATH = $nativePath; LIB = $nativeLib }
}

$originalNativePath = $env:PATH
$originalNativeLib = $env:LIB
try {
    if ($useNativeAot) {
        $toolchainArgs = @{ Runtime = $RuntimeIdentifier }
        $nativeEnvironment = Initialize-DuxelNativeToolchain @toolchainArgs
        $env:PATH = $nativeEnvironment.PATH
        $env:LIB = $nativeEnvironment.LIB
        if (-not $ArtifactRootDirectory) { . (Join-Path $repoRoot 'scripts/BuildArtifactLifetime.ps1') }
        $artifactRoot = Join-Path $sourceDir ("artifacts/{0}/" -f $baseName)
        if ($ArtifactRootDirectory) {
            $artifactRoot = $resolvedArtifactRootDirectory
        }
        $publishDir = $artifactRoot
        if ($TargetFramework -ne 'net10.0-windows7.0') {
            $publishDir = Join-Path $publishDir $TargetFramework
        }
        $targetExePath = Join-Path $publishDir ("{0}.exe" -f $runBaseName)

        $remainingLocks = Get-RunningProcessForImagePath -ImagePath $targetExePath
        if ($remainingLocks.Count -gt 0) {
            $timestamp = (Get-Date).ToString('yyyyMMdd-HHmmssfff')
            $publishDir = Join-Path $artifactRoot ("runs/{0}/" -f $timestamp)
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
            '-p:IlcUseEnvironmentalTools=true',
            "-p:PublishDir=$publishDir"
        )

        if ($NoCache) { $dotnetArgs += '--no-cache' }
        if ($NoBuild) { $dotnetArgs += '--no-build' }
        if ($ExtraArgs) { $dotnetArgs += $ExtraArgs }

        Write-Host "[run-fba] NativeAOT 게시: RID=$RuntimeIdentifier" -ForegroundColor Cyan
        Write-Host "[run-fba] 출력 경로: $publishDir" -ForegroundColor Cyan
        $publishRecord = Start-DuxelPublishArtifact -Root $artifactRoot -Directory $publishDir -ExecutableName "$runBaseName.exe"
        $publishStatus = 'failed'
        try {
            & $dotnetExecutable @dotnetArgs
            if ($LASTEXITCODE -ne 0) {
                throw "NativeAOT 게시가 실패했습니다. 종료 코드: $LASTEXITCODE"
            }

            $assetsDir = Join-Path $sourceDir 'assets'
            if (Test-Path -LiteralPath $assetsDir) {
                $publishAssetsDir = Join-Path $publishDir 'assets'
                New-Item -ItemType Directory -Path $publishAssetsDir -Force | Out-Null
                foreach ($asset in Get-ChildItem -LiteralPath $assetsDir -Force) {
                    Copy-Item -LiteralPath $asset.FullName -Destination $publishAssetsDir -Recurse -Force
                }
            }
            $publishStatus = 'completed'
        }
        catch [System.Management.Automation.PipelineStoppedException] {
            $publishStatus = 'cancelled'
            throw
        }
        finally {
            Complete-DuxelPublishArtifact -Record $publishRecord -Status $publishStatus
        }

        $shouldLaunch = -not $NoLaunch
        if ($shouldLaunch) {
            $exePath = Join-Path $publishDir ("{0}.exe" -f $runBaseName)
            if (-not (Test-Path -LiteralPath $exePath)) {
                $fallbackExe = Get-ChildItem -LiteralPath $publishDir -File |
                    Where-Object { $_.Extension -eq '.exe' } |
                    Select-Object -First 1
                if ($null -eq $fallbackExe) {
                    throw "게시 결과 실행 파일을 찾을 수 없습니다: $exePath"
                }

                $exePath = $fallbackExe.FullName
            }

            Write-Host "[run-fba] 실행: $exePath" -ForegroundColor Cyan
            if ($Wait) {
                $process = Start-Process -FilePath $exePath -Wait -PassThru
                if ($process.ExitCode -ne 0) {
                    throw "NativeAOT 실행이 실패했습니다. 종료 코드: $($process.ExitCode)"
                }
            }
            else {
                Start-Process -FilePath $exePath | Out-Null
            }
        }
    }
    else {
        # dotnet run 인수 구성
        # File-based apps default PublishAot=true, which also disables runtime
        # features under dotnet run. Select an actual managed configuration.
        $managedCommand = if ($NoLaunch) { 'build' } else { 'run' }
        $dotnetArgs = @($managedCommand, '-c', $Configuration, '-p:PublishAot=false', '-p:SelfContained=false')
        if ($NoCache -and -not $NoLaunch) { $dotnetArgs += '--no-cache' }
        if ($NoBuild -and -not $NoLaunch) { $dotnetArgs += '--no-build' }
        $dotnetArgs += $runPath
        if ($ExtraArgs -and -not $NoLaunch) {
            $dotnetArgs += '--'
            $dotnetArgs += $ExtraArgs
        }

        if ($ManagedTimeoutSeconds -gt 0) {
            $managedRunStart = Get-Date
            $exitCode = Invoke-ManagedRunWithTimeout -DotnetArgs $dotnetArgs -DotnetExecutable $dotnetExecutable -TimeoutSeconds $ManagedTimeoutSeconds -LogPrefix $ManagedLogPrefix -KillTree:$KillProcessTreeOnTimeout
            if ($exitCode -ne 0) {
                $global:LASTEXITCODE = $exitCode
                throw "Managed 실행이 실패했습니다. 종료 코드: $exitCode"
            }

            $elapsedSeconds = [int]([Math]::Floor(((Get-Date) - $managedRunStart).TotalSeconds))
            $remainingTimeout = [Math]::Max(1, $ManagedTimeoutSeconds - $elapsedSeconds)
            if (-not $NoLaunch) {
                Wait-DetachedSampleProcessExit -ProcessName $runBaseName -StartedAfter $managedRunStart -TimeoutSeconds $remainingTimeout -KillTree:$KillProcessTreeOnTimeout
            }
            $global:LASTEXITCODE = 0
        }
        else {
            & $dotnetExecutable @dotnetArgs
            if ($LASTEXITCODE -ne 0) {
                throw "Managed 실행이 실패했습니다. 종료 코드: $LASTEXITCODE"
            }
        }
    }
}
finally {
    $env:PATH = $originalNativePath
    $env:LIB = $originalNativeLib
    # 임시 파일 정리
    if ($tempPath -and (Test-Path -LiteralPath $tempPath)) {
        Remove-Item -LiteralPath $tempPath -Force -ErrorAction SilentlyContinue
    }

    if ($tempRoot -and (Test-Path -LiteralPath $tempRoot)) {
        $resolvedTempRoot = [System.IO.Path]::GetFullPath($tempRoot)
        $allowedTempParent = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()).TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
        if (-not $resolvedTempRoot.StartsWith($allowedTempParent, [StringComparison]::OrdinalIgnoreCase) -or
            [System.IO.Path]::GetFileName($resolvedTempRoot) -notmatch '^run-fba-[0-9a-f]{32}$') {
            throw "Refusing temporary cleanup outside the owned run directory: $resolvedTempRoot"
        }
        Remove-Item -LiteralPath $resolvedTempRoot -Recurse -Force
    }
}
