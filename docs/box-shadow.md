# Cached outer box shadows

[한국어](box-shadow.ko.md)

`UiBoxShadow` owns reusable per-context texture leases for two outer shadow layers in CSS front-to-back order. `UiControlContext.BoxShadow(shadow, bounds, cornerRadius)` paints a rounded rectangle shadow without placing a layout item or registering input/accessibility. Reserve `shadow.Options.PaintBounds(bounds)` in layout and retain the owner until its consumers finish.

```csharp
using var shadow = new UiBoxShadow(
    UiFluentShadow.Resolve(UiFluentColorScheme.Light, UiShadowElevation.Shadow8));
// Inside paint:
context.BoxShadow(shadow, bounds, cornerRadius : 8);
```

`UiShadowLayer` supplies Offset, BlurRadius, straight RGBA Color and floating-point Opacity. Blur is Gaussian with sigma=BlurRadius/2;zero blur is analytic hard coverage. Insets and spread are outside this contract. The body remains transparent. Layers composite over each other once, then exclude the analytic body. No background-colored patch or polygon outline is used. Fluent neutral Shadow2/4/8/16/28/64 use pinned CSS offsets/blur and Light ambient/key opacity0.12/0.14 or Dark0.24/0.28. This is token fidelity,not browser-identical raster output.

Rounded-rectangle straight intervals integrate exactly;corners use16-point Gauss-Legendre integration with a normal-CDF approximation. The finite4-sigma support is extended by one logical pixel. Preparation evaluates the analytic shape at physical resolution and queues one immutable RGBA8 color texture. Later paints of the same options/body size/radius/content scale reuse the context's lease;translation does not regenerate it. Contexts retain independent size/DPI/color keys. Changed options are consumed on the next explicit paint;this resource does not request frames or invalidate caller-owned ContentVersion. Dispose it explicitly. Source pixels of queued frames remain immutable.

One raster must fit4,194,304pixels (16MiB RGBA). The existing bounded raster cache limits each cache to128entries/64MiB;live leases,retained references and current-frame entries cannot be evicted. Invalid inputs and exceeded budgets fail before generation/upload;no resolution reduction occurs. Initial CPU preparation is additional work and is not a zero-cost or speedup claim.

Within opaque3D content,the shadow is submitted as a read-only-depth child material. Retained children differing from their captured parent only in WriteDepth inherit the updated parent scene while retaining that override. Final core presentation stably submits opaque writes before translucent reads within each contiguous depth scope. 2D surfaces,presentation scopes,independent depth scopes and external callbacks are barriers;read-material painter order is preserved. General intersecting translucent surfaces still require caller-defined back-to-front ordering. This is separate from isolated group opacity.

Run `./run-gallery.ps1` → **아바타** → **아바타 활동 그림자**. Actual GPU tests cover patterned backgrounds,rounded/circular corners,common cutout,reversed depth submission,cached camera changes and acquired-image reuse. CPU tests compare convolution to an independent dense circle integral. Evidence and unresolved original NativeAOT/performance/full-conformance gates: `artifacts/box-shadow-2026-11-12/`.

Pinned source: [shadow geometry](https://github.com/microsoft/fluentui/blob/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/tokens/src/utils/shadows.ts), [neutral light colors](https://github.com/microsoft/fluentui/blob/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/tokens/src/alias/lightColor.ts), [neutral dark colors](https://github.com/microsoft/fluentui/blob/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/tokens/src/alias/darkColor.ts).
