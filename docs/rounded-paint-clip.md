# Rounded paint clipping

`UiDrawListBuilder.RoundedClip(bounds, radius)` and `UiControlContext.RoundedClip(bounds, radius)` intersect paint with an analytic rounded rectangle. The scope uses the same local coordinates as its geometry. Bounds and radius must be finite; dimensions and radius must be nonnegative. Radius clamps to half the smallest extent. Dispose nested scopes in reverse order.

```csharp
var paint = new UiControlContext(ui);
using (paint.RoundedClip(new(20, 20, 120, 96), 24))
{
    paint.Frame(new(0, 0, 200, 160), new(255, 0, 0), default, 0, 0);
}
```

Nested scopes intersect rather than replace ancestors. Circular subtraction composes with the rounded intersection. The existing circle-only shader path stays separate inside the masked material, and unmasked materials do not fetch mask records. Both use the existing signed-distance derivative coverage policy, including MSAA1. No polygon approximation or forced sample increase is used. Geometry allocation and layout are unchanged. This is a paint scope: arbitrary custom controls must keep hit testing consistent with their visible allocation. Popup builders use their independent paint scope.

The legacy command property `CircleCutoutIndexPlusOne` now also addresses a rounded-mask chain. Auxiliary records use the existing32byte primitive stream with backward relative links, so whole-list copies preserve ancestry. Split copies rebuild links. Retained/reference append composes outer bounds in the command's untranslated local space and intersects its rectangular clip as well. Source geometry remains immutable. Changed mask geometry participates in static revisions, and damage snapshots compare every ancestor including radius. An ancestor-only change repairs every acquired image; unchanged frames can skip drawing and upload. External draw callbacks inside paint clips fail explicitly.

Shader binaries target Vulkan1.2/SPIR-V1.5, keep the128byte push-constant layout and require no new shaderInt64 or subgroup-width feature. The fragment stage reads the mask through existing buffer-device-address support. See the [official buffer-device-address guide](https://docs.vulkan.org/guide/latest/buffer_device_address.html). GPU addresses travel as integer components between stages. The generated chain has strictly backward links.

Run `./run-gallery.ps1` → **Rendering** → **Rounded clipping: Off**. The button clips retained primitives, a projected layer and the cached overlap capture; toggle circular subtraction to combine both. Values survive tabs. No animation timer is introduced. Card now connects this paint scope with a matching curved pointer boundary, including custom floating radii and zero-padding content. Card Preview remains separate work.

Evidence: `artifacts/rounded-content-clip-2026-11-12/verification.json`. Validation covers nested primitives, paired images, text, gradients, cached affine composition, translated retained geometry, compiled descriptor families, opaque depth submission reversal, material alpha and MSAA1/4. NVIDIA runtime evidence does not establish AMD execution. Original fixed RC3 NativeAOT, broad throughput, long-growth and cross-device gates remain open.
