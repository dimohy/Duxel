[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $Binary,
    [Parameter(Mandatory)][ValidateSet('Popover', 'PopupInput', 'Toolbar', 'SearchBox', 'List', 'ListRetirement', 'Markdown', 'Persona', 'Label', 'Radio', 'Toggle', 'Accordion', 'Avatar', 'Presence', 'Card', 'CardFloating', 'Breadcrumb', 'AvatarGroup', 'Dropdown', 'Combobox', 'Announcement', 'MessageBar')][string] $Profile,
    [Parameter(Mandatory)][string] $OutputDirectory,
    [string] $ExpectedFramework = '.NET 11.0.0',
    [ValidateRange(5, 60)][int] $TimeoutSeconds = 45
)

$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'POWERSHELL7_REQUIRED' }
$binaryPath = (Resolve-Path -LiteralPath $Binary).ProviderPath
$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $outputRoot) { throw 'OUTPUT_EXISTS' }
$extension = [IO.Path]::GetExtension($binaryPath)
if ($extension -notin @('.dll', '.exe')) { throw 'UNSUPPORTED_BINARY' }
$binaryRoot = [IO.Path]::GetDirectoryName($binaryPath).TrimEnd('\', '/')
if ($outputRoot.StartsWith($binaryRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'OUTPUT_INSIDE_BINARY_DIRECTORY'
}
if ([string]::IsNullOrWhiteSpace($ExpectedFramework)) { throw 'EXPECTED_FRAMEWORK_REQUIRED' }
$hostMode, $readyPrefix = switch ($Profile) {
    'Announcement' { 'uia-announcement-host'; 'UIA_ANNOUNCEMENT_HOST_READY ' }
    'MessageBar' { 'uia-messagebar-host'; 'UIA_ANNOUNCEMENT_HOST_READY ' }
    'Popover' { 'uia-popover-host'; 'UIA_POPOVER_HOST_READY ' }
    'PopupInput' { 'uia-popup-input-host'; 'UIA_POPUP_INPUT_HOST_READY ' }
    'Toolbar' { 'uia-toolbar-host'; 'UIA_TOOLBAR_HOST_READY ' }
    'SearchBox' { 'uia-searchbox-host'; 'UIA_SEARCHBOX_HOST_READY ' }
    'List' { 'uia-list-host'; 'UIA_LIST_HOST_READY ' }
    'Markdown' { 'uia-markdown-host'; 'UIA_MARKDOWN_HOST_READY ' }
    'ListRetirement' { 'uia-list-retirement-host'; 'UIA_LIST_HOST_READY ' }
    'Persona' { 'uia-persona-host'; 'UIA_PERSONA_HOST_READY ' }
    'Presence' { 'uia-presence-host'; 'UIA_PRESENCE_HOST_READY ' }
    'Avatar' { 'uia-avatar-host'; 'UIA_AVATAR_HOST_READY ' }
    'Card' { 'uia-card-host'; 'UIA_CARD_HOST_READY ' }
    'Combobox' { 'uia-combobox-host'; 'UIA_COMBOBOX_HOST_READY ' }
    'Dropdown' { 'uia-dropdown-host'; 'UIA_DROPDOWN_HOST_READY ' }
    'AvatarGroup' { 'uia-avatar-group-host'; 'UIA_AVATAR_GROUP_HOST_READY ' }
    'Breadcrumb' { 'uia-breadcrumb-host'; 'UIA_BREADCRUMB_HOST_READY ' }
    'CardFloating' { 'uia-card-floating-host'; 'UIA_CARD_FLOATING_HOST_READY ' }
    'Accordion' { 'uia-accordion-host'; 'UIA_ACCORDION_HOST_READY ' }
    'Label' { 'uia-label-host'; 'UIA_LABEL_HOST_READY ' }
    'Radio' { 'uia-radio-controls-host'; 'UIA_RADIO_HOST_READY ' }
    'Toggle' { 'uia-toggle-controls-host'; 'UIA_TOGGLE_HOST_READY ' }
}
$hostExecutable = if ($extension -eq '.dll') { (Get-Command dotnet -CommandType Application).Source } else { $binaryPath }
$binaryHash = (Get-FileHash -LiteralPath $binaryPath -Algorithm SHA256).Hash
$hostHash = (Get-FileHash -LiteralPath $hostExecutable -Algorithm SHA256).Hash
[IO.Directory]::CreateDirectory($outputRoot) | Out-Null
$encoding = [Text.UTF8Encoding]::new($false)
$owned = [Collections.Generic.List[object]]::new()
$clock = [Diagnostics.Stopwatch]::StartNew()
$status = 'failed'
$failure = $null
$results = [ordered]@{}

function Start-OwnedProcess([string] $Name, [string[]] $Arguments) {
    $info = [Diagnostics.ProcessStartInfo]::new($hostExecutable)
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    $info.WorkingDirectory = [IO.Path]::GetDirectoryName($binaryPath)
    if ($extension -eq '.dll') { $info.ArgumentList.Add('exec'); $info.ArgumentList.Add($binaryPath) }
    foreach ($argument in $Arguments) { $info.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $info
    $stdout = [IO.FileStream]::new((Join-Path $outputRoot ($Name + '.stdout.log')), [IO.FileMode]::CreateNew,
        [IO.FileAccess]::Write, [IO.FileShare]::ReadWrite, 1, $true)
    $stderr = [IO.FileStream]::new((Join-Path $outputRoot ($Name + '.stderr.log')), [IO.FileMode]::CreateNew,
        [IO.FileAccess]::Write, [IO.FileShare]::ReadWrite, 1, $true)
    $entry = [pscustomobject]@{ Name = $Name; Process = $process; Stdout = $stdout; Stderr = $stderr;
        OutTask = $null; ErrTask = $null; Started = $false; Terminated = $false; Id = $null; StartedUtc = $null; ExitCode = $null }
    $owned.Add($entry)
    if (-not $process.Start()) { throw ('START_FAILED : ' + $Name) }
    $entry.Started = $true
    $entry.Id = $process.Id
    $entry.StartedUtc = $process.StartTime.ToUniversalTime().ToString('o')
    $entry.OutTask = $process.StandardOutput.BaseStream.CopyToAsync($stdout)
    $entry.ErrTask = $process.StandardError.BaseStream.CopyToAsync($stderr)
    return $entry
}

function Assert-TimeBudget {
    if ($clock.Elapsed.TotalSeconds -ge $TimeoutSeconds) { throw 'PAIR_TIMEOUT' }
}

function Read-Result([string] $Name, [string] $Mode) {
    $path = Join-Path $outputRoot ($Name + '/result.json')
    if (-not (Test-Path -LiteralPath $path)) { throw ('RESULT_MISSING : ' + $Name) }
    $value = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
    if ($value.Framework -ne $ExpectedFramework -or $value.Order -ne $Mode -or $value.Passed -le 0 -or
        $value.Passed -ne $value.Checks.Count -or @($value.Checks | Where-Object { [string]::IsNullOrWhiteSpace($_) }).Count) {
        throw ('RESULT_CONTRACT_FAILED : ' + $Name)
    }
    return $value.Passed
}

try {
    $hostEntry = Start-OwnedProcess 'host' @((Join-Path $outputRoot 'host'), $hostMode)
    $ready = $false
    while (-not $ready) {
        Assert-TimeBudget
        if ($hostEntry.Process.HasExited) { throw 'HOST_EXITED_BEFORE_READY' }
        $path = Join-Path $outputRoot 'host.stdout.log'
        $ready = @(Get-Content -LiteralPath $path | Where-Object { $_.StartsWith($readyPrefix, [StringComparison]::Ordinal) }).Count -gt 0
        if (-not $ready) { Start-Sleep -Milliseconds 20 }
    }
    $clientEntry = Start-OwnedProcess 'client' @((Join-Path $outputRoot 'client'), 'uia-client-pid', [string]$hostEntry.Id)
    while (-not $clientEntry.Process.HasExited) { Assert-TimeBudget; Start-Sleep -Milliseconds 20 }
    if ($clientEntry.Process.ExitCode -ne 0) { throw 'CLIENT_TEST_EXIT' }
    while (-not $hostEntry.Process.HasExited) { Assert-TimeBudget; Start-Sleep -Milliseconds 20 }
    if ($hostEntry.Process.ExitCode -ne 0) { throw 'HOST_TEST_EXIT' }
    $results.host = Read-Result 'host' $hostMode
    $results.client = Read-Result 'client' 'uia-client-pid'
    if ((Get-FileHash -LiteralPath $binaryPath -Algorithm SHA256).Hash -ne $binaryHash -or
        (Get-FileHash -LiteralPath $hostExecutable -Algorithm SHA256).Hash -ne $hostHash) { throw 'EXECUTABLE_CHANGED' }
    $status = 'passed'
}
catch { $failure = $_.Exception.Message; throw }
finally {
    foreach ($entry in $owned) {
        try {
            if ($entry.Started) {
                if (-not $entry.Process.HasExited) { $entry.Process.Kill($true); $entry.Terminated = $true }
                if (-not $entry.Process.WaitForExit(5000)) { throw ('OWNED_PROCESS_NOT_TERMINAL : ' + $entry.Name) }
                $entry.ExitCode = $entry.Process.ExitCode
                [void]$entry.OutTask.GetAwaiter().GetResult()
                [void]$entry.ErrTask.GetAwaiter().GetResult()
            }
        }
        finally { $entry.Stdout.Dispose(); $entry.Stderr.Dispose(); $entry.Process.Dispose() }
    }
    $summary = [ordered]@{ status = $status; failure = $failure; profile = $Profile; expectedFramework = $ExpectedFramework;
        observedAtUtc = [DateTime]::UtcNow.ToString('o'); elapsedSeconds = $clock.Elapsed.TotalSeconds;
        binary = $binaryPath; binarySha256 = $binaryHash; hostExecutable = $hostExecutable; hostSha256 = $hostHash;
        results = $results; processes = @($owned | Select-Object Name, Id, StartedUtc, ExitCode, Terminated); wholeGoalComplete = $false }
    [IO.File]::WriteAllText((Join-Path $outputRoot 'summary.json'), ($summary | ConvertTo-Json -Depth 7), $encoding)
}
Write-Output ('PASS native accessibility : ' + $Profile)
