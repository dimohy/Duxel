[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$GraphicsBinary,
    [Parameter(Mandatory)][string]$ControlBinary,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$ExpectedFramework = '.NET 11.0.0',
    [string]$DotNetHost,
    [string[]]$Suite,
    [ValidateRange(1,60)][int]$SuiteTimeoutSeconds = 60
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'POWERSHELL_VERSION: PowerShell 7 is required.' }
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$manifestPath = Join-Path $repo 'tests/Duxel.Graphics.Tests/contract-suites.json'
$programPath = Join-Path $repo 'tests/Duxel.Graphics.Tests/Program.cs'
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json -AsHashtable
if ($manifest.schemaVersion -ne 1) { throw 'MANIFEST_VERSION: Unsupported contract manifest.' }
$graphics = @($manifest.graphicsContracts)
$excluded = @($manifest.excludedGraphicsModes | ForEach-Object { $_.id })
$classified = @($graphics + $excluded)
$discovered = @([regex]::Matches((Get-Content -LiteralPath $programPath -Raw), '"([a-z][a-z0-9-]*-only)"') |
    ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
if (@($classified | Sort-Object -Unique).Count -ne $classified.Count -or
    @(Compare-Object -ReferenceObject $discovered -DifferenceObject $classified).Count) {
    throw 'CLASSIFICATION_MISMATCH: Every graphics *-only mode must be explicitly classified.'
}
$all = @($graphics + $manifest.controlContract)
foreach ($id in $all) { if ($id -notmatch '^[a-z][a-z0-9-]+$') { throw 'INVALID_SUITE_ID: Invalid contract identifier.' } }
if (@($all | Sort-Object -Unique).Count -ne $all.Count) { throw 'DUPLICATE_SUITE: Duplicate contract identifier.' }
if ($PSBoundParameters.ContainsKey('Suite')) {
    if (!$Suite.Count -or @($Suite | Where-Object { $_ -notin $all }).Count -or
        @($Suite | Sort-Object -Unique).Count -ne $Suite.Count) { throw 'UNKNOWN_SUITE: Select distinct declared contracts.' }
    $selected = @($all | Where-Object { $_ -in $Suite })
} else { $selected = $all }

function Resolve-ValidationBinary([string]$Path, [string]$Name) {
    if (!(Test-Path -LiteralPath $Path -PathType Leaf)) { throw "INPUT_BINARY_MISSING: $Name" }
    $item = Get-Item -LiteralPath $Path
    if ($item.BaseName -ne $Name -or $item.Extension -notin @('.dll','.exe')) { throw "INPUT_BINARY_KIND: $Name" }
    return $item.FullName
}
$graphicsPath = Resolve-ValidationBinary $GraphicsBinary 'Duxel.Graphics.Tests'
$controlPath = Resolve-ValidationBinary $ControlBinary 'Duxel.Control.Tests'
if ([string]::IsNullOrWhiteSpace($ExpectedFramework)) { throw 'EXPECTED_RUNTIME_MISSING: Specify an exact framework description.' }
if ([IO.Path]::GetExtension($graphicsPath) -eq '.dll' -or [IO.Path]::GetExtension($controlPath) -eq '.dll') {
    if (!$DotNetHost) { $DotNetHost = (Get-Command dotnet -CommandType Application).Source }
    if (!(Test-Path -LiteralPath $DotNetHost -PathType Leaf)) { throw 'DOTNET_HOST_MISSING: Managed validation requires a host.' }
    $DotNetHost = (Get-Item -LiteralPath $DotNetHost).FullName
}
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw 'OUTPUT_EXISTS: Use a fresh output directory; previous evidence is preserved.' }
$binaryRoots = @((Split-Path -Parent $graphicsPath),(Split-Path -Parent $controlPath) | Sort-Object -Unique)
foreach ($directory in $binaryRoots) {
    if ($output.Equals($directory,[StringComparison]::OrdinalIgnoreCase) -or
        $output.StartsWith($directory+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) {
        throw 'OUTPUT_IN_INPUT: Results must be outside validation binary directories.'
    }
}
$fontRoot = [Environment]::GetFolderPath([Environment+SpecialFolder]::Fonts)
$fontPaths = foreach ($name in @('segoeui.ttf','seguisb.ttf','segoeuib.ttf','consola.ttf','malgun.ttf','malgunbd.ttf','seguiemj.ttf')) {
    $path = Join-Path $fontRoot $name
    if (!(Test-Path -LiteralPath $path -PathType Leaf)) { throw "FONT_FIXTURE_MISSING: $name" }
    $path
}
function Get-InputSnapshot {
    $paths = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($path in @($manifestPath,$programPath,$PSCommandPath) + $fontPaths) { $null = $paths.Add($path) }
    if ($DotNetHost) { $null = $paths.Add($DotNetHost) }
    foreach ($directory in $binaryRoots) {
        foreach ($file in Get-ChildItem -LiteralPath $directory -Recurse -File) {
            if ($file.Extension -in @('.dll','.exe','.json','.svg','.rgba','.spv','.ttf','.otf','.ttc')) { $null = $paths.Add($file.FullName) }
        }
    }
    foreach ($path in $paths | Sort-Object) {
        [ordered]@{path=$path;bytes=(Get-Item -LiteralPath $path).Length;sha256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash}
    }
}
$inputs = @(Get-InputSnapshot)
$before = ConvertTo-Json -InputObject $inputs -Depth 5 -Compress
New-Item -ItemType Directory -Path $output | Out-Null
$inputs | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $output 'inputs.json') -Encoding utf8

