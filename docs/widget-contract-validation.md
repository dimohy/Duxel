# Widget contract validation

Current Linux x64 WSLg verification used .NET SDK 11.0.100, runtime 11.0.0 and llvmpipe. The managed and NativeAOT suites each passed 15 of 16 fixed-size or headless runs. Their 2,592 presentation hash pairs, 4,160 container and virtual-layout capture hash pairs, and 458 exported file pairs matched. Exported files overlap some of the hash groups. Both styled paragraph runs reproduce seven whole-layout Pango pixel mismatches across 24 alpha bytes while passing all 36 independent native run-mask comparisons. Four SVG replacement runs changed window extent and are outside this fixed-size comparison. [Verification](L:/Cache/Duxel/linux-vulkan-ga-contract-20261120/verification.json).

The Linux x64 .NET 11 managed and NativeAOT Vulkan font path compared the same 36 inputs with a fixed 640×128 window, one or three frames in flight, and 1× and 4× MSAA. Duxel's run textures matched independently composed masks in all 288 captures. The same seven cases and 24 pixel positions differed from whole-layout Pango in each configuration, with no extra positions. A full-size Cairo scratch surface marked with one pixel outside the ink reproduced the whole-layout results in all seven cases. This probe covers Vulkan blending of glyph runs. Existing tests cover paragraph widget layout and placement. [Pixel comparison](L:/Cache/Duxel/linux-styled-vulkan-20261121/verification.json)

Linux x64 .NET 11 managed and NativeAOT drew 36 single-color Pango paragraphs at three integer placements. A shared paragraph surface bounded by the ink plus one pixel of padding matched all 108 full-size 640×128 references. With zero padding, one pixel was lost at each placement of one paragraph. The A8 scratch surface fell from 81,920 bytes to a median of 1,820 bytes. Caller-provided zeroed buffers per glyph run differed in 11 of 36 cases for both full-size and cropped surfaces. The probe ran in an isolated copy. Multicolor runs, widget clipping and cache lifetimes need separate verification. [Surface comparison](L:/Cache/Duxel/linux-pango-surface-20261121/verification.json)

A one-pixel-padded paragraph texture used the Vulkan font path in a fixed 640×128 window. The Linux x64 managed and NativeAOT probe used one or three frame slots, 36 paragraphs at three positions, and 1× and 4× MSAA. All 2,592 pairs matched the full-size reference for white text, a tint with fractional clipping, and two translucent tints in adjoining regions. All 5,184 next-frame redraws reused their textures and matched the first frame. The tints were applied to regions of the same paragraph mask. Widget cache integration and the seven strict whole-Pango comparisons remain open. [Verification](L:/Cache/Duxel/linux-pango-clipped-vulkan-20261121/verification.json)

The `inline-paragraph-cache` mode checks layout and texture reuse through an actual `UiInlineText` widget. Linux x64 .NET 11 managed and NativeAOT ran with one or three frame slots and 1× or 4× MSAA. All 80 Vulkan captures matched freshly created widgets with the same state. Color, integer position and height-only clipping retained the layout and textures. Returning to a recent width reused its layout. Changing the runs or bidi direction rebuilt it. [Verification](L:/Cache/Duxel/linux-inline-paragraph-cache-20261121/verification.json)

The Linux backend can rasterize a whole Pango paragraph to its ink bounds plus one pixel. It shares the existing layout's font selection and direction and releases the native layout before returning. Managed and NativeAOT each matched 36 independent Pango masks and returned empty masks for eight inkless inputs. Each runtime also reproduced the previous 20 widget Vulkan captures. Widget integration still requires matching line placement, color, and cache rules. [Verification](L:/Cache/Duxel/linux-pango-native-raster-20261121/verification.json)

On Linux, `UiInlineText` renders an undecorated, opaque paragraph into one mask when its size and line placement allow the Pango layout to be painted as a unit. Text, font, width and direction identify a texture that newly created widgets can share. Translucent text, different line placement and vector text use the run path. The tested paragraph's initial uploads dropped from seven to one. The four Linux managed and NativeAOT profiles each matched 22 fresh-widget captures and passed 22 cache assertions. The run path retains seven strict Pango differences. [Verification](L:/Cache/Duxel/linux-inline-whole-mask-20261121/verification.json)

