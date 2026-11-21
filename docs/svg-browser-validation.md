# SVG browser comparison

The graphics executable provides test-only `gpu-boundary-hardware`, `gpu-boundary-hardware-approx` and `gpu-boundary-hardware-local` probes. They require the current Windows125% DPI contract and fail before device creation if the framebuffer is not764×372 (or1024×512 for the approximate-size probe). No system setting or interactive resize is changed. The same baked triangle is drawn by the ordinary Vulkan4×MSAA mesh pipeline over black/white. The larger target compares the unchanged764×372 logical region; the local probe replays the same triangle with its integer bounds origin(215,303) and restores the untouched white region for comparison. All three still differ from the frozen white browser image at one pixel, max56. Capturing valid interior/exterior colors is a diagnostic precondition, not a passing browser comparison. Product Core/SVG/Vulkan DLLs are byte-identical to the conic checkpoint. [Results](../artifacts/svg-native-boundary-2026-11-17/verification.json).

The frozen `RootTransforms` group separates fractional intrinsic sizing from equivalent integer-root matrix and device-coordinate triangles. `--browser-root-transforms <fresh-output>` checks white-initialized and transparent phases separately. Transparent output matches the two controls, but all three white-target comparisons fail at (323,323). The browser controls themselves show a56-value difference there between white-initialized rendering and post-composed transparent output. A native four-sample WebGL triangle also matches the current engine's two covered samples. Therefore neither generic integer winding nor root transform arithmetic alone explains every phase. No rendering change or tolerance adjustment was promoted. [Evidence](../artifacts/svg-root-transform-2026-11-17/verification.json).

Bounds candidates with zero CPU coverage are retained and marked `contributes: false`, so a browser-only covered sample is not hidden by the diagnostic. Analytic coverage has no discrete mask and is reported as `fillSampleMask: null`. Independent original-leaf captures localized Red's maximum error to one gray boundary pixel (browser alpha191 versus CPU127), and Yellow's maximum error to its white highlight (browser alpha64 versus CPU0). At the Yellow point, body/gradient/shadow combinations otherwise agree. These are unresolved coverage differences, not corrected product output. [Evidence](../artifacts/svg-pixel-attribution-2026-11-17/verification.json).

For point-level attribution, the managed test executable accepts `--probe-pixel <svg> <width> <height> <x> <y> <fresh-json-file>`. It reads the actual compiled scene and reports the complete CPU pixel, source IDs/tree paths and isolated leaf pixels. Leaf probes use the full viewport, preserving device coordinates; ancestor effects are listed separately and are not applied to isolated leaves. Direct svg/g/a hierarchies are supported; use/text/marker expansion is explicitly rejected. The diagnostic also compares ordinary path sample masks with an integer fixed-point edge calculation. These observations localize a mismatch; they do not replace independent browser references or certify rendering correctness.

2026-11-17 conic implementation: SVG arcs retain control points and weights instead of being approximated as cubics. Shared geometry connects rational extrema/bounds, clipping/masks/filters, stroke tangents/lengths/splits/dashes, CPU diagnostics and GPU evaluation. Controls and weights remain immutable after compilation; camera zoom evaluates on the GPU. Both scene and stroke shaders validate protocol4 and reject the actual previous protocol3 scene shader and previous stroke shader before module creation. Push-constant sizes remain96/40 bytes.

All six frozen arc/control images exactly match transparent GPU output and white-initialized GPU targets. Managed `--browser-arc-opaque-target` also checks the white output and a separate transparent phase, passing all six. Existing `--browser-arc-geometry` and GPU `gpu-arc-geometry` retain their separate resolved-transparent-texture-over-white path; one-channel-value differences remain. References and tolerances are unchanged, and that path is not reported as passing.

| Comparison scope | Before | After |
| --- | ---: | ---: |
| Six transparent GPU arc images | 2/6 exact | 6/6 exact |
| Six white-initialized GPU arc targets | 2/6 exact | 6/6 exact |
| Red original default opaque comparison | 36176 pixels, max55 | 33419 pixels, max56 |
| Yellow original default opaque comparison | 1933 pixels, max52 | 1892 pixels, max34 |
| Complete Gallery document | 60 pixels, max1 | 60 pixels, max1 |

