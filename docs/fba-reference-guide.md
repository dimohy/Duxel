# FBA Project/Package Reference Switching Guide

> Last synced: 2026-07-20
> Korean original: [fba-reference-guide.ko.md](fba-reference-guide.ko.md)

## Core Principles

- Duxel `0.2.10-preview` packages target `net8.0`, `net9.0`, and `net10.0`; FBA samples target `net10.0` because file-based apps require the .NET 10 SDK.
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
5. Launches the NativeAOT executable after publish unless `-NoLaunch` is provided
6. Waits for the NativeAOT executable to exit when `-Wait` is provided
7. Deletes temporary file after execution

## Main Options

| Option | Description |
|---|---|
| `-Managed` | Run managed instead of NativeAOT |
| `-RuntimeIdentifier win-x64` | Set NativeAOT RID |
| `-NoCache` | Pass no-cache option to `dotnet` |
| `-NoBuild` | Pass no-build option to `dotnet` |
| `-NoLaunch` | Publish NativeAOT output without launching it |
| `-Wait` | Wait for launched NativeAOT executable to exit; useful for automated benchmarks |
| `-Platform windows` or `--platform windows` | Set platform value for templated package directives |

## NativeAOT Benchmark Collection

```powershell
$env:DUXEL_SAMPLE_AUTO_EXIT_SECONDS='5'
$env:DUXEL_PERF_TEST_LEVEL='2'
$env:DUXEL_PERF_LOG_PATH='artifacts\perf_2d_render_fps.log'
./run-fba.ps1 samples/fba/perf_2d_render_fps.cs -Wait
Get-Content artifacts\perf_2d_render_fps.log
Remove-Item Env:DUXEL_SAMPLE_AUTO_EXIT_SECONDS
Remove-Item Env:DUXEL_PERF_TEST_LEVEL
Remove-Item Env:DUXEL_PERF_LOG_PATH
```

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