One NativeAOT executable compared twelve `UiInlineText` widgets in a fixed 640×480 Vulkan window with whole masks off and on, three runs each. All 160 frame hashes matched across settings. Steady composition moved from 61.40 to 27.50 µs and draw calls from 84 to 12. Initial glyph uploads moved from 18 to 12 textures and from 41,596 to 208,224 bytes. On changing frames, uploads moved from 1,976 to 17,352 bytes and composition from 377.80 to 413.30 µs. Steady rendering and readback times had overlapping run ranges. [Comparison](L:/Cache/Duxel/linux-inline-whole-mask-ab-20261121/verification.json)

An isolated Linux x64 NativeAOT Vulkan build compared whole-paragraph RGBA and single-channel R8 masks in three runs per format. All 160 frame hashes matched. Glyph texture uploads fell by 75% on the first frame (208,224 to 52,056 bytes) and on changing frames (17,352 to 4,338 bytes). The regression test passed all 22 checks per run with 1× and 4× MSAA and one and three frame slots, and the capture hashes matched. Product texture updates, disposal, and compatibility with other renderers remain to be verified. [Performance comparison](L:/Cache/Duxel/linux-inline-mask-r8-20261121/verification.json) [Regression checks](L:/Cache/Duxel/linux-inline-mask-r8-20261121/cache-verification.json)

The built-in Vulkan app now uses R8 for eligible Linux paragraph masks. Standalone contexts default to RGBA. On llvmpipe, the managed and NativeAOT runs each passed 27 checks at both one and three frame slots. Each run matched 22 reference frames with 1× and 4× MSAA. Lifecycle checks compare creation, two batched odd-sized region updates, destruction, and recreation with RGBA. [Product verification](L:/Cache/Duxel/linux-inline-mask-r8-product-20261121/verification.json)

The actual Linux Gallery Typography page changed its visible description in three runs per mask format. One Linux x64 NativeAOT image used a fixed 1280×900 window, MSAA4, and llvmpipe. Each run recorded 96 samples after 96 warm-up frames. All 32 changed samples initiated a new Vulkan present. Texture staging fell from 25,364 to 6,341 bytes per changed frame (75%). Changed-frame presentation intervals (6.95 to 6.82 ms), process CPU time including setup and warm-up (1.66 to 1.64 s), and peak RSS (560,980 to 561,192 KiB) had overlapping run ranges. The other 64 samples reused the previous presentation. Hardware GPU and historical Windows baseline qualification remain open. [Per-run comparison](L:/Cache/Duxel/linux-gallery-r8-ab-20261121/verification.json)

An isolated Cairo comparison corrected the marker and erase coordinates on translated crop surfaces. It started with a clear A8 surface, tracked whether earlier runs had painted ink, and preserved the original glyph list, including inkless glyphs. A surface covering each line's ink bounds with one pixel of padding matched all 36 independent whole-Pango references in both managed and NativeAOT runs. The existing product run-mask path still differed in the same seven cases at 24 alpha bytes. The next product check will exercise cache reuse and clipping with line bounds and prior paint state. [Comparison](L:/Cache/Duxel/linux-pango-run-origin-20261121/verification.json)

The Linux product now uses the line ink bounds and the prior Cairo destination state for native paragraph runs. It crops the resulting alpha mask before Vulkan upload. Cache checks cover integer translation, changed line bounds, and changed previous paint state. The managed and NativeAOT builds each passed 411 checks, including the seven previously failing strict Pango cases. All 288 Vulkan font-rendering captures matched the independent Pango reference on llvmpipe at 640×128, MSAA 1× and 4×, and one and three frame slots. Hardware GPUs and cold-raster cost remain unmeasured. [Verification](L:/Cache/Duxel/linux-pango-product-runs-20261121/verification.json)

