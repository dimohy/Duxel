# Performance session — 2026-11-23

## CodeView paragraph assembly

Managed stack samples from the first SVG CodeView scroll showed time spent assembling positioned runs and copying paragraph data. Repositioning now writes directly into the glyph array owned by the new run. CodeView also transfers ownership of the freshly assembled paragraph arrays. Public constructors still copy their input and retain the same validation.

Windows RTX 5070 Ti, .NET SDK 11.0.100, runtime 11.0.0. The Gallery diagnostic runtime and assets were identical across variants except for the rebuilt Core DLL. At a fixed 1483 × 956 framebuffer and 141% SVG zoom, each process warmed through the same page setup and then received four bursts of 24 desktop wheel events. The frame timing and allocation comparisons each used three independent processes per variant. The frame timings used application QPC with Vulkan profiling disabled.

| Metric | Before | After |
| --- | ---: | ---: |
| Peak UI-thread allocation during the first wheel burst, median of runs | 1,006,920 B | 815,896 B |
| CPU frame time during active input, median / p95 / p99 | 1.18 / 2.42 / 4.36 ms | 1.19 / 2.15 / 6.04 ms |
| Acknowledged render submissions during active input, mean / 1% low per second | 748.2 / 232.8 | 745.4 / 156.8 |

The first-burst peak allocation fell 19.0%. Timing ranges overlapped, and the candidate had a higher median p99. The experiment did not establish a change in frame speed or display cadence.

Release solution build and 109 Windows checks passed. Those checks covered mixed-script code reflow, paragraph cache lifetime, scrollbar dragging, text accessibility and defensive copies of caller-owned arrays. A 1050 × 620 desktop crop after identical scrolling matched exactly. The run data is under `artifacts/codeview-owned-arrays-20261122` in the local workspace. The existing SVG wheel replay runs with `-DesktopWheel -WheelOnly -VulkanProfile Off`; the separate allocation replay adds `DUXEL_APP_PROFILE_MEMORY=1`.

On WSLg, Linux x64 NativeAOT builds on Dozen and RTX 5070 Ti each passed 108 Gallery presentation checks. In the normal 1280 × 900 Gallery window, a click opened SVG and 30 X11 wheel events scrolled down and back. Five desktop capture pairs matched exactly in the main content area, including both scrolled CodeView positions. WSLg results are under `artifacts/codeview-owned-arrays-wslg-20261123` in the local workspace. WSLg timing, Windows NativeAOT and long-run memory for this change remain open.

## Ordered accessibility text geometry

Accessibility text capture visits graphemes in source order. The capture now advances through visual lines and boundaries in that order. Separate cursors preserve the distinction between the caret position and the snapped start and end of a grapheme, including preedit text.

Windows RTX 5070 Ti, SDK 11.0.100 and runtime 11.0.0. The 120-line mixed-script probe used 12 warmup iterations and 30 rebuild/reuse pairs in each of three independent processes per variant. It compared 5,529 character bounds and 5,530 caret bounds. All were identical. The actual Gallery replay used the same fixed window, 141% SVG zoom, four bursts of 24 desktop wheel events per run and three runs per variant. Render submission measurements cover active wheel input.

| Metric | Before | After |
| --- | ---: | ---: |
| UI composition with fresh accessibility tree, median | 2.25 ms | 1.85 ms |
| First wheel burst peak CPU frame, median | 9.24 ms | 7.80 ms |
| CPU frame time during active input, median / p95 / p99 | 1.13 / 2.18 / 4.86 ms | 1.11 / 2.11 / 3.67 ms |
| Acknowledged render submissions during active input, mean / 1% low per second | 798.4 / 211.3 | 747.1 / 126.7 |

The new-tree composition median fell 17.6% and the first wheel CPU peak median fell 15.6%. The candidate runs had longer gaps in delivery of the fastest wheel events. In a separate replay targeting 8 ms between events, the first wheel CPU peak median fell from 9.27 to 7.56 ms and the p95 submission gap was 7.34 versus 7.38 ms. Submission cadence remains inconclusive across runs and does not measure display refresh.

The Release solution build and 386 Windows regression checks passed, including text accessibility, styled source, code layout and scrollbar interactions. At the same 44% scroll position the 1050 × 620 desktop code crop matched exactly. The local run data, probe results and the 8 ms repeat are under `artifacts/codeview-accessibility-order-20261123`.

The WSLg replay used Linux x64 NativeAOT on Dozen and RTX 5070 Ti at 1280 × 900. Five main-content capture pairs matched exactly and both builds passed 108 Gallery presentation checks. The changed build also passed 916 focused Linux text and accessibility checks, 146 independent AT-SPI client checks and four host checks.

Three separate NativeAOT processes per build measured app-side CPU time with detailed Vulkan profiling disabled. The median peak UI time on SVG entry was 140.93 versus 123.91 ms, with overlapping ranges. The median first wheel CPU peak was 5.50 versus 5.71 ms, while the renderer CPU peak was 4.19 versus 4.55 ms. This replay showed no Linux scroll speedup. The sample does not establish why renderer CPU time shifted. The Linux evidence is under `artifacts/codeview-accessibility-order-wslg-20261123`.

### WSLg SVG GPU stages and memory

A separate NativeAOT replay at the same viewport matched all 593 accepted submissions in the measured phases to their Vulkan timestamps. The first wheel burst's median GPU peak was 0.144 ms before and 0.152 ms after, with overlapping ranges. Both builds had a median of 491,280 staging bytes on that burst, with zero buffer copies and zero texture bytes on the three repeated bursts.

During first SVG entry, peak-frame vector output preparation took a median of 316.06 ms before and 314.45 ms after. Two separate diagnostic runs timed the native call directly. `vkCreateComputePipelines` took 286.29 and 296.41 ms for the vector scene with stack capacity four. The Overview had already created the stack-capacity-one scene pipeline. Neither diagnostic recorded pipeline creation during the first wheel burst.

With app and Vulkan profiling disabled, the builds received 178 and 172 wheel cycles over 185 and 180 seconds. Median process RSS from seconds 20–45 to 155–180 went from 432.57 to 432.39 MiB before and from 434.81 to 433.35 MiB after. These are single-process observations per build over the stated interval. A longer-run memory bound and display cadence remain unverified. The raw measurements and temporary instrumentation patch are under `artifacts/codeview-wslg-gpu-20261123`.

### Dozen uses one pipeline for shallow vector scenes

The Linux device policy reads the standard Vulkan driver ID. For Mesa Dozen, scenes requiring stack capacities one, two, or four share the capacity-four compute pipeline. Larger scenes and other drivers use the scene's required capacity. The shader and scene data are unchanged. [Vulkan driver identification](https://docs.vulkan.org/refpages/latest/refpages/source/VkPhysicalDeviceDriverProperties.html)

On WSLg NativeAOT with Dozen and RTX 5070 Ti at 1280 × 900, three processes per policy measured the first app log and app-side CPU time with detailed Vulkan profiling disabled. Median time from launch to the first app log was 1,077.66 versus 1,077.16 ms. Median peak app CPU time on first SVG entry was 449.77 versus 145.85 ms, a 67.6% reduction. The first wheel CPU peak was 6.34 versus 5.70 ms.

