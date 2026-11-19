param([Parameter(Mandatory)][string]$ResultPath)
$ErrorActionPreference = 'Stop'
$repository = Split-Path $PSScriptRoot -Parent
. (Join-Path $repository 'scripts/BuildArtifactLifetime.ps1')
$fixtureParent = Join-Path $PSScriptRoot '.artifacts'
$fixture = Join-Path $fixtureParent ('build-lifetime-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture -Force | Out-Null
$checks = [Collections.Generic.List[string]]::new()
$ownedProcess = $null
function Stop-OwnedFixtureProcess {
    if ($null -eq $ownedProcess -or $ownedProcess.HasExited) { return }
    foreach ($child in Get-CimInstance Win32_Process -Filter "ParentProcessId=$($ownedProcess.Id)") {
        $handle = Get-Process -Id $child.ProcessId -ErrorAction Stop
        Stop-Process -Id $handle.Id -Force
        $handle.WaitForExit()
    }
    $ownedProcess.Refresh()
    if (-not $ownedProcess.HasExited) { Stop-Process -Id $ownedProcess.Id -Force }
    $ownedProcess.WaitForExit()
}
function Check([bool]$Condition, [string]$Name) {
    if (-not $Condition) { throw "BUILD_LIFETIME_TEST_FAILED: $Name" }
    $checks.Add($Name)
}
function New-Output([string]$Root, [string]$Name, [string]$Status='completed') {
    $record = Start-DuxelPublishArtifact -Root $Root -Directory (Join-Path $Root $Name) -ExecutableName 'lifetime.exe'
    Set-Content -LiteralPath (Join-Path $record.directory 'lifetime.exe') -Value $Name
    Complete-DuxelPublishArtifact -Record $record -Status $Status
    return $record
}
try {
    $normal = Join-Path $fixture 'normal'
    foreach ($index in 1..4) { $null = New-Output $normal "build-$index" }
    Check (-not (Test-Path -LiteralPath (Join-Path $normal 'build-1'))) 'fourth publish removes oldest unchanged completed output'
    Check (@(Get-ChildItem -LiteralPath $normal -Directory | Where-Object Name -Like 'build-*').Count -eq 3) 'successful publish retains exactly three outputs'
    $cleanup = @(Get-ChildItem -LiteralPath (Join-Path $normal '.build-records') -File | Where-Object Name -Like 'cleanup-*.json' | ForEach-Object {Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json})
    Check ($cleanup.Count -eq 1 -and $cleanup[0].executed -and $cleanup[0].bytes -gt 0) 'cleanup preserves exact plan and reclaimed-byte result'

    $lockedRoot = Join-Path $fixture 'locked'
    $lockKey = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($lockedRoot.ToUpperInvariant())))
    $mutex = [Threading.Mutex]::new($false, "Local\DuxelPublish-$lockKey")
    $null = $mutex.WaitOne()
    $job = $null
    try {
        $job = Start-Job -ScriptBlock {
            param($Module, $Root)
            . $Module
            try { Start-DuxelPublishArtifact -Root $Root -Directory (Join-Path $Root 'blocked') -ExecutableName 'lifetime.exe' }
            catch { $_.Exception.Message }
        } -ArgumentList (Join-Path $repository 'scripts/BuildArtifactLifetime.ps1'), $lockedRoot
        $null = Wait-Job -Job $job -Timeout 10
        if ($job.State -ne 'Completed') { throw 'OWNERSHIP_MUTEX_TEST_TIMEOUT' }
        $reply = @(Receive-Job -Job $job)
        Check ($reply.Count -eq 1 -and $reply[0] -eq 'BUILD_ARTIFACT_OWNERSHIP_BUSY' -and -not (Test-Path -LiteralPath $lockedRoot)) 'another process cannot mutate ownership while the root mutex is held'
    } finally {
        if ($null -ne $job) { Stop-Job -Job $job; Remove-Job -Job $job }
        $mutex.ReleaseMutex(); $mutex.Dispose()
    }

    $changed = Join-Path $fixture 'changed'
    foreach ($index in 1..3) { $null = New-Output $changed "build-$index" }
    Set-Content -LiteralPath (Join-Path $changed 'build-1/lifetime.exe') -Value 'user modified'
    $null = New-Output $changed 'build-4'
    Check ((Test-Path -LiteralPath (Join-Path $changed 'build-1')) -and -not (Test-Path -LiteralPath (Join-Path $changed 'build-2'))) 'modified output is protected while next eligible output is removed'

    $protected = Join-Path $fixture 'protected'
    $running = Start-DuxelPublishArtifact -Root $protected -Directory (Join-Path $protected 'running') -ExecutableName 'lifetime.exe'
    Set-Content -LiteralPath (Join-Path $running.directory 'lifetime.exe') -Value 'in progress'
    $null = New-Output $protected 'failed' 'failed'
    $null = New-Output $protected 'cancelled' 'cancelled'
    $rejected = $false
    try { $null = New-Output $protected 'fourth' } catch { if ($_.Exception.Message -ne 'BUILD_ARTIFACT_CAPACITY_PROTECTED_OUTPUTS') { throw }; $rejected = $true }
    Check ($rejected -and -not (Test-Path -LiteralPath (Join-Path $protected 'fourth'))) 'running failed and cancelled records cannot authorize deletion or a fourth build'
    $rejected = $false
    try { $null = Start-DuxelPublishArtifact -Root $protected -Directory $running.directory -ExecutableName 'lifetime.exe' } catch { if ($_.Exception.Message -ne 'BUILD_ARTIFACT_ALREADY_RUNNING') { throw }; $rejected=$true }
    Check $rejected 'concurrent publish cannot overwrite running ownership'
    Complete-DuxelPublishArtifact -Record $running -Status 'cancelled'
    $retry = Start-DuxelPublishArtifact -Root $protected -Directory (Join-Path $protected 'failed') -ExecutableName 'lifetime.exe'
    Set-Content -LiteralPath (Join-Path $retry.directory 'lifetime.exe') -Value 'successful retry'
    Complete-DuxelPublishArtifact -Record $retry -Status 'completed'
    Check ((Get-Content -LiteralPath $retry.recordPath -Raw | ConvertFrom-Json).status -eq 'completed') 'retry replaces failed ownership without adding a retained output'

    $rejected = $false
    try { $null = Start-DuxelPublishArtifact -Root $normal -Directory (Join-Path $fixture 'outside') -ExecutableName 'lifetime.exe' } catch { if ($_.Exception.Message -ne 'BUILD_ARTIFACT_PATH_OUTSIDE_ROOT') { throw }; $rejected=$true }
    Check ($rejected -and -not (Test-Path -LiteralPath (Join-Path $fixture 'outside'))) 'outside path fails before output creation'
    $junction = Join-Path $normal 'junction'
    $null = New-Item -ItemType Junction -Path $junction -Target $changed
    try {
        $rejected = $false
        try { $null = Resolve-DuxelArtifactDirectory -Root $normal -Directory (Join-Path $junction 'child') } catch { if ($_.Exception.Message -ne 'BUILD_ARTIFACT_REPARSE_POINT') { throw }; $rejected=$true }
        Check $rejected 'junction cannot redirect ownership outside the declared root'
    } finally { Remove-Item -LiteralPath $junction -Force }

    $legacy = Join-Path $fixture 'legacy'
    New-Item -ItemType Directory -Path (Join-Path $legacy 'old') -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $legacy 'old/lifetime.exe') -Value 'unregistered original'
    $null = New-Output $legacy 'new-1'
    $null = New-Output $legacy 'new-2'
    $null = New-Output $legacy 'new-3'
    Check ((Test-Path -LiteralPath (Join-Path $legacy 'old/lifetime.exe')) -and -not (Test-Path -LiteralPath (Join-Path $legacy 'new-1'))) 'legacy outputs count toward capacity but are never adopted for deletion'

    $active = Join-Path $fixture 'active'
    $record = Start-DuxelPublishArtifact -Root $active -Directory (Join-Path $active 'build-1') -ExecutableName 'lifetime.exe'
    Copy-Item -LiteralPath (Join-Path $env:SystemRoot 'System32/cmd.exe') -Destination (Join-Path $record.directory 'lifetime.exe')
    Complete-DuxelPublishArtifact -Record $record -Status 'completed'
    $ownedProcess = Start-Process -FilePath (Join-Path $record.directory 'lifetime.exe') -ArgumentList @('/c','ping -n 60 127.0.0.1 > nul') -WindowStyle Hidden -PassThru
    Start-Sleep -Milliseconds 150
    $ownedProcess.Refresh()
    Check (-not $ownedProcess.HasExited) 'owned active-image fixture is actually running'
    foreach ($index in 2..4) { $null = New-Output $active "build-$index" }
    Check ((Test-Path -LiteralPath $record.directory) -and -not (Test-Path -LiteralPath (Join-Path $active 'build-2'))) 'running image is protected despite completed publish status'
    Stop-OwnedFixtureProcess
    $ownedProcess = $null

    $record = [ordered]@{passed=$checks.Count;checks=@($checks.ToArray());fixture=$fixture;completedUtc=[DateTime]::UtcNow.ToString('o');temporaryOutputsRemoved=$false}
    $resultDirectory = Split-Path -Parent ([IO.Path]::GetFullPath($ResultPath))
    New-Item -ItemType Directory -Path $resultDirectory -Force | Out-Null
    $record | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $ResultPath
    $resolved = [IO.Path]::GetFullPath($fixture)
    if (-not $resolved.StartsWith([IO.Path]::GetFullPath($fixtureParent) + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($resolved) -notmatch '^build-lifetime-[a-f0-9]{32}$') { throw 'FIXTURE_CLEANUP_BOUNDARY' }
    # Preserve ownership, cleanup plans and hashes in the result before removing test binaries.
    $record['ownershipAndCleanupRecords'] = @(Get-ChildItem -LiteralPath $resolved -Recurse -File | Where-Object Extension -eq '.json' | ForEach-Object {Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json})
    $record | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $ResultPath
    Remove-Item -LiteralPath $resolved -Recurse -Force
    $record.temporaryOutputsRemoved = $true
    $record | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $ResultPath
    Write-Output "PASS build artifact lifetime $($checks.Count)/$($checks.Count)"
} finally {
    Stop-OwnedFixtureProcess
}
