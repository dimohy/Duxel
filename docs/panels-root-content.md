# Panels and direct application content

Last synced: 2026-11-05

The movable, resizable, collapsible internal Window is now an optional **Panel**. The outer OS application window remains a Window. Default samples compose directly in the app work area without an internal panel shell.

```csharp
Dux.App(Dux.Column(12, Fluent.Title3("My app"), Fluent.Body1("Content")));
Dux.Panel("Inspector", Dux.Column(12, Fluent.Body1("Details")),
    new UiPanelOptions(Position: new(24,24), Size: new(420,300),
        PositionSpace: UiPanelPositionSpace.WorkArea, AutoHeight: true));
ui.BeginPanel("Inspector");
ui.Text("Details");
ui.EndPanel();
```

The source generator closes `UiDefaultPanel` over public `UiPanel<IUiView>` and generates its constructors. User panels inherit the same base. No reflection, string type lookup or runtime widget-theme selection is added. Panel is infrastructure and is excluded from the official Fluent catalog.

Initial placement is the default; later user moves persist. `Placement=Always` explicitly reapplies placement. Options include Open bindings, focus, topmost and AutoHeight. Programmatic collapse and next-panel collapse preserve and restore expanded dimensions. Panel names are available for placement, dimensions, scrolling and drawing.

Legacy `BeginWindow/EndWindow`, `Dux.Window`, `DuxelView.Windows` and `UiWindowOptions` remain with Legacy and EditorBrowsable(Never) metadata, forwarding to the shared implementation. DSL Panel is preferred; Window/BeginWindow normalize to BeginPanel. Native `DuxelWindowOptions` remains unchanged.

The default gallery, typography, Progress and Slider pages render directly. `--panel` explicitly demonstrates internal panels. All Features selects37 direct pages from the menu; `--panels` preserves the explicit old-layout validation mode. Hello, Theme, Dashboard, input/layout samples and Duxel.Sample also compose without internal windows. AppShell defaults to direct content, with `UiAppShellOptions.UsePanel=true` for an explicit optional panel. Benchmark, layer-lab and dedicated validation samples retain their workload and use Panel API names.

A native sizing regression reproduced the UI message handler waiting for GPU presentation inside WM_SIZING. This synchronous wait was removed; WM_SIZE publishes the committed client extent for layout and Vulkan. Proposed outer bounds are no longer published as an actual framebuffer. Official contracts: [WM_SIZING](https://learn.microsoft.com/en-us/windows/win32/winmsg/wm-sizing), [WM_SIZE](https://learn.microsoft.com/en-us/windows/win32/winmsg/wm-size).

Evidence: `artifacts/panel-root-2026-11-05`. Full .NET10 1014 and MSAA4 GPU159 pass. Fixed RC3 NativeAOT passes159 checks each with core/EXT presentation lifetime and1/2/3 frame resources. All942 comparisons of157 managed images against those six native configurations are byte-identical. Reuse the preceding37 direct All Features pages and66 generator checks. These results do not complete Fluent, Private, FPS or Pi qualification.

See the [execution guide](widget-gallery-run.md). Managed `-NoLaunch` now builds without starting an app.

The B078 gallery reproduced black regions/freezing for the user. The WM_SIZING fix alone was insufficient. A focused regression confirmed that **unsuccessful nonblocking image acquisition advanced the semaphore ring**. It now advances only after success; the previous behavior fails the regression and the fixed behavior passes. New swapchain synchronization, stale-size frame rejection and event-driven retries are also retained.

Old swapchains and semaphores are kept until the first new presentation image is reacquired and a submission waiting on that acquisition completes. Idle skipping permits these retirement frames to finish. Sources: [Khronos recreation](https://docs.vulkan.org/samples/latest/samples/api/swapchain_recreation/README.html), [semaphore reuse](https://docs.vulkan.org/guide/latest/swapchain_semaphore_reuse.html). The unextended final-shutdown WaitIdle limitation remains; it is not claimed as presentation-completion proof.

146 GPU checks include16 consecutive NotReady attempts and eventual retirement. Fixed RC3 also checks3×48 continuous sizing updates, continued presentation and subsequent clicks for1/2/3 resource counts. An artificial test cancels before QueuePresent reaches WSI; it does not model the actual QueuePresent OutOfDate contract. Actual OutOfDate still enqueues semaphore waits, so the earlier contrary inference is withdrawn. Track user review of the7E1B gallery separately. Counters and short Private samples are not smoothness, input-to-photon or memory-improvement proof.


## GPU and presentation lifetime during live resize

The user accepted freeze removal in849A/7E1B but still found resizing slower than WinUI. The new path removes device-wide waits and retires old targets after their last graphics submission completes. Supported devices use swapchain-maintenance presentation fences; other devices retain the new-image reacquisition proof. Presentation fences do not measure when pixels reach the display. Follow the [official fence contract](https://docs.vulkan.org/refpages/latest/refpages/source/VkSwapchainPresentFenceInfoKHR.html).

MSAA4 targets retain sufficient capacity during sizing, allocate new capacity in64-pixel increments within the physical image limit, and trim to the exact extent after sizing ends. Render area, pixel resolution and sample count remain unchanged. GPU tests verify resolved pixel equality and final trimming.

An additional candidate occasionally stalled inside vkCreateSwapchain on GPU synchronization. Recreation now checks graphics completion with zero timeout before entering WSI and retries with a fresh frame if busy. Pending recreation survives idle skipping. A test-only busy injection verifies that creation is not entered prematurely.

Three independent fixed-RC3 NativeAOT runs per variant in alternating order use FIFO/VSync, MSAA4,2 frame resources,300ms warmup and3x48 size updates at8ms. The median per-run completed-recreation p95 is19.9392->7.7539ms(61.11% lower); ranges14.1804–23.6590->7.4340–7.9389ms do not overlap. This is not end-to-end latency, presented FPS or WinUI-equivalence proof. Final active Private medians416,411,648->372,330,496B have overlapping process ranges, so no memory win is confirmed.

Evidence: `resize-ready-comparison.json`, `ready-pixel-comparison.json`, `gallery-ready-resize/result.json`, `verification.json`. The actual gallery completed288 size changes and exited0. Its30,838 app-frame attempts are not presented frames. User review of the new SHA52493AB5… build remains separate. The deferred-memory-allocation experiment caused DeviceLost and is disabled.


## Visible resize regression correction

The nonblocking371139A9 path still showed black regions and lag. The user accepted DCA37C18 after committed WM_SIZE joined fresh-frame submission while servicing WSI synchronous messages. Earlier wait-removal notes describe historical speculative WM_SIZING waits; the current committed WM_SIZE synchronization is distinct. See [current contract and validation limits](windows-resize-coherence.md).
