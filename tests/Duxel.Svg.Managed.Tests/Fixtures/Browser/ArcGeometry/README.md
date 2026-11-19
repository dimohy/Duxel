# Independent arc geometry qualification

Six independently authored64x64 SVGs distinguish circular, elliptical, affine
and rotated arcs from authored cubic and straight-line controls. Sources and
browser RGBA hashes are frozen in `sources.json` and `references.json`.
Captures used the existing `CaptureImageReferences.mjs` helper and the actual
Chromium browser on 2026-11-16. Both opaque and transparent references remain
available; the latter separates path coverage from final white composition.

Consumers retain strict whole-image equality with no tolerance:

- Graphics: `<fresh-output> gpu-arc-geometry`
- Graphics premultiplied texture: `<fresh-output> gpu-transparent-arc-geometry`
- Graphics with white initialized before sample resolve: `<fresh-output> gpu-arc-opaque-target`
- Managed SVG: `--browser-arc-geometry <fresh-output>`
- Managed SVG with independently checked white and transparent phases: `--browser-arc-opaque-target <fresh-output>`

The conic renderer now exactly matches all six transparent references and all
six white-initialized targets. Default post-composition remains a distinct
failing phase. A staged90-degree cubic subdivision candidate improved the two
originals but worsened independent arc cases and was rejected. The adopted
correction retains conics through bounds, clipping, strokes, dashes and both
renderers, guarded by shader protocol4.

The white-target mode isolates a second issue: the cubic control has exact
transparent pixels and exact white-target pixels, but differs by one channel
value at44 pixels when a resolved transparent texture is composited over white.
Do not combine those stages or relax either comparison to conceal the difference.

For Chromium156.0.8078.96, the actual SVG path builder forwards arcs to the
platform path builder and then to retained conics. The separate SVG path
normalizer's cubic decomposition is not evidence for that painting path.
The next correction must preserve the actual curve representation through
compilation, bounds, clipping, CPU diagnostics, GPU evaluation and strokes.

Primary execution-path evidence:

- [SVG path builder](https://github.com/chromium/chromium/blob/156.0.8078.96/third_party/blink/renderer/core/svg/svg_path_builder.cc)
- [Platform path builder](https://github.com/chromium/chromium/blob/156.0.8078.96/third_party/blink/renderer/platform/geometry/path_builder.cc)
- [Native path construction](https://github.com/google/skia/blob/ea1e651d7a105f5ca327dfd65d28957957279e2e/src/core/SkPathBuilder.cpp)
