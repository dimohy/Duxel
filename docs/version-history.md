# Version History

This document accumulates version-by-version changes for Duxel.

## 0.1.15-preview (2026-03-05)

## 0.2.0-preview (2026-03-09)

### Changes

- **[Feature]** Expanded `samples/fba/all_features.cs` into a fuller showcase workspace — added dedicated windows for typography, layout, popup/context patterns, input queries, item status, multi-selection, layer/animation preview, markdown studio, and richer built-in tool coverage.
- **[Improvement]** Refined showcase presentation and layout helpers — normalized menu-below window placement, centered compact utility windows, improved compact hero layout stability, and refreshed README documentation links for design and optimization references.
- **[Improvement]** Synchronized release documentation for the 0.2.0-preview rollout — updated README/guide metadata, refreshed paired FBA guide sync markers, and recorded the current showcase scope in the version history.
- **[Bug]** Fixed multiline and markdown line-ending handling — normalized CRLF/LF/CR input paths so multiline text boxes and markdown editing remain stable without hidden carriage-return artifacts.
- **[Bug]** Fixed IME composition latency for Korean typing — Windows IME messages now request frames immediately and live composition text is prioritized so characters appear without bursty delayed updates.
- **[Bug]** Fixed immediate UI layout regressions in showcase/library interactions — resolved text row clipping, child-local columns sizing, compact hero overlap, and added true top-most window semantics so the built-in `Closable Window` stays above normal windows without click-through.

### Packaging / Release

- Bumped package version to `0.2.0-preview` (`Duxel.App`, `Duxel.Windows.App`, `Duxel.Core`, `Duxel.Vulkan`, `Duxel.Platform.Windows`).

## 0.1.15-preview (2026-03-05)

### Changes

- **[Feature]** Added platform text backend abstraction — `IPlatformTextBackend` / `PlatformTextRasterizeRequest` / `PlatformTextRasterizeResult` interfaces for cross-platform text rasterization decoupled from the atlas pipeline.
- **[Feature]** Added DWrite text-run rasterization backend — `WindowsPlatformTextBackend` rasterizes entire font runs with a single DWrite COM call per run (vs. per-glyph), with `BuildFontRuns` for mixed-script (e.g. Latin+Hangul) text splitting.
- **[Feature]** Added `SetDirectTextBaseFontSize` API — new setter on `UiContext` / `UiImmediateContext` to control the DWrite base em-size independently of line height, wired from `DuxelFontOptions.FontSize`.
- **[Improvement]** Migrated all widget text rendering to DWrite direct-text path — every widget (Button, Tree, Tab, Table, Menu, Slider, Input, ListBox, Selectable, Separator, Tooltip, Combo, Drag) now uses `MeasureTextInternal` / `AddTextInternal` which automatically leverages DWrite when available.
- **[Improvement]** Eliminated double-rasterization in DWrite text path — `TryMeasureDirectText` now pre-caches the rasterized result so that `TryRenderDirectText` always hits the cache.
- **[Improvement]** Replaced per-glyph DWrite rasterization with text-run API — single COM call per font run instead of per-glyph, significantly reducing COM overhead.
- **[Improvement]** Reduced allocations in `TrimDirectTextCache` — replaced `List<>` with fixed arrays and added `hasStale` early-exit check to avoid iteration when no entries are stale.
- **[Improvement]** Added font atlas disk cache toggle — `DUXEL_FONT_DISK_CACHE` environment variable to enable/disable font atlas serialization.
- **[Improvement]** Added font atlas diagnostics — `DUXEL_FONT_ATLAS_DIAG`, `DUXEL_FONT_ATLAS_DIAG_LOG`, `DUXEL_FONT_ATLAS_DUMP_DIR` environment variables for atlas build tracing and texture dump.
- **[Improvement]** Added Vulkan font command diagnostics — `DUXEL_VK_FONT_CMD_DIAG`, `DUXEL_VK_FONT_CMD_DIAG_LOG`, `DUXEL_VK_FONT_BOUNDS_ASSERT` environment variables for font texture command tracing and bounds validation.
- **[Improvement]** Added `CodepointSignature` to `UiFontResource` — FNV-1a hash of atlas pixel data for cache invalidation when codepoint set changes.
- **[Improvement]** Added per-frame frozen codepoint snapshot — `frameCodepointSnapshot` prevents mid-frame codepoint drift from `OnMissingGlyph` mutating the active set.
- **[Bug]** Fixed texture ID collision between dynamic atlas and DWrite text — separated ID ranges (dynamic atlas from `1_100_000_000`, DWrite text from `2_100_000_000`).
- **[Bug]** Fixed staging buffer data race in `VulkanRendererBackend.UploadTextureData` — reordered so fence wait completes before host memory write.
- **[Bug]** Fixed Korean text not displaying with text-run API — whitespace in `BuildFontRuns` no longer triggers a font switch, preventing empty alpha bounds on whitespace-only runs.
- **[Bug]** Fixed DWrite base font size using `LineHeight` (~21px) instead of actual build font size (16px) — `_directTextBaseFontSize` now stores the correct em-size.
- **[Bug]** Fixed DWrite text vertical centering — added Y offset to center the rasterized bitmap within the measured line height.
- **[Bug]** Fixed `TryRecreateSwapchain` surface-lost handling — `RecreateSwapchain()` replaced with `TryRecreateSwapchain()` that returns `false` on failure, preventing cascading Vulkan errors.
- **[Bug]** Fixed normalized staging buffer size validation — `GetExpectedTextureDataSize` computes exact byte count per format, and `UploadTextureData` pads undersized pixel buffers instead of crashing.

