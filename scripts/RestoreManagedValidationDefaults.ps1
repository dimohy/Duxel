[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$HttpCacheDirectory,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$ValidationProject,
    [string]$ValidationArtifactsDirectory
)

$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
foreach ($path in @($HttpCacheDirectory, $OutputDirectory)) {
    if (-not [IO.Path]::IsPathFullyQualified($path)) { throw 'VALIDATION_RESTORE_ABSOLUTE_PATH_REQUIRED' }
}
$httpCache = [IO.Path]::GetFullPath($HttpCacheDirectory)
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw 'VALIDATION_RESTORE_FRESH_OUTPUT_REQUIRED' }
New-Item -ItemType Directory -Path $output | Out-Null
$result = [ordered]@{
    observedAtUtc = [DateTime]::UtcNow.ToString('o')
    httpCache = $httpCache
    completed = $false
    failure = $null
    sdk = $null
    projects = @()
}

function Invoke-ValidationDotNet {
    param([string[]]$Arguments, [string]$Name)
    $start = [Diagnostics.ProcessStartInfo]::new($dotnet)
    $start.WorkingDirectory = Join-Path $repository 'tests/managed-validation'
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    # This explicit execution contract does not change user or machine settings.
    $start.Environment['NUGET_HTTP_CACHE_PATH'] = $httpCache
    foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::Start($start)
    try {
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        $process.WaitForExit()
        $text = $stdout.GetAwaiter().GetResult()
        [IO.File]::WriteAllText((Join-Path $output "$Name.stdout.log"), $text)
        [IO.File]::WriteAllText((Join-Path $output "$Name.stderr.log"), $stderr.GetAwaiter().GetResult())
        return [pscustomobject]@{ exitCode = $process.ExitCode; text = $text }
    } finally { $process.Dispose() }
}

try {
    if ($ValidationProject) {
        if (-not [IO.Path]::IsPathFullyQualified($ValidationProject) -or
            -not [IO.Path]::IsPathFullyQualified($ValidationArtifactsDirectory)) {
            throw 'VALIDATION_PROJECT_ABSOLUTE_PATH_REQUIRED'
        }
        $validationPath = [IO.Path]::GetFullPath($ValidationProject)
        $testsRoot = [IO.Path]::GetFullPath((Join-Path $repository 'tests')) + [IO.Path]::DirectorySeparatorChar
        if (-not $validationPath.StartsWith($testsRoot, [StringComparison]::OrdinalIgnoreCase) -or
            [IO.Path]::GetExtension($validationPath) -ne '.csproj' -or
            -not (Test-Path -LiteralPath $validationPath -PathType Leaf)) {
            throw 'VALIDATION_PROJECT_OUTSIDE_TESTS_OR_MISSING'
        }
    } elseif ($ValidationArtifactsDirectory) {
        throw 'VALIDATION_PROJECT_REQUIRED'
    }
    $drive = [IO.DriveInfo]::new([IO.Path]::GetPathRoot($httpCache))
    if ($drive.AvailableFreeSpace -eq 0) { throw 'VALIDATION_HTTP_CACHE_NO_SPACE' }
    New-Item -ItemType Directory -Path $httpCache -Force | Out-Null
    $probe = Join-Path $httpCache ('duxel-write-probe-' + [Guid]::NewGuid().ToString('N'))
    try { [IO.File]::WriteAllBytes($probe, [byte[]](1, 2, 3, 4)) }
    finally { if (Test-Path -LiteralPath $probe) { Remove-Item -LiteralPath $probe } }
    $dotnet = (Get-Command dotnet -CommandType Application).Source
    $version = Invoke-ValidationDotNet -Arguments @('--version') -Name 'sdk'
    if ($version.exitCode -ne 0 -or $version.text.Trim() -ne '11.0.100') { throw 'VALIDATION_RESTORE_SDK_MISMATCH' }
    $result.sdk = $version.text.Trim()
    if ($ValidationProject) {
        $run = Invoke-ValidationDotNet -Name 'validation' -Arguments @(
            'restore', $validationPath, '--artifacts-path', [IO.Path]::GetFullPath($ValidationArtifactsDirectory),
            '-warnaserror', '-m:1', '-nr:false'
        )
        $result.projects += [ordered]@{ name = 'validation'; path = $validationPath; exitCode = $run.exitCode }
        if ($run.exitCode -ne 0) { throw 'VALIDATION_RESTORE_FAILED_validation' }
    }
    $projects = @(
        @{ name = 'linux'; path = 'src/Duxel.Platform.Linux/Duxel.Platform.Linux.csproj'; lock = '2401683a5186f3006ca4c74780dd48c1152a1f952ce176909d7a2bdade176960' },
        @{ name = 'svg'; path = 'src/Duxel.Svg/Duxel.Svg.csproj'; lock = '064ec938a7b33a7a8d5d9bb690f983d44daa426699b3c589c887ea3d9914b790' }
    )
    foreach ($project in $projects) {
        $projectPath = Join-Path $repository $project.path
        $run = Invoke-ValidationDotNet -Name $project.name -Arguments @(
            'restore', $projectPath, '--force-evaluate', '-p:RestoreLockedMode=false', '-warnaserror', '-m:1', '-nr:false'
        )
        $lockPath = Join-Path ([IO.Path]::GetDirectoryName($projectPath)) 'packages.lock.json'
        $hash = (Get-FileHash -LiteralPath $lockPath -Algorithm SHA256).Hash.ToLowerInvariant()
        $result.projects += [ordered]@{ name = $project.name; exitCode = $run.exitCode; lockSha256 = $hash }
        if ($run.exitCode -ne 0) { throw "VALIDATION_RESTORE_FAILED_$($project.name)" }
        if ($hash -ne $project.lock) { throw "VALIDATION_RESTORE_LOCK_MISMATCH_$($project.name)" }
    }
    $result.completed = $true
} catch {
    $result.failure = $_.Exception.Message
    throw
} finally {
    $result | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $output 'result.json') -Encoding utf8
}
