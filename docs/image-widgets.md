# Retained images

[한국어](image-widgets.ko.md)

`Fluent.Image` paints immutable decoded pixels through the normal widget layout. PNG and SVG use the same image-source API. SVG used this way is a bitmap snapshot; use `UiSvg` for retained vector scenes, resolution-aware zoom and rotation.

```csharp
using Duxel.Core;
using Duxel.Platform.Windows;
using Duxel.Svg;

using var store = new SvgStore();
var decoder = new SvgImageDecoder(store, WindowsUiImageDecoder.Shared);
var source = new UiImageSource(decoder.Decode("artwork.svg")); // PNG uses the same path.
using var image = Fluent.Image(source);
image.Size = new(160, 100);
// In the existing render callback:
image.Render(ui);
```

The image source copies RGBA pixels once and does not expose mutable storage. A source can outlive the decoder/store after successful decoding. Share the same source object to share a texture within a context. If a platform decoder has already been registered as `UiImageTexture.ImageDecoder`, `UiImageSource.LoadFromFile(path)` uses that explicit registration and the existing format validation. Loading does file I/O and decoding; perform it before rendering. No file watcher, implicit decoder installation or failure substitution runs during painting.

`Size = default` uses source pixel dimensions as logical layout dimensions. A positive finite size selects a viewport, and parent constraints can override it. The full image stretches to that viewport. `Tint` multiplies straight-alpha RGBA. Changing viewport, parent allocation, position or tint changes the current quad without redecoding or reuploading pixels. Replace `Source` explicitly to display new pixels; assigning an unchanged source/size/tint does not request a frame. This decorative widget has no pointer activation, focus stop, hover animation or accessibility image role. Full Fluent image styling/accessibility acceptance remains open.

Each rendering context uploads a source at native resolution and owns its GPU identities. Widget leases pin live dependencies. The shared cache implementation keeps SVG and bitmap accounting separate: each cache has at most128 entries and64MiB of resident RGBA, so their combined configured capacity is128MiB. This excludes CPU source storage, renderer overhead and in-flight resources. Fully clipped images do not upload. If all entries are pinned, capacity failure is explicit. Disposing a widget releases its leases/source reference; unused cache entries retire under pressure, and renderer teardown releases native resources. A widget can survive one context and paint in another. Previously queued pixel memory remains immutable.

`ui.ImageTextureStatistics` reports queued uploads/bytes and bitmap cache residency. `ui.SvgTextureStatistics` still reports SVG work separately. Neither proves completed GPU work or total process memory.

Disposing a widget does not erase an explicitly retained static layer. Rebuild/invalidate or remove that layer when its content should disappear. A retained layer continues to protect its referenced texture even after widget leases have been released. Removing the layer permits later cache retirement. Submit queued CPU frames in order and dispose each frame only after its consumer finishes. The `raster-lifetime` GPU fixture prepares four CPU frames before submitting any of them: original,128-entry cache fill,protected replay and layer removal/retirement. Both Image and SVG keep their earlier queued geometry/commands/pixels immutable. Frame-resource2/3 configurations are checked, but readback serializes GPU completion; this is not a GPU-overlap or memory-growth benchmark.

Run `./run-gallery.ps1` from the repository root and choose **SVG**. The left bitmap now uses `Fluent.Image`; the right side uses `UiSvg`. The page reports separate bitmap/SVG upload counters. Resize/transform controls and tab revisits let you observe reuse; reloading changed source explicitly replaces the snapshot. See [widget contract validation](widget-contract-validation.md) for reproducible managed builds/tests. Current evidence is under `artifacts/image-widget-2026-11-12/`.

## Rounded viewports and image fit

`image.CornerRadius = 24` clips the allocated viewport with an analytic rounded rectangle; the radius is clamped to half its shorter side. `image.Fit` accepts `UiImageFit.Stretch` (default), `Contain` (centered transparent letterbox), or `Cover` (centered crop). The source texture stays at native resolution. Radius and fit changes do not decode, resample on the CPU, or upload another source texture. Zero radius with Stretch preserves the original triangle path.

`UiSvg.CornerRadius` uses the same GPU viewport mask. Rotation and translation map affine source UVs inside that fixed viewport and reuse the existing raster. Zoom can still rasterize a new resolution for quality; XML remains compiled and cached. Outside-source UVs are transparent. The low-level `UiDrawListBuilder.AddImageRounded` accepts a `UiImageUvTransform`; it uses two existing32-byte records per instance, with no expanded vertex ABI or new device feature.

In Implementation Observer, choose **SVG**. The shared corner slider changes by1px (including arrow keys); **이미지 늘이기 / 이미지 전체 맞춤 / 이미지 채우기** select the bitmap fit. Reset restores radius0 and Stretch. The counters do not request periodic frames. Run `./run-gallery.ps1` from the repository root.

Qualification: `artifacts/rounded-image-2026-11-12/`. Dedicated GPU checks cover MSAA1/4, source UV rotation, fractional AA, actual compiled retained layers, overlapping depth in reversed submission, rear visibility through transparent corners and material opacity. Queued lifetime tests cover resources2/3 with serialized readback; GPU overlap and long-duration growth remain separate. The shader diagnostic uses three alternating managed runs, 64 warmup/256 measured frames per run, one device and the existing rounded-rectangle workload. It is not the original fixed RC3 NativeAOT performance gate or a cross-vendor measurement.

Custom presentations may use UiControlContext.Image(image,bounds,tint,cornerRadius,fit) to paint a borrowed UiImage at explicit bounds without placing an extra layout/input item. The common path validates bounds/radius/fit before acquiring a texture. Keep the image alive and do not mutate/dispose borrowed Avatar content. See fluent2-avatar.md for ownership and parent allocation.