Linux paragraph rasterization now reads the Cairo A8 surface directly and copies only the ink rectangle into managed memory. In the isolated NativeAOT comparison with three runs per mode, the 384-run paragraph took 14.41 to 4.26 ms and allocated 113.28 to 1.35 MB on the managed heap. Masks matched before and after the copy change for all four inputs. The product also passed the 411 styled checks and the retained texture checks. The repeated benchmark paragraphs still differed from whole Pango painting at 5, 24, 25, and 45 pixels. None of those pixels received overlapping run masks. [Comparison](L:/Cache/Duxel/linux-pango-run-cost-20261121/crop-verification.json) [Product verification](L:/Cache/Duxel/linux-pango-run-cost-20261121/product-verification.json)

The expanded inputs reproduced the differences at style-boundary splitting. Keeping the original Pango glyph groups matched whole-layout painting both with Duxel's scaled fonts and when compositing line-sized masks. Drawing the identical glyph IDs, order, and coordinates with the product's smaller style groups on one Cairo surface reproduced the exact 5, 24, 25, and 45 differences. Managed and NativeAOT alpha buffers matched byte for byte. Preserving native groups for equivalent paint and verifying distinct paint at style boundaries remain product work. [Intermediate mask comparison](L:/Cache/Duxel/linux-pango-run-trace-20261121/verification.json)

Linux paragraphs preserve the native Pango glyph group across spans with the same foreground rule. Distinct link, syntax and code-token paint rules keep their own runs. All four repeated inputs matched whole Pango painting, and Vulkan code-view tests verified distinct keyword and string colors. Rebasing code-view lines preserves raster context, and changing an earlier line's paint grouping preserves unchanged native layouts later in the document. [Product and performance evidence](L:/Cache/Duxel/linux-pango-style-groups-20261121/verification.json)

The current Linux x64 product passed 45 checks per run and produced 106 identical captures across managed and NativeAOT, one and three frame slots, at 320×240 on llvmpipe. For the two perspective viewports, the independent oracle found 52 and 56 differing pixels at 1× and 175 and 174 at 4×. The isolated explicit sample-mask candidate matched all four viewport combinations. The fractional alpha-128 scene differed from the product by 3,329 pixels at 1× and 6,628 at 4× with that candidate. A material-aware path still needs pixel and GPU-cost verification. [Product](L:/Cache/Duxel/linux-depth-boundary-20261121/verification.json) [Isolated candidate](L:/Cache/Duxel/linux-depth-boundary-20261121/candidate-verification.json)

An isolated Linux x64 build applied an explicit sample mask only to commands classified as opaque. At a fixed 320×240, both viewports matched the independent perspective oracle with 1× and 4× MSAA. Eighteen protected captures or regions matched the product, including translucent materials, partial texture updates and externally replaced geometry. Three NativeAOT runs per mode used 96 warm-up and 256 sampled frames. The llvmpipe Vulkan time was 4.74 to 5.80 ms (+22.23%), and median frame time was 4.98 to 6.01 ms (+20.83%). Internal fault injection forced the explicit route to be selected after rewriting a previously opaque buffer, with zero pixel changes in these four cases. The candidate remains isolated. [Verification](L:/Cache/Duxel/linux-depth-routed-20261121/verification.json)

An isolated 4× MSAA depth-mask shader added a conservative rejection when all samples are outside the primitive. All 124 captures matched across the two shader builds and one and three frame slots, with 1× and 4× MSAA on Linux x64 NativeAOT and llvmpipe at 320×240. Both shaders used Glslang 15.1.0 and passed SPIR-V validation for Vulkan 1.2. The recompiled control also matched all 124 archived captures. The median Vulkan times were 5.42 and 5.33 ms, and median frame times were 5.64 and 5.59 ms. Three-run ranges overlapped on both metrics. [Comparison](L:/Cache/Duxel/linux-depth-mask-bounds-20261121/verification.json)

The .NET 11 Linux app harness completed nine paired managed and NativeAOT profiles and compared 859 checks per runtime. They cover ordered X11 input, startup retry, text geometry and keyboard behavior, serialized results, and actual Dialog and MessageBar accessibility with an independent AT-SPI client. Both text-scroll runs initially lacked U+1F600 coverage in the supplied font. With an explicit emoji-capable font, both passed 691 checks. The managed Dialog failed its first modal-state check and then passed three reruns. NativeAOT passed all four attempts. [App verification](L:/Cache/Duxel/linux-app-ga-contract-20261120/verification.json).

