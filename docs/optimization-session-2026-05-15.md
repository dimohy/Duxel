# Optimization Session — 2026-05-15

## Scope

Renderer-side structural optimization after the global static cache lifetime fix. The focus is reducing wasted per-frame host-visible dynamic upload buffer sizing when static draw lists are already promoted to renderer-owned device-local static geometry buffers, while keeping the default inline Vulkan rendering path intact.

Local validation context:

- Windows workspace
- Managed FBA benchmark path through `run-fba.ps1`
- Static-heavy `samples/fba/global_dirty_strategy_bench.cs`
- Density: `16000`
- Tiles: `8x6`
- Phase duration: `2` seconds

## Changes and Measurements

| Target | Summary | Validation Command | Before | After | Improvement | Risk / Follow-up |
|---|---|---|---:|---:|---:|---|
| Dynamic upload buffer sizing | The Vulkan renderer now sizes per-frame host-visible dynamic vertex/index upload buffers from non-static draw lists only. Static draw lists continue to be uploaded into renderer-owned device-local static geometry buffers and are skipped for the per-frame dynamic buffer capacity request. | `dotnet build Duxel.slnx -c Release`; managed `global_dirty_strategy_bench.cs` with `DUXEL_GLOBAL_DIRTY_BENCH_DENSITY=16000`, `DUXEL_GLOBAL_DIRTY_BENCH_PHASE_SECONDS=2` | Static-cache phase previously requested per-frame dynamic buffer capacity from full frame draw data, including static background geometry. Previous stable FPS artifact: all-dynamic `429.930`, global-static-cache `1237.753`. | After dynamic sizing run: all-dynamic `409.428`, global-static-cache `1238.619`. After keyed fast-path runs: all-dynamic `434.785` / `409.910`, global-static-cache `1177.404` / `1223.198`. | FPS average of the three 5/15 static-cache runs: `1213.074`, which is `-1.994%` vs the 5/14 stable single-run static-cache baseline. This change is retained as a memory/structure reduction, not an FPS win. | The added dynamic counting pass can be noisy in short managed runs. If CPU profiling shows this pass as visible overhead, move dynamic vertex/index counts into core draw-data creation so the renderer does not rescan draw lists. |
| Static geometry key classification | Keyed static draw lists now short-circuit static classification using `StaticGeometryKey` when it has the static tag prefix. This avoids repeatedly scanning every command in keyed static lists before upload/record decisions. | Same build and benchmark commands as above. | Static list classification always scanned command metadata. | Keyed static lists classify directly from the stable static geometry key. | Not isolated; included in the `1177.404` and `1223.198` post-change runs. | Keep correctness tied to the static tag prefix. Static command-buffer execution remains opt-in and unchanged. |
| Batched staging upload correctness | Fixed Vulkan staging-buffer reuse during upload batching. Each texture/static-buffer upload now reserves a unique staging offset inside the current upload batch, and `vkCmdCopyBuffer` / `vkCmdCopyBufferToImage` use that source offset. The command-buffer start path no longer resets the reserved offset after bytes have already been copied. | `dotnet build Duxel.slnx -c Release`; visual HWND captures with `DUXEL_VK_UPLOAD_BATCH=1`; managed `global_dirty_strategy_bench.cs` with `DUXEL_GLOBAL_DIRTY_BENCH_DENSITY=16000`, `DUXEL_GLOBAL_DIRTY_BENCH_PHASE_SECONDS=2` | Broken captures showed large triangle/tile corruption and garbled text because multiple batched copy commands referenced staging offset `0` while later uploads overwrote the same memory before the batch submitted. | Captures `global-dirty-after-staging-offset-fix-2026-05-15.png` and `global-dirty-manual-static-after-staging-offset-fix-2026-05-15.png` show intact all-dynamic and manual static-cache frames. Benchmark artifact: all-dynamic `402.891`, global-static-cache `1174.738`. | Correctness fix; FPS not compared as an optimization win. | Keep upload batching correctness tied to non-overlapping staging ranges. If large upload batches exceed the staging buffer, the current code flushes/recreates and restarts range allocation. |

## Raw Measurements

| Artifact | all-dynamic FPS | global-static-cache FPS | Notes |
|---|---:|---:|---|
| `global-dirty-after-static-cache-lifetime-fix-2026-05-14.json` | 429.930 | 1237.753 | Previous stable lifetime-safe baseline. |
| `global-dirty-after-dynamic-buffer-sizing-2026-05-15.json` | 409.428 | 1238.619 | After dynamic-only frame upload buffer sizing. |
| `global-dirty-after-static-key-fastpath-2026-05-15.json` | 434.785 | 1177.404 | After keyed static classification fast-path, run 1. |
| `global-dirty-after-static-key-fastpath-rerun-2026-05-15.json` | 409.910 | 1223.198 | After keyed static classification fast-path, run 2. |
| `global-dirty-after-staging-offset-fix-2026-05-15.json` | 402.891 | 1174.738 | After batched staging upload correctness fix with `DUXEL_VK_UPLOAD_BATCH=1`. |

## Interpretation

This change addresses a structural mismatch: static geometry promoted to renderer-owned device-local buffers should not drive host-visible per-frame dynamic upload buffer capacity. The short managed FPS benchmark is noisy and does not show an FPS gain; the average static-cache FPS across the 5/15 runs is slightly below the previous single-run stable baseline. The retained value is lower dynamic upload buffer capacity pressure for static-heavy frames and a clearer separation between dynamic and static geometry paths.

The visible corruption root cause was separate from the static-cache lifetime issue: upload batching recorded multiple copy commands that all read from staging offset `0`. Because the CPU overwrote the same mapped staging memory for subsequent uploads before the batched command buffer was submitted, GPU copies could consume the wrong vertex/index/texture bytes. Reserving unique staging offsets per upload fixes the corruption without disabling batching or adding fallback behavior.

The next higher-impact optimization remains an explicit retained static geometry handle/replay model. The prior borrowed draw-list approach was rejected because it could reference returned pooled buffers and corrupt geometry. Any future zero-copy static path must use renderer-owned lifetime and explicit invalidation rather than borrowed frame draw-list buffers.

## Verification

- `dotnet build Duxel.slnx -c Release` — succeeded, 0 warnings, 0 errors.
- Managed FBA `global_dirty_strategy_bench.cs` after dynamic upload sizing — succeeded and produced `artifacts\global-dirty-after-dynamic-buffer-sizing-2026-05-15.json`.
- Managed FBA `global_dirty_strategy_bench.cs` after keyed static fast-path — succeeded twice and produced `artifacts\global-dirty-after-static-key-fastpath-2026-05-15.json` and `artifacts\global-dirty-after-static-key-fastpath-rerun-2026-05-15.json`.
- Visual HWND capture after staging offset fix — `artifacts\captures\global-dirty-after-staging-offset-fix-2026-05-15.png` shows all-dynamic frame without large triangle corruption or garbled text.
- Visual HWND capture after staging offset fix — `artifacts\captures\global-dirty-manual-static-after-staging-offset-fix-2026-05-15.png` shows manual static-cache frame without large triangle corruption or garbled text.
- Managed FBA `global_dirty_strategy_bench.cs` with `DUXEL_VK_UPLOAD_BATCH=1` after staging offset fix — produced `artifacts\global-dirty-after-staging-offset-fix-2026-05-15.json` with all-dynamic `402.891` FPS and global-static-cache `1174.738` FPS.
