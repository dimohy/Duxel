# Version History

This document accumulates version-by-version changes for Duxel.

## 0.2.1-preview (2026-03-25)

### Major Features

- **[Feature]** Multi-window support (modal/modeless) — `ShowModal()` blocks with owner-window disable, `ShowModalAsync()` provides the async variant, and `ShowModeless()` launches independent non-blocking windows with per-window `DuxelAppSession` lifecycle.
- **[Feature]** System tray icon support — `WindowsTrayIconHost` provides tray icon, tooltip, context menu, double-click handler, minimize-to-tray, and hide-on-close via Win32 Shell API.
- **[Feature]** Pure Vulkan P/Invoke binding layer — replaced Silk.NET Vulkan with direct `LibraryImport`-based bindings (`VulkanApi`, `VulkanStructs`, `VulkanEnums`, `VulkanHandles`, `VulkanExtensions`, `VulkanMarshaling`), fully NativeAOT-compatible.
- **[Feature]** ClearType subpixel text rendering shader — new fragment shader (`imgui_subpixel.frag`) with per-RGB-channel coverage output for DirectWrite ClearType quality on Vulkan.
- **[Feature]** Windows WIC image codec — replaced `System.Drawing.Common` with pure COM-based Windows Imaging Component decoder, supporting GIF animation compositing with proper frame disposal and alpha blending.

### Major Improvements

- **[Improvement]** Session-based application lifecycle — extracted `DuxelAppSession` from monolithic `DuxelApp.RunCore()`, enabling independent session instances with dual-thread render loop, idle frame skip, and incremental font atlas scheduling.
- **[Improvement]** Expanded window options — added configurable `MinWidth`/`MinHeight`, `Resizable`, minimize/maximize button visibility, `CenterOnScreen`/`CenterOnOwner`, owner window handle, custom icon (file/memory), and `WindowCreated` callback.
- **[Improvement]** Platform-specific entry point — FBA samples now use `DuxelWindowsApp.Run()` with `Duxel.$(platform).App` package directive instead of generic `DuxelApp.Run()`.

### Major Bug Fixes

- **[Bug]** Removed `System.Drawing.Common` dependency that blocked clean NativeAOT publishing.

### Packaging / Release

- Removed Silk.NET Vulkan and `System.Drawing.Common` package dependencies.
- Archived experimental layer texture cache backend (`UiLayerCacheBackend.Texture`).
- Removed built-in demo windows (`UiImmediateContext.DemoWindows.cs`).
- Bumped package version to `0.2.1-preview` (`Duxel.App`, `Duxel.Windows.App`).

## 0.2.0-preview (2026-03-09)

### Major Features

- **[Feature]** Expanded the showcase/sample surface — `samples/fba/all_features.cs` was broadened into a richer end-to-end workspace with dedicated windows for layout, typography, popup/context patterns, selection/status tools, markdown editing/viewing, and built-in demo coverage.
- **[Feature]** Added richer extension points at both API and sample level — instance-based custom widget composition (`IUiCustomWidget`), markdown editor/viewer widgets, animated image playback plumbing, and the supporting docs/samples were aligned so advanced UI composition is easier to discover and validate.

### Major Improvements

- **[Improvement]** Refined showcase layout and presentation — regular tool windows align below the menu again, compact utility windows center appropriately, compact hero sections measure wrapped text correctly, and nested child/column layouts now use child-local sizing more reliably.
- **[Improvement]** Polished built-in demo behavior — the built-in `Closable Window` now opens in a more appropriate compact position/size and behaves consistently with the rest of the demo window set.
- **[Improvement]** Synchronized release-facing documentation — updated README links, refreshed FBA guide sync markers, expanded custom widget docs, and aligned package/version metadata for the 0.2.0-preview release.

### Major Bug Fixes

- **[Bug]** Fixed multiline and markdown line-ending handling — CRLF, LF, and CR inputs are normalized so editors no longer accumulate hidden carriage-return artifacts.
- **[Bug]** Fixed Korean IME responsiveness — Windows IME updates now wake frames immediately and prefer live composition text so typing appears without bursty lag.
- **[Bug]** Fixed showcase layout regressions — resolved row text clipping, compact hero overlap, unnecessary child/column width confusion, and related presentation issues uncovered while expanding the sample workspace.
- **[Bug]** Fixed same-line row-height propagation at the library level — mixed-height inline items now carry the tallest row height through `SameLine`, `NewLine`, columns, and tables so follow-up content no longer overlaps or advances to the wrong Y position.
- **[Bug]** Fixed top-most window behavior at the library level — implemented real persistent top-most semantics so the built-in `Closable Window` stays above normal windows without click-through or broken stacking behavior.