Red has fewer differing pixels but a maximum error larger by one. Complete equality of both original documents and the Gallery remains failing. CPU309 checks include eight new conic checks; GPU stroke46, dash46, scene67, pixels58 and background30 checks pass. Invalid weights, intervals and kinds reject before GPU submission. Ordinary widgets1387 and Markdown312 pass, and all677 previous screenshots are byte-identical. This is neither a performance claim nor full SVG completion. [Qualification](../artifacts/svg-conic-2026-11-17/verification.json).

Open **SVG → Arcs and curves** for the same six references. Zoom, rotation, margin, corners and white-background controls remain available; background composition differences remain distinct. Windows67 actual presentation scenarios verify all six previews with zero additional CPU rasterizations. Linux qualification is a host build only.

2026-11-17 source blend precision: the CPU diagnostic and Vulkan shared source-over store all foreground RGB and alpha channels as RGBA8 before blending. Alpha-only rounding failed the independent references and was rejected. Geometry coverage, gradient interpolation, dithering and shader ABI are unchanged; that checkpoint used protocol3; current conic payloads use protocol4.

`gpu-blend-backdrops` and managed `--browser-blend-backdrops` compare all pixels of12 linear/radial gradient fixtures over colored backgrounds against independent Chromium Canvas captures. Source, parent and reference hashes and capture conditions are preserved under `tests/Duxel.Svg.Managed.Tests/Fixtures/Browser/BlendBackdrops`. This set does not replace the original complex SVG references or relax tolerances. [Qualification](../artifacts/svg-source-storage-2026-11-17/verification.json).

Product CPU and GPU qualification both match all12 images and49152 pixels exactly. Existing SVG301, ordinary widgets1387 and Markdown312 checks pass; all677 previous Markdown images are byte-identical. Windows Gallery66 presentation scenarios and the Linux host build also pass. Original opaque complex-SVG differences improve42430→36176 and44101→1933, but maximum errors55/52 and whole-image failures remain. Transparent alpha differences stay277 or improve4816→173. Gallery whole-image differences improve96→60 but still fail. This is neither a performance improvement claim nor complete SVG qualification.

```powershell
$exe = 'W:\Cache\Duxel\svg-rounded-stroke-20261116\publish-491\Duxel.Graphics.Tests.exe'
& $exe 'W:\Cache\Duxel\blend-review-001' 'gpu-blend-backdrops'
```

Use a fresh output directory. This checks12 blend references; it does not substitute for whole-image acceptance of the original two complex documents.

2026-11-17 GPU target backgrounds: shared `UiTextureUpdate.VectorBackground` and widget `UiSvg.Background` initialize the GPU target while internal dependency scenes stay transparent. A changed background dispatches once without reuploading the scene; returning to a cached widget background reuses the output. Shader protocol3 rejects the actual previous protocol2 SPIR-V before module creation. The push-constant range is96 bytes, with background at offset88; no additional Vulkan feature is required.

The new28 checks cover complete pixels against integer references, translucent backgrounds, cache identity, letterboxing, cameras, groups, masks, patterns, Gaussian filters, actual widgets and release. Existing1387, Markdown312, GPU vector57, readback9, SVG pixels49 and document-features30 checks pass. Default transparent output is byte-identical to the two previous complex SVG outputs and677 raw Markdown images. Windows Gallery66 scenarios include both new switch states and actual presentation acknowledgements; Linux is a host build only.

Original full Canvas comparison still fails at42430/44101 pixels through the default transparent path. Separate `gpu-svg-opaque-target-browser` produces41858/47735 differences: one document improves and the other worsens. This path was therefore not adopted as a whole-image equality fix. The gallery feature image still differs at96 pixels. Reference bytes and tolerances are unchanged. [Evidence](../artifacts/svg-target-background-2026-11-17/verification.json).

2026-11-17 reference preflight: the managed and GPU comparison consumers now enforce the capture tool's DPR1 and aligned-viewport contract. Both preserved direct-display references record fractional viewport dimensions and are rejected before rendering. No reference bytes or tolerances were changed. Fresh aligned Red Gallardo screenshots differed at7698 pixels between two captures (maximum channel difference56), so they were not adopted. Both Canvas outputs match their repeat captures and original opaque references byte-for-byte. Direct-display reproducibility and engine output differences remain separate open requirements.

