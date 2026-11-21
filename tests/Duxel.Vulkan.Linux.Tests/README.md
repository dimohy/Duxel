# Linux Vulkan raw-pixel contracts

Append `inline-paragraph-cache` to compare retained paragraph and texture behavior with freshly created widgets in a fixed 320×240 window.

The retained fixture uploads R8 and the fresh reference uploads RGBA. The case checks 22 paragraph captures with 1× and 4× MSAA and adds five checks for texture creation, batched region updates, destruction, and recreation.

Append `styled-paragraph-raster` to compare 36 whole-paragraph masks with independent Pango references.

On .NET 11.0.0 with three frame slots, Windows passed 3,554 checks and WSLg Linux with llvmpipe passed 3,570. Each host's reuse and full-repaint pixels matched. Across hosts, 214 of 574 same-name raw RGBA pairs matched, including all 160 sparse-update cases. The other 360 differed, including text and some rounded-primitive scenes. [Comparison and case hashes](W:/Cache/Duxel/gallery-source-20261117/cross-host-present-1938/comparison.json).

Append `host-pixels` to the Linux command below, or run the Windows graphics test as `Duxel.Graphics.Tests <fresh-output> presentation-reuse-host-pixels`. With three frame slots, both commands save ten 320 × 240 RGBA samples from the same full-repaint scenes. Both sparse controls matched exactly across hosts. Six rounded-primitive scenes differed by at most one RGB level, with identical alpha. At 4× MSAA, many differences were in repeated translucent bars. [Per-channel comparison](W:/Cache/Duxel/gallery-source-20261117/cross-host-rgba-1939/rgba-comparison.json).

The two text samples also save the first-frame texture data before Vulkan. With the same Segoe UI file, the Windows and Linux glyph textures already differ in size and coverage. At 1× and 4× MSAA, 489 pixels differ in the text rows and the other rows match exactly. [Text input and framebuffer comparison](W:/Cache/Duxel/gallery-source-20261117/cross-host-text-1940/text-comparison.json).

To test the Vulkan path with the same captured glyph texture on both hosts, append `glyph-texture-replay <rgba-file> <width> <height>` to the Linux command below, or run `Duxel.Graphics.Tests <fresh-output> glyph-texture-replay <rgba-file> <width> <height>` on Windows. Supply raw RGBA8 bytes from the text preflight. The test writes four raw 320 × 240 frames with whole-pixel and fractional placement at 1× and 4× MSAA.

With one input captured on each host, all eight pairs matched between Linux managed and NativeAOT. Across Windows managed and Linux managed, whole-pixel placement differed by at most one RGB level and fractional placement by at most two. All alpha values matched. This checks textured quads with fixed input and placement. Native text layout and glyph rasterization remain separate. [Replay captures and comparison](W:/Cache/Duxel/gallery-source-20261117/glyph-texture-replay-2039/verification.json)

The same Linux NativeAOT executable ran with lavapipe 25.2.8 on WSLg and with 26.0.8 in an Ubuntu 26.04 container using the same X server. All four whole-pixel frames matched. The four fractional frames differed by up to three RGB levels, with identical alpha. The container also uses different system libraries, so the comparison does not isolate a single Mesa change. The WSL Dozen driver reported no `dualSrcBlend` support on either enumerated GPU. [Driver comparison and device features](W:/Cache/Duxel/gallery-source-20261117/glyph-texture-driver-2040/verification.json)

Append `early-init-cleanup` to exercise renderer construction on one X11 window. The test injects exceptions after surface creation, device function loading, swapchain creation, graphics pipeline creation, and completed setup. It checks that each original exception propagates and initializes the renderer again on the same window.

Reuses Windows PresentationReuseTests/PaintGroupRenderingTests sources with platform-specific window/font factories. The independent reference forces full repaint. This is not the full accessibility runner.

Append `tree-gpu` for Tree hierarchy, checkbox and disclosure hit targets, RTL, and themed hover pixels. Append `tree-label-gpu` for long-label wrapping and truncation in light RTL and dark LTR layouts. Both modes use the Windows Tree fixture at 640 × 420 and save PNGs and a `result.json` with the runtime, frame slots, and passed checks.

Append `container-pixels` to compare rendered ScrollView and custom container scenes against independent reference draws on the same host. The mode covers light/dark themes, 1×/4× MSAA, RTL, clipping, and assigned Grid invalidation. Use three slots to match the Windows command `Duxel.Graphics.Tests <fresh-output> container-pixels`.

Append `virtual-list-pixels` for matching reference draws of keyed virtual lists with fixed and variable heights. Three slots match `Duxel.Graphics.Tests <fresh-output> virtual-list-pixels` on Windows.

Append `virtual-list-host-pixels` to write comparable RGBA captures of the fractional tail on both axes. Each scene has a clear frame, a geometry frame and a complete frame with native text. Use three slots and the same font files on both hosts for a cross-host comparison.

Append `clip-host-pixels` for the minimal scissor scenes using rectangles and triangle meshes.

Append `virtual-grid-pixels` for the two-axis reference draws. Use three slots to match `Duxel.Graphics.Tests <fresh-output> virtual-grid-pixels` on Windows.

Append `virtual-grid-host-pixels` on either host to also save raw RGBA frames with their dimensions in the file names. The isolated cell frame, circle, and scrollbar draws include scissor traces for the fixed and clipped indexed tails.
The bordered rectangle controls vary placement, per-rectangle clipping and the layer type independently.
At 1× MSAA the projected fractional rectangle also checks its blended border and clear pixels immediately outside the clip against values calculated from the geometry.

Append `projected-clip-bench` for the projected rectangle timing fixture or `projected-image-clip-bench` for its texture-sampling variant. They use the same scenes as Windows `image-shader-bench 3 projected-clip` with optional `color-image`, and save per-frame timestamps, draw counts, uploaded bytes, and image hashes.

The result includes one SHA-256 hash per raw RGBA frame. With SDK 11.0.100, runtime 11.0.0, and three slots on WSLg with Mesa lavapipe, managed and Linux x64 NativeAOT each passed 612 checks. All 628 corresponding frame hashes matched.

Arguments: `<fresh output directory> <frame slots1 or3> <Latin regular> <Latin bold> <Korean regular> <Korean bold> <monospace>`. Validate all fonts before opening a window. Results go to `result.json`; failures retain the original error and exit nonzero. Builds require `-p:DuxelValidationHooks=true`. Pinned RC3 NativeAOT commands, executable hashes and scope are in the [integrated guide](../../artifacts/linux-sparse-damage-2026-11-15/README.md) and its `commands.json`.

In the default presentation-reuse mode, the paint-group texture dependency mutation must repair the consumer area on an initialized image and match forced full repaint at 1× and 4× MSAA. With fixed .NET 11.0.0 on WSLg, managed and NativeAOT matched in 44 paint-group images at each of one and three frame slots. [Readback verification](W:/Cache/Duxel/gallery-source-20261117/paint-group-linux-2125/verification.json).