### Packaging / Release

- Bumped NuGet package version to `0.1.15-preview` (all packages: `Duxel.App`, `Duxel.Windows.App`, `Duxel.Core`, `Duxel.Vulkan`, `Duxel.Platform.Windows`).

## 0.1.14-preview (2026-02-28)

### Changes

- **[Bug]** Fixed Hangul fallback font blocked — `UiTextBuilder` had `IsHangulCodepoint()` guard that prevented secondary font lookup (e.g. `malgun.ttf`) for Korean codepoints, causing broken glyphs when the primary font lacked Hangul coverage.
- **[Bug]** Fixed DWrite atlas rasterizer disabled by `DUXEL_DIRECT_TEXT=0` — previously the environment variable switched both the direct-text rendering path **and** the atlas glyph rasterizer to software TTF, losing hinting and producing low-quality Hangul at small sizes. Now `DUXEL_DIRECT_TEXT` only controls the direct-text path; atlas rasterizer always uses DWrite on Windows (explicit `DUXEL_ENABLE_TTF_GLYPH_RASTERIZER=1` to opt in to TTF).
- **[Bug]** Fixed stale dynamic font atlas reuse — `ResolveDynamicFontResource` returned cached atlases even when the codepoint set had grown (e.g. 109 → 115 glyphs), causing missing Hangul characters at certain sizes. Added `CodepointSignature` to `UiFontResource` for cache validation.
- **[Bug]** Fixed `SelectClosestCachedFontSize` size mismatch — a 10% threshold allowed reuse of a 58px atlas for 64px requests, causing glyph metric/UV misalignment. Removed the fuzzy-match logic; each integer size now always builds its own atlas.
- **[Bug]** Fixed mid-frame codepoint drift — `OnMissingGlyph` could mutate `activeCodepointSet` between `PushFontSize` calls within the same frame, causing atlas inconsistency. Introduced per-frame frozen `frameCodepointSnapshot` taken once at frame start.
- **[Bug]** Fixed dynamic font cache not invalidated on new glyphs — added `InvalidateDynamicFontResourceCache()` that destroys stale textures whenever `pendingGlyphs` grows or renderer reports missing glyphs.
- **[Bug]** Fixed Vulkan `ErrorSurfaceLostKhr` crash on window close — `RecreateSwapchain()` replaced with `TryRecreateSwapchain()` that catches surface-lost exceptions gracefully; render thread wrapped in `try/catch` for shutdown-time Vulkan errors.
- **[Improvement]** Added early render loop break on `ShouldClose`/`stopRequested` to prevent extra frames after window close signal.

### Packaging / Release

- Bumped NuGet package version to `0.1.14-preview` (`Duxel.App`, `Duxel.Windows.App`).

## Documentation Update (2026-02-26)

### Changes

