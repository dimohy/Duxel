# Repaint observer

**Global top-right toggle** : repaint highlighting defaults to off, covers every page/popup and survives navigation. The inspector page remains a fixed/changing-content sample. [Linux launch guide](../samples/Duxel.Gallery.Linux/README.md).

Last synced: 2026-11-19

Run `./run-gallery.ps1` from the repository, then select **Repaint observer**. An already open Observer uses its previous executable; review the freshly built executable. Header V-Sync and event-wait options use Fluent Switch; V-Sync defaults to off.

1. Press **Update once** or edit **Repaint observer input**.
2. Enable **Highlight repaints** to outline the actual region Vulkan redrew in the acquired image for 0.2 seconds.
3. Enable **Update text automatically (60 Hz)** for repeated updates. Disabling it returns to event waiting after diagnostic cleanup. A focused editor still needs caret blinking.
4. Disable highlighting for performance comparisons. Stop automatic updates and press **Read measurements** to inspect the preceding page-build duration, draws, uploads, reuse, repair bounds and extra diagnostic repair.

**Measurement reuse** checks reuse of child layout measurements. **Repaint observer** is a separate tab comparing CPU content construction with actual GPU repair. Static panel construction can remain cached while the GPU redraws a larger region containing another change.

The outline shows the scene's actual repair rectangle, including older swapchain images and MSAA history. A reused presentation adds no outline. Header statistics freeze while highlighting to prevent their own feedback loop, and cleanup for the outline is measured separately.

## Common engine path

- `ui.SetRepaintHighlights(enabled)` travels through `UiDrawData.RepaintHighlights`, including batching and cloning.
- `UiRendererFrameMetrics.RepaintBounds` reports content repair in framebuffer pixels. `RepaintDiagnosticRedraw` indicates additional border strips drawn outside that rectangle.
- Vulkan paints the outline after content without inserting diagnostic geometry into content fingerprints. When a previous outline on a swapchain image or the shared MSAA surface extends outside the content repair, Vulkan restores the affected border strips. The profile reports the extra passes and their pixel count as `overlayStrips` and `overlayPixels`.
- For planar widget transforms, damage tracks the old and new screen-space bounds clipped to the parent. At app startup a swapchain image still needs its full initial draw. Projected or depth-writing scenes keep their conservative whole-scene repair.
- The host waits until `RepaintHighlightDeadlineTicks`. A diagnostic-expiry-only wake sets `UiFrameInfo.RepaintDiagnosticCleanup`, preventing conservative full-scene repair from restarting the pulse. Custom host loops must preserve this contract.
- Texture creation/update/destruction no longer invalidates the entire screen. The existing texture revision ledger is reused; command snapshots compare primary and backdrop texture revisions to repair actual referenced regions. Unreferenced textures cause no screen repair. PaintGroup's separate whole-scene invalidation remains unchanged.

## Measurement scope

`Duxel.Graphics.Tests <output> repaint` checks actual MSAA1/4 pixels, outline cleanup, separate content/diagnostic accounting and texture-local repair. It then alternates unchanged and unique equal-length native Fluent text in three runs, each with 96 warmup and 256 measured frames. Highlights and V-Sync are off.

In `text-update.csv`, `buildMs` measures CPU construction; `submitReadbackMs` includes synchronous pixel readback. `gpuUs` contains delayed GPU timestamp samples. Do not convert this diagnostic into claims about interactive Observer FPS or fixed RC3 NativeAOT performance. `scheduling-session repaint` verifies expiry, a later external update and return to waiting in the real pipelined application host.
