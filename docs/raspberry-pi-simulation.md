# Raspberry Pi specification-based simulation — 2026-11-04

[한국어](raspberry-pi-simulation.ko.md)

The current Duxel renderer cannot run unchanged on the examined Pi driver profiles. Specification and source compatibility checks and trace sensitivity analysis are complete. No board execution, ARM instruction emulation or GPU hardware emulation was performed. The overall optimization goal remains active.

## Specifications and compatibility

| Board | CPU | GPU | Theoretical memory bus bandwidth |
|---|---|---|---:|
| Pi4 | Four Cortex-A72 cores, modern boards1.8GHz, launch boards1.5GHz | VideoCore VI / V3D4.2 | 8.0GB/s |
| Pi5 | Four Cortex-A76 cores,2.4GHz | VideoCore VII / V3D7.1 | 17.068GB/s |

The [official launch explanation](https://www.raspberrypi.com/news/introducing-raspberry-pi-5/) supplies clocks,32-bit bus and2000/4267MT/s rates. Bandwidth is calculated as transfers/second×32/8, not sustainable throughput. Current Pi5 Vulkan1.3 and memory options follow the [product specifications](https://www.raspberrypi.com/products/raspberry-pi-5/). GPU generations follow [Mesa documentation](https://docs.mesa3d.org/drivers/v3d.html). Clocks are not converted into FPS ratios.

Profiles are Mesa25.0.7, identified as the Trixie baseline in the [driver developer presentation](https://archive.fosdem.org/2026/schedule/event/BMFQSE-raspberry-pi-gpu-drivers-from-bookworm-to-trixie/), and latest upstream26.2.6 released2026-10-28. Neither is asserted to be installed on the user's absent device.

Official archives were downloaded, SHA256-verified against [25.0.7 notes](https://docs.mesa3d.org/relnotes/25.0.7.html) and [26.2.6 notes](https://docs.mesa3d.org/relnotes/26.2.6.html), and only `src/broadcom/vulkan/v3dv_device.c` was extracted.

- [25.0.7 archive](https://archive.mesa3d.org/mesa-25.0.7.tar.xz): `592272df3cf01e85e7db300c449df5061092574d099da275d19e97ef0510f8a6`.
- [26.2.6 archive](https://archive.mesa3d.org/mesa-26.2.6.tar.xz): `b9436b3e1a8be76fbd4823dc884f90596ab7c6ac5e1867a305e66ffccfb31a92`.

`RequireGpuDrivenRendererFeatures` in `VulkanRendererBackend.Device.cs` requires seven features. Both profiles provide bufferDeviceAddress and dynamicRendering, but lack these five under supported default settings:

- shaderSampledImageArrayDynamicIndexing
- descriptorBindingSampledImageUpdateAfterBind
- descriptorBindingUpdateUnusedWhilePending
- descriptorBindingPartiallyBound
- runtimeDescriptorArray

Omitted members of the complete C aggregate initializer are zero.26.2.6 ties sampled-image indexing to a default-disabled WebGPU override which its source explicitly describes as advertising beyond actual support. The override was not enabled. Duxel's current feature gate therefore rejects these profiles. Vulkan1.3 alone does not establish descriptor indexing support.

The repository currently provides Windows window/input/text implementations, without Linux equivalents. GPU compatibility alone would not complete Linux application support. Any explicit portable path must retain vector quality, retained geometry, damage correctness and input semantics.

## Trace sensitivity

Reused12 independent alternating before/after runs,55,850 frames, from `optimization-evaluation-2026-11-04/comparison-ref`. Existing `measure-overlay-layers.ps1 -SummarizeOnly` revalidated frame counts,FPS,median/p95/p99 and1%low. No additional application benchmark ran.

Inputs: pinned SDK11.0.100-rc.3.26522.148/runtime11.0.0-rc.3.26522.148,NativeAOT Speed,128palettes×256swatches,1600×1000,MSAA1,VSync off,12seconds,three independent repetitions per variant/scenario. Existing .NET10 correctness results were not mixed into performance inputs. Executable and JSON/CSV hashes are recorded.

Every recorded accepted-presentation interval was multiplied by a hypothetical constant. **Factors are not calibrated Pi4/Pi5 ratios**, and do not isolate CPU/GPU/driver/wait costs or physical display refresh.

| Assumed slowdown | Final idle FPS median | Final moving FPS median | Moving p95 | Moving frames exceeding60Hz budget,run median |
|---:|---:|---:|---:|---:|
| 1×,Windows input | 403.15 | 395.35 | 2.887ms | 0.00% |
| 2× | 201.57 | 197.67 | 5.774ms | 0.00% |
| 4× | 100.79 | 98.84 | 11.548ms | 0.70% |
| 8× | 50.39 | 49.42 | 23.097ms | 99.11% |
| 12× | 33.60 | 32.95 | 34.645ms | 99.98% |

Thus a4× scenario mostly meets60Hz while8× does not. These are sensitivity results, not measured or predicted Pi FPS. Existing sampled UI construction CPU remains approximately2.4ms, motivating common CPU reuse work.

## Bandwidth and capacity

At1600×1000 RGBA8 and60Hz, logical scanout is384MB/s. Damage read+write is modeled as pixels×4×2×dirtyFraction×60. Observed moving uploads66,512B/frame contribute3.99MB/s.24 combinations cover two boards,assumed25/50/100% peak efficiency,and0/1/10/100% dirty area.

With moving uploads and scanout,1% dirty traffic totals395.67MB/s,or19.78%/9.27% of assumed25%-peak Pi4/Pi5 bandwidth.100% dirty totals1155.99MB/s,or57.80%/27.09%. Scanout remains when idle. These are logical approximations excluding actual tile behavior,compression/caches,blending overdraw,textures,depth,MSAA resolves,measured damage and competing workloads; they are not GPU traffic measurements or proof of sufficient rendering performance.

Observed Windows Private medians172.15/175.55MiB and working sets123.52/124.98MiB for idle/motion are not Linux ARM64 predictions. Logical RGBA image size is6.10MiB,three assumed images18.31MiB,and one hypothetical MSAA4 color image24.41MiB. These are separate sizes,not disjoint additions to Private or total unified memory. Eight capacity contexts cover1/2/4/8GiB without claiming minimum RAM support.

## Evidence and continuation

`artifacts/raspberry-pi-simulation-2026-11-04/` contains `simulate.ps1`,`simulation.json`,`audit.json`,`verification.json`,`sources.json`,the two official archives and selected source files,and `trace-audit/summary.json`. The model has20 frame sensitivity cases,24 bandwidth cases,8 capacity contexts,and seven feature checks for each driver. The script is scoped analysis reproducibility,not a new product or benchmark path.

```powershell
./artifacts/raspberry-pi-simulation-2026-11-04/simulate.ps1
```

No production/sample/quality changes occurred; existing .NET10 and pinned RC3 regressions were not rerun without a runtime change. Continue common CPU layout/geometry reuse with bounded invalidation and input preservation,then address an explicit GPU compatibility path and Linux window/text boundaries. The user has no board,so this stage uses simulation without claiming completed hardware verification.
