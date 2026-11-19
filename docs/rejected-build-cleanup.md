# Current cleanup outcome — 2026-11-11

Moved22 completed, regenerable output directories containing333 files (69,981,601B, about66.7MiB) to pending deletion. Automatic approval review rejected direct deletion with `blocked by policy`; that process did not start. Actual deletion count is0. This same-volume move is not disk-space reclamation.

Pending folder: `W:\MyWorks\Duxel\artifacts\must_delete_20261111_161030`

Verified absence of all22 original paths and exact sizes/SHA-256 of all333 destination files. Mapping, execution and verification records are outside the pending folder under `artifacts/completed-cleanup-2026-11-11/`: `move-plan.json`, `execution.json`, `verification.json`. The user may delete the pending folder directly in Explorer. Active outputs, protected reproduction baselines and evaluation logs/results/images were excluded from this target list.

The following is historical. Do not rerun its deletion block against the former paths after this move.

---

# Manually cleaning rejected temporary builds

Under the user's completion rule approved on 2026-11-04, deletions rejected by automatic approval review are handed over as explicit cleanup instructions. This document is not evidence of deletion. The pending set contains 22 old validation `bin`/`obj` and dedicated comparison output directories, totaling 69,981,601 logical file bytes (about 66.7MiB).

Exact targets and original file hashes are fixed in `artifacts/overlay-layers-2026-11-04/owned-cleanup-plan.json`. Source files, raw CSV/JSON measurements, logs, images and the distribution executable are excluded. Preserve `baseline-native-bin` and `before-scratch-native-bin` as comparison reproduction inputs. Paths rejected in other earlier tasks are outside this list.

In PowerShell 7, while no project build or test is running, inspect the targets first:

```powershell
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath 'W:\MyWorks\Duxel'
. .\scripts\BuildArtifactLifetime.ps1
$cleanupRoot = [IO.Path]::GetFullPath('artifacts/overlay-layers-2026-11-04')
$cleanupPlanPath = Join-Path $cleanupRoot 'owned-cleanup-plan.json'
if ((Get-FileHash -LiteralPath $cleanupPlanPath).Hash -ne
    '006BF2053E5371A09FF7E141D5564CC847A3B0917663E61161532EE3E3C9EE57') {
    throw 'CLEANUP_PLAN_CHANGED'
}
$cleanupPlan = Get-Content -LiteralPath $cleanupPlanPath -Raw | ConvertFrom-Json
$cleanupPlan.targets | Select-Object target, bytes
```

The following block performs deletion in the same terminal. It verifies boundaries, reparse points, current file hashes and active processes, stopping on changes. Do not change expected hashes or paths merely to bypass a failed check. If some targets were already deleted, regenerate a plan from the actual state before rerunning.

```powershell
$verifiedTargets = foreach ($entry in $cleanupPlan.targets) {
    $target = Resolve-DuxelArtifactDirectory -Root $cleanupRoot -Directory $entry.target
    if ($target -eq $cleanupRoot) { throw 'CLEANUP_ROOT_NOT_ALLOWED' }
    $snapshot = Get-DuxelArtifactSnapshot -Directory $target
    if (($snapshot | ConvertTo-Json -Depth 7 -Compress) -cne
        ($entry.snapshot | ConvertTo-Json -Depth 7 -Compress)) {
        throw "CLEANUP_CONTENT_CHANGED: $target"
    }
    [pscustomobject]@{ path = $target; entry = $entry }
}
if (@($verifiedTargets).Count -ne 22) { throw 'CLEANUP_TARGET_COUNT_CHANGED' }
$drive = [IO.DriveInfo]::new([IO.Path]::GetPathRoot($cleanupRoot))
$freeBefore = $drive.AvailableFreeSpace
$removed = [Collections.Generic.List[object]]::new()
try {
    foreach ($verified in $verifiedTargets) {
        $target = Resolve-DuxelArtifactDirectory -Root $cleanupRoot -Directory $verified.path
        $active = @(Get-CimInstance Win32_Process | Where-Object {
            ($_.ExecutablePath -and $_.ExecutablePath.StartsWith(
                $target + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) -or
            ($_.Name -eq 'dotnet.exe' -and $_.CommandLine -and
                $_.CommandLine.Contains($target, [StringComparison]::OrdinalIgnoreCase))
        })
        if ($active.Count) { throw "CLEANUP_ACTIVE_PROCESS: $target" }
        $snapshot = Get-DuxelArtifactSnapshot -Directory $target
        if (($snapshot | ConvertTo-Json -Depth 7 -Compress) -cne
            ($verified.entry.snapshot | ConvertTo-Json -Depth 7 -Compress)) {
            throw "CLEANUP_CONTENT_CHANGED: $target"
        }
        Remove-Item -LiteralPath $target -Recurse -Force
        if (Test-Path -LiteralPath $target) { throw "CLEANUP_TARGET_REMAINS: $target" }
        $removed.Add([pscustomobject]@{ target = $target; bytes = $verified.entry.bytes })
    }
}
finally {
    $drive = [IO.DriveInfo]::new([IO.Path]::GetPathRoot($cleanupRoot))
    [ordered]@{
        completedUtc = [DateTime]::UtcNow.ToString('o')
        complete = $removed.Count -eq 22
        removedCount = $removed.Count
        removedLogicalBytes = ($removed | Measure-Object bytes -Sum).Sum
        freeBytesBefore = $freeBefore
        freeBytesAfter = $drive.AvailableFreeSpace
        targets = @($removed.ToArray())
    } | ConvertTo-Json -Depth 5 |
        Set-Content -LiteralPath (Join-Path $cleanupRoot 'user-cleanup-result.json')
}
```

After actual execution, inspect `complete`, `removedCount` and remaining paths in `user-cleanup-result.json`. `removedLogicalBytes` is the sum of removed file sizes. Free-space changes also reflect other processes, compression and filesystem allocation granularity; they need not match logical file sizes. If policy rejects the command or files remain in use, record the reason without reporting successful deletion.

The PowerShell syntax and read-only preflight were checked; deletion commands were not executed. [AGENTS.md](../AGENTS.md) records that rejected deletion alone must not stop independent development or verification.
