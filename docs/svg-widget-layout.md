# SVG widget layout

SVG arc conic controls and positive weights are retained at initial compilation. Bounds, clips, filters, strokes, dashes, CPU diagnostics and GPU evaluation share the curve; zoom does not flatten paths on the CPU. Both scene and stroke shaders use protocol4. Push constants remain96 bytes for scenes and40 for strokes. In the three-record low-level curve payload, the interval marker is0 for lines,1 for cubics and negative for conic weight. Stroke kind7 retains three homogeneous weights; dash curve kind2 retains the same weights. Invalid kinds, intervals and weights reject before submission. [Fidelity and remaining scope](svg-browser-validation.md).

2026-11-17: `UiSvg.Background` clears the GPU output target **before** SVG painting. It defaults to transparent and is distinct from final image `Tint`. Translucent colors are stored as premultiplied RGBA8. Internal groups, masks, patterns and filters retain transparent work targets. Background participates in the texture cache key; returning to a cached background reuses its output without reparsing XML or rebuilding the vector scene. Existing margin, zoom, rotation and clipping layout contracts are unchanged.

```csharp
using var preview = new UiSvg(store.Load(svgPath))
{
    Background = new UiColor(255, 255, 255)
};
```

Gallery **추가 샘플 → SVG → 브라우저 비교 → 흰색 배경을 SVG에 포함** compares this with compositing a transparent texture onto white afterward. This choice does not make every document browser-identical. [Evidence and limits](../artifacts/svg-target-background-2026-11-17/verification.json).

2026-11-14: SVG Zoom/RotationDegrees now forward to UiWidget.Transform, with no separate rotation geometry. Margin is inherited from the common widget path. See [shared transforms, origins and visual mode](widget-transform-contract.md).

`UiSvg` participates in layout using the axis-aligned bounds of its rotated, zoomed content. `Size` configures the untransformed logical content; zero uses the document's intrinsic size. A64×48 document becomes48×64 after90° rotation, or96×128 after2× zoom and90° rotation. The following row/column item starts after those bounds.

`Margin` reserves nonnegative left, top, right and bottom space outside the transformed content. It changes measurement and placement, without XML parsing or raster work. `Offset` remains a compatibility shorthand: positive X/Y select left/top margins and negative values select right/bottom margins. New code should use `Margin` explicitly.

```csharp
using var preview = new UiSvg(document)
{
    Size = new(64, 48),
    Zoom = 2,
    RotationDegrees = 90,
    Margin = new(12, 8, 0, 0)
};
// Unconstrained measured size:108×136. The content raster remains128×96.
```

`UiWidgetTransformLayout` supplies the same transformed extent to measurement, paint vertices and raster sizing. Exact quarter turns avoid floating-point size drift. Rotation and margins reuse the raster/texture; zoom requests the corresponding retained-scene resolution. Explicit parent constraints still determine the final allocation. Margins are excluded from the content area, and rounded corners belong to the source content before transformation and rotate/scale with it. Parent scrolling/clipping remains effective.

In Implementation Observer, open **SVG → Browser comparison**. The upper zoom/rotation/margin/corner controls affect both Duxel vector previews. The left browser captures remain the fixed independent references. Each vector reports its layout width/height including transforms and margins. **Reset SVG transforms** restores the initial layout. See [launch and comparison instructions](svg-browser-validation.md#inspect-in-observer).

Validation covers parent-constrained pure measurement, exact and arbitrary rotations, four margins, subsequent row/column flow, cache reuse, actual Vulkan pixels at MSAA1/4, and the Observer controls. Browser pixel equivalence of complete SVG documents is a separate unfinished acceptance criterion.

The shared behavior for all widgets, rotation origins and interaction is tracked in the [common widget transform contract](widget-transform-contract.md). That migration remains incomplete.


