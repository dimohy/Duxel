# Linux Gallery startup (2026-11-22)

On WSLg Dozen, the Gallery's first frame spent most of its startup time creating the general SVG stroke pipeline. The first screen needed solid strokes. Its pipeline also compiled the code for following and subdividing dashed curves.

The scene validator records whether each stroke program contains a dash. The renderer uses a solid-stroke shader when no visible stroke program contains a dash. Both shaders share the same solid-stroke code, buffers, and pipeline layout. The general pipeline is created when a visible scene first needs dashes.

| WSLg Dozen on RTX 5070 Ti | Before median | After median | Reduction |
| --- | ---: | ---: | ---: |
| App initialization to first presented frame | 5701.04 ms | 1775.06 ms | 68.9% |
| Initial stroke pipeline creation | 4164.71 ms | 254.78 ms | 93.9% |

The managed Release builds used SDK 11.0.100, identical fonts and window extent, and three independent runs per variant. The Vulkan pipeline cache reported a hit in all six runs. These measurements cover startup. Opening a scene with dashed strokes for the first time still incurs the general pipeline's creation cost.

The Linux Gallery presentation probe exercised the SVG document, features, arc geometry, and both target backgrounds. Windows solid-stroke and dash pixel checks each passed 46 cases. All 136 paired Windows stroke captures matched the baseline byte for byte.

To reproduce the shader variant from the repository root:

```powershell
glslangValidator -V --target-env vulkan1.2 -Os -DDUXEL_SOLID_STROKES=1 -o src/Duxel.Vulkan/Shaders/vector_stroke.solid.comp.spv src/Duxel.Vulkan/Shaders/vector_stroke.comp
spirv-val --target-env vulkan1.2 src/Duxel.Vulkan/Shaders/vector_stroke.solid.comp.spv
```

## Windows Gallery SVG CodeView

The Arc comparison tab combines six source examples with their SVG previews. The first tab frame spent roughly 84 ms in native CodeView layout. During scrolling, layout stayed cached. The 512-entry DirectText cache evicted the styled runs the next scroll pass needed.

The shared cache now keeps up to 1,536 total entries and still limits ordinary text to 512 entries. The existing idle texture byte limit and frame lifetimes still apply. CodeView also walks the sorted style spans once when it builds hard-line layout inputs.

| Windows Gallery pass | Control CPU time | Updated CPU time | Paragraph rasters, control → updated |
| --- | ---: | ---: | ---: |
| First traversal, 64 frames | 7.62 ms | 3.94 ms | 2,634 → 877 |
| Repeated traversal, 64 frames | 6.50 ms | 2.72 ms | 1,706 → 0 |
| Native width changes, 16 frames | 27.69 ms | 26.02 ms | 2,513 → 1,927 |

The times are medians of per-run frame medians from three managed Windows runs per variant with the RTX 5070 Ti. Both variants used the updated layout code, and the comparison varied only the cache limit. CPU time covers construction and the renderer call. Vulkan query results joined by submission ID put the GPU draw median below 0.04 ms in both scroll passes. The scroll capture after the final traversal matched byte for byte. This test excludes idle time, the screen capture, and the Win32 size call. The window change measurements overlap and leave window-resize performance open. Two of the slowest candidate resize frames spent more than 27 ms processing texture updates and also rebuilt native text layouts. A short atlas experiment lowered some resize spikes at the cost of extra coverage upload bytes. First tab layout and sustained memory need further measurement.

Build `tests/Duxel.Graphics.Tests/Duxel.Graphics.Tests.csproj` in Release with `DuxelValidationHooks=true`. Run the built DLL with a fresh output directory and `gallery-svg-code-stages`. Set `DUXEL_TEST_CODE_CACHE_ENTRIES=512` for the control. Set `DUXEL_TEST_CODE_RESIZE=1` to include the native width sequence. The run writes `stages.tsv` and the Vulkan timing log.

### Share repeated hard-line layouts

A bounded cache in the UI context shares native line layouts when CodeViews have the same text, font and style properties, render mode, and wrap width. Each document still assigns its own source offsets and paint-style indices. The cache resets when layout resources change. It stores at most 512 lines of up to 256 UTF-16 code units each.

