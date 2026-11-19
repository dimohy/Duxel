# .NET 11 comparison

Run from this directory to select the exact .NET 11.0.100 SDK for reproducible comparison.

```powershell
dotnet --version
dotnet run --project ../Duxel.Performance.Bench -c Release -p:DuxelBenchmarkFramework=net11.0 -- ../../artifacts/cpu-net11.json
dotnet run --project ../Duxel.Rendering.Tests -c Release -p:DuxelBenchmarkFramework=net11.0-windows -p:DuxelValidationHooks=true -- ../../artifacts/gpu-net11
../../run-fba.ps1 ../../samples/fba/dynamic_3d_layers_bench.cs -TargetFramework net11.0-windows7.0 -Managed -NoCache
```

The sample paths are resolved from this working directory. Core libraries keep their published TFMs. For new runtime comparisons, use .NET 11.0.0 in both managed and NativeAOT runs and record the SDK, runtime and phase order.

## Historical runtime comparison (2026-11-03)

`artifacts/perf-2026-11-03/runtime-current` records12 independent executions,three for each NET10/NET11 Managed/NativeAOT configuration,with72 audited phase results/raw files. SDK10.0.301/runtime10.0.9 and SDK11.0.100-rc.3.26522.148/runtime11.0.0-rc.3.26522.148 were confirmed. See `docs/dynamic-3d-layers.md` for distributions and limitations. Dynamic stress uses MSAA1,serial rendering and UI-frame intervals;it does not establish the ERD accepted-presentation1000FPS target.

From this directory, run a new managed .NET 11.0.0 comparison with a fresh output path:

```powershell
$resultPath = [IO.Path]::GetFullPath((Join-Path $PWD '../../artifacts/dynamic-net11-recheck.json'))
if (Test-Path -LiteralPath $resultPath) { throw 'Preserve prior results; select another output name.' }
$env:DUXEL_3D_BENCH_OUT = $resultPath
$env:DUXEL_3D_BENCH_SECONDS = '1.5'
$env:DUXEL_3D_BENCH_REVERSE = '0'
$env:DUXEL_VK_PROFILE = '1'
$env:DUXEL_VK_GPU_PROFILE = '1'
$env:DUXEL_VK_PROFILE_EVERY = '64'
$env:DUXEL_VK_PROFILE_OUT = $resultPath + '.vk.log'
../../run-fba.ps1 ../../samples/fba/dynamic_3d_layers_bench.cs -Managed -NoCache -TargetFramework net11.0-windows7.0 -DotnetPath 'C:/Program Files/dotnet/dotnet.exe'
```

For NativeAOT, select a new output path and replace `-Managed` with `-NativeAot -Wait`. Run three independent repetitions and reverse phase order on run 2. The historical matrix also reverses configuration order on run 2. Vulkan diagnostics include warmup and exclude the first logged GPU value of each phase, so their sample set differs from the FPS raw files.
