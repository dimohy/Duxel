# Transparent feature reference

`transparent.rgba` preserves the 320 x 160 straight-alpha Canvas2D capture from the unmodified gallery `features.svg`.

- Source SHA-256: `0A9EDC2927BA02A75BD1BE173A9C97A731C81E500D4100136F11D5D441855D1D`.
- RGBA SHA-256: `C1EA158B62970FFD3E3EF9A4295E4D431271D9439AF1A6AE265A31ABDE03F1C9`.
- Provenance: `artifacts/svg-rounded-stroke-2026-11-16/composition-diagnostic/verification.json` and its preserved `browser355-composition.mjs` / `composition355-source.rgba`.
- The original opaque reference remains unchanged. Transparent and opaque image paths are different acceptance/diagnostic inputs.

The `svg-feature-composition-only` mode converts the transparent capture to premultiplied RGBA8 for the raw-texture comparison, checks integer-over composition independently, and requires exact equality for the entire original opaque image. The last assertion currently fails; the diagnostic metrics are not a reduced acceptance criterion.

## Renderer verification

`renderer-verification.json` records a subsequent observation, not the unrecorded backend of the original capture. Chrome156.0.8078.96 reports `GraphiteDawnD3D11`, Skia revision `ea1e651d7a105f5ca327dfd65d28957957279e2e`. A new opaque capture is byte-identical to the original. A new transparent capture has different straight-alpha RGB at28 pixels but exactly the same premultiplied RGBA8 at every pixel (SHA-256 `CB27DBA372AFCFE00A08ADC51B1F3BC860076A8A72BECD51450748CD6D8258BB`). Neither original reference was replaced.

Before creating the GPU fixture, the qualification consumes this record and verifies source, opaque and premultiplied reference hashes plus the renderer identity. Negative cases reject mismatched references, a missing backend and an unqualified Skia revision. The record is included in every composition result. It identifies the observed target; it does not certify other browsers, GPUs or the two original complex SVG documents.