| Same executable, three runs per variant | Shared layout off | Shared layout on |
| --- | ---: | ---: |
| First Arc tab frame, CPU | 139.96 ms | 88.76 ms |
| CodeView layout in that frame | 82.49 ms | 36.98 ms |
| Layout during window width changes | 9.97 ms | 5.50 ms |
| Whole CPU frame during window width changes | 25.26 ms | 25.06 ms |
| Repeated scroll, CPU | 2.48 ms | 3.39 ms |

First-tab CPU time fell 36.6%. All six final scroll captures and the paired final resize captures matched byte for byte. A separate mixed Korean and English layout check compared source positions, paint indices, glyph placements and carets. The per-run repeated-scroll times overlapped. That phase made zero native CodeView layout calls in both variants. Window-width changes still generated the same raster and upload work.

Set `DUXEL_TEST_DISABLE_SHARED_CODE_LAYOUT=1` for the line-layout control without changing the earlier raster cache limit. Run `codeview-layout-cache` to check the mixed-language layout.

## CodeView glyph placement during width changes

CodeView already retained native shaping for hard lines. Every width change still rebuilt the positioned glyph runs for all lines, including lines whose layout and vertical position had stayed the same. The common CodeView layout now retains one positioned run set per hard line. It rebuilds that set when wrapping changes the line layout or an earlier line moves its vertical position. Source edits create new line records with current source offsets and style indices.

The Windows comparison used the Gallery SVG **Arcs and curves** tab, an RTX 5070 Ti Laptop GPU, MSAA 4, a 1500×960 starting viewport, and .NET SDK 11.0.100 with the managed runtime 11.0.0. Each of three runs per build opened the tab, scrolled down, returned to the initial offset, repeated the scroll, and changed the Win32 width in 16 steps. The run order was A/B, B/A, A/B. The comparison switched only `Duxel.Core.dll` in the test runtime. CPU frame time covers UI composition and the renderer call. The call to change the window size and image capture took place outside that interval.

| Metric | Before | After | Change |
| --- | ---: | ---: | ---: |
| Width-change CodeView layout median | 5.04 ms | 3.26 ms | −35.4% |
| Width-change UI composition median | 16.43 ms | 12.97 ms | −21.1% |
| Width-change CPU frame median | 24.45 ms | 20.37 ms | −16.7% |
| Width-change CPU frame p95 and p99 | 48.80 ms | 48.15 ms | Run ranges overlap |
| Width-change GPU median, joined by submission | 0.0521 ms | 0.0519 ms | Run ranges overlap |
| Cold tab CPU frame | 92.07 ms | 94.82 ms | Run ranges overlap |
| First scroll CPU frame median | 3.34 ms | 3.25 ms | Run ranges overlap |
| Repeat scroll CPU frame median | 3.83 ms | 3.74 ms | Run ranges overlap |

The after-build width-change composition and CodeView layout median ranges were below the before-build ranges. The CPU frame medians for individual runs overlap at their endpoints. A slow width-change frame still rasterized hundreds of runs and spent roughly 23–25 ms processing texture updates. The cache retains one set of positioned runs per line and only the current line layout as its placement key. Its long-session memory effect has not been measured.

All six final scroll captures matched each other, as did all six final width-change captures. Focused checks covered mixed-script placement, caret geometry, the case where reflow moves a later line, and both scrollbar axes. The Gallery verification covered 335 checks. The SVG preview zoom and rotation test posted Win32 pointer input and captured the final view.

Validation commands use the test runtime built with `dotnet build tests/Duxel.Graphics.Tests/Duxel.Graphics.Tests.csproj -c Release`. Set `DUXEL_TEST_CODE_GALLERY_VIEWPORT=1` and `DUXEL_TEST_CODE_RESIZE=1` for `gallery-svg-code-stages`. The focused test modes are `codeview-layout-cache`, `codeview-scrollbars`, `gallery-svg-code-scroll`, `gallery-svg-arc-scroll`, `directtext-cache`, `multiline`, `observer-gallery`, and `gallery-svg-pointer arc win32 burst`. Local run data in `L:/Cache/Duxel/svg-code-frame-20261122/verification.json` includes per-run frame distributions, GPU samples, and capture hashes.