The Windows `primitive-commands` suite creates a fixed 320×240 physical-pixel window and now checks that opaque full-viewport geometry covers the last framebuffer row at 1× and 4× MSAA. Default Vulkan layer viewports use the exact framebuffer dimensions. The former Intel renderer fails the new row assertion. The current renderer passes on Intel and NVIDIA. [Evidence](L:/Cache/Duxel/overlay-bottom-row-20261120/verification.json).

Analytic primitive edges use fine fragment derivatives for their coverage width. The fixed 320×240 Intel/NVIDIA comparison reduced RGB readbacks with a channel difference above one from 20/100 to 6/100. The six remaining captures are depth-order cases at 1× and 4× MSAA. The fresh product build passes 76 primitive assertions on each GPU and matches all 100 qualified candidate captures per GPU. [Product verification](L:/Cache/Duxel/gradient-coverage-20261120/product-verification.json).

The default contract manifest classifies every Graphics `*-only` mode and runs 138 CPU suites, including the Control executable. Vulkan readback and native application or input modes have separate entries. The Observer batch mode includes fractional viewport resizing and should run separately when resize validation resumes. The fixed-size Gallery visual run and Gallery source contracts cover Gallery integration.

## Stable .NET 11 runtime comparison

The Windows x64 managed and NativeAOT builds used SDK 11.0.100, runtime 11.0.0 and three frame slots. The existing `widgets-no-resize` suite passed 4,642 checks in each runtime with 4,038 byte-identical RGB readback pairs. The separate `containers` suite passed 263 checks in each runtime and matched 185 RGB readback pairs at MSAA 1 and 4. The window and resize exclusions in `widgets-no-resize` are listed in its result. The text construction allocation assertion passed in both runtimes, with the original measurements retained in the common comparison. Evidence directories: `W:/Cache/Duxel/gallery-source-20261117/common-widgets-ga-1908` and `W:/Cache/Duxel/gallery-source-20261117/containers-ga-1909`.

## Actual compiler selection and declaration removal (2026-11-15)

The generator fixture now records its API reference version, actual Roslyn assembly versions/commits, runtime mode and load context separately from the .NET runtime. Default tests continue to compile and run with the pinned 4.10 package. `-p:DuxelUseSdkRoslyn=true` executes the generator driver with the selected SDK's actual compiler assemblies. `DuxelSdkRoslynDirectory` explicitly overrides their directory; otherwise the single path comes from `MSBuildToolsPath/Roslyn/bincore`. Invalid mode and missing selected files fail before build with DUXTEST001/002/003.

SDK mode copies the selected assemblies into `SdkRoslyn` and loads the fixture in an isolated collectible AssemblyLoadContext while sharing public Duxel.Core contracts. This supports the Linux SDK's ReadyToRun compiler images without treating them as portable compile references or modifying them. API compilation stays on4.10; the compiler running the generator is explicitly selected. Missing SDK runtime files fail even when the package compiler is present. The fixture is managed and uses dynamic compilation; it is not a NativeAOT runtime test.

Final profiles each pass131 checks: Windows .NET11/package4.10, Windows .NET11/SDK Roslyn5.12, and Linux pinned SDK11.0.100-rc.3.26522.148/runtime11.0.0-rc.3.26522.148 with its own Roslyn5.12 build. Six generated source files are byte-identical to the previous117-check baseline in all three profiles. No product generator implementation, runtime widget or dependency was changed.

The removal matrix observes both same-file declarations and independent syntax trees. Removing the first/middle/last declaration reuses0/1/2 of the two remaining outputs in every profile. Functional type removal and unchanged source bytes pass, but the original first-removal2/2 cache criterion remains incomplete. Splitting declarations into separate files did not meet it. The package's exact Roslyn source commit is `2b7d172669b2f7e55803b55f317cfcc2d4279d76`; its [state-table implementation](https://github.com/dotnet/roslyn/blob/2b7d172669b2f7e55803b55f317cfcc2d4279d76/src/Compilers/Core/Portable/SourceGeneration/Nodes/NodeStateTable.cs#L309) compares corresponding positions. This is a verified limitation of the current path, not a completed optimization or a claim that all possible redesigns are impossible.

