[CmdletBinding()]
param(
    [Parameter(Mandatory)][string[]]$Arguments,
    [Parameter(Mandatory)][string]$WorkingDirectory
)

$ErrorActionPreference = 'Stop'
$cache = Join-Path ([IO.Path]::GetPathRoot($PSScriptRoot)) 'Cache/Duxel/nuget-http'
$drive = [IO.DriveInfo]::new([IO.Path]::GetPathRoot($cache))
if ($drive.AvailableFreeSpace -eq 0) { throw "GALLERY_BUILD_CACHE_NO_SPACE : $cache" }
[IO.Directory]::CreateDirectory($cache) | Out-Null
$probe = Join-Path $cache ('gallery-probe-' + [Guid]::NewGuid().ToString('N'))
try { [IO.File]::WriteAllBytes($probe, [byte[]](1)) }
finally { if (Test-Path -LiteralPath $probe) { Remove-Item -LiteralPath $probe } }
$start = [Diagnostics.ProcessStartInfo]::new((Get-Command dotnet -CommandType Application).Source)
$start.WorkingDirectory = $WorkingDirectory
$start.UseShellExecute = $false
$start.CreateNoWindow = $true
$start.RedirectStandardOutput = $true
$start.RedirectStandardError = $true
# Scope the project's cache to its build process; preserve shell and machine settings.
$start.Environment['NUGET_HTTP_CACHE_PATH'] = $cache
foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) }
$process = [Diagnostics.Process]::Start($start)
try {
    $stdout = $process.StandardOutput.ReadToEndAsync()
    $stderr = $process.StandardError.ReadToEndAsync()
    $process.WaitForExit()
    $output = $stdout.GetAwaiter().GetResult()
    $errors = $stderr.GetAwaiter().GetResult()
    if ($output.Length -gt 0) { Write-Host $output.TrimEnd() }
    if ($errors.Length -gt 0) { Write-Host $errors.TrimEnd() }
    if ($process.ExitCode -ne 0) { throw "Duxel Gallery build failed : $($process.ExitCode)" }
}
finally { $process.Dispose() }
