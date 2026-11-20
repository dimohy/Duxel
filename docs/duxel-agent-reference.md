# Duxel Agent Reference

Last synced: 2026-11-19

The SVG Gallery uses the `BeginChild` document-scrolling option to combine compatible wheel messages over one expanded code block and captured thumb movement. [Fast SVG source scroll](optimization-session-2026-11-19.md#fast-scroll-over-svg-sample-source)

Successive retained layer revisions reuse the stable geometry key, capture-record array and matching owned cache record. Submitted frames retain their own draw lists and resource leases. [Allocation and pixel verification](optimization-session-2026-11-19.md#reusing-cache-records-for-versioned-layers)

Retained layer captures share temporary builder storage at each open layer depth. Each completed draw list holds its own buffers through replay and submission. [Memory and pixel verification](optimization-session-2026-11-19.md#sharing-retained-layer-capture-scratch)

The widget-retention ledger records all four CPU reuse criteria, including prepared layout, geometry replay, cache bounds and queued-frame lifetime. [Verification scope](widget-cpu-retention.md)

Basic widget input, state and GPU pixels were rechecked on Windows .NET 11 with three frame slots. The current managed build matches all shared images from the archived NativeAOT run. [Acceptance status](acceptance-status.md)

The container ledger records all six acceptance criteria, including ScrollView, the public source-generated Footer, Gallery presentation and sample-bound source, and a matched Linux NativeAOT workload comparison. [Acceptance status](acceptance-status.md)

The Gallery acceptance ledger records all ten requirements, including the Windows desktop run, Input and Textarea editing, and Tooltip behavior. [Acceptance status](acceptance-status.md)

At the SVG texture-cache limit, a widget can replace an exclusively owned texture after preparing the new source. Current-frame and retained-layer references continue to protect the old texture. [SVG resource lifetime](svg-support.md)

The Gallery Tree row-button example supports F2 to enter the report action, Enter to invoke it, and Escape to return focus to the row. The Linux X11 client checks delivery and AT-SPI focus in the live Gallery. [Tree keyboard verification](fluent2-tree.md)

Open **입력과 선택 → RadioGroup** in the Gallery to compare wrapping at parent maximum widths of 180 and 360 logical pixels. The relevant C# source follows each live example. [RadioGroup examples](fluent2-radio-group.md)

Vulkan compares changed rectangle and circle records through separate loops. The Windows Arc replay measured a lower first partial-repair planning cost with identical repair bounds and final pixels. [First partial repair measurement](optimization-session-2026-11-19.md#first-partial-repair-during-an-arc-zoom-gesture)

Code views use the parent's exact allocation when both axes are fixed and prepare painting only when the text is in view. In the Gallery Arc replay this reduced managed allocation across zoom, rotation and source selection. [Replay and repair measurements](optimization-session-2026-11-19.md#shared-code-view-work-during-arc-zoom-rotation-and-selection)

Gallery **SVG → 호·곡선 비교** exercises the same page during zoom, rotation, and C# selection. Fluent Nav keeps each accessible group ID for the lifetime of its render scope. Windows managed replay showed lower per-frame allocations in all three interactions, with mixed submission timing. [Measurements and reproduction](optimization-session-2026-11-19.md#arc-comparison-and-navigation-allocations)

The SVG document scrollbar applies captured motion before laying out its contents. In a batch containing only moves of the captured thumb, it lays out at the final position. Vulkan still repaints the affected document viewport. Linux managed and NativeAOT runs verify that both tabs move and restore the original code pixels during a native drag. [Native drag replay](optimization-session-2026-11-19.md#dragging-the-svg-source-scrollbar)

The Gallery starts its FPS clock on the first rendering pass that prepares a frame for presentation. Later samples exclude the host's explicit idle waits through `UiFrameInfo.IdleWaitSeconds`. Selecting expanded read-only code can combine successive pointer moves when the code has no internal scroll range. The Linux X11 Layer and Borders replay compares scrollbar movement, wheel movement and code selection in managed and NativeAOT builds. [Layer and Borders replay](optimization-session-2026-11-19.md#mixed-scrolling-and-source-selection-in-svg-layer-and-borders)

Document wheel batches can continue while the read-only code under the pointer retains focus after button release. Direction changes and scroll boundaries retain their ordered handling. [Wheel replay](optimization-session-2026-11-19.md#wheel-input-after-releasing-a-code-selection)

The SVG comparisons show the exact SVG as the transform settles and update up to two C# examples on each following frame. The Windows Arc replay checks that event-wait mode schedules each pending batch and that all six examples reach the final transform. [Measurements and verification scope](optimization-session-2026-11-19.md#separate-svg-and-code-updates-after-a-gesture)

With Vulkan profiling on, use the `frame` fields to join the submitted frame with its draw data, acquired image and presentation. Match GPU time by `gpuSubmission`, which identifies the completed submission read on that line. [Arc zoom measurements](optimization-session-2026-11-19.md#vulkan-frame-correlation-during-arc-zoom)

On Windows, `gallery-svg-cold-warm input-profile` replays the Arc gestures in two Gallery sessions in one process. It records Core stages and separate navigation, toolbar and page timings for the input and presentation passes. [Gallery section timings](optimization-session-2026-11-19.md#direct-timing-of-gallery-sections-during-repeated-zoom)

When a code view leaves and returns to the viewport, Core can reuse one bounded accessibility geometry snapshot. The clipped view exposes no character geometry. In the Windows Arc comparison, zoom p95 to Vulkan submission fell from 9.56 to 7.52 ms. [Measurements and scope](optimization-session-2026-11-19.md#arc-comparison-when-code-returns-to-the-viewport)

Native text selection builds visual fragments in bounded temporary storage and scales the returned array directly. In the Arc code selection replay, 16-move frame allocations fell from 43.5 to 26.6 KB, with matching selection and pixels. [Selection and rotation measurements](optimization-session-2026-11-19.md#native-text-selection-fragments)

The Arc pointer benchmark includes composition counters for correlating Windows allocation traces. In the starting rotation frames, the trace sampled DirectWrite work for changing status labels. Font selection now avoids temporary per-character strings and produces missing-glyph messages when diagnostics are enabled. [Rotation traces and measurement scope](optimization-session-2026-11-19.md#rotation-startup-and-directwrite-diagnostics)

The SVG Gallery's live angle and size labels reuse text fragments during transforms and return to whole-label painting after the existing idle deadline. The Windows Arc rotation p95 to Vulkan submission was 4.01 → 3.00 ms in three replay pairs. Settled content with highlight off matched byte for byte. [Gesture measurements and rendering trade-offs](optimization-session-2026-11-19.md#svg-gallery-live-number-labels)

The Windows test `gallery-svg-pointer arc win32 burst desktop` observes a marker in the visible test window after each synthetic move. It needs an unobscured window and provides sampled desktop observation time. [Reproduction and limits](optimization-session-2026-11-19.md#windows-visible-desktop-observations)

The legacy immediate menu paints selection with the shared vector checkmark. [Menu guide and GPU verification](menu-foundation.md)

On Linux, the font atlas creates its disk cache under `$XDG_DATA_HOME/Duxel/FontAtlasCache`, or under `$HOME/.local/share/Duxel/FontAtlasCache` when `XDG_DATA_HOME` is unset. A fresh profile can create the directory on first use. Set `DUXEL_FONT_DISK_CACHE=0` to disable disk caching.

The external Linux Gallery probe verifies Rating half-point selection, compact RatingDisplay semantics and selection retention after navigation. [Rating verification](fluent2-rating.md)

A separate AT-SPI probe invokes the NavigationBar's leading and trailing actions, checks its updated title, and drives the secondary period selector. [NavigationBar verification](fluent2-navigation-bar.md)

Built-in RadioGroup layouts inherit `FluentProvider.Direction`. The Gallery direction example shows mirrored placement and arrow navigation while preserving the selected value. [RadioGroup direction](fluent2-radio-group.md)

The Linux platform test reuses the RadioGroup parent allocation and generated/immediate GPU fixtures. The current Linux managed and NativeAOT images match at one and three frame slots. The live Gallery verifies wrapping and preserved selection at both parent widths. [RadioGroup cross-platform results](fluent2-radio-group.md)

The direction example shows mirrored Checkbox and Switch labels, Switch thumb travel and horizontal Slider painting and input. [FluentProvider](fluent2-provider.md)

A separate Windows UI Automation client confirmed exactly one cached `TooltipClosed` after `WM_CLOSE`, with stale live-provider rejection. The strict fixture passed with Windows x64 .NET 11.0.0 managed and NativeAOT builds. [Tooltip shutdown verification](tooltip-events.md#qualification)

The optional live holiday check invokes the actual Gallery button and waits for the presented calendar to show the published May 2026 substitute holiday. It passed on Windows managed and WSLg Linux x64 NativeAOT. [Run the live check](calendar-holidays.md#gallery-verification)

Separate Linux AT-SPI and Windows UI Automation clients select a calendar date after the simulated outage and confirm that selection survives live holiday lookup. Windows managed and x64 NativeAOT Gallery hosts passed. [Calendar verification](calendar-holidays.md#gallery-verification)

The ERD sample prepares the full font atlas before composing its retained scene. Set `DUXEL_ERD_TRACE_STARTUP=1` to inspect the first eight UI frames and application startup timings. `DUXEL_ERD_FAST_STARTUP=1` selects the two-stage atlas for comparison. [Scope and evidence](erd-layer-lab.md).

Set `UiLayerOptions.FontAtlasIndependent` only when a retained layer's paint and layout are independent of the default glyph atlas and its metrics. A retained child that depends on the atlas also makes its parent dependent, including when replayed into the parent. Scale, theme and text-backend changes retain their existing invalidation behavior.

The Windows `directtext-cache 3` fixture verifies atlas replacement under both .NET 11 managed and NativeAOT. Both pass 45 checks and produce 24 byte-identical Vulkan RGB images. Raster text responds to the atlas change, and retained fixed-origin projected text matches a fresh repaint across an atlas line-height change. Evidence is in `W:/Cache/Duxel/gallery-source-20261117/atlas-vector-1922/comparison.json`.

The archived .NET 11 ERD NativeAOT image showed continued Private Bytes growth during motion and stable sampled memory at idle. In the motion trace, all 61 increases in GC commitment occurred in intervals with Gen0 collections. [ERD observations](erd-layer-lab.md)

The Linux Gallery launcher resolves the RID from the WSL distribution's architecture. [Linux launch](../samples/Duxel.Gallery.Linux/README.md)

ERD memory profiling records frame phases and screen subphases. With 1,200 resident tables, the frame-matched NativeAOT trace recorded 4,416 B for UI rendering and 632 B for ordinary Vulkan submissions. [Allocation sampling](erd-layer-lab.md)

Building the fixed ERD panel introduction once reduced sampled UI allocations by 168 B per frame in three alternating runs per version. [Allocation comparison](erd-layer-lab.md).

The ERD scene trace recorded 1,784 B per sampled frame in selected relationship drawing during table motion and 0 B during camera-only motion, using the same NativeAOT image. [Scene allocations](erd-layer-lab.md).

The Windows managed and Linux NativeAOT Gallery verification now includes calendar selection after a simulated holiday outage. [Holiday provider verification](calendar-holidays.md#gallery-verification).

The Gallery uses `Duxel.App.KoreanHolidayProvider` to fetch the public `holidays-kr` year files without an API key. Fetch failures omit holiday annotations while date selection remains available. The separate KASI provider distinguishes portal errors from invalid response headers. [Holiday response behavior](calendar-holidays.md#execution-and-failure-behavior).

The Gallery presentation check also operates DatePicker, TimePicker and DateTimePicker through their actual popup and wheel controls. [Date and time verification](date-time-pickers.md).

An independent Windows UI Automation client opened TimePicker in the Windows x64 .NET 11 NativeAOT Gallery, changed the minute, and verified the saved value after closing the popup. [Windows external accessibility verification](date-time-pickers.md).

The same client also posts Win32 wheel messages to the test Gallery window. One message changes the minute by one step in each direction while seconds stay unchanged. [Windows wheel verification](date-time-pickers.md).

The external Windows client also selects a month and day in DatePicker and verifies that DateTimePicker preserves either half of the saved value when the other half changes. [Windows date picker verification](date-time-pickers.md).

Managed and NativeAOT .NET 11.0.0 matched all 59 Windows Vulkan PNG pairs for the calendar and date/time picker suites. [Runtime image comparison](date-time-pickers.md).

The common Vulkan presentation fixture matched 214 of 574 same-name cases across Windows and WSLg Linux. It matched all 160 sparse-update cases. [Cross-host pixel baseline](../tests/Duxel.Vulkan.Linux.Tests/README.md).

Eight raw samples from that baseline reproduced their hashes. Six rounded-primitive scenes differed by at most one RGB level across hosts and had identical alpha. Both sparse controls matched exactly. [Raw capture commands](../tests/Duxel.Vulkan.Linux.Tests/README.md).

The text preflight diverged at glyph texture creation, before Vulkan received it, even using the same Segoe UI font file. Only text rows differed in the final preflight frame. [Text comparison](../tests/Duxel.Vulkan.Linux.Tests/README.md).

`glyph-texture-replay` reuses captured RGBA8 glyph bytes on both hosts. The eight Linux managed and NativeAOT frames matched. Across Windows and WSLg, differences reached one RGB level at whole-pixel positions and two at fractional positions. [Replay command and evidence](../tests/Duxel.Vulkan.Linux.Tests/README.md)

The same Linux NativeAOT replay on two lavapipe environments matched all four integer-placement frames. Fractional placement differed by up to three RGB levels. The environments also differ in system libraries. [Driver comparison](../tests/Duxel.Vulkan.Linux.Tests/README.md)

Vulkan initialization names each missing required device feature, including `dualSrcBlend`, before attempting logical device creation. [Linux Vulkan requirements](linux-platform.md)

If Vulkan renderer construction fails, it releases acquired swapchain and renderer resources before destroying the device, surface, and instance. The Linux retry fixture checks failures through the end of initial swapchain setup. [Linux retry fixture](../tests/Duxel.Vulkan.Linux.Tests/README.md)

If app startup fails before rendering starts, the session releases its UI context and restores the previous font diagnostics callback. It publishes render wake signals only when it is ready to start the render thread. The Windows and Linux startup retry fixture also tests a shutdown diagnostics exception after presentation. [Startup retry fixture](linux-platform.md)

Disposing an owned Win32 window after its loop stops leaves the UI thread available for the next window. The backend still handles a thread quit message delivered while its loop is running. [Win32 thread quit semantics](https://learn.microsoft.com/en-us/windows/win32/winmsg/wm-quit)

The Linux AT-SPI client verifies month and day selection, preservation of the date and time, and popup closure in the x64 NativeAOT Gallery. [Linux date picker verification](linux-platform.md).

An independent Linux AT-SPI client can open TimePicker, select a minute and read back the committed time in the NativeAOT Gallery. [External Linux verification](linux-platform.md).

The Linux TimePicker probe also verifies that one X11 wheel event moves the minute one step in either direction while retaining seconds. [X11 wheel results](date-time-pickers.md).

The Linux x64 .NET 11.0.0 NativeAOT Gallery passed 100 Vulkan presentation scenarios and exited normally. On WSLg 1.0.79, Windows UI Automation observed the mapped Gallery window on-screen. [Launch and scope](../samples/Duxel.Gallery.Linux/README.md).

New .NET 11 builds, verification and benchmark runs use the stable SDK 11.0.100 and runtime 11.0.0. The root project and runtime comparison configurations reject prerelease SDKs. Historical reports retain their recorded RC3 versions. [SDK comparison](../tests/runtime-net11/README.md).

Toolbar and SearchBox consume scoped FluentProvider direction. Horizontal toolbar geometry and arrow navigation reverse together. SearchBox mirrors the icon and clear action without replacing the editor or its bound text. Try both directions in the FluentProvider Gallery example. [FluentProvider](fluent2-provider.md#local-layout-direction).

The Windows x64 Gallery publishes and runs with .NET 11 GA NativeAOT. Keep the `Assets` directory beside the executable for the gallery previews and retain the redistribution notices. Stable .NET 11.0.0 managed and NativeAOT runs matched 4,038 common widget, 185 container and 245 RadioGroup allocation RGB readback pairs. Resize remains deferred. [Toolchain](native-toolchain-status.md) and [widget validation](widget-contract-validation.md#stable-net-11-runtime-comparison).

The current Windows managed RadioGroup build matches 245 images from the archived NativeAOT executable. Fresh Linux managed and NativeAOT builds match 244 images. The current Windows managed Tooltip fixture also receives one close at immediate HWND teardown. [RadioGroup](fluent2-radio-group.md) and [Tooltip events](tooltip-events.md).

Windows raises ordinary Tooltip events after disconnecting removed live providers. Separate .NET 11.0.0 UI Automation clients received the captured close on focus transfer, target removal, explicit dismissal and window hide. The immediately closed HWND still delivers the final close. [Tooltip event ordering](tooltip-events.md).

Linux x64 managed and NativeAOT Gallery builds each pass the external AT-SPI Tooltip probe. It checks hidden help, Label mode, child notifications and retired objects. The X11 keyboard extension checks that Escape closes the automatic Tooltip while retaining focus, and that Tab and Shift+Tab can reopen it. [Linux Tooltip probe](tooltip-events.md#linux-at-spi).

The Windows `observer-gallery` test checks Gallery navigation panes, scroll reveal, selected tab publication and stable pixels after the displayed source updates. It passed 326 checks on .NET 11.0.0 and exported 525 GPU captures. Evidence is in `W:/Cache/Duxel/gallery-source-20261117/gallery-uia-2082/gallery-9/result.json`.

Calendar is an in-progress Duxel extension for single-date and range selection. Globalization research led to22 model cases covering IUiCalendarSystem/UiCultureCalendar/UiCalendarMonth/UiCalendarDateRange. Visual interaction is the next unfinished step. [Research and contracts](calendar-globalization.md).

Fluent.HeadsUpDisplay provides centered blocking/nonblocking presentation, one-second temporary dismissal and200ms linear transitions. KeyboardHeight is host-supplied. Five Gallery snippets follow actual settings; catalog linkage is47/55. [HUD guide](fluent2-heads-up-display.md).

Gallery Korean descriptions explain how to use each example in natural sentences rather than chains of implementation terms. API names and feature/validation limits are preserved; copy changes are checked with the connected example sources and accessibility assertions. Review record: `artifacts/gallery-korean-copy-2026-11-17/changes.json`.

Nav destinations accept `UiNavSecondaryAction`. The action paints on hover/focus and remains accessible at rest; right-click, Shift+F10 and the menu key expose the same operation. Open `동작과 탐색 → Nav → 개요 고정` to verify selection preservation and execution count. [API and input rules](gallery-navigation.md).

Shared `Fluent.Nav` replaces the gallery’s vertical TabLists. It preserves seven alphabetized functional groups, vector icons, missing badges and the fixed theme header while adding category disclosure, RTL, arrow navigation and Navigation/Link/Pane accessibility. Open `동작과 탐색 → Nav` for the example. [Contract and API](gallery-navigation.md).

Native Vulkan boundary probes use the ordinary triangle pipeline at4×MSAA, with fixed, approximate-size and integer-local replay controls. All retain the one-pixel/max56 browser mismatch; valid capture checks do not certify SVG equality. These are test-only Windows125%-DPI modes. [Contracts](svg-browser-validation.md).

The managed `--browser-root-transforms` comparison retains independent fractional-root and integer-root controls, with separate white-target and transparent phases. Browser background phases diverge at the critical edge, so root-only corrections are not qualified. [Scope](svg-browser-validation.md).

Managed SVG `--probe-pixel` diagnostics attribute a pixel to actual compiled leaves and source tree paths, with isolated colors and fixed-point sample-mask checks. Parent effects and unsupported expanded hierarchies are explicit. This test-only path does not change product rendering or replace strict browser comparisons. [Usage](svg-browser-validation.md).

SVG arcs retain conic controls/weights through bounds, clipping, filters, strokes, dashes, CPU and GPU evaluation. All six arc references exactly match transparent and white-initialized GPU targets. Post-composition and original complex-SVG equality remain open. Both scene and stroke shaders use protocol4 and reject actual previous shaders before module creation. [Qualification and commands](svg-browser-validation.md).

SVG shared source-over stores foreground RGBA8 before blending in both CPU diagnostics and Vulkan. New `gpu-blend-backdrops` and managed `--browser-blend-backdrops` compare12 independent browser references pixel for pixel. This is separate from complete complex-SVG equality; the current shader protocol is4. [Blend qualification](svg-browser-validation.md).

The gallery switches dark/light themes in its fixed header and displays the selected mode. Its 55 components are alphabetized within seven functional groups, with a Fluent vector icon on each group heading. Missing-component badges remain visible. [Navigation and launch](gallery-navigation.md).

`UiSvg.Background` and low-level `UiTextureUpdate.VectorBackground` initialize vector output targets. They default to transparent and do not propagate into internal groups, patterns or filters. Background participates in shared texture and GPU-output identity. Gallery **추가 샘플 → SVG → 브라우저 비교** exposes the switch. The matching shader protocol is4. Full browser equality remains open. [API](svg-widget-layout.md), [Qualification](svg-browser-validation.md).

Managed and GPU browser comparison consumers validate DPR1 and integer-pixel alignment for direct display. Preserved direct-display references currently violate that contract and are rejected before rendering. `svg-reference-contracts-only` checks12 input contracts; `gpu-svg-image-browser` checks the complete Canvas images of the two original documents. Both images still fail; this separate mode does not qualify direct display. [Reference contract and commands](svg-browser-validation.md).

The `svg-feature-composition-only` GPU qualification separates raw premultiplied texture, integer-over composition and the full browser image. It validates the observed browser renderer and reference hashes before GPU work and records them in the result. Its complete-image gate remains failing at96 pixels; rejected shader candidates are isolated and product shaders are unchanged. [Scope](svg-browser-validation.md).

`UiFluentCheckboxPalette.Resolve(context, state, value)` applies system checkbox colors and the context disabled state to Fluent Checkbox and Markdown tasks. [Scope and evidence](fluent2-checkbox.md).

Shared styled text uses CanvasText/LinkText/GrayText and Canvas code/header backgrounds, with palette-aware accessible paint caching. [Scope](system-forced-colors.md).

The shared context-aware `UiFluentInputPalette.Resolve` applies system contrast colors to input frames/text and shared adornments. Fourteen real-GPU Input contracts pass alongside existing input-family regressions; plain selection now uses HighlightText through disjoint shaped-run clips; stepper arrows now consume ButtonText/GrayText and ButtonFace through a dedicated palette; Menu consumes context-aware system colors and the optional post-row `IUiMenuPresentation.PaintFocus` overlay; Dropdown/Combobox now share separate Canvas/CanvasText/GrayText option roles and a post-row outer focus pass. Styled selection now shares background/content phases and system HighlightText. Committed selection around IME preedit retains HighlightText through shared source/display mapping. Composition-clause styling and actual OS transitions remain open. [Scope and evidence](system-forced-colors.md).

SVG circular-corner strokes share analytic outer/inner coverage; noncircular/dashed paths retain general GPU stroke work. Vector-scene shader protocol2 uses specialization ID1 independently of stack-capacity ID0; its marker is checked before module creation. Source and `vector_scene.comp.spv` must be delivered together. The actual old incompatible module is a hash-pinned regression fixture. Observer verification includes the document-features subtab. [Evidence and remaining differences](svg-browser-validation.md).

System contrast colors have an independent `IPlatformThemeProvider.ForcedColors` / `UiTheme.ForcedColors` contract. Applications respect them by default, including custom designs; `RespectSystemForcedColors=false` explicitly opts out. Windows notifications invalidate unchanged light/dark schemes too. This foundation does not qualify all component palettes. [Contract and limits](system-forced-colors.md).

SVG analytic fill metadata no longer bypasses stroke construction. Observer **SVG → 문서 기능** preserves stripes and radial gradients beneath rounded borders. [Regression and remaining browser differences](svg-browser-validation.md).

Native font icons preserve immutable closed contours through `PlatformTextGlyphMesh.Outline`, `PlatformTextGlyphOutline` and `UiVectorCurve`; DirectWrite/HarfBuzz curves share SVG cubic math and GPU fill. Zoom/rotation reuse prepared scenes without CPU raster requests. A fixed font-outline browser RGBA8-composition reference matches exactly; the original opaque official SVG baseline still differs. These are separate scopes. [Contract and current evidence](fluent2-rating.md#retained-native-curves).

The default restore runner also accepts an optional absolute test `-ValidationProject` plus `-ValidationArtifactsDirectory`. It restores that project first, then normal Linux and SVG defaults, with child-only HTTP cache configuration and vulnerability auditing retained. All additional build outputs can stay outside the repository.

`Fluent.Rating` provides typed float scores,half steps,RTL and wrapping through shared radio input/accessibility. `Fluent.RatingDisplay` is a noninteractive rating/review-count image with compact mode and localized numeric formatting. Both share shape/color painting. Observer exposes **평가**. Explicitly select `./run-gallery.ps1 -NoRestore` to reuse restored package assets. [API,verification and remaining scope](fluent2-rating.md).

Set `UiRatingOptions.ItemLabel` for accessible option names. Call `RefreshItemLabels()` when only external translation data changes; input/runtime identities remain stable. Shared font icons use outlines from `IPlatformVectorTextBackend` and the common SVG GPU coverage cache. MSAA1 neutral edges and cache reuse are checked; browser pixel equivalence remains incomplete.

After managed validation, invoke `scripts/RestoreManagedValidationDefaults.ps1` with an explicit HTTP cache and fresh log directory. It probes writable space and the SDK before restoring Linux then SVG and checking lock hashes. User cache settings and vulnerability auditing remain unchanged. [Command and scope](input-event-batches.md#restoring-default-locks-after-validation).

Ordered input regression passes595 checks, including40 for nested text, deadlines, animation visibility, removal and delayed GPU submission. Held-frame immutability and final-consumer release are verified separately from complete cache/platform acceptance. [Command and scope](input-event-batches.md#nested-metadata-and-delayed-frame-submission).

Input-only layer replay retains scheduling and parent text dependencies, then skips presentation command adjustment and frame/scene leases. Input regression passes555 checks, including84 GPU pixel pairs for content, clip, opacity, translation,3D transform, nested layers and texture updates. Time/FPS improvement is unmeasured. [Evidence](../artifacts/goal-audit-2026-11-16/cache-phase-verification.json).

Real button callbacks change margin, rotation, scale or scroll within an input batch before subsequent clicks and Input/Textarea/Markdown edits. The cohort passes345 checks, including84 new assertions across both traversal orders. [Scope and command](input-event-batches.md#event-to-event-widget-geometry).

Ordered input uses common Input/Presentation phases and builds one drawing on the final step. The Gallery enables `UiSliderOptions.CoalescePointerMoves` for SVG transform sliders. `UiCodeView` also collapses consecutive drag moves within the visible text, keeping the selection anchor and edge scrolling. [Input and presentation phases](input-event-batches.md#input-and-presentation-phases)

The App accumulates pending native input in arrival order and hands the list off once per UI drain. `app-ordered-input-only` checks sequence, ownership and storage allocation for a producer burst. [Input batching](input-event-batches.md#input-and-presentation-phases)

Drawing builders now transfer/consume Build output; return the container explicitly. Reset/discard returns only unpublished owned geometry, including channel outputs. Failed callbacks clean unfinished layer ownership and pending scene leases without consuming the caller's earlier frame. [Ownership contract](input-event-batches.md#unpublished-drawing-ownership). Common input/presentation phase activation is described above.

Use `UiContext.InputProfilingEnabled` and `InputProcessingMetrics` to distinguish preparation, UI composition and drawing finalization for one ordinary or batched Render. Immediate screens can enable the next call and inspect the previous call without requesting frames. Counts remain available with timing disabled. [Contract, limits and diagnostic](input-event-batches.md#cpu-processing-measurements).

Windows focus loss now emits key-ups for its owned held keys, preserving ordered events and retiring Core repeat deadlines. Native383 checks pass, including 20 focus-key lifetime checks. Actual keyboard/TSF and wider focus acceptance remain separate. [Contract and executable fixture](input-event-batches.md#windows-focus-loss-and-held-keys).

`UiKeyRepeatSettings.Source` selects engine timers or delivered platform repeats. Windows selects `Platform`; manual settings keep `Engine`. `UiState.KeyRepeatSource` exposes ownership, changing sources clears old deadlines, and platform mode rejects synthetic key timers. Core129 checks pass; focus/lifecycle and input-only CPU acceptance remain separate. [Contract and fixture](input-event-batches.md#repeat-ownership).

Windows key/character messages retain coalesced repetition. `UiKeyEvent.IsRepeat` distinguishes repeated down events; `repeat: false` excludes them. Complete Unicode scalars retain first-unit ownership/count, and repeated IME results are deduplicated. One native message retains one UI input step. [Contract, maximum-count fixture and limits](input-event-batches.md#native-repeat-counts).

Input event time is separate from animation/frame time. Native queued capture carries `MSG.time` through App; click chains and held-key timers consume `EventTimeSeconds`/`InputTimeSeconds` while host time advances once. Core129/native383 checks pass; input-only CPU composition and full keyboard focus/lifecycle remain open. [Clock contract, commands and limits](input-event-batches.md#event-time-and-frame-time).

Windows App and Observer enable native ordered input. Capture-time IMM strings, character ownership and per-step scale reach the real App/Core consumer; native routing/timing/repetition/lifetime passes 383 checks and native App integration passes 37. Owned input disposal calls the required `IUiImeHandler.ReleaseInput` contract; live hidden inputs keep late commits. Custom handlers must implement it. The low-level backend keeps an explicit legacy default for aggregate-only hosts. Actual keyboard/TSF service acceptance and input-only CPU optimization remain open. [Capture commands](input-event-batches.md#windows-native-ordered-capture), [lifetime contract](windows-ime-routing.md#removed-and-hidden-inputs).

The main Windows window, modal windows, and modeless windows now use one backend configuration for ordered input. In the ordinary Gallery, posting Left and a character in one burst inserts the character at the moved caret. The SearchBox value and its displayed C# example also stay synchronized after clearing and navigating away and back.

The same posted Win32 input sequence passed on Windows .NET 11 managed through `ShowModal`, `ShowModalAsync`, and `ShowModeless`. The checks covered the SearchBox clear button, Korean and Latin characters, and a Left-then-character burst. Each verified window closed normally.

The Linux X11 backend now captures one ordered step per event that changes input. Each step carries its own key transitions, committed characters, button edges, wheel delta, and capture-time pixel scale. Verify with `Duxel.Linux.App.Tests x11-ordered-input` on an available X11 display. The fixture sends events only to its own hidden window.

With the Linux Gallery running, the external test command `x11-gallery-search-input <gallery-pid> <fresh-result.json> <x11-window-id>` verifies a burst of X11 keys in SearchBox. It reads the text and caret back through AT-SPI and checks that the edit survives leaving the page and returning. Supply the X11 window ID of the test Gallery instance as a decimal number.

Use `x11-gallery-pixels` with the same arguments to save two 1280 × 900 RGB snapshots each of SearchBox, Overview, and RadioGroup. The probe requires the whole gallery client rectangle to fit on the X11 screen. Compare both snapshots from each build to identify pixels that can change between captures.

The .NET 11.0.0 Linux ARM64 Gallery cross-published on Ubuntu x64. Under QEMU with Xvfb and lavapipe it registered AT-SPI and created a Vulkan swapchain. Both probes timed out before confirming a displayed frame. ARM64 hardware and full rendering remain unverified.

In WSLg, two RGB captures each of Overview, SearchBox, and RadioGroup matched between managed and NativeAOT outside the performance readout. All differing pixels in the full captures were in the FPS and renderer-counter text.

Fresh .NET 11.0.0 builds also match 105 ordered Gallery presentation states. The independent client passes 18 checks per runtime across SearchBox, Tree and the virtual grid. [Linux Gallery validation](linux-platform.md#gallery-on-net-1100)

The external `x11-gallery-fps` mode checks FPS refresh and event-wait behavior through the visible Gallery label and app frame counts. [Linux FPS validation](linux-platform.md#gallery-on-net-1100)

The external `x11-gallery-drawer` mode checks modal focus, retirement, reopened editing state and Save in both Gallery runtimes. [Linux Drawer validation](linux-platform.md#gallery-on-net-1100)

The Linux ARM64 diagnostic Gallery times out at the first graphics fence under QEMU/lavapipe at both 4× and 1× MSAA. [ARM64 diagnostic](linux-platform.md#arm64-first-frame-diagnostic)

The ARM64 diagnostic Overview presents all 130 direct draws with off-screen work omitted. Restoring vector-scene recording reproduces the fence timeout. [ARM64 capture and pass comparison](linux-platform.md#arm64-first-frame-diagnostic)

The ARM64 QEMU comparison completes transfers without compute and times out with compute even when result copies are omitted. Both stroke recording and isolated single-command fills reproduce the timeout. [ARM64 vector compute diagnosis](linux-platform.md#arm64-first-frame-diagnostic)

The same ARM64 diagnostic dispatches complete with minimal shaders including buffer-address loads and stores. Both the original retained-scene shader and its optimized rebuild time out. [ARM64 shader comparison](linux-platform.md#arm64-first-frame-diagnostic)

The ARM64 QEMU diagnostics present with scene setup and a bounds-only loop. Retaining layer-stack compositing reproduces the timeout even with constant fill color. [ARM64 layer compositing comparison](linux-platform.md#arm64-first-frame-diagnostic)

An ARM64 QEMU guest core places signal 11 in executable code called indirectly by lavapipe. A matched syscall-traced run completes two diagnostic presentations with vector copies omitted. [ARM64 fault analysis](linux-platform.md#arm64-first-frame-diagnostic)

An ARM64 marker control writes a known color through compute and displays it only when vector copies are enabled. The original vector scene still fails under the matching copy workload. [ARM64 copy and presentation evidence](linux-platform.md#arm64-first-frame-diagnostic)

The ARM64 guest core identifies the JIT invocation for the 16×17 fill. Cortex-A72 and Cortex-A76 emulation both reproduce the full-vector failure before presentation. [ARM64 dispatch diagnosis](linux-platform.md#arm64-first-frame-diagnostic)

The current Linux x64 NativeAOT build on .NET 11.0.0 completed all 105 WSLg presentation scenarios and three external SearchBox checks. It preserved the same scenario order and reported UI state as the managed build. SearchBox keeps the caret and input order when X11 sends multiple keys together.

`UiContext.NewInputBatch` consumes ordered complete input states and produces one final drawing. Core/editor/button/GPU checks and the connected App transport are separate from native producer integration. [Contract, executable fixture and limits](input-event-batches.md#ordered-core-input-consumption).

Replacing an unclaimed drawing preserves texture commands and exclusive pixel ownership for the next frame. Claimed frames remain exclusively consumer-owned. [Lifetime contract, GPU comparison and command](input-event-batches.md#unpublished-drawing-resource-lifetime).

Injected key/character batches expose their active count while reusing storage; smaller batches cannot replay stale tails. [Contract, regression commands and retained legacy ordering comparison](input-event-batches.md).

Textarea and Markdown persist final cursors for late inactive IME commits. Actual Input/Textarea/Markdown widgets are checked together with queued completion dispatch. [Focus integration and acceptance scope](windows-ime-routing.md).

Windows IME focus changes dispatch session-checked completion on the owner thread. The next input's candidate placement waits for the prior composition to end; stale positions and failures cannot target current input. [Behavior and actual-service acceptance limits](windows-ime-routing.md).

Windows character messages publish only complete Unicode scalars. A split surrogate pair retains the first unit's input across frame/focus changes, with shared lifecycle retirement for raw input and IME. [Boundaries and validation scope](windows-ime-routing.md).

Windows IME preserves both a committed result and the next preedit in a combined message. Attribute-only updates do not reread strings; cancellation and result-only updates clear prior preedit. [Behavior and validation scope](windows-ime-routing.md).

Overlay closing retires accessibility nodes and deferred Label relationships together. See the [acceptance review](acceptance-status.md) for the Observer close/reopen regression and remaining original requirements.

Windows IME pins each composition to its starting input, hides it from other inputs, and routes late results to the original widget. Inactive single-line commits also persist their cursor. [Message-level scope and remaining native protocol work](windows-ime-routing.md).

Single-line characters, paste, and IME commit share insertion-edge completion, including selection replacement and length limits. Source text, visual caret, and accessibility coordinates use the accepted insertion. [Scope](windows-text-shaping.md).

Single-line IME commit preserves the native trailing caret edge, and composed dragging uses the full display sentence width. Inspect commit and selection through **입력·IME → 한 줄 커서 예제**. [Validation scope](windows-text-shaping.md).

Display DPI and OS text size use separate platform scales. Windows follows native accessibility text scaling; Linux X11 follows its own XSETTINGS/X resources and desktop font size. Raster glyph bitmaps honor physical `PixelSnap`, including fractional scrolling. See [system typography](system-typography.md) for precedence, validation commands, and runtime limits.

`run-gallery.ps1` separates the Windows incremental build directory from runtime generations, allowing a new launch while an earlier window holds loaded DLLs. `Observer runtime` identifies the executable copy.

Single-line inputs reuse native geometry for pointer, caret, selection, scroll and accessibility coordinates. IME display strings retain whole-sentence context and trailing caret positions. Windows switch alignment uses actual raster ink bounds. Inspect **입력·IME → 한 줄 커서 예제** and the header switches. Automated OS IME interaction and fresh Linux native qualification remain separate. [Detailed scope](windows-text-shaping.md).

Markdown HTML entities retain their entire source spelling as one caret unit, independent of decoded length. The active editing block reveals source syntax while reusing the parsed document. Native paragraphs retain line boundaries that do not divide an expansion. Inspect **Markdown → 문단 조판 예제**. [Scope and limits](styled-paragraphs.md).

Mixed Markdown documents compose native paragraph slices around table grids, preserving original source offsets, active source reveal, blank rows, pointer/selection geometry and image bounds. Rendering does not reparse the slices or normalize away fractional native glyph origins. Remaining single-line input, source-expansion and whole-editor conformance work is separate from this Windows qualification. [Scope](styled-paragraphs.md).

Markdown table cells share native paragraphs through immutable source slices. Paint, RTL carets, selection, column traversal and image accessibility bounds share those allocations; column sizing and horizontal overflow remain intrinsic and unwrapped. The Observer adds **표 셀 예제**. Mixed documents also share paragraph slices around the grid; remaining input paths and fresh Linux qualification are still open. [Scope](styled-paragraphs.md).

Markdown tasks, quotes and thematic breaks now share native paragraph layout in table-free documents. Image/task source atoms share one boundary module; task objects use Fluent paint. Added 11 focused checks and 80 exact GPU comparisons, and the Observer exposes **구조적 문단 예제**. Table cells and surrounding paragraphs share native layout; fresh Linux runtime qualification remains open. [Current scope](styled-paragraphs.md).

Multiline/Markdown plain arrows and held arrows now share physical native character-edge navigation with bidi affinity; same-row selection collapse and atomic image/soft-wrap boundaries follow the same geometry. Source-order editing, Ctrl+words and single-line input navigation remain separate. See [the qualified scope and remaining limits](styled-paragraphs.md).

Ordinary Markdown images now share native paragraph object layout, RTL pointer geometry and raw source boundaries. Seven focused source checks and 48 independent native GPU image comparisons pass on Windows; the actual Observer verification includes the **이미지 예제** button (42 scenarios). Linux source compiles, but fresh native runtime qualification remains pending. [Scope and evidence](styled-paragraphs.md).

The Observer MessageBar page uses a naturally measured column inside `UiScrollView`; content taller than the tab panel scrolls instead of overflowing its finite height. Rechecked on 2026-11-16 at the reported 707.4625px height and at 240px, with 34 page checks and 41 actual Observer presentation scenarios passing. See [the crash verification](../artifacts/fluent-messagebar-2026-11-15/messagebar-crash-recheck.json).

Low-level native paragraph object requests declare source U+FFFC positions, size and baseline and return real native allocations on Windows. Existing constructors remain available. Owned-widget allocation and base direction are connected. Linux code is connected but fresh runtime qualification is pending. See [the object contract and platform limits](styled-paragraphs.md).

Text-only owned inline flows and MessageBar bodies share the native paragraph while preserving actual child accessibility identities. Atomic-widget flows share native allocation and base direction. Fresh Linux runtime qualification remains open. See [scope and examples](inline-text.md).

Text-only Markdown shares native paragraph wrapping, carets, selection and paint. Hidden markers retain original source positions, and Fluent logical line heights remain authoritative. Structural image, table, task, quote and owned-child integration and new Linux qualification remain open. See [the paragraph contract and qualification scope](styled-paragraphs.md).


Literal `UiInlineText.RightToLeft` now reaches the common native request,invalidates on change and retains on equal assignment. Windows70 checks/128 exact native GPU pairs/40 actual scenarios including the live Typography switch pass. Linux source is connected but its new NativeAOT build failed on Ubuntu filesystem I/O;even `/bin/true` cannot execute. Keep previous `e529777d` selected. Do not rerun the Linux build until the environment is recovered. New task outputs use `W:/Cache/Duxel/literal-direction-20261116` because L: is full. [API/status](inline-text.md).

Latest Linux styled-paragraph delivery is `e529777d`, superseding earlier delivery records below. NativeAOT styled351/original334/text114 and actual Observer41/delivered-launcher38 pass. Linux now implements the same paragraph capability as Windows literal `UiInlineText`; Markdown/atomic/UI-direction/navigation integration remains open. Windows73 source fingerprints17/17 are unchanged and its qualification is reused. [Evidence](../artifacts/fluent-messagebar-2026-11-15/linux-styled-paragraph-verification.json).

The [styled paragraph snapshot](styled-paragraphs.md) is implemented by Windows and Linux and consumed by literal `UiInlineText`. The Typography Observer includes scrollable mixed-weight, cross-font and bidi samples. Markdown projection, atomic inline children and UI base-direction selection remain open. Older qualified binaries are preserved separately; API availability is not whole-product qualification.

Current native line geometry is shared by Windows and Linux uniform-style rows. Windows now preserves complete bidi context across explicit font intervals;16 independent native pixel/vector/caret/Core checks and all related regressions pass. Linux retains its native334/text114 qualification; current shared Observer delivery `7746d0b7` passes41 candidate/38 delivered scenarios. The Input/IME example includes an RTL embedding spanning Korean and Hebrew fonts. Full mixed-style paragraph composition, base-direction/wrapping integration and physical latency remain open. See [Windows shaping](windows-text-shaping.md), [Linux launch](linux-text-shaping.md) and [current evidence](../artifacts/fluent-messagebar-2026-11-15/paragraph-font-context-verification.json).

Remaining work: complete mixed-style paragraph composition, explicit base direction and wrapping integration. The four original Windows prefix-width caret failures and nine Linux native-pointer failures are fixed for the qualified uniform-style rows. Test counts do not complete the whole RTL contract.

Previous qualification (2026-11-16):34 bounded MessageBar page checks and38 actual Windows scenarios passed; empty shaping/zero advance contracts52 passed. Delivery `798c1472` is historical and superseded by the current manifest. [Earlier evidence](../artifacts/fluent-messagebar-2026-11-15/zero-advance-verification.json).

Current MessageBar authority: [latest contract](fluent2-messagebar.md), superseding historical counts below. A scroll view fixes constrained-height termination. Passed38 Windows,41 Linux NativeAOT and38 delivered scenarios. Shared `UiInlineFlowWidget.HorizontalAlignment` Left/Center/Right applies identical per-line offsets to paint and actual child/span positions. MessageBar also aligns its actual body with LTR/RTL. Passed155 CPU checks and176 GPU pairs; full RTL remains incomplete, the six existing Windows shaping failures are fixed through shared DirectWrite shaping (37 expanded checks passed). The Linux manifest selects `c8246d`. The original Windows NativeAOT gate remains separate.

Keyed groups use `UiKeyedWidget` and `SetItems` for real insertion,removal and reordering. Constructor `keys:` establishes stable ownership. Validation precedes immediate adoption;the next prepared frame applies traversal and completed exits physically dispose children. A live/exiting key retains its instance. Passed48models,15viewport checks,587related regressions,424GPU pairs and36Windows/39Linux scenarios. Actual native-client and whole MessageBar conformance remain pending. Review the explicit `tests/Duxel.Graphics.Tests/Fixtures/FluentFactoryInventory.json` API contract when factories change. [Contract](fluent2-messagebar.md).


MessageBarGroup and shared PresenceGroup own controlled child visibility,GPU exit paint,reflow and focus retirement. Use `SetVisible(index, bool)`. Initial content does not animate;ReducedMotion and finite idle scheduling are supported. See the latest dynamic collection contract and remaining conformance scope. Passed17models,140GPU pairs and32Windows Observer pages. [Contract](fluent2-messagebar.md).


Shared `UiInlineFlowWidget` owns real text spans and widget children. `Fluent.MessageBarBody` and `MessageBarTitle` retain continuous shaping,real links/inputs,and measured Margin/Transform extents. Passed31model checks,340existing regressions and152GPU pairs. Full Group API conformance,automatic announcements and whole MessageBar remain incomplete. [Contract](fluent2-messagebar.md).


2026-11-15 Fixed Windows startup by mapping the new MessageBar factories to an actual **메시지 표시** demo page while retaining the startup catalog gate. Run `./run-gallery.ps1`; `./run-gallery.ps1 -Verify` presents all 32 pages and exits. Separate process logs are retained under `%LOCALAPPDATA%/Duxel/ImplementationObserver/logs/`.

Corrected the shared Linux font descent sign, fixing vertical alignment for buttons, badges, checkboxes, radio labels, spinners, body text, input and spin buttons. Restart the old window using `& ./artifacts/linux-present-path-2026-11-15/run-observer.ps1`. Validation passed 25 native metric cases, 32 widget/caret pixel comparisons and 35 NativeAOT presentation scenarios. This change does not establish a performance improvement.


2026-11-15 [MessageBar in progress](fluent2-messagebar.md): added the physical root, owned wrapping actions, official four-intent Light/Dark palettes, icons, and Rounded/Square shapes. The 45 model checks and 64 independent GPU pixel pairs pass. Current source exposes 64 factories with 38/55 parents connected and 17 missing; connection is not full conformance. Owned titles, groups, motion, and Observer integration remain pending, and Linux delivery is unchanged.

2026-11-15 [Mixed-weight inline text](inline-text.md): shared `UiInlineText` provides actual 600/400/700 faces, retained wrapping, and one passive accessibility root. The 32 model checks, 64 existing text checks, and 32 independent GPU pixel pairs pass; the official Fluent catalog and protected Linux delivery remain unchanged.

2026-11-15 [accessibility announcements](accessibility-announcements.md): frame-local announcements independent of visual nodes/focus passed actual Windows UIA and pinned Linux NativeAOT delivery checks. MessageBar visuals and full accessibility remain incomplete.

2026-11-15 [standalone text accessibility](fluent2-text.md): distinct equal-text identities, actual layout bounds, single source reads and native content updates are connected. The actual Observer/launcher are verified and current; overlapping performance ranges are inconclusive and prior executables remain preserved.

2026-11-15 [Skeleton](fluent2-skeleton.md): added loading scopes, Wave/Pulse, inheritance, static motion, native busy semantics and the Observer Skeleton tab. The compiled catalog has62 factories,37/55 connected parents and18 missing; connection counts do not establish full conformance.

## Latest Linux CPU worker qualification

Three independent pairs of the identical RC3 NativeAOT executable show37.60–38.51% lower CPU in three scenes when llvmpipe raster workers change from the default24 to4. MSAA,fonts and damage area are unchanged. Slots1/3 pass7,140 checks and1,216 raw-image pairs exactly match the default-worker runs. The qualified launcher applies this only to its launched process. Source execution can select `-SoftwareRasterThreads 4`; omission retains the driver default. See [launch,scope and remaining limits](../artifacts/linux-driver-worker-budget-2026-11-15/README.md). WSL hardware execution remains unapplied because a mandatory composition feature is missing.

## Small edits inside Linux batches (2026-11-15)

Common Vulkan damage compares changed streamed analytic records and moving edges of constant-style fills. Stationary marks no longer enlarge damage; clipping, textures, masks, resident geometry and acquired-image/MSAA histories retain conservative contracts. Median repair area falls92.36% for the isolated slider and46.67–48.68% for Fluent. Only frozen Fluent CPU reduction11.54% has separated ranges; other CPU/input comparisons support no speedup claim. Linux RC3 NativeAOT7140 checks, Windows7108 checks and2424 raw-pixel pairs pass. [Latest direct launch, scope and evidence](../artifacts/linux-sparse-damage-2026-11-15/README.md). Whole-goal and physical display latency remain separate.

## Shared overlay lifetime and Dialog status (2026-11-15)

Dialog supports the `Open` binding or `Show()`/`Close()` with default250ms entry/exit motion; disable with `MotionEnabled: false`. Closing retires input/accessibility and replays frozen paint. Reopen/hide/disposal manage finite ownership and deadlines; shared coordinates map input/IME. Independent acceptance is11/12. Windows managed UIA and opened Linux RC3 NativeAOT Dialog pass; Windows RC3 NativeAOT and full integration remain. [API, launch and evidence](../artifacts/fluent-dialog-2026-11-15/README.md).

## Stationary button holds and event-driven idle (2026-11-15)

The shared application loop compares button transitions, pointer movement and press/release events. A stationary held button alone no longer forces infinite UI builds. Explicit frame requests, animation deadlines, text autoscroll and SpinButton repetition remain active. The Linux Observer's `--verify-idle-pointer <fresh output directory>` checks three screens × three held buttons without periodic probe frames,24 paced moves and scheduled numeric repetition, then closes its owned window. On this PC, launch the verified executable with `./artifacts/linux-held-input-idle-2026-11-15/run-observer.ps1`. Final NativeAOT passed32 pages and384 inputs; Windows managed validation passed a614-frame application session and415 composite contracts. These are not whole-goal acceptance or physical-display latency claims. [Execution and measurement scope](../artifacts/linux-held-input-idle-2026-11-15/README.md).

## Actual compiler selection and declaration removal (2026-11-15)

The generator fixture now records its API reference version, actual Roslyn assembly versions/commits, runtime mode and load context separately from the .NET runtime. Default tests continue to compile and run with the pinned 4.10 package. `-p:DuxelUseSdkRoslyn=true` executes the generator driver with the selected SDK's actual compiler assemblies. `DuxelSdkRoslynDirectory` explicitly overrides their directory; otherwise the single path comes from `MSBuildToolsPath/Roslyn/bincore`. Invalid mode and missing selected files fail before build with DUXTEST001/002/003.

SDK mode copies the selected assemblies into `SdkRoslyn` and loads the fixture in an isolated collectible AssemblyLoadContext while sharing public Duxel.Core contracts. This supports the Linux SDK's ReadyToRun compiler images without treating them as portable compile references or modifying them. API compilation stays on4.10; the compiler running the generator is explicitly selected. Missing SDK runtime files fail even when the package compiler is present. The fixture is managed and uses dynamic compilation; it is not a NativeAOT runtime test.

Final profiles each pass131 checks: Windows .NET11/package4.10, Windows .NET11/SDK Roslyn5.12, and Linux pinned SDK11.0.100-rc.3.26522.148/runtime11.0.0-rc.3.26522.148 with its own Roslyn5.12 build. Six generated source files are byte-identical to the previous117-check baseline in all three profiles. No product generator implementation, runtime widget or dependency was changed.

The removal matrix observes both same-file declarations and independent syntax trees. Removing the first/middle/last declaration reuses0/1/2 of the two remaining outputs in every profile. Functional type removal and unchanged source bytes pass, but the original first-removal2/2 cache criterion remains incomplete. Splitting declarations into separate files did not meet it. The package's exact Roslyn source commit is `2b7d172669b2f7e55803b55f317cfcc2d4279d76`; its [state-table implementation](https://github.com/dotnet/roslyn/blob/2b7d172669b2f7e55803b55f317cfcc2d4279d76/src/Compilers/Core/Portable/SourceGeneration/Nodes/NodeStateTable.cs#L309) compares corresponding positions. This is a verified limitation of the current path, not a completed optimization or a claim that all possible redesigns are impossible.

Use the existing generator test publish/run command with `-p:DuxelUseSdkRoslyn=true` to select SDK mode; omit it or use `false` for package mode. Select the desired SDK through the working directory/global.json or an explicit dotnet executable, publish into a fresh directory, then run `dotnet exec <published Duxel.Generators.Tests.dll> <fresh result directory>`. Results include `icon-removal-matrix.json`, `icon-incremental-limits.json` and `core-catalog.json`. The current compiled draft catalog exposes59 factories, with35 of55 required parents connected and20 missing; these connection counts are not complete Fluent conformance. Evidence: `artifacts/generator-compiler-selection-2026-11-15/verification.json`.

2026-11-15 Linux accessibility diagnostics add text-attributes-native and probe-failure-contract. Each session path passes48 focus/boundary/attribute checks; typed/source-generated JSON preserves primary managed client failures in the parent. This is not a full-suite retry or historical root-cause completion. [Scope](linux-platform.md).

The Linux launcher supports `PublishedExecutable` and `SampleArguments`: run the explicitly selected build without SDK publishing and reject missing files without selecting another build. Launcher-path NativeAOT verification passes32 screens and384 input changes. The first Linux sample guide command selects this PC's verified NativeAOT build; publishing current managed source is documented separately.

2026-11-15 pinned.NET11 RC3 Linux NativeAOT Observer now has a matched-RC3 managed comparison. Three alternating pairs measured Fluent first composition134.66→31.79ms and native-drag UI composition0.678→0.257ms. Linux fixtures use explicit result types and source-generated JSON to run the same contracts and full raw-image comparisons in both runtimes. This does not replace Windows AOT or whole-widget pixel qualification. [Launch and scope](../samples/Duxel.Gallery.Linux/README.md).

2026-11-15 Linux native input and Vulkan presentation now use separate X11 connections, removing long input stalls caused by sharing one connection. Observer `--verify-pointer-performance <fresh output path>` exercises actual X11 drag input with accessibility enabled in three screens, each with96 warmup/32 measured changes. Three alternating comparisons recorded150→0 delays above50ms across288 measured samples. All32 screen presentations and clean exit also pass. [Current launch guide](../samples/Duxel.Gallery.Linux/README.md).

2026-11-15 shared SVG paint matches the reference browser Graphite linear-position stabilization. Shadows pass CPU9/9/GPU white9/9/transparent9/9; small gradients8/9 and groups8/11. Whole equality remains incomplete. [Current qualification and launch](svg-gpu-camera.md).

2026-11-15 SVG RGB/alpha browser capture uses one explicit channel option. Four real recapture hashes match and three preflight checks pass. Shadow alpha4/9 and whole-browser equality remain incomplete. [API and validation](svg-gpu-camera.md).

2026-11-15 single-paint geometric-clip opacity folding is corrected. Managed271 checks pass and clip differences fall951→87 pixels; whole-browser equality remains incomplete. [Validation and latest sample](svg-gpu-camera.md).

2026-11-15 GPU transparent-texture comparison and layer color precision are available. Managed268, GPU readback9 and Linux presentation32 pass; whole white0/4 and transparent0/2 equality remains incomplete. [Execution, formats and limits](svg-gpu-camera.md).

2026-11-15 SVG comma-form RGB byte resolution and source-storage half ties are corrected. Managed265 checks,32,895 independent color inputs and three new GPU SVGs pass. Whole artwork remains0/4, small gradients2/9 and six raw RGBA cases5/6; straight-alpha RGB readback remains open. See the [guide](svg-gpu-camera.md) for `gpu-color-storage`, `--browser-color-storage` and current Windows/Linux builds.

2026-11-15 SVG compilation folds single-draw opacity into paint to retain color precision and remove unnecessary group composition. Overlap, effects and nested storage boundaries remain intact; two independent single/nested solid RGBA references match. The `gpu-opacity-pixels` profile diagnoses11 separate cases without qualifying whole-browser equality. [GPU camera guide](svg-gpu-camera.md).

2026-11-15 Shared SVG opacity passes66 independent browser alpha cases plus191 managed regressions. Gradient precision changes retain the GPU camera path and reduce whole Canvas differences, but full browser equality remains0/4 and small gradients1/9. New Windows/Linux review builds and commands are in the [GPU camera guide](svg-gpu-camera.md). This does not qualify zero CPU use or complete pixel conformance.

2026-11-15 The gpu-svg-browser profile compares full-resolution GPU pixels against independent browser references; currently0/4. GPU half-channel rounding is corrected and five browser viewport/capture-consumer checks reject invalid references. [Verification and limits](svg-gpu-camera.md).

2026-11-15 SVG strokes and dash metrics/phase/slices/caps/joins are generated on GPU for the current camera. The original48 effects,45 additional strokes and45 dash-boundary comparisons pass independent scalar pixels. Full browser equality remains incomplete. [GPU camera](svg-gpu-camera.md).

2026-11-15 [SVG GPU scene and internal camera](svg-gpu-camera.md): widgets retain vector data on GPU and render only the visible camera window. No CPU widget raster fallback; explicit bitmap export stays separate. Complex pixel conformance remains in progress.

2026-11-14 Portable Observer: shared 29-page UI with host-injected image decoding/SVG fonts; Linux X11/Vulkan page presentation and clean shutdown verified. Global top-right repaint highlighting defaults to off. [Linux launch guide](../samples/Duxel.Gallery.Linux/README.md).

2026-11-14 [Repaint observer](repaint-observer.md): Fluent header switches and a separate **갱신 관찰** tab expose actual Vulkan repair outlines, fixed/changing panels and optional 60Hz text changes. Measurement reuse remains a layout-specific tab. Diagnostic cleanup is counted separately; disable highlights for performance comparisons.

2026-11-19 [Repaint observer](repaint-observer.md) restores old diagnostic outlines in border strips. Vulkan reports their separate pass and pixel counts as `overlayStrips` and `overlayPixels`.

2026-11-19 Vulkan submits drawings immediately after texture-only uploads on the same queue. `upSched(... frameWaitUs=...)` isolates the CPU upload fence wait immediately before graphics submission. The fence still guards staging and command-buffer reuse.

2026-11-19 The Gallery SVG pointer benchmark accepts `win32 highlight` after the tab mode. It checks messages posted to its own window and records the time until the matching Vulkan presentation. [Measurement scope and command](optimization-session-2026-11-19.md#win32-pointer-message-to-vulkan-presentation).

2026-11-19 Wrapped `UiInlineText` retains up to two layouts for the full and scrollbar-reserved widths. The layouts still invalidate on source or typography changes. [Measurement results](optimization-session-2026-11-19.md#reusing-the-two-widths-of-a-wrapped-description).

2026-11-19 The full-source benchmark uses the actual Markdown Gallery code viewport and records visible lines, source hash and viewport coverage. The regular benchmark uses Breadcrumb. [Measurement scope and command](optimization-session-2026-11-19.md#code-dominated-gallery-viewport).

The repaint outline shows the actual scene repair rectangle, including reconstruction of older swapchain images and MSAA history. CodeView scrolls reuse accessibility text geometry when the source and layout stay the same, and native paragraph painting visits only the glyph runs of the visible lines.

2026-11-13 [Popover triggers](fluent2-popover.md): one Click/Hover/Context mode, 500ms close scheduling/cancellation and rejection quiescence, virtual point placement, Menu/Shift+F10. Observer → Popover → 팝업 열기 방식.


2026-11-13 [Popover arrows and surface cache](fluent2-popover.md): WithArrow, 6/8px geometry, combined shadows, integral-movement/scale caching and pointer occlusion reuse the existing Tooltip raster. Full conformance remains incomplete.


2026-11-13 [Popover](fluent2-popover.md): owned arbitrary content, controlled/rejected opening, nonmodal/trapped focus, Escape restoration, natural height/scrolling, nesting, hiding and disposal. Observer → Popover. Catalog: 56 factories, 32/55 top-level connected, 23 missing; full conformance remains incomplete.


2026-11-13 [Popup input foundation](popup-input-modes.md): optional NonModal BeginPopup separates pointer occlusion, keyboard isolation and Card focus-group layers. Observer → 팝업 입력. Use the Fluent Popover API above for owned content.

2026-11-13 [Toolbar](fluent2-toolbar.md): owned ordinary controls, horizontal/vertical/RTL layout, remembered single Tab entry, circular arrows and native UIA orientation. Observer → 도구 모음. Nested Toolbars/full Tabster conformance remain open.

2026-11-13 [SearchBox](fluent2-searchbox.md): shared editing/IME with search/clear, focus return, readonly, authoritative binding readback and native search semantics. Observer → 검색 입력. Icons use Fluent fonts.

2026-11-13 Persona connects person information with six sizes, logical text positions/alignment, owned Avatar/presence and four text slots. Shared consumer-owned avatar paint resources also back Tag media. Observer → 인물 정보. [API](fluent2-persona.md).

2026-11-13 Tag/InteractionTag now accept borrowed `UiTagContent.Media` via `UiTagMedia.Avatar`/`Image`; TagGroup has a once-per-option media selector. Each tag owns its own composite/shadow binding. Observer → 선택 목록 → 담당자 미디어. [Usage](fluent2-tag.md#borrowed-avatar-and-image-media).

2026-11-13 TagGroup accepts optional `emphasized : UiBinding<string[]>`, independent of visible `selection`. Primary Toggle changes emphasis; × removes visibility and retains hidden emphasis for restoration. No activated callback is required. Observer selection tags demonstrate both bindings. [API](fluent2-interaction-tag.md#independent-group-emphasis).

2026-11-13 InteractionTag now supports controlled `Selected : UiBinding<bool>` and visual-only `UiTagOptions.Selected`. Binding getter readback governs same-frame paint/UIA Toggle; secondary Invoke never changes emphasis. Observer → 선택 목록 → 강조 선택 includes rejection controls. See [InteractionTag](fluent2-interaction-tag.md).
2026-11-13 InteractionTag: independent primary/× actions, two-action group navigation and shared selection. Compare **선택 목록 / 확인형 태그** with basic tags. [API](fluent2-interaction-tag.md).

2026-11-13 tag accessibility: remembered focus on reentry, focus transfer after final removal, literal ## names and Windows Toolbar semantics. [Usage](fluent2-tag.md).

2026-11-13 Tag: dismissal, shared selection bindings, wrap layout, keyboard navigation and SVG. Run `./run-gallery.ps1` → **선택 목록 / 선택한 기능 태그**. [API and remaining scope](fluent2-tag.md).

2026-11-13 selection validation: standalone Invalid options reach native ComboBox/editor validation properties. Inspect **선택 목록 / 검증 상태 전환**. Context transfer, disposal and parent allocation regressions: `artifacts/combobox-lifetime-2026-11-13/`.

2026-11-13 Combobox follow-up: dismissal/blur reconciliation, authoritative Open binding acceptance/rejection, actual OpenChanged transitions and readable empty-result text. [API](fluent2-combobox.md). Sample **선택 목록 / 도시 목록 열림 전환**.

2026-11-13 Combobox: actual editor/IME/undo integrates with filtered popup selection while preserving hidden selections/provider IDs. Run `./run-gallery.ps1` → **선택 목록 / 도시 검색**. [API and limits](fluent2-combobox.md).

2026-11-13 Dropdown: bound single/multiple selection, persistent options, native Selection/Value, keyboard search and shared popup scrolling. `./run-gallery.ps1` → **선택 목록**. [API and limits](fluent2-dropdown.md). Use [Combobox](fluent2-combobox.md) for editable input; full conformance remains open.

2026-11-13 AvatarGroup: Spread/Stack/Pie, exact overflow counts, member menus, RTL and image/Korean/SVG content reuse existing Avatar, clip and Menu machinery. Run `./run-gallery.ps1` → **아바타 그룹**. [API and limits](fluent2-avatar-group.md). Catalog48 exposed/25of55 top-level connected/30 missing; complete conformance remains open.

2026-11-12 Breadcrumb: `Fluent.Breadcrumb` provides responsive overflow, Tab/arrow navigation, RTL, disabled focus, text-element Tooltips and native navigation landmarks. Run `./run-gallery.ps1` → **경로**. Default icons use the Fluent font; separately owned SVG icons can also be supplied. [API and limits](fluent2-breadcrumb.md). Evidence: `artifacts/breadcrumb-2026-11-12/verification.json`. Catalog47 exposed/24of55 top-level connected does not mean complete conformance.

2026-11-12 Card selection: `Fluent.Card(..., selection: binding)` / Selection connects the real model. Verified Checkbox Tab/Enter/Space/Toggle events, independent children, disabled/removal behavior and external-model ancestor relayout/cache reuse. Observer 카드 → 카드 선택 모드 전환. CPU55/8485,GPU12/2433,nativeCard20/Avatar23. Dedicated slots/full conformance remain open. [API](fluent2-card.md); `artifacts/card-selection-2026-11-12/verification.json`.

2026-11-12 Avatar corrections: automatic 정세일→정 including decomposed Hangul, name-derived Korean preset, preserved font bitmap OriginX fixes clipped status-circle right edges. Ordinary text/vector placement and parent clips remain unchanged. CPU55/8402,GPU12/2289,nativeAvatar23/Card14. Card selection work remains unqualified. [Details](fluent2-avatar.md); `artifacts/avatar-korean-presence-2026-11-12/verification.json`.

2026-11-12 Card root actions: `Fluent.Card(..., activated: callback)` / `Activated`, automatic focus (null means NoTab with callback, Off otherwise), native Group Invoke, independent child input, pointer cancellation, pinned hover/pressed colors and reserved Shadow8. `IUiCardPresentation.Paint` now receives interaction. Observer 카드 → 카드 루트 동작 전환. [API](fluent2-card.md); `artifacts/card-action-2026-11-12/verification.json`.

2026-11-12 Card focus: `UiCardOptions.FocusMode` Off/NoTab/TabExit/TabOnly, root-only `Disabled`, real Tab/Enter/Escape, child input, native UIA focus and the common GPU outline are available. Run `./run-gallery.ps1` → 카드. Full conformance, selection and dedicated slots remain incomplete. [API](fluent2-card.md); `artifacts/card-focus-2026-11-12/verification.json`.

2026-11-12 Card foundation: `Fluent.Card(label, content, options)` owns a persistent content tree and reuses common box measurement, analytic surfaces, shadow leases and passive native Group semantics. Observer **카드** exposes real input/button and all12 default size/appearance combinations. CPU55/8309, GPU1557, native Card9+Avatar23 and883 previous scene images pass. Catalog43 exposed/23top-level connected/32missing; full Card selection, managed focus groups and dedicated slots remain required. [API and limits](fluent2-card.md); `artifacts/fluent-card-2026-11-12/verification.json`.

2026-11-12 Inactive Avatar: `UiAvatarOptions.IsInactive` uses one owned final-resolution paint group at visual scale0.875/opacity0.8, hides activity ring/shadow and preserves one passive Image provider. `UiMenuIcon.WithContentVersion` explicitly versions external painters; SVG document revisions are observed automatically. Custom `IUiAvatarPresentation.CanRetainPaint` defaults false. Run `./run-gallery.ps1` → Avatar → inactive toggle. CPU54/8283, GPU1374, native23 and635 exact prior scenes; no original RC3/performance completion claim. [Contract](fluent2-avatar.md), `artifacts/inactive-avatar-2026-11-12/verification.json`.

2026-11-12 Final-resolution paint capture: `PaintGroup(..., rasterScale: .875f, textMode: UiPaintGroupTextMode.Projected)` rasterizes transformed text at its destination resolution. The existing fragment specialization is reused with explicit local clip initialization; SPIR-V files are unchanged. CPU54/8269, GPU1128 and635 unchanged prior scene images pass. Ordinary-render paired measurements are mixed, including slower readback-inclusive elapsed time; no speedup or original performance-gate claim. Inactive Avatar remains next. Evidence: `artifacts/projected-paint-2026-11-12/verification.json`; [contract](backdrop-composition.md).

2026-11-12 Runtime paint-only capture: `UiPaintGroup` captures a pure paint callback to actual black/white GPU targets, caches by content/dependency revisions, and supports nested groups and independently owned queued snapshots. CPU54/8267, GPU1119 executions including original RadioGroup parent-allocation264, actual Observer overlap pixels and ordinary submission/retirement checks pass. Runtime input/layout subtree capture, transformed LCD and inactive Avatar remain open. Evidence: `artifacts/paint-group-2026-11-12/verification.json`; [contract](backdrop-composition.md).

2026-11-12 Affine compositor foundation: CPU53/8254, GPU832 assertion executions including actual compiled-family and Observer preview pixels; standalone Observer Release build has0warnings/errors. Evidence: `artifacts/backdrop-composite-2026-11-12/verification.json`. Runtime group capture remains open.

2026-11-12 Experimental affine backdrop composition is under validation. `UiBackdropImage` owns immutable black/white reference renders; `UiImmediateContext.BackdropImage` / `UiControlContext.BackdropImage` and `UiDrawListBuilder.AddBackdropComposite` preserve RGB coverage separately from alpha. This is prepared-image composition, not automatic widget-group capture. Two new shaders passed isolated qualification before promotion; existing SPIR-V is unchanged. Offscreen capture, inactive Avatar scale/group opacity, full regression and performance gates remain open. See [current contract](backdrop-composition.md).

2026-11-12 [Avatar base qualification](fluent2-avatar.md): generated Fluent.Avatar supports14sizes,circular/square,Cover images,cached initials,30named colors andborrowed icons; one passive UIA Image with committed Name events. Observer has 아바타 tab. Current49CPU/8021assertions(Avatar110,Observer175),AvatarGPU100,Observer3,rounded-image40,nativeAvatar10+existing56 pass. Catalog41exposed/55required/33missing; full conformance false. Activity/presence/shadow/motion/forced-color/fullRTL andoriginal gates remain open. Korean font fixtures now explicitly match application Malgun regular/bold registration; GPU compares actual Hangul with the prior missing-font box. Evidence: artifacts/fluent-avatar-2026-11-12/verification.json.


2026-11-12 Rounded image/SVG viewports now share an affine-UV GPU primitive with32-byte paired records. Image Fit supports Stretch/Contain/Cover; CornerRadius changes reuse source textures. Observer SVG exposes1px corner control and fit buttons. See [image contracts](image-widgets.md) and artifacts/rounded-image-2026-11-12/. Original NativeAOT/performance/full Fluent gates remain open.

2026-11-12 [Accordion](fluent2-accordion.md): generated Fluent factory with owned persistent panels, immutable controlled selection, single/multiple/collapsible modes, four sizes, optional borrowed icons, live SetHeader and start/end chevrons. Tab order, focus restoration without UIA, parent clipping, real native ExpandCollapse/relations/events and Observer 접기·펼치기 tab are qualified.47 CPU contracts/7876 assertions (Accordion27,Observer164), native56 andGPU32 pass. Catalog40 exposed/55required/34missing, ScopeComplete=false. Evidence: `artifacts/fluent-accordion-2026-11-12/verification.json`. Collapse motion/RTL/forced colors/reference pixel equivalence/full Fluent and original native/performance gates remain open. Earlier counts below are historical.

2026-11-12 [Label relationship qualification](label-widgets.md): Label publishes a nonfocusable UIA Text provider; the separately rendered primary target connects through LabeledBy after capture in either order. Omission/detach/disposal and aborted capture are checked, with native provider lookup and removal events. CPU46/46 contracts/7844 assertion executions (Label40,Observer159), native Label16/Radio12/Toggle15, GPU Label28 and33 exact prior images pass. Evidence: `artifacts/label-relationships-2026-11-12/verification.json`. Earlier Label LabeledBy limitations below are historical; forced colors/full conformance, high-load/NativeAOT/runtime-image and original whole-goal gates remain open.

2026-11-12 [Native accessibility validation](widget-contract-validation.md): the common collector now emits ordinary control Name and field HelpText changes after publication, while keeping hidden Tooltip and unchanged-frame behavior. Current managed contracts46/46,7827 assertion executions (Label24); separate owned Win32/UIA Label13,Radio12,Toggle15 checks pass. `scripts/Test-NativeAccessibility.ps1` supervises explicit binaries and fresh outputs without builds/SDK switching. Missing input, existing output and wrong-runtime rejection are covered. Evidence: `artifacts/label-native-2026-11-12/verification.json`. Label LabeledBy, forced timeout/interruption, exact RC3 NativeAOT, GPU and full conformance remain separate. Earlier counts below are historical qualifications.

For the legacy widget/GPU bundle while resize is deferred, use the explicit `widgets-no-resize` mode described in [widget validation](widget-contract-validation.md). Its result lists three excluded window/resize groups. Do not call this whole graphics/Fluent acceptance or use it to replace dedicated GPU/native/performance gates.

2026-11-12 [Widget validation](widget-contract-validation.md): current managed suite46/46 with7822 assertion executions (overlap allowed), includingImage23, legacyCPU704 andControl62. Image/SVG GPU74 andObserver7 are separately qualified. The new widgets-no-resize legacy GPU bundle passes4642 checks with each2/3frame configuration and4038 identical PPM files;3window/resize groups are explicitly excluded. Current factory catalog39 exposed names,55 required top-level,35 missing; full conformance remains open. SDK11.0.100/runtime11.0.0 managed validation leaves exactRC3 pins unchanged. External native/UIA, strict generator removal-cache behavior, high-load/long-growth, NativeAOT/runtime-image, resize and whole Fluent/SVG/portability gates remain separate. Evidence: artifacts/image-widget-2026-11-12/verification.json andartifacts/widget-core-gpu-2026-11-12/verification.json.

2026-11-12 [Validated owned-child sizing](widget-measurement.md): natural Field and Tooltip share existing UiRetainedLayout for one validated child size per owner, keyed by explicit constraints and the existing text/resource environment. First measure, live ownership/disposal, opaque children, glyph observers and same-frame invalidation remain checked; legacy immediate AutoField stays uncached. Existing container prepared geometry behavior remains separate. Focused16, delegation18/form17/Field64/containers179/multiline+Tooltip4114/Observer158 pass. GPU owned28/Field1018/delegation28/containers263/Observer50 pass;1425 prior Field images and72 old Observer bodies match. Measurement reuse shows actual owned-child measurement count. Optional cache references are added only to Field/Tooltip, not base UiWidget; no whole-app timing/memory or NativeAOT completion claim. Evidence: artifacts/owned-child-sizing-2026-11-12/verification.json.

2026-11-12 [Owned measurement delegation](widget-measurement.md): Textarea presentation/size/appearance promises default false and known built-ins participate in existing parent retention. Protected CanRetainOwnedMeasurement validates live ownership for Tooltip, Field and explicit user wrappers; arbitrary composition remains non-retained by default. Editing/undo and Tooltip deadlines/show/Escape remain live, including the existing render-time target sizing call. Focused18, multiline/Tooltip4114, Field64/form17/containers179 and Observer157 pass; GPU retained/bypassed28, multiline2560 and Observer50 pass.72 old Observer bodies match; the measurement page now demonstrates multiline editing and delegated Tooltip. No new cache/fields or whole native/performance completion. Evidence: artifacts/delegated-layout-retention-2026-11-12/verification.json.

2026-11-12 [TabList intrinsic measurement](fluent2-tablist.md): explicit CanRetainMeasurement defaults false; known Fluent tabs cache at most256 intrinsic sizes independently of host constraints. Selection/options/resource keys and revision guards preserve dynamic input, failures and invalidation; overflow positions are always rebuilt. Reserved nonoverflow tabs also propagate ancestor retention. Focused17, TabList197/overflow24/Observer152/form17/composite28/containers179 pass. GPU retained/bypassed28, existing TabList800/overflow48/Observer50 pass;793 prior tab images and72 old Observer bodies match. Observer sample has actual tabs, process-wide diagnostic metric count and size-change button. Whole performance/NativeAOT/lifetime gates remain open. Evidence: artifacts/tab-measurement-retention-2026-11-12/verification.json.

2026-11-12 [Form measurement reuse](widget-cpu-retention.md): known Input size/appearance/adornment promises, static Divider/source and Menu trigger promises connect to existing parent retention. Field requires presentation reuse plus a retaining owned child. Explicit Input slots remain an allocation contract and render their actual children; natural Field rendering still measures its child. Focused17, Input202/Divider64/Field64/Menu376/containers179/composite28/scheduling35/Observer149 pass. New retained-vs-bypassed GPU28, existing Input212/Divider525 and Observer50 pass;72 old page bodies match. Measurement reuse now has a real editor/undo/menu checkbox form group with a separate parent counter. No new leaf cache/fields or whole-goal performance/AOT claim. Evidence: artifacts/form-layout-retention-2026-11-12/verification.json.

2026-11-12 Composite measurement correctness: Field size/orientation traits now default to non-retained; built-ins explicitly preserve bounded reuse. TabList ReserveSelectedSpace always reserves both regular and semibold metrics, including when regular is wider. Regression baseline reproduced26 failures; corrected focused28, Field64, TabList197 and Observer145 pass. Existing GPU TabList800/Field1018/Observer50 pass;80 Observer body captures unchanged belowY128. This is correctness work, with no new intrinsic TabList cache or whole-goal completion. Evidence: artifacts/composite-layout-retention-2026-11-12/verification.json.

2026-11-12 [Content measurement reuse](widget-cpu-retention.md): Text requires both an immutable-source policy and a deterministic presentation; typography typeface/metrics and Badge behavior/presentation/icon traits default false. Built-in string/CounterBadge policies opt in; getter Text/Badge remain dynamic. Explicitly sized getter Progress retains declared geometry while reading its actual getter once every render. Link propagates existing ReuseLayout. Spinner retains only when built-in visibility is stable; reveal/animation/pausing still run. No new leaf cache/fields. Focused26, text47/badge73/link61/progress60/spinner186, containers179, Observer145, generators115, contentGPU28/oldleafGPU28/ObserverGPU50 pass;73 earlier bodies match, updated measurement page intentionally differs. Observer Measurement reuse now demonstrates8controls and real Link pointer callbacks. Full performance/NativeAOT and Link semantics remain separate. Evidence: artifacts/content-layout-retention-2026-11-12/verification.json.

2026-11-12 [Leaf layout reuse](widget-cpu-retention.md): static `IUiTogglePresentation.CanRetainMeasurement` and `IUiSliderPresentation.CanRetainMeasurement` opt known Fluent controls into the existing parent cache. Custom Checkbox/Switch size/label traits and `IUiRadioGroupLayout` default false; built-in traits opt in. RadioGroup propagates `ReuseLayout`, and generic Fluent RadioGroup no longer freezes a mutable custom layout. No new leaf cache/fields; render/input continue each frame. Focused14, containers179, radio91, Observer142, generators115, GPU28 and ObserverGPU50 pass;73 prior body captures match. Managed AB/BA/AB removes768000 repeated child measurements per run, with unchanged67584 allocated bytes; no NativeAOT/full-app performance claim. Observer has a new Measurement reuse tab. Evidence: artifacts/leaf-layout-retention-2026-11-11/verification.json.

2026-11-11 [RadioGroup accessibility](fluent2-radio-group.md): actual group/RadioButton roles reuse Selection/SelectionItem, caller bindings, descriptions and roving Focus. Live actions revalidate disabled/clip/popup state; value-based IDs survive reorder and removed getters/actions reject exact0x80040201. Focused18, previous input/allocation91, Observer137, external9/host3 and ObserverGPU47 pass;73 prior bodies match. Parent allocation/normal painting remain unchanged. Full narration, performance and exactRC3 NativeAOT remain open. Evidence: artifacts/radio-accessibility-2026-11-11/verification.json.

2026-11-11 [Common Toggle accessibility](widget-accessibility.md): actual Checkbox/Switch/ToggleButton roles and Off/On/Indeterminate states use one canonical ToggleState, existing action/binding policy and Windows Toggle property events. Checked remains a two-state projection. Core21/Observer134/generators115, external Toggle11/host4 and checkbox-menu6/host3 pass;47GPU checks and73 unchanged bodies preserve rendering. Earlier shared peers update on the requested next frame. Full narration/forced colors/NativeAOT remain open. Evidence: artifacts/toggle-accessibility-2026-11-11/verification.json.

2026-11-11 [Tab overflow](fluent2-tab-overflow.md): optional selected-priority header allocation, generated ellipsis Tab action and shared Fluent popup. Menu selection updates the original header/body in one frame; the group retains one Tab stop and real panel relationships. CPU20/GPU48, external client7/host2, observer70. Containers -> Tab overflow and body; width/RTL controls and retained body counters. Full variants/events/animation/menu icons remain open.

2026-11-11 [Fluent Menu](fluent2-menu.md): generated factory, shared popup, pinned light/dark colors, 138/300 width, 32 rows/2 gaps, regular Type300 and RTL text. CPU57/GPU96, separate external client4/host2 for each presentation and observer53. Exposed36=top-level17+variants19; missing38/55. Exact shadow16, full fidelity and tab overflow integration remain pending.

2026-11-11 [Common menu interaction](menu-foundation.md): Dux.Menu and UiMenu<TPresentation> provide a real transient popup, disabled-aware composite keyboard focus, dismissal and Menu/MenuItem Invoke semantics. CPU21, GPU20, external client4/host2 and observer51 qualify the foundation. Containers -> Menu common behavior is the sample route. Fluent appearance, nested/checked/icon/scrolling menus and tab overflow remain pending.

2026-11-11 [Priority overflow layout](overflow-layout.md): owned horizontal/vertical infrastructure retains hidden widget models, removes their live input/UIA presence, and reports required-content clipping. CPU26, GPU66, external client4/host3 and observer49 pass. Containers -> Overflow common layout provides width/priority/RTL controls. Menu and TabList overflow remain pending.

2026-11-11 [Completed-output cleanup](rejected-build-cleanup.md): direct deletion was policy-blocked. Moved22 verified output directories/333 files to `artifacts/must_delete_20261111_161030`; actual deletions0. Original paths are absent and destination hashes match. Same-volume quarantine is not space reclamation. Evidence: `artifacts/completed-cleanup-2026-11-11/verification.json`.

2026-11-11 [Fluent tab content](fluent2-tab-content.md): optional regular/filled icons, icon-only accessible labels and SubtleCircular/FilledCircular reuse the existing TabList binding, focus and UIA path. Core197, GPU800, native client25/host3 and observer CPU44/GPU40 pass. In the observer select Fluent and scroll to 탭 표현과 아이콘. Official total fidelity and NativeAOT remain open.

2026-11-11 [Accessibility focus publication](accessibility-focus.md): final-frame logical focus and real native HWND focus are distinct. Composite parents do not duplicate child focus; returning to a focused window restores its current virtual child. Core suite111 and owned external client11/host3 pass, with no new frame timer. Evidence: `artifacts/focus-events-2026-11-11/verification.json`. Narration, full desktop/Fluent and NativeAOT remain open.

2026-11-11 [Accessibility relationship changes](accessibility-relationships.md): retained sources emit LabeledBy/ControllerFor changes after publication. Immutable old-target ancestry survives removal; new native payload targets retain actual callable patterns. Dangling targets fail before publication, unchanged relationships are quiet, and source removal emits no dead-source event. Core17 checks plus owned native client12/host4 pass; actual tab/panel integration remains qualified separately. Evidence: `artifacts/relationship-events-2026-11-11/verification.json`. Focus-event qualification follows the scope above; narration and full Fluent/NativeAOT gates remain open.

2026-11-11 [Tab panels](fluent2-tablist.md): `tabs.RenderPanel(ui, key, widgetOrCachedCallback)` renders only the body selected by the current header snapshot, borrowing caller-owned content. Tab/Shift+Tab and UIA focus share navigation; Windows ControllerFor/LabeledBy connect the actual tab and Pane/tabpanel. All ten observer pages consume it. CPU77 (panel25), observerCPU39/GPU37 and external client21/host3 pass;58 previous observer bodies match below128px. Three final .NET11 builds have zero warnings/errors. Evidence: `artifacts/tab-panel-2026-11-11/verification.json`. Later relationship-event qualification is recorded above; screen-reader narration, Fluent fidelity and NativeAOT remain open.

2026-11-11 Immediate regression: `observer-gallery` exercises Hello → Immediate through actual pointer press/release, verifies its deferred event-wait frame and compares the first selected GPU body with four acquired-image replays. This extends the embedded-menu and nested-tab checks; it does not certify unspecified desktop symptoms.

2026-11-11 [Tab selection events](fluent2-tablist.md): selection notifications follow tree publication. Windows raises IsSelected property changes, ElementSelected, explicit deselection and surviving-container invalidation. Initial qualification: CPU52 (basic27 + COM12 + publication13), external UIA client8/host3 pass; both owned processes exit0. Initial/unchanged/reordered states do not invent events. Evidence: `artifacts/tab-selection-events-2026-11-11/verification.json`. The later panel qualification above adds actual body relationships; complete visual states and NativeAOT remain open.

2026-11-11 [Fluent TabList](fluent2-tablist.md): `Fluent.TabList` and inheritable `UiFluentTabList` separate selection binding from arrow-key focus. Three sizes, horizontal/vertical layout, RTL, Transparent/Subtle appearance, reserved width, Enter/Space, Home/End, Escape, disabled skipping and keyed reordering are available. Windows Tab/TabItem types and actual Selection/SelectionItem COM patterns are connected. Initial qualification: CPU39 (27 basic +12 COM), GPU192, observerCPU29/GPU30 and existing accessibility49/containers179/scheduling35 pass. The observer header consumes the actual factory. Catalog35 =16 top-level+19 variants;39 of55 top-level names remain unconnected. Circular/icon/overflow variants, complete fidelity and current NativeAOT remain open. Selection-event qualification is documented above.

2026-11-11 [observer layout, virtualization and rendering](../samples/Duxel.Gallery/README.md): ten tabs now cover every implemented container family, a keyed billion-row variable-height list, nested scrolling, retained primitive geometry and independent3D projection. CPU28 checks actual column changes, row editing, end navigation, reordering with retained values, row disposal, cached callback reuse and explicit animation deadline enable/disable. GPU30 checks acquired-image replay for ten pages at fixed time. The guide command `./run-gallery.ps1` was built and opened successfully. Computer Use read the actual ten-tab window, but input was rejected because user activity was detected; OS input qualification remains incomplete. Evidence: `artifacts/observer-layout-virtual-2026-11-11/verification.json`.

2026-11-11 [implementation observer](../samples/Duxel.Gallery/README.md): `./run-gallery.ps1` builds local .NET11 source and opens Overview, Existing API, Fluent, Typography, Editing, Tooltip and Coverage tabs in one window. All34 exposed names have registered demo tabs; missing or duplicate mappings fail at startup. CPU14 checks include actual pointer callbacks, values retained across tab visits and editing validation. GPU21 checks compare acquired-image reuse at fixed animation time. V-Sync defaults off and event waiting defaults on; FPS text does not request frames. The Fluent Spinner starts paused and schedules animation only after explicit playback. `UiSpinnerOptions.Paused` freezes both phase and continuous scheduling while preserving one-shot delayed reveal. Tab panel focus outlines use visible bounds inside content padding. `SampleScreen.UseEmbeddedMenuBar = true` avoids global viewport insets. Tab selection schedules its deferred frame and floating panels are independent of the calling child's clip, content width and available region. Complete container, virtual-list and rendering-layer pages and actual OS input qualification remain open.

2026-11-11 [Fluent Tooltip factory](fluent2-tooltip.md): `Fluent.Tooltip` and inheritable `UiFluentTooltip` statically bind the existing target and presentation. Catalog34 comprises15 top-level names and19 variants;40 of55 top-level names remain unconnected. Generator115, factoryCPU9 and24 added GPU checks pass, with792 focused Tooltip GPU checks in total. Complete Fluent conformance and current NativeAOT comparison remain separate.

2026-11-11 [Assigned container invalidation](widget-containers.md): when a parent repeats a tight size without measuring, changed Grid/Stack/single-child options take effect on the next frame. Mid-render changes retain this frame's prepared sibling geometry. Explicit cached measurements also refresh their preparation frame; unchanged layouts keep measurement reuse. CPU170/GPU252, ScrollView75, virtual-list75, lifetime101 and accessibility49 pass; all169 prior container images are identical. The existing flow-container FBA runs current local .NET11 sources and presents/exits normally. NativeAOT and full-goal gates remain open.

2026-11-11 [Tooltip UIA Scroll](tooltip-auto-size.md): a visible overflowing tooltip exposes measured two-axis positions and viewport percentages. Accessibility scroll actions use the same offsets as painting, and fitting or dismissed surfaces lose the pattern.

2026-11-11 [Tooltip scrollbars](tooltip-auto-size.md): `ShowScrollbars = true` opts overflowing Tooltip bodies into Duxel overlay scrollbars. Both axes support thumb capture and track clicks. Capture works outside the body and ends when the surface or axis is removed. The generated layout contract FBA opts in.

2026-11-11 [Tooltip shutdown](tooltip-events.md): a Windows close request stops the producer before backend disposal. The tree reports visible tooltip closes before unpublishing ancestry. Immutable event sources, ancestor bounds and a retained native host interface let the external UIA client receive the final close during immediate HWND teardown. Live providers become unavailable; event snapshots expose no actions. Product shutdown does not wait for a client acknowledgment.

2026-11-11 [Tooltip text events](tooltip-events.md): after accessibility publication, the tree reports changes to visible Tooltip Name and effective target HelpText or label Name. Windows projects those changes into UIA property events using the current provider. Text changes keep the tooltip's open lifecycle intact.

2026-11-11 [External UIA on host visibility](tooltip-events.md): a separate client receives two open and two close events across minimize, restore, hide and show in the actual Windows session. Close caches preserve logical identity and offscreen state. The retired live provider becomes unavailable and target HelpText remains readable.

2026-11-11 [Tooltip host visibility](tooltip-targets.md): the Windows backend publishes hidden/minimized state and a hide sequence through the session frame. A host hide sends `HostHidden` requests and releases visual and accessibility surfaces. For zero-extent minimization, the session processes the change with one input-only pass and submits no GPU frame. Restore honors retained controlled values. Backends that omit visibility keep the visible default.

2026-11-11 [Tooltip surface input](tooltip-auto-size.md): overlapping tooltip bodies share one pointer hit selection for hover and both wheel axes. The top painted eligible body receives input. Unmovable wheel axes remain available to the enclosing viewport, and externally dismissing the top body makes the visible body underneath eligible immediately.

2026-11-11 [Tooltip visibility](tooltip-targets.md): `UiTooltip<TBehavior, TPresentation>.Visible` selects automatic (`null`) or caller-owned (`bool`) visibility on first render. `VisibleChangeRequested` reports interaction, Escape and handover requests. The next render observes caller acknowledgement. A declined handover can leave several controlled surfaces open, with separate scroll offsets, texture lifetimes and accessibility relationships. The generated Footer consumer exercises caller-owned visibility. The Fluent factory and full event parity remain open.

2026-11-11 SDK resolution repair: at the user's request, root `global.json` now selects the installed SDK `11.0.100-rc.3.26522.148`, permits prereleases and retains `latestPatch` roll-forward. This fixes the missing `10.0.301` SDK error during editor BuildHost initialization. Public target frameworks are unchanged; ongoing validation still uses .NET11, with .NET9/.NET10 deployment compilation reserved for final completion.

2026-11-11 [Tooltip surface](tooltip-surface.md): compiled default/inverted presentations now measure Type200 with asymmetric CSS padding and paint a rounded body/arrow with sequential RGBA shadows. One window texture slot owns reuse/retirement. CPU4045/Tooltip GPU740 pass, including24 bounded comparisons with12 independent Chromium CSS foreignObject rasters. Native DOM screenshots, DPI/edge pixel equality and full Tooltip conformance remain unverified. No factory/catalog addition. Color IDs below the font-mask threshold are reserved as documented.

2026-11-11 Sample formatting: `samples/fba/widget_tree_theme_fba.cs` uses readable statement layout and spaces around assignment and colons, including named arguments and visible name/value labels. Preserve interpolation format specifiers, URLs and FBA directives when formatting. Whitespace formatting alone does not enforce the space before a named-argument colon; inspect that explicitly.

2026-11-11 [Tooltip auto-size/overflow](tooltip-auto-size.md): stateful size resets now remeasure actual content while retaining candidate/arrow state. Measurement returns visible and full content extents; tooltip wheel clipping/chaining/reset is verified. Additional768 source cases and CPU4022/Tooltip GPU620 pass. Official surface, complete accessibility/control behavior and current NativeAOT remain open.

2026-11-11 [Measured tooltip placement](tooltip-measured-layout.md): compiled per-candidate offsets receive measured dimensions/physical side; target-width matching remeasures content and preserves explicit width. The consumer uses real wrapped text. Additional384 source cases and CPU3252/Tooltip GPU524 pass. Stateful viewport auto-size, official surface/behavior and current NativeAOT remain open.

2026-11-11 [Tooltip offset/cover qualification](tooltip-offset-cover.md): signed main/cross offsets, logical overflow padding, coverTarget and source-matched shift/tether modes now feed the actual consumer. Additional1536 upstream reference cases pass; CPU2863/Tooltip GPU464 pass. Auto-size/reflow, official surface/behavior, complete accessibility notifications and current NativeAOT remain open.

2026-11-11 [Tooltip collision qualification](tooltip-collision.md): locked Floating UI core1.6.8 reference execution now checks 768 rectangle cases; facing-side best-fit, RTL centered priority and arrow alignment reset match. Immutable candidates/separate boundaries/arrow width feed the real tooltip consumer. CPU1326/Tooltip GPU416 pass. Full Fluent surface/behavior and current NativeAOT remain open.

2026-11-11 [Shared tooltip positioning](tooltip-positioning.md): 12 logical placements, RTL, viewport flip/shift and custom arrow anchors feed the actual presentation through a per-frame layout/options snapshot. The generated sample consumes this path. Full Floating UI/Fluent positioning, rounded arrow/shadows and current NativeAOT remain open.

2026-11-11 [Tooltip surface events](tooltip-events.md): actual ToolTip children, control/content views and close-before-removal/open-after-publication now work. Source/ancestry snapshots preserve asynchronous UIA event caches. CPU520 and external client10/host3 pass; official surfaces, property-change/window-shutdown events and current NativeAOT remain open.

2026-11-11 [Tooltip target accessibility](tooltip-accessibility.md): UiAccessibilityNode.Tooltip publishes logical SourceId/text/relationship and maps to Windows HelpText/Name. Field/adornment/nested/frame-text integrity is verified. CPU 502/Tooltip GPU 288 and six actual external UIA checks pass. Fluent factory, official surface, role/events and current NativeAOT remain open.

2026-11-11 [Compiled tooltip targets](tooltip-targets.md): real child ownership, default 250ms show/hide, single window ownership, deferred popup paint and Escape consumption now work. CPU 473/Tooltip GPU 224, existing input/multiline/container regressions and a generated custom consumer sample pass. Fluent.Tooltip factory, official surface, accessibility relationships and current Native/performance remain open.

2026-11-11 [Tooltip reference contract](fluent2-tooltip.md): official guidance, pinned React source and live light/dark captures establish timing, ownership, accessibility and surface references with implementation gaps. No factory or product code is added. All 64 official reference URLs match; managed exposure remains 33 names/14 top-level/41 missing. Implementation and current Native qualification remain open.

2026-11-11 [Shared text drag ownership](text-drag-ownership.md): single-line and multiline now share explicit press ownership and a timestamp; focus-only held input cannot select. CPU444/multiline2560 pass, including context/scrollbar lifetime and pointer GPU checks. Full OS capture/word-drag/partial-occlusion/native fidelity remains open.

2026-11-11 [Multiline source elements](multiline-elements.md): plain editing now keeps combining/supplementary elements intact; all modes align between same-frame edits and queued IME. CPU393/multiline2464 pass; prior input693/multiline3457 images match. [Native status](native-toolchain-status.md) now identifies the historical P: toolchain as unavailable here, with its current location unconfirmed.

2026-11-11 [Single-line drag](input-drag.md): an owned press continues selection outside the field with horizontal autoscroll; release,hidden/clipped/disabled owners and higher popups end capture. CPU336/input-kernel400 pass, with all 357 prior input and 3457 multiline images unchanged. Actual OS capture and complete text/native fidelity remain open.

2026-11-11 [Shared keyboard commands](text-commands.md): Ctrl+Backspace/Delete and held keys remove complete word ranges; multiline Left/Right collapse selections. CPU301, input-kernel304, multiline2336 pass. Full native/Unicode/OS conformance remains open.

2026-11-11 [Single-line source elements](single-line-elements.md): shared movement/deletion, pointer hits, directional selection and repeats preserve complete text elements; boundaries follow buffer lifetime with an ASCII path. CPU231/input-kernel240/FluentInput1362/accessibility 40/lifetime 101/multiline2208 pass. Prior input117/multiline3265 images match. Sample: `fluent_input_elements_fba.cs`. Full shaping/bidi/OS/official fidelity and current Native/performance remain open.

2026-11-11 [Nonwrapping multiline geometry](multiline-hard-lines.md): readonly/Fluent editors preserve CRLF/CR source ranges with owned hard lines, text-element navigation, two-axis gutter feedback and a visible endpoint caret. CPU204/GPU2208 pass with 3,025 unchanged earlier images. The Textarea sample now exercises Wrap switching. Full text/OS/official fidelity and current Native/performance remain open.

2026-11-11 [Text edit integrity](text-edit-integrity.md): bounded insertion preserves complete incoming text elements; full buffers accept selection replacement. External model changes reset undo/redo to the new baseline, while accessibility Value changes remain undoable transactions. CPU175 passes after reproducing 29 baseline failures. Full Unicode/OS input and Native/performance remain open.

2026-11-11 [Fluent Textarea](fluent2-textarea.md): generated `Fluent.Textarea`, three sizes/appearances, wrapped text, Tab/Shift+Tab, Field/UIA focus/value, readonly/placeholder/invalid options and captured scrollbar dragging are connected. CPU126/accessibility 40/lifetime 101/GPU2048 pass. Sample: `fluent_textarea_fba.cs`. Catalog exposure is 33 names with 41 missing top-level names; full conformance, actual OS input and current Native/performance remain open.

2026-11-11 [Multiline accessibility Value actions](fluent2-textarea.md): compiled multiline nodes opt into CR/LF values, reuse normalization/binding/undo and revalidate queued requests. CPU93/accessibility40/GPU320 pass. Single-line validation stays strict; accessibility focus and full Fluent Textarea remain open.

2026-11-11 [Wrapped multiline layout](multiline-wrap.md): opt-in Wrap uses owned visual rows without model newline insertion. Hits, caret, selection, visual navigation/repeat and IME preview share row data; CPU66/GPU320 pass, with 41 unchanged legacy images. Sample: `layout_multiline_wrap_fba.cs`. Full Fluent/complex-script fidelity and current Native/performance remain open.

2026-11-11 [Compiled multiline contract](fluent2-textarea.md): public generic behavior/presentation and owned editor/scrollbars support pure parent measurement, explicit content/line boxes, editing/undo, context transfer and disposal. CPU37/GPU192 pass with 41 unchanged legacy images. Sample: `layout_multiline_contract_fba.cs`. Infrastructure evidence; full Fluent Textarea fidelity and current Native/performance remain open.

2026-11-11 [Managed allocation validation](allocation-validation.md): the strict same-value button test now warms the same isolated loop for 0.25s. Three independent default-JIT lifetime runs pass 101 checks with zero bytes; product code is unchanged. Cold allocation diagnostics remain preserved; Native/whole-app performance remain open.

2026-11-11 [Textarea foundation](fluent2-textarea.md): legacy multiline geometry/editing extraction, boundary wheel propagation and finite geometry guards pass CPU17/GPU32 with 41 identical pre/post images. This initial evidence predates the generated Textarea factory; steady lifetime validation passes after the measurement correction; full component/Native/performance qualification remains open.

2026-11-10 Sample formatting: the original Container/Flex/Stack, single-child, Wrap/Grid, allocated-input and constrained-controls FBA examples now use spaced assignments, named arguments and readable blocks. Display labels use `Count : 0` and `Width : 280`; FBA directives and interpolation format specifiers remain intact. See [sample qualification](sample-format.md).

2026-11-10 [Custom container contract](custom-container-contract.md): external generated layouts pass 15 public-API checks; actual root clipping fixes oversized custom paint/input. Container GPU regression 237 and routing 2,317 pass. Sample: `layout_custom_contract_fba.cs`. Current Native/performance gates remain open; container stages stay 3/6.

2026-11-19 [Linux custom container runtime comparison](custom-container-contract.md#linux-runtime-comparison--2026-11-19): the external x64 managed and NativeAOT sample matched both its initial X11 image and all four held exercise states.

The shared popup rasterizer reuses temporary shadow spans within one invocation. NativeAOT mixed-size tests found that discarding the scratch after every raster lowered the sampled process memory peak, while still reducing allocation versus the original implementation. [Memory policy comparison](optimization-session-2026-11-19.md#popup-scratch-retention-controls)

An external .NET 11 app can use the same contract with a single `Duxel.Windows.App` package reference. `Duxel.App` packs the analyzer resolved for the current build, including builds using a custom artifacts directory.

Custom single-child layouts must return a root size within both axes of the parent's constraints. Measurement and rendering reject a size outside those constraints before caching or painting it.

2026-11-11 [Keyed virtual list](virtual-list.md) uses double offsets and viewport-local placement for billion-item sources. `ExtentIndex` opts into model-supplied variable heights and keeps the visible key and intra-row offset when geometry changes. Rows outside overscan dispose through existing ownership. Automatic row measurement, other axes, cross-key recycling policy and current NativeAOT verification remain open. Container stages stay 3/6. See `layout_virtual_list_fba.cs`.

2026-11-11 [Virtual list accessibility](virtual-list.md#windows-accessibility) exposes a Pane with precise vertical Scroll and ScrollItem for overscan controls. Clients receive a child-list invalidation when the realized range changes. `IUiVirtualListAutomationSource` adds indexed `ItemContainer` lookup and `VirtualizedItem` realization. A held provider follows its key after reordering, and realization can reveal the row through an owned scrolling ancestor.

The shared `virtual-list-pixels` runner checks billion-row fractional offsets on Windows and WSLg. WSLg managed and NativeAOT matched all 768 frame hashes on the same Vulkan driver.

The external Linux `atspi-gallery-virtual-list` check edits an actual Gallery row and verifies bounded exposure and retention of the saved value after eviction and reordering.

A vertical thumb drag retains the exact offset at press. Returning the pointer to that position restores the offset, including its fractional part near the billion-row limit.

For variable heights, a fully clipped list defers restoring the visible key until the current clip is visible. Rows that stay realized keep keyed editor state through a height change or reorder.

2026-11-10 [Owned ScrollView](scroll-view.md): Dux.ScrollView owns finite viewport measurement, real child placement, offset, scrollbar lifetime and invalidation. CPU47 and separate GPU/routing1933 checks pass. World-coordinate precision loss fails explicitly; rebased large content/virtualization and current Native remain pending. Container stages stay3/6. Sample: `layout_scroll_view_fba.cs`.

2026-11-11 [ScrollView accessibility](scroll-view.md#windows-accessibility) exposes a Pane with the Scroll pattern on overflowing axes. Accessible descendants use ScrollItem to reveal themselves through nested viewports while preserving identity and focus. Retained ScrollView and Tooltip providers notify Windows UI Automation when a scroll metric or Scroll pattern availability changes. An external client verifies position and view-size notifications in the running sample.

2026-11-11 [RTL scrolling](scroll-view.md#windows-accessibility) measures owned horizontal offsets from the right and mirrors the vertical scrollbar gutter. ScrollView and Tooltip expose right-to-left UIA percentages. The sample's `Direction` control switches the two-axis view and shows different rows at each endpoint.

2026-11-11 [Pane notifications](scroll-view.md#windows-accessibility) publish clipped screen bounds and offscreen changes for retained panes and their rendered accessible descendants. Direct-child changes invalidate the Pane once after publication. The sample's `Toggle row` lets an external UIA client verify addition and removal.

2026-11-10 [Nested scroll routing](scroll-routing.md): boundary handoff on independent wheel axes, caller clip and popup/window-order hit testing, shared scrollbar capture/release, and measured DirectText window steps. Expanded managed regression passes 1,645 checks and 1,025 readbacks; container stages remain 3/6. Owned scroll widgets, virtualization and current Native remain pending. Sample: `layout_scroll_routing_fba.cs`.

2026-11-10 [Field parent allocation](fluent2-field.md#parent-allocation--2026-11-10): actual width precedes text measurement; static FieldWidth/Arrange and a local layout copy reserve description rows and allocate remaining height to the real editor. The 80 x 64 / 80 x 32 defect is fixed. Managed Field1018 and separate routing1563 checks pass; natural cache, editor/undo and Core UIA metadata persist. Panel measurement rejection/closed-root boundary checked. Native remains unverified; container stages remain3/6. Local sample:layout_field_allocation_fba.cs.

2026-11-10 [Allocated child routing](widget-containers.md#allocated-child-routing--2026-11-10): common child rendering preserves the incoming clip and isolates/restores legacy table/column positioning. Nested Padding now honors actual offsets across 14 leaf variants. Managed 1529 checks pass (1113 CPU and 416 exact raster/vector GPU comparisons). The routing checkpoint found the Field 80 x 64 / 80 x 32 defect; the later Field allocation checkpoint above fixes it. Current Native prerequisites remain unavailable in checked standard paths; alternative L: roots deny access.

2026-11-10 [Badge measurement and allocation](fluent2-badge.md#measurement-and-parent-allocation--2026-11-10): shared geometry and one common root, centered fixed glyph/icon metrics, root clipping and hidden-counter reservation. Read-only providers and the existing bounded number memo are shared across explicit phases. Managed 374 checks pass; Native unverified. No fields/new caches. Local sample: layout_badge_allocation_fba.cs.

2026-11-10 [Link measurement and parent allocation](fluent2-link.md#measurement-and-parent-allocation--2026-11-10): real width controls wrapping; static Arrange returns a local copy while the bounded natural cache stays reusable. Input, paint and clips share the actual root. Managed 495 checks pass across CPU/input, raster/vector, MSAA 1/4, Light/Dark and paced retained frames. No new fields/caches; Native remains unverified. Local sample: layout_link_allocation_fba.cs.

2026-11-10 [Divider measurement and parent allocation](fluent2-divider.md#measurement-and-parent-allocation--2026-11-10): static Measure and CanArrange use one common root; actual width controls caption wrapping and both axes control lines, centering and clipping. Content providers remain read-only and are read once per explicit measurement/render phase. Managed525 checks pass; Native remains unverified. No new widget/context fields or caches. Local sample:layout_divider_allocation_fba.cs.

2026-11-10 [Text parent allocation](fluent2-text.md): CanArrange opts into one shared text root; assigned/measurement width controls wrapping without changing fonts. Existing content-provider reads are preserved. Managed321 checks pass across raster/vector,MSAA1/4,Light/Dark and retained16/3-frame updates. No per-widget fields were added;one context counter is added. Native remains unverified. New local sample:layout_text_allocation_fba.cs.

2026-11-10 [Progress measurement/allocation](fluent2-progress.md): static source/presentation measurement avoids getter reads; declare a getter height or use a tight parent. Static Arrange and the shared render path honor actual roots. Managed306 checks pass; Native remains unverified. New local sample:layout_progress_allocation_fba.cs. Whole-goal counts and resize deferral are unchanged.

2026-11-10 [Spinner allocation](fluent2-spinner.md): static Arrange and shared pure measurement/rendering honor parent roots; managed 520 checks pass, Native remains pending. New/edited C# follows .editorconfig with readable spacing and line breaks; labels use Width : 160. The RadioGroup sample was reformatted and rebuilt successfully.


2026-11-11 Slider numeric accessibility: UiAccessibilityRangeInfo/Node.Range, typed RequestRangeValue and RangeChanged connect generated/generic Slider bindings to Windows RangeValue10003. Final managed28, external client13/host3 and no-acknowledgment Tooltip shutdown checks pass. Removed child providers preserve immutable disconnection identity while data/actions reject removal; HWND-root metadata keeps its original host lifetime. Prior S_OK/0 getter failure is fixed without relaxing the strict HRESULT test. See [Slider contract](fluent2-slider.md) and artifacts/slider-range-2026-11-11/lifetime-verification.json. Narration, complete legacy tab keyboard/flag coverage and NativeAOT remain open.

2026-11-11 SVG curved text references: SvgTextReferences compiles tref into spec-equivalent typed spans after DOM/style parsing, preserving original element/ID/class selector identity and positioning. This fixes empty tref inside textPath without another XML parse or alternate renderer. The sample curved caption uses a referenced definition. CPU112/Observer123/SVG GPU52/Observer GPU7 pass; the actual sample body matches the former literal pixels. See artifacts/svg-support-2026-11-11/tref-verification.json and [SVG text](svg-support.md#referenced-characters-on-a-path). Full conformance/native/AOT remains open.

2026-11-11 SVG document features: SvgCssResources reads resource tokens while ignoring comments and quoted property text; escaped live resources keep existing explicit validation. CPU102/observer123/SVG GPU52/observer GPU7 cover symbol/use, xlink, patterns, masks, radial gradients, markers, embedded PNG and resource token cases. Observer → SVG now has real linked 이미지·텍스트 / 문서 기능 panels and five assets, sharing1px/1°/1% controls. See artifacts/svg-support-2026-11-11/features-verification.json and [SVG guidance](svg-support.md). Complete SVG/CSS/script/native/AOT conformance remains open.

2026-11-11 SVG lifetime repair: disposed SvgScene drops its backend owner/picture references, so a retained stale SvgAsset cannot root backend caches. Thirty-two text reloads retire old native pictures; failed reload preserves live picture/fonts; repeated disposal closes registered font/typeface handles. CPU81, observer120, SVG GPU44 and observer GPU3 pass. See artifacts/svg-support-2026-11-11/lifetime-verification.json and [lifetime limits](svg-support.md#disposal-and-stale-resource-references). Reachability/handle checks do not certify total native memory or NativeAOT.

2026-11-11 SVG explicit-font stage supersedes the earlier font-pending checkpoint: SvgStore(fonts : new SvgFontOptions(defaultFamily, faces)) snapshots SvgFontFace files/collection faces, selects registered actual styles and rejects unresolved families/styles/glyphs before publication. No installed-font substitution; @font-face/embedded document fonts remain unsupported. Retained text scales without repeating XML/face resolution/coverage checks; FontStatistics separates these counts from native drawing. Observer → SVG has four assets, Latin/bold/Korean/textPath and font counters, sharing the1px/1°/1% controls. Current CPU74/observer120/SVG GPU44/observer GPU3 evidence and limitations are in artifacts/svg-support-2026-11-11/font-verification.json and [SVG guidance](svg-support.md). Full script/feature/native/AOT qualification stays open.

2026-11-11 SVG adjustment units: Observer translation1px/rotation1°/zoom1 percentage point; pointer and focused Left/Right arrows share the steps, with explicit units in captions/current values. Observer119 including six fine-step input checks, observer GPU3 and Slider GPU156 pass; app/Graphics builds have zero warnings/errors. ShowStepMarkers defaults true; SVG sets false to preserve continuous rails with fine input. See artifacts/svg-support-2026-11-11/unit-step-verification.json. Font/full SVG/native qualification remains open.

2026-11-11 SVG observer/icon stage: optional Duxel.Svg shares parse-once scenes and rasters with Core UiSvg and UiSvgIcon.MenuIcon/TabIcon. Authored colors are default; MultiplyForeground is explicit. Actual Observer -> SVG exposes both image routes, zoom/rotation/translation, reload/reset and counters; run ./run-gallery.ps1. Retained layer texture references survive widget disposal. Image APIs now map full UVs, preserve alpha/immutable queued pixels and use IsColorImage sampling independent of legacy font-ID ranges. Contracts54, observer113 (icon11), SVG GPU32/MSAA1-4, observer GPU3 and menu glyph GPU216 pass; three builds have zero warnings/errors. See [SVG contract](svg-support.md) and artifacts/svg-support-2026-11-11/observer-verification.json. Font/text, full corpus, native lifetime/deployment/AOT and the original goal remain open.

2026-11-11 current additions: UiMenuOption.Icon/SecondaryContent, UiMenuIcon.Create<TRegular, TFilled>(), UiMenuItemContent and UiControlContext.IsHovered connect typed icon pairs, trailing HelpText, separate clips and RTL to the shared menu engine. Actual sample: Containers → Menu common behavior → 아이콘 메뉴 열기. Evidence: artifacts/menu-content-2026-11-11/verification.json; [menu contract](fluent2-menu.md). TTF icons use cached raster textures in ordinary2D and font-outline meshes in3D layers.

The user expanded the original active goal with SVG bitmap-style file input and SVG-backed icons plus a zoomable/rotatable widget or equivalent reusable API. Both routes share SVG interpretation/resources through the implemented asset/widget stage above. [SVG goal](svg-support.md), artifacts/svg-support-2026-11-11/scope.json track remaining font/full-feature/native qualification. Managed tests are separate from NativeAOT qualification. All original goal axes, resize deferral and the prohibition on MSVC/Windows SDK probes before supplied paths remain in force.

2026-11-10 [RadioGroup allocation](widget-containers.md):managed264 checks/245 images and sample presentation/exit verified. Native needs missing C++ Build Tools. Full goal counts and resize deferral unchanged.

2026-11-06 [all layout cache families](widget-containers.md): Row/Column,Container and Stack share the optional retained result payload with Wrap/Grid;parent-data wrappers use the allocated render size. Custom box/axis/fit policies default to non-retained behavior. Managed/Native175 checks and121 GPU images qualify reuse and dynamic changes. Reused prior Native comparisons retain0B cache hits but include a1.7µs sample;10000 Container creation allocates80,000B more(+1.33%). Overall speed/memory improvement remains unconfirmed. Evidence:`artifacts/container-retention-2026-11-06/verification.json`.

2026-11-06 [retained layout](widget-containers.md): common invalidation follows child ownership without adding per-instance fields to every UiWidget. Opted-in Wrap/Grid retain measured rectangles;Fluent buttons opt in through a compiled presentation policy. Unknown user measurements remain volatile. Managed/Native159 checks and109 GPU images qualify input,text changes and scope/lifetime restoration. Three Native runs each for20000 textless buttons reduce pure-measure median1827.4µs→0.2µs near timer resolution,with0B on both sides and overlapping process-memory ranges. This is not whole-frame/FPS/Pi performance. Evidence:`artifacts/layout-retention-2026-11-06/verification.json`.

2026-11-06 [Wrap/Grid](widget-containers.md): generated `UiWrap`/`UiVerticalWrap` and `UiGrid`/`UiHorizontalGrid` use the same ownership,measurement and input path,with static axis selection. `Dux.UniformGrid` preserves legacy `Dux.Grid` binding. Runtime options retain child instances;`UiArrangedChildrenWidget` validates custom arrangement constraints. Managed/RC3 Native each136 checks and93 exact GPU images;10000 Wrap plus10000 Grid children measure at0B after warmup. This is a pure allocation contract,not app throughput/Private improvement. Sample:`layout_flow_containers_fba.cs`;evidence:`artifacts/flow-containers-2026-11-06/verification.json`.

2026-11-06 [Stack/Positioned](widget-containers.md): generated inheritable implementations share multi-child ownership and guarded Build parent-data discovery with flex. Edge constraints, live options, clipped standard pointer input and topmost press/release buttons are qualified by95 managed/Native checks and53 matching GPU images.10000 small positioned children measure at0B after warmup;this is not whole-frame allocation or throughput. This was the Stack-only checkpoint;the newer Wrap/Grid entry qualifies the combined stage. Evidence:`artifacts/stack-containers-2026-11-06/verification.json`.

2026-11-06 [Weighted containers](widget-containers.md): generated UiExpanded/UiFlexible/UiSpacer,Dux.Expanded/Flexible/FlexSpacer,weighted Row/Column and cross-axis Stretch. Live Flex changes preserve child state and request a frame. Custom leaves can use UiControlContext.ConstrainToLayout. Legacy Dux.Spacer(float) stays unchanged. Managed/Native67 checks each,37 matching GPU images,0B warmed10000-child pure measurement. One of six container stages is complete;throughput,general leaf stretching and incremental layout remain open.

2026-11-06 [Actual high-load application/command accumulation candidate](optimization-evaluation.md): common builder accumulates primitive-run count/bounds and commits immutable commands before other accesses. Managed/Native74 checks each pass;100 GPU images match.12 alternating app runs have overlapping frame/FPS/Private/working-set ranges:improvement is inconclusive. This differs from the earlier2048-text memory probe. Full goal remains active;resize remains deferred.

2026-11-06 [Retained text maintenance](directtext-layout.md): reuse the exact no-eviction-candidate proof until cache membership or layer retention changes. Fixed RC3 Native2048-entry Render allocation falls99.74%;35 focused GPU checks pass. Short post-phase Private is51.83% higher;this is not whole-app memory improvement. Raw references and hashes:`artifacts/retained-text-maintenance-2026-11-06/verification.json`.

2026-11-06 [Shared widget storage](widget-cpu-retention.md): UiBuildContext lazily creates dependency/ownership change storage; direct widgets share their callback/context slot. Fixed RC3 Native probes reduce10000 Container construction allocations23.47% and first-Build allocations73.81%,with unchanged0B steady traversal. These are scoped allocation results,not whole-app Private/FPS or completed Fluent coverage. Window move/resize investigations are deferred by user request; preserve their source and evidence.

2026-11-06 [Live resize ordering](windows-resize-coherence.md): DC656134 passed bounded Native cycles but failed user review for trails and delayed size following. The current candidate restores committed-WM_SIZE submission and keeps graphics readiness before replacement, while presentation fences continue to protect deferred resource destruction. Validation is .NET11 only. Numerical submission tests do not establish visual acceptance; see the resize acceptance ledger.

2026-11-06 [Windows caption default](extended-title-bar-guide.md): native System chrome is now the default (`UseDuxelTitleBar=false`), including Windows text, height and buttons. The app initializes direct root content layout in System mode. Explicit Duxel/ExtendedContent remain available. Repeated-resize regression investigation is active; Native preview `804F7691` passed first-presentation/15-second exit checks, not long-resize qualification.

2026-11-06 [Compiled containers](widget-containers.md): Flutter-inspired box constraints and generated UiContainer/UiRow/UiColumn now share UiWidget ownership and constrained Build measurement. The executable `layout_containers_fba.cs` demonstrates real Fluent callbacks. Flex children, advanced layouts, scrolling/virtualization and incremental layout remain open; see the six-stage container plan.

2026-11-06 [Bounded animation and nested replay](frame-scheduling.md): Spinner/Progress suppress offscreen and zero-opacity wakes through the common bounds-aware request. Cached layers now reuse hidden geometry during unrelated frames through retained animation bounds; projected culling and conservative union false positives remain. Cached child/parent translations add and clips use command-local coordinates. Evidence is under `artifacts/animation-visibility-2026-11-06/` and `artifacts/hidden-animation-cache-2026-11-06/`; measured checkpoints predate containers and resize changes.

User validation policy,2026-11-06: use .NET11 only for ongoing functional/regression/performance validation; compile .NET9/.NET10 for deployment only at final completion. Preserve existing results; do not repeat their development runs. Keep fixed RC3 NativeAOT performance comparisons and existing public TFMs. The user's2026-11-11 SDK repair request supersedes preserving the old root `global.json` SDK pin.

2026-11-06 [Scheduled frames](frame-scheduling.md): UI-clock deadlines use the interruptible app wait. Spinner delay and cached-layer replay preserve scheduling without timer objects. Visible motion and caret blink remain active; see linked evidence and limits.

2026-11-06 [Owned Input child slots](fluent2-input.md): BeforeSlot/AfterSlot, atomic SetSlots, key/ownership/ancestor checks, Build lifetime and focus-within use real generated buttons/user widgets. The gallery includes a clear action. NET10 full2959/GPU3561 and fixedRC3 Native3561 per mode pass;2737 full/833 focused images agree. Intrinsic measurement, Field/UIA/motion and complete official conformance remain open.


2026-11-06 [Input before/after content](fluent2-input.md): default text slots and static custom icon/presentation composition separate editing bounds and prevent narrow-root overlap. NET10 full2934/GPU3408 and fixedRC3 Native full3408 per mode pass;2481 full/577 focused matched images agree per mode. All67 Core generated files are unchanged. Interactive child slots, Field/UIA/motion and full official conformance remain open.


2026-11-06 [Badge allocation probe](fluent2-badge.md): isolated exact loop, cold records and an invalidated-memo control distinguish0B steady reads from320000B actual allocation. Three managed focused runs/Native focused each50 and managed fullGPU2428 pass;2097 images match preserved Native outputs. Product source is unchanged and the original12336B cause remains open; no speedup claim.


2026-11-06 [Input](fluent2-input.md) shares editing and lifetime across generated/default/custom inheritance, Dux and direct calls. Three sizes/four appearances connect Tab, readonly, placeholder, invalid and IME ownership release. NET10 full2913/focused229, fixed RC3 Native full2427 and focused229 per mode pass;193 matched-size images agree per mode. The managed full GPU rerun failed the existing Badge12336B allocation assertion and is not a full pass. Discovery has30 names=11/55 top-level+19 variants,44 missing. Full official visual/UIA/Field/slot/motion and Pi conformance remain open.


2026-11-06 [RadioGroup](fluent2-radio-group.md) now uses shared widgets, generated factories, Dux and direct calls. It supports grouped Tab/arrows/Space, ordered selection callbacks, three layouts and bounded retained line layout. Common text blocks preserve multiline baselines and glyph ink. Full official visual/UIA/Field/RTL contracts remain open.

2026-11-06 [Checkbox presentation](fluent2-checkbox.md): existing generated widgets now bind mixed/checked/disabled states, Regular14/20 typography, eight static presentation combinations and exact12/16px icons. Behavior, inheritance and lifetime remain shared; preview with `--checkbox`. Official visual/UIA/RTL/long-label fidelity and full Fluent coverage remain open.

2026-11-06 [Shared depth attachment](shared-depth-target.md): the common graphics queue now owns one depth image instead of three. Three alternating fixedRC3 Native processes per variant reduce depth allocation66.67% and Private24.91%; FPS/GPU timing ranges overlap. NET10 full1862, managed/Native queued-depth1067 checks pass. Same-DPI old1109 images×2 and managed1117 images×2 match exactly. Whole goal, long-duration memory and Pi hardware remain open.

2026-11-05 [Panels and direct content](panels-root-content.md): default samples no longer use an internal Window shell. Optional generated Panels and DSL/legacy compatibility share one kernel. Windows sizing no longer waits for GPU presentation in the message handler. See the [execution guide](widget-gallery-run.md).

2026-11-05 reported gallery defects: [Internal window placement](window-placement.md) documents initial placement, work-area coordinates, AutoHeight and drag-release persistence.
The [execution guide](widget-gallery-run.md) now points to gallery-window-fix artifacts. FullNET10 1004, shared managed/RC3 108 and428 unchanged images pass. The user accepted title placement, spacing and natural internal dragging; this does not qualify whole-app performance.

Preview current progress with the [execution guide](widget-gallery-run.md): .NET10 local source or a validated RC3 snapshot, with default/`--typography`/`--progress`/`--slider` modes.
2026-11-05 [Fluent Slider](fluent2-slider.md) binds generated inheritable widgets, static behavior/presentation, pointer/keyboard, steps/reversed/vertical travel.
FullNET10 995, shared managed/RC3 99,423 identical preceding images.24 catalog names/289 known requirements/265 missing names are not final coverage. Complete SliderFloat/VSlider/Dux.Slider migration and official palette/UIA/fidelity remain open.

2026-11-05 default Fluent Text: [Text follow-up](fluent2-text.md) covers actual Body1(14px/20px), default wrapping and inherited/getter/typed immediate paths.
Nongeneric ui.Text APIs retain explicit context-font compatibility policies. Low-level Fluent requires DirectText, metric backend and TypographyFonts; missing setup fails explicitly.
Common wrapping reuses fitting input strings and resets width at newlines. FullNET10 963, shared managed/RC3 67,417 identical preceding images. Complete Text variants/UIA/default nongeneric migration and app performance remain open.

2026-11-05 common gradients: [Rounded gradients](rounded-gradients.md) documents `AddRectFilledGradient`, `UiLinearGradient` and actual ProgressBar integration.
The64B primitive preserves32B strides and works with static/dynamic/compiled layers and damage. Same256 gradients: draws512→1, upload61,440→16,384B; whole-app FPS/Private gains are unverified.
Validation:954 fullNET10,45 shared managed/fixedRC3 GPU,62 input checks,323 identical original-shader reference images at the same size. Complete widgets/Fluent/memory goals remain open.

2026-11-05 progress transitions: [the follow-up](fluent2-progress.md) covers static behavior binding, widget-owned state,300ms ease and release.
NET10 full915, shared managed/native62 checks and323 unchanged images passed. Transition storage adds24B/construction; this is not a whole-app memory/speed improvement.
Immediate types carry no new state field. Complete official motion/masking, UIA and default API migration remain open.

2026-11-05 progress: [generated ProgressBar](fluent2-progress.md) closes value/getter sources and presentation types, including indeterminate/reduced-motion rendering.
Use the existing FBA's --progress mode. NET10 full898, shared managed/native46 checks,323 unchanged images and seven new GPU images passed.
Legacy overlay APIs remain preserved. Full default migration,300ms transitions, curved masks, semantics and official fidelity remain open.

2026-11-05 keyboard: [generated control input](widget-keyboard.md) covers Tab, Enter/Space, focus visuals, cancellation, lifetime and remaining scope.
NET10 full871/focused159/generators66, the same24 checks in fixed-RC3 NativeAOT and323 unchanged images passed.
`tests/Duxel.Control.Tests` reuses the test body without opening a window. Complete Tab integration, official focus visuals, UIA and performance remain open.

2026-11-05 typography: [17 named Fluent text presets](fluent2-typography.md) bind real400/600/700 font faces and official line heights through generated types.
Use `DuxelFontOptions.Typography`, `UiContext.SetTypographyFonts` and the metrics backend. The existing FBA accepts `--typography` to render all17.
NET10 full847, generators66,323 unchanged images and fixed-RC3 NativeAOT first present/exit0 in both modes passed.
Default plain Text migration, complete variants, UIA, official visual comparison and performance remain open. The22 catalog names are not overall support coverage.

2026-11-05 text: [Generated Text widgets](fluent2-text.md) bind string/function source policies and presentation as closed types.
Existing Dux.Text/color/wrapping and immediate APIs share one kernel, with inherited ReadText and custom layout/paint examples.
Full official typography, semantics and visual fidelity remain open; discovery names are not complete support rates.

2026-11-05 retained scale: [10,000-button input ID reuse](widget-scale.md) covers the4,096-character
widget-owned cache, parent changes, callback correctness and three fixed RC3 CPU comparisons.
Full801 checks pass. CPU median reduction12.55–14.13% is not application FPS;
Lazy storage now resolves transient factory allocation; extra retained-cache residency and whole-app validation remain open.

2026-11-05 selection button: `Fluent.ToggleButton`, `Dux.ToggleButton` and immediate `ui.ToggleButton`
share a generated inheritable widget and the boolean value/input kernel. See [toggle notes](fluent2-toggle.md).
Complete Fluent conformance and10,000-button performance remain unverified.

2026-11-05 catalog follow-up: [Fluent migration](fluent2-coverage.md) explains the generator's TSV draft input.
It checks standard references and missing factories; `DuxelRequireCompleteFluentCatalog=true` rejects current incompleteness.
The current289 names are not the final complete denominator; catalog validation does not prove visual conformance.

2026-11-05 latest: [Compiled toggles and identity](fluent2-toggle.md) documents generalized
`UiWidgetStyle` constructor binding, bool/mixed Checkbox and Switch, and label-independent retained input IDs.
NET10 passed787; RC3 full GPU-particle comparison failed and remains unresolved despite two focused passes.

2026-11-05 follow-up: [Complete Fluent migration](fluent2-coverage.md) records the expanded
official component scope, attribute-driven standard discovery and `Dux.Button()`'s generated Fluent factory.
Full component coverage, nonstandard exclusion and matching look and feel remain unverified.

2026-11-05: [Widget implementation contracts](widget-implementation.md) defines the first Fluent 2
theme, inheritable built-in buttons and source-generated concrete behavior/presentation binding.
Other control migrations and performance validation remain open; numeric skin generation is not completion evidence.

2026-11-04: [Widget CPU reuse progress](widget-cpu-retention.md) defines atlas glyph-run cache
scope,budgets,callbacks and frame ownership; it does not skip whole default DirectText composition.

2026-11-04: [Pi specification simulation](raspberry-pi-simulation.md) separates official specs,
required-feature mismatches in two Mesa versions,and trace sensitivity. It does not establish Pi FPS or Linux support.

2026-11-04 follow-up: the common Vulkan renderer preserves screen-space static/dynamic content
per swapchain image and redraws damaged regions. See [retained rendering](retained-rendering.md)
for MSAA history, alpha composition, snapshot lifetime and full repaint conditions. Windows
validation does not establish Raspberry Pi performance or Linux/ARM64 support. That document
and SESSION_HANDOFF.md take precedence for the latest follow-up results.

> Last synced: 2026-11-14
> Audience: coding agents and developers building apps, samples, and reusable UI components with Duxel
> Scope: Duxel feature map, architecture boundaries, recommended workflows, and sample anchors

Korean: [docs/duxel-agent-reference.ko.md](duxel-agent-reference.ko.md)

## Markdown image Component scrolling (2026-11-15)

The current pinned RC3 NativeAOT Observer is `/home/dimohy/.cache/duxel/observer-image-component-rc3-aot-20261115/publish/Duxel.Gallery.Linux`. Launcher-path verification passes32 screen presentations and384 native input changes with normal exit. Earlier golden performance binaries remain preserved; this is regression coverage, not a fresh comparative speed claim.


Markdown already registers its internal text viewport with the common scroll tree. Image `Component.ScrollTo` and `ScrollToPoint` therefore reuse actual rendered image rectangles and the existing queued viewport movement, including linked and table images. No synthetic text node or second projection model is introduced. View and Editor checks preserve the exact Unicode source, caret, selection, runtime identity and focus; revealing a linked image does not navigate it. Parsed Markdown and resolved image resources are reused.

A nested invalid-point case exposed an error-order bug: the inner clipped slice could produce `InvalidOperation` before the outer viewport rejected the point. The common engine now validates the outer visible point bounds before planning any inner movement and consistently returns `InvalidArgument`. The old failing request did not move geometry; the correction is error classification and validation order.

Focused verification covers 930 layout checks across View/Editor, local/nested LTR/nested RTL, ordinary/linked/table images, all seven placements and exact viewport edges. Independent native clients pass 68 checks in each of 12 explicit/registered scenarios, including all three coordinate spaces; 24 host lifecycle checks confirm owned-window unregistration. Existing common component175 and text-scroll691 checks pass against the identical Core binary. This does not certify a physical reader, full accessibility conformance or image performance. Evidence: `artifacts/linux-image-component-scroll-2026-11-15/verification.json`.

The focused test modes are `image-component-model` and `image-component-native`, followed by a fresh result directory and explicit Latin/Korean font paths. Run the native mode under the existing accessibility session or `dbus-run-session --`. The previous note that editor images needed a new source-aware Component implementation was an unverified assumption; source inspection and these actual calls establish reuse of the existing viewport path.

## Component scrolling through shared viewports (2026-11-15)

Linux `Component.ScrollTo` and `ScrollToPoint` now request actual movement through `UiAccessibilityTree.RequestScrollIntoView`. `UiAccessibilityScrollAlignment` accepts top-left, bottom-right, top/bottom/left/right edge, nearest, or a finite UI client point. The existing `UiAccessibilityTextScroll` API is preserved and delegates to the same alignment calculation. Nearest placement keeps a viewport-spanning component stable instead of alternating between opposite edges.

Screen, window and parent coordinates are converted to UI client coordinates. Point targets must be inside the outer visible scrolling viewport, and offsets stay within each owner's range. Completion follows the frame that publishes the movement. Requests preserve control identity and do not focus or invoke it. This connects both shared `ScrollView` ancestors and registered Markdown text viewports. The image-specific qualification above extends the original button fixture.

Focused verification: 175 common model checks across all four outer/inner RTL combinations; 691 existing text-scroll checks including virtual and popup cases; independent native clients with 24 checks per explicit/registered session and four host lifecycle checks. Supported net8/net9/net10 Core/Linux builds pass without warnings. The net10 build also exposed a clipboard MULTIPLE overload ambiguity: explicitly selecting `pairs.Data.AsSpan()` preserves writable pair updates. Its existing private-selection clipboard suite passes 35 checks on .NET10.0.15. No whole accessibility-suite retry, physical-reader qualification or new performance claim is included. Evidence: `artifacts/linux-component-scroll-2026-11-15/verification.json`.

To run the focused native profile using a freshly published test executable:

```bash
dbus-run-session -- /path/to/Duxel.Linux.App.Tests component-scroll-native \
  /path/to/fresh-result /path/to/latin.ttf /path/to/korean.ttf
```

The corresponding headless layout profiles are `component-scroll-model` and `text-scroll-common-regression` with the same result/font arguments. The native profile creates and closes only its owned windows and preserves primary child failure records.

## Current verified baseline — 2026-11-03

2026-11-04: shared GPU edits use `UiGpuParticleSystem.Append`/`Truncate` and per-frame
`UiGpuParticleSnapshot`. Existing positions,directions,rotation and collision state survive;
only new bodies upload. VSync ON uses FIFO; `UiRendererFrameMetrics` exposes applied
`VSyncEnabled`/`MsaaSamples`/`PresentationMode`. NativeAOT actual-input24checks,
NET10/11each474checks and142/142unchanged images pass. Latest5000-body/Size6/Render-MSAA1
three-run average median:5561.85FPS. See the [current record](polygon-performance.md)
for conditions,reproduction and current executable SHA.

The shared bouncing-polygon engine work follows the [progress record](polygon-performance.md).
Shared APIs are `SpatialGrid2D`, `Duxel.Core.Simulation.CircleCollisionSolver2D`,
`UiDrawListBuilder.AddRegularPolygons`, and `UiRendererFrameMetrics.FramebufferSize`.
Circles and rectangles share a GPU instance stream while preserving analytic coverage.
The sample defaults to 5,000 bodies, Size 6 and a large window, with a GPU vector canvas
and retained backdrop geometry. `UiGpuParticleSystem`/`UiGpuParticleFrame`/`AddGpuParticles`
keep state on the GPU for integration, spatial queries, collision and vertex generation.
`ParticleJacobiSimulation` implements the same model on the CPU. `UiDrawList.GetCommandFeatures`
and `UiDrawData.GetCommandFeatures` reuse callback/GPU inspection for unchanged command state.
Backdrop caching uses shared `BeginLayer`, and the common render pipeline defaults on;
there is no sample-owned draw-list cache.
The common FBA launcher uses `scripts/BuildArtifactLifetime.ps1` to check ownership,
hashes and active processes and retain at most three outputs. Unregistered, failed,
cancelled, modified and running outputs remain protected.
GPU implementation, correctness, regression and local delivery are verified. Use the
executable SHA256 in the linked record. Equal-algorithm CPU849.07→GPU5448.07FPS
(three-run medians),NET10/11each465checks,136/136unchanged images and13artifact-lifetime
checks pass. See the record for conditions and limits.

The latest default NativeAOT is **DE068949**,published locally at the [executable](../samples/fba/artifacts/erd_layer_lab/net11.0-windows7.0/erd_layer_lab.exe).1200tables/MSAA4/.NET11 RC3/Speed,three independent run-average medians:camera1093.47FPS,entity motion1044.80FPS,best single-run average1180.66FPS. All3runs per scenario exceed1000;this is not a per-frame1ms guarantee. GPU NET10/11each438,direct414,status panel9each,equivalent-visible-window runtime12/12,and current resource2/2checks pass. See [final results](dynamic-3d-layers.md#final-verification-results--2026-11-03) for design,commands,raw measurements and limits. Later stage-by-stage numbers are historical.

The common launcher's PowerShell automatic-variable collision that dropped extra build options is fixed(`InputArguments`,regression7/7). Explicitly select the native speed profile with `-ExtraArgs @('-p:OptimizationPreference=Speed')`. Single-sample opaque edges also use real depth coverage;default4x provides smooth curves. This architecture does not substitute per-layer bitmap caches.
## Purpose

Use this document as the primary reference when writing Duxel code.

This guide is intentionally written for both:

- coding agents that need one practical reference before generating or modifying Duxel code
- developers who want a compact but high-coverage handbook for the current Duxel surface area

This guide is designed to be strong enough that an agent can generate ordinary Duxel code without inspecting the source tree first.

For normal app code, sample code, DSL examples, and reusable widgets, prefer this document first. Use source inspection only when the task is unusually deep, renderer-internal, or platform-internal.

When this document conflicts with the current source code, prefer the current source code and then update this document in the same change set.

## Freshness rule

Treat this file as a living reference.

When project structure, sample layout, public API direction, runtime workflow, or documentation priority changes, update this document together with the implementation.

Always keep the `Last synced` date at the top aligned with the most recent meaningful update to this document.

## What Duxel is

Duxel is an immediate-mode GUI framework for .NET 8, .NET 9, and .NET 10.

Current implementation direction:

- renderer: Vulkan
- primary platform: Windows-native backend
- runtime style: NativeAOT-friendly
- main UI API: `UiImmediateContext`
- app lifecycle entry: `UiScreen.Render(UiImmediateContext ui)`
- package surface:
  - `Duxel.App`
  - `Duxel.Windows.App`

Both packages ship `net8.0`, `net9.0`, and `net10.0` runtime assets. FBA samples remain `net10.0` because the file-based app feature requires the .NET 10 SDK; ordinary project consumers may target any of the three frameworks.

This is the `0.2.12-preview` public package contract. `Duxel.App` and `Duxel.Windows.App` expose `net8.0`, `net9.0`, and `net10.0` assets; FBA remains a .NET 10 SDK workflow.

For Windows app generation, prefer `net8.0-windows7.0`, `net9.0-windows7.0`, or `net10.0-windows7.0` in the consuming executable so platform analysis is explicit. The Duxel package projects themselves intentionally use portable `net8.0`, `net9.0`, and `net10.0` TFMs.

`Duxel.Windows.App` is the single-package Windows entry and depends on `Duxel.App`. Its package dependency must allow analyzer assets to flow so the integrated `Duxel.Core.Dsl.Generator` remains available to Windows-package consumers.

## Standalone generation rule

If you are a coding agent and the user asks for Duxel code generation:

1. start from this document
2. choose one of the templates in this document
3. use the capability map and cookbook sections in this document
4. inspect source code only if the request targets internals not covered here

For most user-facing Duxel work, this document should be sufficient.

## Source-of-truth order

When you need authoritative context, read in this order:

1. this file: `docs/duxel-agent-reference.md`
2. `README.md`
3. task-specific docs:
   - `docs/extended-title-bar-guide.md`
   - `docs/fba-reference-guide.md`
   - `docs/fba-run-samples.md`
   - `docs/getting-started-fba.md`
   - `docs/custom-widgets.md`
   - `docs/ui-dsl.md`
4. design baseline:
   - `docs/design.ko.md`
5. current source code under `src/` when you need deeper verification or unsupported internals

## Repository map

Top-level areas that matter most:

- `src/`
  - `Duxel.Core` — immediate-mode core, widgets, layout, text, state, draw data, DSL runtime
  - `Duxel.App` — app-facing facade and shared runtime wiring
  - `Duxel.Windows.App` — Windows runner entry surface
  - `Duxel.Platform.Windows` — Windows platform backend
  - `Duxel.Vulkan` — Vulkan renderer backend
- `samples/`
  - `samples/Duxel.Sample` — project-style reference app
  - `samples/fba/*.cs` — file-based app samples
- `docs/` — user and contributor documentation
- `global.json` — selects installed .NET SDK `11.0.100-rc.3.26522.148`, allows prereleases and retains patch-only roll-forward
- `run-fba.ps1` — contributor path for local-source FBA validation

## Architecture boundaries

Preserve these boundaries when adding or modifying features:

- `Duxel.Core`
  - owns immediate-mode behavior, layout, widgets, draw list generation, state, text-facing APIs
- `Duxel.Platform.Windows`
  - owns Windows-specific behavior such as input, clipboard, IME/TSF integration, text backend integration, and native windowing
- `Duxel.Vulkan`
  - owns rendering, GPU resources, swapchain flow, and submission
- `Duxel.App` and `Duxel.Windows.App`
  - own app bootstrapping, developer-facing entry flow, option validation, and runtime wiring

Do not casually collapse these boundaries.

### Interactive Windows resize flow

During a Win32 move/size modal loop, `Duxel.App` treats `IPlatformBackend.IsInteractingResize` as continuous work. `Duxel.Platform.Windows` caches the actual client dimensions committed by `WM_SIZE` and increments the resize sequence there. `WM_SIZING` requests a frame without predicting framebuffer dimensions or waiting for the GPU. Hosts with an independent render thread and invalidation callback join matching-frame submission in committed `WM_SIZE`, servicing WSI synchronous messages through `QS_SENDMESSAGE`/`PM_QS_SENDMESSAGE`. Exit/renderer failures cancel the wait. Fresh committed dimensions remain available after the gesture ends. See [correction,user acceptance and remaining stability checks](windows-resize-coherence.md).

Queued draw data whose dimensions no longer match the committed framebuffer is rejected and retried with fresh layout. Matching frames recreate the swapchain at the committed pixel extent during sizing; clipping is not scaled to substitute for rebuilding at the actual size. Recreation polls graphics completion with zero timeout before entering WSI; an unfinished recreation remains pending across idle skipping. Prior views/MSAA/depth targets retire after their last graphics submission completes. Old swapchains and semaphores use supported presentation fences or the portable new-image reacquisition proof. The old handle is passed through `VkSwapchainCreateInfoKHR.oldSwapchain`; unrelated renderer resources are preserved.

MSAA capacity is retained while sufficient during live sizing and trimmed to the exact extent afterwards, without reducing pixel resolution or samples. VSync ON uses FIFO; VSync-off prefers Immediate. Do not add a fixed240 FPS cap. See [Panel and live-resize evidence](panels-root-content.md) for RC3 stage measurements,942 byte-identical GPU image comparisons and remaining memory/visual limits.

A valid frame with a non-zero framebuffer remains renderable even when its draw lists contain no geometry. The renderer must still record the clear, submit, and present so an empty first screen can become visible. This does not disable normal idle-frame skipping: unchanged frames are still filtered by the app runtime before renderer submission.

## Primary public entry points

The most important downstream-facing surfaces are:

- `UiScreen`
- `UiImmediateContext`
- declarative UI API:
  - `IUiView`
  - `DuxelView`
  - `Dux`
  - `DuxelView.Display`
- `DuxelAppOptions`
- `DuxelWindowOptions`
- `DuxelRendererOptions`
- `DuxelFontOptions`
- `DuxelFrameOptions`
- `DuxelDebugOptions`
- compiled design API:
  - `UiCompiledDesign`
  - `IUiDesign`
  - `UiWindows11Design`
- `DuxelWindowsApp.Run(...)`
- custom widget API:
  - `IUiCustomWidget`
  - `MarkdownEditorWidget`
  - `MarkdownViewerWidget`

## Option reference with defaults

These defaults are safe to rely on when generating ordinary Duxel code.

### `DuxelAppOptions`

| Property | Type | Default | Purpose |
|---|---|---|---|
| `Window` | `DuxelWindowOptions` | `new()` | window title, size, vsync |
| `Renderer` | `DuxelRendererOptions` | `new()` | validation, profile, AA, text renderer |
| `Font` | `DuxelFontOptions` | `new()` | font paths, atlas sizing, startup glyph strategy |
| `Frame` | `DuxelFrameOptions` | `new()` | line height, idle frame skip, font rebuild cadence |
| `Debug` | `DuxelDebugOptions` | `new()` | logging and frame capture |
| `Theme` | `UiTheme` | `UiCompiledDesign.Default.Theme` | fallback theme preset |
| `Design` | `UiCompiledDesign?` | `null` | optional compiled design; overrides platform default theme tracking when set |
| `FontTextureId` | `UiTextureId` | `new(1)` | font texture slot |
| `WhiteTextureId` | `UiTextureId` | `new(2)` | white texture slot |
| `Screen` | `UiScreen` | (required) | immediate-mode app entry |
| `Clipboard` | `IUiClipboard?` | `null` | direct clipboard injection |
| `ImageDecoder` | `IUiImageDecoder?` | `null` | custom image decode path |
| `KeyRepeatSettingsProvider` | `IKeyRepeatSettingsProvider?` | `null` | custom key repeat timing |
| `ClipboardFactory` | `Func<IPlatformBackend, IUiClipboard?>?` | `null` | platform-aware clipboard factory |

### Compiled design

By default, Windows apps resolve to `UiCompiledDesign.Windows11` or `UiCompiledDesign.Windows11Dark` from the current OS app theme, and the active design updates when Windows sends an app theme change. Use `DuxelWindowsApp.Run<TDesign>(...)`, `DuxelApp.Options<TDesign>(...)`, or `DuxelAppOptions.Design` when the visual shape of controls must be fixed by code or source generation instead of runtime theme parsing.

```csharp
DuxelWindowsApp.Run<UiWindows11Design>(
    new ProductScreen(),
    title: "Windows 11 styled Duxel",
    width: 980,
    height: 700);
```

For custom compile-time designs, implement `IUiDesign` and pass the generated/static value:

```csharp
public readonly struct ProductDesign : IUiDesign
{
    public static UiCompiledDesign Create()
        => UiCompiledDesign.Windows11 with
        {
            Theme = UiTheme.GitHubDark,
            Tokens = UiDesignTokens.Windows11 with { ControlCornerRadius = 6f }
        };
}

DuxelWindowsApp.Run<ProductDesign>(
    Dux.App(new ProductScreen()),
    title: "Product Surface");
```

`UiTheme` remains the color palette. `UiStyle` remains layout sizing. `UiDesignTokens` controls widget shape such as corner radius, border width, pressed offset, focus ring thickness, and related control geometry. Existing runtime theme changes update colors only; compiled design tokens remain the active shape contract.

When `Design` is `null` and `Theme` is left at its default, Duxel follows the platform theme provider. Explicit `Theme` or `Design` values are treated as app-authored choices and are not overwritten by OS theme changes.

The next homework is a skin layer that can replace rendering strategy per control type. Today, themes, tokens, modifiers, and custom widgets can adjust much of the appearance, but there is not yet an official design-level layer for swapping the built-in rendering policy of controls such as `Button`, `TextField`, `Segmented`, and `Scrollbar`. Start the next session from the [Declarative Control Skin Roadmap](declarative-control-skin-roadmap.md).

### Declarative UI

For SwiftUI/Compose-style composition in C#, use `IUiView` nodes created through the grouped `DuxelView` factories. This keeps dot-completion useful: `DuxelView.Layout`, `DuxelView.Controls`, `DuxelView.Text`, `DuxelView.Display`, `DuxelView.Menus`, and `DuxelView.Windows` expose the common authoring surface without putting every helper into one large class. For compact app code and samples, the `Dux.*` aliases expose the same surface.

```csharp
var running = Dux.State(true);
var project = Dux.State("Duxel Control Surface");
var progress = Dux.State(0.62f);
var channel = Dux.State(ReleaseChannel.Preview);
var tabItems = new[] { "Layout", "Theme", "Windows" };

var screen = Dux.Screen(
    Dux.Group(
        Dux.MainMenuBar(
            Dux.Menu(
                "File",
                Dux.MenuItem("Reset", () => progress.Value = 0f),
                Dux.MenuItem("Running", () => running.Value = !running.Value, selected: () => running.Value))),
            Dux.VStack(
                10f,
                Dux.Header(
                    () => project.Value,
                    Dux.Meta("Preview"),
                    Dux.Meta(() => running.Value ? "Running" : "Paused", UiTextTone.Success)),
                Dux.Tabs(
                    "dashboard-tabs",
                    Dux.Tab(
                        "Overview",
                        Dux.Section(
                            "Controls",
                            Dux.Form(
                                Dux.Field("Project", Dux.TextField("project", project)),
                                Dux.Field("Channel", Dux.EnumSegmented<ReleaseChannel>("channel", channel)),
                                Dux.Field("Running", Dux.Toggle("running", running)),
                                Dux.Field("Progress", Dux.Slider("progress", progress, 0f, 1f).ItemWidth(360f))))),
                    Dux.Tab(
                        "Tasks",
                        Dux.List(
                            "task-list",
                            tabItems,
                            item => Dux.Text(item),
                            new UiVector2(0f, 120f),
                            border: true))))));

enum ReleaseChannel
{
    Stable,
    Preview,
    Canary
}
```

`UiState<T>` is the ergonomic state holder for app code. It converts to `UiBinding<T>` for controls, so `Dux.Checkbox("Running", running)` and `Dux.TextField("project", project)` stay concise. The declarative aliases `Dux.TextField(...)`, `Dux.TextArea(...)`, `Dux.NumberField(...)`, and `Dux.Slider(...)` take stable control ids and hide immediate-mode `##` labels from app code. Use `state.Set(value)` and `state.Update(current => next)` for explicit state changes. Use `Dux.Bind(get, set)` when state already lives somewhere else, and `UiBinding<T>.Map(...)` for writable derived bindings. Dynamic text and conditional views accept `Func<T>` so values are evaluated during rendering. `UiComponent` is the base class for reusable view objects that implement `Build()`.

For app-scale surfaces, prefer small `UiComponent` classes over one large render method. A component owns or receives state in its constructor and returns an `IUiView` from `Build()`. The recommended startup shape is `DuxelWindowsApp.Run<ProductDesign>(Dux.App(new ProductScreen(...)))`, which binds the compiled design and root component before the first frame. `Dux.App(...)` is the semantic alias for wrapping a declarative root view into the `UiScreen` expected by the runtime.

Use `Dux.AppShell(...)` for the common product surface shape: an optional menu, a themed sidebar, a dynamic header, meta text, command content, and the selected page body. Navigation items are authored with `Dux.NavItem(...)`. The active page can be controlled by a regular `UiState<int>`/`UiBinding<int>`, but enum-backed `UiState<T>` keeps app navigation type-safe.

```csharp
Dux.AppShell(
    () => projectName.Value,
    selectedPage,
    [
        Dux.NavItem(ProjectPage.Overview, "Overview", new OverviewPage(state), "Run controls"),
        Dux.NavItem(ProjectPage.Tasks, "Tasks", new TasksPage(state), () => $"{tasks.Count} active rows"),
        Dux.NavItem(ProjectPage.Notes, "Notes", new NotesPage(state), "Composition notes")
    ],
    new UiAppShellOptions(WindowTitle: "Workspace", SidebarTitle: "Workspace"),
    Dux.Meta(() => $"Runs {runs.Value}"),
    Dux.Meta(() => channel.Value));
```

High-level layout helpers describe product surfaces in the same shape users see on screen: `Dux.Section(...)` for titled areas, `Dux.Form(...)` and `Dux.Field(...)` for labeled settings, `Dux.Grid(...)` for repeated card/metric layouts, `Dux.List(...)` for repeated rows, `Dux.Tabs(...)` / `Dux.Tab(...)` for tabbed flows, `Dux.Tree(...)` for hierarchical content, and `Dux.Table(...)` / `Dux.TableColumn(...)` for structured data. For dynamic lists and grids, prefer keyed overloads such as `Dux.List("tasks", tasks, task => task.Id, task => Dux.StatusRow(task.Name, task.Owner, () => task.Progress))` or `Dux.Grid(tasks, task => task.Id, task => new TaskTile(task))`; `Dux.Key(...)` and `.Key(...)` expose the same stable ID scope for individual view fragments.

Declarative control flow stays inside the view tree. Use `Dux.When(...)` and `Dux.Unless(...)` for one-sided conditional content, `Dux.Switch(...)` with `Dux.Case(...)` for state-specific fragments, and `Dux.ForEachIndexed(...)` when the displayed row needs its index. These helpers evaluate dynamic values during rendering, so app code can keep state changes in `UiState<T>` while the view remains a pure description of the current screen.

Display helpers make common product UI fragments concise. Use `Dux.AppShell(...)` for sidebar-based product shells, `Dux.Header(...)` for product title plus meta rows, `Dux.Surface(...)` or `Dux.Card(...)` for padded themed panels, `Dux.SettingsGroup(...)` with `Dux.Setting(...)` for labeled settings, `Dux.Callout(...)` for semantic status/help messages, `Dux.EmptyState(...)` for empty/disabled states, `Dux.Toolbar(...)` for compact command rows, `Dux.MetaBar(...)` with `Dux.Meta(...)` for compact status rows, `Dux.PropertyList(...)` with `Dux.Property(...)` for detail key/value lists, `Dux.StatusRow(...)` for selectable progress rows, `Dux.Badge(...)` for semantic status pills (`UiBadgeTone.Neutral`, `Accent`, `Success`, `Warning`, `Danger`), and `Dux.MetricCard(...)` or `Dux.Metric(...)` for dashboard numbers. Use `Dux.PrimaryButton(...)`, `Dux.DangerButton(...)`, or `.ButtonRole(...)` to give commands a product action hierarchy without manually pushing button colors. Surface and child content keeps the regular Duxel scrollbar behavior by default. The grouped factory surface is `DuxelView.Display`.

```csharp
Dux.Grid(
    3,
    Dux.MetricCard("Runs", () => runs.Value.ToString(), "Queued sessions"),
    Dux.MetricCard("Progress", () => $"{progress.Value:P0}", "Current workflow"),
    Dux.MetricCard(
        "Priority",
        () => priority.Value.ToString(),
        () => priority.Value >= 4 ? "Needs attention" : "Normal cadence",
        new UiMetricCardOptions(ValueTone: UiTextTone.Warning)));
```

For product commands, prefer `Dux.CommandBar(...)` when the action role, tooltip, and enabled state matter. It keeps command semantics in values and lets the compiled design style the buttons:

```csharp
Dux.CommandBar(
    Dux.Command("Queue", QueueRun, UiButtonRole.Primary, tooltip: () => "Queue one run"),
    Dux.Command("Reset", Reset, enabled: () => canReset.Value));
```

Settings surfaces can use `Dux.SettingsGroup(...)` so each row owns its label, description, and control:

```csharp
Dux.SettingsGroup(
    Dux.Setting("Project", Dux.TextField("project", project), "Shown in the workspace shell."),
    Dux.Setting("Channel", Dux.EnumSegmented<ReleaseChannel>("channel", channel)));
```

Selection controls include `Dux.Segmented(...)` for compact mode/channel pickers backed by `UiBinding<int>` or typed `UiBinding<T>`. Use `Dux.Choice(value, label)` for typed choices and `Dux.EnumSegmented<TEnum>(...)` when the state is an enum. These are available alongside `Dux.Combo(...)`, `Dux.ListBox(...)`, `Dux.RadioButton(...)`, and `Dux.Selectable(...)`.

`UiModifier` and fluent extension methods provide local, compile-time-authored visual rules. Common modifiers include `.FontSize(...)`, `.Foreground(...)`, `.Tone(...)`, `.Accent()`, `.Success()`, `.Warning()`, `.Danger()`, `.Muted()`, `.Title(...)`, `.Subtitle(...)`, `.Caption(...)`, `.ItemWidth(...)`, `.FillWidth()`, `.Padding(...)`, `.Frame(...)`, `.FillFrameWidth(...)`, `.Width(...)`, `.Height(...)`, `.Background(...)`, `.Border(...)`, `.CornerRadius(...)`, `.Tooltip(...)`, `.VisibleIf(...)`, `.Disabled(...)`, `.StyleColor(...)`, and `.StyleVar(...)`. Text hierarchy can be authored through `Dux.Title(...)`, `Dux.Subtitle(...)`, `Dux.Caption(...)`, or reusable `UiTextStyle` values. Semantic text tone uses `UiTextTone` so app code can say `.Success()` or `.Warning()` instead of carrying color constants. Use the shape modifiers when a view should own its rendered appearance instead of relying on a runtime theme slot:

```csharp
Dux.Callout(
    "Run Status",
    () => status.Value,
    options: new UiCalloutOptions(Tone: UiTextTone.Success, Height: 88f));
```

`.Panel(...)` is the built-in Windows 11-style surface modifier. It uses `UiPanelStyle` internally with `UiStyleColor.FrameBg` / `UiStyleColor.Border`, `UiDesignToken.ControlCornerRadius`, 14 px padding, and fills the available content width by default. Use `Dux.Panel(content, ...)` when a factory call reads better than a fluent modifier. Reusable compiled styles are regular C# types; implement `IUiViewStyle` when the same visual rule should be named, tested, and reused like a SwiftUI `ViewModifier`. Styles can use `UiStyleColor` and `UiDesignToken` slots so their colors and radius come from the active compiled design while the view shape remains authored in C#:

```csharp
readonly record struct DashboardPanelStyle(float Height = 0f) : IUiViewStyle
{
    public IUiView Apply(IUiView view)
        => view
            .Padding(14f)
            .Background(UiStyleColor.FrameBg)
            .Border(UiStyleColor.Border)
            .CornerRadius(UiDesignToken.ControlCornerRadius)
            .FillFrameWidth(Height);
}

Dux.TextWrapped(() => status.Value)
    .Style(new DashboardPanelStyle(116f));
```

`Frame` supplies stable layout dimensions, `FillFrameWidth` fills the current content width, and `Background`, `Border`, and `CornerRadius` describe the compiled view shape. `Background` and `Border` accept either explicit `UiColor` values or semantic `UiStyleColor` slots from the active design. `CornerRadius` accepts either a fixed float or a semantic `UiDesignToken`. Padding placed before a shape modifier becomes the decorated view's internal padding, so both left and right padding participate in the measured shape. `IUiViewStyle` composes those same modifiers into a compile-time-authored style type; `.Style<TStyle>()` applies a default struct style and `.Style(style)` applies a configured style instance. For repeated dynamic content, keep using keyed list/grid overloads or wrap the decorated fragment with `.Key(...)` so local interaction state stays stable. Declarative menu composition is available through `Dux.MainMenuBar(...)`, `Dux.Menu(...)`, and `Dux.MenuItem(...)`. `DuxelView.Custom(ui => ...)` is the extension point for custom declarative nodes that need direct `UiImmediateContext` access.

### `DuxelWindowOptions`

| Property | Default |
|---|---|
| `Width` | `1280` |
| `Height` | `720` |
| `Title` | `"Duxel"` |
| `VSync` | `true` |
| `IconPath` | `null` (Windows default application icon) |
| `IntegrateSystemChrome` | `true` |
| `TitleBarMode` | `DuxelTitleBarMode.Default` |
| `UseDuxelTitleBar` | `true` |
| `DuxelTitleBarHeight` | `48f` |

`IntegrateSystemChrome` applies Windows 11 DWM caption, text, border color, rounded corner, and dark-mode attributes from the active startup theme/design. When the app uses the default platform-following design, Windows `WM_SETTINGCHANGE` theme notifications refresh the Duxel theme and render clear color at runtime.

Windows creates and configures the native HWND without `WS_VISIBLE`. `WindowCreated` still receives the valid hidden HWND for setup and diagnostics, but applications should not show it themselves. After the renderer successfully presents frame 1, `IPlatformBackend.NotifyFirstFramePresented()` posts a private window message; the Windows UI thread then calls `ShowWindow` and `UpdateWindow`. This ordering prevents Vulkan device, pipeline, font, and first-screen work from appearing as an empty native window.

`UseDuxelTitleBar` is enabled by default, so Windows apps remove the native caption and render a Duxel-owned title bar inside the Vulkan surface unless the app explicitly sets `UseDuxelTitleBar = false`. The app runtime wraps the user `UiScreen`, reserves the top viewport inset, draws the app icon, title, minimize/maximize/close buttons, and delegates window move/minimize/maximize/close commands through `IWindowChromeController`. This is library-owned chrome: application screens do not draw or synchronize these buttons. The Maximize button reads `IWindowChromeController.IsMaximized` every frame, displays one square while restored, changes to the overlapping-squares Restore glyph while maximized, and returns to the Maximize glyph after restore.

`TitleBarMode` explicitly selects `System`, `Duxel`, or `ExtendedContent`. Its `Default` value preserves source compatibility by resolving from `UseDuxelTitleBar`; an explicit non-default mode takes precedence. `ExtendedContent` makes the complete Vulkan client area available from `(0, 0)`. Duxel draws visible caption glyphs over the opaque Vulkan surface while Windows/DWM retains the native button hit codes, commands, system menu, resize, maximize, and Snap Layout contracts.

`ExtendedContent` is a library feature, not behavior implemented only by an FBA sample. The supported application surface is:

This is the current repository-source/local-package contract. Do not claim that a public NuGet version contains the APIs until that exact package version has been published and verified.

| Public library API | Role |
|---|---|
| `DuxelTitleBarMode.ExtendedContent` | Enables the library-owned extended client/frame path |
| `ui.TryGetWindowIcon(out UiImageTexture)` | Returns the effective icon already used by the native window and taskbar |
| `ui.TryGetCaptionButtonBounds(out UiRect)` | Returns Duxel's right-anchored logical caption cluster at `Y = 0` with `DuxelTitleBarHeight` |
| `ui.SetTitleBarDragRegions(ReadOnlySpan<UiRect>)` | Delegates the supplied non-interactive spans to the Windows backend as native caption hit regions |

The library owns DWM frame extension, the shared `DuxelCaptionButtonRenderer`, native caption hit codes, Snap Layout compatibility, non-client hover/pressed state, system styles/menu, DPI conversion, resize borders, and maximized monitor work-area sizing. `Duxel` and `ExtendedContent` therefore use identical 48-pixel button placement and glyph geometry. The public cluster remains at logical `Y = 0` with `DuxelTitleBarHeight` in restored and maximized states; the maximized DWM metadata value such as `Y = -8` and its native 30-pixel height are not used for Duxel visual placement. Application code owns only the visual arrangement of its icon, tabs, search, menus, title text, and empty drag gaps. In `samples/fba/extended_title_bar_fba.cs`, `ExtendedTitleBarScreen` demonstrates this public application contract with tab-shaped Home/Documents controls; the centered empty-area label is intentional sample UI. `ExtendedTitleBarDiagnostics` is verification-only P/Invoke code and must not be treated as required production implementation. Coding agents should use the public APIs instead of recreating Win32 caption behavior. They must visually verify button pixels and the icon right-click system menu because successful Win32 hit-test diagnostics alone are insufficient. If a request needs only conventional Duxel chrome, select `DuxelTitleBarMode.Duxel`; select `ExtendedContent` only when application UI must occupy `y = 0`.

In `ExtendedContent`, query the Duxel-owned button cluster and replace the platform drag-region snapshot from the current UI frame:

```csharp
if (ui.TryGetCaptionButtonBounds(out var captionButtons))
{
    var dragLeft = 320f;
    var dragRight = captionButtons.X;
    ui.SetTitleBarDragRegions(dragRight > dragLeft
        ? [new UiRect(dragLeft, 0f, dragRight - dragLeft, 48f)]
        : []);
}
else
{
    ui.SetTitleBarDragRegions([]);
}
```

The rectangles use Duxel logical client coordinates. Each call atomically replaces the previous region set; pass `[]` to clear it. Keep tabs, buttons, menus, text inputs, and other interactive controls outside those rectangles so their hit test remains `HTCLIENT`. `TryGetCaptionButtonBounds` can return `false` before a usable client width is available. See `docs/extended-title-bar-guide.md` for the complete layout, Win32/DWM contract, and AI validation workflow, and `samples/fba/extended_title_bar_fba.cs` for the executable reference sample.

`IconData` takes precedence over `IconPath`. When neither is set, Duxel explicitly assigns the Win32 system `IDI_APPLICATION` icon to the window class so the native window, taskbar, and Alt+Tab do not reselect a Duxel-branded icon from the executable or icon cache. The Duxel-owned title bar converts that same effective `HICON` to RGBA through WIC and receives it from `IWindowIconProvider.GetWindowIconImage()`, so its icon is identical to the window icon rather than a separately selected fallback. Its title is left-aligned after the icon by default. Right-clicking the icon, title, or another non-button point in the title bar opens the native window system menu; dragging, title-bar double-click maximize/restore, and Alt+Space remain available. `Duxel.Windows.App` no longer injects a branded `ApplicationIcon`; set the application's `ApplicationIcon`, `IconPath`, or `IconData` explicitly when branding is required. An explicit missing `IconPath` or invalid `IconData` fails at startup instead of silently changing the icon.

An `ExtendedContent` screen can call `ui.TryGetWindowIcon(out var windowIcon)` and draw that texture in its application-owned title bar. This exposes the already resolved effective window icon and keeps the in-content icon consistent with the taskbar instead of requiring the sample or application to select another image. Registering the icon rectangle as a drag region also gives it native caption semantics; the Windows backend routes its non-client right-click to the standard system menu. For a copyable developer bootstrap, complete screen implementation, library/sample responsibility table, and coding-AI completion workflow, use `docs/extended-title-bar-guide.md`.

### `DuxelRendererOptions`

| Property | Default |
|---|---|
| `MinImageCount` | `3` |
| `EnableValidationLayers` | `Debugger.IsAttached` |
| `Profile` | `DuxelPerformanceProfile.Display` |
| `MsaaSamples` | `0` |
| `EnableGlobalStaticGeometryCache` | `true` |
| `FontLinearSampling` | `false` |
| `TextRendering` | `DuxelTextRenderingMode.DirectText` |

`DuxelTextRenderingMode.DirectText` is the default text path because the atlas output is not the preferred visual baseline. Use explicit `Atlas` only for atlas renderer A/B profiling. Use explicit `Auto` only when a sample should stay atlas-first but needs immediate DirectText missing-glyph visual fallback before the atlas catches up.

When touching the DirectWrite atlas glyph path, keep oversampled rasterization metrics in logical pixels before they reach the atlas. Bitmap data may be rasterized at `fontSize * oversample`, but glyph advances, text-run advances, offsets, and baselines must be normalized back to the logical em size after downsampling. Bump the font-atlas disk cache version whenever cached placement metrics change.

### `DuxelFontOptions`

| Property | Default |
|---|---|
| `PrimaryFontPath` | Windows `segoeui.ttf` |
| `SecondaryFontPath` | Windows `malgun.ttf` |
| `SecondaryScale` | `1f` |
| `FastStartup` | `true` |
| `UseBuiltInAsciiAtStartup` | `true` |
| `StartupBuiltInScale` | `2` |
| `StartupBuiltInColumns` | `16` |
| `StartupFontSize` | `16` |
| `StartupAtlasWidth` | `512` |
| `StartupAtlasHeight` | `512` |
| `StartupPadding` | `1` |
| `StartupOversample` | `1` |
| `StartupRanges` | ASCII range `0x20..0x7E` |
| `StartupGlyphs` | empty |
| `FontSize` | `16` |
| `AtlasWidth` | `1024` |
| `AtlasHeight` | `1024` |
| `Padding` | `2` |
| `Oversample` | `2` |
| `InitialRanges` | ASCII range `0x20..0x7E` |

### `DuxelFrameOptions`

| Property | Default |
|---|---|
| `LineHeightScale` | `1.2f` |
| `PixelSnap` | `true` |
| `UseBaseline` | `true` |
| `FontRebuildMinIntervalSeconds` | `0.25` |
| `FontRebuildBatchSize` | `16` |
| `EnableIdleFrameSkip` | `true` |
| `IdleSleepMilliseconds` | `2` |
| `IdleWakeCheckMilliseconds` | `1000` |
| `IdleEventWaitMilliseconds` | `0` |
| `WindowTargetFps` | empty dictionary |
| `IsAnimationActiveProvider` | `null` |

### `DuxelDebugOptions`

| Property | Default |
|---|---|
| `Log` | `null` |
| `LogEveryNFrames` | `60` |
| `LogStartupTimings` | `false` |
| `CaptureOutputDirectory` | `null` |
| `CaptureFrameIndices` | empty |

With `LogStartupTimings = true`, the startup log also reports `PipelineCache[Hit]`, `PipelineCache[Miss]`, `PipelineCache[Incompatible]`, `PipelineCache[Saved]`, `PipelineCache[Unchanged]`, and cache I/O-unavailable states. Use these events with `StartupClear` and `FirstFramePresented` to distinguish cache behavior from device initialization and first-screen rendering.

## Copy-paste starter templates

Use these templates directly when generating code.

### Minimal FBA immediate-mode app

```csharp
#:property TargetFramework=net10.0
#:property platform=windows
#:package Duxel.$(platform).App@*-*

using Duxel.App;
using Duxel.Core;
using Duxel.Windows.App;

DuxelWindowsApp.Run(new DuxelAppOptions
{
  Window = new DuxelWindowOptions
  {
    Title = "Hello Duxel",
    Width = 1280,
    Height = 720,
    VSync = true,
  },
  Screen = new DemoScreen(),
});

public sealed class DemoScreen : UiScreen
{
  private int _count;
  private bool _enabled = true;
  private float _volume = 0.5f;
  private string _name = "Duxel";

  public override void Render(UiImmediateContext ui)
  {
    ui.Text("Hello from Duxel");

    if (ui.Button("Increment"))
    {
      _count++;
    }

    ui.SameLine();
    ui.Text($"Count: {_count}");

    ui.Checkbox("Enabled", ref _enabled);
    ui.InputText("Name", ref _name, 128);
    ui.SliderFloat("Volume", ref _volume, 0f, 1f);

  }
}
```

### Immediate-mode app with customized options

```csharp
using Duxel.App;
using Duxel.Core;
using Duxel.Windows.App;

DuxelWindowsApp.Run(new DuxelAppOptions
{
  Window = new DuxelWindowOptions
  {
    Title = "Configured Duxel App",
    Width = 1440,
    Height = 900,
    VSync = true,
  },
  Renderer = new DuxelRendererOptions
  {
    Profile = DuxelPerformanceProfile.Display,
    EnableValidationLayers = true,
    MsaaSamples = 0,
    EnableGlobalStaticGeometryCache = true,
  },
  Font = new DuxelFontOptions
  {
    FontSize = 16,
    FastStartup = true,
  },
  Frame = new DuxelFrameOptions
  {
    LineHeightScale = 1.2f,
    EnableIdleFrameSkip = true,
  },
  Screen = new DemoScreen(),
});
```

### Minimal custom widget

```csharp
using Duxel.Core;

public sealed class CounterWidget : IUiCustomWidget
{
  private int _count;

  public void Render(UiImmediateContext ui)
  {
    ui.Text("Counter Widget");
    if (ui.Button("Add"))
    {
      _count++;
    }

    ui.SameLine();
    ui.Text($"Value: {_count}");
  }
}
```

### Markdown widget usage

```csharp
using Duxel.Core;

public sealed class MarkdownScreen : UiScreen
{
  private readonly MarkdownEditorWidget _editor = new("editor")
  {
    Label = "Markdown",
    Height = 220f,
    ShowStats = true,
    Text = "# Hello\n\n- item 1\n- item 2",
  };

  private readonly MarkdownViewerWidget _viewer = new("viewer")
  {
    Height = 260f,
    ShowBorder = true,
    ShowStats = true,
  };

  public override void Render(UiImmediateContext ui)
  {
    ui.BeginPanel("Markdown Studio");
    _editor.Render(ui);
    _viewer.Markdown = _editor.Text;
    _viewer.Render(ui);
    ui.EndPanel();
  }
}
```

### Minimal DSL app (project-based)

```csharp
// Program.cs
using Duxel.App;
using Duxel.Core.Dsl;
using Duxel.Windows.App;

var screen = new UiDslScreen("Ui/Main.ui", "Ui/theme.duxel-theme");

DuxelWindowsApp.Run(new DuxelAppOptions
{
  Window = new DuxelWindowOptions { Title = "DSL Demo" },
  Screen = screen,
});
```

## Core app patterns

### Immediate-mode screen pattern

Use `UiScreen` when building a normal app or sample screen.

- override `Render(UiImmediateContext ui)`
- rebuild the UI every frame
- keep your own state on the screen instance or external model

Internal window titles follow the same label/ID convention as controls: `Visible title##stable-id` renders only `Visible title` while the complete string remains the window state identity.

Default recommendation: if the user simply asks for a Duxel sample, generate an immediate-mode `UiScreen` example unless they explicitly ask for DSL.

### DSL pattern

Use the `.ui` DSL when the task is declarative UI authoring, state binding, or generator-based workflows.

- parser: `UiDslParser`
- runtime execution: `UiDslDocument.Emit(...)`
- binding path: `IUiDslValueSource`, `IUiDslEventSink`
- docs: `docs/ui-dsl.md`

### Custom widget pattern

Use instance-based reusable widgets when behavior does not belong on the built-in immediate-mode surface.

- implement `IUiCustomWidget`
- keep widget-local state inside the object when appropriate
- render through `Render(UiImmediateContext ui)`

Current built-in reusable widget references:

- `MarkdownEditorWidget`
- `MarkdownViewerWidget`

## UiImmediateContext capability map

This section is a practical feature inventory for agents and developers.

### Windows and layout

- `Begin(...)` / `End()`
- `BeginChild(...)` / `EndChild()`
- `SameLine()`
- `NewLine()`
- `Spacing()`
- `Separator()`
- `Dummy(...)`
- `Columns(...)`, `NextColumn()`
- table APIs such as `TableSetupColumn(...)`, `TableNextRow()`, `TableNextCell()`
- indentation and width control:
  - `Indent()` / `Unindent()`
  - `SetNextItemWidth(...)`
  - `PushItemWidth(...)` / `PopItemWidth()`
- cursor and region control:
  - `GetCursorPos()` / `SetCursorPos(...)`
  - `GetContentRegionAvail()`

### Text and typography

- `Text(...)`
- `TextColored(...)`
- `TextDisabled(...)`
- `TextWrapped(...)`
- `TextUnformatted(...)`
- `LabelText(...)`
- font control:
  - `PushFont(...)` / `PopFont()`
  - `PushFontSize(...)` / `PopFontSize()`
- alignment helpers and wrapped text behavior are demonstrated in `samples/fba/all_features.cs` and `samples/fba/text_render_validation_fba.cs`

### Buttons and basic choice widgets

- `Button(...)`
- `SmallButton(...)`
- `InvisibleButton(...)`
- `ArrowButton(...)`
- `Checkbox(...)`
- `CheckboxFlags(...)`
- `RadioButton(...)`

Practical signatures:

- `bool Button(string label)`
- `bool Button(string label, UiVector2 size)`
- `bool SmallButton(string label)`
- `bool InvisibleButton(string id, UiVector2 size)`
- `bool ArrowButton(string id, UiDir dir)`
- `bool Checkbox(string label, ref bool value)`
- `bool CheckboxFlags(string label, ref int flags, int flagsValue)`
- `bool RadioButton(string label, bool active)`
- `bool RadioButton(string label, ref int value, int buttonValue)`

### Text input and numeric input

- `InputText(...)`
- `InputTextWithHint(...)`
- `InputTextMultiline(...)`
- `InputInt(...)`, `InputInt2(...)`, `InputInt3(...)`, `InputInt4(...)`
- `InputFloat(...)`, `InputFloat2(...)`, `InputFloat3(...)`, `InputFloat4(...)`
- `InputDouble(...)`, `InputDouble2(...)`, `InputDouble3(...)`, `InputDouble4(...)`
- scalar-array editing APIs where supported by the current surface

Practical signatures:

- `bool InputText(string label, ref string value, int maxLength)`
- `bool InputTextWithHint(string label, string hint, ref string value, int maxLength)`
- `bool InputTextMultiline(string label, ref string value, int maxLength, float height)`
- `bool InputTextMultiline(string label, ref string value, int maxLength, int visibleLines)`
- `bool InputInt(string label, ref int value)`
- `bool InputFloat(string label, ref float value, string format = "0.###")`
- `bool InputDouble(string label, ref double value, string format = "0.###")`

### Sliders and drags

- `SliderFloat(...)`, `SliderFloat2(...)`, `SliderFloat3(...)`, `SliderFloat4(...)`
- `SliderInt(...)`, `SliderInt2(...)`, `SliderInt3(...)`, `SliderInt4(...)`
- `SliderAngle(...)`
- `VSliderFloat(...)`
- `DragFloat(...)`, `DragFloat2(...)`, `DragFloat3(...)`, `DragFloat4(...)`
- `DragInt(...)`, `DragInt2(...)`, `DragInt3(...)`, `DragInt4(...)`
- `DragFloatRange2(...)`
- `DragIntRange2(...)`

Practical signatures:

- `bool SliderFloat(string label, ref float value, float min, float max)`
- `bool SliderInt(string label, ref int value, int min, int max)`
- `bool DragFloat(string label, ref float value, float speed = 0.01f, float min = float.NegativeInfinity, float max = float.PositiveInfinity, string format = "0.###")`
- `bool DragInt(string label, ref int value, float speed = 0.1f, int min = int.MinValue, int max = int.MaxValue)`

### Selection, combos, lists

- `Combo(...)`
- `BeginCombo(...)` / `EndCombo()`
- `ListBox(...)`
- `BeginListBox(...)` / `EndListBox()`
- `Selectable(...)`
- list and selection behavior examples live in `samples/fba/all_features.cs`, `samples/fba/item_status.cs`, and related sample sections

Practical signatures:

- `bool Combo(ref int currentIndex, IReadOnlyList<string> items, int popupMaxHeightInItems = 8, string? id = null)`
- `bool Combo(ref int currentIndex, int itemsCount, Func<int, string> itemsGetter, int popupMaxHeightInItems = 8, string? id = null)`
- `bool BeginCombo(string previewValue, int popupMaxHeightInItems = 8, string? id = null)`
- `bool ListBox(ref int currentIndex, IReadOnlyList<string> items, int visibleItems = 6, string? id = null)`
- `bool Selectable(string label, ref bool selected)`
- `bool Selectable(string label, bool selected)`

Combo popup backgrounds, item rendering, hit testing, and scrollbars share the intersection of the popup bounds and the owning content clip. The intersected height is the scrolling viewport, so a constrained popup can still scroll until its final item is fully visible. Items outside an internal window's content area do not render or receive input through its bottom padding.

### Trees, tabs, menus, popups, tooltips

- `TreeNode(...)`, `TreeNodeId(...)`, `TreePop()`
- `SetNextItemOpen(...)`
- `BeginTabBar(...)` / `EndTabBar()`
- `BeginTabItem(...)` / `EndTabItem()`
- `TabItemButton(...)`
- `BeginMainMenuBar()` / `EndMainMenuBar()`
- `BeginMenuBar()` / `EndMenuBar()`
- `BeginMenu(...)` / `EndMenu()`
- `MenuItem(...)`
- `OpenPopup(...)`
- `BeginPopup(...)` / `EndPopup()`
- `BeginPopupContextItem(...)` and related context-popup helpers
- `BeginTooltip()` / `EndTooltip()`
- `SetTooltip(...)`

Practical signatures:

- `bool BeginMenuBar()`
- `bool BeginMenu(string label)`
- `bool MenuItem(string label, bool selected = false, bool enabled = true)`
- `bool BeginPopup(string id)`
- `bool BeginPopupContextItem(string id)`
- `bool BeginTooltip()`
- `bool BeginTabBar(string id)`
- `bool BeginTabItem(string label)`
- `bool TreeNode(string label, bool defaultOpen = false)`
- `bool TreeNodeEx(string label, UiTreeNodeFlags flags = UiTreeNodeFlags.None)`

### Color, images, and rich visuals

- `ColorButton(...)`
- `ColorEdit3(...)`, `ColorEdit4(...)`
- `ColorPicker3(...)`, `ColorPicker4(...)`
- `Image(...)`
- `ImageWithBg(...)`
- `ImageButton(...)`

For image-centric workflows, start with `samples/fba/image_and_popups.cs` and `samples/fba/image_widget_effects_fba.cs`.

Practical signatures:

- `bool ColorEdit3(string label, ref float r, ref float g, ref float b)`
- `bool ColorEdit4(string label, ref float r, ref float g, ref float b, ref float a)`
- `bool ImageButton(string id, UiTextureId textureId, UiVector2 size, UiColor? tint = null)`

### Progress, plots, and feedback

- `ProgressBar(...)`
- `PlotLines(...)`
- `PlotHistogram(...)`

Practical signature:

- `void ProgressBar(float fraction, UiVector2 size, string? overlay = null)`

### Styling and scoped overrides

- `PushStyleColor(...)` / `PopStyleColor()`
- `PushStyleVar(...)` / `PopStyleVar()`
- `PushId(...)` / `PopId()`
- `BeginDisabled()` / `EndDisabled()`

When generating dynamic lists, manage IDs explicitly to avoid collisions.

### State and interaction queries

- `IsItemHovered()`
- `IsItemActive()`
- `IsItemClicked()`
- `GetItemRectMin()`
- `GetItemRectMax()`
- `GetItemRectSize()`
- mouse and cursor queries through the current immediate context surface

For live behavior inspection, see `samples/fba/input_queries.cs` and `samples/fba/item_status.cs`.

### Custom drawing and layers

Use custom drawing when the built-in widget set is not enough.

`UiLayerOptions.Scene3D = new UiLayer3D(model * view * projection)` projects dynamic layer geometry through the real Vulkan depth pipeline. It shares one rendering scope and swapchain depth attachment across layers; it does not rasterize each layer to an offscreen texture. Dynamic `EndLayer` transfers frame-owned geometry without copying/rebasing and reuses its builder by id. Plain 2D lists retain their ordering. See [dynamic 3D layers](dynamic-3d-layers.md) and `samples/fba/dynamic_3d_layers_bench.cs` for coordinates, clipping, opacity, lifetime, validation, and measurement contracts.

`ContentVersion` retains geometry until its revision changes while camera matrices remain independently dynamic. `UiLayer3D.Viewport` and `DepthScopeId` support independent widget cameras in the same renderer; `TryMapScreenPoint` maps pointer rays to source plane pixels. See `samples/fba/dynamic_layer_camera_fba.cs`. Analytic vector edges rasterize at final resolution on zoom. `IPlatformVectorTextBackend` provides outline meshes for projected native text; ordinary 2D native text and raster images keep their source-resolution limitations. `UiDrawListBuilder.GetClipRect()` exposes the active local clip. `GetRendererFrameMetrics()` returns the previous submitted frame's counters (enable Vulkan profiling for measured counters). `IsMouseDown/Clicked/Released(2)` queries the middle button; `GetKeyModifiers()` supplies Ctrl combinations. Vulkan skips offscreen 3D commands with known bounds while keeping their geometry resident. See [ERD layer lab](erd-layer-lab.md) and `scripts/measure-erd-layers.ps1` for scalable editing and demand-rendering comparisons.

On devices with Vulkan `shaderClipDistance`, a fractional projected viewport uses an integer hardware viewport and a remapped projection. Four clip planes enforce the original fractional bounds for both ordinary and cutout geometry. Devices without the feature, and viewports whose integer envelope would exceed device limits, use the original viewport path. Depth scope identity still uses the requested viewport and scope ID.

Depth-writing rounded fills, gradients, and rounded images can use an explicit 4× sample mask when Vulkan reports standard sample positions and the renderer confirms opaque geometry and texture. Fractional framebuffer viewports also need `shaderClipDistance`. Integer framebuffer viewports within device limits can use the mask with that feature unavailable. The mask applies to both color and depth. Partial texture uploads invalidate the opacity proof.

`UiLayerOptions.Translation` preserves the opacity proof for the geometry. A translucent layer uses ordinary coverage and blending.

- draw-list builder primitives include rectangles, circles, lines, bezier curves, text, images, clip control, texture switching, and callbacks
- layer and canvas-oriented flows are demonstrated by layer bench samples and mixed rendering samples
- retained static draw lists use `StaticGeometryKey` as their cache identity; change the key when retained geometry content changes
- `UiDrawList.StaticGeometryStamp` is the cheap geometry-content stamp used by renderer caches; layer caches populate it automatically, and custom retained static producers may set it with `UiDrawList.ComputeStaticGeometryStamp(...)`
- Layer opacity and append translation are carried through `UiDrawCommand.Opacity`/`UiDrawCommand.Translation` and applied in the Vulkan vertex shaders, so retained static geometry keys/stamps should describe geometry content, not layer opacity or placement
- Use `UiDrawListBuilder.Split(count)` and `SetCurrentChannel(index)` when the producer already knows independent draw groups and can preserve the intended stacking. Choose `MergeChannelsAsDrawLists()` only when those groups can remain separate draw lists and a focused gate proves the extra draw-list boundary helps; `global_dirty_strategy_bench.cs` is a counterexample where copy-free channel output is slower. Use `Merge()` when one physically merged draw list is needed or when the focused gate favors copy-merge. Channel output order is explicit draw order, so do not channelize overlapping content if that changes the desired stacking.

Start here for custom drawing and caching references:

- `samples/fba/all_features.cs`
- `samples/fba/layer_widget_mix_bench_fba.cs`
- `samples/fba/global_dirty_strategy_bench.cs`
- `samples/fba/vector_primitives_bench_fba.cs`

## Cookbook for common generation tasks

### Basic form window

Use this pattern for settings pages, dialogs, and tool panels.

```csharp
public sealed class SettingsScreen : UiScreen
{
  private string _userName = "guest";
  private bool _autoSave = true;
  private int _quality = 2;
  private float _scale = 1.0f;

  public override void Render(UiImmediateContext ui)
  {
    ui.BeginPanel("Settings");

    ui.InputText("User Name", ref _userName, 128);
    ui.Checkbox("Auto Save", ref _autoSave);
    ui.SliderFloat("Scale", ref _scale, 0.5f, 2.0f);

    ui.Text("Quality");
    ui.RadioButton("Low", ref _quality, 0);
    ui.RadioButton("Medium", ref _quality, 1);
    ui.RadioButton("High", ref _quality, 2);

    if (ui.Button("Apply"))
    {
      // commit state
    }

    ui.EndPanel();
  }
}
```

### Menu bar pattern

```csharp
if (ui.BeginMainMenuBar())
{
  if (ui.BeginMenu("File"))
  {
    _ = ui.MenuItem("Open");
    _ = ui.MenuItem("Save");
    _ = ui.MenuItem("Exit");
    ui.EndMenu();
  }

  if (ui.BeginMenu("View"))
  {
    _ = ui.MenuItem("Show Grid", selected: true);
    ui.EndMenu();
  }

  ui.EndMainMenuBar();
}
```

### Popup pattern

```csharp
if (ui.Button("Open Popup"))
{
  ui.OpenPopup("demo-popup");
}

if (ui.BeginPopup("demo-popup"))
{
  ui.Text("Popup content");
  ui.EndPopup();
}
```

### Tab pattern

```csharp
if (ui.BeginTabBar("main-tabs"))
{
  if (ui.BeginTabItem("General"))
  {
    ui.Text("General content");
    ui.EndTabItem();
  }

  if (ui.BeginTabItem("Advanced"))
  {
    ui.Text("Advanced content");
    ui.EndTabItem();
  }

  ui.EndTabBar();
}
```

### Tree pattern

```csharp
if (ui.TreeNode("Assets", defaultOpen: true))
{
  ui.BulletText("Textures");
  ui.BulletText("Shaders");
  ui.BulletText("Fonts");
  ui.TreePop();
}
```

### List and combo pattern

```csharp
private readonly string[] _modes = ["Display", "Render", "Debug"];
private int _modeIndex;

public override void Render(UiImmediateContext ui)
{
  ui.BeginPanel("Selection");
  ui.Combo(ref _modeIndex, _modes, 8, "mode");
  ui.Text($"Current: {_modes[_modeIndex]}");
  ui.EndPanel();
}
```

### Two-column settings pattern

For legacy two-column settings layouts, prefer `Columns(...)` only when matching existing samples. For new structured data, prefer table APIs.

### Custom draw canvas pattern

```csharp
var canvas = ui.BeginWindowCanvas(new UiColor(0xFF101820));
var draw = ui.GetWindowDrawList();

draw.AddRect(canvas, new UiColor(0xFFFFFFFF));
draw.AddLine(
  new UiVector2(canvas.X, canvas.Y),
  new UiVector2(canvas.X + canvas.Width, canvas.Y + canvas.Height),
  new UiColor(0xFF39AFFF),
  2f);

ui.EndWindowCanvas();
```

## Generation rules of thumb

- for quick demos, compose directly in the application work area; use `BeginPanel(...)` / `EndPanel()` only for an explicit collapsible panel
- keep state in private fields on the screen or widget object
- prefer immediate-mode examples over renderer internals unless the user explicitly asks for renderer work
- prefer existing sample naming and patterns over inventing new abstractions
- prefer table APIs for new structured data views
- use `run-fba.ps1` for validating local source behavior in this repository
- if the request is about markdown editing or viewing, prefer the built-in markdown widgets before inventing a custom parser

## Sample-first feature lookup

Prefer an existing sample before inventing a new pattern.

| Need | Start here |
|---|---|
| Broad feature showcase | `samples/fba/all_features.cs` |
| Declarative C# dashboard | `samples/fba/declarative_dashboard_fba.cs` |
| Declarative DSL + Theme | `samples/Duxel.ThemeDemo` |
| Layout and style control | `samples/fba/advanced_layout.cs` |
| Legacy columns | `samples/fba/columns_demo.cs` |
| Images, popups, tooltip patterns | `samples/fba/image_and_popups.cs` |
| Image effects and animated images | `samples/fba/image_widget_effects_fba.cs` |
| Keyboard and mouse query APIs | `samples/fba/input_queries.cs` |
| Item lifecycle and status queries | `samples/fba/item_status.cs` |
| Text alignment and render validation | `samples/fba/text_render_validation_fba.cs` |
| Font style and size validation | `samples/fba/font_style_validation_fba.cs` |
| Layer cache and dirty strategy behavior | `samples/fba/idle_layer_validation.cs`, `samples/fba/layer_dirty_strategy_bench.cs`, `samples/fba/global_dirty_strategy_bench.cs` |
| Draw-layer and widget mixing | `samples/fba/layer_widget_mix_bench_fba.cs` |
| DirectText changing-string performance | `samples/fba/directtext_dynamic_text_bench_fba.cs` |
| Rendering stress | `samples/fba/ui_mixed_stress.cs` |
| Physics/perf benchmark | `samples/fba/Duxel_perf_test_fba.cs` |
| App-scale reference | `samples/Duxel.Sample` |

## Preferred execution workflows

### End-user package workflow

Use package mode when consuming Duxel as a library.

- `dotnet run hello.cs`
- `dotnet run samples/fba/all_features.cs`

FBA samples use package directives such as:

```csharp
#:property TargetFramework=net10.0
#:property platform=windows
#:package Duxel.$(platform).App@*-*
```

### Contributor local-source workflow

Use `run-fba.ps1` when validating local source changes.

Preferred examples:

- `./run-fba.ps1 samples/fba/all_features.cs -NoCache`
- `./run-fba.ps1 samples/fba/all_features.cs -Managed`
- `./run-fba.ps1 samples/fba/perf_2d_render_fps.cs -Wait`

Why:

- it rewrites package directives to local project references
- it normalizes Windows entry calls when needed
- it supports NativeAOT by default
- it can wait for NativeAOT GUI samples to exit with `-Wait`, which is required for automated file-log benchmark collection
- it reflects workspace source changes, unlike plain package-mode execution
- it resolves sample/assets/output files as literal paths; repository scripts enumerate literal directories and filter file properties instead of passing filesystem wildcards to PowerShell providers

When invoking a PowerShell script programmatically, use hashtable splatting for named parameters. Use argument arrays for native executables. Do not pass a string array containing tokens such as `-Path` to a PowerShell script because array splatting can bind them as positional values.

Do not rely on `dotnet run samples/fba/<file>.cs` to validate local source edits.

## Build and validation

Default repository build:

- `dotnet build Duxel.slnx -c Release`

Minimum validation expectation after meaningful code changes:

- build the solution, or
- run at least one relevant sample path

Prefer the smallest relevant validation that still proves the change.

In this repository, also inspect `.github/copilot-instructions.md` and the relevant `.github/skills/*/SKILL.md` files when the task touches .NET, performance, Vulkan, native imports, or FBA samples. Treat them as local project guidance for Codex too, while keeping `AGENTS.md` and explicit user/developer instructions as the active conversation contract.

## Environment variables and runtime toggles

Common runtime toggles used across samples and diagnostics include:

- `DUXEL_APP_PROFILE`
- `DUXEL_PERF_PROFILE`
- `DUXEL_PERF_BENCH_OUT`
- `DUXEL_PERF_BENCH_SECONDS`
- `DUXEL_PERF_INITIAL_POLYGONS`
- `DUXEL_PERF_GLOBAL_STATIC_CACHE`
- `DUXEL_SAMPLE_AUTO_EXIT_SECONDS`
- `DUXEL_IMAGE_PATH`
- `DUXEL_LAYER_BENCH_BACKEND`
- `DUXEL_LAYER_BENCH_OPACITY`
- `DUXEL_LAYER_BENCH_PARTICLES`
- `DUXEL_LAYER_BENCH_LAYOUTS`
- `DUXEL_LAYER_BENCH_PHASE_SECONDS`
- `DUXEL_LAYER_BENCH_DISABLE_FAST_RENDER`
- `DUXEL_LAYER_BENCH_OUT`
- `DUXEL_GLOBAL_DIRTY_BENCH_OUT`
- `DUXEL_GLOBAL_DIRTY_BENCH_PHASE_SECONDS`
- `DUXEL_GLOBAL_DIRTY_BENCH_DENSITY`
- `DUXEL_GLOBAL_DIRTY_BENCH_COLS`
- `DUXEL_GLOBAL_DIRTY_BENCH_ROWS`
- `DUXEL_GLOBAL_DIRTY_CHANNEL_DRAWLISTS`
- `DUXEL_PIPELINE_ORDER_BENCH_OUT`
- `DUXEL_PIPELINE_ORDER_PHASE_SECONDS`
- `DUXEL_PIPELINE_ORDER_ITEMS`
- `DUXEL_STATIC_LAYER_MOVE_ORDER_BENCH_OUT`
- `DUXEL_STATIC_LAYER_MOVE_ORDER_PHASE_SECONDS`
- `DUXEL_STATIC_LAYER_MOVE_ORDER_ITEMS`
- `DUXEL_STATIC_LAYER_MOVE_ORDER_AMPLITUDE`
- `DUXEL_STATIC_CACHE_REBUILD_BENCH_OUT`
- `DUXEL_STATIC_CACHE_REBUILD_PHASE_SECONDS`
- `DUXEL_STATIC_CACHE_REBUILD_WARMUP_SECONDS`
- `DUXEL_STATIC_CACHE_REBUILD_LAYERS`
- `DUXEL_STATIC_CACHE_REBUILD_DENSITY`
- `DUXEL_STATIC_CACHE_REBUILD_GPU_OVERDRAW`
- `DUXEL_DIRECT_TEXT_PAGE`
- `DUXEL_DTPAGE_UPLOAD_BENCH_OUT`
- `DUXEL_DTPAGE_UPLOAD_PHASE_SECONDS`
- `DUXEL_DTPAGE_UPLOAD_PAGE_SIZE`
- `DUXEL_DTPAGE_UPLOAD_REGION_WIDTH`
- `DUXEL_DTPAGE_UPLOAD_REGION_HEIGHT`
- `DUXEL_DTPAGE_UPLOAD_REGIONS`
- `DUXEL_DTPAGE_UPLOAD_PAGES`
- `DUXEL_DTPAGE_UPLOAD_WARMUP_FRAMES`
- `DUXEL_TEXT_RENDERING`
- `DUXEL_VK_COMMAND_SCHEDULER`
- `DUXEL_VK_COMMAND_SCHEDULER_MAX_WINDOW`
- `DUXEL_UI_COMMAND_SCHEDULER`
- `DUXEL_VK_COMMAND_DIAG`
- `DUXEL_VK_COMMAND_DIAG_EVERY`
- `DUXEL_VK_COMMAND_DIAG_FRAMES`
- `DUXEL_VK_COMMAND_DIAG_OUT`
- `DUXEL_VK_FONT_DIAG`
- `DUXEL_VK_FONT_DIAG_OUT`
- `DUXEL_VK_PROFILE`
- `DUXEL_VK_PROFILE_EVERY`
- `DUXEL_VK_PROFILE_OUT`
- `DUXEL_VK_GPU_PROFILE`
- `DUXEL_VK_UPLOAD_BATCH`
- `DUXEL_VK_UPLOAD_QUEUE`
- `DUXEL_VK_STATIC_GEOMETRY_UPDATE`
- `DUXEL_VK_STATIC_GEOMETRY_INPLACE_UPDATE`
- `DUXEL_VK_STATIC_GEOMETRY_ROTATING_UPDATE`
- `DUXEL_VK_STATIC_PRIMITIVE_TRIANGLES`

When a task depends on a runtime toggle, verify the exact name and meaning in the relevant sample before documenting or using it.

For Vulkan command-recording profiling, set `DUXEL_VK_PROFILE=1`. `DUXEL_VK_PROFILE_EVERY` controls the log interval in frames, and `DUXEL_VK_PROFILE_OUT` appends profile lines to a file. Profile lines include `device(vendor=... vid=... did=... type=... name=... gfxQ=... uploadQ=... xferCandQ=... tsBits=... tsPeriodNs=... gpuTs=...)` and `policy(upload=... transferCandidate=... staticPrimTri=... staticUpdate=... staticUpdateReq=... scheduler=... schedWindow=... staticSecondaryMin=...)` so device/vendor gates remain attributable when artifacts from NVIDIA, AMD, Intel, and integrated/discrete devices are compared later. `uploadQ` is the queue family currently used for upload command buffers; `xferCandQ` is only the detected non-graphics transfer-capable candidate. `policy(upload=graphics transferCandidate=1)` therefore means a candidate exists, not that uploads are already using it. `DUXEL_VK_UPLOAD_QUEUE=transfer` opts into the split transfer upload path only when that candidate exists; `policy(upload=transfer transferCandidate=1)` is the profile proof that upload copy command buffers are using it. `staticUpdateReq` is the requested mode and `staticUpdate` is the resolved device policy. `scheduler` is the resolved command-scheduler mode: `disabled`, `static`, or `all`. Profile lines also include `stateUs(pipe=... desc=... buf=... push=... scissor=...)` to separate pipeline binds, descriptor binds, vertex/index/primitive buffer binds, push constants, and scissor sets. `clipCache(calc=... reuse=...)` reports actual scissor rectangle computation count and consecutive same clip/translation reuse count. `staticSec(cand=... cmds=... draws=...)` reports static draw lists that crossed the active `staticSecondaryMin` threshold, plus the recorded commands/draws inside those candidates; `cand=0` means secondary command buffers are not a useful next-path hypothesis for that frame even if static lists exist. `listWork(staticCmd=... dynCmd=... staticDraw=... dynDraw=... staticPipe=... dynPipe=... staticClip=... dynClip=... staticScissor=... dynScissor=... staticPush=... dynPush=... staticGeom=... dynGeom=... staticPrim=... dynPrim=...)` attributes command-recording work to static cached replay versus dynamic draw lists. Use it to decide whether the next bottleneck is static replay policy or dynamic UI ordering/channelization. `staticGeom(hit=... create=... replace=... update=... reuse=... hash=...)` reports static geometry cache reuse, buffer creation, content replacement, same-shape in-place content update, same-shape rotating-buffer reuse, and fallback content hashing in the upload phase. `staticMem(active=... activeBytes=... retired=... retiredBytes=...)` reports active static geometry entries/bytes and retired rotating-pool entries/bytes for memory-pressure checks. `staticPrim(expand=... expandPrim=... layout=... force=... autoSkip=... autoSkipPrim=... autoSkipMut=... expandBytes=... autoSkipBytes=...)` reports per-frame static primitive triangle expansion decisions, which is necessary because `policy(... staticPrimTri=1 ...)` only says the device policy allows the path. `autoSkipMut` is the subset of skipped lists suppressed because the same static geometry tag is actively changing content. `pipeClass(font=... texTri=... colorTri=... texPrim=... colorPrim=... solid=...)` reports the actual pipeline class selected after renderer policy, which is more precise for pipeline-switch analysis than the original source command kind alone. `sched(probe=... hit=... miss=... nochange=... lists=... merged=... us=...)` reports opt-in command scheduler activity and cost. `upSched(sub=... prepSub=... wait=... flush=... bytes=... texRegions=... bufCopies=... submitUs=... prepUs=... waitUs=...)` reports staging upload scheduler submissions, split-transfer graphics-prepare submissions, upload fence waits, batch flushes, staged byte volume, texture copy regions, static/buffer copy commands, and their submission/wait costs. `imgTrans(total=... toDst=... toShader=... present=... color=... xferStage=... gfxStage=... us=...)` reports image layout transition counts and separates transfer-queue-compatible stage masks from barriers that still require graphics stages. `state=` excludes scissor because scissor is also part of the `clip=` bucket, so interpret scissor separately.

Performance demos should match the specific bottleneck being tested. Use broad layer-widget scenes as final regressions, but add or pick a focused FBA demo when the question is narrow. `samples/fba/pipeline_ordering_bench_fba.cs` is the focused gate for dynamic solid/text pipeline ordering cost; it includes alternating, grouped solids-then-text, copy-merge `channelized-solid-text`, and copy-free `channel-drawlists-solid-text` phases and uses `DUXEL_PIPELINE_ORDER_BENCH_OUT`, `DUXEL_PIPELINE_ORDER_PHASE_SECONDS`, and `DUXEL_PIPELINE_ORDER_ITEMS`. `samples/fba/dynamic_widget_ordering_bench_fba.cs` is the focused gate for widget-like dynamic producer ordering with row-clip churn; it includes alternating widget rows, grouped solids-then-text, copy-merge channelized, and copy-free channel-drawlists phases, and uses `DUXEL_DYN_WIDGET_ORDER_BENCH_OUT`, `DUXEL_DYN_WIDGET_ORDER_PHASE_SECONDS`, `DUXEL_DYN_WIDGET_ORDER_WARMUP_SECONDS`, `DUXEL_DYN_WIDGET_ORDER_ITEMS`, and `DUXEL_DYN_WIDGET_ORDER_ROW_CLIPS`. `samples/fba/vector_primitives_bench_fba.cs` is the focused gate for primitive-heavy geometry and accepts `DUXEL_VECTOR_BENCH_WORKLOAD=mixed`, `rect-outline`, or `axis-line`, plus `DUXEL_VECTOR_BENCH_OUT`, `DUXEL_VECTOR_BENCH_PHASE_SECONDS`, and `DUXEL_VECTOR_BENCH_COUNTS`. `samples/fba/Duxel_perf_test_fba.cs` is the polygon physics/perf smoke; it defaults to the Render profile and global static backdrop cache, `DUXEL_PERF_PROFILE=render|display` can override the startup profile, `DUXEL_PERF_GLOBAL_STATIC_CACHE=0` disables the retained static backdrop references, and the Render Profile checkbox applies the profile immediately by switching MSAA between `1x` and `4x`. `samples/fba/global_dirty_strategy_bench.cs` is the focused gate for global static-background caching versus dynamic overlay updates; `DUXEL_GLOBAL_DIRTY_CHANNEL_DRAWLISTS` compares channel copy-merge against separate draw-list output for that exact background/overlay channel structure. `samples/fba/static_layer_moving_order_bench_fba.cs` is the focused gate for moving static-layer replay schedule reuse; it uses `DUXEL_STATIC_LAYER_MOVE_ORDER_BENCH_OUT`, `DUXEL_STATIC_LAYER_MOVE_ORDER_PHASE_SECONDS`, `DUXEL_STATIC_LAYER_MOVE_ORDER_ITEMS`, and `DUXEL_STATIC_LAYER_MOVE_ORDER_AMPLITUDE`. `samples/fba/static_cache_rebuild_bench_fba.cs` is the focused gate for cache replay, false-dirty static rebuilds, mutating static geometry replacement/update cost, Core cache-copy allocation pressure, and static primitive triangle memory pressure; it reports `avgAllocatedBytes` per measured frame and uses `DUXEL_STATIC_CACHE_REBUILD_BENCH_OUT`, `DUXEL_STATIC_CACHE_REBUILD_PHASE_SECONDS`, `DUXEL_STATIC_CACHE_REBUILD_WARMUP_SECONDS`, `DUXEL_STATIC_CACHE_REBUILD_LAYERS`, `DUXEL_STATIC_CACHE_REBUILD_DENSITY`, `DUXEL_STATIC_CACHE_REBUILD_PRIMITIVE_MODE` (`circles`, `rects`, or `mixed`), `DUXEL_STATIC_CACHE_REBUILD_CIRCLE_SEGMENTS`, and optional `DUXEL_STATIC_CACHE_REBUILD_GPU_OVERDRAW` for GPU-bound variants. `samples/fba/texture_upload_barrier_bench_fba.cs` is the focused gate for texture upload copy/barrier behavior before transfer-queue policy changes; it uses `DUXEL_TEXTURE_UPLOAD_BENCH_OUT`, `DUXEL_TEXTURE_UPLOAD_PHASE_SECONDS`, `DUXEL_TEXTURE_UPLOAD_SIZE`, `DUXEL_TEXTURE_UPLOAD_REGION_SIZE`, `DUXEL_TEXTURE_UPLOAD_REGIONS`, `DUXEL_TEXTURE_UPLOAD_TEXTURES`, and `DUXEL_TEXTURE_UPLOAD_WARMUP_FRAMES`. `samples/fba/directtext_page_upload_bench_fba.cs` is the focused gate for DirectText page-style partial texture upload behavior without platform glyph rasterizer cost; it uses `DUXEL_DTPAGE_UPLOAD_BENCH_OUT`, `DUXEL_DTPAGE_UPLOAD_PHASE_SECONDS`, `DUXEL_DTPAGE_UPLOAD_PAGE_SIZE`, `DUXEL_DTPAGE_UPLOAD_REGION_WIDTH`, `DUXEL_DTPAGE_UPLOAD_REGION_HEIGHT`, `DUXEL_DTPAGE_UPLOAD_REGIONS`, `DUXEL_DTPAGE_UPLOAD_PAGES`, and `DUXEL_DTPAGE_UPLOAD_WARMUP_FRAMES`.

`BenchFrameRecorder` is the shared raw-frame accumulator for focused gates. Its statistics include samples, measured seconds, average FPS, median/p95/p99 frame time, and 1% low FPS. `layer_widget_mix_bench_fba.cs` writes these fields in JSON schema version 2; consumers must check `schemaVersion` before assuming the tail fields exist. The layer-widget and DirectText gates allocate `1,048,576` sample slots and cap phase duration at 30 seconds so valid long runs, including the DirectText Atlas control observed near `10.7k` FPS, do not overflow the earlier `262,144`-sample bound.

`samples/fba/directtext_dynamic_text_bench_fba.cs` is the focused gate for stable DirectText cache hits versus changing-string cache misses. It requires `DUXEL_DIRECTTEXT_BENCH_OUT`; controls are `DUXEL_DIRECTTEXT_BENCH_PHASE_SECONDS`, `DUXEL_DIRECTTEXT_BENCH_WARMUP_FRAMES`, `DUXEL_DIRECTTEXT_BENCH_ROWS`, and `DUXEL_DIRECTTEXT_BENCH_CORPUS_FRAMES`. Its JSON includes frame and text-work median/p95/p99 tails, 1% low FPS, average allocated bytes per frame, and generation collection counts.

`samples/fba/analytic_rounded_primitives_bench_fba.cs` is the focused 1x-MSAA quality/performance gate for rounded fills, combined fill/border panels, border-only rounded rectangles, and circles. Without `DUXEL_ROUNDED_BENCH_OUT` it displays large visual reference shapes. With an output path it runs the self-terminating benchmark controlled by `DUXEL_ROUNDED_BENCH_COUNTS`, `DUXEL_ROUNDED_BENCH_PHASE_SECONDS`, and `DUXEL_ROUNDED_BENCH_WARMUP_SECONDS`, and records average FPS, median/p95/p99 frame time, and 1% low FPS. Startup also asserts that rounded rectangles/circles produce primitive instances rather than CPU polygon vertices and that replay preserves the border-only geometry variant.

Axis-aligned `UiDrawListBuilder.AddRect(...)` outlines use rect-filled primitive emission instead of triangle polyline geometry. Rounded outlines, rounded fills, and `AddCircleFilled(...)` stay as GPU primitive instances: the fragment shader evaluates signed distance and derivative-based coverage, so quality is independent of caller-supplied circle segment counts. The fill+border `AddRectFilledRounded(...)` overload emits one combined instance and one blend pass. Border-only rounded rectangles use eight non-overlapping perimeter cells generated in the vertex shader, excluding the guaranteed-empty center to avoid full-panel fragment overdraw. Horizontal and vertical `UiDrawListBuilder.AddLine(...)` calls also use rect-filled primitives; diagonal lines keep the quad triangle path.

`DUXEL_VK_STATIC_GEOMETRY_UPDATE` controls same-shape static geometry content changes. Valid values are `auto`, `replace`, `inplace`, and `rotating`. `auto` resolves to `rotating` on validated NVIDIA discrete GPUs and to `replace` elsewhere until AMD/Intel and broader NVIDIA gates prove the same policy. Use `replace` to force allocation/replacement, `inplace` to force fence-waited reupload into existing buffers, and `rotating` to force retired-buffer reuse.

`DUXEL_VK_STATIC_GEOMETRY_INPLACE_UPDATE=1` is a backward-compatible explicit override for `DUXEL_VK_STATIC_GEOMETRY_UPDATE=inplace`. If a static geometry cache entry has the same vertex/index/primitive counts and the same expanded primitive triangle layout, Vulkan waits for all in-flight frame fences and reuploads into the existing device-local buffers instead of allocating replacement buffers. Keep this for focused A/Bs; rotating update is the cleaner default candidate because it avoids full in-flight fence waits.

`DUXEL_VK_STATIC_GEOMETRY_ROTATING_UPDATE=1` is a backward-compatible explicit override for `DUXEL_VK_STATIC_GEOMETRY_UPDATE=rotating`. Same-shape content changes retire the current static geometry buffer into a frame-safe reuse pool, then activate a safe retired buffer when one is available or create a new buffer to seed the pool. The retired pool is capped per tag to the frame count and idle retired buffers are trimmed after `StaticGeometryRetiredReuseGraceFrames`. This is the default resolved policy on validated NVIDIA discrete GPUs and remains explicitly available for vendor/device gates elsewhere.

`DUXEL_VK_COMMAND_SCHEDULER=all` or `1` enables the opt-in overlap-constrained Vulkan command scheduler for every eligible draw list. `DUXEL_VK_COMMAND_SCHEDULER=static` enables the same scheduler only for static-layer replay draw lists, which keeps dynamic whole-list scheduling out of the hot path while preserving the measured static-layer replay win. The scheduler uses `UiDrawCommand` coverage bounds to preserve required overlap order while grouping ready commands by pipeline class, then merges newly adjacent compatible commands during recording. In `all` mode, dynamic draw-list construction also computes `UiDrawList.CommandScheduleStamp` so cache-hit scheduling does not repeat full command hash/compare work in Vulkan command recording. It is not a default path: use it only with focused profiling, and keep `DUXEL_VK_COMMAND_SCHEDULER_MAX_WINDOW` bounded so broad scenes do not pay unbounded scheduling cost.

Static cached layer replay computes a conservative command schedule shape stamp once during layer capture when `DUXEL_VK_COMMAND_SCHEDULER=static`, `all`, or `1`. The stamp is based on stable local command shape and raw local command bounds. Vulkan scheduling uses the same conservative bounds for static-layer draw lists so moving or clipping a stable layer can reuse a cached safe order instead of repeating overlap analysis or per-frame replay-shape hashing.

When a dirty static layer rebuilds to the same draw-list shape, Core should refresh the existing `UiLayerCachedList` storage in place instead of allocating new local vertex/index/command/primitive arrays. Recompute `StaticGeometryStamp` and command schedule shape stamp after the overwrite, keep the stable `duxel.layer.static:{layerId}:list:{i}` key, and invalidate replay so opacity/translation/clip commands are rebuilt under the updated content stamp.

`DUXEL_UI_COMMAND_SCHEDULER=1` enables the earlier builder-stage scheduling experiment in `UiDrawListBuilder.Flush()`. Keep this separate from `DUXEL_VK_COMMAND_SCHEDULER`: dynamic whole-list builder scheduling can cost more than it saves when draw-list content changes every frame, so it is for structural experiments only. Prefer stable layer/static schedule caching or explicit draw channels before considering it for a default path.

For GPU timestamp profiling, also set `DUXEL_VK_GPU_PROFILE=1`. It only activates when the selected graphics queue supports timestamps, and profile lines add `gpuRender=...` with render-pass GPU execution time in microseconds. Use it to separate CPU command-recording cost from shader/GPU-side cost.

Static cached rect/circle primitive expansion to triangle vertex/index geometry defaults to `DUXEL_VK_STATIC_PRIMITIVE_TRIANGLES=auto`. The automatic policy enables it only on NVIDIA/AMD discrete GPUs, then applies per-draw-list byte and mutation guards. `auto` skips expansion when estimated expanded vertex/index bytes exceed `32x` the primitive-instance bytes; it also suppresses expansion for a static geometry tag for `30` frames after the tag's content hash changes, so mutating static layers use the upload-cheaper primitive-instance path until stable. Force expansion with `1/true/on` or disable it with `0/false/off`. This path keeps cached static draw-list command order, clipping, opacity, and texture state while letting expanded primitives ride the indexed triangle draw mode. Use profile `staticPrim(...)` counters to verify actual expand/skip decisions instead of relying only on the device policy block.

Static lists containing circles, rounded rectangles, or primitive borders are never expanded to legacy triangle geometry, even when static primitive expansion is forced. Those shapes require analytic fragment coverage and remain on the primitive-instance path; only plain filled rectangles are eligible for static triangle expansion.

The driver owns the opaque `VkPipelineCache` contents and decides reuse; Duxel owns only persistence of the bytes returned by `vkGetPipelineCacheData`. That persistence file is keyed by vendor ID, device ID, and `pipelineCacheUUID`. Duxel validates the 32-byte Vulkan header before returning data to the driver, treats missing/incompatible/unreadable persistence as an empty in-memory `VkPipelineCache`, and writes new bytes through a unique temporary file plus atomic replacement. Readers allow replacement sharing, so simultaneous Duxel processes see a complete old or new file rather than a partial write. Persistence failure never selects another renderer and never prevents the already valid Vulkan pipeline from rendering. A driver may additionally maintain its own internal cache outside this Duxel-managed file.

The `staticPrim(...)` profile block includes `layout=...`, the number of expanded primitive layouts materialized during that frame. A stable static circle cache should materialize layouts only on creation/rebuild (`layout>0`) and report `layout=0` on cache-hit frames; a nonzero steady-hit value indicates repeated CPU layout work.

The implementation boundary for static primitive auto decisions is `VulkanRendererBackend.StaticPrimitivePolicy.cs`; static geometry code should call this policy boundary instead of embedding device/heuristic decisions in upload code.

### GPU-driven renderer architecture (2026-07-03)

The Vulkan backend is a GPU-driven renderer with exactly **one graphics pipeline**, **one bindless texture descriptor set**, and **two shaders** (`Shaders/imgui.vert`, `Shaders/imgui.frag`):

- **Bindless textures**: one global `sampler2D[]` combined-image-sampler array (capacity 4096, UPDATE_AFTER_BIND + PARTIALLY_BOUND), bound once per frame. Textures get slot indices from a free-list allocator in `TextureResources`; slots are recycled at the deferred texture-destroy point. There are no per-texture descriptor sets.
- **Unified dual-source blending**: every draw outputs premultiplied color plus a per-channel blend factor (`One/OneMinusSrc1Color`, `One/OneMinusSrc1Alpha`). Standard draws emit `blendFactor = vec4(alpha)` (exactly equivalent to `SrcAlpha/OneMinusSrcAlpha`); ClearType subpixel text emits per-channel coverage, selected by the high bit of the fragment texture-index push constant.
- **Vertex pulling**: the pipeline has no vertex input state. The vertex shader reads 20-byte/5-dword `UiVertex` streams and 32-byte/8-dword packed `PrimitiveInstance` records through buffer device addresses (`GL_EXT_buffer_reference`). Primitive records carry bounds, radius/type encoding, border thickness, fill color, and border color.
- **Push constant layout**: vertex range `[0,40)` = `scale`(8) + `translate`(8) + `opacity`(4) + `drawMode`(4) + vertex-buffer address(8) + primitive-buffer address(8); fragment range `[40,44)` = packed texture index + subpixel mode bit. `drawMode` 0 = indexed triangle pulling, 1 = analytic primitive instances (six-vertex bounding quad for fills/circles and a 48-vertex eight-cell perimeter for border-only rounded rectangles).
- **Per-draw state**: `vkCmdBindPipeline` happens once per frame; per-draw variation is only push constants, index-buffer binds, and dynamic scissor. `vkCmdBindVertexBuffers` no longer exists in the backend.
- **Dynamic rendering**: there are no render pass or framebuffer objects. Frames record `vkCmdBeginRenderingKHR`/`vkCmdEndRenderingKHR` with `RenderingAttachmentInfo`; MSAA resolves inline (`resolveMode = AVERAGE`, MSAA target → swapchain image). Explicit image barriers replace implicit render-pass transitions: `UNDEFINED→COLOR_ATTACHMENT` before rendering and `COLOR_ATTACHMENT→PRESENT_SRC` after. The pipeline chains `PipelineRenderingCreateInfo` with the swapchain format.
- **Required device features** (explicit failure when missing, no fallback): `shaderSampledImageArrayDynamicIndexing`, `descriptorBindingSampledImageUpdateAfterBind`, `descriptorBindingUpdateUnusedWhilePending`, `descriptorBindingPartiallyBound`, `runtimeDescriptorArray`, `bufferDeviceAddress`, `dualSrcBlend`, `dynamicRendering` (`VK_KHR_dynamic_rendering`).
- **Dynamic geometry memory**: per-frame vertex/primitive buffers prefer BAR memory (`DEVICE_LOCAL|HOST_VISIBLE|HOST_COHERENT`) and fall back to the stated host-visible requirement when the device lacks it. This preference is required for vertex-pulling performance: shader BDA reads from plain host memory cross PCIe per vertex and measurably regress dynamic scenes.

### Renderer module map

`VulkanRendererBackend` is organized into 19 partial-class modules. Extend the matching module instead of adding new single-purpose files:

- `VulkanRendererBackend.cs` — `IRendererBackend` shell, constructor/bootstrap order, lifecycle/teardown, render-entry frame order, settings/API mutation.
- `VulkanRendererBackend.Device.cs` — instance/surface/physical-device/queue/device setup, required-feature gating, vendor/device policy state and env parsing, pipeline cache identity.
- `VulkanRendererBackend.Swapchain.cs` — swapchain selection policy, resize-only versus full recreate flow, image views/MSAA targets, semaphore capacity, and preservation of non-swapchain renderer resources during live resize.
- `VulkanRendererBackend.PipelineResources.cs` — bindless descriptor layout/pool/set, pipeline layout/push ranges, the single graphics pipeline, samplers, validated/atomic pipeline-cache persistence and diagnostics, embedded shader loading.
- `VulkanRendererBackend.Resources.cs` — generic buffer/image/view/memory helpers (`CreateBuffer` with preferred-memory selection, `GetBufferDeviceAddress`), image layout transition policy and `imgTrans(...)` counters.
- `VulkanRendererBackend.TextureResources.cs` — texture create/update/destroy, bindless slot allocator and descriptor writes, upload batching, font/white texture id classification, deferred destroys.
- `VulkanRendererBackend.UploadScheduler.cs` — staging buffer lifetime, upload batching (`DUXEL_VK_UPLOAD_BATCH`), upload queue policy (`DUXEL_VK_UPLOAD_QUEUE`), submission/wait accounting.
- `VulkanRendererBackend.DynamicGeometry.cs` — per-frame vertex/index/primitive buffer capacity/mapping (BAR-preferred), dynamic geometry upload, frame geometry preparation.
- `VulkanRendererBackend.StaticGeometry.cs` — static cache identity/lifetime/LRU, content/shape derivation, frame preparation, policy application, replacement policy.
- `VulkanRendererBackend.StaticGeometryStorage.cs` — static device-local buffer materialization (with device addresses), static upload/layout writing, retired-buffer pool.
- `VulkanRendererBackend.StaticPrimitivePolicy.cs` — static primitive triangle-expansion auto policy (`DUXEL_VK_STATIC_PRIMITIVE_TRIANGLES`), byte/mutation guards, decision profiling.
- `VulkanRendererBackend.Frame.cs` — frame orchestration (acquire/submit/present/fences), shared frame-fence waits, frame command-recording preparation, frame completion.
- `VulkanRendererBackend.CommandRecording.cs` — `RecordCommandBuffer` (binds the bindless set and the single pipeline once), frame/render-pass begin/end, recording state aggregation, frame/draw-list contexts, draw-list traversal.
- `VulkanRendererBackend.CommandState.cs` — draw-mode push state, texture-index push state, vertex/primitive address push + index-buffer bind state, transform/opacity push state, scissor state.
- `VulkanRendererBackend.CommandDraw.cs` — draw-path orchestration, draw dispatch (indexed/instanced), command classification, texture lookup cache, primitive instance encoding.
- `VulkanRendererBackend.CommandScheduling.cs` — opt-in overlap-constrained command scheduler (`DUXEL_VK_COMMAND_SCHEDULER`), schedule cache, merge expansion.
- `VulkanRendererBackend.CommandDiagnostics.cs` — command diag gating/labels (`DUXEL_VK_COMMAND_DIAG`), font diagnostics (`DUXEL_VK_FONT_DIAG`), record profile counters and `CommandRecordStats` construction.
- `VulkanRendererBackend.Diagnostics.cs` — profile line output, device/policy text, GPU timestamp interpretation, failure tracing.
- `VulkanRendererBackend.Types.cs` — shared renderer records (`StaticGeometryBuffer` with device addresses, `TextureResource` with slot index, frame resources).

Upload command pooling, staging buffer lifetime, `DUXEL_VK_UPLOAD_BATCH`, `DUXEL_VK_UPLOAD_QUEUE`, staging offset reservation, batch flush, graphics upload-prepare command submission, transfer upload-copy submission, and single-time upload submission belong in `VulkanRendererBackend.UploadScheduler.cs`. Texture updates and static geometry upload code should call this boundary instead of growing their own staging command paths. Upload batching defaults to enabled; set `DUXEL_VK_UPLOAD_BATCH=0`, `false`, or `off` only for diagnosis. The transfer upload path is opt-in only: keep the default graphics path until focused texture/page upload gates prove the split path is faster on the target vendor/device. On the local NVIDIA `10de:2f58` gate, transfer was slower by about `12-13%` on generic texture updates and `15-19%` on DirectText page updates because `upSched(prepSub=1 ...)` and higher upload waits outweighed shorter transfer-copy submissions.

Image layout transition policy stays in the `Resources` module. Use `imgTrans(total=... toDst=... toShader=... present=... color=... xferStage=... gfxStage=... us=...)` to prove whether texture/page uploads are barrier-heavy or copy-heavy; `texture_upload_barrier_bench_fba.cs` should show same-texture non-overlapping region batches at `total=2` and many-texture updates at `total=2 * textureCount`. `xferStage` means the stage masks are transfer-queue-compatible; `gfxStage` means graphics/fragment/color stage masks must be split or moved before transfer-only upload recording.

For font command diagnostics, set `DUXEL_VK_FONT_DIAG=1`. Use `DUXEL_VK_FONT_DIAG_OUT` to redirect the verbose command log to a file.

For general Vulkan command sequence diagnostics, set `DUXEL_VK_COMMAND_DIAG=1`. Use `DUXEL_VK_COMMAND_DIAG_OUT` to log each draw command's `pipe`, texture id, clip, and static-binding status for pipeline-switch analysis. Logging is limited by default with `DUXEL_VK_COMMAND_DIAG_EVERY=120` and `DUXEL_VK_COMMAND_DIAG_FRAMES=1`.

Draw-list command merging must treat callback-free zero-element commands as state placeholders, not draw-order barriers. When adding a real draw, the builder removes a trailing empty placeholder before testing contiguous command merge eligibility; keep opacity, texture, clip, translation, kind, vertex offset, callback, and user data in the merge predicate. Prefer clipped draw helpers such as clipped `AddImage(...)` when the caller already knows the effective clip, instead of pushing/popping the builder clip stack around one draw.

Sampler-free pipeline variants no longer exist: since the GPU-driven renderer migration every draw samples the bindless texture array, solid draws sample the app 1x1 white texture, and pipeline selection cost is gone because there is only one pipeline. `DUXEL_VK_TRIANGLE_COLOR_PIPELINE`, `DUXEL_VK_SOLID_UNIFIED_PIPELINE`, and `DUXEL_VK_SOLID_UNIFIED_STATIC` were removed together with the split pipelines and the solid-triangle sentinel; do not reintroduce them.

For text renderer A/B profiling, set `DUXEL_TEXT_RENDERING=direct`, `atlas`, or `auto`. `DirectText` is the default. Explicit `atlas` forces the atlas renderer for comparison, and explicit `auto` keeps atlas-first rendering but uses DirectText as an immediate visual fallback for missing atlas glyphs; use it only when that fallback behavior is intentional.

DirectText page-texture packing is enabled only with `DUXEL_DIRECT_TEXT_PAGE=1`. It packs DirectText bitmaps into 1024x1024 page textures, reserves a 1px border around each region, and extrudes edge pixels into that border to reduce atlas sampling bleed. Page creation uploads only the border-inclusive packed region, and consecutive non-overlapping partial updates for the same texture can be batched into one Vulkan upload/copy submission. Use `samples/fba/directtext_page_upload_bench_fba.cs` as the focused upload-policy gate for this path before changing upload batching or transfer-queue defaults. DirectText page quads should use the clipped image helper rather than a `PushClipRect -> AddImage -> PopClipRect` sequence. It is off by default to preserve text appearance and must not become a default path without rendered visual validation.

As of 2026-07-10, a page-enabled performance run reached `1819.709` FPS, but the rendered all-features capture was mostly black with the UI missing. Treat that result as a rejected diagnostic candidate, keep page mode off by default, and require a passing visual comparison before reconsidering it.

## Constraints and non-goals

Assume these repository rules are intentional unless the task explicitly changes them:

- .NET 8, .NET 9, and .NET 10 package targets
- Windows-first platform reality
- Vulkan renderer requirement
- NativeAOT-friendly direction
- no casual fallback-path expansion
- no defensive-code sprawl used as a substitute for root-cause fixes

When something is unsupported, prefer explicit failure or explicit scope boundaries over hidden fallback behavior.

## How an agent should approach Duxel work

Recommended sequence:

1. identify whether the task is about core UI behavior, app bootstrapping, Windows platform behavior, Vulkan rendering, DSL, or docs
2. read the nearest relevant section of this document, then the nearest relevant sample if an example is needed
3. preserve architectural boundaries
4. make the smallest change that solves the actual problem
5. validate with a build or a relevant sample run
6. update docs if the change affects public behavior, workflow, or extension points
7. update this document when the stable guidance changes

## Documentation links

Use these documents together with this reference:

- `README.md` — package-facing overview and quick start
- `docs/extended-title-bar-guide.md` — ExtendedContent app layout, platform contracts, and AI validation workflow
- `docs/getting-started-fba.md` — first-time FBA walkthrough
- `docs/fba-reference-guide.md` — package/project switching and `run-fba.ps1`
- `docs/fba-run-samples.md` — sample catalog and one-command runs
- `docs/custom-widgets.md` — reusable widget extension path
- `docs/ui-dsl.md` — declarative UI reference
- `docs/version-history.md` — grouped release history
- `docs/design.ko.md` — current architecture and design baseline

## Maintenance note

If you learn a stable workflow rule that future agents or developers need in order to use Duxel correctly, add it here and refresh the `Last synced` date.

## Common layer batching

Vulkan GPU resource bindings use reference records to reduce large value copies during preparation, classification and recording. Existing fence/retirement rules own GPU lifetimes; collecting a record does not free GPU resources. With profiling enabled, `UiRendererFrameMetrics.RendererAllocatedBytes` measures managed allocations on the calling thread inside the renderer method body; otherwise it is null. This differs from UI scene allocations and whole-process memory. ERD `rendererAllocatedBytesPerFrame` remains unknown when an older executable lacks instrumentation.

`UiDrawList.ComputeCommandTopologyStamp` identifies index ranges, vertex offsets, command kinds, textures and callback presence. A static layer reusing a same-size command array gets a new command wrapper when this information changes. Geometry revisions and command topology are tracked separately: texture-only updates reevaluate GPU grouping without reuploading vertices/indices.

CPU batching checks command/content history only when the next list shares its 3D state. Combined output wrappers are valid only for the batcher's exclusive borrowed frame and may be reused after `Frame.Dispose()`. Source lists and geometry are unchanged. Copy the `UiLayer3D` value to preserve a camera. Replay-wrapper allocations are removed; the combined `UiDrawData` frame-header allocation remains.

`BenchFrameRecorder.FrameSeconds` exposes recorded samples in submission order as a read-only view. Further recording/reset may change its contents; save raw CSV after measurement. `tests/Duxel.Performance.Bench <output.json> --retained` measures only the CPU batcher; its iteration rate is not application FPS. ERD's 1000 FPS target, current per-scenario values and attainment appear in summary `fpsTargets` and `fps-target.md`.

`UiLayer3D.PlaneModel = new UiLayerPlaneModel(translation, depthOffset)` separates a shared `Transform` camera from per-plane source-pixel translation and normalized post-projection depth. After slot warmup, camera-only changes write no per-plane buffers; changing one model writes 16B. Camera push constants and final GPU drawing remain separate costs. Use `SharedCameraLayerDraws` and `DUXEL_VK_SHARED_LAYER_CAMERA=0` to inspect this path. General rotations, world-Z and complete-MVP transforms remain supported; see the linked layer documentation for coordinates.

`SharedBasisLayerDraws` reports common matrix-basis draws; `DUXEL_VK_SHARED_LAYER_BASIS=0` disables this optimization for comparison. CPU visibility matrices and pushed GPU matrices are tracked independently so adjacent groups do not retransmit the same camera basis. Camera changes skip retained geometry generation/uploads but still project and rasterize visible vectors on the GPU.

Vulkan also combines resident lists with different 3D matrices using GPU command data and retained index streams. `CompiledLayerDraws`/`CompiledLayerCommands` count real combined draws/source commands. `CompiledIndexUploadBytes`, `LayerStateUploadBytes` and `CompiledIndexCacheBytes` expose added transfers and memory. `DUXEL_VK_COMPILED_LAYERS=0` selects direct submission for comparison. See [dynamic 3D layers](dynamic-3d-layers.md) for domains, bounds and current added memory.

Vulkan tests whole-list bounds before state changes and skips redundant command tests for fully interior lists. `UiRendererFrameMetrics.LayerTransformPushes` counts layer matrix pushes; `CulledLayerLists` counts rejected lists. GPU residency remains independent of visibility.

`UiDrawData` implements `IDisposable`. Use `using var drawData = ui.GetDrawData()` to release frame resources after rendering and diagnostics. Released draw-list wrappers may be reused and must not be accessed afterward. Geometry caches and frame-local matrix states have separate lifetimes.

`UiRetainedLayerBatcher.Prepare(UiDrawData)` combines adjacent resident lists with identical 3D state and retains their CPU vector data. Vulkan applies it automatically. Borrow `Frame.DrawData` synchronously until `Frame.Dispose()`; the caller retains ownership of the original input. Dispose the batcher to release its cache. `UiRendererFrameMetrics.BatchedSourceLists` and `RebuiltBatchGroups` report engine batching. See [dynamic 3D layers](dynamic-3d-layers.md) for conditions and limits.

`UiDrawList.CommandStateStamp` is a nonzero identity for unchanged command descriptor state. Use `ComputeCommandStateStamp(commands)` after changing command kind, ranges, texture, clip, translation or opacity; zero means unknown and disables descriptor reuse. A copied draw list with edited commands must recompute or clear the stamp. Geometry bindings and display transforms invalidate cached descriptors independently. Resident replay and retained batching populate the stamp automatically. Warm camera-only frames skip descriptor reconstruction while retaining CPU visibility checks and GPU drawing. Hidden command descriptors are populated before caching a source; display-context changes invalidate hidden members too. `UiRendererFrameMetrics.CompiledDescriptorBuilds` counts CPU descriptor constructions, separately from GPU transfer bytes. `DUXEL_VK_DESCRIPTOR_REUSE=0` disables this optimization; `scripts/measure-erd-layers.ps1 -CompareDescriptorReuse -Executable <exe>` compares both paths in one executable.

`UiRendererFrameMetrics.PresentationSequence` advances only after a successful/suboptimal Vulkan `QueuePresent`. `PresentationTimestampTicks` uses `Stopwatch` ticks; `PresentationIntervalSeconds` is the interval between accepted requests (zero for the first). Consume an interval only when its sequence advances: skipped renders can leave the previous metrics intact. These counters measure application presentation throughput, not monitor refreshes or GPU completion. The ERD HUD/raw CSV now use this clock and export `fpsTimingBasis: accepted-presentation`; historical results without that field used UI-loop intervals. The measurement runner rejects mixed timing bases in a comparison.

Fully visible compiled members with current descriptor state use their contiguous cached index span without visiting each source command. Partial visibility and unknown/changed state keep command-level traversal. Precomputed kind-transition summaries preserve profiling counters, including boundaries between members and empty commands. `CompiledCommandVisits` counts actual source-loop visits; `CompiledLayerCommands` retains the number of submitted nonempty source commands. Set `DUXEL_VK_COMMAND_SPANS=0` or use `-CompareCommandSpans -Executable <exe>` for comparison. Camera/model state updates and real GPU draws remain necessary.

Aggregate culling bounds are cached by nonzero `CommandStateStamp` in the common Vulkan layer visibility path, including direct submission. `ComputeCommandStateStamp` now includes bounds coordinates and `HasBounds`; callers must refresh the stamp when either changes. The cache retains source-space bounds, then reclassifies them with the current camera/model/display context every frame. Unknown stamps recompute bounds; unknown/invalid bounds retain conservative visibility. Reordered lists compare state before reusing a slot. `LayerBoundsCommandVisits` counts commands scanned while aggregating layer bounds, separately from `CompiledCommandVisits`. Set `DUXEL_VK_LAYER_BOUNDS_REUSE=0`, or use `-CompareLayerBoundsReuse -Executable <exe>`, for comparison. This caches CPU bounds only; it adds no raster layer target and does not suppress GPU vector rendering.

`UiImmediateContext.TryReplayLayer(id, in options)` submits valid retained layer content without opening a layer scope. On false, use the normal `BeginLayer`/`EndLayer` path to build or refresh it. A matching content version (or static cache), clean entry and stored content are required; valid empty content returns true. A miss does not open a scope or change the stored version. Successful replay validates the current scene and preserves opacity, translation, parent text dependencies and frame-owned camera wrappers. Do not call `EndLayer` for a successful direct replay. The ERD uses this common engine API for its grid, relationship batches, selected connectors, tables and overview. `DUXEL_ERD_DIRECT_REPLAY=0` selects normal scopes; `scripts/measure-erd-layers.ps1 -CompareDirectLayerReplay -Executable <exe>` compares both in the same executable. This avoids scope setup/copies; it still performs per-layer lookup and submission and is not a bulk retained-scene API.

```csharp
if (!ui.TryReplayLayer(id, options))
{
    if (ui.BeginLayer(id, options)) DrawContent(ui);
    ui.EndLayer();
}
```

Retained geometry and command state are protected by one CPU-frame lease, acquired on the first retained submission and transferred to `UiDrawData`. Retired pooled sources drain when their last referencing frame epoch finishes; continuously overlapping newer frames do not prevent old retirement. A retained source or replay command array is copied before modification while a frame still borrows it. Camera-only frames keep sharing the unchanged geometry. This adds no GPU idle wait or layer render target.

`GetDrawData()` transfers responsibility for the returned ownership object: dispose it after consumption. Repeated calls or record copies do not create additional ownership. `UiContext` releases unclaimed output at the next frame/render, EndFrame or disposal, and also releases pending retained leases from an abandoned render. Claimed frames can retain their geometry beyond context disposal. These guarantees cover geometry/command snapshots and do not make UiContext concurrently writable.

`UiDrawData.ScaleClipRects` and `UiDrawList.ScaleClipRects` produce independently owned copies, including empty cases. Frame geometry/camera leases are not copied; texture-update containers are copied along with geometry and commands. Scaled frame texture payloads are captured into independently owned memory. A scaled copy and its original can be disposed independently.

`UiImmediateContext.GetDrawDataFrameId()` identifies the current render pass within one UiContext. The value is copied into `UiDrawData.DrawDataFrameId`, preserved by clip copies and carried into `UiRendererFrameMetrics`. It is distinct from the renderer's accepted `PresentationSequence` and from geometry content versions.

`GetPresentationReader()` returns an `IUiPresentationReader` with an atomic latest snapshot, a bounded FIFO of accepted presentation samples and an explicit overwrite count. `Drain(Span<UiRendererFrameMetrics>)` consumes samples for one reader; the reader can collect final results after UI state buffer disposal. UI convenience methods expose the same stream. A duplicate presentation sequence updates latest metrics without another sample; a regressing sequence is rejected within one renderer history. GPU timestamp values retain their existing frame-slot delay and are not reinterpreted as timing for the current source ID. The UI context itself is not concurrently writable.

ERD now uses `presentation-history-v1`, matches CPU scene telemetry by source frame ID, checks every accepted sequence, uses the actual presentation timestamp for warmup and drains final results on save. Legacy one-column frame CSV remains unchanged; an additional `.presentations.csv` links each sample to its sequence/source ID. It reuses BenchFrameRecorder.FrameSeconds instead of keeping a duplicate duration buffer; source IDs replace that buffer at the same capacity. The runner audits link rows, ordering, counts, overwrite state and warmup metadata. Mixed sampling methods cannot produce an improvement percentage. The bounded opt-in pipeline described below consumes this presentation history.

`DuxelFrameOptions.EnableRenderPipeline` opts into a bounded UI/renderer pipeline (default false). A dedicated consumer owns Vulkan calls, settings and presentation feedback; the producer remains the sole UiContext writer. One pending frame plus one executing frame applies backpressure without dropping frames. Settings travel with their source frame; resize and first-frame notifications follow actual accepted presentation. Shutdown drains and joins before UI/renderer disposal; consumer errors release queued ownership, wake shutdown and propagate the original exception.

`UiDrawData.CaptureTexturePayloads()` captures all texture-update payload slices into one pooled frame-owned buffer before exclusive thread handoff. Frames without payload bytes do not allocate a buffer; unchanged retained geometry is not copied. Call before the producer can modify source pixel memory. `ScaleClipRects` also snapshots texture payloads, so its copy survives original-frame disposal. A record copy remains shared ownership, not a second independent lifetime. Callback-containing frames require a completion barrier before the producer starts another UI frame; ordinary frames overlap. This preserves UI/callback exclusion, not callback thread identity. External callback closures must not assume the UI producer thread.

ERD selects this engine option with `DUXEL_ERD_PIPELINE=1`; per-run JSON records `renderPipeline`. The performance runner's `-CompareRenderPipeline` alternates Off/On in the same executable and checks the returned mode. Current GPU tests include bounded queue backpressure, FIFO shutdown, failure propagation, texture ownership and three retained geometry revisions produced before Vulkan consumption; the public path remains opt-in pending full performance and interaction verification. In pipelined APP_PROFILE output, `getDraw` includes queue residence and `total` is source-to-render completion latency, not serial CPU work. SubmitTimeMs is the latest completed render duration.
Pipeline warmup uses `fpsWarmupMethod: first-accepted-presentation-v1`: the deadline is exactly one Stopwatch second after the first accepted presentation, never after the second UI call. JSON includes the start tick and clock frequency; the comparison runner checks this duration, the first measured timestamp and exact samples/framesAfterWarmup equality. Different warmup methods cannot produce an improvement percentage. The earlier UI-anchored twelve-run set is retained at render-queue-ab with INVALID_FOR_FINAL_COMPARISON qualification; its FPS is not the current result. The source inspection and corrected rerun budget are preserved. ERD now enables the engine pipeline by default; `DUXEL_ERD_PIPELINE=0` explicitly selects serial execution. The library option itself remains opt-in for consumers with callback thread-affinity requirements.
Producer admission waits for the pending slot before sampling input and constructing UI. The application therefore does not build a third CPU frame behind one active and one pending frame. Renderer and UI work still overlap. Callback commands are executable work even with zero elements/geometry: builder flush, static append and retained replay preserve them; adjacent identical hooks never merge; Vulkan invokes each hook in command order. Callback-bearing frames wait for completion before the next UI. Hooks run on the consumer thread in pipeline mode and must not mutate submitted geometry or recursively render/change Vulkan recording state.
Preceding ERD verification: .NET11 RC NativeAOT,1200tables,dense/resident,MSAA4,VSyncoff, three18-second runs per mode; current camera908.61FPS/motion957.30FPS,target1000,NOT_MET. Same-image serial baselines667.91/666.71FPS. Evidence: artifacts/perf-2026-11-03/render-queue-admission-ab and render-queue-admission-native-promotion.json. GPU322 checks per NET10/NET11, direct298; latest manual interaction/memory/runtime performance audit remains pending.

`DuxelFrameOptions.UseEventDrivenPlatformLoop` lets the platform owner wait while UI and renderer threads continue running. It requires the explicit `IPlatformEventWakeBackend` capability and fails before GPU setup if unavailable; the library option defaults false. Windows implements a latched thread-safe `WakeEvents()` and waits on that event plus queued input using MsgWaitForMultipleObjectsEx/MWMO_INPUTAVAILABLE. A SafeHandle reference protects the event during the native wait. Previously observed unread messages still dispatch. Wake/result failures are explicit; no polling timeout substitutes for a failed wake.

Exit interrupts UI and platform waits and cancels resize acknowledgement waits. Cursor changes and worker errors wake the platform owner. Session wake callbacks are detached under the same lock used by concurrent callers before backend/handle disposal. RequestFrame can advance UI/render work while the owner sleeps. The existing present-based first-frame and resize notifications are unchanged. Tests now reference the actual App assembly; real application tests use separate owner threads because WM_QUIT belongs to a thread queue. Test watchdogs fail the case rather than claiming successful production wake.

ERD enables this option by default (`DUXEL_ERD_EVENT_LOOP=0` selects polling). `-ComparePlatformEventLoop` alternates both modes in one executable with the render pipeline enabled. JSON records the mode, application-run CPU milliseconds and monotonic wall milliseconds, their ratio `averageBusyCpuCores` (one full logical core = 1), and process-lifetime peak working set. CPU scope includes initialization, warmup and shutdown; peak working set is not GPU VRAM or a steady-state heap measurement. Camera timing/geometry/quality are otherwise unchanged.
Latest live-stable ERD image: .NET11 RC NativeAOT,1200tables,18sec×3 per scenario,current883.41/980.45FPS,target1000,NOT_MET. See event-loop-live-current and event-loop-native-promotion.json. A separate same-engine controlled event-loop phase reduces CPU core-equivalent usage42.09%/40.27% without an established FPS percentage; peak process working set remains about1.62GiB. GPU tests332 per NET10/NET11,direct308. No-output sessions retain no benchmark samples and allocate none of the24MiB measurement buffers; six-second smoke plus source invariant, not long-endurance proof. Native frame profiling diagnostics include log-frame overhead.

Detailed CPU profiling now samples the frames that `DUXEL_VK_PROFILE_EVERY` will log. `DUXEL_VK_PROFILE_TIMING_SAMPLING=0` retains every-frame private CPU durations;1 is the default. Invalid boolean values fail explicitly. The single renderer timing clock returns0 for an uncollected start and its elapsed helper returns0 for that sentinel, avoiding fabricated elapsed values. Draw/upload/residency/command counters, allocation measurements, GPU timestamp queries and accepted-presentation timing remain independent and run as before. No geometry, shader, MSAA or camera behavior changes.

`UiRendererFrameMetrics.CpuTimingCollected` identifies frames with detailed CPU durations; it does not mean counters/GPU timing were skipped on other frames. ERD JSON reports actual `cpuTimingCollectedFrames` and `gpuTimingSamples` after warmup. `-CompareCpuTimingSampling` compares both modes in the same native image with a1,000,000-frame log interval, both engine pipeline/event loop enabled. It checks baseline CPU timing on every measured frame, sampled timing on no short-run frame, positive GPU timing/draws and unchanged warmup/process contracts. Idle is rejected before a child starts because its benchmark collector intentionally records no timing samples.
Latest FPS record,2026-11-03: .NET11 RC NativeAOT,1200 tables,dense/resident,MSAA4,VSync off,one-second warmup after first accepted presentation,three independent18-second runs per scenario. Target1000 FPS. Camera median946.46 FPS(94.65%,range903.91–960.34); camera plus entity motion978.39 FPS(97.84%,range936.18–1000.71). The best single-run average1000.71 FPS is distinct from repeated medians; neither scenario meets the target across runs. The live panel displays FPS/1000. Raw records are in artifacts/perf-2026-11-03/cpu-timing-ab; fps-current.json preserves the latest machine-readable score. All12 presentation-link/warmup/timing/process audits pass. Detailed CPU durations default to log-frame sampling, while per-frame counters,GPU timestamps and presentation clocks remain intact. On/Off FPS run ranges overlap, so no improvement percentage is established.

Automatic transient CPU builder reservation now follows the largest owned draw-list chunk. Borrowed resident layer vertices/indices no longer inflate temporary buffer reservation. Public scene totals,GPU geometry and explicit Reserve are unchanged. NET10/NET11 GPU366 checks each and direct submission342 pass. NativeAOT performance/memory comparison is pending; this is not yet a claim of improved FPS or reduced working set.

2026-11-03 completed transient-reservation comparison: same .NET11 RC NativeAOT/1200tables/dense/resident/MSAA4/VSync off,three independent18-second runs per variant/scenario in alternating order. Camera874.66→921.29 FPS(+5.33%);motion880.82→932.94 FPS(+5.92%); run ranges are disjoint. Median process peak working set decreases about0.25%/0.32%,but remains about1.6GiB and is not GPU VRAM. p95/p99/1% low ranges overlap; no latency improvement percentage is established. Current FPS921.29/932.94 means92.13%/93.29% of target1000; current image best single-run average999.52. Historical image record1000.71 remains separate. Presentation/warmup/process/timing/geometry audit passes12/12; scene builds0/frame(camera),1/frame(motion). See artifacts/perf-2026-11-03/reservation-ab and fps-current.json. Default NativeAOT EXE/PDB promotion matches measured imageAC3D25FB by hash readback.

Per-frame static geometry bindings use a dense array indexed directly by draw-list number. Preparation,dynamic upload,compiled plans and command recording share the same frame binding. Reset clears previous references and sets the new list range,so shrinking/empty frames and dynamic lists cannot inherit stale bindings. GPU ownership,fences and retirement remain unchanged. Removing hash lookup is a performance hypothesis until a separate NativeAOT comparison completes.

Completed dense-binding comparison(2026-11-03): .NET11 RC NativeAOT,1200tables,dense/resident,three18-second runs per variant/scenario. Camera921.10→946.04 FPS;motion1006.53→974.12 FPS. Run ranges overlap,so neither improvement nor regression percentage is established. Current ranges721.13–1013.41(camera),880.19–984.70(motion) retain all low runs. Current-image best single-run average1013.41;median target attainment94.60%/97.41%;neither meets1000 across runs. p95/p99/1% low ranges also overlap. Motion median peak working set increases1730801664→1731289088bytes(about0.47MiB),with disjoint ranges;camera memory ranges overlap. Presentation/warmup/timing/process/geometry audits pass12/12. Geometry generation remains0/1 per frame and all GPU measurements stay enabled. Evidence:artifacts/perf-2026-11-03/frame-bindings-ab;latest score fps-current.json. Default EXE/PDB matches measured image917A9C59 by hash readback.

Current-source.NET10/11 Managed/NativeAOT comparison completes12runs/72phases;source/sample/mode/exit audits and72GPU profile phases are in artifacts/perf-2026-11-03/runtime-current. See dynamic-3d-layers.md. MSAA1/serial/UI-frame timing differs from the ERD MSAA4/pipelined/accepted-presentation1000FPS target. Latest desktop status-panel capture remains unverified after automatic approval rejection;no alternate desktop-capture route was attempted.

Compiled groups default to2,097,152 index entries. DUXEL_VK_COMPILED_INDEX_LIMIT strictly accepts1,048,576/2,097,152/4,194,304/8,388,608;other values fail explicitly. The256-descriptor and512MiB total index-cache limits remain. UiRendererFrameMetrics.CompiledIndexLimit reports the actual setting. ERD JSON compiledIndexLimit/compiledIndexCachePeakBytes report actual capacity and maximum observed compiled-index cache bytes over measured frames,not total VRAM. scripts/measure-erd-layers.ps1 -CompareCompiledIndexCapacity alternates1Mi/2Mi in one executable and verifies capacity/cache/draw/GPU/presentation data. A1.2M-index GPU boundary case confirms2→1draws,identical pixels and zero camera-only reupload. NET10/11pass390checks each,direct366,runner10/10. ERD FPS impact remains unproven until repeated comparison.

Completed same-image1Mi/2Mi index-capacity NativeAOT comparison(2026-11-03):1200tables,dense/resident,MSAA4,VSync off,three nominal18-second executions per mode/scenario. Current camera988.05 FPS(98.80%,range899.53–1044.10),motion966.90 FPS(96.69%,828.92–1074.02);best single-run average1074.02. Neither meets1000 across runs.1Mi→2Mi FPS923.78→988.05 and1058.46→966.90 has overlapping ranges;no improvement/regression percentage is established. Mean draws97.59→91.91 and80.69→74.65;compiled index-cache peak270330504/270330552bytes is unchanged between modes. Distinguish the deterministic2→1draw/exact-pixel boundary proof from real-run averages. Per-frame means include camera-state sample weighting,not pure same-view work reduction. Raw frames,presentation links,actual limits/cache,warmup,process metrics and0/1scene-builds audits pass12/12. Default EXE/PDB matches measured image60FE419D. See fps-current.json and index-capacity-ab.

Measurement inspection:App auto-exit starts at appStartUtc before font setup;ERD tour time advances from rendering start;presentation sampling begins after first accepted present+1second. Actual sampled intervals span14.84–15.21seconds inside nominal18-second runs,and mean visible command counts differ. This does not prove every cause of FPS variability. Before more full comparisons,separate preparation from measured camera/motion workload start. Preserve these results as observations under their original nominal-run contract. runtime-current.NET10/11 results use matching source hashes before this capacity change;do not promote them to final performance measurements of the new image.

Pass -ControlledWorkloadClock explicitly to measure-erd-layers.ps1 to separate preparation from measured camera/motion. It sets DUXEL_ERD_WORKLOAD_CLOCK=post-warmup and DUXEL_ERD_MEASURE_SECONDS. The real scene warms at its initial pose;the monotonic workload epoch starts after first accepted presentation+1sec. Requested seconds mean measured workload duration;App duration+120 auto-exit is explicitly a guard. Ordinary operation and legacy nominal-total measurements retain wall-total. Idle/old separate baseline executable combinations fail before execution. Source timestamps exclude queued preparation frames;workload.csv links every presentation source to its input timestamp. source-epoch-v2 warmup differs from legacy v1 and cannot be mixed for improvement percentages. Duxel.Core.BenchWorkloadClock returns allocation-free step values. The extra8MiB timestamp buffer exists only for controlled measurement output. NET10/11each397,direct373,runner15/15 pass. No new FPS attainment is claimed before actual NativeAOT interval validation.

> Latest measurement: **target 1000 FPS**, camera **870.27 FPS(87.03%)**, camera plus entity motion **906.79 FPS(90.68%)**, best single-run average **935.32 FPS**. Stable target remains unmet. .NET11 RC NativeAOT,1200 tables,MSAA4,actual18 seconds after preparation,three runs per scenario,identical initial camera. Measured SHA256:94E22DE0FFBB5F47D9CAD2E36D6F03207F10CDB2DA6D150F121A403FCF0A52CC. The default distribution EXE/PDB now matches this measured image by hash readback. Latest score:`artifacts/perf-2026-11-03/fps-current.json`; evidence:`workload-controlled-capacity-ab/`. Previous-condition best1074.02 FPS is not directly comparable.

The real dynamic3D sample and window chrome were rendered by the current renderer and reviewed through its owned GPU buffer: panel title,bottom line,compact line spacing and content-sized panel are visible. NET10/11 each pass8/8,including colored geometry;final pixels are identical. Evidence:artifacts/perf-2026-11-03/status-panel-gpu-review.json. This is neither desktop/DWM capture nor an FPS measurement. The initial texture-order hypothesis was rejected by exact pixel equality;no engine defect is claimed.

Axis-aligned layer bounds specialization uses two opposite transformed endpoints only for exact scale/translation matrix shapes;rotation/perspective retain general homogeneous culling and the AA guard. UiRendererFrameMetrics.AxisAlignedLayerCulling reports the renderer setting;DUXEL_VK_AXIS_ALIGNED_CULLING accepts0/1(default1). ERD JSON reports axisAlignedLayerCulling. -CompareAxisAlignedCulling selects same-image alternating general/specialized runs with common2Mi capacity and supports -ControlledWorkloadClock. GPU checks NET10/11each403,direct379,general403;119raw GPU images identical. FPS remains unconfirmed for this candidate;see optimization-session-2026-11-03.md and axis-culling-input-contract.json.

> Latest measurement/default distribution: **target1000 FPS**,camera **1054.95 FPS(105.49%)**,camera plus entity motion **1116.81 FPS(111.68%)**,best single-run average **1122.60 FPS**. All three independent run averages per scenario exceed1000; ranges1044.20–1081.47/1031.41–1122.60 FPS. .NET11 RC NativeAOT,1200tables,MSAA4,actual18seconds after preparation,three runs per scenario,identical initial camera. This does not guarantee a1ms deadline for every frame. SHA256:09584FF4DF471401C9B53047BA793885BFA919290CEAA2B7D5EB27D501260AAA. Default EXE/PDB hash readback verified. Latest score:`artifacts/perf-2026-11-03/fps-current.json`;evidence:`axis-culling-ab/`(audit12/12).

`scripts/measure-erd-resources.ps1` observes live resources. Before launch it checks the declared EXE hash,a fresh output path and existing same-name processes;it preserves existing windows. Empty benchmark output avoids sample arrays. One-second process samples record private bytes,working set,CPU,handles,threads and logged frames. Normal window close/process exit is verified;emergency termination,missing data and long sample gaps cannot pass. Process memory is distinct from VRAM and live-object size.

On Windows x64, `-IncludePrivateAllocations` records commit and reserve sizes by allocation base as JSON lines. After warmup, the observer captures the list every ten seconds and also once per second in the last 15 seconds.

Use `-IncludeVirtualMemory` on Windows x64 to record committed address ranges by region type. Follow the [ERD memory observation guide](erd-layer-lab.md) when comparing these ranges with GC counters.

Enable `DUXEL_ERD_TRACE_VIEWPORT=1` to log allocations and layer rebuild counts on ERD viewport changes. [ERD diagnostics](erd-layer-lab.md)

Image09584FF4,30-second preparation:over179.97active seconds,first/last-third private-byte medians rise118.31MiB and working set116.41MiB. Cause remains unresolved;this is not a leak-free result. Handles511–521,threads13–21,normal exit. Over59.84idle seconds:zero additional logged frames,46.875ms CPU(about0.0783% of one logical core),private/working-set median changes-0.168/-0.082MiB,normal exit. Completing2/2observations does not complete memory validation. Evidence:`artifacts/perf-2026-11-03/resource-observer-audit.json` and individual `samples.csv` files.

Optional `DUXEL_APP_PROFILE_MEMORY=1` adds an estimated current managed heap,last-collection commit/heap sizes,generation collection counts and total allocations only to emitted App profile records(default0). It does not force GC. Observer `-IncludeGcMetrics` probes the first profile for this capability and writes in-window `gc-samples.csv`. This diagnostic needs a newly built candidate;existing09584FF4does not contain it. The1000FPS evidence and default EXE remain protected.
GC diagnostics pass NET10/NET11 GPU406each,Release0warnings/errors and completed-profile parsing6/6. The first GC profile of NativeAOT diagnostic candidateB13F2B47is verified. Default09584FF4and its1000FPS evidence remain protected. The new180-second cause-isolation observation is tracked in resource-gc-state.json;memory validation remains incomplete until observation and cause assessment finish.

Optional `IMemoryOwner<byte>` properties `PlatformTextRasterizeResult.PixelOwner` and `UiTextureUpdate.PixelOwner` transfer exclusive pixel ownership. Pixels remain immutable while consumed. `UiDrawData.CaptureTexturePayloads()` copies existing borrowed updates;an exclusively owned update stays with its frame without another copy. Frame consumption/discard,superseded queued updates,measurement-only text eviction and context disposal release ownership. GPU textures retain the existing fence/retirement lifetime.

Windows multiline bitmap composition uses returnable pooled storage,clears its pixels and performs the same composition. Measurement-only backend calls also return storage.3D vector layers,shaders,font outlines and camera quality remain unchanged. Direct rasterization callers dispose PixelOwner after use or transfer it once to a texture update;they must not return or mutate storage after transfer.

Validation:.NET11 GPU422,.NET10 text-atlas422,direct398;all119before/after GPU images are byte-identical. Lifetime checks cover mixed owned/borrowed frames,consumer-thread return,frames outliving their context,unconsumed frames,cache eviction and actual raster byte reuse including transparency. Candidate52E300FA's first180-second observation has private/working-set median changes-10559488/-11585536bytes;this is one run. Quantitative claims await three independent runs per variant. Default1000FPS image09584FF4remains protected.

Latest repeated memory validation (2026-11-03): B13F2B47 before/52E300FA after,three runs each,30-second preparation plus180active seconds,1200tables,camera plus entity motion,no benchmark output. All6/6runs exited normally and passed raw-sample,GC and hash audit. First/last-third private-byte growth medians are116.33MiB→0.375MiB(99.68%less growth,disjoint ranges);managed-heap growth123693912→-721148bytes;GC-commit growth122568704→0bytes. All-thread allocated bytes per logged UI frame are9067.79→8064.53(-11.06%,disjoint ranges). This is neither99.68%less total memory nor universal leak/VRAM validation. CPU ranges overlap,so no CPU gain is claimed. Evidence:`artifacts/perf-2026-11-03/pixel-owner-resources-summary.json`. Current-source MSAA boundary correctness,FPS and runtime revalidation remain separate work.

MSAA depth boundaries and actual embedded-shader validation follow the2026-11-03fix in [dynamic3D layers](dynamic-3d-layers.md). NET10/11each pass435checks,direct411,status panel9each;new ERD FPS remains in measurement. Five relevant local repository skills were checked against official sources,updated and structurally validated(locally Git-ignored files,pinned baseline SDK preserved).

Latest measurement(2026-11-03): NativeAOT694550C9 includes the MSAA boundary fix and pixel ownership reuse.1200tables,dense/resident,MSAA4,VSync off,actual post-preparation18seconds×3per scenario:camera937.19FPS(93.72%,range919.51–982.09),camera plus entity motion960.30FPS(96.03%,942.55–970.95),best single-run average982.09FPS. Both remain below1000. Preserve09584FF4's earlier1000+records as historical evidence with its reproduced MSAA boundary defect;they do not establish latest-source correctness/FPS. Retain the correctness fix and do not infer an isolated regression percentage across these stages. Raw frame statistics,presentation/source links,warmup,initial pose,settings and hashes pass6/6audit. A one-off parent wrapper incorrectly checked native LASTEXITCODE after the PowerShell runner completed and exited1;the existing runner verified all6normal child exits. Results were directly audited without rerunning the expensive workload. Evidence:artifacts/perf-2026-11-03/coverage-erd-current and fps-current.json. Default09584FF4EXE/PDB was not replaced;latest candidate retains its binding-candidate path. Final-source NET10/11 Managed/AOT comparison and throughput work remain.

## Automatic popup and foreground presentation layers — 2026-11-04

Menus, popups, context menus, combos, tooltips and foreground drawing share dynamic presentation layers. Widget input and layout still execute; identical geometry and commands reuse immutable snapshots. Replaced buffers retire after the final queued-frame reader finishes. Screen-anchored planes require no dedicated depth images and preserve alpha blending, ClearType and scissors. Ordinary 3D-scene depth testing remains available.

`SetNextPopupPosition(UiVector2)` positions the next `BeginPopup` in display coordinates. `GetOverlayLayerMetrics()` returns `ReusedLists` and `RebuiltLists` for completed presentation scopes in the current UI frame. Position changes update that popup's geometry; this does not skip arbitrary widget input or make every move matrix-only. `UiLayer3D.PresentationDepthScopeId` is reserved.

Dedicated sample: `samples/fba/overlay_layer_lab.cs`. See [the presentation layer lab](overlay-layer-lab.md) for execution and comparison.

The common batcher's transient builder no longer rents a second full-size working array after transferring geometry ownership. Regular frame builders retain capacity as before. Validation passed 510 checks on each of .NET10 and 11 and preserved 155 prior images. Against the previous distribution, large-load private bytes decreased by 1.84MiB idle and 3.32MiB moving; overlapping FPS ranges make this stage's throughput difference inconclusive. Current NativeAOT EXE SHA256: `40C51851C9469653BA40DBCD9B8FE36ABD2D3411538E1A5FC8090AB4DD786B92`.

Rejected temporary-build deletions do not stop independent development. Completion reports retain exact targets and [manual cleanup guidance](rejected-build-cleanup.md), without reporting deletion as completed.

For another session, read the [2026-11-04 conversation summary and handoff](../SESSION_HANDOFF.md) (Korean). It records the completed integration, executable hashes, validation evidence, remaining optimization candidates and a copyable continuation prompt. Recheck the current worktree and evidence before using its recorded state.
# Comprehensive feature showcase

See [sample validation and execution](all-features-validation.md) for `samples/fba/all_features.cs`. `run-fba.ps1 -ArtifactRootDirectory` selects a dedicated NativeAOT output below repository `artifacts`, retaining ownership and retention checks. `TextWrapped` respects the current column's available width, and `UiFpsCounter.Tick()` counts each frame using high-resolution timing.

The [common optimization assessment](optimization-evaluation.md) distinguishes automatic GPU reuse from opt-in CPU layer retention. Empty-damage frames skip streamed geometry uploads and draw planning while retaining input, layout and submission. Reuse `--widget-optimization-only` for ordinary widgets, scrolling and resize validation. Pinned RC3 comparison confirmed idle FPS+5.63% and zero uploads; Private reduction remains inconclusive and idle working set increased about0.50MiB. Raspberry Pi hardware performance is unverified.


## DirectText layout/cache update — 2026-11-05

Optional `IPlatformTextLayoutBackend` and revision contracts, a single widget-owned metrics memo and rasterization after clipping now share the common widget path. Backend/revision changes invalidate static layers.10000-button CPU median79.0671 to2.07605ms is not application FPS or Pi performance. See [implementation,matched measurement,lifetime and GPU validation](directtext-layout.md) and [separate particle precision correction](particle-contact-precision.md).


## Badge/CounterBadge follow-up

Default Badge factories and generated discovery now use common inherited widgets.64 appearance/color combinations,six sizes,three shapes,counter visibility/overflow/dot are implemented. Official visual parity,UIA,RTL/forced colors remain open. See [API,generated code,lifetime and validation](fluent2-badge.md).

Badge source/options publish before frame requests and retain only behavior-specific value-type state. Final validation is.NET10 full1095/focused49 and RC3 Native243 per EXT/core,distinct from complete component/UIA/official visual conformance. The gallery guide includes the latest --badge invocation.


## Cross-platform reference follow-up

46 Android Compose-v2 sources/54 classified rows now feed actual generator input. Known required names increased289 to325,with26 standard factory names and299 unconnected; the final denominator remains open.81 generator/72 widget-tree checks and managed/RC3 Native catalog parity passed. See [sources,mappings,regression and remaining scope](fluent2-platform-inventory.md).
# Fluent 2 scope correction (2026-11-05)

Follow the [official-site implementation scope](fluent2-scope.md). Production generation separates55 official-site top-level names and19 parented variants. The325 repository research names are not the goal denominator. Evidence: artifacts/fluent-site-scope-2026-11-05/verification.json.

Text and generated typography Source/Color changes publish state before requesting an idle frame; equal assignments do not request one. Dispose releases the source and rejects subsequent mutations. See [text lifetime validation](fluent2-text.md).

[Divider](fluent2-divider.md) shares generated static/dynamic widgets and Dux.Divider/ui.Divider presentation. UiTheme.FluentColorScheme is shared color data; BadgeColorScheme is a compatibility alias. Gallery: --divider;8 top-level factories and19 variants. Official parity/UIA/forced colors/arbitrary-child slot remain unfinished.

Common Button Label/Size publish before waking idle frames; equal assignments do not wake. Callback-only replacement does not repaint. A separate framework stage releases Button/Text/Badge/Divider resources even if user OnDispose throws or omits base. See [mutation/disposal contract](widget-implementation.md). Other widgets still require participation audit.

Progress Source/Size, Slider Label/Options/Size and Toggle Label participate in common invalidation and owned-reference release. Disposed Slider/Toggle Value access throws ObjectDisposedException; caller-owned state is not disposed. Current27 names/7 base families participating does not qualify full official features. See [ownership follow-up](widget-implementation.md).

## Actual Button fonts and sizes

The generated default Button uses actual Semibold14/20. Bind small Regular12/16 or large Semibold16/22 through `UiButtonStyle`. Surrounding-font restoration, bounded metrics and static-layer invalidation share the common path; the non-generic immediate Button retains its compatibility presentation. [States, validation and remaining scope](fluent2-button.md), [run guide](widget-gallery-run.md).

The current-source `--buttons` page also includes primary/outline/subtle/transparent. Bind them statically to shared behavior using `UiButtonStyle` and the size-specific `UiFluentButtonPresentation<TAppearance>` family. [Exact API and remaining scope](fluent2-button.md).

The `--buttons` square/circular size rows also use generated shared widgets. Compose a shape statically inside an appearance, such as `UiFluentPrimaryButtonAppearance<UiFluentCircularButtonShape>`. User frame algorithms share this extension point. See [scope and limits](fluent2-button.md).

Bind `UiFluentIconButtonPresentation<TSize,TAppearance,TIcon,TPosition>` through a generated style. The `--buttons` Add icon uses the pinned official font, including a user-declared generated icon type; icon-only UIA remains incomplete. [Composition, validation and limits](fluent2-button.md).

See [official icons](fluent2-icons.md) for the bundled fonts and `[UiFluentIcon]` generation path. Add regular/filled/interactive is provided; full symbol API, SVG/browser parity and UIA are not complete.

Equal selected icon metadata reuses source construction/output. Generator functional107 and all57 Core-generated files were checked. Reemission after shared-file declaration removal remains incomplete; see [tracking and limits](fluent2-icons.md).

The DirectText harness now honors current Fluent Semibold14/20 and passed three alternating independent runs per variant on fixed RC3. CPU78.9842→1.9952ms and Private77451264→36249600B compare existing capabilities under the same current font policy, not a new engine patch/GPU/app FPS improvement. [Conditions and limits](directtext-layout.md).

[Switch sizing, composition and validation](fluent2-switch.md)

[Link navigation, inheritance, static presentation and cache](fluent2-link.md)

[Single-line input behavior, presentation and lifetime foundation](input-kernel.md)
## Fluent Field composition

`Fluent.Field(UiWidget control, UiVector2 controlSize, UiFieldOptions options)` and `Dux.Field` own a real child. They share label focus,required/validation/hint composition,Input error propagation and Build lifetime. Default/user Fields inherit the generated `UiField<TBehavior,TPresentation>` path; presentation types select size and orientation statically. See [API,lifetime and limits](fluent2-field.md) and the [run guide](widget-gallery-run.md). `CurrentField` metadata does not establish Windows UIA support.

## Common widget measurement

Use `UiWidget.Measure(ui, availableSize)`,`UiControlContext.Measure(child, availableSize)` and the `MeasureContent` override. Built-in form controls share presentation measurement without reading value bindings or invoking callbacks. `Fluent.Field(control)` selects natural sizing; explicit-size constructors remain available. The immediate API is `AutoField<TBehavior,TPresentation>`. Unsupported measurement never substitutes rendering. See [scope,lifetime,failure and examples](widget-measurement.md).

# Widget accessibility foundation

See [widget-accessibility.md](widget-accessibility.md) for the common semantic tree, actual Windows UIA Button/Input provider, generated COM bridge, queued actions and explicit remaining scope. `UiWidget.AccessibleName` names icon controls; Field metadata applies to the primary child. This does not complete all Fluent roles, patterns, events or official fidelity.

# UIA removal and external Field qualification

`UiAccessibilityTree.ElementsRemoved` notifies outside the publication lock. Windows prunes weak provider keys/capacity immediately and drains retired native providers through a queued window message. See [accessibility](widget-accessibility.md). Focused NET10/RC3 NativeAOT40, external Field14 and independent lifetime host3/client4 pass; long-duration memory, events and broader Fluent qualification remain open. Evidence: `artifacts/uia-lifetime-2026-11-06/verification.json`.

# Fluent Spinner checkpoint

[Spinner](fluent2-spinner.md) now uses a generated inheritable control, separate static behavior/presentation, common annular geometry and actual UIA ProgressBar semantics. Use `Fluent.Spinner`/`Dux.Spinner` or caller-owned immediate behavior state. Gallery: `--spinner`. Eight sizes, appearances, label placement, delay and RTL/reduced motion are implemented; official browser fidelity, slots/forced colors, notifications and deadline scheduling remain open. Catalog32=13/55 top names+19 variants;42 top names remain unconnected.

Shared UiWidget Build storage now reuses keyed ownership and existing identity slots;dependency scratch lists reside on existing dictionaries. Fixed RC3 Native3+3 runs per mode show lower construction/first-Build allocation with unchanged steady0B,101 lifetime and175 container checks,GPU121 exact. See [scope and limitations](widget-cpu-retention.md#shared-widget-build-storage--2026-11-06). Full-goal counts unchanged;resize remains deferred.

Generated Infrastructure helpers: Dux.Padding,Align,Center,SizedBox,Expand,ConstrainedBox use UiSingleChildBox with statically selected layout/options/presentation;existing Container shares its ownership/cache base. See [constraints,examples,229 managed/Native checks,GPU159 and first-layout storage limits](widget-containers.md#generated-single-child-layouts--2026-11-06). Container stage3/6;remaining leaf/scroll/full integration work open.

Slider/Toggle presentations now expose static Arrange for changed parent allocations;generated widgets opt into the same constrained measure/paint/input path. User immediate controls can opt in with constrainLayout:true;legacy overloads stay natural-sized. See [contracts and172 managed/Native checks,GPU43](widget-containers.md#parent-allocation-for-slider-and-toggle--2026-11-06). Other leaf families remain open;container stages3/6 unchanged.

Input presentation static Arrange now resolves parent allocation for field/editing/text/owned-child geometry;live resize preserves editor identity and undo. Existing immediate calls stay natural;new explicit constrainLayout overload and public MeasureInput share the common path. See [212 managed/Native checks,GPU25 and limits](widget-containers.md#editable-input-parent-allocation--2026-11-06). Containers3/6;remaining leaf families open.

2026-11-11 observer idle/focus repair: Spinner122, observer73, TabList197, scheduling35, GPU panel16 and observer43 checks pass. The Fluent page starts paused with explicit playback. Actual pixels verify all four panel edges including right/bottom padding. Evidence: artifacts/observer-idle-panel-2026-11-11/verification.json. NativeAOT, the whole goal and measured throughput improvement are not claimed.


2026-11-11 shared menu navigation: UiMenuOptions.MaximumHeight, viewport height limits, shared scrollbars, Unicode initial-character cycling and keyboard row reveal use one path for theme/Fluent/tab overflow menus. Cumulative search, complete submenu/checked/UIA conformance remain pending. See [menu guidance](fluent2-menu.md).


Menu navigation evidence: artifacts/menu-navigation-2026-11-11/verification.json. Managed menu95, observer75, tab overflow20, scheduling35, accessibility49, widget tree72 and GPU124 pass. Separate theme/Fluent clients each4/hosts2 exit0 qualify existing-menu regressions, not the entire new scroll UIA contract.


2026-11-11 menu scrolling accessibility: Menu Scroll and row ScrollItem reuse existing requests and notifications. Independent popup boundaries stop reveal propagation to the outer page. Managed menu117, observer77, ScrollView75, virtual list75, GPU132 and separate UIA client6/host3 pass. Evidence: artifacts/menu-scroll-uia-2026-11-11/verification.json. Retired direct-provider reads and external mutations are rejected; observed S_OK/0 from the retained external pattern getter is recorded separately. Full menu/narration/NativeAOT remain pending.


2026-11-11 Slider hover: UiFluentSliderPalette.Resolve/UiSliderPaint replace the neutral-button hover mapping with Fluent compound-brand and neutral state tokens. Light/dark hover retains brand colors and stays distinct from disabled. Fixed ProgressBar pixels remain independent of Slider hover. See [Slider guidance](fluent2-slider.md) and artifacts/slider-hover-2026-11-11/verification.json.


2026-11-11 menu expansion: UiAccessibilityNode.Expansion/UiAccessibilityExpansionState, ExpansionChanged and Expand/Collapse requests connect real menu buttons and tab overflow to idempotent states, MenuOpened/MenuClosed and Windows ExpandCollapse. Close precedes unpublication; open follows publication. Tooltip shutdown lifetime is regression checked. See [menu guidance](fluent2-menu.md) and artifacts/menu-expansion-2026-11-11/verification.json for scope and limits.

2026-11-11 checkbox menus: UiMenuOption.Checkbox borrows UiBinding<bool>; activation updates state before callbacks and keeps the menu open. Both built-in presentations share a logical checkmark column. Custom presentations implement UiMenuItemContent overloads explicitly. UiAccessibilityNode.Checked/ToggleChanged and Toggle requests connect Windows pattern10015 and properties30041/30086. Observer: Containers → Menu common behavior → 체크 메뉴 열기. See [menu guidance](fluent2-menu.md), artifacts/menu-checkbox-2026-11-11/verification.json. The full menu visual/narration scope remains open.

2026-11-11 radio menus: UiMenuRadioGroup(name, UiBinding<string?>) and UiMenuOption.Radio define contiguous, named exclusive groups with independent borrowed state. Activation commits peer marks and closes; selected-radio removal is rejected. MenuGroup exposes Windows Group/Selection, with child SelectionItem and the real group as container. Transient selection/property events use immutable event snapshots with read-only SelectionItem, retaining final cached state after dismissal without allowing stale mutations. Observer: Containers → Menu common behavior → 라디오 메뉴 열기. Evidence: artifacts/menu-radio-2026-11-11/verification.json; [contracts](fluent2-menu.md). Submenus, icon/secondary content, shadow16/forced colors and narration remain separate work.


Legacy SliderFloat/SliderInt, scalar/vector/array wrappers, SliderAngle and VSliderFloat/VSliderInt share the typed numeric queue and live enabled/clip/range validation. Float values remain continuous: horizontal step is a keyboard increment, not a numeric snapping grid. Continuous controls report SmallChange0; integers report1 and retain ties-to-even float rounding. Angle publishes degrees and writes radians to its caller. Integer publication reflects final wrapper conversion. Existing float-based integer/double precision and keyboard/focus behavior are unchanged; no full-width precision or new Focus capability is claimed. Legacy tab selection/panel relationships are qualified separately; full keyboard/flag coverage remains open. Focused legacy16 (combined44), Observer127, external legacy client6/host3 and Observer GPU47 including actual pointer-selected Immediate controls pass. Run ./run-gallery.ps1 → Existing API → Hello / Immediate. Evidence: artifacts/slider-range-2026-11-11/legacy-verification.json.

### Legacy BeginTabBar/BeginTabItem semantics (2026-11-11)

The streaming legacy API now publishes a TabList, real Tab SelectionItems and only the active TabPanel, with reciprocal ControllerFor/LabeledBy links. Body controls belong to their panel; nested bars restore the original outer parent. TabItemButton remains an Invoke button, outside tab selection. Required selection rejects removing the selected header. Closing or removing a pending target cannot report a successful selection that was never published.

Pointer and accessibility selection preserve the existing next-frame transition, with one body per frame. A UiAccessibilityTree Select request with wait=true completes after that selected frame is published; closing/removing the target before publication returns InvalidOperation/Unavailable. Existing Windows SelectionItem methods still accept requests asynchronously: observe committed property/selection events before reading the new state. Selected headers/panels keep the legacy nonfocusable contract; full keyboard/flag coverage and narration remain open. A committed transition schedules no continuing wakeup or animation deadline.

Managed legacy25, common TabList197, actual Observer131, external legacy client15/host3 and generated-tab regression client25/host3 pass. ObserverGPU47 passes and all73 comparable body captures match the prior numeric-accessibility baseline (FPS header excluded). Tests distinguish equally named headers/panels by their actual TabList scope; generated panel checks still require focusability by default, while legacy checks explicitly require false. Run ./run-gallery.ps1 → Existing API → Hello / Immediate. Evidence: artifacts/legacy-tabs-2026-11-11/verification.json. NativeAOT/full-goal qualification is separate.

## Standalone icons

`Fluent.Icon(painter, new(24, 24), foreground)` creates a decorative retained widget. Use `UiMenuIcon.Create<TRegular, TFilled>()` for compiled font/vector painters or `svgIcon.MenuIcon` for an existing `UiSvgIcon`. A standalone icon always paints the regular/rest variant; it has no pointer activation, focus stop or hover animation. Supply the foreground explicitly. SVG authored colors remain unchanged unless the borrowed painter uses `MultiplyForeground`.

The widget borrows its painter. Dispose widgets before their `UiSvgIcon`, and dispose SVG painters before the document/store. SVG source reload requests a frame through the existing SVG texture path. Repeated unchanged painting requests no periodic frame and reuses its raster/texture. `Size`, `Color` and `Icon` changes request a frame; invalid/empty inputs fail explicitly. `Size` is logical layout space; parent constraints can change the allocated viewport. This is not an accessible image role or an interactive button; use a labeled control for actions.

Run `./run-gallery.ps1` from the repository root, select **SVG**, then find **독립 SVG 아이콘 (Fluent.Icon)** below the icon menu. The same page also compares bitmap input with the transformable `UiSvg` widget. This demonstrates the new factory; full Fluent conformance remains incomplete.

## Retained Image API

Use Fluent.Image with immutable UiImageSource for PNG/SVG bitmap snapshots and per-context texture leases. See [retained images](image-widgets.md) for loading, layout, lifetime, cache limits and Observer verification. Full Fluent image styling/accessibility remains open.

Latest cache-lifetime evidence: artifacts/raster-lifetime-2026-11-12/verification.json.45 managed suites/7803 assertion executions,10 new CPU checks and17 GPU checks per configuration pass. The earlier44-suite result remains historical evidence.

Latest Label qualification: artifacts/fluent-label-2026-11-12/verification.json.46 suites/7822 assertion executions including19 Label contracts, Label GPU28 andObserver50 pass. Legacy core GPU4642 and4038 unchanged prior images were also checked. [Standalone Label API and execution](label-widgets.md). Previous45-suite results remain historical evidence.

## Standalone presence badge
`Fluent.PresenceBadge` supports eight statuses, OutOfOffice and six sizes. Run `./run-gallery.ps1` → **아바타** → **상태 배지**; change status, OutOfOffice and size. The scrollable page preserves state and requests no periodic frames. Default icons use compiled Fluent font glyphs; supplied SVG painters reuse their cache. [API, resource lifetime and validation](fluent2-presence-badge.md). Avatar overlay and activity ring are described below; full inactive appearance remains open.

## Activity ring and gallery scrolling

Set `avatar.Options = avatar.Options with { ShowActivityRing = true };` and optionally `avatar.ActivityText = "active";`. The default name gains the activity wording; `AccessibleName` still overrides it. The label is cached on mutation. A ring remains one passive Image with no action, focus, hover or animation timer.

The body keeps the selected14-size contract at natural size. Native layout measures the complete visual extent: stroke2 for body sizes<=48,3 for<=64,4 above64; the ring extends two stroke widths on each side (32px body→40px total). This reserves the decoration instead of painting over neighboring allocations. The body and gap scale uniformly under tighter parent allocations. Circular/square geometry and Light/Dark ring colors reuse the existing analytic border-only primitive and pinned persona palette. The gap paints nothing; it preserves the underlying pixels. Image sizes and ring toggles reuse the existing texture lease. `ShowActivityRing=false` restores the ordinary avatar; it does not claim Fluent inactive appearance.

Run `./run-gallery.ps1` from the repository root→**아바타**→**아바타 활동 링**. The sample sets Korean activity wording. The page now uses the common `UiScrollView`, allowing its growing gallery to exceed the viewport without flex overflow. Actual scroll-to-bottom and offset/model preservation across tabs are checked at800×400.

Current evidence: `artifacts/fluent-avatar-ring-2026-11-12/verification.json`, managedSDK11.0.100/runtime11.0.0. Full Fluent active/inactive transitions, forced colors and original exactRC3 NativeAOT/full-workload qualification remain open. [PresenceBadge](fluent2-presence-badge.md) is available standalone and as the Avatar overlay described below, with a shared transparent cutout across image/initials/icon/ring paths.

## Circular subtraction
Use CircleCutout for a real transparent hole shared by primitive/image/text/SVG paint and retained/compiled layers. Run `./run-gallery.ps1` → **렌더링** → **원형 잘라내기**. [API and validation limits](circle-cutout.md). Avatar presence consumes this mask; see the overlay contract below.

Qualification snapshot:51/51 CPU suites,8136 assertion executions(CircleCutout20,Observer189);88 mask checks per configuration, five configurations with the actual Khronos validation instance/device layer loaded and no captured VUID/error. Configurations cover frame resources2/3,shared camera/basis settings,generic compiled mode and both schedulers. Additional GPU checks:primitive74,rounded-image40,SVG74,Avatar220,Presence552,Observer55. Assertions repeat across settings; these are not1455unique feature requirements. The sample now draws its execution counter after the actual body construction,so the same event frame reports its committed count without requesting another frame.

## Avatar presence overlay

Set `Presence` on `UiAvatarOptions` to `new UiAvatarPresenceOptions(UiPresenceStatus.Available)`; null removes it. `Size = null` selects the pinned automatic badge size: avatar <28 →6px, >=28 →10px, >=40 →12px, >=56 →16px, >=64 →20px, >=96 →28px. An explicit `UiBadgeSize` overrides this mapping. The badge anchors to the body's bottom/right, or bottom/left with `RightToLeft`. The transparent gap is1px through Medium and2px for Large/ExtraLarge. Image, initials, font/SVG icon and activity ring share the common analytic circle subtraction; the badge paints after that scope is disposed. No background-colored patch is used.

Natural measurement reserves the union of body, activity ring and badge, including an explicit badge larger than the body. The union fits uniformly inside the actual parent rectangle. `PresenceText = null` uses the status wording; set a nonempty complete localized status (including OutOfOffice when applicable) to override it. The cached accessible name combines person, status and activity; `AccessibleName` overrides it. There is still one passive Image provider, no child badge provider, focus stop, Invoke or animation timer. Existing source leases and borrowed icon ownership apply.

Run `./run-gallery.ps1` → **아바타** → **아바타 상태 배지**. Controls change status, OutOfOffice, automatic/explicit size and RTL; image, Korean initials, SVG icon and ring controls remain available. State persists across tabs. Managed validation and limits are recorded under `artifacts/avatar-presence-2026-11-12/`. Full inactive appearance/group opacity, motion/forced colors, full Fluent conformance and original exactRC3 NativeAOT/performance gates remain open.

Validation:51/51 CPU contracts,8193 assertion executions(Avatar185,Observer191);Avatar GPU380 across Light/Dark and MSAA1/4,Observer11,native Avatar17. Existing233 Avatar images are byte-identical. This is managedSDK11.0.100/runtime11.0.0 qualification. External draw callbacks and distinct nested circle scopes remain unsupported by the common mask contract.

## Avatar activity shadow

Set `ShowActivityShadow = true` independently of `ShowActivityRing`. Both add the cached ActivityText once to the same passive Image name. The pinned body-size mapping is <=28→Shadow4,<=48→Shadow8,<=64→Shadow16,larger→Shadow28. Shadow-only uses the body contour;with a ring it uses the complete ring contour. The measured union reserves shadow support,ring and an optional oversized badge,then fits uniformly to the actual parent rectangle. The shadow paints before body/ring subtraction and the badge,matching the pinned shadow pseudo-element. Image/initials/font/SVG all use this shared path.

Run `./run-gallery.ps1` → **아바타** → **아바타 활동 그림자**. State persists across tabs and no periodic frames are requested. [Common shadow API](box-shadow.md). Full inactive0.875scale/group0.8opacity and transitions/forced colors remain open;existing per-command layer opacity cannot substitute for group composition. Current managed evidence: `artifacts/box-shadow-2026-11-12/`.

Validation:CPU52/52 contracts,8247 assertion executions(Shadow37,Avatar201,Observer192);four explicitly verified compiled profiles x256shadowGPU checks,Avatar392,cutout88,primitive74,rounded40,Observer12;nativeAvatar19. Assertions repeat across settings. Existing498 image files remain byte-identical. Sample description uses the common wrapped text presentation,including the800x400 scroll regression.


`Fluent.CardFooter(children, action)` owns 12px-spaced content and an optional far-end action. It uses shared retained measurement and transparent accessibility structure. Run `./run-gallery.ps1` → 카드 to inspect the live footer. Card Preview is documented below. See [Card](fluent2-card.md).


`Fluent.CardHeader(header, description, image, action)` owns four optional slots; source order is image/title/description/action. Two centered auto rows are used when description exists, otherwise a flex row. See [Card](fluent2-card.md). Card Preview is documented below.


2026-11-12: cached paint groups retain their DirectText source keys through the same reference/revision store as retained layers. Sources survive600-frame idle eviction while a group owns them; version replacement and last-owner disposal release them. Cached nested paint propagates text dependencies. No layer store is required for paint texture retention. This fixes Implementation Observer rendering-tab termination with `Paint-group source texture is missing`. CPU55/8565,new lifetime15,GPU2477,native50 and two separate614-frame application sessions pass. Evidence: `artifacts/observer-rendering-freeze-2026-11-12/verification.json`.


`Fluent.Card(..., selection: binding, floatingAction: widget)` owns an immutable top/right4px slot without intrinsic growth. The supplied control replaces the hidden selection proxy, paints last and navigates first. Bind a real checkbox explicitly; child disabled state stays independent. `OnSelectionChanged(UiBinding<bool>, bool)` commits the captured frame binding. Custom floating radii use the shared body clip and curved pointer boundary. See [Card](fluent2-card.md); evidence artifacts/card-floating-action-2026-11-12/verification.json.


`UiDrawListBuilder.RoundedClip(bounds, radius)` and `UiControlContext.RoundedClip` provide nested analytic paint intersections, composed with circular subtraction. Relative mask ancestry survives retained copies; damage tracks all ancestor radii. This scope does not add input/layout behavior. [Rounded paint clipping](rounded-paint-clip.md). Run `./run-gallery.ps1` → 렌더링 → 둥근 클리핑. Card Preview is documented below.


Card clips primary/floating paint and pointer acquisition to the same body curve. Custom metrics support padding0 and large radii; use Dux.Expand for body-filling content. Root shadow/focus and higher popup tiers remain independent. Native Focus/Invoke/Toggle and keyboard navigation still work. Run `./run-gallery.ps1` → 카드 → 둥근 경계와 입력. [Card](fluent2-card.md); evidence artifacts/card-rounded-content-2026-11-12/verification.json. Card Preview is documented below.


`Fluent.CardPreview(content, logo)` owns media and an absolute32px logo placed12px from the left/bottom. The logo does not contribute to intrinsic size. Put direct slots in `Dux.CardContent([...])` as the Card's primary child (composition-only wrappers are supported). Preview bleeds through the Card padding: vertical left/right and first top/last bottom; horizontal top/bottom and first left/last right. `UiCardOptions.Orientation` defaults to Vertical. Horizontal final Header/Footer grows into remaining bounded width. Other custom child trees keep their own layout. Main-axis overflow fails explicitly; CSS flex-shrink/min-content conformance remains open. Preview content and logo retain their actual input/accessibility providers; Card border paints above Preview. Replays keep measured allocations without scheduling periodic frames.

Run `./run-gallery.ps1` → **카드** → **미디어 미리보기**. Compare vertical/horizontal SVG cards, toggle preview width/size, and inspect the fixed logo and full-bleed edges. SVG is loaded once per content lifetime. Evidence: `artifacts/card-preview-2026-11-12/verification.json`. The46 exposed factories still connect23 of55 top-level names; this variant is not another top-level completion.


CardPreview qualification: full managed CPU55/8754(Card407), affected Observer followup223 after test-only additions, twelve GPU profiles2673(Card608), native Card27/CardFloating30/Avatar23, hidden/visible application sessions614frames each. Of2316 unchanged-baseline images,2300 are exact and16 differ only in the dynamic HUD above y32; changed gallery and new images are separately qualified. Ordinary preview was not launched. Evidence: `artifacts/card-preview-2026-11-12/verification.json`. Full official conformance and original RC3 deployment/performance gates remain open.


2026-11-12 minimum allocation correction: CardContent and direct vertical CardPreview preserve the Card body's minimum after shadow/padding deflation. Vertical Header/Footer first resolve their intrinsic width before the body stretches; a horizontal final Header/Footer receives remaining minimum width even when the maximum is loose. Preview keeps its natural main-axis extent; horizontal media centers within the minimum height. The prepared parent-input snapshot keeps replay stable. Run `./run-gallery.ps1` → **카드** → **부모의 최소 크기**; change parent minimum width and orientation. This uses an actual constrained parent without a fixed Card width. Full CSS min-content/flex-shrink conformance remains open.

Evidence: `artifacts/card-minimum-allocation-2026-11-12/verification.json`. Nine initial contract failures were reproduced; after both minimum propagation and intrinsic-slot correction, full CPU55/8772(Card419,Observer226), GPU12profiles2725(Card656), native80 and hidden/visible614-frame sessions pass. Of2332 existing RGB image baselines,2312 are exact and20 differ only in the dynamic HUD above y32;32 new captures and the changed Observer gallery have independent geometry/pixel/replay checks. Timing improvement was not measured. Factory/top-level counts remain46 exposed,23 connected of55. The original deployment/performance/full conformance gates remain open.

Additional in-flight regression: the Card GPU profile also passes656checks with requested MaxFramesInFlight3, giving13profiles/3381checks in total. All517 matching captures between requested2/3 slots are RGB byte exact. This does not measure actual simultaneous frame count.

2026-11-13 [Popover owned menus](fluent2-popover.md): An owned menu portal participates in hover retention and outside-pointer containment. Hovering it cancels the leave deadline; clicking its item invokes once without dismissing the parent Popover. Escape closes the menu first, then the parent on the next press. Open menus retain their trigger focus inside trapped Popovers too. Observer → Popover → 팝업 내부 메뉴 exposes the actual path.

2026-11-13 [Popover entry motion](fluent2-popover.md): default400ms group fade, directional10px slide, immediate exit, same-time caching, live input/IME/accessibility geometry and finite scheduling. Observer → Popover → 팝업 모션. Full conformance remains incomplete.

2026-11-13 [SearchBox clear](fluent2-searchbox.md): clear after a held pointer click preserves model/display synchronization, focus, Tab navigation, undo and Korean retyping. Observer → 검색 입력.

2026-11-13 [Popover paint extent](fluent2-popover.md): custom PaintBounds, viewport default, bounds/budget checks and wide-decoration sample preserve Fluent pixels. A native Popover failure remains under diagnosis; full conformance is incomplete.


`UiPopoverOptions.CloseOnFocusLeave` defaults to `false`. Enable it to dismiss when focus leaves the trigger, content, or an owned nested surface for another control, preserving that destination focus. It also observes departure from the trigger when autofocus is disabled. A rejected binding write keeps the surface open; reentering and leaving the region rearms dismissal. No Timer or periodic frame request is added. In Implementation Observer, choose Popover → **포커스 이탈 시 닫기**. This supplies shared behavior required by the official InfoButton; it does not complete InfoLabel itself.


### Drag-scroll tab headers

Set `UiTabListOptions.Scroll = new UiTabListScrollOptions()` to enable horizontal header scrolling. This is a separate choice from the existing `Overflow` allocation mode; do not enable both. Implementation Observer uses this mode for its top-level pages.

Drag the header or use the wheel. Each ellipsis appears only on an obscured edge, allowing left-only, right-only, or both. Clicking an ellipsis opens that side's hidden-tab context menu without moving the header. Choosing an item selects and reveals the original tab and panel. Selecting or focusing an edge tab also reveals one neighbouring tab in that direction to support continued navigation. Releasing a drag does not activate the tab under the pointer.

The bottom scrollbar overlays the header without reserving layout space. Defaults are120ms entry,400ms stationary hold and160ms exit; a held drag keeps it shown. Configure timing, curves and reduced motion through shared `UiScrollbarMotionOptions`, with per-tab overrides in `UiTabListScrollOptions.Motion`. Settled animation stops scheduling frames. See the [shared transition design and API](finite-transitions.md).


All scrollbar families use the same [shared motion module](scrollbar-motion.md).


Direct comparison with the official React demo confirms a pointer cursor while retaining the transparent default hover background. Disabled headers use a not-allowed cursor and disabled foreground. Duxel applies that cursor only within the actual clip and unoccluded popup tier. Keyboard Tab shows a focus outline; Enter and Space retain the existing header button contract. No extra hover background is added to the official default presentation.

Sources: [design guidance](https://fluent2.microsoft.design/components/web/react/core/accordion/usage), [live React demo](https://react.fluentui.dev/?path=/docs/components-accordion--docs). Browser and pinned-source evidence is preserved in `artifacts/accordion-hover-2026-11-13/official-evidence.json`. This checks the stated interaction scope, not complete Fluent visual or motion conformance.


InfoLabel: `Fluent.InfoLabel(text, info, target, options, informationLabel)` borrows its input target and owns supplemental popover content. See [API, sizing and ownership](fluent2-info-label.md); Observer page: **추가 정보**.

2026-11-13 [SpinButton](fluent2-spinbutton.md): nullable numeric binding, bounded draft commits, shared text editing, native Spinner/Value/RangeValue and owned held-repeat scheduling. Observer → **숫자 입력** exposes the generated factory and unit parser. Catalog58 factories,34/55 official names connected,21 missing; full conformance remains open.


2026-11-13 [Numeric pointer focus](fluent2-spinbutton.md): provisional pointer focus clearing no longer commits a same-editor caret click. Actual destination commands still see the committed value; aborted dispatch restores owned focus. Regression evidence: artifacts/spinbutton-pointer-focus-2026-11-13/verification.json.


2026-11-13 [List prototype](fluent2-list.md): persistent arbitrary rows, nullable/single/multiple selection, row actions and composite child navigation in Observer → **목록**. Native Grid/GridItem exposes loaded dimensions, logical coordinates, spans, cells and containing-grid relationships with atomic publication and change notifications. Lifecycle and external-client qualification remain open; no generated factory/count increment yet.


2026-11-13 [List alignment and selection paint](fluent2-list.md): natural content is vertically centered, composite sample children share a center, custom measurement reuse defaults false, and checkmarks paint after all selection writes. Row focus outlines also wait for all row focus requests and roving-entry repair. Evidence: artifacts/list-paint-2026-11-13/verification.json and artifacts/list-focus-2026-11-13/verification.json. Native grid evidence: artifacts/list-grid-2026-11-13/verification.json. Factory qualification remains open.


2026-11-13 [Combobox arrow](fluent2-combobox.md): shared editor pointer retention preserves expander clicks across held frames, including controlled Open bindings and RTL fields. Observer → 선택 목록 → 도시 검색. Evidence: artifacts/combobox-arrow-2026-11-13/verification.json.


Markdown implementation: [source/display contracts and API](markdown-widgets.md), Dux.MarkdownView / Dux.MarkdownEditor, Observer → Markdown. Only the active code block shows its fences; inactive code renders its body alone. Composition uses the caret line's font, and disposed widgets reject callback assignment. Pipe tables share column alignment, empty-cell source mapping, horizontal overflow and raw editing with the same buffer. Qualification remains in progress.

MarkdownView link navigation: cached inline/reference/autolink/table destinations, hand cursor, release activation with drag cancellation; default system web/mail handler, Navigate callback and absolute BaseUri for relative URLs. See markdown-widgets.md.
2026-11-13 List factory: Fluent.List exposes plain/single/multiple overloads and is consumed by Observer → 목록;59 factories,35/55 top-level names connected,20 missing. See fluent2-list.md for ownership, native qualification and remaining limits.
Markdown images: ResolveImage maps cached canonical URIs to immutable UiImageSource; InvalidateImages retries and wakes event-wait mode. Existing PNG/SVG decoders and texture leases are reused. Observer → Markdown → 이미지 예제. See markdown-widgets.md for loading policy and limits.

MarkdownView links support Tab/Shift+Tab, Enter, fragment focus outlines and document-viewport reveal. Windows exposes Hyperlink/Invoke, read-only destination Value, ScrollItem and one Invoked event per activation. Dragging keeps text selection. Focus reveal propagates through owned ScrollView ancestors without accessibility and preserves manual/sibling scrolling. Legacy window scrolling and physical screen-reader qualification remain separate; see markdown-widgets.md.

Generator/catalog changes also require Duxel.Generators.Tests, outside the widget contract runner. It retains an independent official-site fixture and records actual runtime/OS/architecture. Windows and self-contained Linux x64 managed executions pass117 checks with six exact generated sources; declaration-removal caching and Linux UI/ARM64 remain open. See widget-contract-validation.md for the portable target override and command.

Linux X11 now exposes an owned LinuxPlatformTextBackend: explicit-file OpenType shaping, layout/raster/metrics and the existing atlas factory feed the common Vulkan renderer. Bounded native font caches and final-release callbacks preserve resource lifetime. Text fixtures include Korean/Latin, kerning, combining marks and multi-frame GPU submission. Linux vector text and12-degree oblique now use HarfBuzz7+ outlines and locked LibTessDotNet1.1.15. Bounded immutable glyph meshes feed the existing projected renderer. Vector73, raster87 and platform31 pass;51,933 Cairo interior samples and six GPU reference pairs plus three2/3-flight pairs match. Full typography, app runner and Linux/ARM64/fixed-RC3 NativeAOT qualification remain open. See linux-platform.md and artifacts/linux-platform-2026-11-13/vector-verification.json.

Linux application entry point: Duxel.Linux.App/DuxelLinuxApp reuses DuxelAppSession.RunCore with an owned X11 platform and explicit installed fonts. The interactive samples/Duxel.Linux.Sample defaults to V-Sync off and event-driven idle. WaitEvents(0) now waits indefinitely and dispatches native events. Initial app17 and platform34 assertions pass; actual visible-window idle, worker invalidation, button/XIM ASCII input, two exit paths and unhooked rendering are verified. See linux-platform.md, the paired sample READMEs and artifacts/linux-app-2026-11-13/verification.json. Custom window integrations, full platform acceptance and original fixedRC3 NativeAOT gates remain open.

Linux text clipboard: LinuxX11Clipboard/IUiClipboard uses independent XCB connections, a sleeping owner thread and scoped synchronous readers. The runner owns a default service only when Clipboard/ClipboardFactory are absent; caller services remain caller-owned. UTF8/TEXT/Latin1 negotiation, TARGETS/TIMESTAMP/MULTIPLE and bounded INCR are covered by35 independent-Xlib/lifetime checks; the updated app21 checks include real Ctrl+C/V with Korean/emoji and injected-service lifetime. All automated tests use private selections, preserving the user's CLIPBOARD/PRIMARY. Opening the sample does not acquire/read the clipboard. App-exit persistence, clipboard images, COMPOUND_TEXT, Wayland and complete display-hang recovery remain open. Updated sample/build commands and native dependency libxcb.so.1 are in linux-platform.md and the paired sample README; evidence is artifacts/linux-clipboard-2026-11-13/verification.json. This is managed .NET11/x64 functionality, not the fixedRC3 NativeAOT/device gate.

Linux bitmap/SVG images: the runner now defaults ImageDecoder to LinuxUiImageDecoder.Shared and preserves an explicit decoder. Installed GdkPixbuf/GLib/GObject decode PNG/JPEG/GIF/BMP/ICO to independent straight RGBA8, honoring row stride and first-frame/ICO-directory-zero semantics. PNG-compressed ICO uses explicit embedded-PNG dispatch; DIB ICO remains native. Malformed input fails. SVG retains the existing SvgStore/SvgImageDecoder/UiSvg path with the managed SVG backend and Linux HarfBuzz text assets. Image63/app25 checks cover independent pixels, actual X11-window PNG/SVG images/widgets, native rotation without XML/raster repetition, idle and prior clipboard contracts. Existing Windows SVG112 checks also pass on .NET11. Sample → right-side PNG/SVG/rotation; exact build/run and remaining animation/EXIF/color/accessibility/ARM64/fixedRC3 limits are in linux-platform.md and paired sample READMEs. Authority: artifacts/linux-images-2026-11-13/verification.json.

Linux native accessibility: DuxelLinuxApp enables AT-SPI by default using a private GIO connection/context/thread. Accessible/Application/Component/Action/Cache/Value expose the shared tree, supported actions, native keyboard focus and external cache-change events. Missing session/accessibility buses fail; explicit disable uses Run(options, session, new LinuxAtSpiOptions { Enabled = false }). Low-level LinuxX11PlatformOptions.Accessibility remains opt-in. UiAccessibilityTree.CaptureSnapshot/Published provide revision-consistent snapshots and changed-only notifications. Button(id, label, size) keeps a changing label's identity stable; legacy single-line inputs retain accessibility focus. Actual app34, independent native client17 on each runner path, failed-start8/zero retained threads or per-attempt descriptors, Windows control78 and shared-XCB clipboard35 pass. Current sample/build/run: samples/Duxel.Linux.Sample/README.md. Evidence: artifacts/linux-accessibility-2026-11-14/verification.json; details in linux-platform.md. Text/EditableText, Selection, table/grid, full relationships/localization, physical screen readers, DPI, ARM64/hardware and fixedRC3 NativeAOT remain open.
Linux numeric accessibility: range-bearing controls expose native AT-SPI Value (bounds/current/increment/text) and committed change events. RequestRangeValue preserves existing widget rounding; invalid/nonfinite, readonly and disabled writes fail without mutation. The lower sample integer slider adjusts by1. Actual app37/native27 per runner plus5 numeric-policy checks qualify this subset; negative replies use independent gdbus because the installed libatspi setter aborts on errors. No rendering hooks or internal-public product API were added. Current authority: artifacts/linux-accessibility-2026-11-14/value-verification.json. Text/EditableText, Selection, grids and full platform/accessibility qualification remain open; see linux-platform.md.
Progress range publication: generated and generic UiProgress widgets expose readonly logical ranges, omit numeric values in indeterminate mode and retain identity across mode changes. Visual interpolation does not publish intermediate accessibility values. Explicit immediate overloads accept an id; old id-free overloads keep paint-only behavior. Parent child keys remain authoritative, and no accessibility tree means no semantic ID resolution. Linux AT-SPI refreshes cached interfaces on mode changes. Control95 and actual app35/native32 per runner qualify this subset; full physical UIA/screen-reader conformance is separate. Sample → lower progress and Progress mode button. Current evidence: artifacts/linux-accessibility-2026-11-14/progress-verification.json; APIs in fluent2-progress.md.
Linux child Selection: the native seventh interface supports selected references/counts, single replacement/multiple addition, original-child versus selected-subset indices, SelectAll/ClearSelection, required/disabled policy and cached state/event updates. The common gate protects the final required option while allowing one of several required choices to be removed. Actual ComboBox popup ListBox children own Selection; outer combo indices remain structural. Bulk operations preflight known refusals and commit live bindings sequentially; later refusal/hierarchy changes can retain earlier commits. Current app35/native48 per runner/selection-policy7/numeric-policy5/control99/clipboard35 evidence: artifacts/linux-accessibility-2026-11-14/selection-verification.json. Sample is640x520 with lower single/multiple city lists. Full selection/widget/virtualization/physical conformance and Text/EditableText/Table/Grid remain open; use linux-platform.md and fluent2-list.md. No root-goal completion counts are promoted by this subset.
Shared text state: UiAccessibilityNode.TextInfo and edit-result AccessibilityText carry immutable committed text/caret/selection. UiAccessibilityTextContent maps Unicode scalars to UTF16 without split surrogates; unchanged frames reuse state and caret-only changes reuse content/maps. GetAccessibilityTextInfo(id) reads only the current render frame/scope. RequestTextSelection uses the exact published content/runtime ID, permits readonly selection, preserves focus/text and rejects stale, disabled or invalid grapheme requests. Source and auxiliary binding reconciliation stay coherent; there is no separate editor/global text cache. The sample bottom line displays scalar/caret/selection state. Control146 and actual Linux app37/native48 per runner regressions qualify the foundation; native Text/EditableText boundary/attribute/geometry/range-edit integration is still OPEN. Current authority artifacts/linux-accessibility-2026-11-14/text-model-verification.json; paired accessibility-text guides explain APIs and limits.
Native Text subset: nodes with published TextInfo now export the eighth AT-SPI interface. Scalar counts/caret/ranges/code points and one directional contiguous selection use existing immutable content and guarded render-thread requests. Native caret/selection events and exact scalar text-change payloads reach an independent libatspi client without cache clearing. Actual Backspace/undo removes/restores one emoji scalar. Current managed .NET11 app37/native66 per runner, selection7/numeric5/control146 and clipboard35 qualify this subset; full boundary/attribute/geometry/EditableText/styled/Markdown/protected/physical conformance remains open. Current authority artifacts/linux-accessibility-2026-11-14/native-text-verification.json; full contracts accessibility-text.md. No original completion counts are promoted.
Published text geometry: UiAccessibilityNode.TextLayout carries immutable UiAccessibilityTextLayout data tied to the current Content. Character/range extents, clipped point lookup and actual source-line ranges use the editor's content origin, line height, scroll, grapheme boxes and plain wrapped map. Combining scalars share a logical box; empty ranges remain zero-width. Unchanged layouts are reused, prior snapshots survive reflow/disposal, and reconciled auxiliary edits replace their geometry. Late numeric focus commits invalidate old draft geometry. Single/multiline pointer input now establishes accessible keyboard focus. Native Text adds character/actual-line GetStringAtOffset and character/range/point geometry in screen/window/parent coordinates. Current app37/native75 per runner/control163 and clipboard35 evidence: artifacts/linux-accessibility-2026-11-14/text-geometry-verification.json. Active preedit/styled/Markdown/unwrapped-legacy multiline geometry, other granularities/attributes, scroll and EditableText remain open; see accessibility-text.md. No original completion count is promoted.
Guarded text edits: CanEditText, UiAccessibilityTextEdit and RequestTextEdit preserve the existing selection constructor and use source/runtime guards plus the real buffer/history. Replace/Copy/Cut/Paste cover native Insert/Delete/SetContents; exact limits, grapheme/UTF16 checks, readonly copy, final binding publication and typed clipboard IO/timeout results are explicit. Native ninth EditableText exports all six content methods with scalar offsets and UTF8-byte InsertText lengths; split scalars/negative lengths fail. Tests use a private selection and actual Ctrl+Z/Y. Current managed app37/native92 per runner/control189 and clipboard35 evidence: artifacts/linux-accessibility-2026-11-14/text-edit-verification.json. Clipboard/editor/external bindings are not a single transaction; full styled/preedit/physical and remaining Text conformance stays open. See paired accessibility-text guides.
Native Unicode boundaries: GetStringAtOffset supports all five granularities. Pango 1.0 ABI libpango-1.0.so.0 is probed before AT-SPI bus creation; qualified 1.52.1, supported little-endian ABI. Per-paragraph native Unicode/language processing supplies word/sentence starts and CRLF/Unicode paragraph intervals in scalar coordinates. Current UI culture or explicit undetermined und tag chooses language. One immutable table per live Content/language survives caret-only refresh, invalidates on replacement/locale change and is removed with its node; no global text cache or measured speedup claim. Driver 59 includes38 app/session+21 boundary contracts, native 101 per runner, unchanged Core/control189, clipboard35: artifacts/linux-accessibility-2026-11-14/text-boundaries-verification.json. Actual multiple-paragraph segmentation is module-level; live RPC uses single-line input. Deprecated boundaries/attributes/full styled/preedit/scroll/physical qualification remain open. See accessibility-text.md and paired Linux/sample guides.

Legacy Text update: Before/At/AfterOffset supports all7 scalar/start/end boundary types using retained Pango ends and real painted lines. UiAccessibilityTextLayout.LineCount/GetLine exposes immutable UiAccessibilityTextLine(Start, End, NextStart); End excludes separators, soft-wrap End == NextStart. Driver93=38 app/session+55 boundary/layout, native127 per runner, fresh common controls193, clipboard35. Multiline/soft-wrap checks are actual Linux layout module tests, not native multiline RPC. See artifacts/linux-accessibility-2026-11-14/legacy-text-boundaries-verification.json and paired accessibility-text guides for exact limits.

Uniform source paint: UiAccessibilityNode.TextAppearance exposes UiAccessibilityTextAppearance(Content, Foreground) captured by the actual plain editor painter. Same content/color reuses it; missing painting, different source, styled text or composition cannot reuse stale metadata. Native default/run/value attribute queries expose fg-color only, separating defaults from empty explicit overrides. Driver93/native134 per runner/fresh Windows controls201/clipboard35. Font metadata, styled runs and attribute-change notifications remain incomplete. See artifacts/linux-accessibility-2026-11-14/text-attributes-verification.json and paired accessibility-text guides.

Native source-color notifications (2026-11-14): retained text nodes now emit object:text-attributes-changed when exported foreground RGB or uniform-paint availability changes. Content/caret identity alone does not trigger it; unexposed alpha changes are excluded. An owned test wrapper changes actual editor paint to RGB17,83,201 and restores it through ordinary rendering. Independent libatspi observes one event per change with unchanged text/caret, then no duplicate during200ms after an acknowledged identical-color repaint. Product sample layout/behavior is unchanged. Driver93/native137 per runner, unchanged common Core/control201 reused after32 source hashes and Core DLL verification, clipboard35 rerun. Evidence: artifacts/linux-accessibility-2026-11-14/paint-events-verification.json. This qualifies foreground notifications only; actual selected-font metadata, styled runs and full physical-screen-reader conformance remain open.
## Selected source-font metadata (2026-11-14)

The optional IPlatformTextFontRunBackend.GetFontRuns(request, nameLanguage) capability is implemented by LinuxPlatformTextBackend. It returns a read-only list of PlatformTextFontRun(StartUtf16, EndUtf16, Font) over the original source. The same SelectFont rule is used by actual layout, raster and vector rendering. Supplementary characters retain UTF16 lengths, CR/LF separators have no painted runs, and a source tab selects the face used for its four expanded spaces. This reports pre-shaping font selection; layout/shaping validation remains separate and no point-size conversion is inferred.

PlatformFontMetadata contains OpenType family/subfamily names (name IDs1/2), OS/2 weight and native italic flag, the actual synthetic-oblique flag, and normalized requested name language. HarfBuzz reads the exact shaping face's tables through its public opaque ABI. Missing names in that language remain null; no English or filename substitute is used. An absent OS/2 table leaves its fields null, while malformed/truncated metadata fails explicitly. Metadata is retained for the most recent requested language on each existing bounded font resource; the returned immutable values survive later language changes and native disposal. Each query creates its own source-run list. No speedup is claimed.

The sample's existing two-font command now prints the selected source faces for `정A` once at startup, before showing the same UI. The owned smoke prints Malgun Gothic and DejaVu Sans, weight400. The .NET11/Linux x64 driver111 comprises38 app/session,55 boundary/layout and18 font-selection/metadata checks. Independent fontTools4.66.1 reads the exact input files and matches names/OS2 values; its SHA256/name records are in font-runs-independent-oracle.json. HarfBuzz8.3.0, native137 per runner, fresh common controls201 and clipboard35 pass. Evidence: `artifacts/linux-accessibility-2026-11-14/font-runs-verification.json`. This foundation is not yet published as editor/AT-SPI font runs; selected-font UI attributes, point-size units, styled documents and full physical qualification remain open. References: [HarfBuzz names](https://harfbuzz.github.io/harfbuzz-hb-ot-name.html), [OpenType OS/2](https://learn.microsoft.com/en-us/typography/opentype/spec/os2).

Editor font attributes (2026-11-14): UiAccessibilityTextAppearance now owns validated immutable scalar UiAccessibilityTextFontRun records, FontRunCount/GetFontRun/GetFontRangeAt, and a constructor accepting original PlatformTextFontRun intervals. It preserves separator gaps and rejects split scalars, overlaps, invalid weights/names. Shared editor paint queries an optional direct-font backend only when content/measurement configuration/name language changes; unchanged frames reuse the snapshot and color-only changes share its font map. A shared font-size resolver keeps actual direct measurement/rendering and metadata requests aligned, without exporting an inferred point size. UiAccessibilityTree.FontNameLanguage starts at und, validates changes and requests a frame; UiImmediateContext.SetAccessibilityFontNameLanguage configures it. The sample explicitly selects en (LinuxDemoScreen.FontNameLanguage) and the owned fixture verifies ko changes without English substitution. Native GetAttributes/GetAttributeRun now expose actual family-name/weight/style overrides with optional fg-color defaults, scalar range boundaries, unpainted gaps and terminal-offset behavior. Adjacent ranges with equal exported attributes merge. Native and simulated oblique remain distinct in metadata; OS/2 bit9 applies only from version4. Notifications compare exported attributes on unchanged source, skip unexposed differences and avoid duplicating text-change events. Actual name-language changes/restoration and unchanged repaint are observed independently. Current .NET11/Linux driver114=38 app/session+58 boundary/layout+18 font-selection; native142 each, fresh common controls209, clipboard35. Evidence: artifacts/linux-accessibility-2026-11-14/font-attributes-verification.json. Plain editor font-run publication is qualified; point size, styled/Markdown/preedit text, atlas-only/mixed fallback font metadata and physical screen readers remain open. See the OpenType OS/2 reference for declared style flags.

Nominal point size (2026-11-14): UiAccessibilityTextSize(PhysicalPixels, ContentScale) explicitly models the direct-renderer em request. LogicalPixels = PhysicalPixels / ContentScale; Points = LogicalPixels *72/96, using the engine logical96-DIP reference and renderer float precision. This is nominal formatting size, not glyph ink height, line spacing or measured monitor inches. Validate rejects zero/nonfinite/unrepresentable conversions before metadata queries/publication. UiAccessibilityTextAppearance.Size is nullable; old2/3arg constructors remain and unknown size stays absent. Its new4arg constructor accepts the size; cache keys include it and color-only snapshots preserve it. Native size is an invariant-culture point string, available through named queries and known default attributes (fg-color+size), merged into GetAttributeRun(true); explicit face overrides remain separate. Actual size changes/restoration notify once, identical-size repaint stays quiet200ms. Configured-content-scale module tests1/1.25/1.5/2/2.5 qualify compiled12-logical-pixel typography as9pt and legacy18 as13.5pt; zoom1.25 yields16.875pt, independent of doubled line spacing. Actual native24-logical-pixel input returns18pt and restores13.5pt without source/caret changes. .NET11/Linux driver127=38 app/session+71 boundary/layout+18 font selection, native146 each, fresh controls214 and clipboard35. Evidence: artifacts/linux-accessibility-2026-11-14/text-size-verification.json. Physical DPI hotplug/hardware and styled/Markdown/preedit font sizes remain unqualified. References: https://www.w3.org/TR/css-values-4/#absolute-lengths and https://gnome.pages.gitlab.gnome.org/gtk/atk/enum.TextAttribute.html.

Sample visibility: GetAccessibilityTextAppearance(id) reads the immutable appearance already staged in the current render frame and current ID scope, without requesting a frame. The Linux sample footer now shows the actual input point size (initially 글자 : 13.5 pt), so the new value is visible using the existing two-font launch command; unavailable metadata is omitted.

### Styled Markdown source geometry and editing

MarkdownView and MarkdownEditor now publish committed source text, caret/selection and immutable geometry from their actual styled projection. Native offsets refer to the unchanged Markdown source. Hidden delimiters have zero-width boxes; visible headings and body text use their actual typography height, and table text uses cell alignment and padding. Revealing the focused block replaces the layout snapshot without mutating retained snapshots. Guarded range replacement edits source, reparses the projection and reconciles application-normalized bindings before publishing the final text and layout. Read-only views reject mutations. Styled font/foreground attributes, composition geometry and full document semantics remain unqualified; source coordinates are not a rendered-text-only accessibility model.

Qualification: managed .NET11/Linux x64 driver148 (38 app/session +92 boundary/layout including21 styled cases +18 font selection), independent native146 per existing sample runner, Windows Control214, clipboard35 and owned sample auto-exit0. The21 styled cases execute real Linux CPU layout; they are not a live AT-SPI Markdown reader or hardware GPU qualification. Evidence: `artifacts/linux-accessibility-2026-11-14/styled-text-verification.json`. Test-only paint requests/acknowledgements now use immutable sequence marker files; no overwrite/unlink race or content-read polling is required, and original time limits remain.

### Independent native Markdown verification

An owned editor/viewer surface now runs through the unhooked product renderer and an independent libatspi client. Both explicit and registered Linux runners pass22 native Markdown checks each: original source/scalar ranges, hidden syntax and heading/table boxes, hit testing, source selection and its event, block reveal and real X11 focus, source insertion/deletion and change events, fenced code replacement, read-only refusal, and real X11 Ctrl+Z/Ctrl+Y through shared source history. Native text remains the unchanged Markdown source, including syntax. This verifies native protocol use; physical screen-reader behavior, styled attributes and composition geometry remain open. The normal sample and Observer layout are unchanged.

Latest driver152 =42 app/session/startup +92 boundary/layout +18 font-selection. The existing native sample146 checks per runner still pass separately. Common Control214/clipboard35/sample-smoke evidence is reused only with exact unchanged product source and matching binaries; these were not rerun for a test-only change. Source manifest and results: `artifacts/linux-accessibility-2026-11-14/markdown-native-verification.json`. Native child12-second and outer45-second limits are unchanged.

### Per-source styled paint attributes

`UiAccessibilityTextAppearance` now supports sparse styled paint as well as its existing uniform constructors. `HasUniformPaint` distinguishes them. For styled text use `PaintRunCount`, `GetPaintRun` and `GetPaintRangeAt`; `Foreground` rejects a uniform lookup on a styled snapshot. `UiAccessibilityTextSourcePaintRun` accepts original UTF-16 intervals and the snapshot validates/copies them into scalar ranges. Hidden delimiters, line separators and nontext replacement objects retain unknown paint/font gaps. Known local sizes retain the nominal pixel/content-scale/point contract.

The shared styled painter and metadata use the same typography scopes and color resolver. Exact projection/resource/scale/language/palette keys reuse the immutable map; unchanged frames do not query font runs again. This is verified reuse, not a measured performance improvement. Native defaults remain unchanged for uniform inputs. Styled native defaults are empty; explicit source runs contain actual family/weight/style/foreground/point size, merged only when exported fields match. Revealing syntax emits one attribute event without changing source; an unchanged focus repaint stays quiet for200ms. Configuring a bold role with a regular font reports its real400 weight; emphasis reports actual simulated oblique, and link paint retains its real color.

Final .NET11/Linux driver157 =42 app/session +97 boundary/layout (26 styled cases) +18 font selection. Independent Markdown34 EACH explicit/registered runner; existing sample146 EACH; fresh Windows Control220, clipboard35 and owned sample exit0. Evidence: `artifacts/linux-accessibility-2026-11-14/styled-paint-verification.json`. Composition geometry/attributes, physical screen readers, hardware/ARM64 and fixedRC3 NativeAOT remain unqualified. Current native text still contains Markdown source syntax.

### Committed source geometry during IME preedit

`UiAccessibilityTextLayout.HasComposition` identifies geometry mapped through an active preedit display. Content, scalar counts and source caret remain committed text. Existing source characters use their actual displayed positions after the inserted preedit; an empty range at the insertion follows the displayed preedit caret. A preedit-only hit returns no committed offset. Display rows map back to committed source bounds, including empty preedit-only rows; no composition text is returned as source line content. Grapheme alignment follows the real composed line map. Single-line suffix geometry shares the painter's rounded suffix origin.

Composition text/insertion/width join the layout cache identity, so changed preedit cannot reuse an old box map. Unchanged preedit reuses the immutable snapshot; cancellation restores normal geometry and paint, and commit changes source once. Source paint attributes remain absent during composition rather than retaining a stale uniform/styled map. Physical IME integration, native composition events and composition paint attributes remain unqualified.

Controlled IME47 cases run against actual Linux text layout for single-line, multiline and Markdown: original source identity/scalars, prefix/suffix/caret positions, preedit-only hits, cache invalidation/reuse, newline and soft-wrap rows, combining marks, start/end insertion, empty source, cancellation and commit. Latest .NET11/Linux driver204 =42 app/session +144 boundary/layout (71 prior plain +26 styled +47 composition) +18 font selection. Existing native Markdown34/sample146 per runner, fresh Windows Control220/clipboard35/owned sample exit0 also pass; these native checks are not new live IME/preedit proof. Evidence: `artifacts/linux-accessibility-2026-11-14/composition-layout-verification.json`.

### Standard unwrapped geometry and multiline scroll publication

The standard editable multiline path with Wrap=false now publishes committed hard-line geometry through one retained map in its existing accessibility entry. It uses the same text measuring engine as the legacy painter, preserves blank/trailing lines and scalar source positions, and clips hits to that painter's field. This adds geometry without changing the legacy editing/painter route or introducing a global text cache. Legacy preedit on this no-visual-map route still omits geometry until its caret/underline integration is qualified; the earlier47 controlled composition cases cover other routes.

Compiled multiline inputs now apply guarded accessible two-axis scroll requests before painting, publish viewport/content/offset and register their viewport for reveal. Markdown reuses the same helper. Layout cache identity includes effective width, wrap mode and styled projection alongside source/resources/version/clip/composition. Local version numbers from different line maps cannot certify equal geometry. A corrected standard-path comparison rejects the old version-only reuse; the final structural key also retains geometry across fresh ownerless maps when layout is equal. Earlier comparison setup accidentally used the navigable Fluent route/default padding and is explicitly excluded as bug proof. No measured speedup is claimed.

Final .NET11/Linux driver219 =42 app/session +159 boundary/layout (71 plain +26 styled +47 controlled composition +15 unwrapped/scroll/cache) +18 font selection. Independent document/input42 EACH runner consists of34 Markdown +8 true standard unwrapped native cases; ordinary sample146 EACH still passes. Fresh Windows Control220, clipboard35 and owned sample exit0 also pass. Evidence: `artifacts/linux-accessibility-2026-11-14/unwrapped-scroll-verification.json`. Native text substring scrolling/bounded ranges, legacy preedit, physical IME/readers, hardware/ARM64 and fixedRC3 NativeAOT remain open.

### Guarded native text-range scrolling

`UiAccessibilityTree.RequestTextScroll` accepts `UiAccessibilityTextScroll` with the exact published Content, an ordered UTF-16 source range and either a placement or a finite client point. `UiAccessibilityTextScrollPlacement` provides top-left, bottom-right, top/bottom/left/right edge and nearest alignment. The render thread resolves current source geometry and clamps offsets to the local viewport's scroll limits before painting. Source text, caret, selection and focus remain unchanged. Read-only multiline text can scroll; disabled/stale/unavailable requests are refused and active preedit returns Busy. Points outside the visible local viewport are rejected. A short already-visible multiline document succeeds without movement or scrollbars. When wait=false, Success means queued, not completed publication.

AT-SPI Text now exports ScrollSubstringTo and ScrollSubstringToPoint with scalar-to-UTF16 conversion and shared screen/window/parent-to-client conversion. Native methods return completion after the published frame. Support is local multiline text with geometry; Local and ancestor range scrolling and bounded queries are described below. Oversized ranges/edge alignments use bounded offsets rather than promising impossible exact placement at content edges. Cancellation is checked before committing offsets; a forced late-cancel race was not independently qualified.

Final .NET11/Linux driver244 =42 app/session +184 boundary/layout (71 plain +26 styled +49 controlled composition +15 unwrapped +23 source-scroll) +18 font selection. Independent document/input59 EACH =34 Markdown +8 unwrapped +17 range-scroll; existing sample146 EACH. Fresh Windows Control226, clipboard35 and owned sample exit0 also pass. The native cases verify actual glyph positions, all three coordinate systems, nearest/idempotent/invalid/no-movement requests and unchanged source selection/focus; CPU cases exercise all7 placements. Evidence: `artifacts/linux-accessibility-2026-11-14/text-range-scroll-verification.json`. Protocol: https://raw.githubusercontent.com/GNOME/at-spi2-core/main/xml/Text.xml and https://gnome.pages.gitlab.gnome.org/at-spi2-core/libatspi/enum.ScrollType.html.


### Bounded visible source ranges

`UiAccessibilityTextLayout.GetBoundedRanges(bounds, xClip, yClip)` returns nonempty, end-exclusive Unicode scalar ranges in the original Content. Bounds use client coordinates; each axis accepts None, Minimum, Maximum or Both. Cells must intersect both the query and current painting clip. Minimum/Maximum omit cells crossing that query edge; Both requires full containment. Merely touching an edge does not intersect. Runs split at visual lines and unpainted source gaps: hidden Markdown syntax, zero-width separators and offscreen source cells are omitted. Combining graphemes retain all committed scalars sharing their painted cell. This is a source geometry query, not a rendered-only document model. It preserves text, caret, selection and layout reuse; no rendering is requested by the query.

AT-SPI Text.GetBoundedRanges exports `a(iisv)` with exact source text and scalar offsets, using the shared screen/window/parent conversion. Invalid dimensions, clipping policies or coordinates return explicit protocol errors. Empty/disjoint bounds return an empty array. Native GArray ownership is verified by the independent libatspi client. GNOME contract: https://raw.githubusercontent.com/GNOME/at-spi2-core/main/xml/Text.xml.

Current .NET11/Linux driver271 = app/session42 + boundary/layout211 + font-selection18. The new bounded-range module contributes27 CPU cases and23 independent native cases per runner; document/input82 EACH and ordinary sample146 EACH pass. Fresh Windows controls226, private clipboard35 and owned product sample autoexit0 pass. Evidence: `artifacts/linux-accessibility-2026-11-14/bounded-ranges-verification.json`. An early fixture assumed a naturally clipped cell; the final fixture deliberately clips a real cell. Its pre-fix query exposed an invisible cell portion outside the viewport; after the intersection correction the same regression passes. Physical readers/IME, object/image semantics, virtualized documents, ancestor source-range scrolling, hardware/ARM64 and fixedRC3 NativeAOT remain open.


### Single-line source-range scrolling

The same `UiAccessibilityTree.RequestTextScroll` and AT-SPI ScrollSubstringTo/ScrollSubstringToPoint now support built-in single-line inputs through their shared editing kernel. They align the original source range horizontally before painting, clamp to actual content width, and preserve text, caret, selection and focus. The seven placement values reuse the common contract; a single-line input has no vertical scrolling, so vertical/point-Y requests clamp to that capability. Point targets must be inside the visible local text viewport. Ancestor scrolling is integrated as described below.

A successful request retains its viewport while source, caret, anchor, width, font measurement, active state and committed offset stay unchanged. Keyboard/pointer activity, changed source/layout/focus or explicit accessibility focus/selection requests resume ordinary caret visibility, including an explicit request for the same caret. Unchanged focused caret-blink frames no longer chase the old caret and undo the requested viewport. Composition refuses range movement with Busy. Inactive inputs also clamp stale nonzero offsets when externally replaced with shorter text; no overflow is invented for a short source. Retention belongs to the existing accessibility entry, not a global input cache.

Current .NET11/Linux driver310 =42 app/session +250 boundary/layout +18 font-selection. New single-line CPU38 cases and one additional controlled-preedit refusal pass; independent native21 cases per runner bring document/input to103 EACH (previous82+21); ordinary146 EACH pass. Native coverage includes all placements, three coordinate systems, no focus theft, actual X11 focus/Home navigation,650ms of focused observation and a guaranteed frame publication triggered by another input, explicit same-caret release, end insertion and shortening. Fresh Windows common controls227, private clipboard35 and owned product sample autoexit0 pass. The old common geometry fixture manually shifted a short non-scrollable input; it now exercises legal overflow and additionally verifies shortening clamps the offset. Evidence: `artifacts/linux-accessibility-2026-11-14/single-line-scroll-verification.json`. Physical readers/IME, legacy preedit geometry, full document semantics, hardware/ARM64 and fixedRC3 NativeAOT remain open.


### Source-range reveal through enclosing scroll views

Source scrolling now reuses the existing inner-to-outer ScrollItem planner after the input's local movement. Offscreen text with a published scrollable ancestor can accept the guarded request. The current source geometry and full local text viewport determine the visible slice; subsequent ancestor offsets are applied on the next render, and synchronous completion waits for the published geometry. Seven placements, client points and empty EOF insertion ranges are supported for the qualified nested single-line, multiline and Markdown paths. Point alignment starts from the local leading edge and must target the outermost scrollable viewport's visible area; nonscrolling hosts can restrict this area but cannot enlarge it.

Every planning pass checks target runtime/current-frame presence, enabled state, exact Content identity, composition and current point bounds. Text, caret, anchor and focus are preserved. Source replacement during the staged move fails; target removal returns Unavailable. The existing two-second request timeout remains unchanged, and canceled plans do not apply delayed ancestor offsets. Earlier local movement or already-published offsets are not rolled back on later failure: this is a staged reveal, not an atomic transaction. wait=false still reports queue admission only. Popup ownership boundaries and the existing bounded planning-attempt limit are retained; RTL and fixed-extent virtualized paths are qualified below; physical reader qualification remains open.

Current .NET11/Linux driver401 =46 app/session +337 boundary/layout +18 font-selection. New ancestor CPU87 checks cover all three input producers and seven placements, pending-until-paint, scalar hits, source/focus preservation, all local point cases, invalid points inside the window but outside the owning viewport, mid-request source changes, real timeout/cancellation, target retirement and EOF insertion geometry. Separate owned product windows pass61 independent native checks EACH explicit/registered runner; the previous document/input103 EACH and ordinary146 EACH remain green. Fresh Windows common227, ScrollView/motion96 and overflow26 CPU checks, private clipboard35 and product sample autoexit0 pass. Three lifetime test probes now observe the protected OnDispose lifecycle hook rather than overriding private-protected resource cleanup. The native Windows lifetime hosts were compiled, not executed. Evidence: `artifacts/linux-accessibility-2026-11-14/ancestor-text-scroll-verification.json`.


### RTL and virtualized source-range reveal

The ancestor contract is now checked for all four outer/inner ScrollView.RightToLeft combinations across single-line, multiline and Markdown inputs (348 CPU checks), including the left-side scrollbar gutter and logical/physical horizontal offsets. Separate product windows qualify the both-RTL case through61 independent native checks per runner.

Fixed-extent virtual lists also qualify realized/overscan source rows in a billion-item model near index123456789, with fractional double offsets above30 billion pixels. The66 CPU checks verify exact row allocation, every placement, point alignment, stable realized identity, scalar hits, unchanged text/caret/focus, exact nearest-offset reuse, EOF and owner disposal while keeping realization bounded. Independent native virtual fixtures contribute61 checks per runner. Unrealized text needs the existing model lookup/realization API first; variable extents and popup composition remain additional integration gates.

A reproduced defect put a requested x=60 source cell at x=19 when its enclosing virtual list could scroll only vertically. Local/ancestor point handling now inspects horizontal and vertical capabilities separately: the input handles an axis its ancestors cannot scroll, while the ancestor planner handles the remaining axis. The same regression now reaches(60,60) with double precision retained for the vertical offset. Immediate controls in fixed rows explicitly use constrainLayout=true; otherwise an explicit width can exceed the scrollbar-reduced slot. Tests assert this allocation before requesting movement.

Current .NET11/Linux driver736 =54 app/session +664 boundary/layout +18 font-selection. Native ordinary146, document/input103, normal ancestor61, RTL ancestor61 and virtual source61 EACH explicit/registered runner pass. Fresh Windows common227, ScrollView/motion96, virtual list75 and overflow26 CPU checks, private clipboard35 and product sample autoexit0 pass. Evidence: `artifacts/linux-accessibility-2026-11-14/rtl-virtual-text-scroll-verification.json`. The reusable `text-scroll-virtual` executable profile runs only the66 CPU checks with real Linux fonts, avoiding unrelated native/window work during focused diagnosis. Physical reader/IME, legacy preedit, variable extents, full document semantics, hardware/ARM64 and fixedRC3 NativeAOT remain open.

### Standard multiline IME composition

Standard editable multiline inputs with Wrap=false now retain pointer focus after release and idle frames, using the same focus observation as single-line inputs. During nonempty IME preedit they use the existing shared visual composition map for painting, underline, caret, input-method rectangle, scrolling and accessible source geometry. Cancellation or commit returns to the normal hard-line path; committed source and retained immutable snapshots stay intact. Both owned and ownerless inputs are covered. Custom composing snapshots still need their matching display map to publish geometry.

The controlled composition suite now passes104 checks across single-line, wrapped multiline, Markdown and both standard unwrapped modes. It includes real pointer focus, Korean/emoji/combining preedit, hard newlines, wrapped or horizontally scrolled long preedit, cancellation, one-time commit and outside-click release. This is injected IME coverage, not physical OS input-method validation. Current .NET11/Linux driver790 =54 app/session +718 boundary/layout +18 font-selection; independent native profiles remain ordinary146, document/input103, normal/RTL/virtual ancestor61 each per runner. Fresh Windows common227, clipboard35 and the owned product sample autoexit0 also pass. Evidence: `artifacts/linux-accessibility-2026-11-14/legacy-ime-layout-verification.json`.

### Variable-height virtual source ranges

The existing indexed-extent API is now checked with a billion rows repeating256.25/384.75/192.5px heights and fractional double offsets beyond32 billion pixels. The shared virtual-text fixture runs78 CPU checks across single-line, multiline and Markdown inputs: all placements, exact point hits, EOF, stable keys/source/selection/focus and bounded realization. Growing the preceding row by64.5px preserves the exact keyed viewport anchor; a further32.25px growth after request admission is resolved against the new boundaries before publication. The fixture uses actual indexed row allocations, not fixed-height substitutes. No engine API or rendering behavior changed in this qualification.

The focused native profile opens two owned product X11/Vulkan windows (explicit/registered runner), passes61 independent source-range checks each, and records4 driver/retirement checks. The preceding aggregate attempts FAILED: the existing ordinary native client exited134 after recording146 checks; a second aggregate attempt reproduced that exit. A narrowed ordinary-client run instead rejected GrabFocus. One GDB diagnostic reproduced that focus refusal and did not explain the earlier post-result exit. These outcomes are preserved separately and are not accepted as full-driver success. Prior full-driver790/Windows227/clipboard35/sample0 evidence remains at its previous snapshot; no new whole-goal completion is claimed. Current focused evidence: `artifacts/linux-accessibility-2026-11-14/variable-height-text-scroll-verification.json`. Physical readers/IME, popup integration, hardware/ARM64 and fixedRC3 NativeAOT remain open.

Subsequent validation: the ordinary native client now writes its result only after event scopes and atspi_exit have completed; recorded checks alone no longer precede native cleanup. Two narrowed runs finish with exit0. The final aggregate run also exits0: Linux872 =58 app/session +796 boundary/layout +18 font-selection; ordinary native146, document/input103 and normal/RTL/fixed-virtual/variable-virtual source61 each pass per explicit/registered runner. The older focus refusals and post-result exit134 remain preserved; these passing runs do not establish their original cause or prove a product focus fix. Temporary focus/cleanup tracing and the unsupported callback-destruction experiment were removed. Current authority: `artifacts/linux-accessibility-2026-11-14/native-cleanup-record-verification.json`. Product binaries are unchanged; Windows227/clipboard35/sample0 are inherited, not rerun here.

### Floating popup source-range boundaries

A real regression moved the logical owner from(180,180) to(196,279) while revealing text in a floating popover, then dismissed that popover and returned Unavailable. Popover surfaces now capture an independent, non-scrolling content boundary. Their own body viewport still scrolls; the shared reveal planner stops before the trigger's logical scroll ancestors. Logical accessibility parent relationships are retained and no public API was added.

The real-font CPU profile passes138 checks over single-line, multiline and unwrapped Markdown in ordinary and focus-trapping popovers. It verifies every placement, exact point alignment, scalar hits, stable popup/owner geometry, unchanged source/caret/selection/focus, rejection of points outside the popup, and closing an actually pending request without delayed owner movement. The retirement fixture resets the popup's own viewport before admission; it does not mistake an already completed local request for cancellation. Native fixtures reuse the same surface and pass21 independent checks for each of the three inputs in each explicit/registered runner (six product X11/Vulkan windows); the focused driver adds12 completion/retirement checks. Native viewport bounds are read from the real owning pane and all prior ancestor profiles retain their original fixed geometry oracles.

Final .NET11/Linux1022 =70 app/session +934 boundary/layout +18 font-selection. All18 independent native profiles pass, including the six popup profiles; ordinary146, document/input103 and normal/RTL/fixed/variable ancestor61 each remain green. Fresh Windows common227 and popup240 CPU checks, clipboard35 and the owned product sample2sec autoexit0 also pass. Evidence: `artifacts/linux-accessibility-2026-11-14/popup-text-scroll-verification.json`. The earlier intermittent ordinary focus/exit134 cause remains unconfirmed; physical readers/IME, hardware/ARM64, full document semantics and fixedRC3 NativeAOT remain open.

### Image semantics and keyboard selection

Rendered images now publish noninteractive Image children with plain alternative names and actual clipped Component bounds; offscreen images remain discoverable and can use the shared item-reveal planner. Available empty-alt images are decorative and omitted; an unavailable image uses the same visible unavailable text. Viewer image labels belong to their actual Link parent. Editing a block shows its exact source and retires that block's image nodes until the rendered projection returns. Metadata follows the same projected-image and geometry helpers as painting, including cell padding for a leading table image without outer pipes. Image/URI resolution and parsed Markdown remain cached.

A keyboard regression entered hidden image URI characters: one Shift+Right from source8 stopped at9 instead of the image end24. Horizontal movement and hit testing now respect the rendered image's complete source boundary and skip invisible syntax; raw editor blocks retain ordinary text movement. Selection is tinted above opaque images so Shift selection remains visible, and clearing selection restores the original picture. Image row lookup is independent of caret affinity at soft-wrap edges. CPU84 checks cover left/right/up/down with and without Shift, ordinary/linked and tall images in View/Editor. Actual X11 Shift+Right/Left and owned-window pixel captures verify whole-image selection, visible tint, clearing and unchanged source. This is keyboard text selection, not object handles or image editing.

Basic image semantics add31 CPU checks and independent native21 checks per explicit/registered runner (roles, names, link parents, bounds, hit testing, source-range reveal and real keyboard/pixel feedback). Final managed .NET11/Linux1141 =74 app/session +1049 boundary/layout +18 font selection; all20 independent native profiles pass. Fresh Windows common227 and composite/Markdown415 CPU checks, private clipboard35 and the owned product sample2sec autoexit0 pass. At that earlier keyboard checkpoint the native Image interface was still open; the current Image contract is documented below. Physical reader/IME, remote image policy, hardware/ARM64 and fixedRC3 NativeAOT remain open. Evidence: `artifacts/linux-accessibility-2026-11-14/markdown-image-interaction-verification.json`.

## Managed SVG and native image checkpoint — 2026-11-14

The SVG backend is now managed geometry/rasterization with Windows DirectWrite/WIC and Linux HarfBuzz/GdkPixbuf. Windows deploys no Skia/HarfBuzz/image-codec DLL. The Linux native HarfBuzz assets are target-specific. Existing resource/reload/LRU, zoom/rotation and font-snapshot contracts remain. See [SVG platform rendering](svg-platform-rendering.md) and `artifacts/svg-managed-2026-11-14/verification.json`; the earlier Skia checkpoints are historical.

SVG → 브라우저 비교 contains hash-pinned complex originals and Chromium references. Follow the [comparison/run contract](svg-browser-validation.md); full pixel equality is unfinished. Paint templates, stroke construction, coverage, sampling, compositing and filters have separate responsibilities. `UiTextureUpdate.IsColorImage` marks straight-alpha RGBA8 input; shared Core conversion and Vulkan premultiplied filtering prevent transparent-color fringes in PNG/SVG. Font coverage is not converted. Image mode uses0x40000000, coverage mode0x80000000, and descriptor indices use the lower30bits.

General SVG paths use the observed GPU browser's four samples,1/256-pixel grid and Wang uniform subdivision in `SvgCubicTessellation`. Inherited `visibility` and descendant overrides compile into retained geometry, text and image nodes; hidden text advances and hidden geometry's group bounds remain intact. Observer document features demonstrate a visible white diamond inside a hidden group. Exact small-example pixels do not qualify the unfinished complex originals.

`SvgSamplePixel` and `SvgRasterSurface.Samples` retain divergent sample colors through compositing, distinguishing geometric clips/group opacity from resolved image masks/spatial filters. Alpha and luminance masks are supported. Existing scene/raster caches remain in use with no new public API.

`SvgOrderedDither` uses8×8 Morton thresholds; `PaintServerOpacity` converts pattern/gradient opacity to a rounded8-bit value. Three pattern examples match full RGBA. Some gradient/solid one-byte differences and complete original-document equality remain unfinished.

`SvgStroke` retains the complete miter join interior and computes translated polygon winding in double precision. Eight hash-pinned native browser full-RGBA fixtures cover joins and large origins; curved stroke tessellation and whole-document equality remain separate unfinished checks.

`SvgStrokeCurve`, `SvgStrokePath` and `SvgStrokeTessellation` separate retained control points, dash-length measurement and combined parametric/angular stroke expansion. Twenty browser full-RGBA fixtures cover curves, dashes, cusps, joins, caps and transforms; the two isolated original Red strokes match exactly. Straight round-cap analytic coverage and whole-document equality remain unfinished.

`SvgViewport` clipping applies per draw/layer composite, with geometric intersection for analytic rectangles. Sixteen browser full-RGBA fixtures cover fractional clip boundaries. Both isolated original Red fill/stroke combinations now match; the obsolete final document clip pass is removed.

`UiAccessibilityNode.ImageInfo` optionally publishes immutable `UiAccessibilityImageInfo` with the complete rendered rectangle before clipping, description and optional locale. Ordinary `Node.Bounds` remains clipped. Projected resolved Markdown images provide it; decorative images remain omitted, unavailable captions do not advertise a native image. Linux exposes `org.a11y.atspi.Image` only when this metadata exists: description, locale, size, position and extents in screen/window/parent coordinates. An unspecified locale is an empty protocol string. Size encloses rendered logical geometry in native integer pixels and can differ from the source bitmap dimensions. Source-range reveal and keyboard selection remain unchanged; physical-reader qualification remains open; image Component scrolling is qualified above.

2026-11-14 SVG circles, ellipses, rounded rectangles and undashed round-cap lines now share SvgAnalyticRRect with interpolated AA coverage/paint coordinates and subpixel width scaling. Browser Canvas2D was identified as GraphiteDawnD3D11 at the pinned revision; independent19-input Linux comparison improves1/19→11/19 exact, with identical Windows outputs. Positive regressions11 do not waive remaining8 failures or whole originals0/2. See [browser validation](svg-browser-validation.md#shared-analytic-shape-coverage).


2026-11-14 [SVG widget layout](svg-widget-layout.md): zoom/rotation participate in measurement and row/column flow. Margin reserves four outer edges; Offset is a signed-margin compatibility shorthand. Browser comparison shares the upper controls and displays measured extents, with fixed independent references on the left.


The [common widget transform contract](widget-transform-contract.md) retains the expanded all-widget scope, relative origins, inverse input, accessibility and popup anchoring. Common `UiWidget.Margin` is implemented and inherited by SVG, buttons, checkboxes and sliders; use the Observer `위젯 배치` tab to exercise it. Common rotation remains in progress. Source-space rounded SVG geometry uses existing paired image records; no CPU corner raster or extra record stride is introduced.


Affine clip damage snapshots include both the shape and its following transform record. A matrix-only change repairs newly exposed and hidden pixels; unchanged acquired images return to zero draws. Native MSAA1/4 regressions cover rectangular, rounded and circular masks plus dynamic inverse parent/child rotations. Projected widget scenes still use conservative full-scene invalidation; this change does not claim partial redraw for arbitrary 3D scenes.

Transformed popups: on **위젯 배치**, adjust rotation, scale and margins, then use **변환 팝업**, **변환 메뉴** and **변환 도구 설명**. Triggers transform while popup content remains upright in display coordinates. A shared scope separates owner transforms, clipping, pointer, accessibility and IME coordinates and restores them on exit. Deferred tooltips retain screen anchors and caller-owned visibility even with the pointer outside the window. Qualification covers declarative Popover/menu/Tooltip and compatibility BeginPopup/BeginTooltip, modal, Combo and menu paths. Arbitrary 3D-camera combinations and screen-reader conformance remain unqualified.

On 위젯 배치 → 기존 팝업 API, apply shared rotation/scale and exercise open, execute and close. The three legacy Combo overloads share screen-anchor placement. Popup width is the maximum of the original content width and transformed trigger width; transformed owner clipping is replaced by display clipping. Screen popups do not inherit the owner widget's forced vector-text mode.

A screen popup separates text mode from camera layers already open at entry. Camera layers opened inside the popup remain active. Retained layers record their effective vector/screen text mode, so StaticCache and unchanged ContentVersion rebuild on a mode transition and reuse geometry afterward. MSAA1/4 comparisons cover translated/rotated parents, nested 2D layers, transformed widgets and exact mode-transition/restoration pixels. This does not complete the original fixed RC3 NativeAOT, general 3D input or screen-reader qualification.


### SVG clip storage inspection (2026-11-15)

SVG → 클립·경계 exposes nine independent Chromium/GPU pairs with zoom and rotation. CPU/GPU9/9 and existing opacity9/11 pass; whole-browser equality remains incomplete. Scenes are retained and clip folding preserves authored object bounds. See the [SVG GPU camera guide](svg-gpu-camera.md).


### Opacity layer raster origin (2026-11-15)

SVG → 레이어·테두리 adds five independent reference/GPU pairs. Opacity storage layers retain a dithering pixel origin independently of paint coordinates. Existing groups pass10/11, basic layer controls5/5, and additional stroke domains3/5. GPU zoom retains scene and bounds.


### Analytic rectangle stroke and visible reference parity (2026-11-15)

Clip and stroke share rectangle recognition. Axis-aligned miter strokes use path flag64 and one inner-bound record; invalid bounds/count/flag combinations reject before GPU submission. Zero-width strokes match empty scenes without generated work. Observer exposes five additional stroke domains and checks native pixel scale/actual first-pair bytes. Thin284 and nonuniform-scale GPU11 pixels remain unresolved; whole SVG equality is incomplete.

### SVG camera bounds selection and diagnostics

The common Vulkan vector shader rejects out-of-bounds paint work while preserving structural commands, one-pixel AA margin and source order. It creates no CPU visibility lists. `gpu-svg-camera-bench 3` records full output hashes and separate CPU/GPU timings for two originals and five zooms, with 16 warmup/32 measured frames per case. Three alternating runs show 4.7–14.3% lower GPU time in 9/10 cases; Red 400% and serialized latency remain inconclusive. See the [execution and measurement guide](svg-gpu-camera.md).

### Completed Vulkan work reuse and Linux UI measurement

Upload/device work buffers reuse only after the GPU fence and q+N retirement deadline; completed cache is bounded to64MiB/256 entries. Immutable scene ownership remains separate. `gpu-vector-cache` verifies no early reuse with1/3 slots, stable allocations after warmup, raw RGBA preservation and fence-safe release. `gpu-svg-camera-present-bench` excludes measured-frame CPU readback. Linux Observer `--verify-slider-performance` compares the same ordinary slider in isolated/full/frozen-diagnostic conditions. Header diagnostics sample with FPS to reduce observer overhead. Cold entry and native pointer latency remain separate validation targets.


### Common animation deadlines and Linux input streams

Widget animations schedule their next sample with the shared default60Hz `UiFloatTransition` interval; actual input and earlier repeat requests remain immediate. Explicit continuous rendering remains available. Three Linux120Hz comparisons reduce Fluent CPU58.62%, builds74.02% and median dispatch81.96%; isolated slider CPU instead rises12.85%. Use the preserved pinned RC3 NativeAOT [launch guide](../artifacts/linux-animation-pacing-2026-11-15/README.md), verified across33 pages. Physical display/user acceptance and whole-product acceptance remain open.


## Linux presentation reuse and default NativeAOT launching

The common renderer compares the last actually presented scene with native invalidation sequence. Identical new CPU work is accepted without image acquisition, submission or presentation. `UiRendererFrameMetrics.PresentationReused` distinguishes this result; actual Present sequence/timestamp do not advance. Expose/remap and visual changes present again; callbacks, GPU animation and dirty paint groups still execute. Current product host integration is Linux X11; other hosts keep their existing route. `DUXEL_VK_PRESENT_REUSE=0` disables only this acceptance path for same-executable comparison.

Three alternating pinned RC3 NativeAOT pairs reduce isolated-slider CPU from52.97% to24.90%(one core=100%), a53.00% reduction. Whole Fluent scenes and dispatch latency have overlapping run ranges; no additional speedup is established there. Original native acknowledgement timeouts also occur in the prior executable and remain unresolved. Independent GPU588 pairs/3704 checks, actual expose/remap21 checks,33 presentation scenarios,384 native inputs, nine stationary holds with zero extra builds, dragging and SpinButton repetition pass. Physical display experience and whole-product acceptance remain separate.

Without `PublishedExecutable`, `run-gallery-linux.ps1` now publishes NativeAOT inside WSL using the pinned SDK. Select its exact installation with `-LinuxDotnet`; use `-Managed` only for explicit managed comparisons. Missing SDKs/version mismatch fail without managed substitution. Use the [presentation reuse guide](../artifacts/linux-present-path-2026-11-15/README.md) for an immediately runnable verified executable, hash and commands.


### Synchronous Dialog activation origin and accessibility focus

`Dialog.Show()` in a synchronous `UiButton` activation callback remembers the actual invoking button. Ordinary accessibility Invoke does not steal focus; dismissal returns to that button. Programmatic calls outside callbacks retain previous-focus semantics. Pending origins use a token that does not retain the UI context. CPU107, Windows UIA13/host2, Linux AT-SPI14/host3 and registry retirement checks pass. Dialog is11/12; pinned RC3 Windows AOT/full integration remain. [Evidence and scope](../artifacts/fluent-dialog-2026-11-15/focus-origin-verification.json).


### Fixed NativeAOT Linux probe results and opened Dialog

Linux.App.Tests now replaces14 anonymous result writes and the observation object with shared typed/source-generated JSON; reflection JSON is disabled. `result-contract <fresh output>` checks names/casing/order/values/formatting;25 managed/AOT JSON files match. Original field lists from14 writes/one observation are preserved. Actual opened Dialog in pinned RC3 NativeAOT passes independent client14/host3 checks and registry retirement. The full Linux accessibility suite was not rerun; do not expand this to Windows RC3 AOT, whole integration or a new speedup. [Commands and scope](../artifacts/linux-probe-results-aot-2026-11-15/README.md).

MessageBar: use **표시할 때 자동 공지** and **차분한 공지로 고정** on the scrollable **메시지 표시** page and inspect the announcement request count. Actual title relationships and one mount/priority announcement are supported; reorder/removal do not repeat it. Requests do not prove spoken completion. MessageBar is 12/13, with full conformance pending. [API and scope](fluent2-messagebar.md).

MessageBar → **오른쪽에서 왼쪽 배치** switches real slot/action placement. Common `UiWrapOptions.RightToLeft` mirrors child X positions while retaining navigation and row order. It does not guarantee arbitrary child paragraph bidi. Whole MessageBar remains 12/13.

MessageBar is 12/13. Actual title targets and automatic announcements passed Windows UIA 8 client/1 host and pinned Linux RC3 NativeAOT AT-SPI 9 client/3 host checks. Complete paragraph RTL remains; Windows NativeAOT is a separate gate. The qualified Linux Observer is retained.

MessageBar multiline/shape/direction/announcement options also apply to newly inserted dynamic messages. Passed 34 interaction/page checks and 38 Windows/41 Linux/38 delivered presentation scenarios. MessageBar is 12/13; complete RTL/reflow remains pending.




Duxel Gallery: run ./run-gallery.ps1 or ./run-gallery-linux.ps1 with the existing explicit Linux arguments. The fixed header includes light/dark choice; seven functional groups list55 canonical component names alphabetically, with Fluent icons and16 inline missing badges. Routes borrow persistent example models. UiTabOption.Badge / UiTabBadge and UiControlContext.PaintFluentBadge share the common badge pipeline. UiImmediateContext.GetStyle(includeRequested: true) reads the latest queued theme for ordered settings input; the default returns the applied scoped theme. [Current verification](gallery-navigation.md):410 gallery,73 Badge allocation,238 Rating/forced-color and704 legacy checks;64 normal Windows presentation scenarios and Linux host build. Native Linux and complete component qualification remain separate.


## Drawer and compact navigation

`Fluent.Drawer(title, content, options : new(...))` owns its content and optional footer actions. `Open`, `OpenChanged`, `Show()` and `Close()` control visibility. `UiDrawerOptions` supports overlay/inline hosting, logical Start/End/Bottom placement, RTL, 320/592/940/full standard extents, explicit extent, modal and light-dismiss behavior, and fixed header/footer with a scrolling body. Inline hosting requires finite cross-axis space or `InlineCrossExtent`.

Overlay entry/exit translates retained content without scaling it; completed motion stops requesting frames. Inline allocation currently changes immediately; animated inline reflow and full platform conformance remain pending. `ScrollBody : false` allows an already scrolling, finite-height body such as Gallery navigation. Hidden navigation hosts call `RenderPage(..., navigationVisible : false)` so the named Pane does not reference an absent Link.

Run `./run-gallery.ps1` → **알림과 진행 상태 → Drawer** for overlay and inline examples. The compact Gallery keeps the theme control and groups inside its navigation Drawer. Input models survive dismissal and theme changes. Official guidance: [Fluent Drawer](https://fluent2.microsoft.design/components/web/react/core/drawer/usage). Verification: [Drawer record](../artifacts/drawer-2026-11-17/verification.json).


2026-11-17 Drawer hosting verification: 564 Gallery checks include eight additional GPU/interaction checks. Captures cover compact navigation closed/open in dark/light themes, selection, explicit dismissal and both Drawer sample modes. The normal Windows runner passes 72 presented scenarios, including open/close/reopen/save with actual accessible commands, visible focus inside the overlay and focus restoration after close. Linux host compilation passes; native Linux was not rerun. Product modules are byte-identical to the prior Drawer build, so prior core regression results are reused. Inline allocation animation remains pending. [Evidence](../artifacts/drawer-hosting-2026-11-17/verification.json).


## Independent examples and equivalent source

Every Gallery route has a distinct persistent page; duplicate page destinations fail startup validation. Twenty-one formerly shared component destinations now have standalone examples. Combined regression pages remain under **종합·회귀 예제**. Each page displays a selectable, read-only C# source pane below its live example, with independent vertical/horizontal scrolling.

Standalone source comes from `CallerArgumentExpression` on the exact executed factory. Existing complex examples display their actual `.cs` file embedded at build time. This requires no repository files at runtime. Complex files use the Gallery project's supporting types and references. Planned components explicitly report that executable source is unavailable.

`UiViewHost` borrows an `IUiView` and hosts sequential rendering inside finite declarative bounds without assigning the entire allocation to each leaf. Child clips, popup tiers and nested windows retain their own geometry. The caller owns the borrowed view. Inline Drawer uses the shared `UiFloatTransition` for allocation and clipping, preserves full-size content, reverses from the sampled extent and retires interaction immediately on close. Initial open inline placement is immediate; later changes animate unless `MotionEnabled` is false. [Verification](../artifacts/gallery-source-2026-11-17/verification.json).

The shared page heading matches the sidebar label and keeps the description close, with a larger gap before the examples. A sample's settings, status and preview precede its source in the same scroll area. Overview and Scope omit source.

## Gallery page layout and source display

Windows project and executable names are `Duxel.Gallery`; Linux uses `Duxel.Gallery.Linux`. One shared page frame displays each heading and description. Natural examples are measured against the actual scroll-content width and source follows immediately. Markdown uses the selected editor, preview or nested-preview height. Canvas examples place source after their explicitly allocated viewport.

`UiCodeView(code, options)` provides selectable, read-only C# source. `UiCodeViewOptions.Wrap`, `LineNumbers` and `MaximumVisibleLines` control wrapping, line numbers and initial visible lines. Full display, wrapping and line numbers are enabled by default. Turning full display off collapses to the default 32-line limit. `InitiallyExpanded` sets the initial expansion state. Lexical syntax highlighting is recomputed only when source changes, preserving original UTF-16 positions. Overview, Scope and unimplemented pages omit source.

Gallery builds scope `Cache/Duxel/nuget-http` on the repository drive to the child build process, preserving user and machine environment settings.

Gallery headers belong to separate regions. The navigation title and summary stay above the left menu; theme, diagnostics and options stay at the top of the right content area. They do not share one full-window header.

Source copy uses a translucent vector icon at the code surface’s top right. Copy shows a confirmation above that icon for 1.2 seconds and stops scheduling frames after dismissal. `UiCodeView.Metrics` exposes measurement, paint-layout and syntax-parse counts. Shared `UiVisualTextVariants` retains two width variants for scrollbar probing and committed layout, avoiding repeated wrapping when those widths alternate. A named `UiSvg` publishes its Image role, description and bounds through `AccessibleName`; unnamed decorative SVGs add no accessibility node.

When CodeView source changes, unchanged leading and trailing lines can reuse their native layouts if typography and rendering mode match. Shifted lines use the new document's source and paint-style positions. [CodeView and SVG composition measurements](optimization-session-2026-11-19.md#codeview-reuse-after-an-svg-transform)

The navigation header reuses the vector symbol from the repository logo.svg with a title, feature caption and status badges. Unimplemented badges use orange. Theme, performance and options share one content-header row; narrow layouts pin the theme on the right and scroll the other items horizontally. Sessions skip widget layout for zero-sized closing or minimized clients and wait for the next native event.

Code highlighting distinguishes interpolated string text, interpolation delimiters, embedded C# expressions, escapes, methods and operators. It preserves the original UTF-16 buffer and reparses only on source changes. Dark/light palettes and forced colors are supported. Korean display examples use 디모이.

Full code measurement and painting share line geometry. Native snapshots are retained per hard source line; only lines wider than the new viewport require native rewrapping. Text raster keys check fonts, glyphs, advances, offsets, subpixel phase and backend generation while allowing integer translation and source-index changes to reuse pixels. Currently painted text is protected from the 512-entry cold-cache budget. Newly hidden keys become eligible after one frame; layer ownership and idle expiration remain independent.

Vulkan reuses small text textures of identical dimensions/format only after the existing fence retirement deadline. The pool is bounded by 2,048 entries and 16MiB of actual allocations. Descriptor pressure reclaims completed idle entries; shutdown destroys the pool. Quality, clipping and damage contracts remain intact. See `artifacts/gallery-source-2026-11-17/verification.json` for validation.

`UiCodeViewOptions.ShowToolbar` defaults to `true` and controls the heading, line count and display-options toolbar. Gallery uses `MaximumVisibleLines: null, ShowToolbar: false`, keeping wrapping, line numbers and full display enabled while showing only code and its copy action. `GallerySample` captures the actual preview factory's C# expression and places an independent `UiCodeView` directly after that preview. Cards and typography use per-example source placement; other compound pages are still being separated.

`Fluent.TagPicker(label, query, selection, items, options)` displays selected values as tags and searches candidates in the same field. It preserves selection order and excludes selected choices from search results. Backspace or Left at the start of empty input focuses the last tag; Delete dismisses that tag. Rejected controlled-selection changes preserve the current tags and query. An external Label delegates to the internal editor. Secondary actions, freeform entry and full Fluent conformance remain incomplete.

Gallery snippets omit the common `using Duxel.Core;` directive. Actual project files and the general-purpose `UiCodeView` buffer remain unchanged. Other imports and matching text inside source string literals are preserved. Import `Duxel.Core` at project scope when moving examples into another application.

`GallerySample.BindSource` compares typed snapshots of current settings and updates that code block only after a change. Rating color, shape, direction, selected value and review count follow the actual controls. Per-example source placement currently covers Cards, typography, Rating and Avatar. Other compound pages and live setting connections remain in progress.

Gallery source blocks escape Unicode line and paragraph separators in user-edited string values so copied C# preserves the original text and compiles. Icon snippets using `UiFluentIcon` also require the icon generator and its pinned catalogs, as described in the [Fluent icon guide](fluent2-icons.md).

With the Windows graphics runner, `<fresh-output> gallery-samples-only` exports the displayed Card, Containers, Drawer, and Markdown examples into `sources/`. The Markdown files cover editor, viewer, nested scroll, and images. For independent compilation add `Duxel.Core` to the project's imports and reference the packages used by the example. Keep the runtime dependencies and any referenced SVG assets alongside the app output.

Navigation separates Fluent controls, Duxel widgets and integration/development tools. Classification does not depend on Korean or English titles. `Fluent.Nav` implements Fluent 2 Nav design and behavior on the Duxel engine and remains in the Fluent section; distinguish this style from its generic `UiNav<TPresentation>` base. SVG, Markdown, layout and virtualization belong to Duxel, while repaint, measurement and rendering inspection belong to development tools.

ScrollView owns clipping through its viewport. It does not intersect the translated full-content allocation a second time, preventing cancellation error at fractional end offsets from terminating normal rendering. Origins that cannot distinguish physical pixels are still explicitly rejected; content at those scales requires virtualization.

Gallery layout examples show adjacent source for nine practical cases: Row, Column, Wrap, UniformGrid, Stack, Align, Center, Padding and Container. Counter, grid-column, overlay-position and alignment changes update the matching initializer; unchanged state retains parsed syntax. Existing compound tab/menu/overflow examples moved to Layout Integration under the integration section. That integration page now also provides separate sources for eight practical cases.

`UiNavGroup.SectionHeader(value, label)` creates a noninteractive section title, distinct from category icons/disclosures and indented destinations. It does not add another navigation nesting level. See the [API and accessibility rules](gallery-navigation.md#section-titles-and-categories).

Windows Gallery explicitly uses Assets/Brand/duxel.ico for both its ApplicationIcon resource and Window.IconPath. It contains 16, 20, 24, 32, 40, 48, 64, 128 and 256 px images. The ICO is generated offline from the existing SVG brand mark and reused; window-icon setup does not reparse SVG at startup. Default icon behavior for other applications keeps its existing contract. Built-in Fluent icons use dedicated TTF glyph outlines and GPU caching; UiSvgIcon separately supports SVG icons.

SpinButton shows separate code blocks for quantity and custom-unit input, including current value, read-only/size/appearance settings and the unit parser/formatter. Toolbar source tracks orientation, size, RTL, child toggle state and action count. Remaining page-source separation and setting connections are still in progress.

The AvatarGroup Gallery places its source directly below the actual group. The code follows the current layout, size, inline-item limit, direction and selected overflow member. It includes SVG image and icon setup and reuses the parsed source while the state remains unchanged.

The Persona and Accordion Gallery pages place source directly below each actual preview. Persona code follows the current name, information lines, six presentation options, inactive state and SVG image setting. Accordion code follows open panels, memo input, nested button count and all four options. Existing widget instances and input state remain intact, and unchanged state reuses the parsed code.

The List Gallery attaches separate source blocks to its plain, single-select and composite multi-select examples. Code reflects the selected city and member array, memo input, command count and selection-retention setting, including the composite row-action handler. Each block uses its actual preview factory and retains its parsed source while state remains unchanged.

The Dropdown Gallery attaches separate source blocks to seven examples: single selection, multiple selection, Combobox search, actionable feature tags, basic tags, controlled InteractionTag and avatar tags. Code follows current values and presentation options, query/open state, tag emphasis/rejection and inactive avatars. GallerySample.Define captures the actual shared option-value factory; each relevant snippet includes its SVG resource setup and Field layout.

The Popover Gallery attaches separate source blocks to Fluent and custom-surface examples. Code follows input, command count, open state, rejected close requests, motion, focus, appearance and positioning options. The custom presentation includes its actual separately compiled declaration, read once from the cached source resource. Declaration files contain the common Core import, Gallery file-scoped namespace and complete types; a separate string implementation is not maintained.

The Drawer Gallery attaches separate source blocks to overlay and inline panels. Code follows shared memo input, save count, visibility and every Drawer option, including the actual creation expression and renderOverlays(ui) hook invoked after body rendering. The Gallery retains its existing overlay rendering outside the body clip. Skeleton sizes 48 and 96 each own source; the existing Watch path updates previews and code together when loading, motion or appearance switches change.

The Dialog Gallery attaches separate source blocks to its modal/nonmodal/alert opener and motion-curve preview. Code reflects input, validation messages, visibility and every Dialog option; renderOverlays(ui) keeps the dialog outside body rendering. prepareMotion(ui) updates the curve value before the body. Only target/curve configuration updates the motion snippet, so animated values do not trigger source reparsing. Checks cover saved input, opener focus restoration and the end of frame scheduling after the 250 ms transition.

The MessageBar Gallery attaches independent code to its four fixed intents and keyed dynamic messages. Snippets follow current layout, width, shape, direction, announcement/motion options, dismissal state, action count, key order and next-key counter. Insertion, removal and reorder code is captured from the actual factories and callbacks; diagnostic announcement counts do not regenerate snippets. Each source uses its own UiCodeView with full display, wrapping and line numbers enabled by default.

Common arranged widgets validate prepared bounds against the child snapshot revision. A same-count reorder or replacement invalidates geometry belonging to previous child identities. Regression checks cover unequal-height PresenceGroup reorder/replacement and rapid MessageBar reorder.

Native MessageBar verification reveals the upper controls before input, then scrolls to the inserted messages before inspecting geometry. Clipped offscreen accessibility bounds are not treated as actual layout sizes; verification uses published visible bounds and public options.

The VirtualList Gallery places source immediately below the billion-row preview and its actual navigation commands. Code includes the separately compiled row source and variable extent index declarations. Height mode, key ordering and edited keyed values update the snippet; scroll offset and realization/lifetime diagnostics do not cause reparsing.

Set `UiVirtualListOptions.Axis` to `UiScrollAxis.Horizontal` for a list with a bottom scrollbar. Set `FlowDirection` to specify left-to-right or right-to-left order, or inherit it from the containing Fluent provider. `ItemExtent` and `ExtentIndex` follow the selected axis. The thumb, wheel and accessibility requests share the logical double offset. Try both direction controls in the [VirtualList Gallery example](virtual-list.md#verification-and-sample).

The Linux test runner's `atspi-gallery-virtual-horizontal` mode drives the actual Gallery over D-Bus and verifies `Component.ScrollTo` for a clipped, live editor.

For cross-host Vulkan comparisons, `virtual-list-host-pixels` saves separate RGBA captures of the clear, geometry and native text from the same billion-item tail scene on both axes.

Its `.scissor.tsv` records the logical clip, effective scissor and render area for each recorded draw.

`clip-host-pixels` isolates leading-edge Vulkan clipping with a bordered rectangle and triangle mesh across MSAA 1× and 4×.

`Dux.VirtualGrid` realizes a keyed rectangle across two logical scroll axes. Native table lookups return a provider for distant row and column coordinates without scrolling. An explicit realization request fills that same provider. See the [virtual grid example](virtual-list.md#two-axis-grid).

`virtual-grid-pixels` compares the two-axis grid with independently positioned Vulkan reference draws, including scrollbar interaction between the axes.
`virtual-grid-host-pixels` runs the same checks and exports selected raw RGBA frames for cross-host comparison.

In the Windows graphics runner, `<fresh-output> image-shader-bench 3 projected-clip` measures GPU and readback time for 256 fractional bordered rectangles with individual clips in a projected layer. Use the same runner and GPU for before/after comparisons.
Add `color-image` to sample textures in the same layout. The Linux Vulkan runner exposes both workloads as `projected-clip-bench` and `projected-image-clip-bench`, appended after the font arguments.

The projected layer shader evaluates edge coverage before discarding fragments outside the local clip. This keeps neighbouring fragments available for coverage derivatives at fractional borders.

The Windows graphics runner's `<fresh-output> radio-allocation-matched 3` requests a 400 × 300 physical client area when it creates the test window. It checks the framebuffer size before starting Vulkan and uses a fixed 16-pixel base text size for the matched image suite.

Compare each constrained RadioGroup capture against its immediate-mode reference on the same host to verify parent allocation and clipping. A pixel difference between hosts inside the assigned rectangle can remain even when both hosts pass this geometry check.

The widget layout Gallery attaches independent source to eight examples: button, checkbox, slider, input, popover, menu, tooltip and compatibility popup. Common margins, rotation, scale, pivot and visual/layout mode apply to each actual target and its code. Edited values and action counts update only related snippets; compatibility popup source includes its actual separately compiled declaration.

The layout integration Gallery provides eight independent sources for tab overflow, priority overflow, and infrastructure/Fluent/long/checkbox/radio/icon menus. Code reuses actual factories and owned tab-panel declarations, following width, direction, orientation, priorities, panel counters and shared checkbox/radio state. Transient measurement and hover diagnostics do not trigger source reparsing.

Integration source bindings compare typed value snapshots rather than preformatted strings. Equal state skips formatting. After warmup,1000 passes over the eight unchanged SynchronizeSource callbacks allocate zero managed bytes on the calling thread; this is distinct from complete frame performance.

### Rendering examples and named sequential regions

The Rendering Gallery interleaves five persistent previews with their actual compiled source: retained geometry, 3D projection, prepared reference compositing, runtime group compositing and projected text. Current revision, clipping and animation switches update only affected source snapshots. Allocation changes use separate cache versions and do not change the displayed content revision. Animation schedules frames only while enabled.

Set `UiViewHost.AccessibleName` to expose its finite allocation as a read-only, nonfocusable accessibility pane. Sequential children retain their own names/actions and become children of that pane. Unnamed hosts keep the unwrapped hierarchy. Named drawing regions support the existing ancestor ScrollIntoView action, allowing offscreen Gallery previews to be revealed before pixel verification.

### Fixed Gallery headers: scoped regression evidence

Revealing a lower Rendering preview preserves sidebar branding, global option and theme-switch geometry. CPU geometry at 1280x900 and GPU pixels at the actual 1600x1125 framebuffer are checked, including the existing multi-page action sequence. Earlier apparent clipping in an image preview was not present in the original PNG; matching header-region hashes confirm the correction. No product renderer change was needed. This evidence does not extend to every compact/resize/Linux scenario. See [verification](../artifacts/gallery-fixed-header-2026-11-17/result.json).

### Popup input example source

The Popup Input Gallery uses its natural body height and places actual source immediately below it. The captured source includes owned popup controls, balanced BeginPopup/EndPopup rendering, current NonModal/Exclusive mode, open state and both command counters. Popup rendering runs after the page body clip is released, preserving the floating surface and exclusive background-input behavior. This infrastructure example remains separate from the Fluent Popover widget.

### Tooltip examples and live caller-owned visibility

The Gallery Tooltip page provides four adjacent preview/source pairs: automatic description, arrow, accessible name and caller-owned visibility. Actual Fluent.Tooltip factories preserve the description/name relationship. Only the caller-owned example's source follows Visible changes, including Escape through VisibleChangeRequested; automatic hover does not reparse source. The page owns its persistent root immediately and has its own source file.

### Editing examples and reactive validation source

Input/IME separates name validation and memo editing with a linked label into two adjacent preview/source pairs. The actual compiled ValidatedNameExample declaration watches its borrowed name state and updates Field validation; copied source retains that behavior. Memo source includes current text,wrap/read-only flags and all label text/options,including escaped strings and bitwise flag expressions. Snapshots exclude caret/selection and unchanged state retains parsed syntax. Reveal each input before accessibility value actions because source blocks separate the two previews vertically. These sample checks do not replace operating-system IME composition acceptance.

### Measurement examples and source snapshots

Measurement Reuse separates retained primitive controls and the composite form into two bounded preview/source columns. Actual compiled MeasurementPrimitives/MeasurementForm declarations retain their original measured overrides,control bindings and finite420x420/420x430 panels. Source follows current values,label/body/badge/spinner/width settings and tab size; measurement/progress-read counters are excluded. Separate source snapshots avoid making diagnostic counters a source-rebuild timer. Counter reuse remains distinct from complete CPU/GPU/FPS cost.

### Repaint experiment source and diagnostic isolation

Repaint Inspection owns one natural-height experiment followed immediately by its actual compiled RepaintExperiment source. Automatic-change and CPU-profiling settings synchronize; runtime input text,revisions,draw counters and measurement snapshots intentionally do not rewrite source,so observation is not contaminated by code-view changes. The experiment retains its64px dynamic and140px static panels,frame scheduling and snapshot actions. Its body render timer excludes the sibling source view and outer natural measurement; global CPU profiling still measures the complete UI Render scope. The borrowed sequential host and all controls/content are explicitly owned.

Markdown Gallery modes now place one matching live source block immediately below the selected editor, viewer or nested preview. The current document and selected Markdown options synchronize through a typed snapshot; caret/selection and scroll offsets do not rewrite code. The nested example captures both real ScrollView factories. MarkdownImageResources is the shared compiled, cached SVG/memory-image resolver included in the source; unavailable images retain their alternative-text behavior.

Markdown document snippets use readable multiline C# raw literals. The delimiter expands beyond embedded quote runs; CR/control characters retain escaped literal encoding so copied text remains exact.

The Fluent overview now groups counter/badges, shared toggle state, mixed checkbox, radio choice, slider/progress, internal link and Spinner playback into seven adjacent preview/source pairs. Each tab appearance has its own source and compiled icon declarations. Current bound values synchronize independently; Spinner ticks and idle frames preserve parsed sources. ControlsPage and TabExamples own their persistent roots before first rendering.

The integrated Spinner example sets Width180 so it can share a row with its playback button. Integration regressions reveal radio/counter targets before real input; source insertion can move them offscreen without changing their semantic contracts.

SVG primary examples now have separate inline sources for image/vector comparison, SVG text and document features. Sources expose current transforms, corners and image fit, and return the SvgStore resource owner with the preview so the caller can dispose them in the correct order. The actual page continues to share one store and bitmap decoder. Browser fixture and icon sources now use the same inline path; no whole-page reference remains.

Use ui.RenderNaturalHeight(widget) when placing a naturally sized widget tree into sequential content. It measures with finite available width and unbounded height, allocates the full result and keeps parent clipping/scroll ownership. Ordinary widget.Render retains bounded-layout validation. This bridge avoids treating a scroll viewport remainder as a vertical Column content budget; no catch, clipping bypass or arbitrary height cap is used.

Legacy BeginChild/EndChild now publishes its own accessibility Pane and actual scroll range, preserves nested parents and enclosing clips, and consumes ScrollIntoView/scroll requests. This makes offscreen descendants revealable before pointer input; a900px SVG regression scrolls past both source blocks and opens the real icon menu. The pane uses the caller’s display label rather than a scoped internal ID.

SVG browser comparisons now use the compiled SvgBrowserPair host and shared SvgBrowserReference fingerprint validation. Each pair owns its complete source,including current transforms/background,portable asset paths and actual helper declarations. SvgExampleSource centralizes resource-returning source factories. SVG icon adapters have separate live TabList,Menu and standalone Icon sources. Active-source metadata follows the selected tab and the former mixed whole-page reference is removed.

Application theme changes now synchronize native Windows caption, text and border colors through optional IWindowThemeController. DuxelAppSession coalesces color updates and applies them on the owning platform event thread, only when colors change. Windows reuses its checked DWM color path, preserves forced-color priority and honors disabled system-chrome integration. No process-wide Windows theme setting is changed.

The legacy Gallery page uses twelve ImmediateSampleSection callbacks shared with Duxel.Sample. Each actual callback supplies its own captured source and typed state snapshot; string and syntax rebuilding occur only when that section changes. The Gallery owns one UiCodeView per visited section, placed immediately after its controls with full source, wrapping, line numbers and copy action. Inactive tabs and collapsed groups hide their source blocks while preserving sample state. The standalone sample keeps its original controls without Gallery source presentation.

Legacy child accessibility reveal now applies the pending scroll offset before rendering descendants, using the published scroll range. This keeps their geometry and reported offset in the same frame and prevents repeated reveal deltas. The finite nested Gallery regression waits for completed accessibility requests and verifies visible sliders, table edits and source state rather than treating queue acceptance as completion.

GalleryNavigation.LatestAddedMenus is the single explicit latest menu-addition batch (currently TagPicker). These implemented destinations receive a green NEW badge on the right. Replace the batch when new destinations are added; existing menu edits, elapsed time and visit state do not extend it. Planned destinations retain their separate orange unimplemented badge. Unknown or planned NEW keys fail startup validation.

Combobox completion publishes visible popup rows and hidden canonical options against the same filtered projection. TagPicker pointer selection clears and closes the input first, then rebuilds its exclude-selected projection only after accessibility publication. Keep strict per-frame ID uniqueness; do not suppress duplicate-ID failures or discard canonical selected options. Regression coverage uses real press/release selection of Kim and Park, repeated expansion, closed option state, and the actual Gallery Vulkan popup path.

Scrolled tab and navigation bodies derive clipping from physical content end coordinates rather than remaining layout budgets. Legacy tabs, Fluent tabs and navigation pages share this calculation; semantic bounds and focus outlines follow the visible region. [Contract and qualification](fluent2-tab-content.md).

All22 standalone examples use the shared inline source widget. Sixteen interactive factories update initializers through typed comparisons; unchanged state reuses source and syntax. Explicit selection and completion records support partial verification continuation, not automatic cache enforcement. [Contract and qualification](gallery-navigation.md).

Fluent.FluentProvider scopes palette, geometry tokens, optional UiTypographyFonts and layout direction across measurement and painting. Set `Direction` to inherit or override the surrounding scope. Supported navigation and popup controls resolve their positions from this scope, including deferred Tooltip. [API, limits and qualification](fluent2-provider.md).


Fluent.Shimmer owns a Skeleton placeholder and real content, reusing finite transitions and exit-paint snapshots. Settled loaded content requests no frames. [API, example and limits](fluent2-shimmer.md).


The Gallery desktop navigation uses280 logical pixels in its default window; compact drawer switching remains unchanged.


Accepted Windows WM_CLOSE hides the window before safe resource teardown; existing LogStartupTimings also reports shutdown phases. [Measurements and scope](windows-close-response.md).

The Windows x64 .NET 11 NativeAOT Gallery also exits normally when WM_CLOSE arrives with a visible TimePicker popup. [Popup close verification](windows-close-response.md).

The Windows .NET 11 managed and NativeAOT Gallery verifier presented the same 104 scenarios in order, then exited normally via `DuxelAppSession.Exit()`. [App-driven exit verification](windows-close-response.md).


Fluent.ProgressIndicator owns a task label, description and determinate/indeterminate track. Track hiding preserves text; reduced motion uses opacity changes. [API, limits and qualification](fluent2-progress-indicator.md).


Fluent.ActivityIndicator uses the Fluent iOS270degree/750ms/five-size contract. Start/Stop preserves allocation; color, naming and stopped visibility are configurable. [API, limits and evidence](fluent2-activity-indicator.md).


Dux.Calendar/RangeCalendar provide localized single/range selection and year/month wheels. Dux.DatePicker/TimePicker/DateTimePicker connect editable fields and popups, sharing UiWheelPicker<T>. IUiCalendarHolidayProvider or immutable snapshots annotate holidays; network failures, interrupted connections and timeouts omit annotations without UI errors; date selection stays available. [Calendar](calendar-globalization.md), [editable pickers](date-time-pickers.md), [holidays](calendar-holidays.md).

The external `atspi-gallery-range-calendar` client checks reverse date selection, culture changes and clearing in Linux Gallery. It passed on the current .NET 11 x64 managed build and the previously published x64 NativeAOT build. [RangeCalendar verification](calendar-globalization.md)

The `x11-gallery-range-calendar` mode also checks that Enter starts a new range and Escape keeps the previously committed dates. It sends X11 events to the specified Gallery window and reads the result via AT-SPI. [Keyboard verification](calendar-globalization.md)

`x11-gallery-range-calendar-pixels` also confirms a range through the keyboard and checks that switching to Thai Buddhist preserves it. WSLg managed and NativeAOT matched six viewport pairs below the diagnostic header. [Run the pixel check](calendar-globalization.md)

- SegmentedControl body preview: `UiSegmentedControlWidget` / `UiSegmentOption` / `UiSegmentedControlOptions`. [API and remaining scope](fluent2-segmented-control.md). The Fluent factory remains unregistered and the pending badge is retained.

- SegmentedControl preview supports the four standard light/dark enabled/disabled styles, size15 body2, horizontal inset16 and ease-out selection movement. Options include Style/HorizontalInset/UnreadLabel. Scrolling/transparent masks remain open; the Fluent factory is unregistered.

- Horizontal opacity masks: UiHorizontalOpacityMask and PaintGroup/BackdropImage/BackdropComposite overloads. UiImageUvTransform changes the cached camera; mask/UV-only changes retain captures. [API and example](horizontal-opacity-mask.md).

`Fluent.SegmentedControl` is registered(50/55,5 pending). It provides intrinsic-width drag/wheel scrolling, accepted-selection reveal and UiPaintGroup cache/UV camera with hidden-edge opacity masks. Five Gallery samples show live source; the latest NEW batch is Select. Actual Linux/NativeAOT/external assistive-technology acceptance remains open. [Current contract](fluent2-segmented-control.md).

Toast provides UiToastController/UiToasterWidget nonmodal notifications, expiry/hover/focus pause, F6 return and native activation propagation. Independent UiToastWidget shares progress/spinner and long-content ScrollView composition. Viewport/text-scale capacity defers excess notifications while preserving unused duration. One live host per controller is enforced; disposal permits replacement without losing notifications. Four Gallery samples show live source. Fluent.Toast is registered; Fluent.Toaster creates its viewport host. Select alone carries NEW and has no pending badge. Full platform acceptance remains open. [API and running](fluent2-toast.md).

Toast title/body/status use native paragraph shaping and RTL per-line alignment. Compact width moves content below the header; insufficient header width defers notifications. Shared UiInlineText.HorizontalAlignment independently aligns each line left/center/right. UiProgressValue.RightToLeft and the corresponding UiProgress property set the logical start of Fluent progress painting.

Fluent.Select is a plain single-choice field with first-enabled initialization, closed arrows/Home/End/Page and typeahead, required selection accessibility, three sizes/four appearances and a shared popup. Its three inline Gallery samples show current values and options. [Contract](fluent2-select.md).

Tree has an internal retained widget consuming the validated hierarchy/expansion/cascading-selection models, qualified by input24/GPU7 checks. It exposes Tree/TreeItem ancestry and RTL layout. The public factory and Gallery are connected. External assistive-technology acceptance remains pending. [Contracts and remaining work](fluent2-tree.md).

Accessibility `CaptureSelection(id, runtimeId, out selection)` and `ResolveSelectionContainer` expose selection membership from one published revision, also available on immutable snapshots. Windows UIA and Linux AT-SPI share ownership semantics; Tree queries exclude independent nested list selections. Collapsed single-selected/demanded items publish only needed virtual paths, with ItemContainer/Realize/ScrollIntoView expanding ancestry while retaining RuntimeId, selection and focus. Actual Linux/external assistive-technology acceptance remains unverified. [Tree contracts](fluent2-tree.md).

Public `UiTree<TPresentation>` separates row measurement/painting through `IUiTreePresentation`. Informational, nullable-single and terminal-multiple constructors support size/appearance/direction options and committed Unicode name search. The Gallery demonstrates the public Fluent.Tree factory and row actions. [Usage/contracts](fluent2-tree.md#public-presentation-and-incremental-name-search).


Three-argument `UiTreeItem` uses a presentation-owned string label with default wrapping, size-specific typography and `UiTreeOptions.TextOverflow = UiTreeTextOverflow.Ellipsis`; truncated rows expose a full-name Tooltip. The existing fourth widget argument preserves explicit content. [Contract](fluent2-tree.md#plain-string-labels).

Run the shared Tree Vulkan checks on Windows or Linux with `tree-gpu` and `tree-label-gpu`. They cover pointer hit targets, RTL hierarchy, themed hover states, and long-label layout. [Linux runner and arguments](../tests/Duxel.Vulkan.Linux.Tests/README.md).

The same runners offer `container-pixels` for ScrollView offset and clip checks, custom container root clipping, and Grid updates under an unchanged parent allocation. Each host compares its output with independently drawn references.

The Gallery **Containers** page includes an interactive ScrollView with vertical and two-axis options and matching inline source. [ScrollView usage](scroll-view.md).

The Linux `atspi-gallery-tree` probe runs as a separate libatspi client. It operates the actual Gallery Tree and opens an action from a TreeGrid cell. [Command and scope](fluent2-tree.md#inspecting-accessibility-on-linux).


For controls inside a Tree row, opt into `UiTreeOptions.RowActions`. F2 enters, arrows navigate and Escape returns to the row. RowActions also exposes the TreeGrid accessibility role with one content cell per expanded row. The Linux AT-SPI bridge exports Table and TableCell from published grid metadata. See the [Tree sample and contract](fluent2-tree.md#controls-inside-a-row).

The Gallery registers `Fluent.Tree` and shows two live examples. `AlignSelectorToTop` moves the selector and hit area to the first row slot. Tree, Accordion, Nav, option fields, legacy Combo and submenus use the same 160 ms expansion and collapse timing.

Switch thumb travel, Fluent ToggleButton selection color and Fluent Checkbox marks use finite state transitions. The binding and accessibility state update immediately. Event-wait mode resumes when the transition ends.


`Fluent.TextField` composes the shared single-line editor and Field with a clear action that stays available for nonempty text. The Gallery has examples for short text and validation on submit. See [the Input and TextField guide](fluent2-input.md#textfield).

`Fluent.CardNudge` is a controlled inline suggestion that dismisses through its close button or a horizontal swipe. The Gallery includes a live source example. [CardNudge API and interactions](fluent2-card.md#cardnudge).

`Fluent.Carousel` owns a series of slide widgets and uses a bound slide key. It provides labeled steps or image previews and optional autoplay with a pause control. Open **콘텐츠와 목록 → Carousel** for both examples and their sources. [Carousel API and interactions](fluent2-carousel.md).

The `atspi-gallery-carousel` Linux client checks both Gallery examples in managed and NativeAOT builds. It covers nine external AT-SPI checks, including preview selection, wrapping, autoplay and pause. [Carousel verification command](fluent2-carousel.md).

`Fluent.NavigationBar` owns the contextual screen title, leading and trailing action slots, and an optional secondary control. It reflows at narrow widths and can use a themed, transparent or branded surface. Open **동작과 탐색 → NavigationBar** for two examples. [NavigationBar API and interactions](fluent2-navigation-bar.md).

Run the current Gallery with `./run-gallery.ps1` on Windows or `./run-gallery-linux.ps1` on WSLg. The Linux build can find installed fonts automatically. [Linux Gallery instructions](../samples/Duxel.Gallery.Linux/README.md).

Fluent menu and option popups use Shadow16. TabList moves the selection indicator and its width between tabs using a finite transition, with `UiTabListOptions.ReducedMotion` for immediate updates. Fluent Checkbox and side-label RadioGroup layouts center the first line's visible ink on the indicator.

In Linux Gallery, `x11-gallery-radio-group` checks vertical keyboard focus and single selection through X11 and AT-SPI. [RadioGroup usage and GPU comparison](fluent2-radio-group.md)

In the Gallery's SVG examples, the preview responds immediately to transform changes. Each associated C# example catches up 120 ms after the last change. See [the SVG performance notes](svg-gpu-camera.md) for the `gallery-svg-profile` diagnostic and its scope.

The Gallery's main SVG browser comparison keeps `UiSvg.UseFixedResolutionPreview` enabled by default. Zoom and rotation reuse one GPU image at the browser reference resolution even after input stops. Enable the high-resolution switch to rasterize at the final zoom after a 120 ms quiet interval. [SVG preview and timings](svg-gpu-camera.md)

The Vulkan shader skips curve searches for samples outside the conservative control-point bounds. The [SVG camera guide](svg-gpu-camera.md) records the repeatable software Vulkan timings and Windows output comparisons.

With high-resolution comparison enabled, SVG scrolling keeps the fixed-resolution GPU image active until the content stops moving for 120 ms. Final painting uses the exact visible resolution. [Scroll verification](svg-gpu-camera.md)

The Vulkan SVG shader skips material evaluation when the sample coverage mask is empty. The [SVG camera guide](svg-gpu-camera.md) records the Windows hardware GPU measurements and software Vulkan limits.

The Gallery's transform sliders start the exact-render idle timer on pointer release. Use the `gallery-svg-profile` pointer trace to check that a following gesture supersedes the pending frame. [SVG camera guide](svg-gpu-camera.md)

The Vulkan renderer bounds axis-aligned SVG blur reads using cached scene draw bounds and leaves kernel normalization unchanged. [Camera performance and filter output](svg-gpu-camera.md)

SVG path coverage reuses the pixel's camera constants. Use the Vulkan device line to distinguish hardware and CPU measurements. [Camera performance](svg-gpu-camera.md)

When timing SVG, join the GPU data by `gpuSubmission`. Use `vectorBlurGpuUs` for all blur stages and `vectorOutsideBlurGpuUs` for the remaining vector scope, which includes copies. [Timing fields](svg-gpu-camera.md)

Use `vectorPathGpuUs` and `vectorCopyGpuUs` to distinguish vector image rendering dispatches and the three profiled copy sites. [Path and transfer timings](svg-gpu-camera.md)

The Gallery's `--verify-gpu-camera` run also covers an exact 2× zoom at 37° rotation. Each camera scenario waits for both browser comparison previews to leave the fixed-resolution gesture mode before accepting the Vulkan presentation acknowledgement. The verifier checks that camera changes do not add CPU SVG rasterizations.

The external Linux `x11-gallery-svg-pixels` mode selects the actual browser comparison and captures the 1280×900 window at the default transform and at 2× zoom with 37° rotation. The .NET 11 managed and Linux x64 NativeAOT captures matched below the diagnostic header in Ubuntu WSLg with Mesa lavapipe. This compares the visible Gallery pixels across runtimes.

`UiSlider.IsPointerDragging` describes the slider's pointer capture in its latest traversal. The SVG Gallery keeps the interactive preview while any transform slider holds capture. For rotated exact output, the shared SVG renderer sizes the visible camera using the full clip chain in widget coordinates.

The `svg-replacement-pressure` test alternates 3,000×3,000 and 3,200×3,200 SVG textures on Vulkan. It checks one live texture, a frame-slot bound on retired textures pending GPU completion, and complete retirement after draining the slots. Both the Windows Graphics and Linux Vulkan test runners support this mode.

The Linux Vulkan `radio-parent-pixels` mode exercises the shared RadioGroup parent allocation test fixture with 1×/4× MSAA and frame slots 1/3. The fixed .NET 11.0.0 managed and NativeAOT outputs match in all 104 image pairs on WSLg. See [RadioGroup](fluent2-radio-group.md).

For NavigationBar repaint diagnostics on Linux, run the independent `atspi-gallery-navigation-repaint` client against the owned Gallery PID with `DUXEL_VK_PROFILE_OUT` shared by both processes and per-frame Vulkan profiling enabled. The logged repair reason distinguishes a fresh or stale acquired image from damage compared against its recorded consumers.

On Linux, the Vulkan presentation-reuse fixture checks paint-group dependency revision changes against forced full repaint. It compares raw pixel hashes between fixed .NET 11 managed and NativeAOT runs at frame slots 1/3. See [Linux Vulkan pixel contracts](../tests/Duxel.Vulkan.Linux.Tests/README.md).

The Linux Gallery `x11-gallery-provider-radio-direction` fixture uses three horizontal RadioGroup choices to verify visual arrow direction, preserved selection and native focus. The same example also checks Checkbox, Switch and Slider values. See [RadioGroup](fluent2-radio-group.md).

`Fluent.DataGrid<T>` provides fixed headers, virtualized rows, typed sorting and column-level editing. In Gallery, look under **Fluent 컨트롤 → 콘텐츠와 목록 → DataGrid**. [Fluent DataGrid](fluent2-data-grid.md)

`Dux.VirtualGrid` reuses cells in overlapping ranges and only binds cells that enter the realized range. Call `Refresh()` if the source mutates existing cells in place. Set `UiVirtualGridOptions.OwnsWheelInput` if its cell widgets have no nested scrolling. Gallery's **가상 그리드** uses this option to combine consecutive wheel steps before painting the new range.

`DuxelAppSession.ShowFileDialogAsync` opens a file, save or folder dialog and returns the paths the user selected. Gallery shows the three modes under **통합·개발 도구 → 시스템 연동 → 파일 대화 상자**. [File and folder dialogs](file-dialogs.md)

Dragging a text selection beyond the visible edge continues scrolling while the button stays down. CodeView and multiline editors scroll in both directions where text overflows. Single-line inputs scroll horizontally. Any remaining motion is passed to a containing scroll view so a CodeView that fills a longer document can keep extending its selection.