Use the existing generator test publish/run command with `-p:DuxelUseSdkRoslyn=true` to select SDK mode; omit it or use `false` for package mode. Select the desired SDK through the working directory/global.json or an explicit dotnet executable, publish into a fresh directory, then run `dotnet exec <published Duxel.Generators.Tests.dll> <fresh result directory>`. Results include `icon-removal-matrix.json`, `icon-incremental-limits.json` and `core-catalog.json`. The current compiled draft catalog exposes59 factories, with35 of55 required parents connected and20 missing; these connection counts are not complete Fluent conformance. Evidence: `artifacts/generator-compiler-selection-2026-11-15/verification.json`.

## Generator validation on Windows and Linux

The widget contract runner does not run `Duxel.Generators.Tests`. Changes to the compiler or `FluentComponents.tsv` also require that executable: it compares production requirements with an independently reviewed official-site fixture. On 2026-11-13 this caught four missing fixture variants: CardFooter, CardHeader, CardPreview and InteractionTag. Their parent guidance was reviewed again; the top-level denominator remains55.

Current Windows and Ubuntu24.04 x64 executions each pass117 checks on .NET11.0.0. All six captured generated source files are byte-identical across OSes. Result files record the framework, OS and process architecture. The existing declaration-removal cache criterion remains incomplete:0of2 remaining outputs cached. These results do not qualify Linux window/input/text/Vulkan, ARM64, Pi or fixed RC3 NativeAOT/performance.

`LocalSource.targets` keeps `net11.0-windows` by default. Portable validation explicitly sets both `DuxelSourceTargetFramework=net11.0` and `DuxelBenchmarkFramework=net11.0`; the generator still targets netstandard2.0. The Linux test is a separate self-contained managed publish with runtime11.0.0, built from the existing Windows SDK. Ubuntu's global .NET10 installation and the repository RC3 pin are unchanged.

Run the qualified Linux executable from PowerShell, using a fresh result directory:

```powershell
wsl.exe -d Ubuntu --exec /mnt/l/Cache/Duxel/linux-generator-2026-11-13/publish-final/Duxel.Generators.Tests /mnt/l/Cache/Duxel/linux-generator-2026-11-13/user-result
```

Full build arguments, source hashes and both results: `artifacts/goal-audit-2026-11-13/verification.json`.

2026-11-12 Current Avatar base qualification:49default suites/8021assertion executions,Avatar110 andObserver175. GPU avatar100(Light/Dark,MSAA1/4),observer-avatar3,rounded-image40 run separately. Native supervisor now accepts Profile Avatar(2host+8client); Accordion13,Label16,Radio12,Toggle15 also pass on the same final binary. Korean typography fixtures explicitly register Malgun regular/bold like the application defaults; Avatar tests compare registered Hangul with an intentionally unregistered-font reference and restored exact pixels. Evidence: artifacts/fluent-avatar-2026-11-12/verification.json. Activity/presence/fullconformance andoriginal NativeAOT/performance gates remain separate. This supersedes historical counts below.

Latest Accordion qualification: `artifacts/fluent-accordion-2026-11-12/verification.json`.47 CPU contracts/7876 assertion executions, including Accordion27 andObserver164. Native Accordion13 plusLabel/Radio/Toggle43, andAccordion GPU32 pass. The existing native runner also accepts Profile=Accordion. Default CPU order now starts with widget-lifetime-only so catalog/structure mismatches fail before the remaining suite. Forced colors/motion/RTL/full Fluent/native-performance gates remain separate.

Latest qualification: `artifacts/label-relationships-2026-11-12/verification.json`.46/46 CPU contracts,7844 assertion executions includingLabel40 andObserver159; separate native Label16/Radio12/Toggle15 andLabel GPU28 pass, with33 exact prior PPMs. The native command below remains the execution path. Earlier numbers are historical. Label now includes the actual Text provider/LabeledBy relation; forced-color/full Fluent/NativeAOT/performance gates remain separate.