The live Windows Gallery test sent wheel messages and 20 width changes to the selected tab. Three runs per build with the same executable and freshly built Core control showed overlapping app timing ranges. The controlled wheel medians were approximately 0.71 ms on both builds. An earlier cached local Core assembly gave higher wheel timings, so that comparison was excluded from the patch's performance results. These are sampled application timings, with idle frames also present in the resize interval. They do not measure the delay from the physical mouse to the displayed frame. The reported 149 ms scroll frame did not recur in the isolated comparison. Raw logs are indexed in `L:/Cache/Duxel/svg-code-frame-20261122/live-summary.json`.

## Staging space for bursts of small texture uploads

On the same Windows SVG tab, one width-change frame uploaded 316 glyph coverage textures and 462,724 bytes. Three instrumented runs showed that all 316 textures were newly allocated. Vulkan device memory allocation took 10.08–20.95 ms. The staging buffer also filled seven times before its final submission, and upload wait time was 6.50–11.20 ms. The backend now reserves the aligned payload size before recording a top-level texture batch, up to 512 KiB. Larger payloads still grow the staging buffer through the existing path. The fence, layouts, and retirement rules are shared with the existing upload path.

The comparison used three separate processes per arm in A/B, B/A, A/B order, at 1500×960 with MSAA 4 on the RTX 5070 Ti Laptop GPU. It switched only the Vulkan assembly and ran on managed .NET 11.0.0 with SDK 11.0.100. The CPU timings exclude the Win32 size call and captures.

| Metric | Before | After | Change |
| --- | ---: | ---: | ---: |
| Burst texture processing | 23.51 ms | 14.03 ms | −40.3% |
| Burst upload submissions | 8 | 1 | −87.5% |
| Burst upload wait | 9.501 ms | 0.0034 ms | −99.96% |
| Burst CPU frame | 48.64 ms | 42.23 ms | −13.2% |
| Width-change CPU median | 20.46 ms | 17.07 ms | −16.6% |
| Width-change CPU p95 and p99 | 48.64 ms | 42.23 ms | −13.2% |
| Width-change GPU median | 0.0523 ms | 0.0518 ms | Run ranges overlap |

The aligned reservation does not reduce the number of new images or uploaded bytes. Each phase's final PNG was byte-identical across all six runs. The change preserves RGB coverage and single-channel coverage through the existing upload functions. The memory reservation can grow the retained staging buffer earlier than before and remains subject to the existing idle trim. Native image allocation and long-session private and working-set measurements remain open. The first-scroll CPU ranges overlap. Repeat-scroll CPU composition varied even when no texture upload occurred, so the repeat timings are not attributed to the reservation.

Use the same Release `gallery-svg-code-stages` invocation and viewport flags described above. Windows `directtext-cache`, `gallery-svg-code-scroll`, `observer-gallery` (335 checks), and `gpu-stroke-pixels` (46 checks) passed. `presentation-reuse` reached the same failure in both arms when checking the newly visible area of a resized paint group. Linux Gallery passed 108 of 108 present checks with RTX 5070 Ti on WSLg Dozen, including the SVG arc. That execution used a Linux build to resolve the Linux HarfBuzz native asset. The earlier Windows-built Linux probe stopped at the missing asset. The per-run frame distributions, upload waits, exact captures, instrumentation, and binary receipts are available in `L:/Cache/Duxel/svg-texture-stages-20261122/verification.json` and the adjacent logs.

## Shared device memory for small text images

The staging change removed repeated transfer waits. The remaining burst made hundreds of Vulkan device memory allocations for individual glyph coverage images. Reusable text images now own aligned ranges in 2 MiB pages dedicated to optimal-tiled text images. The renderer keeps each range with its image through frame-fence retirement and idle image reuse. Eviction returns the range, and a page is freed when its last range is released. Image creation failures release their bound memory before a texture can be registered. The renderer registers the resource before recording its upload so normal device teardown can find it if recording fails.

The Windows comparison used the same 1500×960 SVG tab, MSAA 4, RTX 5070 Ti Laptop GPU, SDK 11.0.100, and managed runtime 11.0.0. A separate checkout contained only these changes. The test ran three processes per arm in B/A, A/B, B/A order and switched only `Duxel.Vulkan.dll` in the fixture. CPU measurements exclude the Win32 size call and captures.

