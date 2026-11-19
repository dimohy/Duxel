# Shared local publish ownership and retention. Records remain outside deleted outputs.
function Resolve-DuxelArtifactDirectory {
    param([string]$Root, [string]$Directory)
    $rootPath = [IO.Path]::GetFullPath($Root).TrimEnd([IO.Path]::DirectorySeparatorChar)
    $path = [IO.Path]::GetFullPath($Directory).TrimEnd([IO.Path]::DirectorySeparatorChar)
    if ($path -ne $rootPath -and -not $path.StartsWith($rootPath + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'BUILD_ARTIFACT_PATH_OUTSIDE_ROOT'
    }
    $cursor = $path
    while ($cursor.Length -ge $rootPath.Length) {
        if (Test-Path -LiteralPath $cursor) {
            if ((Get-Item -LiteralPath $cursor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'BUILD_ARTIFACT_REPARSE_POINT' }
        }
        if ($cursor -eq $rootPath) { break }
        $cursor = [IO.Path]::GetDirectoryName($cursor)
    }
    return $path
}

function Get-DuxelArtifactSnapshot {
    param([string]$Directory)
    $files = @(); $directories = @()
    foreach ($item in Get-ChildItem -LiteralPath $Directory -Recurse -Force) {
        if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'BUILD_ARTIFACT_REPARSE_POINT' }
        $relative = [IO.Path]::GetRelativePath($Directory, $item.FullName)
        if ($item.PSIsContainer) { $directories += $relative }
        else { $files += [pscustomobject]@{path=$relative;bytes=$item.Length;sha256=(Get-FileHash -LiteralPath $item.FullName -Algorithm SHA256).Hash} }
    }
    return [pscustomobject]@{files=$files;directories=$directories}
}

function Invoke-DuxelArtifactLock {
    param([string]$Root, [scriptblock]$Action)
    $rootPath = Resolve-DuxelArtifactDirectory -Root $Root -Directory $Root
    $key = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($rootPath.ToUpperInvariant())))
    $mutex = [Threading.Mutex]::new($false, "Local\DuxelPublish-$key")
    $acquired = $false
    try {
        try { $acquired = $mutex.WaitOne(0) }
        catch [Threading.AbandonedMutexException] {
            $acquired = $true
            throw 'BUILD_ARTIFACT_OWNER_ABANDONED'
        }
        if (-not $acquired) { throw 'BUILD_ARTIFACT_OWNERSHIP_BUSY' }
        & $Action
    } finally {
        if ($acquired) { $mutex.ReleaseMutex() }
        $mutex.Dispose()
    }
}

function Start-DuxelPublishArtifact {
    param([string]$Root, [string]$Directory, [string]$ExecutableName)
    $parameters = @{Root=$Root;Directory=$Directory;ExecutableName=$ExecutableName}
    Invoke-DuxelArtifactLock -Root $Root -Action { New-DuxelPublishArtifactRecord @parameters }
}