Native accessibility uses the existing compiled Graphics binary with a fresh output directory:

```powershell
$p = @{
    Binary = '<absolute-path-to-Duxel.Graphics.Tests.dll>'
    Profile = 'Label' # Label, Radio or Toggle
    OutputDirectory = '<absolute-fresh-output-directory>'
}
./scripts/Test-NativeAccessibility.ps1 @p
```

The supervisor starts only owned host/client processes, waits for fixture readiness, enforces a shared time limit and validates runtime/result contracts. It preserves independent logs, process exits and executable hashes, and terminates only its owned unfinished processes after failure. It never builds or switches SDKs. Current evidence: `artifacts/label-native-2026-11-12/verification.json`; CPU46/46 suites,7827 assertion executions (overlap allowed), Label24. Native Label13/Radio12/Toggle15 checks pass separately. Missing input, existing output and wrong-runtime rejection are exercised; forced timeout/interruption cleanup remains untested. Exact RC3 NativeAOT, GPU and whole-goal qualification remain separate.

`raster-lifetime-only` adds a CPU queued-frame/retained-layer pressure contract. Its GPU counterpart is `dotnet exec <GraphicsBinary> <fresh-output> raster-lifetime 2` (repeat independently with `3`). Image and SVG each fill the128-entry cache, dispose the source widget, replay retained content under pressure, remove the layer and retire its texture. Four CPU frames are prepared before FIFO GPU submission. Current45-suite qualification includes10 new CPU checks; each GPU configuration passes17 checks. Nine PPM files match across configurations, and four Image/SVG stage pairs match. Readback waits for completion, so GPU-overlap/long-growth acceptance remains separate. See [image lifetime](image-widgets.md).

The legacy GPU/widget bundle can now run as `widgets-no-resize`. It shares the normal test bodies while explicitly excluding `RootResizeRenderingTests`, `WindowInteractionTests` and `WindowRenderingTests`; their names appear in `result.json/SkippedTests`. The default invocation remains unchanged. The new mode rejects the native resize `msaa4` option before platform creation. It does not replace specialized GPU modes, external UIA sessions, NativeAOT, high-load performance or complete Fluent/SVG acceptance.

After the build below, run `dotnet exec <GraphicsBinary> <fresh-output-directory> widgets-no-resize 2`, then independently with frame count `3`. Use the actual absolute DLL path produced by that build. Run GPU cases sequentially and preserve stdout/stderr plus all result/pixel files. Current qualification: `artifacts/widget-core-gpu-2026-11-12/verification.json`;4642 checks per frame configuration,4038 matching PPM files, and the deferred option rejection verified. These are functional managed GPU results, not timing or process-memory comparisons.

[한국어](widget-contract-validation.ko.md)

Run the existing managed widget contracts through one explicit, isolated process runner. The default classification currently contains45 Graphics contracts and the separate Control keyboard/progress contract:46 suites. The standalone-icon contract covers compiled and borrowed SVG painters. Legacy CPU contracts previously reachable only after GPU initialization are now available as `legacy-contracts-only` (22 existing groups).

## Build and run

Use PowerShell7 on Windows with the registered Segoe UI/Consolas/Malgun font fixtures. Start at the repository root. Change `$build` to a writable location outside the test binary directories when needed. The dedicated `tests/managed-validation/global.json` pins SDK11.0.100; qualification uses runtime11.0.0. It does not replace the root or existing RC3 pins and does not qualify fixed RC3 NativeAOT. There is no SDK installation or native toolchain discovery.

