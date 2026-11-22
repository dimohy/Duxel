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
