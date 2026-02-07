<#
.SYNOPSIS
    Duxel FBA 샘플을 로컬 프로젝트 참조로 실행합니다.
.DESCRIPTION
    FBA 파일의 #:package 지시문을 #:project로 치환한 임시 파일을 만들어 dotnet run으로 실행합니다.
    원본 파일은 변경하지 않습니다.
.PARAMETER Path
    실행할 FBA .cs 파일 경로.
.PARAMETER NoBuild
    dotnet run에 --no-build 플래그를 전달합니다.
.PARAMETER NoCache
    dotnet run에 --no-cache 플래그를 전달합니다.
.PARAMETER ExtraArgs
    dotnet run에 전달할 추가 인수.
.EXAMPLE
    ./run-fba.ps1 samples/fba/all_features.cs
.EXAMPLE
    ./run-fba.ps1 samples/fba/all_features.cs -NoCache
#>
param(
    [Parameter(Mandatory, Position = 0)]
    [string]$Path,

    [switch]$NoBuild,
    [switch]$NoCache,

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

# #:package Duxel.App@... → #:project 치환
$projectRef = '#:project ../../src/Duxel.App/Duxel.App.csproj'

# FBA 파일이 samples/fba/ 외의 위치에 있을 수도 있으므로 상대 경로 계산
$sourceDir = Split-Path $sourcePath -Parent
$appCsprojAbs = Join-Path $repoRoot 'src/Duxel.App/Duxel.App.csproj'
$relativePath = [System.IO.Path]::GetRelativePath($sourceDir, $appCsprojAbs) -replace '\\', '/'
$projectRef = "#:project $relativePath"

$pattern = '#:package\s+Duxel\.App@[^\r\n]+'
if ($sourceContent -notmatch $pattern) {
    Write-Host "[run-fba] #:package Duxel.App 지시문을 찾을 수 없습니다. 원본 그대로 실행합니다." -ForegroundColor Yellow
    $tempPath = $null
}
else {
    $replaced = $sourceContent -replace $pattern, $projectRef

    # 임시 파일 생성 (원본과 같은 디렉토리에 .tmp.cs)
    $baseName = [System.IO.Path]::GetFileNameWithoutExtension($sourcePath)
    $tempPath = Join-Path $sourceDir "$baseName.tmp.cs"
    Set-Content -Path $tempPath -Value $replaced -Encoding UTF8 -NoNewline

    Write-Host "[run-fba] 프로젝트 참조로 실행: $relativePath" -ForegroundColor Cyan
}

$runPath = if ($tempPath) { $tempPath } else { $sourcePath }

# dotnet run 인수 구성
$dotnetArgs = @('run')
if ($NoCache) { $dotnetArgs += '--no-cache' }
if ($NoBuild) { $dotnetArgs += '--no-build' }
$dotnetArgs += $runPath
if ($ExtraArgs) {
    $dotnetArgs += '--'
    $dotnetArgs += $ExtraArgs
}

try {
    & dotnet @dotnetArgs
}
finally {
    # 임시 파일 정리
    if ($tempPath -and (Test-Path $tempPath)) {
        Remove-Item $tempPath -Force -ErrorAction SilentlyContinue
    }
}
