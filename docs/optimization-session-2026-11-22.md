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

### Concurrent preparation of the first scene and stroke pipelines

On WSLg Dozen with an RTX 5070 Ti, the remaining startup cost was concentrated in the first vector scene and solid-stroke pipeline creations. Their sequential medians were 332.46 and 263.01 ms. All vector buffer allocations together took a median of 10.00 ms.

When a stroke scene needs a previously unseen scene pipeline, the Vulkan renderer can prepare its scene pipeline and stroke work concurrently. Both operations complete before the scene can register or dispatch. The pipelines own separate layouts and use the same internally synchronized pipeline cache. [Vulkan compute pipeline creation](https://docs.vulkan.org/refpages/latest/refpages/source/vkCreateComputePipelines.html)

Three Linux x64 NativeAOT processes per version used .NET 11.0.0, a warm pipeline cache, a visible 1280 × 900 X11 window, and 4× MSAA. Time from renderer initialization to the first accepted frame fell from a median of 1,363.79 to 1,072.57 ms (21.35%). Time from the initial clear to that frame fell from 779.52 to 485.69 ms (37.69%). The navigation and overview X11 captures matched across all six runs. The live FPS header was outside the pixel comparison.

The Linux Gallery accepted all 108 presentation checks. Windows passed 46 solid-stroke checks, 46 dashed-stroke checks, two blend checks, and two transparent-arc checks per version. All 324 paired Windows captures matched byte for byte after rerunning the solid-stroke fixture at the same framebuffer extent as the control. The Windows Release solution built without warnings or errors.

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

## Gallery scroll hitch during render-loop recompilation

The live Windows Gallery recorded a recurring pause near frame 4,930 of a new process. A .NET EventPipe trace places a 40.384 ms Tier 1 on-stack replacement of the render-thread loop inside a 40.462 ms gap between completion of one frame and the start of the next. The trace recorded no GC suspension in that interval. Keeping the loop small and calling a separate frame body avoids recompiling the full body on the render thread. The change leaves input collection, frame scheduling, rendering, and the optional render queue on their existing paths. [.NET 11 on-stack replacement](https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-11/)

The replay used the actual Gallery window, RTX 5070 Ti Laptop GPU, SDK 11.0.100, .NET 11.0.0, MSAA 4, V-Sync off, and continuous rendering. It opened the SVG Arcs and curves tab, verified 141% SVG zoom, posted the same Win32 wheel input, and dragged the SVG zoom slider from the lower position to 262% and back to 139%. Each arm ran in three fresh processes with alternating order. The replay used the same executable, assets, instrumentation, and runtime, with only the App module exchanged. Its first scroll moved the document from 0% to 37.96% in every run. Captured intervals measure the time between completions of accepted Vulkan submissions that presented a new image.

| Phase | Longest interval in each run before | Longest interval in each run after |
| --- | ---: | ---: |
| First scroll | 11.91–12.44 ms | 3.45–13.81 ms |
| Repeated wheel scrolling | 41.52–49.22 ms | 12.12–12.42 ms |
| SVG zoom slider | 12.07–12.25 ms | 12.21–12.40 ms |
| Programmatic window width changes | 19.35–24.32 ms | 15.84–21.27 ms |
| Width changes bracketed by Win32 sizing messages | 11.93–12.14 ms | 12.83–15.06 ms |

The median of the three worst repeated-wheel intervals fell from 49.03 ms to 12.29 ms, a 74.9% decrease. The render loop recompile was an isolated hitch in each baseline run. The replay did not reproduce the reported 149 ms Gallery HUD sample. The width-change results vary by input path, and general resize work remains on hold. [Matched replay and trace correlation](L:/Cache/Duxel/svg-native-cadence-20261122/pair-verification.json) · [Runtime event trace](L:/Cache/Duxel/svg-native-cadence-20261122/run-17/runtime.tsv)

The Release solution built without warnings or errors. Windows passed 335 Gallery checks, four SVG scrollbar checks, 76 app input checks, 32 scheduling checks, and 11 shutdown and failure checks. The WSLg Dozen Gallery presented all 108 verification views.

A separate single pair used a copy of the local working source, with only the frame-loop change applied to the second build. The longest repeated-wheel presentation interval was 42.27 → 12.42 ms in that pair.

## Compact indices for dense compiled Vulkan layer groups

The compiled shader reads the local vertex index from the low 16 bits and the draw descriptor from the high bits. Dense groups now store the local index as an unsigned 16-bit value. The draw adds the descriptor through Vulkan's base vertex offset. Smaller groups retain the 32-bit representation and one draw per contiguous visible range. A group must contain at least 128 Ki indices and average at least 1,024 indices per descriptor. Each compact group saves at least 256 KiB of index payload. Cache admission and accounting use the stored width. [Vulkan indexed drawing and base vertex](https://docs.vulkan.org/refpages/latest/refpages/source/vkCmdDrawIndexed.html)

The Windows replay used 1,200 ERD tables, RTX 5070 Ti Laptop GPU, 1440×940, MSAA 4, and .NET 11.0.0. Each comparison ran three processes per arm in alternating order, with the same program and only the Vulkan module exchanged. Each run replayed 219 poses. Five readback poses in both comparisons matched the control byte-for-byte. The 32-bit compiled renderer and fully direct renderer also differ at these poses. The compact renderer preserves the existing compiled renderer's image.

| Windows metric | Before | After |
| --- | ---: | ---: |
| Compiled index payload, detailed profiling on | 270,330,552 bytes | 135,339,672 bytes |
| Native compiled draw calls, fitted motion median | 36 | 3,603 |
| Fitted motion CPU median, detailed profiling off | 3.440 ms | 3.327 ms |
| Fitted motion CPU p95, detailed profiling off | 4.092 ms | 4.126 ms |
| Continuous zoom CPU median, detailed profiling off | 1.488 ms | 1.513 ms |
| Fitted motion GPU median, detailed profiling on | 2.343 ms | 2.332 ms |
| Fitted motion CPU p95, detailed profiling on | 6.415 ms | 7.054 ms |

The render timings exclude readback from the measured phases. The first fitted CPU frame medians with detailed profiling off were 1,934.82 ms and 1,731.18 ms, with a reduction in all three pairs. With detailed profiling on, the corresponding medians were 1,737.50 ms and 1,909.68 ms, with overlapping run ranges. The Windows pairs show a reduction in compiled index payload and no consistent frame-time benefit across both configurations. The profile-on and profile-off comparisons ran at different Windows DPI settings. Each comparison kept its own physical extent and DPI fixed. [Detailed profiling](L:/Cache/Duxel/erd-compiled-u16-20261122/product-verification.json) · [Profiling disabled](L:/Cache/Duxel/erd-compiled-u16-20261122/quiet-verification.json) · [Existing direct and compiled pixel differences](L:/Cache/Duxel/erd-compiled-u16-20261122/direct-difference.json)

The indirect experiment used a packed index buffer with Vulkan multi-draw indirect commands. Its Windows replay still issued 36 Vulkan calls, with 135,261,552 bytes of compiled index and command data. On WSLg Dozen, the same executable processed 36,480 versus 12,000 profiled frame IDs over the 120-second measurements. Sampled frame duration medians were 8.15 ms and 16.40 ms. The experiment stays out of the product. [Indirect drawing contract](https://docs.vulkan.org/refpages/latest/refpages/source/vkCmdDrawIndexedIndirect.html) · [WSLg indirect experiment](L:/Cache/Duxel/erd-compiled-u16-20261122/indirect-live-verification.json)

The matched WSLg Dozen replay used 1,200 ERD tables at 1440×940 and MSAA 4, with the RTX 5070 Ti and the same executable. Only the Vulkan module changed. Each run completed 120 measured seconds and 121 process samples. The compiled index payload peaked at 346,056,552 and 173,202,672 bytes. Median elapsed CPU-side frame samples were 8.569 and 8.337 ms, with p95 at 36.725 and 25.928 ms. GPU sample medians were 0.144 and 0.111 ms. Median process RSS over the final 30 seconds was 1,232.58 and 1,235.71 MiB. The measured reduction is in GPU index buffer payload. The app profiler samples a subset of frames. [WSLg matched replay](L:/Cache/Duxel/erd-compiled-u16-20261122/product-live-verification.json)

The Windows Release solution built without warnings or errors. Windows passed 3,183 Rendering checks, 173 DirectText checks, 335 Gallery checks, and four SVG scrollbar checks. WSLg Dozen presented 108 Gallery verification views.

## Faster retained geometry fingerprints on the first ERD view

The Windows .NET thread trace ranked `UiDrawList.ComputeStaticGeometryStamp` first for exclusive stack samples during a launch and five-frame capture (21.29%). The first fitted frame in that trace spent 1,199.55 ms composing the UI and 524.59 ms submitting it. Core previously folded every byte of each vertex, index and primitive through a dependent FNV multiplication. It now hashes the four geometry streams with .NET's XXH3 implementation and feeds each stream's result into the next one. Stream lengths and order contribute to the fingerprint. Tests cover record layout, stable identity, and changes to all four streams. [Microsoft's XXH3 API](https://learn.microsoft.com/en-us/dotnet/api/system.io.hashing.xxhash3.hashtouint64) · [Windows thread trace](L:/Cache/Duxel/erd-first-fit-cpu-20261122/trace-1/fit.nettrace)

The equality contract between retained CPU geometry and its Vulkan copy is unchanged. The hash value is a cache key for the current implementation. The build uses Microsoft's `System.IO.Hashing` package, with the package and xxHash attribution listed in the Core distribution notices.

The Windows before/after ran three fresh processes per arm in alternating order on the RTX 5070 Ti Laptop GPU, using .NET 11.0.0, 1,200 tables, 1440×940 and MSAA 4. The replay used the same executable and swapped only the Core module. Each run replayed 219 fixed poses. Five rendered poses matched byte-for-byte across all six runs. Readback was outside the timed phases.

| Windows metric | Before | After |
| --- | ---: | ---: |
| First fitted CPU-side frame | 1,696.56 ms | 1,276.79 ms |
| First fitted UI composition | 1,176.69 ms | 747.45 ms |
| First fitted GPU frame | 2.494 ms | 2.495 ms |
| Fitted motion CPU median | 3.398 ms | 3.546 ms |
| Continuous zoom CPU median | 1.710 ms | 1.663 ms |

The first fitted CPU-side frame improved in each pair. Repeated motion and zoom show no consistent improvement. [Matched Windows replay](L:/Cache/Duxel/erd-first-fit-cpu-20261122/geometry-stamp-windows.json)

The actual WSLg Dozen application ran the same 1,200-table motion tour for 120 measured seconds, with 121 memory samples per run. Only the Core module changed. In this single pair, the interval after synchronous atlas setup until the first scene presentation was 3,452.00 → 2,618.83 ms. Atlas setup itself took 1,640.91 → 722.01 ms, so it is reported separately. Sampled elapsed CPU-side frame medians were 8.162 and 8.456 ms. GPU sample medians were 0.110 and 0.111 ms, and each run advanced by 38,640 frame IDs in the measurement window. Process RSS over the final 30 seconds was 1,233.98 and 1,238.42 MiB at the median. Static and compiled Vulkan peak buffer payloads were equal. [WSLg startup markers](L:/Cache/Duxel/erd-first-fit-cpu-20261122/startup-wslg.json) · [WSLg sustained replay](L:/Cache/Duxel/erd-first-fit-cpu-20261122/product-live-verification.json)

The Windows Release solution built with zero warnings and errors. Windows passed 3,183 Rendering checks, 176 DirectText and retained geometry checks, 335 Gallery checks, and four SVG scrollbar checks. The follow-up CPU trace shows glyph command merging as the most frequent caller stack for the sampled reference-copy helper. [Follow-up trace caller stacks](L:/Cache/Duxel/erd-first-fit-cpu-20261122/bulk-move-stacks.log)

WSLg Dozen presented all 108 Gallery verification views.

## Retained Vulkan index topology fingerprints

Vulkan fingerprints the index order for each retained draw list. The compiled group cache reuses its index buffer after vertex-only changes and replaces it when index content changes. It now hashes the full index span with the XXH3 implementation already used by Duxel.Core. The cache rule and index layout stay the same. The rendering regression covers index-only changes with an unchanged count and compares the updated layer to a fresh layer. [Microsoft XXH3 API](https://learn.microsoft.com/en-us/dotnet/api/system.io.hashing.xxhash3.hashtouint64)

The Windows replay used an RTX 5070 Ti Laptop GPU, .NET 11.0.0, 1,200 ERD tables, 1440×940 and MSAA 4. The fixed executable replayed 219 poses, with three alternating runs per version and only the Vulkan module exchanged. Five readback poses in all six runs matched byte-for-byte.

| Windows metric | Before | After |
| --- | ---: | ---: |
| First fitted CPU-side render call | 518.13 ms | 448.96 ms |
| First fitted CPU-side frame including composition | 1,471.80 ms | 1,307.76 ms |
| First fitted GPU rendering | 2.500 ms | 2.494 ms |
| Continuous zoom CPU-side median | 1.710 ms | 1.702 ms |

The first render call was shorter in each pair. It includes staging, submission, and any frame waits. The full first-frame elapsed measurements include UI composition, whose run ranges overlap. Later frame timing did not show a consistent gain. [Matched Windows replay](L:/Cache/Duxel/erd-glyph-batch-20261122/submission-verification.json) · [Frame and capture details](L:/Cache/Duxel/erd-glyph-batch-20261122/topology-windows.json)

Two discarded CPU-side experiments changed glyph-run command merging and deferred inspection of vector ink metrics. The glyph-run variant had first-frame medians of 1,319.16 → 1,400.57 ms with detailed profiling and 1,388.81 → 1,403.26 ms with it disabled. The deferred ink variant measured 1,267.35 → 1,395.50 ms. Each experiment retained the existing capture pixels. [Glyph-run replay](L:/Cache/Duxel/erd-glyph-batch-20261122/glyph-batch-windows.json) · [Glyph run without profiling](L:/Cache/Duxel/erd-glyph-batch-20261122/quiet-verification.json) · [Deferred ink replay](L:/Cache/Duxel/erd-glyph-batch-20261122/ink-windows.json)

The WSLg Dozen application ran the 1,200-table motion tour for 120 measured seconds per arm, with 121 memory samples each. The same executable and assets ran with only the Vulkan module exchanged. Sampled CPU-side elapsed time medians were 8.571 and 8.273 ms, and GPU sample medians were 0.109 and 0.110 ms. Compiled index buffer peak payload remained 173,202,672 bytes. Final-30-sample RSS medians were 1,235.93 and 1,238.13 MiB. In the single startup pair, the interval from full atlas readiness to first scene presentation was 3,130.70 → 2,575.93 ms. The atlas preparation times also differed. These observations do not establish a sustained frame-time or process-memory improvement. [WSLg sustained replay](L:/Cache/Duxel/erd-glyph-batch-20261122/product-live-verification.json) · [Startup markers](L:/Cache/Duxel/erd-glyph-batch-20261122/startup-wslg.json)

Windows Release built without warnings or errors. Rendering passed 3,185 checks, including both index-only revision checks. DirectText 176, Gallery 335, and four SVG scrollbar checks passed.

WSLg Dozen presented all 108 Gallery verification views.

## Windows SVG first view and rectangular coverage

In the actual Windows Gallery, opening SVG initially caused a presentation gap around 161 ms. An independent EventPipe run paired 1,460 JIT compilations on the render thread within its longest frame, accounting for 120.26 ms of that instrumented frame. This includes both first-time compilation and on-stack replacement during software rasterization. Preoptimizing the five raster methods shortened first entry but nearly doubled the second render of the complex Gallardo fixture (71.89 → 136.92 ms). That experiment was discarded.

Axis-aligned rectangle coverage now evaluates the two edge columns and fills the interior with one coverage value per row. Both versions produced identical float coverage in six inputs, including subpixel, single-column, fractional, clipped, offscreen, and 4,096×512 cases. All four complex fixture zoom captures matched byte for byte.

| Measurement | Before | After |
| --- | ---: | ---: |
| 4,096×512 rectangle coverage, median | 7.627 ms | 0.279 ms |
| Gallery SVG first entry, median of longest presentation intervals | 160.71 ms | 154.04 ms |
| Gallery repeated wheel, median of longest presentation intervals | 11.77 ms | 11.51 ms |
| Gallery scrollbar drag, median of longest presentation intervals | 12.63 ms | 11.57 ms |
| Gallardo second render at 101%, median | 71.48 ms | 79.13 ms |
| Gallardo at 101% after eight warmup renders, median | 48.15 ms | 48.13 ms |

The large rectangle spent 96.3% less time producing coverage. The Windows Gallery first entry gap was 4.2% shorter in the median. Scrolling ranges overlap, and the reported 6.7 FPS during sustained interaction did not reproduce. The second Gallardo render was 10.7% slower in the short, cold sequence. In the separate prewarmed replay with 24 renders per process, the medians were almost equal.

Gallery replay used .NET SDK 11.0.100 and runtime 11.0.0, RTX 5070 Ti Laptop GPU, MSAA 4, a fixed 1500×995 Windows window, continuous rendering and VSync off. Each published and candidate build ran three independent replays of the Arc comparison at 141%. The timings are app-side Vulkan presentation intervals. An additional trace exercised the SVG zoom slider up to 262% and back to 139%, with a longest interval of 11.35 ms. It did not exercise native window resizing.

To repeat the CPU comparisons, build the managed SVG tests from each revision and run them with a fresh output directory and with the existing --zoom-profile command. The Windows managed SVG suite passed 316 checks, the SVG API and widget suite passed 138, and the WSLg Dozen Gallery presented all 108 verification views. [Windows replay and CPU comparison](L:/Cache/Duxel/svg-live-drag-20261122/comparisons.json) · [Runtime event evidence](L:/Cache/Duxel/svg-live-drag-20261122/runtime-evidence.json) · [WSLg presentation results](L:/Cache/Duxel/svg-live-drag-20261122/gallery-verify/result.json)

## Windows reuse of unchanged Vulkan presentations

The Windows platform now exposes the native presentation invalidation token already used by the Vulkan renderer. Windows paint, window-position, size, DPI, and display notifications advance the token. Unchanged draw data can reuse the existing presentation until a native invalidation arrives. When V-Sync is on, the Windows renderer continues using FIFO presentation to pace its frame loop. [Win32 update regions](https://learn.microsoft.com/en-us/windows/win32/gdi/the-update-region)

The same instrumented binary ran three alternating comparisons with V-Sync off, with only DUXEL_VK_PRESENT_REUSE switched between runs. The input used the actual Win32 Gallery Arc comparison at 141%, a fixed 2000×995 window, and the RTX 5070 Ti Laptop GPU. Both arms ran SDK 11.0.100 and .NET 11.0.0.

| Windows replay measurement | Reuse disabled | Reuse enabled |
| --- | ---: | ---: |
| 2.5 s with no new input, median Vulkan submissions | 5,212 | 12 |
| Repeated wheel, median Vulkan submissions | 4,728 | 488 |
| Scrollbar drag, median Vulkan submissions | 6,128 | 773 |
| First 48 wheel inputs to the changed submission, median of run medians | 2.75 ms | 2.86 ms |
| First 48 wheel inputs to the changed submission, median of run p95 | 5.19 ms | 4.59 ms |

All scroll positions reached their expected offsets. The final guarded V-Sync-off build submitted 12 frames in the same idle interval and 486 during the repeated wheel. An explicit Win32 InvalidateRect during the three enabled runs was followed by a new submission within 0.80–0.98 ms. The reduction concerns Vulkan submissions. CPU composition continues in continuous mode, and the wheel latency ranges overlap.

With V-Sync on, the unguarded experiment composed 8,937 frames and submitted 10 in 2.5 s. The disabled control composed and submitted 360. With the final Windows FIFO guard the same window composed and submitted 361.

Windows and WSLg Dozen Gallery each presented all 108 verification views. The Windows scrollbar replay passed four checks. On .NET 11 the presentation reuse suite completed the same 3,853 checks and 643 identical pixel pairs before the same existing paint-group extent assertion stopped both the baseline and candidate. [Timing and native paint replay](L:/Cache/Duxel/svg-win-present-reuse-20261122/result.json) · [Baseline pixels](L:/Cache/Duxel/svg-win-present-reuse-20261122/verify-net11-base/result.json) · [Candidate pixels](L:/Cache/Duxel/svg-win-present-reuse-20261122/verify-net11-product/result.json)

## DirectText layout retention during SVG scrolling and width changes

EventPipe samples from the Windows Gallery showed DirectWrite repeatedly laying out SVG descriptions and tab headings during the slow part of the gesture. These requests reuse the same strings, even when the owning controls are outside the viewport. The idle cache trimmed their metrics to make room for text with raster data, so the next frame could lay out the same hidden text again.

Core now keeps ordinary pixel-free measurements used in the previous frame in the active working set. While active, they do not compete with raster entries for the idle budget. When measurement stops, the existing entry limits and expiry rules apply. The regression test repeats a document whose layout-only working set exceeds the idle limit and verifies its return to that limit after the document leaves the working set.

The Windows Gallery ran on .NET SDK 11.0.100, runtime 11.0.0, and an RTX 5070 Ti Laptop GPU with continuous rendering and V-Sync off. Each arm ran three separate processes with the same executable, working sources, assets, and Win32 input sequence. Only the Core assembly differed. The replay selected Arcs and curves at 141%, scrolled through the source blocks, and changed the window width through 12 values from 1,500 pixels. The table uses the median of the three per-run statistics.

| Windows Gallery measurement | Before | After |
| --- | ---: | ---: |
| Deep scroll CPU-side frame p95 | 7.482 ms | 0.781 ms |
| Deep scroll CPU-side frame p99 | 8.511 ms | 1.333 ms |
| Deep scroll 1% low, app render cadence | 95.31 FPS | 408.62 FPS |
| Width change CPU-side frame median | 2.560 ms | 0.462 ms |
| Width change CPU-side frame p95 | 13.710 ms | 0.870 ms |
| Width change CPU-side frame p99 | 17.105 ms | 8.671 ms |
| Width change 1% low, app render cadence | 48.40 FPS | 91.72 FPS |

CPU-side p95 fell by 89.6% in deep scrolling and 93.7% during the width changes. Early wheel p95 was 0.916 → 0.884 ms. The CPU timings include UI composition and the renderer call. App render cadence includes repeated frames and is independent of monitor refresh. The replay did not reproduce sustained 6.7 FPS. These are managed Windows measurements. [Three-run comparison](L:/Cache/Duxel/svg-wheel-live-20261122/comparison-summary.json) · [Frame cadence and 1% lows](L:/Cache/Duxel/svg-wheel-live-20261122/measurements.json)

The Windows Release solution built with zero warnings and errors. On .NET 11.0.0, 178 DirectText checks, four CodeView scrollbar checks, and 335 Gallery checks passed. The new layout-only regression failed with the control Core assembly and passed with the updated one. The two SVG and CodeView captures after scrolling and after a 16-step width change matched byte for byte. The Windows captures used the Gallery code-stage replay with `DUXEL_TEST_CODE_GALLERY_VIEWPORT=1` and `DUXEL_TEST_CODE_RESIZE=1`. [DirectText checks](L:/Cache/Duxel/svg-wheel-live-20261122/check-direct-text/result.json) · [Gallery checks](L:/Cache/Duxel/svg-wheel-live-20261122/check-gallery/result.json)

The settled SVG zoom and rotation capture also matched in the Win32 pointer replay. WSLg Dozen on the RTX 5070 Ti acknowledged all 108 Gallery presentations, including SVG. [Pointer replay](L:/Cache/Duxel/svg-wheel-live-20261122/product-slider/pointer.tsv) · [WSLg Gallery verification](L:/Cache/Duxel/svg-wheel-live-20261122/gallery-verify/result.json)

## Compiled layer command traversal allocations

During the 1,200-table ERD motion tour, an EventPipe trace recorded 645 sampled allocation events with stacks. The Vulkan compiled layer opacity scan accounted for 130 command-enumerator samples. The scan now visits the existing command span by readonly reference, preserving the original order and early exit.

WSLg Dozen used an RTX 5070 Ti Laptop GPU at 1440×940 with MSAA 4. With SDK 11.0.100 and runtime 11.0.0, each arm ran three times in managed mode and three times as Linux x64 NativeAOT. The Vulkan call allocated a median of 2,008 bytes per sampled frame in every control run and 712 bytes in every updated run, a 64.5% reduction. The app and Vulkan logs used separate sampling intervals to limit allocation from diagnostic formatting at the app sample points. [Allocation stacks](L:/Cache/Duxel/erd-steady-allocations-20261122/allocation-trace/allocations.csv) · [Managed results](L:/Cache/Duxel/erd-steady-allocations-20261122/alloc-comparison.json) · [NativeAOT results](L:/Cache/Duxel/erd-steady-allocations-20261122/native-alloc-comparison.json)

The separate NativeAOT binaries showed CPU-side frame p95 medians of 19.89 → 23.22 ms under the wall-clock-driven tour. A follow-up used a single NativeAOT binary with both traversal modes and a fixed simulation step. Its three runs per mode matched draw counts, culling and compiled buffer payload at all 20 sampled Vulkan submissions. The corresponding CPU-side p95 medians were 23.79 → 23.53 ms with overlapping run ranges. The allocation saving is repeatable. Sustained FPS and process RSS improvements remain unverified. [Same-binary replay](L:/Cache/Duxel/erd-steady-allocations-20261122/probe-alloc-comparison.json) · [Draw and visibility match](L:/Cache/Duxel/erd-steady-allocations-20261122/probe-geometry.json)

On Windows, each arm replayed 219 fixed poses three times. The five capture poses matched byte for byte in all six runs. Median CPU-side frame time during motion was 4.98 → 5.01 ms and during zoom was 2.90 → 2.87 ms. GPU medians were 2.333 → 2.334 ms for motion and 0.531 → 0.532 ms for zoom. [Windows fixed-pose replay](L:/Cache/Duxel/erd-steady-allocations-20261122/topology-windows.json)

The Release solution built without warnings or errors, and the Windows Vulkan presentation reuse suite passed 3,858 checks on .NET 11.0.0. [Presentation reuse checks](L:/Cache/Duxel/erd-steady-allocations-20261122/presentation-reuse/result.json)

A separate WSLg NativeAOT comparison ran the same 1,200-table scene for 180 measured seconds per version. Cumulative managed allocations fell from 457.85 to 352.96 MiB. The final 30-second median RSS was 1,182.46 → 1,181.22 MiB, and RSS rose by 14.12 → 14.62 MiB from the first window to the final window. GC commitment and peak Vulkan geometry payloads were unchanged. This single pair confirms lower allocation volume. A sustained RSS or FPS improvement remains unverified. [180-second comparison](L:/Cache/Duxel/erd-command-residency-20261122/comparison.json)

## Linux text measurement and paint reuse

The Linux backend keeps one short result from its exact measurement path. The next paint can reuse the positioned glyphs and ink bounds when the request matches and every font used for the paint is still live. Relative font resources continue through fresh layout, preserving lookup if the working directory changes. The result holds managed glyph positions and uses the existing backend-owned fonts for Cairo drawing. [Pango glyph geometry](https://docs.gtk.org/Pango/struct.GlyphGeometry.html) · [Cairo scaled-font ink extents](https://www.cairographics.org/manual-1.12.4/cairo-cairo-scaled-font-t.html)

On Linux x64 NativeAOT with SDK 11.0.100 and runtime 11.0.0, three processes per version ran nine repetitions of 2,048 measure-and-paint pairs per process. The median of per-run medians fell from 74.69 to 47.35 ms (36.6%). Allocations fell from 61,166,976 to 47,715,712 bytes (22.0%). With measurements batched before separately drawing, median time was 73.20 → 73.62 ms and allocations were unchanged. All 51 comparison cases, including Korean and Latin mixed text, multiline input, and font eviction, matched in pixels and metrics. These timings cover text processing. [NativeAOT text comparison](L:/Cache/Duxel/linux-text-measure-reuse-20261122/comparison.json)

In the actual managed ERD motion tour on WSLg Dozen RTX 5070 Ti, the instrumented backend reused 570 of 571 ordinary text raster requests. The 335 Pango geometry checks, 114 Vulkan text baseline checks, and 108 Gallery presentation checks passed. The separate app replay below covers frame time. Long-duration RSS effects of text reuse remain unverified. [ERD reuse count](L:/Cache/Duxel/linux-text-measure-reuse-20261122/erd.stdout) · [Pango tests](L:/Cache/Duxel/linux-text-measure-reuse-20261122/paragraph-geometry/result.json) · [Vulkan text tests](L:/Cache/Duxel/linux-text-measure-reuse-20261122/text-baseline/result.json) · [Gallery](L:/Cache/Duxel/linux-text-measure-reuse-20261122/gallery-verify/result.json)

Three actual app runs per version on WSLg Dozen RTX 5070 Ti used a fixed-motion ERD tour with 1,200 tables and the same 120 SVG scroll positions. The CPU frame p95 medians were 23.61 to 23.79 ms for ERD and 4.79 to 4.81 ms for the SVG page. CPU and GPU timing ranges overlapped. The measured improvement in isolated Linux text processing did not produce a measurable app-level speedup in these runs. [ERD app replay](L:/Cache/Duxel/linux-text-app-comparison-20261122/erd-comparison.json) · [SVG app replay](L:/Cache/Duxel/linux-text-app-comparison-20261122/gallery-comparison.json)

## SVG CodeView geometry during scrolling

The shared CodeView path retains up to four position-independent accessibility geometry snapshots for fully clipped code blocks, at up to 256 KiB of geometry per snapshot. Retention expires when a block leaves the render tree. Geometry is reused only when the existing content, measurement, and position checks pass. Clipped nodes still expose no text geometry.

The WSLg native Gallery ran three processes per version, with the same 120 scroll positions driven through the accessibility scroll action in each process. Rebuilding character and caret geometry fell from 45 to 15 times per run. Median managed allocation for the measured scroll fell from 8,329,616 to 3,589,752 bytes (56.9%). The median process CPU-frame p95 fell from 4.67 to 4.22 ms. GPU stage medians were 53.14 to 51.33 microseconds, with overlapping run ranges. Wall-clock frame intervals also overlapped. Scroll positions, visible code height, and source line counts matched at all 120 positions.

The Windows SVG Vulkan capture at the end of the scroll matched byte for byte. Window resizing and the reported sustained 149 ms frame are outside this verification. Default Linux Gallery reached the first accepted Vulkan frame 1.35 to 1.49 seconds from launch across three warm-cache runs with a visible X11 window. The first frame spent 0.55 to 0.60 seconds preparing six vector outputs on the CPU. Startup was measured separately and received no product change. [Scroll samples and startup timings](L:/Cache/Duxel/linux-text-app-comparison-20261122/geometry-verification.json)

## SVG bitmap preparation on first Gallery entry

Windows EventPipe traces matched six on-stack replacement compilations to the first SVG frame on the UI thread. They occupied 28.56–28.82 ms across three traces. The rounded rectangle rasterizer was one of the larger compilations. It now processes a triangle's pixel row in a separate method, preserving triangle order, sampling, clipping, and compositing. The Gallery keeps the runtime's normal compilation settings.

With SDK 11.0.100, runtime 11.0.0, and the same Windows Gallery replay, three interleaved launches per version put median bitmap preparation at 41.25 → 36.63 ms (11.2% lower). Median first CPU frame time was 167.05 → 160.89 ms (3.7% lower). The control frames ranged from 165.92 to 204.13 ms and the changed frames ranged from 158.22 to 162.83 ms. This measurement covers the first frame after navigation. Sustained scroll and window resize FPS remain unverified for this change. [CPU timing and runtime events](../artifacts/svg-cold-parallel-20261122/jit-comparison.json)

The Release solution built without warnings. Managed SVG and SVG API tests passed 316 and 138 checks respectively, and all 11 pairs of software-rendered PNGs matched byte for byte at 480 × 240. Gallery acknowledged 108 Vulkan presentations on Windows and 108 on Linux x64 NativeAOT with WSLg Dozen and an RTX 5070 Ti. [Raster comparison](../artifacts/svg-cold-parallel-20261122/rrect-pixel-comparison.json) · [Windows Gallery](../artifacts/svg-cold-parallel-20261122/windows-rrect-gallery/result.json) · [Linux Gallery](../artifacts/svg-cold-parallel-20261122/linux-rrect-verify/captures/result.json)

## Static vertex and index buffer binding

On WSLg Dozen, GDB recorded 1,528 glibc heap expansion calls with `vkBindBufferMemory` on the stack before the ERD window submitted its first frame. The renderer now places the index stream at offset zero and the vertex stream after its aligned end, in one buffer for indexed static geometry. It uploads the two ranges separately and accounts for the shared handle once at retirement. Primitive storage keeps its existing path. [GDB call stacks](../artifacts/erd-linux-text-residency-20261122/current-glibc-growth-stacks.json) · [Vulkan index buffer requirements](https://docs.vulkan.org/refpages/latest/refpages/source/vkCmdBindIndexBuffer.html)

In three alternating .NET 11.0.0 Linux x64 NativeAOT runs per version using SDK 11.0.100, RTX 5070 Ti, 1,200 tables, 1440 × 940, and MSAA 4, buffer creations and memory binds each fell from 3,614 to 2,414 (33.2%). The median sampled CPU-side frame p95 was 24.62 → 21.53 ms, the GPU timestamp median was 0.0628 → 0.0625 ms, and accepted presentation interval p95 was 10.45 → 9.94 ms. Execution ranges overlapped. First presentation times also overlapped. The board crop matched byte for byte across two additional idle runs per version. These captures exclude the live diagnostics panel and adjacent minimap. [Six-run replay](../artifacts/erd-static-bindings-20261122/comparison.json) · [Board reference hash](../artifacts/erd-static-bindings-20261122/board-control-1/pixels.json)

The same motion tour ran once per version for 180 measured seconds, with the changed version first. Last-30-second median RSS was 1,182.70 → 1,176.23 MiB. The increase from the first to the last 30-second medians was 14.34 → 14.71 MiB. The lower absolute RSS appeared in this pair and the short-run peak samples. A reduction in ongoing RSS growth remains unverified. [180-second comparison](../artifacts/erd-static-bindings-20261122/long-comparison.json)

The Windows Release solution built without warnings. With the measurement counters removed, Windows .NET 11.0.0 passed 3,858 presentation-reuse and buffer-lifetime checks at each of one and three frame slots. Gallery acknowledged 108 Vulkan presentations on Windows and 108 on the WSLg Linux x64 NativeAOT build. [Windows lifetime checks](../artifacts/erd-static-bindings-20261122/final-net11-presentation-reuse-3/result.json) · [Linux Gallery](../artifacts/erd-static-bindings-20261122/linux-gallery-product/captures/result.json)

## CodeView line number texture lifetime

CodeView line numbers now use the existing ten-second document text idle window after painting. The shared DirectText cache refreshes that deadline on subsequent use and keeps its entry and texture limits. This addresses the short wall-clock lifetime of the 600-frame rule when Gallery composes thousands of frames per second.

Three fresh Windows Gallery processes per version alternated with only the Core DLL exchanged. The replay used SDK 11.0.100, runtime 11.0.0, Vulkan 1483 × 720, no V-Sync, and 141% SVG zoom. Each process traversed the SVG arc document down, up, and down again with 129 offset-changing submissions per phase. Line number coverage bytes fell from 56,232 to 0 on the return to the top and 64,548 to 0 on the second descent in all three pairs. The first descent still requested 2,620,936 bytes for the code body in both versions.

| Second-descent CPU measure | Before | After |
| --- | ---: | ---: |
| Median frame time | 1.497 ms | 1.434 ms |
| p95 frame time | 3.055 ms | 2.834 ms |
| p99 frame time | 3.593 ms | 3.305 ms |
| Average frame-rate equivalent | 548.28/s | 597.64/s |
| Slowest 1% frame-rate equivalent | 268.23/s | 281.97/s |

Independent p95 ranges overlap. The rates above describe sampled CPU frame work. The replay has no visual display latency measurement, and it did not reproduce the reported 6.7 FPS interval. The entry and texture budgets limit retained text. The change can keep part of that budget occupied longer. [Replay comparison](../artifacts/codeview-gutter-retention-20261122/product-comparison.json)

Release solution build, six CodeView lifetime checks, and 178 DirectText checks passed. The previous version reproduced the re-upload after 650 fast frames with line numbers enabled. The changed version reused the pixels and expired them after 11 elapsed seconds. Four fixed-size Gallery scroll captures matched byte for byte. Windows and WSLg Linux x64 NativeAOT each verified 108 Gallery Vulkan presentations. [Cache expiry logs](../artifacts/codeview-gutter-retention-20261122/age-candidate.log) · [Pixel hashes](../artifacts/codeview-gutter-retention-20261122/fixed-pixel-comparison.json) · [Windows Gallery](../artifacts/codeview-gutter-retention-20261122/windows-gallery/captures/result.json) · [WSLg Gallery](../artifacts/codeview-gutter-retention-20261122/linux-gallery/captures/result.json)


## DirectWrite font collections shared within a CodeView layout pass

The Windows paragraph backend now reuses DirectWrite font collections while CodeView shapes the lines in a layout pass. Each paragraph still owns its text layout, paint effects and glyph callbacks. The batch releases its font collections before returning. This removes repeated collection registration for the same font without retaining a new cache across frames.

The Windows GPU fixture replaced every line in a 300-line mixed-script document in each composition. Three independent processes on each side used .NET SDK 11.0.100 and runtime 11.0.0. Each recorded 30 compositions and excluded the first ten for warm timing. Per-run ranges were 58.49–59.49 ms before and 20.03–26.58 ms after for paragraph layout. The instrumentation measured UI CPU work at a 950 × 640 viewport.

| UI CPU work | Baseline median | Reused collections median |
| --- | ---: | ---: |
| Composition | 61.77 ms | 29.33 ms |
| Native paragraph layout path | 58.94 ms | 26.42 ms |
| Font collection construction | 40.89 ms | 0.28 ms |
| Paragraph rasterization | 0.059 ms | 0.169 ms |
| Texture update request per composition | 27,500 B | 27,500 B |

Native paragraph time fell by 55.2% on this replacement workload. The application replay swapped the Core and Windows platform DLLs and used the same 1483 × 956 framebuffer, RTX 5070 Ti, and no V-Sync. First SVG display CPU medians were 151.19 ms and 148.97 ms. The execution ranges overlapped. The replacement workload represents code changes across the whole document and does not establish a scroll performance gain. Monitor display timing and long-duration memory were outside this comparison.

Seven captured cases included mixed scripts, tabs and decomposed Hangul, bidirectional text, supplementary-plane text, wrapping, empty source and long code. The 21 pixel pairs and 21 glyph, caret and selection geometry pairs all matched. Windows checks passed for DirectWrite shaping, paragraph geometry, font context, DirectText caching and CodeView. Four added cases verify that reused font collections preserve the same glyphs and carets. Windows and WSLg Dozen Gallery each verified 108 Vulkan presentations.

Use `replay-product.ps1` for the first-display A/B, `verify-windows.ps1` for Windows verification and `verify-linux.sh` for the WSLg build and verification in the evidence directory. [Layout and captures](../artifacts/directwrite-paragraph-stages-20261122/comparison.json) · [Gallery timing](../artifacts/directwrite-paragraph-stages-20261122/first-display-comparison.json) · [Windows verification](../artifacts/directwrite-paragraph-stages-20261122/verification/result.json) · [WSLg verification](../artifacts/directwrite-paragraph-stages-20261122/linux-gallery/captures/result.json)

## DirectWrite font reuse for vector text

The first ERD view builds vector meshes for 1,200 tables. Contextual text used to create a DirectWrite font collection for each text request. Duxel now reuses collections on the first vector-text cache miss through the end of composition. Retained scenes with no misses do not open a batch. Platforms without paragraph batching continue through the optional backend interface.

Windows RTX 5070 Ti, 1440 × 940, MSAA 4, V-Sync off. Each matrix alternated three baseline and three candidate processes using the same diagnostic executable and fixed camera and table positions. Values are medians of the per-run results.

| Measurement | Baseline | Candidate |
| --- | ---: | ---: |
| First contextual text shaping, 219-pose replay | 169.961 ms | 44.822 ms |
| First contextual text shaping, 459-pose replay | 168.310 ms | 42.532 ms |
| First CPU frame, 219-pose replay | 1,651.622 ms | 1,507.348 ms |
| First CPU frame, 459-pose replay | 1,658.543 ms | 1,553.126 ms |
| Full-view drag CPU p95 after 240 warm-up frames | 4.382 ms | 4.356 ms |
| Repeated zoom CPU p95 after 240 warm-up frames | 3.888 ms | 4.037 ms |

Contextual shaping took 74–75% less time across 1,323 calls. The first frame requested the same 588,533,060 bytes for upload. First-frame CPU times overlapped in the longer replay. CPU p95 for early drag increased from 7.101 ms to 8.959 ms in the shorter replay, with no new shaping in that interval. The warmed drag and zoom CPU and GPU ranges overlapped. Sustained FPS and long-duration memory effects remain unverified.

Both diagnostic matrices matched pixels for all 30 capture pairs. The product build replayed all 219 positions with five additional exact captures. Windows passed 650 text and CodeView checks. Windows and Linux x64 NativeAOT under WSLg Dozen each presented all 108 Gallery verification screens on the RTX 5070 Ti. This experiment exercised the ERD vector text cache. The reported SVG slowdown during scaling and scrolling remains open.

[219-pose comparison](../artifacts/erd-vector-font-batch-20261122/comparison.json) · [Warm comparison](../artifacts/erd-vector-font-batch-20261122/warm-comparison.json) · [Product pixel verification](../artifacts/erd-vector-font-batch-20261122/product-replay/result.json)

## MessageBar intrinsic measurement and CodeView selection viewport

A Windows sample of the MessageBar Gallery page found repeated DirectWrite paragraph shaping under MessageBar measurement. Measuring the same body without a width limit replaced its prepared wrapped layout. The next constrained measurement then reshaped it. MessageBar now keeps the intrinsic size separately when the body opts into measurement retention. Body changes, layout context and measurement keys still invalidate reuse.

The Windows replay ran three processes per version in alternating order on the RTX 5070 Ti at 1984 × 956, MSAA 4 and V-Sync off. Each process paused on MessageBar and applied 16 identical width changes. The same timestamped Gallery runtime loaded a different Core DLL.

| Measurement | Before | After |
| --- | ---: | ---: |
| Settled UI composition, median of run medians | 3.579 ms | 0.290 ms |
| Width interaction UI composition, median of run medians | 4.081 ms | 0.664 ms |
| Width interaction total CPU, median of run p95 | 10.135 ms | 5.826 ms |
| Width interaction GPU draw, median of run p95 | 0.092 ms | 0.081 ms |

The UI composition ranges were disjoint. Vulkan GPU timestamps and app composition counts do not measure monitor scan-out. MessageBar dismissal, reflow, accessibility and GPU pixel comparisons passed.

CodeView now tests drag overshoot against the visible editor viewport and clamps caret hit testing to the text bounds separately. The previous build failed the upper and lower padding checks and the horizontal line-number gutter check. The fixed build keeps both scroll positions stable inside the viewport, scrolls beyond the actual top and bottom edges, and preserves the existing enclosing-document drag behavior.

[Windows replay](../artifacts/gallery-messagebar-fps-44bd4f89-20261122/repeat-summary.json) · [MessageBar GPU checks](../artifacts/gallery-messagebar-fps-44bd4f89-20261122/messagebar-gpu/result.json)

## MessageBar fast slider drag

A native Windows drag exposed a bottleneck after the intrinsic layout fix. A burst could present many intermediate slider widths. Each width also rebuilt the two Gallery CodeView sources. The width slider now coalesces captured pointer moves, and the source examples keep their width during a held drag. Releasing the pointer synchronizes the final value. The live MessageBars continue following the slider. Keyboard and automation width changes still synchronize without waiting for a drag.

Three new processes per version replayed a full out-and-back drag using native pointer input. Each issued 136 moves, at a median interval of 2.10–2.13 ms, on the RTX 5070 Ti at 1984 × 956 with V-Sync off. Only the Gallery assembly was exchanged.

| Active drag measurement, median of run p95 | Before | After |
| --- | ---: | ---: |
| App UI composition | 52.874 ms | 8.662 ms |
| App CPU frame time | 58.995 ms | 14.689 ms |
| Time between accepted Vulkan submissions | 39.668 ms | 14.703 ms |

The three execution ranges did not overlap. All release checks matched the example source width to the final slider value. An event-driven replay verified the same behavior. The Vulkan submission interval measures the application path. The monitor scan-out rate was not recorded.

[Replay summary](../artifacts/messagebar-native-drag-a5ef45bc-20261122/summary.json) · [Raw replay](../artifacts/messagebar-native-drag-a5ef45bc-20261122/comparison.json)
