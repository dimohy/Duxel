# Gradient source storage over authored backgrounds

These 12 SVGs derive from the existing `Gradients` fixtures: `linear-alpha`,
`radial-alpha`, `linear-opaque` and `linear-transformed`. Each adds an opaque
rectangle behind the gradient, using one of three backgrounds: `#0f579b`,
`#caa12d` or `#252b41`. The six opaque gradients control interpolation and
dithering; the six translucent gradients exercise source-over onto nonwhite
destinations without shape-edge coverage.

`sources.json` records immutable source and parent hashes. `references.json`
records independent Chromium Canvas2D captures, source hashes, RGBA hashes,
dimensions, user agent and capture time. Captures used the existing
`CaptureImageReferences.mjs` helper on 2026-11-16, before product promotion.
The authored backgrounds make opaque and transparent canvas captures identical.
No original SVG or reference was replaced.

Consumers compare every channel of every pixel without tolerance:

- Graphics executable: `<fresh-output> gpu-blend-backdrops`
- Managed SVG executable: `--browser-blend-backdrops <fresh-output>`

The local correction is RGBA8 source storage before source-over. Passing this
set does not establish full complex-SVG or cross-browser equality.
