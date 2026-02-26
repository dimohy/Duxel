# FBA Project/Package Switching Guide

> Synced: 2026-02-26  
> Korean original: [fba-reference-guide.md](fba-reference-guide.md)

## Overview

Current FBA samples use:

```csharp
#:package Duxel.$(platform).App@*-*
```

Execution modes:

- End users: `dotnet run <sample>.cs` (NuGet package path)
- Contributors: `./run-fba.ps1 <sample>.cs` (local project-reference path)

## Common Commands

```powershell
dotnet run samples/fba/all_features.cs
./run-fba.ps1 samples/fba/all_features.cs
./run-fba.ps1 samples/fba/all_features.cs -Managed
```

## What `run-fba.ps1` Does

1. Detects `#:package` and maps it to local `#:project` references.
2. For Windows path, normalizes entry call to `DuxelWindowsApp.Run(...)` in temp file.
3. Runs NativeAOT publish by default.
4. Uses `dotnet run` when `-Managed` is specified.
5. Deletes temp file after execution.

## Frequently Used Options

| Option | Description |
|---|---|
| `-Managed` | Run managed instead of NativeAOT |
| `-RuntimeIdentifier win-x64` | NativeAOT RID override |
| `-NoCache` | Pass no-cache behavior to dotnet command |
| `-NoBuild` | Skip build where supported |
| `-Platform windows` / `--platform windows` | Resolve templated package platform |

## Profile Switch

```powershell
$env:DUXEL_APP_PROFILE='render'
./run-fba.ps1 samples/fba/Duxel_perf_test_fba.cs -Managed
Remove-Item Env:DUXEL_APP_PROFILE
```