The new `svg-reference-contracts-only` mode passes12 input-contract checks. `gpu-svg-image-browser` independently compares the complete Canvas images of both original documents without qualifying direct display; both still fail at42430 and44101 differing pixels. Existing `gpu-svg-browser` and managed `--browser` now reject the unaligned direct references before rendering. The managed SVG regression passes301 checks. A unit-circle radial-gradient candidate left output unchanged and was rejected; five product DLLs and the embedded shader match the preceding normal build. [Evidence](../artifacts/svg-reference-preflight-2026-11-17/verification.json).

```powershell
$exe = 'W:\Cache\Duxel\svg-rounded-stroke-20261116\publish-476\Duxel.Graphics.Tests.exe'
& $exe 'W:\Cache\Duxel\reference-contracts-review-001' 'svg-reference-contracts-only'
& $exe 'W:\Cache\Duxel\original-images-review-001' 'gpu-svg-image-browser'
```

The second command currently fails because the complete images differ. It does not qualify full equality or the overall SVG goal.

2026-11-17 renderer qualification: the observed Chrome156.0.8078.96 uses `GraphiteDawnD3D11`, Skia revision `ea1e651d7a105f5ca327dfd65d28957957279e2e`. A fresh opaque capture is byte-identical to the original; all premultiplied RGBA8 pixels of the transparent capture are also identical. The original capture backend was not recorded and is not inferred retrospectively. The composition test now consumes `renderer-verification.json`, checks source and phase-reference hashes before GPU work, rejects mismatched references or missing renderer information, and includes that record in `composition.json`. A signed-edge interpolation candidate worsened96→97 pixels; an operation-order candidate left output unchanged. Neither was adopted. Six new reference-contract checks and three existing contracts pass, but the complete-image assertion still fails. Product shaders are unchanged. [Evidence and limits](../artifacts/svg-renderer-qualification-2026-11-17/verification.json).

2026-11-17: a separate `svg-feature-composition-only` qualification mode now reads the retained GPU texture before framebuffer composition, compares it with the preserved transparent browser capture, and checks the complete opaque browser image. On the gallery document-features example, integer premultiplied-over matches all51200 framebuffer pixels exactly; the direct browser image still differs at96 pixels (maximum channel difference1). The transparent comparison has1454 differing pixels, including17 alpha differences. These are separate rendering stages, not interchangeable references. The strict complete-image assertion currently fails, so passing interior-color checks cannot qualify full browser equality. The original complex SVG documents remain separate open acceptance targets. One isolated physical-circle-distance shader candidate worsened opaque differences96→180 and alpha differences17→101; it was rejected. Product shader resources and five product assemblies remain byte-identical to the preceding checkpoint. No Skia dependency, background-color bias, tolerance or shader replacement was added. [Evidence](../artifacts/svg-feature-composition-2026-11-17/verification.json).

2026-11-16 follow-up: circular-corner primitive strokes now share outer-minus-inner analytic coverage in bitmap and GPU output. Noncircular/dashed curves retain their existing stroke program. The fixed Observer reference improves253→96 differing pixels, maximum channel error65→1; full equality remains open. The intermediate pale-blue triangle came from new scene records paired with an old embedded shader. The matching shader is now included, and a protocol2 specialization marker is checked before shader-module creation. Regression29 includes the actual previous SPIR-V rejection, malformed records and unchanged source colors. Normal Observer verification now presents46 scenarios including **SVG document features**. [Evidence](../artifacts/svg-rounded-stroke-2026-11-16/verification.json).

2026-11-16: fixed Observer **SVG → Document features** losing stripe/radial-gradient fills under a pale-blue border. The stroke path inherited rounded-rectangle/ellipse **fill** coverage metadata and bypassed stroke construction. `SvgStrokePlan.GpuSource` now keeps only analytic coverage derived from the stroke itself; rounded fills and genuine round-cap line strokes retain their optimizations. The original SVG is unchanged. Color regression12, isolated SVG301, GPU stroke46 and GPU SVG49 checks pass. Eight predetermined interior pixels match an independent Chromium capture exactly. The full320×160 reference still differs at253 edge/compositing pixels (maximum channel difference65); this is not full SVG browser equality. [Evidence](../artifacts/svg-feature-colors-2026-11-16/verification.json).