| Metric | Before | After | Change |
| --- | ---: | ---: | ---: |
| Processing the 316-texture burst | 11.31 ms | 2.32 ms | −79.5% |
| Width-change CPU p95 and p99 | 43.81 ms | 27.71 ms | −36.8% |
| Cold tab CPU time | 91.60 ms | 88.39 ms | −3.5% |
| Width-change CPU median | 19.27 ms | 20.67 ms | +7.3%, overlapping run ranges |
| Repeat scroll CPU median | 2.95 ms | 3.85 ms | +30.4%, overlapping run ranges |

On the matched workload the burst and tail each improved in all three pairs. The first independent comparison also had a higher width-change median. The median increase was concentrated in UI composition, with renderer CPU medians about the same. The measurements do not establish a general scroll or resize speedup. All final scroll PNGs matched and all final width-change PNGs matched.

The new `text-image-memory` mode in `Duxel.Graphics.Tests` and `Duxel.Vulkan.Linux.Tests` exercised one and three in-flight slots, injected an image-view failure, and ran 96 batches of 48 R8 and RGBA images per configuration. Every configuration exercised both reuse and idle eviction. Reserved page memory peaked at 22–24 MiB on Windows and 26–32 MiB on WSLg Dozen. All four returned to zero on renderer shutdown. These figures count Vulkan page capacity in the fixture, not process memory or GPU-wide VRAM. The separate long-running app memory comparisons remain open.

The isolated checkout passed `Duxel.slnx -c Release`, Windows `directtext-cache`, `gallery-svg-code-scroll`, `gpu-stroke-pixels`, and `observer-gallery` (335), plus Linux Gallery (108 presentations) on RTX 5070 Ti via Dozen. Repeat the timing with `gallery-svg-code-stages` and the flags above. Raw distributions and renderer hashes are in `L:/Cache/Duxel/svg-texture-page-product-20261122/review-performance/verification.json` and `run-receipts.json` in the same folder. Lifetime outputs are in `review-text-image-memory` and `review-dozen-lifetime` under the parent folder.

## Keep recently viewed styled text across fast frames

The Gallery's continuous loop can draw more than 1,000 frames per second. The previous 600-frame idle limit could evict CodeView paragraph runs after about half a second, so returning to recently viewed source would rasterize the same runs and upload their textures again. Styled paragraph runs now expire after 10 seconds of UI elapsed time. The existing cache entry and idle texture byte limits still apply, and the ordinary and short-lived text policies are unchanged.

The actual Windows Gallery opened SVG → Arcs and curves on RTX 5070 Ti Laptop GPU, MSAA 4, VSync off and continuous rendering. The managed build used SDK 11.0.100 and runtime 11.0.0. Both versions used the same executable and renderer with only `Duxel.Core.dll` swapped. Each of three independent runs per arm warmed up the tab, scrolled down to the source, then posted six alternating sweeps of 30 wheel events with about 1.8 seconds between sweeps. The table gives medians across runs for the five return sweeps. One candidate run overlapped a compiler invocation and was repeated. The logs preserve the excluded run.

| Metric | Before | After | Change |
| --- | ---: | ---: | ---: |
| Texture copies | 2,586 | 277 | −89.3% |
| Bytes submitted with texture copies | 5,898,152 | 398,620 | −93.2% |
| Total CPU time processing textures across the sweeps | 14.61 ms | 6.87 ms | −53.0% |
| Per-frame CPU median | 0.656 ms | 0.676 ms | +3.1% |
| Per-frame CPU p95 | 0.935 ms | 0.975 ms | +4.3% |
| Per-frame CPU p99 | 2.109 ms | 1.924 ms | −8.8% |
| Slowest 1% CPU-frame rate equivalent | 317.3/s | 359.7/s | +13.4% |