```powershell
$repo = (Get-Location).Path
$build = 'L:/Cache/Duxel/contracts/build'
$options = @(
    '-c', 'Release', '-warnaserror',
    '-p:DuxelBenchmarkFramework=net11.0-windows',
    ('-p:DirectoryBuildTargetsPath=' + (Join-Path $repo 'samples/Duxel.Gallery/LocalSource.targets')),
    '-p:UseArtifactsOutput=true', ('-p:ArtifactsPath=' + $build), '-m:1'
)
Push-Location (Join-Path $repo 'tests/managed-validation')
try {
    foreach ($name in @('Duxel.Graphics.Tests', 'Duxel.Control.Tests')) {
        $project = Join-Path $repo ('tests/{0}/{0}.csproj' -f $name)
        $buildArguments = @('build', $project) + $options
        & dotnet @buildArguments
        if ($LASTEXITCODE -ne 0) { throw ('Build failed : ' + $name) }
    }
} finally { Pop-Location }
$contractParameters = @{
    GraphicsBinary = Join-Path $build 'bin/Duxel.Graphics.Tests/release/Duxel.Graphics.Tests.dll'
    ControlBinary = Join-Path $build 'bin/Duxel.Control.Tests/release/Duxel.Control.Tests.dll'
    OutputDirectory = Join-Path $repo ('artifacts/widget-contracts/' + (Get-Date -Format 'yyyyMMdd_HHmmss'))
    ExpectedFramework = '.NET 11.0.0'
}
./scripts/Test-WidgetContracts.ps1 @contractParameters
```

The runner requires a fresh output directory and never overwrites previous evidence. It does not build automatically or choose a different binary/runtime after failure. It records executable/dependency/assets/font hashes before execution and verifies they remain unchanged at the end. A binary hash identifies the tested build; the runner alone does not certify source-to-binary equivalence. Preserve the actual build logs with the result.

## Scope and results

`tests/Duxel.Graphics.Tests/contract-suites.json` explicitly classifies the published `*-only` identifiers. A source identifier missing from classification fails before a child starts. `catalog-only` is inventory; `session-failure-only` is a real Win32/GPU application-session fixture. They are explicitly outside the default CPU set. GPU readback, external UIA clients/application sessions, generator compiler/incremental tests, resize (deferred), high-load/long-growth performance, NativeAOT/runtime-image and full Fluent/SVG/platform acceptance remain separate requirements.

Each suite runs sequentially in a fresh process with an argument list and hidden console. stdout/stderr, PID, terminal exit code and result validation are separate. Default timeout is60seconds per suite; failure stops further suites and lists those not run. A successful exit without the expected result, exact framework/mode, matching positive assertion count and empty failure file is rejected. `inputs.json`, per-suite `process.json`/`result.json`/logs and `summary.json` are the evidence. `assertionExecutions` can include overlapping tests; it is not unique requirement coverage. `completedDefaultSuite` concerns this declared set only; `fullGoalComplete` remains false.

For a focused rerun, add `Suite = @('owned-sizing-only', 'legacy-contracts-only')` to a new parameter hashtable/output directory. A subset never reports the complete default suite. To verify the runner itself, pass the same binary paths and a fresh output directory to `./tests/Test-WidgetContracts.Runner.Tests.ps1`. Its6 checks cover the positive subset/space path, unknown suite, missing binary, unclassified mode, wrong runtime despite child exit0, and preservation of existing output. Forced timeout/interruption branches were not exercised by this qualification.

Current qualification: artifacts/contract-integration-2026-11-12/verification.json. All42 suites pass with7753 assertion executions, including704 legacy CPU assertions and62 Control assertions. No GPU/native/high-load or whole-goal completion follows from these numbers.

Latest extension qualification: artifacts/standalone-icon-2026-11-12/verification.json. All43 contracts pass with7770 assertion executions, including17 standalone icon checks. The42-suite result above remains historical evidence. Observer SVG GPU comparisons7 ran separately.

Latest Image qualification: artifacts/image-widget-2026-11-12/verification.json. All44 contracts pass with7793 assertion executions (Image23), SVG/Image GPU74 and Observer SVG GPU7 checks. Earlier42/43-suite results remain historical evidence, not whole-goal completion.

Latest cache-lifetime evidence: artifacts/raster-lifetime-2026-11-12/verification.json.45 managed suites/7803 assertion executions,10 new CPU checks and17 GPU checks per configuration pass. The earlier44-suite result remains historical evidence.