function New-DuxelPublishArtifactRecord {
    param([string]$Root, [string]$Directory, [string]$ExecutableName)
    $rootPath = Resolve-DuxelArtifactDirectory -Root $Root -Directory $Root
    $directoryPath = Resolve-DuxelArtifactDirectory -Root $Root -Directory $Directory
    $recordsPath = Join-Path $rootPath '.build-records'
    $null = Resolve-DuxelArtifactDirectory -Root $rootPath -Directory $recordsPath
    New-Item -ItemType Directory -Path $recordsPath -Force | Out-Null
    $records = @(Get-ChildItem -LiteralPath $recordsPath -File | Where-Object Name -Like 'publish-*.json' | ForEach-Object {
        Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json
    })
    $outputs = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    # Include unregistered legacy binaries in the limit, but never delete them.
    foreach ($file in Get-ChildItem -LiteralPath $rootPath -Recurse -File -Filter $ExecutableName) {
        $null = Resolve-DuxelArtifactDirectory -Root $rootPath -Directory $file.DirectoryName
        $null = $outputs.Add($file.DirectoryName)
    }
    foreach ($record in $records) {
        $null = Resolve-DuxelArtifactDirectory -Root $rootPath -Directory $record.directory
        if (Test-Path -LiteralPath $record.directory) { $null = $outputs.Add($record.directory) }
    }
    $needed = if ($outputs.Contains($directoryPath)) { 0 } else { 1 }
    $candidates = @($records | Where-Object { $_.status -eq 'completed' -and $_.directory -ne $rootPath -and $_.directory -ne $directoryPath -and $outputs.Contains($_.directory) } | Sort-Object completedUtc)
    foreach ($candidate in $candidates) {
        if ($outputs.Count + $needed -le 3) { break }
        $candidatePath = Resolve-DuxelArtifactDirectory -Root $rootPath -Directory $candidate.directory
        $active = @(Get-Process -ErrorAction Stop | Where-Object { $_.Path -and $_.Path.StartsWith($candidatePath + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) })
        if ($active.Count -gt 0) { continue }
        $snapshot = Get-DuxelArtifactSnapshot -Directory $candidatePath
        $expected = $candidate.snapshot | ConvertTo-Json -Depth 6 -Compress
        if (($snapshot | ConvertTo-Json -Depth 6 -Compress) -cne $expected) { continue }
        $bytes = ($snapshot.files | Measure-Object -Property bytes -Sum).Sum
        $cleanupPath = Join-Path $recordsPath ('cleanup-' + [guid]::NewGuid().ToString('N') + '.json')
        $cleanup = [ordered]@{target=$candidatePath;files=$snapshot.files;bytes=$bytes;executed=$false;createdUtc=[DateTime]::UtcNow.ToString('o')}
        $cleanup | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $cleanupPath
        # The complete snapshot, absolute boundary and active processes were checked above.
        Remove-Item -LiteralPath $candidatePath -Recurse -Force -ErrorAction Stop
        $cleanup.executed = $true
        $cleanup | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $cleanupPath
        $null = $outputs.Remove($candidatePath)
    }
    if ($outputs.Count + $needed -gt 3) { throw 'BUILD_ARTIFACT_CAPACITY_PROTECTED_OUTPUTS' }
    $key = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($directoryPath.ToUpperInvariant())))
    $recordPath = Join-Path $recordsPath "publish-$key.json"
    if (Test-Path -LiteralPath $recordPath) {
        $previous = Get-Content -LiteralPath $recordPath -Raw | ConvertFrom-Json
        if ($previous.status -eq 'running') { throw 'BUILD_ARTIFACT_ALREADY_RUNNING' }
    }
    New-Item -ItemType Directory -Path $directoryPath -Force | Out-Null
    $record = [pscustomobject]@{root=$rootPath;directory=$directoryPath;recordPath=$recordPath;status='running';ownerPid=$PID;startedUtc=[DateTime]::UtcNow.ToString('o');completedUtc=$null;snapshot=$null}
    $record | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $recordPath
    return $record
}

function Complete-DuxelPublishArtifact {
    param([object]$Record, [ValidateSet('completed','failed','cancelled')][string]$Status)
    Invoke-DuxelArtifactLock -Root $Record.root -Action { Set-DuxelPublishArtifactResult -Record $Record -Status $Status }
}

function Set-DuxelPublishArtifactResult {
    param([object]$Record, [ValidateSet('completed','failed','cancelled')][string]$Status)
    $null = Resolve-DuxelArtifactDirectory -Root $Record.root -Directory $Record.directory
    $Record.status = $Status
    $Record.completedUtc = [DateTime]::UtcNow.ToString('o')
    # Root-layout legacy output contains other retained builds; never make it deletable.
    if ($Record.directory -ne $Record.root) { $Record.snapshot = Get-DuxelArtifactSnapshot -Directory $Record.directory }
    $Record | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $Record.recordPath
}