CPU timings include the render callback and renderer call. They exclude time between calls, so the rate equivalent gives a CPU timing comparison. The reported 149 ms frame cadence did not occur in this replay. New wrap widths can still generate new layout and textures, and native window resizing needs separate verification. The longer retention interval may keep more of the existing bounded cache occupied. In the `gallery-svg-code-stages` replay, the final SVG captures matched byte-for-byte. `codeview-paragraph-cache-age` checks initial upload, reuse after 650 short frames, and expiry after more than 10 seconds. Raw logs, input replay, binary receipts and metrics are in `L:/Cache/Duxel/svg-live-spike-20261122/verification.json` and its parent directory.

## Avoid redundant DirectWrite paragraph preparation

The SVG arc page rebuilt nine CodeView paragraph layouts per width on the measured path. An instrumented control measured 12.86 ms of UI composition, including 3.37 ms assembling native CodeView layouts, 0.86 ms resolving styles, and 0.40 ms assembling visual row heights. A separate candidate reused CodeView typography and scanned ordered style runs once. Its 18 matching captures preserved pixels, but width-change p95 increased from 27.18 to 30.89 ms. The candidate was dropped.

The Windows backend prepares style spans without expansion buffers when they contain no tabs. It still composes Hangul and maps glyphs and carets back to the original UTF-16 positions. Lines without trailing whitespace reuse the full line's hit-test extents for its visible width. The backend continues to take shaping and hit-test geometry from DirectWrite. [DirectWrite glyph positions and the draw callback](https://learn.microsoft.com/en-us/windows/win32/api/dwrite/nf-dwrite-idwritetextrenderer-drawglyphrun)

The A/B fixture ran the Windows SVG arc page at 1500×960 and MSAA 4 on RTX 5070 Ti Laptop GPU using SDK 11.0.100 and managed runtime 11.0.0. Both arms used the same instrumented Core and graphics fixture. Only `Duxel.Platform.Windows.dll` changed. Three fresh processes per arm alternated order. Each opened the page and ran the 64-input first and repeat scroll phases before the 16 width samples. The captures ran separately. The table uses medians across runs.

| Metric | Before | After | Change |
| --- | ---: | ---: | ---: |
| Per-width managed composition allocation | 3,208,260 B | 3,155,044 B | −1.7% |
| Per-width CPU for native CodeView layout assembly | 3.371 ms | 3.192 ms | −5.3% |
| Width-change CPU median | 20.16 ms | 19.16 ms | −5.0% |
| Width-change CPU p95 and p99 | 27.93 ms | 27.13 ms | −2.8% |
| Slowest 1% CPU-frame rate equivalent | 35.81/s | 36.85/s | +2.9% |
| Width-change GPU median | 0.0521 ms | 0.0543 ms | +4.3% |
| First arc tab CPU frame | 88.02 ms | 85.86 ms | −2.4% |

Allocation decreased in all three matched comparisons. CPU ranges overlap. Repeat-scroll CPU p99 increased from 7.73 to 11.22 ms even though uploads stayed at zero, and all 18 matching screenshot pairs were byte-identical. Width changes still submitted 462,724 coverage bytes across the phase. These timings exclude the window size call and the capture, and they do not establish app FPS or resolve the reported 149 ms stall. `gallery-svg-code-stages` with `DUXEL_TEST_CODE_GALLERY_VIEWPORT=1` and `DUXEL_TEST_CODE_RESIZE=1` replays the input. Measurements and hashes are in `L:/Cache/Duxel/codeview-reflow-style-20261122/direct-verification.json`, with allocation and layout stages in `direct-probe.json` in the same directory.

## Measure CodeView height before composing document geometry

Wrapping a clipped CodeView still changes the scrollable document height. The measurement uses native hard-line layouts and their style heights to compute the exact block height. The visible editor joins those same line layouts into document-wide glyph runs, carets and accessibility geometry when needed. CodeView retains resolved paragraph styles with the source revision and layout resources so width changes and the subsequent first paint can share them. Non-native text takes the existing measurement path.

Three fresh Windows processes per arm replayed the Gallery SVG arc page and 16 width samples with SDK 11.0.100, managed runtime 11.0.0, MSAA 4 and RTX 5070 Ti Laptop GPU, starting at 1500×960. The fixture and renderer were identical. The runs alternated the order of the two Core DLLs in pairs. The table shows medians across runs. Capture readback ran separately from the timed samples.

