# FBA Project/Package Reference Switching Guide

> Last synced: 2026-03-09  
> Korean original: [fba-reference-guide.ko.md](fba-reference-guide.ko.md)

## Core Principles

- Current FBA samples use `#:package Duxel.$(platform).App@*-*`.
- End users run NuGet package mode via `dotnet run <file>.cs`.
- Contributors run local project-reference mode via `./run-fba.ps1`.

## Most Common Commands

```powershell
# NuGet package mode
dotnet run samples/fba/all_features.cs

# Local project-reference mode (NativeAOT by default)
./run-fba.ps1 samples/fba/all_features.cs

# Local project-reference mode (managed)
./run-fba.ps1 samples/fba/all_features.cs -Managed
```

## How `run-fba.ps1` Works

The script does not modify your original FBA file. It creates a temporary file and runs that.

1. Detects `#:package` and rewrites to `#:project`
	- `Duxel.App` → `src/Duxel.App`
	- `Duxel.Windows.App` or `Duxel.$(platform).App` (windows) → `src/Duxel.Windows.App`
2. On Windows path, normalizes entry call to `DuxelWindowsApp.Run(...)`
3. Defaults to `dotnet publish -p:PublishAot=true`
4. Uses `dotnet run` when `-Managed` is provided
5. Deletes temporary file after execution

## Main Options

| Option | Description |
|---|---|
| `-Managed` | Run managed instead of NativeAOT |
| `-RuntimeIdentifier win-x64` | Set NativeAOT RID |
| `-NoCache` | Pass no-cache option to `dotnet` |
| `-NoBuild` | Pass no-build option to `dotnet` |
| `-Platform windows` or `--platform windows` | Set platform value for templated package directives |

## Profile Switching

```powershell
$env:DUXEL_APP_PROFILE='render'
./run-fba.ps1 samples/fba/Duxel_perf_test_fba.cs -Managed
Remove-Item Env:DUXEL_APP_PROFILE
```

## Version Expression Examples

```csharp
#:package Duxel.$(platform).App@*-*   // latest prerelease included
#:package Duxel.$(platform).App@0.*-* // latest prerelease in 0.x
#:package Duxel.$(platform).App@*     // latest stable only
```
