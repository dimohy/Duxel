# Windows resize and frame-submission ordering

## Native pixel capacity during a live gesture

The synchronous exact-size candidate passed30 Native cycles but still had a1024.4454ms peak SetWindowPos call. GC pause total was8.661ms; the final13.22s pass spent13.18s in size calls. Stability alone therefore does not meet the responsiveness requirement. Detailed WSI diagnostics also perturb this workload and are separated from the low-overhead run; no exact internal driver cause is claimed.

The renderer now queries surface-maintenance capabilities for the selected present mode. When ONE_TO_ONE and top-left gravity are supported, a live gesture reuses an oversized swapchain image and renders only the actual native viewport. Pixels are neither stretched nor resampled. Growth uses the same64-pixel capacity rounding as existing MSAA targets; shrinking retains capacity until the gesture ends. The first settled frame recreates the exact native target and trims MSAA capacity. Unsupported surfaces keep exact-size presentation. Present fences still protect destruction of old objects; no device-idle workaround, present-mode switch or quality reduction is added.

Focused .NET11 GPU checks compare retained/full repaint and exact unscaled swapchain references, verify stable swapchain identity within capacity, native-sized readback, ABI sizes and settled capacity release. MSAA1/4 pass on the current driver; Native live qualification and user acceptance remain pending. See `artifacts/resize-stress-2026-11-06/` and its L-cache evidence references. [Khronos defines ONE_TO_ONE as unscaled pixel mapping](https://docs.vulkan.org/refpages/latest/refpages/source/VkPresentScalingFlagBitsKHR.html).

## 2026-11-06 live resize correction

The user rejected both5BFBD8BE andDC656134: resizing was faster but trails remained and content did not follow the changing window size immediately. Passing thirty resize cycles for each Native frame-slot count1/2/3 in DC656134 did not establish visual acceptance. The asynchronous System WM_SIZE path described below is a superseded experiment.

The current candidate restores the accepted DCA37C18 ordering: committed WM_SIZE waits for submission of the corresponding frame before returning. It retains native System chrome, WVR_REDRAW, shutdown message pumping, graphics readiness checks, monotonic frame age, frame-owned acquire semaphores and consistent fractional-DPI framebuffer rounding. WM_PAINT only invalidates. During the wait, sent messages and selected non-input posted messages remain dispatchable. The fixture now asserts immediately after synchronous SetWindowPos that the committed sequence has been submitted; eventual completion is insufficient.

The first synchronous candidate41737B46 failed that immediate assertion after16 completed Native cycles and had increasing pass durations. Present fences were also prerequisites for replacement. The current candidate checks graphics completion before replacement while retaining present-fence protection of destruction in the deferred retirement path. [The Vulkan fence contract](https://docs.vulkan.org/refpages/latest/refpages/source/VkSwapchainPresentFenceInfoKHR.html) defines resource-lifetime safety; it is not a physical display timestamp or a requirement to delay new swapchain creation. The current .NET11 managed two-slot30-cycle check passes; Native and visual acceptance remain pending. This separates a code-level unnecessary dependency from an unproven driver-level causal explanation.

## Superseded System chrome experiment

The user confirmed the standard title appearance in804F7691, but reported resize latency and trails. DCA37C18 below is an earlier custom-chrome checkpoint, not acceptance of System mode. Current candidate5BFBD8BE awaits a separate visual review.

Committed WM_SIZE in System mode posts one coalesced message and returns immediately. That queued message waits for presentation outside native size/synchronous paint callbacks. No proposed WM_SIZING extent or timer recovery is used. Explicit Duxel chrome preserves its WM_SIZE handshake. The System wait dispatches sent messages and paint only, without recursively consuming subsequent mouse/key input. WM_PAINT requests a frame and does not wait inside that callback.

System WM_NCCALCSIZE retains DefWindowProc's native frame calculation and returns WVR_REDRAW according to the [Windows client-pixel preservation contract](https://learn.microsoft.com/en-us/windows/win32/winmsg/wm-nccalcsize). Normal retained layers/caches/partial damage remain active. Resolution, MSAA and vector quality are unchanged.

A live managed stack showed the UI thread in Thread.Join and the render thread in vkCreateSwapchainKHR. Shutdown now cancels/wakes and dispatches messages until the render thread exits. A focused regression sends synchronous WM_NULL after shutdown begins and passes. Window-owner creation also stalled, so that experiment was removed. Automation starts after actual first presentation/window show rather than UI construction plus300ms, and verifies native visibility. Evidence and unresolved cases remain under `artifacts/resize-stress-2026-11-06/`. Short execution/input checks do not prove visual latency or trail removal.

2026-11-05. The user confirmed **no black region and fast,natural resizing** in the DCA37C18 gallery. The earlier371139A9 still showed black regions and lag. This accepts continuous outer-window resizing in the current gallery, not complete Fluent coverage,whole-app memory or Pi qualification.

## Regression and correction

Historical HEAD waited for presentation in `WM_SIZING` after publishing a proposed extent. Removing that wait also disconnected native size commitment from fresh-frame submission. The user observed exposed black regions before the expanded window received a matching frame. Input/no-freeze tests and offscreen GPU readback did not cover that visible timing interval. CPU text benchmark improvements do not establish a resize fix.

`WM_SIZING` neither predicts future framebuffer dimensions nor waits. The committed `WM_SIZE` joins successful submission of a matching frame. `MsgWaitForMultipleObjectsEx` waits on the completion event plus `QS_SENDMESSAGE`; `PeekMessage` with `PM_QS_SENDMESSAGE` dispatches synchronous Vulkan WSI messages. It does not recursively consume the next queued mouse gesture. Renderer errors and session exit cancel the wait. This applies to hosts with an independent frame producer and invalidation callback; a host rendering on the window thread must not wait for itself.

Fresh committed dimensions remain available after a nested end-sizing message. Layout no longer depends on the outer event loop returning from that same waiting `WM_SIZE` to update its snapshot. Static/dynamic layers,caches,MSAA4,native resolution and VSync policy remain intact. No arbitrary FPS cap,reduced resolution or vector-quality reduction was added.

Completion here means successful submission after `vkQueuePresentKHR` returns. It is not a physical display timestamp or input-to-photon measurement. Black-region removal and natural response have separate user acceptance evidence.

## Regression and limits

The fixture now wires the actual `session.RequestFrame` callback. A test-only no-wait control fails the assertion that `WM_SIZE` returns after submission at its committed extent. The candidate passes and proves that a render-thread synchronous native message can complete while the UI waits. Clicks use the actual button rectangle times current DPI. Readiness requires both active control and actual mouse-down before release.

.NET10 full regression passed1035 checks. Pinned RC3 Native frame slots1/2 passed. One3-slot run timed out waiting for release; an instrumented run passed. A passing rerun alone is not treated as resolving that single failure; focused stability results are recorded separately in verification.json. Existing swapchain-stage and GPU-image evidence is reused only within compatible scope. User acceptance is not converted to a numeric speedup.

Evidence: `artifacts/resize-coherence-2026-11-05/user-acceptance.json`, `interactive-run.json`,stdout/stderr. Original Windows source and fixture remain protected as before.cs files. Prior executables are not overwritten and user windows are not closed.

References: [Vulkan Win32 WSI message-pump requirements](https://docs.vulkan.org/spec/latest/chapters/VK_KHR_surface/wsi.html), [MsgWaitForMultipleObjectsEx](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-msgwaitformultipleobjectsex), [PeekMessage sent-message processing](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-peekmessagew), [WM_SIZE](https://learn.microsoft.com/en-us/windows/win32/winmsg/wm-size).


Additional stability observation: Native3-slot validation requiring actual mouse-down passed three independent processes (nine passes),four assertions each. Managed validation including the final same-thread-host guard passed four assertions; net8/9/net10 solution build had zero warnings/errors. The earlier release timeout still has no confirmed cause.
