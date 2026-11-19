# Windows close response

Gallery shutdown stops rendering and releases text/Vulkan resources before destroying the HWND. Previously the window stayed visible throughout teardown, producing a roughly0.1s hesitation. An accepted WM_CLOSE now hides the window after the tray interception branch. HWND, Vulkan-surface and accessibility teardown ordering is preserved.

One phase-observed baseline spent about0.1ms joining the render thread,3.3ms releasing UiContext and102.5ms releasing Vulkan, including52.4ms in instance destruction. These are observed phase durations, not a breakdown of driver internals.

Three independent processes per side ran the same default Gallery after its first real frame plus250ms settling. Order: after1,before1,before2,after2,after3,before3. Visibility means Win32 WS_VISIBLE, not the compositor's final displayed pixel.

| Median interval | Before | After |
| --- | ---: | ---: |
| Close request→hidden |116.94ms|13.77ms|
| Close request→HWND no longer owned |121.30ms|77.56ms|
| Close request→process exit |130.91ms|82.80ms|

Hidden-response time improves88.22%. Process exit also improved in these samples; this does not guarantee the same cleanup improvement across pages or drivers. Every after-run retained its HWND after hiding and exited0 without termination. Core/App/Vulkan/Gallery module hashes match; only the Windows platform module differs. FPS and frame p95/p99 are not applicable to this close-response measurement.

Existing LogStartupTimings diagnostics now include ShutdownTiming and VulkanShutdown phases. No new timer or background cleanup is introduced. The probe verifies its owned process/window PID before posting WM_CLOSE. Run the built probe client with a fresh output directory:

```text
Duxel.Graphics.Tests.exe <fresh-output> gallery-close-latency <absolute-Gallery-executable>
```

Evidence: artifacts/gallery-close-latency-2026-11-17/result.json. Windows Gallery and Linux HOST builds pass with0warnings/errors. Actual Linux, pinned RC3 NativeAOT and the separate external UIA TooltipClosed gate remain unqualified. Long-lived/heavy-page and app-driven RequestExit scenarios are outside this comparison. Hiding before destruction does not change tray interception or modal-owner teardown ordering.

To test close with an active TimePicker popup, use `gallery-close-time-popup` in the same command. The probe opens and verifies the popup through UI Automation before posting WM_CLOSE. On the Windows x64 .NET 11.0.0 NativeAOT image, the owned window hid after 10.83 ms and the process exited normally after 184.10 ms. The overview control run also exited normally. These single-run observations qualify shutdown with an open popup. The external TooltipClosed delivery check is separate. Evidence: `W:/Cache/Duxel/gallery-source-20261117/gallery-popup-close-1935/gallery-close-time-popup/result.json`.

Run `Duxel.Gallery.exe --verify <fresh-output>` to exercise the app-driven `DuxelAppSession.Exit()` path. Windows x64 managed and NativeAOT .NET 11.0.0 builds each presented the same 104 scenarios in order, stopped the render thread, disposed the UI context, destroyed the Vulkan instance, and exited with code 0. Evidence: `W:/Cache/Duxel/gallery-source-20261117/gallery-session-exit-1936/comparison.json`.

Win32 reference: https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-iswindowvisible
