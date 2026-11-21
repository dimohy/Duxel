# Optimization Session — 2026-07-16

## Scope and acceptance target

Windows interactive resize must not use a fixed 240 FPS cap. Its response target follows the current monitor cadence (for example, at least 60/144/240 updates per second on 60/144/240 Hz displays), and the outer window must not visibly outrun its rendered contents during fast drag.

The first user-observed baseline was: slow dragging updated immediately, while fast dragging enlarged the outer window and left the rendered contents at an older extent. After the first continuous-loop/fast-recreate candidate, the user reported a substantial response improvement but the same outer-first freeze under sufficiently fast dragging.

## Hypothesis evolution

### Retained findings

1. One-shot `FrameInvalidated` work allowed the render thread to return to idle during a Win32 move/size modal loop.
2. Cross-thread `GetClientRect` reads were unnecessary; `WM_SIZE` can maintain an atomic client-size cache.
3. Exact proactive swapchain recreation for every pointer sample races the UI snapshot: the surface can change again between draw-data creation and `vkCreateSwapchainKHR`, causing the renderer to reject the completed draw data and present nothing.
4. Windows commits the non-client drag rectangle independently from Vulkan completion unless the `WM_SIZING` handler couples that step to a presented Duxel frame.

### Rejected candidates

- **Exact recreate on every interactive mismatch:** improved slow/moderate resizing but failed the user's fast-drag gate because continuously changing extents could keep every completed draw data one generation behind the newly created swapchain.
- **Switch FIFO to Mailbox/Immediate on `WM_ENTERSIZEMOVE`, then switch back on exit:** a synthetic one-step Win32 stress probe reached the exit request but the process remained in renderer shutdown for more than 15 seconds. Runtime present-mode switching during the live loop was removed. The retained policy chooses the low-latency present mode only when the swapchain is created normally.

## Retained implementation

- `DuxelAppSession` treats `IsInteractingResize` as continuous work and captures `WindowSize`, `FramebufferSize`, and `InteractiveResizeSequence` consistently.
- `WindowsPlatformBackend` caches `WM_SIZE` dimensions and predicts the next client dimensions from the `WM_SIZING` drag rectangle before Windows commits the outer size.
- Each `WM_SIZING` increments a sequence, requests a frame, and waits until that sequence is acknowledged by a successfully queued present. Renderer failure cancels the wait explicitly; there is no timer-based debounce or arbitrary wait timeout.
- `IRendererBackend.TryRenderDrawData(...)` is a backward-compatible default interface method. The Vulkan implementation returns `true` only when `vkQueuePresentKHR` returns success or suboptimal. The existing `void RenderDrawData(...)` surface remains available.
- During interactive resize, an extent mismatch does not proactively recreate. Command recording maps the newest layout to the full current swapchain viewport and scales clip coordinates proportionally. WSI out-of-date/surface-loss results still recreate; after the modal loop, an exact mismatch recreates the final swapchain.
- Resize-only recreation passes `oldSwapchain`, replaces image views/MSAA/per-image tracking, and preserves geometry, descriptors, frame command resources, upload resources, and the graphics pipeline when the format is unchanged.
- VSync swapchain creation prefers Mailbox when supported and otherwise uses guaranteed FIFO. VSync-off prefers Immediate. Present mode is not changed inside a live resize loop.

Microsoft documents `WM_ENTERSIZEMOVE` as entry into a modal move/size loop and `WM_SIZING` as the point where an application can monitor or alter the drag rectangle. Vulkan documents FIFO as a queue that can make the application wait when full, while Mailbox replaces an already queued request with the newest request. These rules support the sequence handshake and creation-time present policy; they do not themselves prove a device-specific frame rate.

- https://learn.microsoft.com/windows/win32/winmsg/wm-entersizemove
- https://learn.microsoft.com/windows/win32/winmsg/wm-sizing
- https://docs.vulkan.org/tutorial/latest/03_Drawing_a_triangle/01_Presentation/01_Swap_chain.html

## Before / after evidence

| Metric | Before | Retained after | Result |
|---|---|---|---|
| Scheduler | One-shot invalidation | Continuous work for the entire move/size loop | Structural fix retained |
| Size source | Render-thread `GetClientRect` / committed `WM_SIZE` | Atomic `WM_SIZE` cache plus predictive `WM_SIZING` size | Structural fix retained |
| Outer/content ordering | Windows could commit outer size before a Duxel frame | Each `WM_SIZING` waits for its presented sequence | Structural synchronization retained |
| Interactive extent mismatch | Recreate/reject loop could present nothing | Render newest layout into full current viewport; recreate only when WSI requires it | Structural fix retained |
| Fast-drag visual behavior | Outer frame visibly outran/froze contents | Awaiting post-change physical mouse-drag confirmation | Not yet quantified |
| Average FPS | Not measured | Not measured | **INVALID: no FPS claim** |
| Median / p95 / p99 frame time | Not measured | Not measured | **INVALID: no latency claim** |
| 1% low FPS | Not measured | Not measured | **INVALID: no tail-FPS claim** |
| Improvement rate | N/A | N/A | **INVALID: no percentage claimed** |

The attempted synthetic `WM_ENTERSIZEMOVE`/`WM_SIZING` sequence was useful for rejecting runtime present-mode switching, but it does not reproduce the system-owned modal loop and is not accepted as a visual or numerical performance benchmark. Quantitative fields remain invalid instead of being inferred from CPU render-loop attempts.

## Environment and validation

- SDK: `10.0.302` (`global.json` selects the `10.0.301` feature band with patch roll-forward).
- Configuration: Release.
- Warmup: not applicable to build/compatibility validation.
- Independent accepted measured runs: `0`; no numerical A/B benchmark was accepted.
- Release build: passed with `0` warnings and `0` errors.
- NativeAOT local-source FBA: `all_features.cs` publish/run gate.
- Source hygiene: `git diff --check`.

Commands:

```powershell
dotnet --version
dotnet build Duxel.slnx -c Release --no-restore
$env:DUXEL_SAMPLE_AUTO_EXIT_SECONDS = '2'
./run-fba.ps1 samples/fba/all_features.cs -NoCache
git diff --check
```

## Risks and follow-up

- The synchronous `WM_SIZING` handshake deliberately trades unconstrained border motion for border/content coherence. A heavy UI can make the border follow the pointer more slowly, but it cannot let the border run ahead of the acknowledged frame.
- `vkQueuePresentKHR` success means the present request was accepted, not that photons have reached the panel. Physical 60/144/240 Hz validation still requires monitor-attributed present/composition timing and high-speed visual capture.
- Drivers exposing only FIFO may still apply FIFO queueing; the sequence handshake remains the ordering guarantee in that case.
- Dragging across monitors with different refresh rates needs an explicit physical gate because the active compositor timing changes during the interaction.