function Invoke-Contract([string]$Id) {
    $directory = Join-Path $output $Id
    New-Item -ItemType Directory -Path $directory | Out-Null
    $binary = if ($Id -eq $manifest.controlContract) { $controlPath } else { $graphicsPath }
    $resultPath = Join-Path $directory 'result.json'
    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.WorkingDirectory = Split-Path -Parent $binary
    if ([IO.Path]::GetExtension($binary) -eq '.dll') {
        $start.FileName = $DotNetHost
        $start.ArgumentList.Add('exec')
        $start.ArgumentList.Add($binary)
    } else { $start.FileName = $binary }
    if ($Id -eq $manifest.controlContract) { $start.ArgumentList.Add($resultPath) }
    else { $start.ArgumentList.Add($directory); $start.ArgumentList.Add($Id) }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $start
    $stdout = [IO.File]::Create((Join-Path $directory 'stdout.log'))
    $stderr = [IO.File]::Create((Join-Path $directory 'stderr.log'))
    $record = [ordered]@{id=$Id;binary=$binary;startedAtUtc=[DateTime]::UtcNow.ToString('o');pid=$null;exitCode=$null;timedOut=$false;status='failed';failureId=$null;passed=0;framework=$null}
    try {
        if (!$process.Start()) { throw 'PROCESS_START: Contract process did not start.' }
        $record.pid = $process.Id
        $copyOut = $process.StandardOutput.BaseStream.CopyToAsync($stdout)
        $copyErr = $process.StandardError.BaseStream.CopyToAsync($stderr)
        if (!$process.WaitForExit($SuiteTimeoutSeconds * 1000)) {
            $record.timedOut = $true
            $process.Kill($true)
            if (!$process.WaitForExit(5000)) { throw 'PROCESS_TERMINATION: Owned timed-out process did not terminate.' }
        }
        [Threading.Tasks.Task]::WhenAll([Threading.Tasks.Task[]]@($copyOut,$copyErr)).GetAwaiter().GetResult()
        $record.exitCode = $process.ExitCode
        if ($record.timedOut) { $record.failureId = 'SUITE_TIMEOUT' }
        elseif ($record.exitCode -ne 0) { $record.failureId = 'TEST_EXIT' }
        elseif (!(Test-Path -LiteralPath $resultPath -PathType Leaf)) { $record.failureId = 'RESULT_MISSING' }
        else {
            $result = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json -AsHashtable
            $record.framework = $result.Framework
            if ($result.Framework -ne $ExpectedFramework) { $record.failureId = 'RUNTIME_MISMATCH' }
            elseif (!$result.ContainsKey('Passed') -or !$result.ContainsKey('Checks') -or
                ($result.Passed -isnot [long] -and $result.Passed -isnot [int]) -or
                $result.Passed -lt 1 -or $result.Passed -ne @($result.Checks).Count) { $record.failureId = 'RESULT_SCHEMA' }
            elseif ($Id -ne $manifest.controlContract -and $result.Order -ne $Id) { $record.failureId = 'RESULT_MODE' }
            elseif (@($result.Checks | Where-Object { $_ -isnot [string] -or [string]::IsNullOrWhiteSpace($_) }).Count) { $record.failureId = 'RESULT_CHECKS' }
            else {
                $failureFile = Join-Path $directory 'failures.txt'
                if ((Test-Path -LiteralPath $failureFile) -and ![string]::IsNullOrWhiteSpace([IO.File]::ReadAllText($failureFile))) {
                    $record.failureId = 'ASSERTIONS_FAILED'
                } else { $record.status = 'passed'; $record.passed = $result.Passed }
            }
        }
    } finally {
        try {
            if ($record.pid -and !$process.HasExited) {
                $record.failureId = 'PROCESS_INTERRUPTED'
                $process.Kill($true)
                if (!$process.WaitForExit(5000)) { throw 'PROCESS_TERMINATION: Interrupted owned process did not terminate.' }
            }
        } finally {
            $record['finishedAtUtc'] = [DateTime]::UtcNow.ToString('o')
            $stdout.Dispose(); $stderr.Dispose(); $process.Dispose()
            $record | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $directory 'process.json') -Encoding utf8
        }
    }
    return $record
}

