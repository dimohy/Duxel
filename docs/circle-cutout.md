# Analytic circular subtraction

`UiDrawListBuilder.CircleCutout(center, radius)` and `UiControlContext.CircleCutout(center, radius)` subtract one circle from local paint. Existing rectangular clipping still applies. The gap preserves underlying pixels; it never paints an assumed background color. Positive finite radius and finite center are required.

```csharp
var draw = ui.GetWindowDrawList();
var clip = new UiRect(0, 0, 320, 240);
using (draw.CircleCutout(new(100, 80), 24))
{
    draw.AddRectFilledRounded(new(24, 24, 160, 112), new(30, 150, 210),
        ui.WhiteTextureId, 12, clip);
}
```

Coordinates are in the geometry's local logical space. Command translation and3D projection move the circle with the content. Primitive fill/border/circle, rounded images, gradients, font triangles and SVG texture painting share analytic coverage. Material opacity remains separate from depth/sample coverage. No XML, bitmap composition or polygon strips are needed.

The scope creates no animation timer and requests no periodic frame. The owner must request a frame when its model changes. Unchanged2D acquired images skip draw/streamed upload after each actual image has been repaired. Explicit3D/depth scenes retain their existing conservative whole-scene rendering policy; stable resident geometry is reused, but unrelated dynamic background upload/draw is not claimed to be zero.

One mask uses existing32-byte primitive storage. The128-byte push-constant and64-byte compiled descriptor contracts remain unchanged. Append/split/channel and retained replay preserve mask indices and immutable queued inputs. A changed outer mask publishes a derived content stamp even when the retained source version stays fixed. Damage comparison includes mask data independently of the rendered primitive range. Scoped retained references currently copy frame-owned geometry/commands to protect borrowed inputs; no zero CPU/allocation claim.

Direct nested scopes and external draw callbacks inside the scope fail explicitly. Channel/reference replay can preserve an already identical circle; distinct nested subtraction is unsupported. Use the full draw-list Append overload when replaying mask records; the vertices/indices/commands-only overload cannot transport the required auxiliary data.

Normal and cutout shaders share `imgui_vertex.glsl`/`imgui_fragment.glsl`, with compile-time entry wrappers. The normal entry contains no mask interface or fragment branch; its SPIR-V matches the protected baseline except two source-extension metadata records. Mask shader modules and a bounded nine-mode pipeline family are created on first use and retired with existing graphics resources. No new device feature, barrier, import or shaderInt64 requirement is introduced. Runtime entries are imgui.vert,imgui.frag,imgui_cutout.vert,imgui_cutout.frag,particles.comp; pass all five through the existing consistency tool's EntryShaders parameter.

Run `./run-gallery.ps1` from the repository root→**Rendering**→**Circle cutout**. Compare the patterned rear surface and the projected3D cutout; animation remains off unless explicitly enabled. The sample keeps the toggle across tabs.

Evidence: `artifacts/circle-cutout-2026-11-12/verification.json`. ManagedSDK11.0.100/runtime11.0.0 only. The first unified shader candidate had a measured cost increase and was retained as negative evidence. The final no-mask diagnostic uses three alternating independent processes,64warmup/256measured frames with unchanged256rounded rectangles: median-of-run-medians29.888→29.712us(-0.59%), overlapping variation; no speedup claim. All measured pixel hashes and141prior reference images agree. Original fixedRC3 NativeAOT/full workload,Private/working-set,crossvendor andlong-growth gates remain open. Avatar PresenceBadge placement/combined names/cutout integration remains the next consumer task.

This is a paint-only scope; layout,hit testing,focus and accessibility bounds remain unchanged. Deferred inline child captures also consume the parent mask.

Qualification snapshot:51/51 CPU suites,8136 assertion executions(CircleCutout20,Observer189);88 mask checks per configuration, five configurations with the actual Khronos validation instance/device layer loaded and no captured VUID/error. Configurations cover frame resources2/3,shared camera/basis settings,generic compiled mode and both schedulers. Additional GPU checks:primitive74,rounded-image40,SVG74,Avatar220,Presence552,Observer55. Assertions repeat across settings; these are not1455unique feature requirements. The sample now draws its execution counter after the actual body construction,so the same event frame reports its committed count without requesting another frame.
