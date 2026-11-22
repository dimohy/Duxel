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
