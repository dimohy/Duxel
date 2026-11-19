# Linux Duxel Gallery

The current Linux x64 NativeAOT build on .NET 11.0.0 completed all 105 WSLg presentation scenarios and three external SearchBox checks. It preserved the same scenario order and reported UI state as the managed build. SearchBox keeps the caret and input order when X11 sends multiple keys together.

```powershell
.\run-gallery-linux.ps1 -PublishedExecutable '/home/dimohy/.cache/duxel/gallery-linux-aot-2016/image/Duxel.Gallery.Linux'
```

An earlier Windows-to-WSLg check used Linux x64 NativeAOT on .NET 11.0.0. All 100 presentation scenarios completed and the process exited normally. X11 mapped the window and Windows UI Automation reported it on-screen in 49 observations. WSLg 1.0.79 logged none of the previous decoder errors during the run. The window-state observations do not compare host pixels. Evidence is under `W:/Cache/Duxel/gallery-source-20261117/linux-ga-1912`.

From Windows PowerShell, launch the Gallery in WSLg with `.\run-gallery-linux.ps1`. On Linux, run the published `Duxel.Gallery.Linux` binary with no arguments. It uses Fontconfig to find installed Latin regular and bold, Korean regular and bold, and monospace fonts. Ubuntu supplies `fc-match` in `fontconfig` and Korean desktop fonts in packages such as `fonts-noto-cjk`. WSL can use the Windows Malgun Gothic pair from the mounted host if the distribution lacks Korean fonts. To override the selection, supply those five paths in order with the WSL launcher's `-Fonts` argument or pass the paths directly to the binary. The current binary also accepts `--verify <fresh output directory>` without font arguments.

The launcher publishes and runs NativeAOT in WSL by default. Use `.\run-gallery-linux.ps1 -Managed` to publish managed code with the same pinned Linux SDK. Use `-PublishedExecutable` to open an existing executable without publishing.

For a source launch, the launcher uses the WSL distribution's architecture. It selects `linux-x64` for `x86_64` and `linux-arm64` for `aarch64`. NativeAOT publishing requires the native toolchain for that distribution. WSLg x64 is the verified execution target here.

The .NET 11.0.0 Linux ARM64 Gallery cross-published on Ubuntu x64. Under QEMU with Xvfb and lavapipe it registered AT-SPI and created a Vulkan swapchain. Both probes timed out before confirming a displayed frame. ARM64 hardware and full rendering remain unverified.