Acceptance requires the same output as the pinned browser. Successful display or a small average error does not pass. The two original documents currently fail full pixel equality.

## Originals and references

| Original | Complexity | Source and license |
| --- | --- | --- |
| Red Gallardo | 251 paths,228 linear gradients,51 radial gradients,17 patterns and Gaussian blur | [Wikimedia Commons](https://commons.wikimedia.org/wiki/File:Red_Gallardo.svg), Tomeq183/Tacosunday/Svgalbertian, CC BY-SA3.0 |
| TheStructorr Lamborghini Gallardo | 132 paths,82 linear gradients,31 radial gradients and3 patterns | [Wikimedia Commons](https://commons.wikimedia.org/wiki/File:TheStructorr_Lamborghini_Gallardo.svg), Michał Pecyna, CC0 |

Original bytes are unchanged. `tests/Duxel.Svg.Managed.Tests/Fixtures/Browser/sources.json` pins source URLs, SHA-256 and authors. `references.json` records the observed browser version, scale, size, capture time and pixel hashes. Retain `ATTRIBUTION.md` from that directory.

References independently cover direct SVG display and native SVG-image rendering through Canvas2D. Conditions are white, sRGB and DPR1. Both use intrinsic-size `xMidYMid meet` placement with intrinsic viewport clipping. Image rendering additionally captures transparent pixels. Direct display and image rendering have different intermediate color-storage stages and are tested separately.

White results require zero difference in every RGBA channel. Transparent results require exactly equal alpha and premultiplied RGBA8 values. Different straight-alpha RGB representations of the same premultiplied pixel are not visual differences. Mean error and severe-pixel ratio are diagnostic metrics, never acceptance thresholds.

## Observer

```powershell
Start-Process -FilePath 'L:\Cache\Duxel\linux-accessibility-2026-11-14\observer-managed-build\bin\Duxel.Gallery\release\Duxel.Gallery.exe' -WorkingDirectory 'W:\MyWorks\Duxel' -WindowStyle Normal
```

Open **SVG → Browser comparison**. The browser reference appears on the left and `UiSvg` renders the same original on the right. White backgrounds and underlying pixel resolution match. First entry verifies source/reference hashes. Redraws and tab returns retain XML and raster caches. The document-features panel also exposes dashed strokes, markers, patterns and masks.

No ordinary Observer window is launched automatically. This is a .NET11.0.100 managed validation build, not the fixed RC3 NativeAOT result.

## Run comparison

Use the stable SDK directory `W:\MyWorks\Duxel\tests\managed-validation` and a fresh result directory:

```powershell
Set-Location -LiteralPath 'W:\MyWorks\Duxel\tests\managed-validation'
dotnet run --project 'W:\MyWorks\Duxel\tests\Duxel.Svg.Managed.Tests\Duxel.Svg.Managed.Tests.csproj' -c Release -p:TreatWarningsAsErrors=true -p:UseArtifactsOutput=true -p:ArtifactsPath='L:\Cache\Duxel\svg-browser-build' -- --browser 'L:\Cache\Duxel\svg-browser-review-001'
```

Each rendering path writes actual/difference images and `result.json`. Remaining differences produce exit1. Do not regenerate references or increase tolerances to turn an implementation mismatch into a pass.

For an explicit recapture, call `captureBrowserReferences(page, fixtureDirectory, freshOutputDirectory)` exported by `CaptureBrowserReferences.mjs` through the existing browser-skill runtime. It validates originals, DPR and dimensions, writes a fresh directory, leaves page ownership to the caller and never promotes references automatically.

## Path edges and visibility

The observed Chrome156 Canvas2D backend is GraphiteDawnD3D11, with Skia revision ea1e651d7a105f5ca327dfd65d28957957279e2e. Browser.getVersion and the complete chrome://gpu snapshot are preserved separately in the analytic-coverage evidence archive; ANGLE describes WebGL and does not identify this Canvas2D renderer. General path coverage uses the observed four sample positions and byte-normalized resolve levels. `SvgCubicTessellation` computes resolution-aware uniform subdivision with Wang's formula, rounded to a power of two; raster vertices use a1/256-pixel grid. Stroke length and dash placement retain their separate adaptive subdivision. Ellipses, rounded rectangles and undashed straight round-cap lines share SvgAnalyticRRect; browser equivalence remains unfinished for some inputs.

`Fixtures/Browser/Coverage/` preserves six independent browser pixel references, source/output hashes and observed conditions. Full RGBA comparisons cover a triangle, concave polygon, cubic, rectangle, visibility inheritance and hidden clipping. Explicit32×32 Canvas pixels avoid DOM scaling. Passing these small cases does not pass the two complex originals. The isolated original paths now pass the later stroke/viewport fixtures, while both complete originals remain failing.

`visibility` inherits: descendants of `hidden` or `collapse` containers can restore `visible`. Hidden geometry remains in group bounds; hidden text preserves following glyph advances. This includes `initial`, `inherit`, `unset`, CSS precedence, use instances, images, text and clipping. Observer's document-features white diamond demonstrates a visible descendant inside a hidden group.

Mathematical references are the [Wang formula explanation](https://github.com/google/skia/blob/main/src/gpu/tessellate/WangsFormula.h) and [D3D11 rasterization specification](https://microsoft.github.io/DirectX-Specs/d3d/archive/D3D11_3_FunctionalSpec.htm). The implementation is an independent managed module and adds no Skia package/native dependency. These observed GPU browser conditions do not qualify every browser or device.

## Engine modules

`SvgStrokeCurve` retains analytic control points through dash slicing. `SvgStrokePath` measures user-space distance separately; `SvgStrokeTessellation` merges positional Wang subdivisions with angular subdivisions and offsets the actual tangents in the requested device mapping. It splits inflections, half-turns and collinear cusps, and shares angular precision with point strokes and round joins. Square caps are separate tangent extensions. The `CurveStrokes` corpus pins twenty independent full-RGBA browser cases, including two isolated strokes from the original Red document. Both original strokes and their combined fills/strokes now match. A separately captured straight round-cap line still differs because its analytic edge coverage is not yet matched; it remains a failed diagnostic, not a passing fixture.

`SvgViewport` supplies active pixel coverage to each draw and layer composite, instead of clipping the already combined document once. Opacity/filter/mask layers receive the clip on their target composite. Analytic rectangles intersect the clip geometrically before computing edge coverage. The `Viewports` corpus pins sixteen independent full-RGBA cases covering repeated draws, group/filter boundaries, coincident/crossing rectangle edges and two original fill/stroke paths. The former 39-pixel one-byte difference in the Red body path is corrected. The obsolete final `ClipViewport` pass is removed.

`SvgStroke` fills both the bevel interior and the outer tip of a miter join. It measures polygon winding relative to one vertex in double precision, so translating a stroke does not reverse overlapping pieces through cancellation. Eight independent native browser fixtures in `Fixtures/Browser/StrokeJoins` pin full RGBA for left/right/oblique joins and large coordinate origins. They reproduced missing join coverage and a translated winding hole before the correction. The separate `CurveStrokes` corpus verifies subsequent curved-stroke changes; neither small corpus certifies whole-document equivalence.

Color dithering uses8×8 Morton ordering and byte-normalized thresholds. The [GPU threshold calculation](https://github.com/google/skia/blob/main/src/gpu/DitherUtils.cpp) is a mathematical reference, not an external renderer dependency. Pattern/gradient opacity is converted to a rounded8-bit value by `SvgCompositing.PaintServerOpacity`. The `PaintServers/` fixtures exactly match independent browser RGBA for pattern opacity0.1,0.5 and0.9.

Six separately captured linear/radial gradients still differ by up to one premultiplied byte. Solid opacity0.9 also retains a one-byte red-channel difference. No tolerance is added to pass those cases. Color-storage precision and complete equality of the two originals remain unfinished.

Overlapping boundaries retain sample colors instead of compositing an early coverage average. `SvgSamplePixel` stores four premultiplied RGBA8 colors. `SvgRasterSurface.Samples` retains extra state only for pixels whose sample colors differ; the ordinary pixel array remains the current resolved view used by spatial filters and image export.

Group opacity and geometric clipping preserve sample locations. Mask images apply after resolving colors, and Gaussian filters consume resolved input. `mask-type="alpha"` is distinguished from the default luminance mask. Eight independent full-RGBA browser fixtures in `Fixtures/Browser/Compositing/` verify these boundaries, including adjacent and repeated triangles. Observer's white diamond now consists of four triangles to expose shared-edge compositing.

Passing these examples does not pass the two complex originals. Equal-load first-raster timing, allocation and memory improvements have not been measured.

| Module | Responsibility |
| --- | --- |
| Core `UiRgba8` | Shared straight/premultiplied RGBA8 boundary conversions |
| SVG `SvgCoverageRasterizer` | Geometric coverage, independent of paint and compositing |
| SVG `SvgCompositing` / `SvgOrderedDither` | Consistent color storage, composition and gradient dithering |
| SVG `SvgSamplePixel` / `SvgRasterSurface.Samples` | Per-sample color and explicit layer resolve boundaries |
| SVG `SvgRasterSampling` | Premultiplied interpolation for embedded images and patterns |
| SVG `SvgStrokePlan` | Retained authored geometry/style with resolution-aware stroke construction |
| SVG `SvgPaintTemplates` | Local gradient/pattern references resolved during initial compilation |
| SVG viewport/filter modules | Intrinsic clipping and Gaussian region, color-space and transform handling |
| Vulkan color-image path | One conversion into staging storage per upload, filtering before shared tint/coverage evaluation |

`UiTextureUpdate.IsColorImage = true` still accepts straight-alpha RGBA8. Upload conversion never mutates caller storage. Font coverage remains distinct from color images. Incorrect payload sizes fail instead of being truncated or zero-padded. Replaying a cached resolution does not repeat XML, stroke construction or upload.

Newly implemented routes include dashes/offsets/positive `pathLength`, gradient/pattern template chains, foreign editor-data handling and a single `feGaussianBlur`. Full SVG specification coverage and browser equality remain ongoing.

## Shared analytic shape coverage

`SvgAnalyticRRect` retains a canonical rectangle, elliptical radii and an affine frame at scene compilation. Rendering interpolates original local coordinates and the device-space antialias outset across a circumscribed carrier mesh, then evaluates the implicit ellipse and bounding planes. The mesh does not approximate the curved boundary with flat polygon edges. Subpixel shapes use width-dependent coverage scale/bias; paint sampling uses the interpolated base coordinates in the antialias ring. Paths remain available for bounds and general strokes; XML is not reparsed during rendering and no Skia runtime dependency is introduced.

The independent native browser qualification corpus has19 inputs. On the same Linux x64 .NET11 runtime, exact full RGBA8 matches increased from1/19 to11/19; all19 current Windows/Linux outputs are identical. Eleven positive regressions are hash-pinned under `Fixtures/Browser/Analytic/`. Eight failures remain archived, including a slanted capsule (2 alpha pixels, maximum1), skewed ellipse, circular rounded corners, thin capsule and gradient-painted rounded rectangle. Positive fixtures do not replace the full qualification corpus. Both complete original documents still fail all four strict rendering paths. Equal-load timing, allocation and memory comparisons have not run; no performance improvement is claimed.

The mathematical reference is the browser-pinned [Graphite analytic render step](https://github.com/google/skia/blob/ea1e651d7a105f5ca327dfd65d28957957279e2e/src/gpu/graphite/render/AnalyticRRectRenderStep.cpp) and its [fragment coverage functions](https://github.com/google/skia/blob/ea1e651d7a105f5ca327dfd65d28957957279e2e/src/sksl/sksl_graphite_frag.sksl). These observations identify one renderer/device configuration, not universal browser equivalence. See `artifacts/svg-analytic-coverage-2026-11-14/verification.json` for immutable inputs, failed outputs, before/after results and renderer identity.

The upper transform controls now affect both Duxel vectors in Browser comparison. Rotation/zoom change widget layout bounds; the Margin control reserves left space. The browser captures stay fixed, and each vector displays its measured bounds. See [SVG widget layout](svg-widget-layout.md).


This checkpoint Windows/.NET11 validation build can be run with the following command. The full-image gate currently exits1 and writes composition.json plus failures.txt; this is an unresolved acceptance failure, not a passing diagnostic.

```powershell
& 'W:/Cache/Duxel/svg-rounded-stroke-20261116/publish-466/Duxel.Graphics.Tests.exe' 'W:/Cache/Duxel/svg-feature-composition-check' svg-feature-composition-only
```
