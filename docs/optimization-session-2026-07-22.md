# Optimization Session — 2026-07-22

## Startup presentation and Vulkan pipeline-cache persistence

- Date/session: 2026-07-22, first-frame window presentation
- Area: `Duxel.App`, `Duxel.Platform.Windows`, `Duxel.Vulkan`
- Type: startup UX, cache I/O integrity, cross-process persistence
- Hypothesis: the native window is visible before synchronous Vulkan initialization and the first present, so hardware-dependent startup work appears as a blank window; non-atomic persistence of driver-owned Vulkan cache bytes can also produce repeated misses or startup failures.
- Change: create the HWND hidden, show it on the Windows UI thread only after the first successful present, validate the driver-owned Vulkan cache header, and persist the opaque bytes through atomic replacement without making Duxel file persistence a rendering requirement.
- SDK: `10.0.302`
- Conditions: Windows 11, NVIDIA GeForce RTX 5070 Ti Laptop GPU, Release, warm driver cache, independent process launches, Duxel ThemeDemo, three post-change runs. Startup measurement has no frame warmup by definition.

### Verification commands

```powershell
dotnet build Duxel.slnx -c Release -warnaserror
$env:DUXEL_SAMPLE_AUTO_EXIT_SECONDS='1'
samples/Duxel.ThemeDemo/bin/Release/net10.0-windows7.0/win-x64/Duxel.ThemeDemo.exe
```

Additional cache gates replaced the active cache with missing, incompatible/read-only, and two-process concurrent-write cases while preserving and restoring the original cache file.

### Before / After

| Metric | Before | After | Improvement |
|---|---:|---:|---:|
| User-visible blank-window interval | at least 337.19 ms median | 0 ms by presentation ordering | 100% eliminated |
| First-frame-presented time | 337.19 ms median (4 runs) | 338.78 ms median (3 runs) | -0.47% |
| Process start to visible content | window visible before Duxel startup timer | 437.76 ms median, after present | ordering corrected |

Post-change raw runs: first present `352.82 / 338.78 / 337.58 ms`; visible HWND `463.87 / 430.45 / 437.76 ms`. The change intentionally does not claim faster Vulkan initialization; it removes exposure of that time as an empty window.

### Cache results

| Case | Result |
|---|---|
| Existing compatible cache | `PipelineCache[Hit]`, exit 0 |
| Missing cache | `Miss` → `Saved`, exit 0 |
| Incompatible read-only cache | `Incompatible` → `PersistenceUnavailable`, first frame shown, exit 0 |
| Two simultaneous processes without cache | both exit 0, final header/vendor/device/UUID valid, no temporary files left |

### Frame-distribution metrics

Average FPS, median/p95/p99 steady-state frame time, and 1% low FPS are not applicable to this startup-only gate. The steady-state renderer and draw loop are unchanged; the measured target is HWND visibility relative to the first successful present.

### Risks / follow-up

- Hidden-surface present and UI-thread show were verified on the listed NVIDIA/Windows system; AMD, Intel, virtual GPU, and remote-session confirmation remains desirable.
- `WindowCreated` now receives a hidden HWND. Applications that explicitly call `ShowWindow` inside that callback can bypass the blank-window guarantee.