| Metric | Before | After | Change |
| --- | ---: | ---: | ---: |
| First arc tab CPU frame | 84.50 ms | 72.73 ms | −13.9% |
| Width-change CPU median | 20.80 ms | 16.51 ms | −20.6% |
| Width-change CPU p95 and p99 | 28.42 ms | 23.53 ms | −17.2% |
| Slowest 1% width-change CPU-frame rate equivalent | 35.18/s | 42.50/s | +20.8% |
| Width-change GPU median | 0.0525 ms | 0.0526 ms | Within run variation |
| First traversal CPU p99 | 11.68 ms | 18.76 ms | +60.7% |
| Repeated traversal CPU median | 3.79 ms | 3.56 ms | −6.0% |
| Repeated traversal CPU p99 | 6.36 ms | 6.98 ms | +9.9% |

Width-change CPU median and p95 improved in all three pairs. First traversal step 20 incurred about 7 ms of native paragraph assembly when a previously clipped block became visible. The median of summed first-tab plus first-traversal CPU work was 314.85 → 318.85 ms across three runs. The reordering improves width changes and initial tab display, with a slower first scroll into new content. GPU timing and upload volume remained close. The timed fixture did not reproduce the reported 149 ms cadence.

All 18 screenshot pairs were byte-identical and the mixed-source test produced identical measured and fully laid-out heights at three widths, covering a blank line in a comment, tabs, decomposed Hangul, numbers and punctuation. Reproduce the Windows run with `gallery-svg-code-stages` and the viewport and width flags above. Raw frame times, GPU submission matches, input, and screenshot hashes are in `L:/Cache/Duxel/codeview-height-only-20261122/verification.json` and its parent directory.

All 223 replay steps per process agreed on scroll offset, visible code height and coverage upload bytes. The Windows observer suite passed 2,374 checks after its scroll assertion was pointed at the SVG document's own scroll pane. The old fixture could not find a common page pane on either version. Windows GPU Gallery, multiline selection and scrollbar checks and WSLg Dozen Gallery passed, including 108 verified Linux presentations.

## Keep CodeView positioning loops at line scope

The first-scroll spike at input 20 reused the native layouts for the entire 4,335-character block. Three instrumented runs found zero new native builds on that input. The disassembly showed CoreCLR compiling `UiCodeParagraphLayouts.Prepare` on stack while it was active. With the same instrumented binaries and three fresh processes per group, toggling `DOTNET_TC_OnStackReplacement` changed the median assembly time from 7.7204 to 0.6442 ms and the input's CPU frame from 19.4453 to 6.1151 ms. Disassembly confirmed Tier1-OSR only in the enabled group. This is a diagnostic comparison with a runtime-wide switch. [CoreCLR OSR design and diagnostics](https://github.com/dotnet/runtime/blob/main/docs/design/features/OsrDetailsAndDebugging.md)

The product change moves caret and glyph placement loops into line-scoped methods. The source offsets, float operations, coverage textures and runtime options use the same inputs. The first candidate, which reduced glyph copies and reused accessibility style heights, did not consistently improve the target frame. An `AggressiveOptimization` attribute improved the first-scroll tail but increased median width-change CPU time in its comparison. A separate style scan on top of the line-scoped loops increased the first-scroll tail. These three trials were excluded from the product patch.

The product replay used SDK 11.0.100, managed runtime 11.0.0, RTX 5070 Ti Laptop GPU, 1500×960 and MSAA 4. The two Duxel.Core DLLs ran three fresh processes each in alternating pairs, with the same executable and Vulkan renderer. Timing excludes capture readback and the window resize call. The table shows medians across runs.

| Metric | Before | After | Change |
| --- | ---: | ---: | ---: |
| First SVG tab CPU frame | 72.85 ms | 72.16 ms | −0.9% |
| First-scroll CPU median | 3.48 ms | 3.40 ms | −2.3% |
| First-scroll CPU p95 | 6.15 ms | 6.13 ms | −0.3% |
| First-scroll CPU p99 | 18.82 ms | 14.03 ms | −25.4% |
| Slowest 1% first-scroll CPU-frame rate equivalent | 53.14/s | 71.26/s | +34.1% |
| First-scroll GPU median | 0.0422 ms | 0.0417 ms | Within run variation |
| Width-change CPU median | 17.25 ms | 14.09 ms | −18.3% |
| Repeated-scroll CPU median | 3.92 ms | 2.71 ms | −30.8% |