Latest Label qualification: artifacts/fluent-label-2026-11-12/verification.json.46 suites/7822 assertion executions including19 Label contracts, Label GPU28 andObserver50 pass. Legacy core GPU4642 and4038 unchanged prior images were also checked. [Standalone Label API and execution](label-widgets.md). Previous45-suite results remain historical evidence.

### Rounded image/SVG validation

The current default is47 Graphics contracts plus Control:48 suites/7905 assertion executions, including rounded-image24 andObserver169. This supersedes earlier historical counts above. GPU modes `rounded-image` (40 checks,MSAA1/4), `rounded-image-lifetime 2` and `rounded-image-lifetime 3` (19 each), `svg` (74) and `observer-svg` (7) run separately with the existing explicit Graphics binary and fresh absolute output directory. Run GPU processes sequentially. `image-shader-bench` explicitly enables GPU timestamps and writes256 raw measured samples after64warmups; it measures the existing rectangle scene, not a complete application performance gate. Evidence: `artifacts/rounded-image-2026-11-12/verification.json`. No resize or fixedRC3 NativeAOT qualification is implied.

## Standalone presence badge
`Fluent.PresenceBadge` supports eight statuses, OutOfOffice and six sizes. Run `./run-gallery.ps1` → **Avatar** → **Presence badge**; change status, OutOfOffice and size. The scrollable page preserves state and requests no periodic frames. Default icons use compiled Fluent font glyphs; supplied SVG painters reuse their cache. [API, resource lifetime and validation](fluent2-presence-badge.md). Avatar overlay/cutout/activity remain open.

## Activity ring and gallery scrolling

Set `avatar.Options = avatar.Options with { ShowActivityRing = true };` and optionally `avatar.ActivityText = "active";`. The default name gains the activity wording; `AccessibleName` still overrides it. The label is cached on mutation. A ring remains one passive Image with no action, focus, hover or animation timer.

The body keeps the selected14-size contract at natural size. Native layout measures the complete visual extent: stroke2 for body sizes<=48,3 for<=64,4 above64; the ring extends two stroke widths on each side (32px body→40px total). This reserves the decoration instead of painting over neighboring allocations. The body and gap scale uniformly under tighter parent allocations. Circular/square geometry and Light/Dark ring colors reuse the existing analytic border-only primitive and pinned persona palette. The gap paints nothing; it preserves the underlying pixels. Image sizes and ring toggles reuse the existing texture lease. `ShowActivityRing=false` restores the ordinary avatar; it does not claim Fluent inactive appearance.

Run `./run-gallery.ps1` from the repository root→**Avatar**→**Toggle activity ring**. The sample sets Korean activity wording. The page now uses the common `UiScrollView`, allowing its growing gallery to exceed the viewport without flex overflow. Actual scroll-to-bottom and offset/model preservation across tabs are checked at800×400.

Current evidence: `artifacts/fluent-avatar-ring-2026-11-12/verification.json`, managedSDK11.0.100/runtime11.0.0. Full Fluent active/inactive transitions, shadow, badge overlay/transparent cutout, forced colors and original exactRC3 NativeAOT/full-workload qualification remain open. [PresenceBadge](fluent2-presence-badge.md) is currently standalone; ring and badge must ultimately share a real transparent cutout across image/initials/icon paths.

## Circular subtraction
Use CircleCutout for a real transparent hole shared by primitive/image/text/SVG paint and retained/compiled layers. Run `./run-gallery.ps1` → **Rendering** → **Circle cutout**. [API and validation limits](circle-cutout.md). Avatar badge integration remains open.

Qualification snapshot:51/51 CPU suites,8136 assertion executions(CircleCutout20,Observer189);88 mask checks per configuration, five configurations with the actual Khronos validation instance/device layer loaded and no captured VUID/error. Configurations cover frame resources2/3,shared camera/basis settings,generic compiled mode and both schedulers. Additional GPU checks:primitive74,rounded-image40,SVG74,Avatar220,Presence552,Observer55. Assertions repeat across settings; these are not1455unique feature requirements. The sample now draws its execution counter after the actual body construction,so the same event frame reports its committed count without requesting another frame.