- **[Improvement]** Reworked `README.md` as English-first and added `README.ko.md`.
- **[Improvement]** Rewrote `docs/ui-dsl.md` based on current parser/runtime (`UiDslParser`, `UiDslWidgetDispatcher`, `UiDslPipeline`).
- **[Improvement]** Synchronized FBA guide docs (`docs/getting-started-fba.md`, `docs/fba-reference-guide.md`, `docs/fba-run-samples.md`) to the current sample directive (`Duxel.$(platform).App`).
- **[Improvement]** Added synchronization timestamps to major docs for clearer update baselines.

## 0.1.13-preview (2026-02-20)

### Changes

- **[Bug]** Changed `Duxel.Windows.App`/`Duxel.Platform.Windows` target framework from `net10.0-windows` to `net10.0` to resolve NU1202 compatibility errors in `net10.0` FBA runs and preserve future Linux cross-platform FBA test paths.

### Packaging / Release

- Bumped NuGet package version to `0.1.13-preview` (`Duxel.App`, `Duxel.Windows.App`).

## 0.1.12-preview (2026-02-20)

### Changes

- **[Feature]** Added DirectWrite text rendering system — new `WindowsDirectWriteGlyphRasterizer`, runtime direct-text toggle API (`SetDirectTextEnabled`/`GetDirectTextEnabled`), `DUXEL_DIRECT_TEXT` environment variable support, and text cache management (LRU 256 entries).
- **[Feature]** Completed Windows platform backend separation — new `WindowsPlatformBackend` (975 lines), full removal of GLFW platform (`Duxel.Platform.Glfw`).
- **[Feature]** Added immediate-mode animation framework — `AnimateFloat` API (easing such as OutCubic), continuous render request via `RequestFrame`, and animation track state management.
- **[Feature]** Added runtime font-size control APIs — `PushFontSize`/`PopFontSize`, `fontSize` parameter on `DrawTextAligned`, and font atlas rasterizer split (`UiFontAtlas.Rasterizers.cs`).
- **[Feature]** Promoted many widget/benchmark helper APIs — `BeginWindowCanvas`/`EndWindowCanvas`, `DrawOverlayText`, `UiFpsCounter`, `DrawKeyValueRow`, `BenchOptions`, `DrawLayerCardSkeleton`/`DrawLayerCard`/`DrawLayerCardInteractive` (`UiLayerCardInteraction` struct).
- **[Feature]** Extended layout system — `EnableRootViewportContentLayout`, `AlignRect`, `SetNextItemVerticalAlign`, vertical alignment support for `SameLine`.
- **[Feature]** Added icon system — built-in icon rendering in `UiImmediateContext.Icons`.
- **[Feature]** Added Windows calculator FBA — cyber backdrop/ripple/FX button/translucent UI showcase (`windows_calculator_fba.cs`) and RPN trace/multi-base showcase (`windows_calculator_duxel_showcase_fba.cs`).
- **[Improvement]** Unified widget API signatures — added `string? id` parameters to Combo/ListBox/Table/Tree to prevent ID collisions.
- **[Improvement]** Improved IME handling stability — refactored `WindowsImeHandler`.
- **[Improvement]** Switched 10+ FBA samples from boilerplate (FPS/overlay/bench parser/card rendering) to library APIs, significantly simplifying code.
- **[Improvement]** Verified average +5.87% FPS improvement in Direct Text ON/OFF A/B benchmark (375→397 FPS).

### Packaging / Release

- Bumped NuGet package version to `0.1.12-preview` (`Duxel.App`, `Duxel.Windows.App`).

## 0.1.11-preview (2026-02-17)

### Performance Highlights

- Applied global static cache strategy (`duxel.global.static:*`) to benchmark samples, reducing static background regeneration cost and documenting reproducible differences against all-dynamic rendering.
- Validated layer dirty strategy as `all` vs `single`, confirming improved cache rebuild count and FPS when invalidation scope is reduced.
- Ran hot-path experiments for text/layer/clip paths; retained valid optimizations and immediately rolled back attempts with measured regressions.

### Benchmark & Measurement

