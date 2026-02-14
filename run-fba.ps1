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

# #:package Duxel.App@... → #:project 치환
$projectRef = '#:project ../../src/Duxel.App/Duxel.App.csproj'

# FBA 파일이 samples/fba/ 외의 위치에 있을 수도 있으므로 상대 경로 계산
$sourceDir = Split-Path $sourcePath -Parent
$appCsprojAbs = Join-Path $repoRoot 'src/Duxel.App/Duxel.App.csproj'
$relativePath = [System.IO.Path]::GetRelativePath($sourceDir, $appCsprojAbs) -replace '\\', '/'
$projectRefForFba = $appCsprojAbs -replace '\\', '/'
$projectRef = "#:project $projectRefForFba"

$pattern = '#:package\s+Duxel\.App@[^\r\n]+'
if ($sourceContent -notmatch $pattern) {
    Write-Host "[run-fba] #:package Duxel.App 지시문을 찾을 수 없습니다. 원본 그대로 실행합니다." -ForegroundColor Yellow
    $tempPath = $null
}
else {
    $replaced = $sourceContent -replace $pattern, $projectRef

    # 임시 파일 생성 (격리된 임시 디렉토리 + 원본과 동일한 파일명)
    $tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("run-fba-{0}" -f ([guid]::NewGuid().ToString('N')))
    New-Item -ItemType Directory -Path $tempRoot -Force | Out-Null
    $tempPath = Join-Path $tempRoot ("$baseName.cs")
    Set-Content -Path $tempPath -Value $replaced -Encoding UTF8 -NoNewline

    Write-Host "[run-fba] 프로젝트 참조로 실행: $relativePath" -ForegroundColor Cyan
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

try {
    if ($useNativeAot) {
        $publishDir = Join-Path $sourceDir ("artifacts/{0}/" -f $baseName)

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

        & dotnet @dotnetArgs
        if ($LASTEXITCODE -ne 0) {
            throw "Managed 실행이 실패했습니다. 종료 코드: $LASTEXITCODE"
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
