# ProgressIndicator

Run `./run-gallery.ps1` → **Alerts and progress → ProgressIndicator**. Completion, waiting, track visibility and reduced-motion options update the adjacent source of each example. NEW now belongs only to ProgressIndicator.

```csharp
var progress = Fluent.ProgressIndicator(new(
    Label : "Upload",
    Description : "Sending selected files.",
    PercentComplete : .25f,
    Width : 360,
    ValueText : "File 1 of 4"));
progress.Options = progress.Options with { PercentComplete = .75f };
```

ProgressBar is the track; ProgressIndicator owns the task label, track and description layout. UiProgressIndicatorWidget is the owning implementation and UiFluentProgressIndicator is its generated Fluent adapter. PercentComplete uses0..1, clamping finite out-of-range values; null means indeterminate. Default Width180 and BarHeight2 are logical pixels. Both must be finite and positive; non-finite completion is rejected before rendering.

ProgressHidden removes only the track, its accessibility element and its animation scheduling, retaining label and description. Description wraps to width. Existing ProgressBar300ms value interpolation and indeterminate painting are reused. ReducedMotion snaps determinate interpolation and replaces horizontal indeterminate motion with uniform opacity changes; it is not a complete pause.

The progress node is read-only and nonfocusable. Naming follows ordinary AccessibleName, visible Label, then AccessibleLabel when Label is absent. Determinate Range is0..100 and publishes requested progress immediately, independently of visual interpolation. Description supplies help text; ValueText supplies native ItemStatus and value text. Indeterminate progress is Busy without a numeric Range.

This supports string labels/descriptions and native status metadata. It does not reproduce React nodes, CSS slots or complete browser aria-valuetext semantics, nor qualify all color/motion/external accessibility/modal compositions. Those remain in the full goal. Canonical linkage is45/55 with10 unlinked.

Evidence: artifacts/fluent-progress-indicator-2026-11-17/result.json. CPU14 and basicGPU8 are reused after matching Core hashes. One fixture incorrectly assumed reduced motion meant frozen output; current official implementation confirms opacity animation, so only corrected CPU1 and additional Vulkan4 were run. Catalog4,GalleryGPU270 and Windows/Linux HOST builds pass. Actual Linux and pinned RC3 NativeAOT remain unrun.

Official API: https://github.com/microsoft/fluentui/blob/master/packages/react/src/components/ProgressIndicator/ProgressIndicator.types.ts
Current official motion implementation: https://github.com/microsoft/fluentui/blob/master/packages/react-components/react-progress/library/src/components/ProgressBar/useProgressBarStyles.styles.ts