If a taskbar icon appears by itself, check `Microsoft-Windows-TerminalServices-RDPClient/Operational` in Windows Event Viewer. `WireDecoderError_DecodeW2S1InvalidData` with `0x80070490` identifies the reported WSLg display failure. [WSLg issue #1489](https://github.com/microsoft/wslg/issues/1489)

## Earlier runs and verification

Current shared NativeAOT Observer delivery is `7746d0b7`, with41 candidate and38 delivered scenarios passing. The unchanged Linux paragraph module retains its earlier native334/text114 qualification. The shared Input/IME example now includes an RTL embedding across Korean and Hebrew font intervals. Run `./artifacts/linux-present-path-2026-11-15/run-observer.ps1` from the Windows repository root. Previous `ce552ef3` is preserved. [Current evidence](../../artifacts/fluent-messagebar-2026-11-15/paragraph-font-context-verification.json) and [Linux dependencies/launch](../../docs/linux-text-shaping.md) supersede older delivery records below.

Current delivery (2026-11-15), superseding older records below: a scroll view fixes constrained-height MessageBar page termination. Passed38 Windows,41 Linux NativeAOT and38 delivered scenarios. Run `& 'W:/MyWorks/Duxel/artifacts/linux-present-path-2026-11-15/run-observer.ps1'`. The manifest selects `c8246d`, preserving prior `43119a`. RTL updates actual body line alignment and action placement. Full bidi remains incomplete, the six existing Windows shaping failures are fixed through shared DirectWrite shaping (37 expanded checks passed). [Qualified scope](../../docs/fluent2-messagebar.md).

2026-11-15 Fixed MessageBar page termination on constrained height by owning a scroll view. Verified small height,125% text and actual add/reorder/remove commands. Use the **키를 가진 동적 메시지** section. Passed36Windows,39Linux NativeAOT and36delivered-launcher scenarios. The Linux manifest selectsfff949;restart an old window using the documented launcher.


The MessageBar page now owns a real Group. Use **닫기**,**모두 다시 열기**,and **동작 줄이기** to inspect exit fade,reflow and focus. Passed17models,140GPU pairs,32Windows pages,and35pinned RC3 NativeAOT Linux presentation cases plus32delivered-launcher pages. The Linux manifest now selects310c73;restart an old window with the documented launcher. Automatic announcements,title relations and full dynamic-collection conformance remain pending.


2026-11-15 Fixed Windows startup by mapping the new MessageBar factories to an actual **메시지 표시** demo page while retaining the startup catalog gate. Run `./run-gallery.ps1`; `./run-gallery.ps1 -Verify` presents all 32 pages and exits. Separate process logs are retained under `%LOCALAPPDATA%/Duxel/ImplementationObserver/logs/`.

Corrected the shared Linux font descent sign, fixing vertical alignment for buttons, badges, checkboxes, radio labels, spinners, body text, input and spin buttons. Restart the old window using `& ./artifacts/linux-present-path-2026-11-15/run-observer.ps1`. Validation passed 25 native metric cases, 32 widget/caret pixel comparisons and 35 NativeAOT presentation scenarios. This change does not establish a performance improvement.


2026-11-15 standalone Fluent text now publishes actual Korean content, updates and stable native identities in the current delivery. CPU64, Label63,24raw GPU pairs and independent native clients passed. Three new performance pairs have overlapping ranges, so their direction is inconclusive; prior measured binaries remain preserved.

2026-11-15 the current delivery includes the **스켈레톤** tab. Three independent runs per variant compared it with the protected Linux performance baseline using 4 workers. No disjoint regression appeared in the tracked CPU/input/composition metrics; this does not claim a new speed improvement. `artifacts/linux-observer-delivery.json` is the single current-delivery manifest. Historical measured executables and records remain preserved.

Launch directly from PowerShell on this PC:

```powershell
& 'W:/MyWorks/Duxel/artifacts/linux-present-path-2026-11-15/run-observer.ps1'
```


## Latest Linux CPU worker qualification

Three independent pairs of the identical RC3 NativeAOT executable show37.60–38.51% lower CPU in three scenes when llvmpipe raster workers change from the default24 to4. MSAA,fonts and damage area are unchanged. Slots1/3 pass7,140 checks and1,216 raw-image pairs exactly match the default-worker runs. The qualified launcher applies this only to its launched process. Source execution can select `-SoftwareRasterThreads 4`; omission retains the driver default. See [launch,scope and remaining limits](../../artifacts/linux-driver-worker-budget-2026-11-15/README.md). WSL hardware execution remains unapplied because a mandatory composition feature is missing.

Use the [latest batched-damage/NativeAOT direct launch guide](../../artifacts/linux-sparse-damage-2026-11-15/README.md). From the repository root, run `./artifacts/linux-sparse-damage-2026-11-15/run-observer.ps1`. Actual repair area falls46.67–92.36%; frozen Fluent CPU falls11.54%. Other CPU/input ranges overlap and support no speedup claim. Records below are separate historical checkpoints.

2026-11-15 shared input-loop fix: a stationary held button no longer counts as new input on every iteration. Three alternating RC3 NativeAOT pairs reduced redundant builds from239–317 per400ms to0 in every candidate condition. Process CPU work in this stationary-hold workload decreased99.94–99.96%. Paced dragging and scheduled SpinButton repetition remain active. The final executable passed32 presented pages,384 native input changes and normal exit. Physical display latency and overall user acceptance remain separate. On this PC, run `./artifacts/linux-held-input-idle-2026-11-15/run-observer.ps1` from the repository root to open the verified executable. [Validation scope](../../artifacts/linux-held-input-idle-2026-11-15/README.md).

2026-11-15 current source update: the launch path includes shared Component scrolling and Markdown image validation. This NativeAOT build passes32 screen presentations and384 native pointer changes with normal exit. Earlier comparative performance numbers below belong to the preserved `observer-rc3-aot-publish` benchmark build; this update is a regression check, not a new speedup comparison.

2026-11-15 input latency fix: Vulkan presentation now owns a dedicated X11 connection instead of sharing the input connection. Three alternating native drag comparisons recorded input delays above50ms in150/288 baseline samples and0/288 candidate samples. The candidate maximum was4.074ms. These are native dispatch measurements, not physical mouse-to-monitor latency.

2026-11-15 latest GPU camera/linear-gradient builds and launch commands: [SVG guide](../../docs/svg-gpu-camera.md). Linux presentation32 passes; whole-browser pixel equality remains incomplete.

Last synced: 2026-11-15

Uses the same 31 page implementations as the Windows Observer. The Linux host supplies X11, Vulkan, a Linux image decoder and explicit font files. V-Sync defaults to off and event waiting to on. The top-right **갱신 영역 강조** switch defaults to off; enabling it observes every page and popup.

## Launch in WSLg from Windows

On this PC, run the latest verified NativeAOT fix with the command below from repository-root PowerShell. It opens the existing executable without publishing or compiling; WSL does not need a .NET SDK. This path identifies this PC's verification build. On another PC, specify your own published executable.

```powershell
$fonts = @(
    '/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf',
    '/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf',
    '/mnt/c/Windows/Fonts/malgun.ttf',
    '/mnt/c/Windows/Fonts/malgunbd.ttf',
    '/usr/share/fonts/truetype/dejavu/DejaVuSansMono.ttf'
)
$launch = @{
    Distribution = 'Ubuntu'
    Fonts = $fonts
    PrivateAccessibilityBus = $true
    PublishedExecutable = '/home/dimohy/.cache/duxel/observer-text-accessibility-rc3-20261115/publish/Duxel.ImplementationObserver.Linux'
    SoftwareRasterThreads = 4
}
./run-gallery-linux.ps1 @launch
```

Font order is Latin regular, Latin bold, Korean regular, Korean bold, monospace. These are the tested WSL host's example paths; Windows fonts are not a Linux requirement. Supply other explicitly installed fonts. This project neither copies nor redistributes those font files.

To publish current source, remove `PublishedExecutable` from `$launch`. The default now publishes NativeAOT inside WSL with the SDK pinned by `global.json`. Its default location is `.cache/duxel/toolchains/dotnet-<pinned version>/dotnet` under the WSL user home; select another installation with `-LinuxDotnet /absolute/path/dotnet`. A missing tool or mismatched SDK fails before publishing without substituting managed output. Initial publishing requires native compilation time. Use `-Managed` to publish managed code with the same pinned Linux SDK. Each publish uses a separate run directory to preserve existing executables.

Requires an X11 `DISPLAY` such as WSLg, a Vulkan driver, X11, FreeType, HarfBuzz, Cairo, GdkPixbuf, Pango, session D-Bus and AT-SPI. `-PrivateAccessibilityBus` explicitly starts an isolated accessibility bus for verification. Omit it to use the existing desktop accessibility bus. Missing capabilities fail explicitly; the host does not switch renderers or silently disable accessibility.

## Run directly on Linux

Pass the five font paths to the published `Duxel.ImplementationObserver.Linux` executable. With a .NET11 SDK installed, run the following from the repository's `tests/managed-validation` directory, replacing the repository paths:

```bash
dotnet run --project /path/to/Duxel/samples/Duxel.ImplementationObserver.Linux/Duxel.ImplementationObserver.Linux.csproj \
  -c Release -p:DuxelSourceTargetFramework=net11.0 \
  -p:DirectoryBuildTargetsPath=/path/to/Duxel/samples/Duxel.ImplementationObserver/LocalSource.targets \
  -- /path/latin.ttf /path/latin-bold.ttf /path/korean.ttf /path/korean-bold.ttf /path/mono.ttf
```

## Verification

Append `--verify <fresh output directory>` after the font arguments to visit each page, wait for its Vulkan presentation acknowledgement, then advance. The verifier closes only its owned window after all pages and writes `result.json`. Normal interactive execution does not exit automatically.

All 29 pages and clean shutdown passed on Ubuntu/WSLg x64 with managed .NET11.0.0. This does not certify browser pixel equality, full AT-SPI conformance or screen-reader behavior. Existing full Linux accessibility-suite issues remain separate. [Common repaint guide](../../docs/repaint-observer.md).

## GPU camera development build

The current executable is `/home/dimohy/.cache/duxel/observer-idle-verified-20261115/publish/Duxel.ImplementationObserver.Linux`. After defining the five-font `$fonts` array above:

```powershell
$exe = '/home/dimohy/.cache/duxel/observer-idle-verified-20261115/publish/Duxel.ImplementationObserver.Linux'
wsl.exe -d Ubuntu --exec dbus-run-session -- $exe @fonts
```

Open SVG → browser comparison to inspect zoom, rotation and margins. Append `--verify-gpu-camera <fresh output path>` for automatic verification. It adds complex SVG presentations at 100%, 200% and 400% to the existing 29 pages, for 32 scenarios, and rejects CPU raster work from the first camera output onward. All32 presentations and normal exit passed. The explicit bitmap example remains at one CPU rasterization; camera zoom adds zero. Full pixel conformance remains separate, as described in the [GPU camera guide](../../docs/svg-gpu-camera.md).

The current WSL device is llvmpipe (software Vulkan on CPU). These32 results establish functionality, not hardware GPU speed; see the selected-device limits in the GPU camera guide.

## Ordinary slider diagnosis

Append `--verify-slider-performance <fresh output directory>` after the five font paths. It compares an isolated Fluent slider, the full Fluent page, and frozen diagnostic text, each with16 warmup/32 measured frames. This measures shared-widget model changes, composition, GPU time, presentation intervals and repair bounds, not native pointer latency or pixel equality. It exits only its owned window.

The Present counter no longer changes the header every frame. The whole diagnostic row samples on the existing0.5-second FPS cadence and does not request its own frames. Three alternating llvmpipe runs changed the Fluent interval median from8.17 to2.60ms and repair area from905352 to108489px. Isolated/frozen-control timing ranges overlap, so no separate improvement is claimed there. Native input stalls are addressed below; the following NativeAOT section covers first-use composition. Physical display latency remains unmeasured.

## Native input regression

Append `--verify-pointer-performance <fresh output path>` after the five fonts. It sends X11 events exclusively to the probe window and checks actual pointer hit testing and value bindings for minimal/full/frozen-diagnostic slider screens, each with96 warmup/32 measured changes. Accessibility stays enabled and the owned window exits normally. It does not move the desktop pointer or send input to another window.

Unlike the model-value sweep, this traverses native platform input. Queue acknowledgement includes about2ms of probe scheduling and is not monitor display latency. Across three alternating pairs, dispatch medians changed98.195→1.486ms for the minimal slider and70.297→1.351ms for frozen diagnostics. Full Fluent median ranges overlap, so that median difference is not claimed as an improvement; long stalls are reported separately. First-use composition is compared in the NativeAOT section below; physical display latency remains unmeasured.

## Pinned Linux NativeAOT build

The current path above is Linux x64 NativeAOT built with SDK `11.0.100-rc.3.26522.148` and runtime `11.0.0-rc.3.26522.148`. Running it needs no .NET installation; the existing Linux native libraries and five explicit fonts remain required. This is separate from the original Windows NativeAOT qualification.

Three alternating same-RC3 pairs measured Fluent first composition134.6612→31.7940ms(76.39% reduction), UI composition during96-warmup native dragging0.67805→0.25695ms(62.10%), and presentation interval1.971045→1.545877ms(21.57%). Minimal slider dispatch gets slightly slower, about1.34→1.53ms; this is not a blanket input-latency claim. First-use means each process's first composition with page/font caches warm, not cold boot.

Both managed/NativeAOT pass87 text,73 vector and34 render contracts. All40 full output files, including12 rawRGBA images, match byte for byte; this does not establish pixel equality for the entire Observer. The NativeAOT Observer also passes32 presentations and clean exit.

For a new source publish, use the .NET 11.0.100 Linux SDK from the repository root. Select the installed SDK with `-LinuxDotnet` when running the Windows launcher, or call it directly on Linux as shown below.

```bash
dotnet publish samples/Duxel.Gallery.Linux/Duxel.Gallery.Linux.csproj \
  -c Release -r linux-x64 --self-contained true -p:PublishAot=true \
  -p:DuxelSourceTargetFramework=net11.0 \
  -p:DirectoryBuildTargetsPath="$PWD/samples/Duxel.Gallery/LocalSource.targets" \
  -p:RuntimeFrameworkVersion=11.0.0 \
  --artifacts-path "$HOME/.cache/duxel/gallery-net11-build" \
  -o "$HOME/.cache/duxel/gallery-net11-publish" -warnaserror -m:1 -nr:false
```

Use fresh output paths to preserve verified executables. At this historical checkpoint the Windows launcher used managed publishing. The current `run-gallery-linux.ps1` defaults to pinned-SDK NativeAOT source publishing; managed publishing requires `-Managed`.

To launch only the published executable, define `$fonts` above and run this without an SDK publishing step:

```powershell
$launch = @{
    Distribution = 'Ubuntu'
    Fonts = $fonts
    PrivateAccessibilityBus = $true
    PublishedExecutable = '/home/dimohy/.cache/duxel/observer-idle-verified-20261115/publish/Duxel.ImplementationObserver.Linux'
}
./run-gallery-linux.ps1 @launch
```

Only for finite verification, add `SampleArguments = @('--verify-pointer-performance', '/tmp/fresh-result-path')`; the result path must not exist. Omitting `PublishedExecutable` retains managed source publishing. An explicitly missing executable fails instead of selecting a different build.

MessageBar: use **표시할 때 자동 공지** and **차분한 공지로 고정** on the scrollable **메시지 표시** page and inspect the announcement request count. Actual title relationships and one mount/priority announcement are supported; reorder/removal do not repeat it. Requests do not prove spoken completion. MessageBar is 12/13, with full conformance pending. [API and scope](../../docs/fluent2-messagebar.md).

MessageBar → **오른쪽에서 왼쪽 배치** switches real slot/action placement. Common `UiWrapOptions.RightToLeft` mirrors child X positions while retaining navigation and row order. It does not guarantee arbitrary child paragraph bidi. Whole MessageBar remains 12/13.

MessageBar is 12/13. Actual title targets and automatic announcements passed Windows UIA 8 client/1 host and pinned Linux RC3 NativeAOT AT-SPI 9 client/3 host checks. Complete paragraph RTL remains; Windows NativeAOT is a separate gate. The qualified Linux Observer is retained.

MessageBar multiline/shape/direction/announcement options also apply to newly inserted dynamic messages. Passed 34 interaction/page checks and 38 Windows/41 Linux/38 delivered presentation scenarios. MessageBar is 12/13; complete RTL/reflow remains pending.