### Packaging / Release

- Bumped package version to `0.2.0-preview` (`Duxel.App`, `Duxel.Windows.App`, `Duxel.Core`, `Duxel.Vulkan`, `Duxel.Platform.Windows`).

## 0.1.15-preview (2026-03-05)

### Major Features

- **[Feature]** Introduced the platform text rasterization abstraction — `IPlatformTextBackend` and its request/result contracts decouple cross-platform text rasterization from the atlas pipeline.
- **[Feature]** Added the DWrite text-run backend and base font-size control — `WindowsPlatformTextBackend` now rasterizes mixed-script text per font run, and `SetDirectTextBaseFontSize` lets the DWrite em-size stay independent from line height.

### Major Improvements

- **[Improvement]** Migrated the widget text stack onto the DWrite-aware path — core widgets now share `MeasureTextInternal` / `AddTextInternal`, while run-level rasterization replaces per-glyph COM traffic.
- **[Improvement]** Consolidated text/cache maintenance and diagnostics — direct-text pre-caching, lower-allocation cache trimming, atlas disk-cache toggles, atlas/Vulkan font diagnostics, and codepoint signature/snapshot tracking now work together as one maintainable pipeline.

### Major Bug Fixes

- **[Bug]** Fixed DWrite correctness end to end — resolved Korean text loss on whitespace runs, corrected base em-size usage, and centered rasterized text within measured line height.
- **[Bug]** Fixed GPU text resource reliability — separated dynamic-atlas/DWrite texture ID ranges, reordered staging uploads to avoid fence/write races, and hardened texture data size normalization.
- **[Bug]** Fixed swapchain recreation failure handling — `TryRecreateSwapchain()` now returns failure cleanly instead of cascading Vulkan errors after surface loss.

### Packaging / Release

- Bumped NuGet package version to `0.1.15-preview` (all packages: `Duxel.App`, `Duxel.Windows.App`, `Duxel.Core`, `Duxel.Vulkan`, `Duxel.Platform.Windows`).

## 0.1.14-preview (2026-02-28)

### Major Improvements

- **[Improvement]** Reduced shutdown/render-loop churn — the render loop now exits earlier when `ShouldClose` / `stopRequested` is observed, preventing extra frames during teardown.

### Major Bug Fixes

- **[Bug]** Fixed Hangul fallback and DWrite atlas selection — Korean codepoints can reach secondary fonts again, and `DUXEL_DIRECT_TEXT=0` no longer downgrades the atlas rasterizer away from DWrite.
- **[Bug]** Fixed dynamic atlas cache consistency as one reliability pass — stale atlas reuse, fuzzy cached-size matching, mid-frame codepoint drift, and missing invalidation on new glyph discovery were all removed so each font size/codepoint snapshot resolves deterministically.
- **[Bug]** Fixed shutdown-time Vulkan surface-loss handling — swapchain recreation now fails safely and close-time render-thread errors no longer cascade into crashes.

### Packaging / Release

- Bumped NuGet package version to `0.1.14-preview` (`Duxel.App`, `Duxel.Windows.App`).

## Documentation Update (2026-02-26)

### Major Improvements

- **[Improvement]** Reworked the documentation surface as one coordinated pass — refreshed the English/Korean READMEs, rewrote the UI DSL guide against the current parser/runtime, synchronized the FBA guides, and added sync timestamps to clarify document freshness.

## 0.1.13-preview (2026-02-20)

### Major Bug Fixes

- **[Bug]** Fixed FBA compatibility for `net10.0` consumers — retargeted `Duxel.Windows.App` and `Duxel.Platform.Windows` from `net10.0-windows` to `net10.0`, removing NU1202 failures while preserving future cross-platform FBA validation paths.

### Packaging / Release

- Bumped NuGet package version to `0.1.13-preview` (`Duxel.App`, `Duxel.Windows.App`).

## 0.1.12-preview (2026-02-20)

### Major Features

