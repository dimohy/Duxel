[CmdletBinding(DefaultParameterSetName = 'Gallery')]
param(
    [Parameter(ParameterSetName = 'FullVerification')]
    [switch]$Verify,
    [Parameter(ParameterSetName = 'LiveHolidayVerification')]
    [switch]$VerifyLiveHolidays,
    [string]$VerificationOutput,
    [switch]$NoRestore
)

$ErrorActionPreference = 'Stop'
$verificationMode = if ($VerifyLiveHolidays) { '--verify-live-holidays' } elseif ($Verify) { '--verify' }
if ($VerificationOutput -and -not $verificationMode) { throw 'VerificationOutput requires Verify or VerifyLiveHolidays.' }
if ($VerificationOutput) {
    $VerificationOutput = [IO.Path]::GetFullPath($VerificationOutput)
    if (Test-Path -LiteralPath $VerificationOutput) { throw 'Use a fresh verification output directory.' }
}
$project = Join-Path $PSScriptRoot 'samples/Duxel.Gallery/Duxel.Gallery.csproj'
$targets = Join-Path $PSScriptRoot 'samples/Duxel.Gallery/LocalSource.targets'
$cacheRoot = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Duxel/Gallery'
$artifacts = Join-Path $cacheRoot 'source-build'
$buildArgs = @(
    'build', $project, '--configuration', 'Release',
    "-p:DirectoryBuildTargetsPath=$targets", '-p:UseArtifactsOutput=true',
    "-p:ArtifactsPath=$artifacts", '-m:1', '-nr:false', '-warnaserror', '-v:minimal'
)
if ($NoRestore) { $buildArgs += '--no-restore' }
& (Join-Path $PSScriptRoot 'scripts/InvokeGalleryBuild.ps1') -Arguments $buildArgs -WorkingDirectory $PSScriptRoot
$buildOutput = Join-Path $artifacts 'bin/Duxel.Gallery/release'
$runId = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
$runs = Join-Path $cacheRoot 'runs'
New-Item -ItemType Directory -Path $runs -Force | Out-Null
$runtime = Join-Path $runs $runId
if (Test-Path -LiteralPath $runtime) { throw 'Gallery runtime directory already exists.' }
Copy-Item -LiteralPath $buildOutput -Destination $runtime -Recurse
$executable = Join-Path $runtime 'Duxel.Gallery.exe'
$logs = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) ('Duxel/Gallery/logs/' + $runId)
New-Item -ItemType Directory -Path $logs | Out-Null
$stdoutPath = Join-Path $logs 'stdout.log'
$stderrPath = Join-Path $logs 'stderr.log'
$start = [Diagnostics.ProcessStartInfo]::new()
$start.FileName = $executable
$start.WorkingDirectory = $PSScriptRoot
$start.UseShellExecute = $false
$start.RedirectStandardOutput = $true
$start.RedirectStandardError = $true
$start.WindowStyle = if ($verificationMode) { [Diagnostics.ProcessWindowStyle]::Hidden } else { [Diagnostics.ProcessWindowStyle]::Normal }
$start.CreateNoWindow = [bool]$verificationMode
if ($verificationMode) {
    if (-not $VerificationOutput) { $VerificationOutput = Join-Path $logs 'verification' }
    $VerificationOutput = [IO.Path]::GetFullPath($VerificationOutput)
    if (Test-Path -LiteralPath $VerificationOutput) { throw 'Use a fresh verification output directory.' }
    $start.ArgumentList.Add($verificationMode)
    $start.ArgumentList.Add($VerificationOutput)
}
$stdout = [IO.File]::Open($stdoutPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::Read)
$stderr = [IO.File]::Open($stderrPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::Read)
$process = [Diagnostics.Process]::new()
try {
    $process.StartInfo = $start
    if (-not $process.Start()) { throw 'Duxel Gallery process did not start.' }
    Write-Host "Gallery runtime : $runtime"
    Write-Host "Gallery logs : $logs"
    $stdoutCopy = $process.StandardOutput.BaseStream.CopyToAsync($stdout)
    $stderrCopy = $process.StandardError.BaseStream.CopyToAsync($stderr)
    $process.WaitForExit()
    $null = $stdoutCopy.GetAwaiter().GetResult()
    $null = $stderrCopy.GetAwaiter().GetResult()
    $exitCode = $process.ExitCode
}
finally {
    $stdout.Dispose()
    $stderr.Dispose()
    $process.Dispose()
}
if ($exitCode -ne 0) {
    $errorDetails = (Get-Content -LiteralPath $stderrPath -Tail 20 -Encoding utf8) -join [Environment]::NewLine
    throw "Duxel Gallery exited with code : $exitCode`nError log : $stderrPath`n$errorDetails"
}
if ($verificationMode) { Write-Host "Gallery verification : $VerificationOutput" }

