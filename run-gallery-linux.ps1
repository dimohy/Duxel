[CmdletBinding()]
param(
    [string]$Distribution = 'Ubuntu',
    [ValidateCount(5, 5)]
    [string[]]$Fonts,
    [switch]$PrivateAccessibilityBus,
    [ValidateNotNullOrEmpty()]
    [string]$PublishedExecutable,
    [string]$LinuxDotnet,
    [ValidateSet('net8.0', 'net9.0', 'net10.0', 'net11.0')]
    [string]$TargetFramework = 'net11.0',
    [switch]$Managed,
    [ValidateNotNull()]
    [ValidateRange(1, 32)]
    [Nullable[int]]$SoftwareRasterThreads,
    [string[]]$SampleArguments = @()
)

$ErrorActionPreference = 'Stop'
$wsl = (Get-Command wsl.exe -CommandType Application | Select-Object -First 1).Source
$distributionArgs = @('-d', $Distribution)

foreach ($font in $Fonts) {
    & $wsl @distributionArgs --exec test -r $font
    if ($LASTEXITCODE -ne 0) { throw "Linux Gallery font is not readable: $font" }
}
if (-not $Fonts) {
    & $wsl @distributionArgs --exec fc-match --version *> $null
    if ($LASTEXITCODE -ne 0) { throw 'Automatic Linux Gallery fonts require fc-match. Install fontconfig or pass five Linux paths with -Fonts.' }
}
$display = & $wsl @distributionArgs --exec printenv DISPLAY
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($display)) {
    throw 'Linux Gallery requires an X11 DISPLAY, such as WSLg.'
}

$linuxExecutable = $PublishedExecutable
if (-not $PSBoundParameters.ContainsKey('PublishedExecutable')) {
    $sdkVersion = (Get-Content -LiteralPath (Join-Path $PSScriptRoot 'global.json') -Raw | ConvertFrom-Json).sdk.version
    $linuxHome = & $wsl @distributionArgs --exec printenv HOME
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($linuxHome)) { throw 'Could not resolve the WSL user home.' }
    $linuxHome = $linuxHome.Trim()
    $linuxRepository = & $wsl @distributionArgs --exec wslpath -u $PSScriptRoot
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($linuxRepository)) { throw 'Could not resolve the repository inside WSL.' }
    $linuxRepository = $linuxRepository.Trim()

    if (-not $PSBoundParameters.ContainsKey('LinuxDotnet')) {
        $LinuxDotnet = "$linuxHome/.cache/duxel/toolchains/dotnet-$sdkVersion/dotnet"
    }
    if ([string]::IsNullOrWhiteSpace($LinuxDotnet)) { throw 'LinuxDotnet requires an explicit executable path.' }
    & $wsl @distributionArgs --exec test -x $LinuxDotnet
    if ($LASTEXITCODE -ne 0) {
        throw "Pinned Linux SDK is unavailable: $LinuxDotnet. Specify -LinuxDotnet with SDK $sdkVersion."
    }
    $actualSdk = & $wsl @distributionArgs --cd $linuxRepository --exec $LinuxDotnet --version
    if ($LASTEXITCODE -ne 0 -or "$actualSdk".Trim() -ne $sdkVersion) {
        throw "LinuxDotnet must provide SDK $sdkVersion; found: $actualSdk"
    }

    $machine = & $wsl @distributionArgs --exec uname -m
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($machine)) {
        throw 'Could not determine the WSL machine architecture.'
    }
    $runtimeIdentifier = switch ($machine.Trim()) {
        'x86_64' { 'linux-x64' }
        'aarch64' { 'linux-arm64' }
        default { throw "Unsupported Linux Gallery architecture: $machine" }
    }

    $repositoryHash = [Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($linuxRepository))
    $repositoryKey = [Convert]::ToHexString($repositoryHash).Substring(0, 12).ToLowerInvariant()
    $publishAot = if ($Managed) { 'false' } else { 'true' }
    $mode = if ($Managed) { 'managed' } else { 'native' }
    $buildRoot = "$linuxHome/.cache/duxel/gallery-source-$repositoryKey/$sdkVersion/$TargetFramework/$mode"
    $runId = [Guid]::NewGuid().ToString('N')
    $publishDirectory = "$buildRoot/runs/$runId"
    $publishArgs = @(
        '--cd', $linuxRepository, '--exec', $LinuxDotnet,
        'publish', 'samples/Duxel.Gallery.Linux/Duxel.Gallery.Linux.csproj',
        '-c', 'Release', '-f', $TargetFramework, '-r', $runtimeIdentifier, '--self-contained', 'true',
        '--disable-build-servers', '-o', $publishDirectory, '-warnaserror', '-m:1', '-v:minimal',
        "-p:PublishAot=$publishAot", '-p:BuildInParallel=false',
        "-p:ArtifactsPath=$buildRoot/build"
    )
    & $wsl @distributionArgs @publishArgs
    if ($LASTEXITCODE -ne 0) { throw "Linux Gallery $mode publish failed ($LASTEXITCODE)." }
    $linuxExecutable = "$publishDirectory/Duxel.Gallery.Linux"
}

& $wsl @distributionArgs --exec test -x $linuxExecutable
if ($LASTEXITCODE -ne 0) { throw "Linux Gallery executable is unavailable: $linuxExecutable" }

$runArgs = @('--exec')
if ($PSBoundParameters.ContainsKey('SoftwareRasterThreads')) {
    $runArgs += @('env', "LP_NUM_THREADS=$SoftwareRasterThreads")
}
if ($PrivateAccessibilityBus) { $runArgs += @('dbus-run-session', '--') }
$runArgs += @($linuxExecutable) + @($Fonts | Where-Object { $_ }) + $SampleArguments
& $wsl @distributionArgs @runArgs
if ($LASTEXITCODE -ne 0) { throw "Linux Gallery exited with code $LASTEXITCODE." }