In a separate GPU profile, the baseline created a capacity-four pipeline in all three SVG entries. The shared policy created it during Overview and performed no new vector-scene pipeline creation on SVG entry. First wheel GPU peak ranges overlapped. Five actual WSLg viewport pairs matched exactly, and the comparison builds passed 108 Gallery presentation checks each. A diagnostic build of the driver-gated product verified ID 23 on Dozen and ID 13 on llvmpipe. Dozen selected capacity four at startup and reused it on SVG entry. llvmpipe created capacities one and four at their original stages. Results are under `artifacts/wslg-svg-stack-pipeline-20261123` in the local workspace.

The driver-gated build passed all 108 Gallery presentation checks on Dozen. The Windows Release solution build completed with zero warnings or errors, and the Windows Gallery passed all 108 presentation checks.

## MessageBar width changes

Each width-only option update invalidated the MessageBar arrangement and the cached unconstrained size of its body. This repeated natural measurement during rapid dragging, even though the body content and its measurement context had not changed. The arrangement and ancestor measurements still invalidate on width changes. The body's natural size remains reusable until a child or other measurement input changes.

The Windows Gallery comparison used the same process image, assets, and diagnostic profiler with only `Duxel.Core.dll` exchanged between versions. Each arm ran in three independent managed .NET 11.0.0 processes (SDK 11.0.100) on an RTX 5070 Ti. After warming the page, each foreground window received two bursts of 68 desktop mouse moves at a median spacing of 2.08–2.13 ms. V-Sync and detailed Vulkan profiling were disabled. App-side CPU time and accepted presentation intervals were measured during active input.

| Metric | Before | After |
| --- | ---: | ---: |
| UI composition p95 | 8.21 ms | 6.00 ms |
| App CPU frame time, median / p95 / p99 | 12.38 / 14.28 / 16.55 ms | 10.21 / 11.41 / 13.28 ms |
| Accepted presentation interval, median / p95 / p99 | 12.58 / 14.46 / 16.73 ms | 10.41 / 12.79 / 18.61 ms |
| Accepted presentation rate, mean / 1% low | 90.68 / 59.78 FPS | 102.41 / 53.74 FPS |

UI composition p95 fell 26.9%, app CPU p95 fell 20.1%, and accepted presentation interval p95 fell 11.6%. Per-process p95 ranges were disjoint for all three metrics. The worst 1% of presentation intervals did not improve, and these intervals measure app-side acknowledgements. The separate comparison with slider markers hidden did not improve p95, so the product retains the original slider. The event-driven drag reached both ends of the tested sweep and published the final CodeView width after button release.

The Windows solution built in Release. All 52 MessageBar checks and 34 Gallery page checks passed. The baseline failed the new width-only measurement reuse check, and both variants correctly invalidated the cache after a body size change. All 64 Windows MessageBar GPU fixtures matched their reference pixels. The Windows Gallery and the Linux x64 NativeAOT Gallery on WSLg Dozen acknowledged 108 presentations each. The timing comparison uses the managed Windows runtime because Windows NativeAOT tooling remains on hold. Run it with `pwsh -File artifacts/messagebar-width-layout-20261123/isolate.ps1 -Root artifacts/messagebar-width-layout-20261123` after preparing the two profiled Core builds. Raw timing, input and binary hashes are in `artifacts/messagebar-width-layout-20261123/isolated`.

### Fast MessageBar drag follow-up

The native-window CPU trace showed repeated inline paragraph layout during width changes. A captured, opted-in slider also processed the first movement of every host input batch before coalescing the remaining moves. Text-only inline flows now retain their prepared source until content invalidation, and a single native left-to-right paragraph line can keep its geometry as long as its full advance fits. Width changes below that threshold still reflow the body. An already captured slider now starts a move-only batch at its last position. Mixed batches preserve button transitions, keyboard events, wheel input and native dispatch.

The same Windows managed .NET 11.0.0 Gallery and App binaries ran on an RTX 5070 Ti, exchanging only the Core DLL. Each version ran in three independent foreground processes with two 68-move bursts per process and 2.09–2.11 ms median event spacing. V-Sync, event-driven mode and detailed Vulkan profiling were disabled during the paired timing runs.

| Metric | Before | After | Reduction |
| --- | ---: | ---: | ---: |
| UI composition p95 | 6.27 ms | 3.44 ms | 45.1% |
| App CPU frame time p95 | 11.75 ms | 9.10 ms | 22.5% |
| Accepted presentation interval p95 | 11.93 ms | 9.30 ms | 22.1% |
| Accepted presentation interval p99 | 15.45 ms | 11.04 ms | 28.6% |

The table reports medians of per-process percentiles. The presentation interval comes from application-side Vulkan acknowledgements. The separate event-driven run reached widths 775 and 385, and the CodeView showed 385 after release. The short timing bursts establish a reduction in application latency under this workload. The severe drop reported by the user did not occur in the baseline replays. Raw timings, binary hashes and the CPU trace are in `artifacts/messagebar-drag-followup-20261123`.

Windows passed 725 focused input, inline flow and MessageBar checks. The 64 MessageBar and 776 additional inline-flow pixel comparisons matched their references. Both the Windows Gallery and the Linux x64 NativeAOT Gallery on WSLg Dozen acknowledged 108 presentations, including the MessageBar option changes and dynamic group interactions.

## Linux font selection for spaces in mixed text

