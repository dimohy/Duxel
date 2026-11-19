# Horizontal opacity masks and cached cameras

`UiHorizontalOpacityMask` fades the output's left and right edges linearly. It does not cover content with a presumed canvas color. Existing black/white reference composition retains independent RGB text coverage.

```csharp
// Keep the group alive across frames. Change contentVersion when content or fonts change.
ui.PaintGroup(group, sourceBounds, contentVersion, PaintContent,
    new UiImageUvTransform(new(.25f, .25f), new(.5f, 0), new(0, .5f)),
    new UiHorizontalOpacityMask(.25f, .25f),
    destination : viewport, textMode : UiPaintGroupTextMode.Projected);
```

- LeftFadeFraction/RightFadeFraction are fractions of output width. Zero disables that edge. Both must be finite and nonnegative with sum at most1. The default mask is identity.
- Mask overloads are available on `PaintGroup`, `BackdropImage` and `BackdropComposite`. Camera overloads accept the existing `UiImageUvTransform`. Original public signatures remain available.
- The mask belongs to destination coordinates; UVs select the cached source. Changing only mask, UV or final opacity does not recapture content. Content, fonts, capture size, DPI and textMode follow their capture-version contracts.
- The scaling/projection example explicitly uses `Projected`; existing Native independent RGB coverage remains supported. This camera samples an already captured image, so enlargement quality depends on capture resolution. Specify rasterScale when needed.
- Existing triangle vertex-alpha interpolation is reused. Both edges require at most12 vertices and18 indices, with no shader, GPU-feature or supported-version change. The CPU prepares this small composition mesh; the GPU performs per-pixel composition. No CPU mask bitmap or per-frame text recapture is introduced.
- Existing circle subtraction and final opacity still compose correctly. Identity preserves the old API's exact geometry and commands. Material state and colors do not leak into subsequent ordinary images.

Run `.\run-gallery.ps1`, then **Integration/development tools → Development tools → Rendering**. The bottom **edge opacity mask and cached camera** example provides both/left/right/no mask, backdrop switching and camera zoom. Its live source is immediately below. Capture count remains unchanged when only the mask, backdrop or camera changes.

[Execution evidence](../artifacts/horizontal-opacity-mask-2026-11-18/result.json) compares different backdrops, both/one/no edge masks, opacity and MSAA1/4 against an independent affine composition equation. It includes nonzero RGB text coverage with zero alpha, overlap receiving opacity once, retained captures under mask/UV changes and actual Gallery source synchronization.

Actual Linux, pinned RC3 NativeAOT and complete3D-transform qualification were not run this increment. SegmentedControl scrolling integration and the full project goal remain active.