$records = [Collections.Generic.List[object]]::new()
$status = 'running'
$currentSuite = $null
$failure = $null
try {
    foreach ($id in $selected) {
        $currentSuite = $id
        $record = Invoke-Contract $id
        $records.Add($record)
        if ($record.status -ne 'passed') { throw ("CONTRACT_FAILED: {0}/{1}; see process.json and stderr.log." -f $id,$record.failureId) }
        Write-Output ("PASS {0} : {1}" -f $id,$record.passed)
    }
    $after = ConvertTo-Json -InputObject @(Get-InputSnapshot) -Depth 5 -Compress
    if ($before -ne $after) { throw 'INPUTS_CHANGED: Validation inputs changed during execution.' }
    $status = 'passed'
} catch {
    $status = 'failed'
    $failure = $_.Exception.Message
    throw
} finally {
    $passedSuites = @($records | Where-Object { $_.status -eq 'passed' })
    $assertionExecutions = 0L
    foreach ($entry in $passedSuites) { $assertionExecutions += $entry.passed }
    [ordered]@{
        observedAtUtc=[DateTime]::UtcNow.ToString('o');status=$status;expectedFramework=$ExpectedFramework
        suiteTimeoutSeconds=$SuiteTimeoutSeconds;dotNetHost=$DotNetHost;osVersion=[Environment]::OSVersion.VersionString
        defaultSuiteCount=$all.Count;requestedSuites=$selected;passedSuites=$passedSuites.Count
        completedDefaultSuite=($status -eq 'passed' -and $selected.Count -eq $all.Count)
        notRun=@($selected | Where-Object { $_ -notin @($records | ForEach-Object { $_.id }) })
        assertionExecutions=$assertionExecutions;assertionsMayRepeat=$true
        currentSuite=$currentSuite;failure=$failure;suites=@($records.ToArray())
        excludedGraphicsModes=$manifest.excludedGraphicsModes;excludedScopes=$manifest.excludedScopes
        fullGoalComplete=$false
    } | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $output 'summary.json') -Encoding utf8
}
