# SVG image and widget goal

Current widget rendering: [GPU scene and camera](svg-gpu-camera.md). CPU Rasterize/Decode remains explicit bitmap export; widget zoom retains GPU vectors. Complex pixel conformance is still in progress.

Last synced: 2026-11-14

The current backend uses managed SVG geometry and rasterization, DirectWrite/WIC on Windows, and HarfBuzz/GdkPixbuf on Linux. Skia and Svg.Skia have been removed. Windows SVG114, GPU76, Observer288 CPU/7 GPU, and platform Windows18/Linux17 checks pass. See artifacts/svg-managed-2026-11-14/verification.json and [platform rendering](svg-platform-rendering.md). Complete SVG/native/AOT qualification remains open; the older checkpoints below describe the previous backend.

The user expanded the existing active goal with two required routes: accept SVG wherever ordinary bitmap files are loaded, and provide SVG-backed icons plus a dedicated zoomable/rotatable widget or an equivalent reusable API. Both routes must share interpretation/resource ownership and preserve the original engine/widget/container/integration/portability/Fluent scope. The scope authority is artifacts/svg-support-2026-11-11/scope.json.

The optional Duxel.Svg module implements the shared asset/raster layer. Core provides UiSvg with zoom/rotation/offset/tint, and UiSvgIcon adapters for menus/tabs. The actual Observer SVG tab exposes both image routes and icons. Existing bundled Fluent icons remain TTF glyphs: cached raster textures in ordinary2D, font-outline meshes in3D layers. SVG scenes are retained and rasterized at the requested resolution, then drawn as textured GPU quads. Fluent Image remains an unconnected catalog component.

## SVG icons and execution

Observer controls use1px translation,1° rotation and1 percentage-point zoom steps. Pointer input and Left/Right arrows share those steps; the underlying UiSvg properties continue to accept finite floating-point values. ShowStepMarkers is false for these controls so fine steps preserve a continuous visible rail.

```csharp
using var icon = new UiSvgIcon(store.Load("regular.svg"), store.Load("filled.svg"),
    UiSvgIconColorMode.MultiplyForeground);
var option = new UiTabOption("home", "Home", Icon : icon.TabIcon);
var action = new UiMenuOption("open", "Open") { Icon = icon.MenuIcon };
```

The default Authored mode preserves SVG colors. MultiplyForeground explicitly multiplies authored RGBA by control foreground; white SVG geometry works as a themed monochrome icon. It does not replace fills/currentColor. Menu hover selects filled only when enabled; tabs select filled from selection independently of hover. Painters do not advance layout, share the widget's texture/cache path, and wake event-wait rendering on source changes. Dispose the icon after its consumers and before its store.

Run `./run-gallery.ps1` from the repository root, then select SVG. Compare ordinary image input with the dedicated widget, drag zoom/rotation/translation, reset, reload and use icon menus/tabs. The original assets are samples/Duxel.Gallery/Assets/Svg; runtime reads output copies as explained in the paired sample README. No application preview was launched by this qualification; the actual page was rendered through the owned GPU test fixture.

Earlier observer/icon checkpoint:54 asset/widget/image checks; observer113 including11 icon contracts and real pointer slider/reset/reload/menu paths; actual GPU32 at MSAA1/4 and observer-body reuse3; existing menu376/tab197/scheduling35 and menu GPU216 regressions. Three final builds (SVG tests, Graphics tests, actual Observer app) have zero warnings/errors. Artifacts: artifacts/svg-support-2026-11-11/observer-verification.json. That checkpoint predates explicit font support described below; the full feature corpus, device-lifetime/deployment/NativeAOT and performance benchmarks remain open.

GPU tests exposed and fixed three image contracts: Image/ImageWithBg/ImageButton now map full UVs; ordinary image effects preserve source alpha and immutable queued pixels; UiTextureUpdate.IsColorImage explicitly prevents SVG/runtime image IDs being classified as font coverage masks. UiImageTexture and SVG set it automatically. Default bitmap preparation reuses immutable source memory, while changed RGB effects create separate snapshots. Font coverage behavior is retained for existing font resources and checked by actual menu glyph regressions.

## Document features and the Observer example

Run `./run-gallery.ps1` → SVG → **문서 기능**. This actual tab panel shows a repeating pattern, radial gradient, a currentColor symbol reused through use, a cutout mask and an endpoint arrow marker. **이미지·텍스트** returns to the ordinary-image/widget comparison. Both panels retain their resources and share the1px/1°/1% controls; switching panels requests a frame without creating an animation timer. The five original assets live in the sample's Assets/Svg directory.