- **[Feature]** Added the first full DirectWrite text pipeline — new DWrite rasterizer, runtime direct-text toggle APIs, environment-variable control, and text cache management.
- **[Feature]** Completed the Windows platform/backend separation — introduced `WindowsPlatformBackend` and fully removed the GLFW platform path.
- **[Feature]** Added foundational UI runtime building blocks — animation tracks, runtime font-size control, layout/alignment helpers, icon rendering, and promoted canvas/overlay/card helper APIs for reuse.
- **[Feature]** Added richer Windows-focused showcase apps — calculator-style FBA samples now demonstrate translucent surfaces, FX interactions, and multi-base/RPN scenarios.

### Major Improvements

- **[Improvement]** Unified core widget and platform behavior — widget APIs gained explicit IDs where needed, IME handling was stabilized, and the runtime surface became easier to compose consistently.
- **[Improvement]** Moved sample and benchmark boilerplate into reusable library APIs — more than 10 FBA samples now share common helpers instead of duplicating FPS, overlay, parsing, and card-rendering logic.
- **[Improvement]** Verified measurable Direct Text gains — the ON/OFF A/B benchmark confirmed an average FPS improvement of about 5.87% (375→397).

### Packaging / Release

- Bumped NuGet package version to `0.1.12-preview` (`Duxel.App`, `Duxel.Windows.App`).

## 0.1.11-preview (2026-02-17)

### Major Improvements

- **[Improvement]** Standardized performance experimentation around global static cache and layer dirty strategy — benchmark samples now compare static/dynamic invalidation paths more reproducibly, and hot-path trials retain validated wins while rolling back measured regressions.
- **[Improvement]** Strengthened benchmarking automation and logging as one workflow — clip/layer A/B scripts gained timeout/process cleanup, repeated perf comparison reports now summarize averages/variance/improvement, and optimization sessions became easier to trace.

### Packaging / Release

- Bumped NuGet package version to `0.1.11-preview` (`Duxel.App`, `Duxel.Windows.App`).

## 0.1.10-preview (2026-02-15)

### Major Improvements

- **[Improvement]** Hardened Vulkan layer-cache tag handling — texture-compose reuse now survives opacity suffixes, and backend/opacity combinations were revalidated more consistently.
- **[Improvement]** Expanded benchmark controls for rendering regressions — fixed-opacity validation knobs and richer collision-response dynamics made performance samples more useful for repeatable comparisons.

### Packaging / Release

- Bumped NuGet package version to `0.1.10-preview` (`Duxel.App`, `Duxel.Windows.App`).

## 0.1.9-preview (2026-02-15)

### Major Improvements

- **[Improvement]** Simplified the distribution story — package descriptions were refreshed and NuGet delivery stayed focused on `Duxel.App` and `Duxel.Windows.App`.
- **[Improvement]** Simplified the sample surface around DSL validation — only `samples/Duxel.Sample` remains, older sample projects were removed, and FBA package directives were standardized on `Duxel.Windows.App`.
- **[Improvement]** Cleaned the surrounding docs to match the reduced sample/package surface — README tables, removed-sample references, ImGui design docs, and `docs/todo.md` were reorganized together.

## 0.1.8-preview (2026-02-15)

### Major Features

- **[Feature]** Simplified package distribution into a two-package model — `Duxel.App` and `Duxel.Windows.App` became the public delivery surface while lower-level packages were bundled underneath.
- **[Feature]** Added platform injection hooks to `Duxel.App` — key repeat, clipboard, and IME services can now be supplied through options instead of hard Windows dependencies.
- **[Feature]** Shipped DSL source generation through the app package — `Duxel.Core.Dsl.Generator` is included as an analyzer so source generation works from a single package install.

### Major Improvements

- **[Improvement]** Reduced Windows coupling in the app layer and documentation surface — direct Windows service references were removed from the app package, and cumulative history was moved out of `README.md` into this dedicated document.

---

## 0.1.7-preview

### Major Features

- **[Feature]** Added platform-neutral image APIs and runtime Windows registration — `UiImageTexture`, `UiImageEffects`, and `IUiImageDecoder` decouple image support from Windows specifics while `Duxel.App` wires the Windows decoder at runtime.
- **[Feature]** Extended the FBA image showcase — the sample now supports web image source selection and GIF frame animation playback.

### Major Improvements

- **[Improvement]** Improved Vulkan AA toggle handling and validation workflows — runtime TAA/FXAA switching reconfigures resources more safely, and MSAA/FXAA comparison procedures became more repeatable.
- **[Improvement]** Refined collapse/expand UI presentation — the collapsed state preserves a small body peek without allowing canvas overflow.