- Improved long-run stability of clip clamp A/B automation (`scripts/run-vector-clip-ab.ps1`, `scripts/run-layer-widget-clip-ab.ps1`) with timeout/process cleanup.
- Added repeated performance comparison automation (`scripts/run-duxel-perf-ab.ps1`) to standardize baseline/candidate averages, variance, and improvement rates.
- Strengthened performance logging policy and session logs for traceable change-validation-result records.

### Packaging / Release

- Bumped NuGet package version to `0.1.11-preview` (`Duxel.App`, `Duxel.Windows.App`).

## 0.1.10-preview (2026-02-15)

### Rendering / Layer Cache

- Improved static tag detection for Vulkan texture layers so texture compose reuse works correctly even when opacity suffix (`:oXXXXXXXX`) is present.
- Revalidated reused-tag consistency for backend/opacity combinations in layer static-cache checks and documented regression points.

### Samples / Bench

- Added `DUXEL_LAYER_BENCH_OPACITY` environment variable to `samples/fba/idle_layer_validation.cs` for fixed-opacity regression benchmark automation.
- Extended collision response model in `samples/fba/Duxel_perf_test_fba.cs` so angular velocity/rotation direction also react on impacts (impulse + damping).

### Packaging / NuGet

- Bumped NuGet package version to `0.1.10-preview` (`Duxel.App`, `Duxel.Windows.App`).

## 0.1.9-preview (2026-02-15)

### Packaging / NuGet

- Updated package descriptions for `Duxel.App` and `Duxel.Windows.App` to reflect the current distribution structure.
- Kept NuGet distribution to two packages only (`Duxel.App`, `Duxel.Windows.App` at 0.1.9-preview).

### Samples

- Simplified project samples to DSL-focused validation with only `samples/Duxel.Sample` retained.
- Removed: `samples/Duxel.PerfTest`, `samples/Duxel.Sample.NativeAot`.
- Switched FBA sample package directives to `Duxel.Windows.App` baseline.

### Documentation

- Updated README project sample table and build/distribution guidance to match the current sample structure.
- Cleaned references to removed samples in related docs (`docs/ui-dsl.ko.md`, `docs/getting-started-fba.ko.md`).
- Consolidated ImGui-related split docs into `docs/design.ko.md` and removed `docs/imgui-coverage.md`.
- Reorganized `docs/todo.md` into a remaining-work-only document by removing completed items.

## 0.1.8-preview (2026-02-15)

### Packaging / Distribution

- Simplified package distribution strategy to two packages: `Duxel.App` and `Duxel.Windows.App`.
- Stopped standalone NuGet distribution for `Duxel.Core`, `Duxel.Vulkan`, `Duxel.Platform.Windows`; bundled them into upper-level packages.
- Included `Duxel.Platform.Windows` inside `Duxel.Windows.App`, so Windows app users can install a single package.

### Architecture

- Removed direct Windows dependencies from `Duxel.App` (`WindowsClipboard`, `WindowsImeHandler`, `WindowsUiImageDecoder`, `WindowsKeyRepeatSettingsProvider`).
- Added option hooks for platform-specific injection:
	- `KeyRepeatSettingsProvider`
	- `ClipboardFactory`
	- `ImeHandlerFactory`

### DSL / Source Generator

- Included `Duxel.Core.Dsl.Generator` as an analyzer in `Duxel.App` (`analyzers/dotnet/cs`) so source generation works from a single package installation.

### Documentation

- Kept only latest-version highlights in `README.md` and moved cumulative history to this document.

---

## 0.1.7-preview

### Rendering / Performance

- Improved TAA/FXAA toggle path in Vulkan backend and made runtime AA switching safely reconfigure resources/pipelines.
- Refined performance samples/checklists for repeatable MSAA/FXAA comparison experiments.

### Core / Platform

- Added platform-neutral image APIs to `Duxel.Core` (`UiImageTexture`, `UiImageEffects`, `IUiImageDecoder`).
- Split Windows-specific decoder into `Duxel.Platform.Windows` and registered it at runtime in `Duxel.App`, removing platform dependency from Core.

### Samples / UI

- Added web image source selection (PNG/JPG/GIF) and GIF frame animation playback to FBA image sample.
- Adjusted collapse/expand UI behavior to keep a 3px body peek when collapsed while preventing canvas overflow.