Linux now retains the preceding explicit face for a space separator when the face contains that glyph. Plain and styled Pango layouts and the reported font ranges use the same rule. A tab is still four spaces in the native layout, and an unsupported space still uses the explicit font selection policy. Pango applies face attributes to the specified text ranges. [Pango text attributes](https://gnome.pages.gitlab.gnome.org/gtk/Pango/pango_markup.html)

The Linux x64 NativeAOT comparison used .NET 11.0.0 and identical Segoe UI and Malgun font files. Of 72 cases at 16, 18, 32 and 64 px, the number matching DirectWrite within 0.001 px increased from 40 to 72 for advance and from 44 to 72 for visible glyph X positions. The 32 px mixed line changed from 231.09375 px to 233.578125 px, matching DirectWrite. Caret geometry matched in 68 cases. Four bidirectional cases retained caret differences, and the baseline differed in 12 cases using only Latin letters, numbers or symbols.

On WSLg Dozen with the RTX 5070 Ti, both Vulkan variants used the original indexed glyph triangles. The mixed line's ten visible glyph X coordinates went from two matches to ten. Comparing 26 distinct perspective frames with the Windows reference, 14 had fewer differing pixels, and 12 kept the same difference count. The near view changed from 621 to 43 differing pixels at 1× MSAA and from 892 to 210 at 4×. The Linux outlines were unchanged. Each Linux variant's one-slot and three-slot captures matched exactly. Native outlines still differ across platforms, and no frame-time improvement was measured in this comparison.

The Linux x64 NativeAOT Gallery acknowledged 108 presentations. The Linux text and vector modes passed 87 and 75 checks in a focused test build. Its baseline failed the new contextual space assertion, and the updated build passed the space and tab assertions. The validation build excluded a separate radio fixture that references a missing test type on the base commit. The Windows Release solution built with zero warnings and errors. Replays and comparison data are in `artifacts/linux-contextual-space-20261123`.

### Linux regression host and baseline attribution

The shared RadioGroup allocation checks now use each host's existing capture callback. The command-line wrapper that requires `PresentationReuseTests.Fixture` is compiled only by the Linux Vulkan test project. This restores the Linux platform test build with all its modes included. The independent Pango oracle now builds the updated space face ranges from Fontconfig coverage. Its original reference differed from the product in 30 checks, all on the mixed Hangul and Latin case at five sizes. The revised oracle passed all 335 paragraph geometry checks, including native Cairo pixels, caret locations, pointer hit testing and selection regions. Both Linux test projects ran on .NET 11.0.0 NativeAOT under WSLg Dozen. All eight platform modes passed 667 checks. The seven selected Vulkan modes passed 968 checks, including both radio frame-slot configurations. The Windows managed .NET 11.0.0 RadioGroup GPU run passed 264 checks.

The Linux text backend uses the larger ascent of the configured font pair for plain vector text. The Windows vector path reported the ascent of the face used by each tested standalone Latin, digit and punctuation line. At 32 px this accounts for a 0.296875 px baseline difference when Segoe UI is used and Malgun is configured but unused. To isolate the effect, the same Linux executable replayed the saved native meshes twice. The diagnostic input moved only the affected glyphs vertically and set their baseline to the Windows value. The native control reproduced all 52 prior Linux frames exactly. Of 26 distinct frame comparisons, six improved and 20 stayed identical to the native control in difference count. Across those six frames the differing-pixel count went from 259 to one. Glyph outlines, horizontal advances, and product typography policies are unchanged. Evidence is in `artifacts/linux-fixture-host-separation-20261123`.

### Single-line bidirectional End navigation

In an actual `Fluent.Input` frame, DirectWrite and Pango could report different X positions for the same logical line-end offset. In `한글 אב 123` at the default Fluent input size, Windows placed the caret at X=69 and Linux at X=89 relative to the input rectangle. Pressing Left next moved to source offset 8 on Windows and offset 4 on Linux. The common single-line input handler now records the trailing edge of the last logical text element for End, Ctrl+End and Shift+End. Engine key repeat uses the same handler. DirectWrite reports a requested leading or trailing side, and Pango represents those sides as the edges of a grapheme rectangle. [DirectWrite hit testing](https://learn.microsoft.com/en-us/windows/win32/api/dwrite/nf-dwrite-idwritetextlayout-hittesttextposition) · [Pango index to position](https://docs.gtk.org/Pango/method.Layout.index_to_pos.html)

With the same Segoe UI and Malgun files, the two platforms matched all 30 tested combinations of text and input after the change, up from 22. The comparison used the caret rectangle passed to the IME handler and the source offsets published for accessibility. Linux changed in eight cases, including two bidirectional suffix forms. All Windows observations matched the baseline. The Windows paragraph geometry suite passed 233 checks, including held End and independent DirectWrite trailing-edge checks. The replay sent key events through the control without a physical keyboard or a presented window. It checked caret geometry and selection, with no frame-time or rendering performance claim. The trace and comparator are in `artifacts/bidi-input-end-20261123`.

## Ten-minute ERD run on the installed Dozen driver

Commit `0173f62c` ran as Linux x64 NativeAOT (SDK 11.0.100, runtime 11.0.0) on WSLg, Mesa Dozen 26.3.1 and RTX 5070 Ti. The ERD sample toured 1,200 tables and 2,400 links at 1440 × 940, 4× MSAA and V-Sync off. The first presentation was logged at 2.36 s. Three captures from the live X11 window show the close and overview views while the table and its links move. After 30 s of warmup, the run collected 601 process samples over 600.08 s, then exited normally.

| Process memory | First 30 sample median | Last 30 sample median | Change |
| --- | ---: | ---: | ---: |
| Resident set | 1,164.45 MiB | 1,214.49 MiB | +50.05 MiB |
| Anonymous resident set | 1,019.41 MiB | 1,069.46 MiB | +50.05 MiB |
| Private resident set | 1,110.22 MiB | 1,160.27 MiB | +50.05 MiB |
| GC commit | 1,403.70 MiB | 1,403.95 MiB | +0.25 MiB |

The sampled active static geometry and compiled layer cache held at 718.91 MiB and 165.18 MiB. The text image pool peaked at 10.13 MiB. These Vulkan counters describe tracked allocations. Private resident set and GC commit measure different aspects of process memory. Of 724 sampled application CPU frames, p95 was 23.19 ms. Of 691 recorded Vulkan profiles, delayed GPU timing p95 was 7.14 ms. At the 24 samples with no static lists culled, each recording issued 3,640 draw calls and the median recording time was 1.14 ms. These sparse samples do not measure display cadence.

The first 300 s show 23.98 MiB of RSS growth on the same Dozen binary as the three previous stock-driver runs, which grew 20.69–21.74 MiB. The earlier fixed-driver runs grew 0.02–0.14 MiB. The ERD executables and the allocator mapping labels differ across sessions, so the older runs provide context for the current observation. This turn uses one run with the installed driver. It does not establish a general memory bound or isolate the owner of every anonymous mapping. The separate earlier allocation trace identifies the enhanced barrier allocation leak corrected by Mesa upstream. The current Mesa release list still showed 26.3.1. [Mesa release notes](https://docs.mesa3d.org/relnotes.html)

The replay, source and runtime hashes, process samples, raw mappings, Vulkan profiles and window captures are under `artifacts/dozen-erd-endurance-600s-20261123`. The workload and product source are unchanged.

### Windows ERD small-upload staging memory

The Windows ERD 1,200-table motion tour ran for 180 seconds per condition after a 15-second warmup. The reverse-order replay used the original managed executable, companion binaries and observer by hash. It exchanged only the Vulkan DLL, starting with the control and then enabling the small-texture staging reservation from `85863cfb`. The earlier 120-second replay used the opposite order. Both ran at 1440 × 940 on the RTX 5070 Ti. The new processes each supplied 181 steady samples and exited normally.

Recomputing all runs with the first and last 30 samples, private bytes increased by 37.59 MiB for the control and 37.84 MiB for the reservation build in the 180-second replay. Estimated GC commitment rose by 75.22 and 75.65 MiB, respectively. The reservation build's final private median was 4.44 MiB above the control, versus 33.51 MiB in the earlier 120-second replay. The final working-set difference went from 26.35 MiB above the control to 3.35 MiB below it. Each duration has one process per condition, so duration and run order cannot be separated. The observations do not establish a memory-growth effect from the staging change.

The app CPU p95 samples were 1.693 and 1.635 ms in the new pair. The sampled whole-frame GPU p95 values were 0.429 and 0.430 ms. These independently sampled processor durations do not measure display cadence. The comparison and process receipts are in `artifacts/erd-windows-staging-memory-20261123`.

### ERD status text attribution on WSLg

The 1,200-table motion tour ran at 1440 × 940, 4× MSAA and V-Sync off on WSLg Dozen 26.3.1 with an RTX 5070 Ti. One Linux x64 NativeAOT .NET 11.0.0 diagnostic executable ran three times per condition. The control kept the normal status text refresh, while the other condition kept the initially drawn text at its normal position and stopped only its updates. The tour and table animation ran in both conditions. Each process displayed an X11 window and exited successfully.

All runs built 1,323 vector text meshes in the first composition and none in the sampled steady window. First-composition vector mesh work had a 62.78 ms median. That measurement includes 41.20 ms of shaping and 18.42 ms of outline work, reported as separate medians. After warmup, normal status refresh produced a median of 23.91 bitmap layout calls, 20.01 raster calls and 177,895 requested text texture bytes per second. With the status refresh frozen, all three counters stayed at zero in every run. Normal refresh used a median of 1.60 ms of bitmap layout time and 0.38 ms of bitmap raster time per second.

Per-run app CPU p95 ranges overlapped at 18.26–18.88 ms with normal refresh and 18.19–23.88 ms with frozen status. Whole-frame GPU timestamp p95 ranges also overlapped, at 4.72–6.01 ms and 4.55–7.32 ms respectively. The comparison identifies the source of the recurring bitmap work. It does not establish a frame-speed change or a memory bound. Texture bytes are application upload requests, and the Vulkan timestamps include the complete render pass. The steady samples span 8.61–10.68 seconds per process. Raw data, binary and driver hashes, and the verification result are in `artifacts/erd-hud-text-stages-20261123`. [Vulkan timestamp queries](https://docs.vulkan.org/samples/latest/samples/api/timestamp_queries/README.html)

### First ERD presentation with normal status refresh

Three additional WSLg runs used a byte-identical rebuild of the diagnostic executable, the same Dozen driver and fonts, and normal status refresh. Per-frame logging matched the first app composition, accepted Vulkan submission and delayed GPU timestamp by frame or submission ID. This measured the first frame inside the app, after process and window initialization.

| First-frame stage | Median |
| --- | ---: |
| App CPU total | 1,519.98 ms |
| UI composition | 619.24 ms |
| Vector text mesh preparation within composition | 65.35 ms |
| Bitmap text rasterization within composition | 1.10 ms |
| Renderer CPU total | 884.29 ms |
| Renderer upload processing | 444.71 ms |
| Renderer command recording | 336.91 ms |
| GPU render interval for submission 1 | 7.28 ms |

All three first frames recorded 758,308,276 staging bytes through 200 upload submissions, 3,608 buffer copies and 27 texture regions. Direct-text texture requests totalled 278,236 bytes, and active static geometry occupied 753,835,732 bytes. The separate GPU render interval excludes host upload processing and does not measure display cadence. Renderer upload processing and command recording took longer than text preparation in this workload. The replay data is in `artifacts/erd-wslg-first-frame-20261123`.

### Coalescing static vertex and index copies

A diagnostic renderer staged the index and vertex ranges together when they shared a destination buffer and both fitted in the existing staging allocation. Expanded primitives kept their original upload path. Three alternating processes per condition used the same Linux x64 NativeAOT binary, normal status updates, the 1,200-table motion tour and the same WSLg Dozen RTX 5070 Ti setup.

The first frame recorded 3,608 versus 2,408 buffer copies in every run, with 758,308,276 bytes on both paths. The larger contiguous ranges raised upload submissions from 200 to 203. The median app CPU time was 1,462.65 versus 1,404.30 ms, upload processing 480.64 versus 447.41 ms, and full-frame GPU render time 7.47 versus 7.11 ms. All three per-run ranges overlapped. Command-recording ranges also overlapped.

The fixed replay matched 219 scene states. Of six Vulkan readback pairs, three were pixel-identical and the other three differed in 17 to 50 pixels by at most one channel level. Repeating the control produced up to 55 differing pixels and the same maximum channel delta. The measurements did not justify adopting the diagnostic path. Live frames, fixed poses, raw captures and hashes are in `artifacts/erd-static-copy-coalescing-20261123`.

### Static geometry payload on the first ERD frame

Three WSLg runs with one Linux x64 NativeAOT diagnostic binary recorded the exact byte range supplied to each first-frame static geometry copy. The normal 1,200-table view, status refresh and RTX 5070 Ti Dozen setup were used. The static ranges accounted for 753,835,732 of 758,308,276 staged bytes in every run.

| Static payload | Copies | Bytes |
| --- | ---: | ---: |
| Vertices | 1,200 | 580,467,600 |
| Indices | 1,200 | 172,826,276 |
| Primitive instances | 1,208 | 541,856 |

Within each payload class, every copy had a distinct SHA-256 fingerprint. The fingerprint histograms matched across the three processes. No index range encoded a plain zero-based sequential stream. All 1,200 vertex ranges had one constant UV pair per range. Keeping one pair per range with 12 bytes per vertex would avoid up to 232,187,040 bytes (221.43 MiB) of per-vertex UV data before metadata and alignment. The Vulkan draw contract supports the existing index stream independently of how Duxel's shader reads the vertices. [Vulkan indexed drawing](https://docs.vulkan.org/refpages/latest/refpages/source/vkCmdDrawIndexed.html)

This is a candidate size estimate from observed input. The diagnostic hashes the staging ranges and does not measure performance. A packed representation has yet to be qualified for pixels, upload time, rendering time and shader or binding overhead. Raw range records and the independent comparison are in `artifacts/erd-static-payload-20261123`.

### Paired run with the isolated Dozen correction

Rebuilding `0173f62c` reproduced the stock run's NativeAOT executable SHA-256 exactly. The same ERD executable then ran with a separately built Mesa Dozen 26.3.1 ICD containing the enhanced-barrier allocation-base correction. The patch bytes match the earlier isolated driver's patch. The installed ICD and library hashes were rechecked, and the live process mapped the isolated ICD's library. The unmodified driver rebuilt for this experiment has a different `.text` hash from the installed library. This pair compares the installed driver with the corrected build. The earlier allocation tracing and three 300-second pairs provide separate attribution evidence.

| Metric | Installed Dozen | Isolated corrected Dozen |
| --- | ---: | ---: |
| RSS change between first and last 30 sample medians | +50.05 MiB | +0.08 MiB |
| Private resident set change | +50.05 MiB | +0.01 MiB |
| GC commit change | +0.25 MiB | +0.13 MiB |
| App CPU sampled p95 | 23.19 ms | 23.54 ms |
| Delayed GPU sampled p95 | 7.14 ms | 7.17 ms |

Both 600-second runs ended normally with 601 samples each. The renderer tracked the same 718.91 MiB static geometry and 165.18 MiB compiled layer cache in both runs. Each app capture set includes close and zoomed-out views. The wall-clock motion tour yields different frame counts across runs, so the pair establishes the observed RSS difference under this workload. FPS, all-workload memory bounds and OS-level GPU residency remain separate. Raw comparisons and captures are in `artifacts/dozen-erd-corrected-600s-20261123`.

### Bidirectional line endpoints

Reviewing the 72 saved mixed-script layout cases showed that the four remaining cross-platform caret differences all occur at the final source offset of one bidirectional string at four sizes. Every interior caret matches within 0.001 px. At 32 px, DirectWrite reports X = 127.0 at the trailing edge of the final digit while Pango reports the full row advance of 174.390625. The line advance matches. This comparison narrows the compatibility question to endpoint affinity and does not exercise an editor's End key. [DirectWrite hit testing](https://learn.microsoft.com/en-us/windows/win32/api/dwrite/nf-dwrite-idwritetextlayout-hittesttextposition) · [Pango grapheme positions](https://docs.gtk.org/Pango/method.Layout.index_to_pos.html)

### MessageBar width slider step markers

The 300–800 px slider could emit 499 decorative rectangles at each traversal. The Gallery now disables step markers for this slider and keeps 1 px increments and live preview. Windows Gallery Release on managed .NET 11.0.0 replayed 136 desktop pointer moves per process at approximately 2.06 ms spacing, with three processes per version in alternating pairs. Only the Gallery DLL was exchanged. The replay used a fixed window, a common QPC logging build, and a minimum 850 ms after opening the page before starting the drag.

| Median of three runs | Markers on | Markers off |
| --- | ---: | ---: |
| App CPU median | 7.93 ms | 7.79 ms |
| App CPU p95 | 9.52 ms | 9.08 ms |
| App CPU p99 | 11.03 ms | 10.62 ms |
| App-side submission interval median | 8.07 ms | 7.95 ms |
| App-side submission interval p95 | 11.02 ms | 10.53 ms |
| App-side submission interval p99 | 15.86 ms | 10.90 ms |
| Submission interval mean FPS | 131.02 | 135.70 |
| Submission interval 1% low FPS | 63.05 | 91.75 |

The observed median submission p99 fell 31.3%, with overlapping per-run ranges and too few burst frames per run for a robust tail speedup estimate. The reported severe stutter did not reproduce in the control. In event-driven mode the width reached 775 px, returned to 385 px, and the example code showed 385 px after release. Gallery verified 108 presentation scenarios. The local command was `replay.ps1 -Runtime <gallery-runtime> -Output <run> -DesktopSlider` with the fixture and raw results in `artifacts/messagebar-width-ticks-20261123`. This comparison does not measure monitor scanout or GPU execution time.

### MessageBar preview during repeated fast drags

Native Windows sampling found repeated DirectWrite paragraph layout and accessibility geometry notifications during width changes. The thumb still tracks the latest pointer position. The Gallery applies the latest width to the messages and width readout at most 60 times per second while the pointer is held. A pending width schedules one frame at its deadline. Release applies the exact final width on the next frame and refreshes the example source. Keyboard and accessibility adjustments apply without the drag interval.

Paired Windows .NET SDK 11.0.100 and managed runtime 11.0.0 measurements used the same App and Core DLLs, with only the Gallery DLL swapped. For each mode, three independent foreground runs per variant replayed 680 pointer moves at about 2.1 ms spacing after at least 850 ms on the page. The replay covered repeated full-range sweeps, returning to a narrow width and releasing. V-Sync was disabled. The table shows medians of per-run statistics.

| Metric | Event-driven before → after | Continuous before → after |
| --- | ---: | ---: |
| UI composition median | 2.58 → 1.20 ms | 2.52 → 1.09 ms |
| App CPU median | 7.78 → 3.02 ms | 7.66 → 2.74 ms |
| App CPU p95 | 9.14 → 7.37 ms | 8.75 → 7.41 ms |
| App CPU p99 | 10.89 → 8.85 ms | 10.69 → 8.19 ms |
| App submission interval median | 7.97 → 3.20 ms | 7.78 → 2.89 ms |
| App submission interval p95 | 9.38 → 7.58 ms (−19.2%) | 8.95 → 7.59 ms (−15.2%) |
| App submission interval p99 | 11.82 → 9.88 ms | 11.78 → 8.47 ms |
| App submission mean FPS | 125.57 → 260.32 | 130.21 → 278.78 |
| App submission 1% low FPS | 79.53 → 77.97 | 72.44 → 96.95 |

All 12 replays reached 775 px during the drag and 385 px after release, with the final code showing 385 px. The app submitted more light frames. Accumulated UI composition time per second rose from 334 to 401 ms in event-driven mode and from 331 to 394 ms in continuous mode. The abrupt drop reported by the user did not recur in the control. The measurements cover app-side presentation acknowledgments and leave monitor scanout and GPU time unmeasured. Run `pwsh -File artifacts/messagebar-fast-width-20261123/replay.ps1 -Runtime <profiled-gallery> -Output <run> -DesktopSlider`, adding `-EventDriven` for that mode. The fixture, app timestamps, input and binary hashes are under `artifacts/messagebar-fast-width-20261123`.

The Windows Release solution built with zero warnings and errors. The 40 focused page assertions covered the preview deadline, release before the deadline, and releasing at the original width. Windows managed Gallery and WSLg Dozen Linux x64 NativeAOT Gallery on the RTX 5070 Ti each acknowledged all 108 presentation scenarios. The Linux run verifies Gallery behavior and startup, with drag timing measured on Windows.

### Constant UV in resident ERD vertex streams

The ERD upload trace found identical UV pairs within each of 1,200 static vertex streams. An isolated Vulkan prototype stores a pair once per stream and packs each vertex into 12 bytes of position and color. It selects the layout through an unused address bit and removes that bit before the shader dereferences the aligned address. Streams with varying UVs and expanded primitive triangles retain the 20-byte layout. All six temporary vertex entry points compiled and passed Vulkan 1.2 SPIR-V validation. [Vulkan buffer device address alignment](https://docs.vulkan.org/guide/latest/buffer_device_address_alignment.html)

The same Linux x64 NativeAOT .NET 11.0.0 executable showed the 1,200-table window on WSLg Dozen with an RTX 5070 Ti Laptop GPU, 1440×940 and 4× MSAA. Three processes per format ran in alternating pairs using the same device, fonts and motion tour. Both formats used the experimental shader. The Linux SDK was 11.0.100.

| First frame, median of three processes | 20-byte vertices | Shared UV | Observed reduction |
| --- | ---: | ---: | ---: |
| Staging bytes | 758,308,276 | 526,130,836 | 30.62% |
| Upload submissions | 200 | 132 | 34.00% |
| Buffer copies | 3,608 | 3,608 | 0% |
| Host upload call | 432.75 ms | 410.48 ms | 5.15% |
| Renderer CPU | 886.38 ms | 885.55 ms | 0.09% |
| App CPU | 1,522.25 ms | 1,398.08 ms | 8.16% |
| GPU draw | 7.65 ms | 7.12 ms | 7.03% |

The host timing ranges overlap. The whole GPU measurement isolates the draw submission, while the upload timings measure host calls. For moving 8-second runs after frame 96, the joined whole-frame GPU p95 median was 0.67→0.75 ms with overlapping ranges. A separate 219-step replay compared identical camera and table states for both formats and a control repeat. The fixed-fit GPU median was 7.73 ms in the control, 7.44 ms with shared UV, and 8.09 ms in the control repeat. Three of six screen captures matched exactly. The rest differed by at most one channel level on 5–55 pixels, and the control repeat showed up to 57 such pixels. Three visible captures of a texture layer switching between varying UV, shared UV and varying UV again matched exactly across the cropped 64×64 region. The middle update used 24 fewer staging bytes for its four vertices.

The format stays in the prototype pending a direct comparison with the unmodified shader, Windows RGB subpixel output, and long-run memory. The live command is `wsl bash /mnt/w/MyWorks/Duxel/artifacts/erd-constant-uv-20261123/run.sh` from a fresh isolated build. Logs, input hashes, fixed-pose captures and validation results are in `artifacts/erd-constant-uv-20261123`.

### Original shader and shared-UV controls

One Linux x64 NativeAOT executable loaded the original vertex shader, the experimental shader with normal vertices, or the experimental shader with shared UV. All loaded SPIR-V files were matched to their expected SHA-256 hashes. Three processes per condition replayed the ERD 1,200-table motion tour on WSLg Dozen and the RTX 5070 Ti at 1440×940 with 4× MSAA, rotating the order.

| Median of three processes | Original | Format branch, normal vertices | Format branch, shared UV |
| --- | ---: | ---: | ---: |
| First staging bytes | 758,308,276 | 758,308,276 | 526,130,836 |
| First upload submissions | 200 | 200 | 132 |
| First app CPU | 1,428.07 ms | 1,451.35 ms | 1,360.31 ms |
| First renderer CPU | 905.21 ms | 922.99 ms | 822.66 ms |
| First host upload call | 463.00 ms | 465.37 ms | 370.44 ms |
| First GPU draw | 7.64 ms | 7.55 ms | 7.05 ms |
| Motion app CPU p95 after frame 96 | 11.44 ms | 11.44 ms | 11.98 ms |
| Motion GPU p95 after frame 96 | 0.75 ms | 0.68 ms | 0.79 ms |

Relative to the original, the shared-UV form reduced first renderer CPU median by 9.1% and increased motion GPU p95 median by 5.9%. The three-process ranges did not overlap for those two comparisons. Motion CPU ranges overlapped, and the elapsed-time tour can submit different poses at different rates. GPU timestamps measure draw submissions. The upload measurements are host call times.

On Windows, the managed .NET 11 fixture exercised stock, normal and shared-UV vertices on the NVIDIA Vulkan driver with dual-source blending enabled. Full 640×420 readbacks matched the stock shader at 1× and 4× MSAA in both candidate modes. The two candidate modes preserved all 4,401 chromatic text pixels in the mixed-script text crop. Forcing dual-source blending off reduced that count to zero. The constant-UV colored quad used 24 fewer logical static geometry bytes, and the varying-UV quad and neutral circle matched. The captures and measurement inputs are in `artifacts/erd-uv-shader-control-20261123`.

The same instrumented Linux executable ran the original format and the shared-UV format for one 180-second sampling interval each. Both processes exited normally after 181 one-second samples. Median process RSS grew by 21.13 MiB and 20.63 MiB between the first and last 30 samples, and the final medians were 1,175.76 MiB in both arms. Logical active static geometry stayed at 718.91 MiB and 497.49 MiB respectively. This accounting and process RSS do not measure operating-system GPU residency. The two runs establish the result for this device and order.

A second prototype shares the attribute fetches and uses SPIR-V `OpSelect` for the UV word index. All six vertex entry points passed Vulkan 1.2 validation. Three real runs per mode presented the same 219-step replay for the original shader, the packed shader with a branch, and the packed shader using selection. At the fixed full view, median per-run GPU p95 was 8.15, 8.13 and 7.81 ms. Across the same 93 zoom poses it was 7.50, 7.13 and 6.95 ms. The original and the selection variant ranges did not overlap in either phase. Three of each run's six readbacks exercised a visible texture control and all texture crops matched exactly. Of 18 full-frame comparisons against the original reference process per mode, 12 original repeats, nine packed branch captures and eight selector captures matched exactly. The maximum channel delta was one in each mode. The two additional Windows selector captures matched the original at 1× and 4× MSAA, including the 4,401 ClearType text pixels.

With the same live executable and nine new independent processes, the selection variant with shared UV reduced the first renderer CPU median from 899.49 to 831.97 ms (7.5%) and the host upload call from 442.52 to 382.06 ms (13.7%). Those ranges did not overlap. The first app CPU range overlapped and the transfer reduction stayed at 30.62%. After frame 96, the live motion GPU p95 median was 0.67, 0.71 and 0.78 ms for original, normal-selection and packed-selection conditions, with overlapping ranges. The fixed and live workloads yielded different tail directions. The format remains experimental pending matched real interaction replay and a check of dynamic CodeView and Gallery rendering on the uncompressed path.

### CodeView and ERD window input replay

The Windows managed Gallery used the same five library binaries across six runs. Each run opened SVG Arcs and Curves and sent the same 444 wheel messages to the same viewport in three passes. UI Automation confirmed scrolling from 0% to 100%, back to 0%, and to 100% again. The loaded vertex shader hashes matched the intended shader directory for each run. Compression was disabled for the selector variant to isolate its effect on the ordinary draw path. Each shader condition passed 108 Gallery presentation scenarios.

| Median of three runs, p95 | Original | Selector, normal vertices |
| --- | ---: | ---: |
| First scroll app CPU | 2.691 ms | 2.703 ms |
| First scroll GPU draw | 0.06166 ms | 0.06115 ms |
| Repeat scroll app CPU | 1.545 ms | 1.561 ms |
| Repeat scroll GPU draw | 0.06083 ms | 0.06058 ms |

The per-run ranges overlap for all four comparisons. The frame timings come from accepted presentations and delayed Vulkan timestamp queries joined by submission ID. The replay uses a fixed measured viewport and leaves general resize performance outside the test. The same GPU and unchanged library hashes isolate the vertex shader choice. The native window replay site and logs are in `artifacts/erd-uv-input-replay-20261123`.

On WSLg Dozen RTX 5070 Ti, three runs per condition used one Linux x64 NativeAOT .NET 11 executable at 1440×940, 4× MSAA. The harness sent X11 pointer messages to the ERD window with automatic tour and motion disabled. Each run acknowledged 60 moves of the same table and 72 zoom-wheel steps, with the same client coordinates, table positions and zoom targets. Each acknowledged input was joined to its own UI frame, presentation and delayed GPU timestamp. Zoom interpolation ran normally between events.

| Median of three runs, p95 | Original | Selector, shared UV |
| --- | ---: | ---: |
| Drag app CPU | 28.53 ms | 29.40 ms |
| Drag GPU draw | 8.22 ms | 8.16 ms |
| Zoom app CPU | 24.16 ms | 25.21 ms |
| Zoom GPU draw | 6.98 ms | 7.05 ms |

App CPU p95 increased 3.1% on drag and 4.3% on zoom, with disjoint per-run ranges. GPU p95 ranges overlap in both phases. The app profile's interval between UI render completion and renderer entry increased from 13.67 to 14.21 ms on drag and 11.53 to 11.99 ms on zoom. That interval includes draw-data extraction and render-queue handoff, so the measurements do not localize the delay to either operation. The Vulkan upload-call p95 changed from 1.28 to 1.31 ms on drag and from 0.88 to 0.88 ms on zoom. The product retains its original shader and static vertex layout.

### Render queue timing during matched ERD input

The app profile's existing `getDraw` field runs from the end of UI rendering to entry into the Vulkan renderer. An isolated diagnostic build measured the actual `GetDrawData()` call, producer-side texture payload capture and command inspection, enqueue, waiting for the consumer, and renderer entry separately. Time blocked before taking the next input snapshot is measured separately from this interval. The components were checked against the existing field for all input frames.

The same Linux x64 NativeAOT executable replayed 60 drag moves and 72 wheel steps per run at 1440×940 with WSLg Dozen and the RTX 5070 Ti. The measured source frames, GPU submissions, table positions and zoom targets were joined and checked. Three rotated runs per condition used the stock shader with the queue, the UV selector with the queue, or the stock shader on a single thread.

| Stock shader and Windows-mounted logs, median of three per-run p95 values | Drag | Zoom |
| --- | ---: | ---: |
| `GetDrawData()` call | <0.001 ms | <0.001 ms |
| Queue preparation | 0.035 ms | 0.032 ms |
| Wait between enqueue and consumer dequeue | 14.18 ms | 12.84 ms |
| Capacity wait before input snapshot | 14.28 ms | 13.64 ms |
| App profile elapsed frame time with queue | 29.85 ms | 27.09 ms |
| App profile elapsed frame time on one thread | 15.58 ms | 14.63 ms |

The one-thread arm removed queue waiting. These frame intervals include elapsed waiting with the queue enabled, and they do not measure the rate of completed presentations or input-to-display latency. The figures alone do not determine a preferred scheduling policy.

The two queued conditions were also run three times each with both log files written to the WSL Linux filesystem. With the stock shader, queue-wait p95 moved from 14.18 to 11.36 ms on drag and from 12.84 to 9.73 ms on zoom. Profiled elapsed frame p95 moved from 29.85 to 24.30 ms and from 27.09 to 22.24 ms. The phases were grouped by filesystem. This establishes sensitivity to the profiling environment and leaves run-order effects open. Microsoft recommends keeping files on the WSL filesystem for Linux I/O. [WSL filesystem guidance](https://learn.microsoft.com/en-us/windows/wsl/filesystems)

The earlier UV selector CPU regression changed direction under the split timestamps. With native logs, its elapsed frame p95 was 23.95 ms on drag and 21.56 ms on zoom, with ranges overlapping the stock shader in both phases. The selector remains experimental. A timing method with deferred log writes and paired input-to-accepted-presentation timestamps is needed before choosing the queue policy or revisiting the rendering change. All 15 exits, 1,980 matched input frames, and the stage reconstruction checks are recorded in `artifacts/erd-render-queue-stages-20261123/verification.json`.

### ERD input latency and submission throughput with deferred recording

One isolated Linux x64 NativeAOT executable recorded accepted presentation IDs and monotonic timestamps to a preallocated array, then saved them at shutdown. Per-frame app and Vulkan file profiling were disabled. The X11 replay recorded each issue time and matched it to the submitted frame that consumed it. The input acknowledgement used a pipe, and the replay saved the pipe contents after exit. The clock check linked the two processes' monotonic timestamps. Three rotated runs per condition exercised 60 drag moves and 72 zoom steps with matched table positions and zoom targets. The experiment used continuous frame scheduling at 1440×940 and 4× MSAA on WSLg Dozen, with RTX 5070 Ti and Intel adapters enumerated.

| Median of three runs | Stock queue | UV selector with queue | Stock shader, single thread |
| --- | ---: | ---: | ---: |
| Drag input to accepted presentation p95 | 15.16 ms | 14.82 ms | 14.53 ms |
| Zoom input to accepted presentation p95 | 24.10 ms | 24.82 ms | 17.83 ms |
| Accepted presentations/s in the 1.25 s full-view window | 71.01 | 73.06 | 64.79 |
| Accepted presentations/s during the zoom sequence | 193.71 | 194.21 | 157.09 |

The single-thread arm reduced zoom p95 by 26.0% and reduced full-view submission throughput by 8.8% versus the stock queue. The run ranges did not overlap for these measures. Drag latency and the selector comparison overlapped. The test retained the shipping queue and geometry format because the single-thread result trades latency for throughput and the selector still lacks a consistent interaction gain. Zoom input was paced by app consumption, so only the fixed full-view window measured a common elapsed duration. The endpoint is the app's acknowledgement after the Vulkan presentation call. Monitor scanout is outside the measurement. The Vulkan contract queues the image for presentation. [Vulkan presentation call](https://docs.vulkan.org/refpages/latest/refpages/source/vkQueuePresentKHR.html)

All 1,188 matched input frames and nine normal exits are recorded in `artifacts/erd-input-present-latency-20261123/verification.json`.

### Event-driven input admission

The same deferred-recording method compared the stock queue, a candidate that waits for the render queue to become idle before draining fresh input, and a single-thread control. All three used the original shaders and the event-driven platform loop on WSLg Dozen. Three rotated runs per condition presented all 1,188 targeted drag and wheel inputs with identical table positions and zoom targets. The candidate checked pending input under the input lock and released that lock before waiting. It kept the existing queued work and never discarded a frame.

| Median of three runs | Stock queue | Wait for idle before input | Single thread |
| --- | ---: | ---: | ---: |
| Drag input to accepted presentation p95 | 15.27 ms | 16.15 ms | 14.23 ms |
| Zoom input to accepted presentation p95 | 25.30 ms | 29.12 ms | 17.69 ms |
| Full-view accepted presentations/s | 71.19 | 70.70 | 64.40 |
| Accepted presentations/s during zoom replay | 195.99 | 177.03 | 154.94 |

Waiting for idle increased zoom input latency p95 by 15.1% and reduced zoom replay throughput by 9.7%. Both run ranges were disjoint. The app's frame-start-to-accept p95 nevertheless fell from 20.85 to 13.19 ms because the candidate waited before the frame timing started. The stock render policy is retained. The zoom replay waited for the app to consume each input, so the throughput describes this replay. Full-view throughput uses the same 1.25-second window in every run. Raw timestamps and comparison logic are in `artifacts/erd-event-admission-20261123`.

### Reuse CodeView line-number strings

CodeView now reuses invariant decimal labels for line numbers at and above 300 using a lazily allocated cache of 128 entries per widget. The gutter width label uses the same path. Number formatting below 300 follows the conventional .NET cache. The widget releases its label cache on disposal. [Runtime number formatting](https://raw.githubusercontent.com/dotnet/runtime/main/src/libraries/System.Private.CoreLib/src/System/Number.Formatting.cs)

Six alternating Windows managed .NET 11.0.0 processes rendered a 1,600-line document with mixed scripts, numbers, punctuation and wrapped rows. The same executable replayed 320 deep-scroll frames per process, after 96 warm-up frames at the deep viewport. The scroll positions matched. All 24 captured outputs matched the reference at their respective four positions. The release-form implementation was also rebuilt and verified against the same 320 scroll positions and four reference readbacks.

| Median of three runs | Existing formatting | Bounded label cache | Change |
| --- | ---: | ---: | ---: |
| Mean managed allocation per composition | 2,039.25 B | 1,239.55 B | −39.2% |
| Line number paint allocation p95 | 736 B | 0 B | −100% |
| UI composition p95 | 0.3879 ms | 0.3931 ms | +1.3% |

This change reduces repeated allocations on deep CodeView scrolling. The fixture showed no frame-time gain. The Windows managed Gallery and Linux x64 NativeAOT Gallery on WSLg Dozen each passed all 108 presentation scenarios. Process memory over a sustained session and the reported SVG slow frame remain separate measurements. The local replay and raw CSVs are in `artifacts/codeview-line-labels-20261123`.

### First visible Windows CodeView frame in Gallery

Tested the clean `db0ba84e` snapshot with diagnostic timing enabled on managed .NET 11.0.0 and an RTX 5070 Ti Laptop GPU. Each of three independent processes opened SVG from the Gallery, selected Arcs and curves, set the existing 141% zoom fixture, and replayed 444 wheel messages. The cold table uses the first frame in each navigation interval that requested CodeView coverage and had both a matching app submission acknowledgment and a Vulkan timestamp. Those navigation frames used a 1984 × 956 framebuffer. The wheel phases below used 1483 × 720 at a fixed size.

| Median of three processes | Open SVG | Open Arcs and curves |
| --- | ---: | ---: |
| App CPU frame | 163.36 ms | 57.92 ms |
| UI composition | 158.24 ms | 55.54 ms |
| CodeView paragraph preparation | 12.44 ms | 17.44 ms |
| Windows paragraph backend calls within preparation | 7.63 ms | 11.81 ms |
| Unbounded / finite-width backend layouts | 53 / 0 | 76 / 6 |
| Paragraph rasterization across the frame | 1.12 ms | 0.50 ms |
| Renderer CPU for all texture uploads | 3.72 ms | 0.90 ms |
| GPU draw timestamp interval | 0.181 ms | 0.086 ms |

The SVG image-definition operation took 37.08 ms at the median. The UI composition column includes the full page, beyond the CodeView paragraph timing. The Windows backend timing includes its managed preparation and DirectWrite work. It does not separate native layout, caret and range queries, or the managed assembly of run data.

Across the first, return and repeat passes, 1,161 changed frames matched their Vulkan timestamps. The first pass requested 2,685,088 bytes of CodeView coverage in each run, and the return and repeat passes requested zero. The first and repeat app CPU p95 medians were 4.61 and 1.75 ms. The corresponding GPU p95 medians were 0.066 and 0.063 ms. The first and repeat paths reached the same scroll offsets. These are diagnostic frame measurements on one Windows machine. They do not measure monitor scanout, long-run memory, or a before/after product speedup.

DirectWrite keeps positioned glyphs in text layouts. The cold count here points to new layouts, most of them unbounded, and makes the cost of the Windows paragraph backend the next measurement target. The renderer timestamps measure execution separately from CPU-side texture preparation. [DirectWrite layout reuse](https://learn.microsoft.com/en-us/windows/win32/direct2d/direct2d-and-directwrite) · [Vulkan timestamp queries](https://docs.vulkan.org/samples/latest/samples/api/timestamp_queries/README.html)

Build the diagnostic snapshot with `DuxelValidationHooks=true` and run `artifacts/codeview-live-stages-20261122/replay.ps1 -Runtime <diagnostic-gallery> -Output <run> -Mode Continuous`. The diagnostic patch, raw timing files and analysis are in `artifacts/codeview-first-presentation-20261123`. No product runtime code changed.

### Skip redundant DirectWrite font settings

The initial DirectWrite text format supplies the first span's face and size to the whole layout. Apply explicit font setters only to ranges with a different face or size. Drawing effects still mark every original paint range. The source text, layout width, rounding, caret and range queries, and glyph capture use the same code paths. [CreateTextLayout](https://learn.microsoft.com/en-us/windows/win32/api/dwrite/nf-dwrite-idwritefactory-createtextlayout) · [SetDrawingEffect](https://learn.microsoft.com/en-us/windows/win32/api/dwrite/nf-dwrite-idwritetextlayout-setdrawingeffect)

On Windows managed .NET 11.0.0 with the RTX 5070 Ti Laptop GPU, six independent Gallery processes used one diagnostic executable. Three runs per variant alternated in pairs and replayed the same 444 native wheel messages after opening the SVG page and Arcs and curves tab. Cold navigation used a 1984 × 956 framebuffer and the scroll replay used a fixed 1483 × 720 framebuffer. Cold frame selection joined the first CodeView coverage request to its accepted app submission and Vulkan timestamp.

| First Arcs and curves frame, medians | Before | After |
| --- | ---: | ---: |
| Windows paragraph preparation | 2.52 ms | 2.55 ms |
| DirectWrite layout setup and glyph capture | 4.73 ms | 3.14 ms |
| Caret, range and line geometry queries | 3.21 ms | 3.17 ms |
| Managed glyph and paragraph output assembly | 1.07 ms | 1.06 ms |
| Total Windows paragraph backend | 11.72 ms | 10.18 ms |
| CodeView layout preparation | 17.38 ms | 15.76 ms |
| App CPU frame | 58.22 ms | 56.48 ms |

Layout setup and glyph capture decreased 33.7%, with all three candidate results below the baseline range. Total backend and app frame ranges overlap. First-scroll CPU p95 was 4.41 → 4.64 ms and repeat-scroll CPU p95 was 1.74 → 1.76 ms. The first-scroll Vulkan draw p95 was 0.066 ms in both variants. The replay verifies lower cost in the cold layout setup and capture stage. It leaves whole-app speedup, monitor scanout and long-run memory unverified.

The diagnostic source patch, replay results and local analysis are under `artifacts/codeview-directwrite-stages-20261123`. To reproduce the comparison, use the existing `replay.ps1` in `artifacts/codeview-live-stages-20261122` against the diagnostic build with `DuxelValidationHooks=true`. Toggle `DUXEL_PROBE_SKIP_DEFAULT_FONT_SETTINGS` between 0 and 1 in the diagnostic build only.

The clean product solution built in Release with zero warnings and errors. The baseline, diagnostic variant, and clean product passed 424 assertions each across native paragraph geometry, styled paragraph output, and CodeView line-layout caching. The diagnostic variants matched 201 CodeView scroll states and the final Vulkan capture matched byte for byte in all three builds. Windows managed Gallery and WSLg Dozen Linux x64 NativeAOT Gallery each acknowledged 108 presentation scenarios with the product change.