First-scroll p99 improved in all three pairs. The width and repeated-scroll samples overlap between independent processes. The fixture did not reproduce the 149 ms interval. All 223 replay steps per run matched on scroll offset, visible code height and uploaded coverage bytes, and 18 screenshot pairs matched byte-for-byte. Re-run with `gallery-svg-code-stages`, `DUXEL_TEST_CODE_GALLERY_VIEWPORT=1`, and `DUXEL_TEST_CODE_RESIZE=1`. Frame, GPU and image evidence is in `L:/Cache/Duxel/codeview-first-scroll-20261122/split-verification.json`. The JIT comparison is in the adjacent `osr-verification.json`.

## Compact retained Vulkan triangle indices

Each ERD table keeps a separate static triangle buffer. Vulkan now stores its indices as 16-bit values when the mesh fits the addressable vertex range, each source index fits, and primitive expansion is unnecessary. Upload converts directly into the staging buffer and clears padding for odd index counts. The static buffer records its format and binds it with the matching Vulkan index type. Rotating reuse and updates require both format and shape to match. Dynamic, expanded and compiled indices keep their existing 32-bit representation. [Vulkan index binding](https://registry.khronos.org/vulkan/specs/latest/man/html/vkCmdBindIndexBuffer.html)

The Windows test used RTX 5070 Ti Laptop GPU, 1440×940, MSAA 4, 1,200 ERD tables, 120 initial fit frames with a moving table, and 93 timed zoom frames. It ran three fresh processes per arm with alternating order, the same .NET SDK 11.0.100 and runtime 11.0.0, and only the Vulkan DLL exchanged. The table shows medians across runs. Five additional readbacks per run were excluded from timing. The physical extent was checked at each step.

| Metric | Before | After | Change |
| --- | ---: | ---: | ---: |
| Active static geometry bytes | 723,494,016 | 588,533,056 | −18.7% |
| First-frame staging bytes | 723,494,020 | 588,533,060 | −18.7% |
| First fit-view CPU frame | 1,878.71 ms | 1,939.89 ms | +3.3%, overlapping ranges |
| Fit-view motion CPU median | 3.493 ms | 3.443 ms | −1.4% |
| Fit-view motion CPU p95 | 4.015 ms | 6.062 ms | +51.0%, overlapping ranges |
| Continuous zoom CPU median | 1.660 ms | 1.673 ms | +0.8% |
| Continuous zoom GPU median | 0.719 ms | 0.702 ms | −2.3% |

All five capture poses matched across the six default-renderer runs. Five additional capture pairs matched with layer compilation disabled, exercising the direct index-buffer binding. The change saves 128.71 MiB of static buffer payload on this scene. The measurements do not show a consistent frame-time improvement and exclude swapchain creation and capture readback. [Windows fixed replay](L:/Cache/Duxel/erd-static-ushort-20261122/fixed-verification.json) · [Direct draw captures](L:/Cache/Duxel/erd-static-ushort-20261122/native-parity.json)

On WSLg Dozen, the same Linux ERD executable and 1,200-table motion tour ran for 120 measured seconds per arm, with only the Vulkan DLL exchanged. The physical extent stayed at 1440×940 with MSAA 4 and the RTX 5070 Ti. Each process exited normally with 121 RSS samples. Active static buffer payload peaked at 883.73 → 718.91 MiB. Compiled layer indices peaked at 330.03 MiB in both runs. The last-30-sample RSS median was 1,236.42 → 1,238.71 MiB. These runs show a reduction in static GPU buffer payload while process RSS stayed in the same range. The sampled CPU and GPU records in `live-verification.json` are not a full presentation-interval distribution. [WSLg memory and module receipts](L:/Cache/Duxel/erd-static-ushort-20261122/live-verification.json)

The Release solution built with zero warnings and errors. Windows verification passed 173 DirectText and static geometry checks, 335 Gallery checks, and four scrollbar checks covering the two SVG examples. The WSLg Gallery presented all 108 verification views, including the vector previews.
