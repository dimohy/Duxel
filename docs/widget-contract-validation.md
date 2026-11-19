# Widget contract validation

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
`Fluent.PresenceBadge` supports eight statuses, OutOfOffice and six sizes. Run `./run-gallery.ps1` → **아바타** → **상태 배지**; change status, OutOfOffice and size. The scrollable page preserves state and requests no periodic frames. Default icons use compiled Fluent font glyphs; supplied SVG painters reuse their cache. [API, resource lifetime and validation](fluent2-presence-badge.md). Avatar overlay/cutout/activity remain open.

## Activity ring and gallery scrolling

Set `avatar.Options = avatar.Options with { ShowActivityRing = true };` and optionally `avatar.ActivityText = "active";`. The default name gains the activity wording; `AccessibleName` still overrides it. The label is cached on mutation. A ring remains one passive Image with no action, focus, hover or animation timer.

The body keeps the selected14-size contract at natural size. Native layout measures the complete visual extent: stroke2 for body sizes<=48,3 for<=64,4 above64; the ring extends two stroke widths on each side (32px body→40px total). This reserves the decoration instead of painting over neighboring allocations. The body and gap scale uniformly under tighter parent allocations. Circular/square geometry and Light/Dark ring colors reuse the existing analytic border-only primitive and pinned persona palette. The gap paints nothing; it preserves the underlying pixels. Image sizes and ring toggles reuse the existing texture lease. `ShowActivityRing=false` restores the ordinary avatar; it does not claim Fluent inactive appearance.

Run `./run-gallery.ps1` from the repository root→**아바타**→**아바타 활동 링**. The sample sets Korean activity wording. The page now uses the common `UiScrollView`, allowing its growing gallery to exceed the viewport without flex overflow. Actual scroll-to-bottom and offset/model preservation across tabs are checked at800×400.

Current evidence: `artifacts/fluent-avatar-ring-2026-11-12/verification.json`, managedSDK11.0.100/runtime11.0.0. Full Fluent active/inactive transitions, shadow, badge overlay/transparent cutout, forced colors and original exactRC3 NativeAOT/full-workload qualification remain open. [PresenceBadge](fluent2-presence-badge.md) is currently standalone; ring and badge must ultimately share a real transparent cutout across image/initials/icon paths.

## Circular subtraction
Use CircleCutout for a real transparent hole shared by primitive/image/text/SVG paint and retained/compiled layers. Run `./run-gallery.ps1` → **렌더링** → **원형 잘라내기**. [API and validation limits](circle-cutout.md). Avatar badge integration remains open.

Qualification snapshot:51/51 CPU suites,8136 assertion executions(CircleCutout20,Observer189);88 mask checks per configuration, five configurations with the actual Khronos validation instance/device layer loaded and no captured VUID/error. Configurations cover frame resources2/3,shared camera/basis settings,generic compiled mode and both schedulers. Additional GPU checks:primitive74,rounded-image40,SVG74,Avatar220,Presence552,Observer55. Assertions repeat across settings; these are not1455unique feature requirements. The sample now draws its execution counter after the actual body construction,so the same event frame reports its committed count without requesting another frame.