Focused CPU pixel fixtures additionally cover legacy xlink use and embedded PNG straight alpha. Known interior/exterior pixels and declared tile periods are checked independently of the renderer's own output. MSAA1/4 GPU checks cover symbol viewport/currentColor, periodic pattern transparency, masked geometry and unchanged acquired-image replay. These are explicit tested cases, not a full SVG specification compliance claim.

CSS resource checks now recognize tokens rather than matching arbitrary substrings: comments and quoted property text can contain documentation such as `url(...)`, `@import` or `@font-face` without being treated as live resources. Actual URL/at-rule tokens, including escaped names, still use the existing local-fragment/embedded-raster policy. Malformed URL/string/comment/escape input fails explicitly. The scanner validates resource references; it is not a replacement CSS renderer or proof of full CSS support.

Document-feature checkpoint: CPU102, Observer123, SVG GPU52 and actual Observer GPU7 checks; the feature panel is selected through real pointer input and replayed. See artifacts/svg-support-2026-11-11/features-verification.json. Earlier font/lifetime results remain historical checkpoints. Complete feature/script coverage and native/deployment/AOT qualification remain open.

## Explicit SVG fonts and retained text

### Referenced characters on a path

SVG1.1 tref is compiled once to the equivalent span of referenced character data described by the [W3C text specification](https://www.w3.org/TR/SVG11/text.html#TRefElement). This includes tref nested in textPath, which the pinned backend otherwise renders empty. The compiler uses the already parsed XML DOM and the backend's typed clone API, retaining the original element name, ID, class, styles and positioning. It does not introduce another XML parse, renderer or per-frame reference lookup.

Both href and xlink:href accept resolved local fragments. Target markup supplies characters, while the referring element supplies presentation; entity-decoded characters and neighboring text retain their order. Source Reload recompiles changed referenced content transactionally. The sample's curved line in SVG → 이미지·텍스트 now reads from a reusable text definition in text.svg.

Focused tests compare plain, nested, CSS-styled/positioned and curved references with authored literals/spans, including competing tref/tspan selectors. Bitmap decoding,100 replays, enlarged raster and target Reload share the resource path. Current CPU112, Observer123, SVG GPU52 and Observer GPU7 pass. The actual reference-sample body is pixel-identical to the previous literal sample; only variable FPS header text differs. Evidence: artifacts/svg-support-2026-11-11/tref-verification.json. Full SVG/CSS/script/native/AOT qualification remains open.

Register font files when creating the store. Family names below are application aliases; style/weight come from each actual font face. Register a separate bold/italic face when the document requests that style.

```csharp
using var store = new SvgStore(fonts : new SvgFontOptions("UI",
[
    new SvgFontFace("UI", "Assets/Fonts/ui-regular.ttf"),
    new SvgFontFace("UI", "Assets/Fonts/ui-bold.ttf"),
    new SvgFontFace("Korean", "Assets/Fonts/korean.ttf")
]));
var document = store.Load("label.svg");
using var label = new UiSvg(document) { Zoom = 2, RotationDegrees = 15 };
var fonts = store.FontStatistics;
```

Use `font-family="UI, Korean"` in SVG or CSS. Without an authored family the configured DefaultFamily is inherited from the root. Ordered registered families select a face that covers each grapheme cluster; unresolved family/style or missing glyph coverage throws before publication. There is no implicit installed-font substitution or synthetic style. `text`, positioned/styled `tspan` and `textPath` use the backend text shaping path. Mixed Latin/Korean CSS text is checked against explicit per-font tspans. The Arabic RTL case checks rendering, not complete bidi/script conformance.

The store snapshots file bytes once at construction, sharing the same file/collection face across aliases. CollectionIndex defaults to0; MaximumFontBytes defaults to32MiB per file. Changing the original font file does not mutate existing scenes. Create a new store to replace font snapshots, then dispose old consumers before their store. DTDs, scripts, external font URLs, document-embedded SVG fonts and `@font-face` rules remain unsupported and fail explicitly. Text without SvgFontOptions is rejected.

Text is compiled into the retained scene once per SVG revision. Replay and new raster sizes do not repeat the XML parse, registered-face resolution or glyph-coverage checks. Native drawing can still rasterize glyphs; FontStatistics does not measure all native text work. LoadedFaces counts unique registered file/collection faces, TypefaceRequests counts provider calls and GlyphChecks counts uncached coverage queries. None is an FPS or total-memory metric.

Observer → SVG now includes text.svg with regular/bold Latin, Korean and curved text; the same1px/1°/1% controls transform the image and text widgets. Source assets and font counters are visible; scroll the child page to its explanatory hint if needed. This Windows sample explicitly registers Segoe UI regular/bold and Malgun Gothic from the OS Fonts directory; those files are not bundled or redistributed. Portable apps supply their own licensed font paths.

Earlier font checkpoint:74 asset/widget/image/font checks,120 Observer checks,44 SVG GPU checks at MSAA1/4 and3 actual Observer body-replay checks. The GPU font comparison covers the full viewport against the standard SVG engine using the same explicit face, with RGB tolerance1; it is integration evidence, not an independent SVG conformance suite. See artifacts/svg-support-2026-11-11/font-verification.json. Full text/script corpus, variable fonts, native memory/lifetime, packaging and NativeAOT remain unqualified.

## Retained widget

### Disposal and stale resource references

Disposing the store closes its retained native pictures and registered font/typeface handles. A stale borrowed SvgAsset rejects use and no longer roots its disposed backend owner or the owner's render caches. Reload closes the retired picture after the new scene is committed; a failed reload keeps the committed picture and font handles alive. Already published raster snapshots remain valid immutable pixel buffers.

The lifetime regression retains stale asset/scene references deliberately, checks native handle closure, and uses a forced-GC reachability check for the backend owner. It also checks32 successful text reloads, failed reload and repeated disposal. This proves the tested ownership boundaries; it does not measure real workload memory savings or certify every native backend allocation/device teardown. Evidence: artifacts/svg-support-2026-11-11/lifetime-verification.json. The lifetime checkpoint CPU suite has81 checks; earlier font74 is a preserved checkpoint.

```csharp
using var view = new UiSvg(store.Load("icon.svg"))
{
    Size = new(240, 180),
    Zoom = 2,
    RotationDegrees = 45,
    Offset = new(8, 0)
};
// Inside UiScreen.Render:
view.Render(ui);
var uploads = ui.SvgTextureStatistics;
```

Size is the logical viewport; its default uses intrinsic dimensions. Parent constraints affect measurement and actual placement. Zoom changes displayed size around the viewport center, clipped to the viewport. Positive rotation is clockwise. Offset translates the image; RasterScale changes quality without changing geometry. Pixel resolution uses size × Zoom × RasterScale × ContentScale, rounded up. Tint multiplies authored RGBA; white preserves colors, without replacing currentColor.

Widgets borrow the document and subscribe to explicit reload notifications. Dispose widgets before the store. Context-specific leases share one texture per document identity/revision/pixel size and pin live dependencies. Rotation, offset, tint and unchanged frames request no additional raster/upload. Returning to an unevicted size reuses its texture. Different contexts own independent GPU identities while sharing CPU scenes/rasters. No periodic timer or animation request is created.

The context cache allows up to 128 entries and 64 MiB of logical RGBA residency. Under pressure it can replace an SVG widget's previous texture if that widget owns the only lease and neither the current frame nor a retained layer uses it. The replacement source is prepared before retirement is queued, and previously published frames retain their original texture identity. Shared and retained entries still count against the budget. Disposal releases the widget's leases. The renderer owns native retirement, which can lag behind the logical cache because previously submitted work is still in flight. Reload and render on the UI thread in order.

`svg-replacement-pressure` records the Vulkan image allocation sizes for the current SVG texture and for retired textures still referenced by submitted frames. It reports these separately from the logical RGBA cache budget and verifies retirement with one and three frame slots.

When the live working set exceeds the budget, the request fails without changing its resolution. Unleased entries may stay cached until there is pressure or the context is disposed. `SvgTextureStatistics` reports logical residency and queued requests.

The earlier48-check widget checkpoint is preserved in artifacts/svg-support-2026-11-11/widget-verification.json. Current54 checks additionally protect retained-layer texture references after widget disposal and ordinary image alpha/immutable snapshots/color-metadata coalescing. Cache pressure cannot delete textures referenced by retained layer commands; removing that layer permits eviction. Manually versioned layers still require the caller to update ContentVersion when their visual inputs change.

## Parse once and reuse

```csharp
using Duxel.Core;
using Duxel.Svg;
using Duxel.Platform.Windows;

using var store = new SvgStore();
var decoder = new SvgImageDecoder(store, WindowsUiImageDecoder.Shared);
UiImageTexture.ImageDecoder = decoder;
var bitmap = UiImageTexture.LoadFromFile("icon.svg", new UiTextureId(101));
var svg = store.Load("icon.svg"); // Same resource; no second file read or XML parse.
var normal = svg.Rasterize(64, 48);
var enlarged = svg.Rasterize(256, 192);
var same = svg.Rasterize(64, 48); // Same immutable cached raster as normal.
```

Reference src/Duxel.Svg/Duxel.Svg.csproj when using current source. The Core module remains independent of Skia. Supply this decoder through the app's ImageDecoder option as well, so app initialization uses the same one-time registration. The store owns scenes and outlives all consumers. IUiSvgDocument and UiSvgRaster are Core contracts; frame consumers borrow immutable-by-contract pixel memory. Bitmap decoding returns copies, preventing callers from changing cached results.

Load canonicalizes the path and performs an in-memory lookup after first use. Source XML bytes are parsed once into a DOM; the backend walks that DOM once to compile a retained scene. Rasterize draws the scene and never reads XML. Raster results are keyed by resource revision and pixel dimensions in a bounded LRU. New sizes can rasterize without reparsing; returning to an un-evicted size reuses the same raster. Rotation/position use widget geometry transforms, rather than the XML/raster cache key.

Call Reload explicitly when the source changes; there is no per-frame file polling. Identical bytes preserve scene/revision/raster identity. Changed bytes compile transactionally, invalidate that asset's rasters and notify Changed after publication. Failed reload reports the error and leaves the previous committed resource intact. Disposal releases owned scenes/cache and rejects stale consumers. The default retained raster budget is64MiB and source limit16MiB; explicit limits can be supplied and oversized requests fail rather than clamp.

Statistics separates SourceReads, XmlParses, SceneCompiles, Rasterizations, RasterCacheHits and CachedRasterBytes. The last figure is retained CPU raster memory, not total native/GPU memory. Tests record100 same-size replays with one read/parse/compile/raster, and800 concurrent requests with one read/parse/compile/raster plus799 hits. Output correctness includes straight alpha, contain fitting, gradients/clip-path, viewBox-only icons, reload and LRU eviction. These are functional work-count contracts, not a throughput/FPS improvement claim.

Current input policy prohibits DTDs, scripts, external resources and unresolved fragments, with explicit errors rather than broken-image placeholders. Text requires the explicit font registration described above. Complete SVG capabilities, full text/script coverage, native packaging/AOT and full native/device qualification remain open. Do not treat the asset module as full SVG conformance.

Build/run the focused validation with .NET11 and samples/Duxel.Gallery/LocalSource.targets, following artifacts/svg-support-2026-11-11/product-verification.json. The test project is tests/Duxel.Svg.Tests/Duxel.Svg.Tests.csproj; pass an owned output directory to its executable. API, cache and result snapshots are separate from the earlier five-check candidate probe.

The backend contract accounts for SVG feature coverage, exact errors, resource ownership, display-scale quality, unchanged-frame reuse, deployment assets and NativeAOT. Direct2D documents a subset and silently ignores unsupported elements/attributes, so unchecked passthrough is unsuitable. The previous asset module used Svg.Skia5.2.3. The current managed backend and platform-specific native services are documented in svg-platform-rendering.md. Source documentation and successful analyzer/JIT checks are not application-level NativeAOT proof; full capabilities and the remaining routes still require qualification.

Acceptance must cover ordinary SVG file input, intrinsic dimensions/viewBox/alpha, painting such as gradients and clipping, retained source/resource reuse, scale and rotation, widget layout/clipping/lifetime, event-wait scheduling, actual Observer examples and paired API/run guidance. Exact feature and gate denominators remain unconfirmed until the backend contract is fixed. NativeAOT/runtime-image comparisons remain part of the original goal; no MSVC/Windows SDK probing or installation is allowed before the user supplies paths. Resize remains deferred.

UiSvg.CornerRadius adds a fixed rounded viewport while rotation/translation retain the same raster. See [rounded image contracts](image-widgets.md#rounded-viewports-and-image-fit) for fit, transparent source UVs, Observer controls and validation limits.

See [browser comparison](svg-browser-validation.md) for complex real originals and separate direct-SVG/image references. Observer → SVG → 브라우저 비교 displays browser and Duxel results side by side. Core, SVG and Vulkan share RGBA8 boundary conversion, and color images interpolate in premultiplied form. XML remains once per source revision. Full pixel equality is still unfinished.

General paths use four GPU samples and Wang uniform cubic subdivision separately from smooth primitive coverage. Inherited `visibility`, visible descendants and hidden text advances are supported. The comparison guide records independent pixel examples, actual capture conditions and remaining ellipse/complex-original differences.
