# Fluent 2 Skeleton

`Fluent.Skeleton(child, options)` owns loading content and supplies inherited properties to `Fluent.SkeletonItem(options)`. The Observer **Skeleton** tab exposes loading/completed content, Wave/Pulse, translucent appearance, reduced motion and static output.

```csharp
var loading = Fluent.Skeleton(
    Dux.Column(new(Spacing: 8, MainAxisSize: UiMainAxisSize.Min),
        Fluent.SkeletonItem(new(Size: 48, Shape: UiSkeletonShape.Circle)),
        Fluent.SkeletonItem(new(Width: 240)),
        Fluent.SkeletonItem(new(Width: 160))),
    new(Animation: UiSkeletonAnimation.Wave));
loading.AccessibleName = "Loading profile";
```

## Behavior and layout

Defaults are Wave/Opaque/16px/Rectangle. Null Animation, Appearance, Size, Shape, ReducedMotion and Paused inherit the nearest Skeleton. Width is local and never inherited. Nested scopes override their own descendants; resolved styles participate in retained measurement keys. Normal and exceptional exits restore the enclosing scope.

Supported sizes are 8, 12, 14, 16, 20, 22, 24, 28, 32, 36, 40, 48, 52, 56, 64, 72, 92, 96, 120 and 128px. Rectangle uses Size as height, a 4 px default radius and finite parent width. Square/Circle naturally measure Size×Size. Non-square circle allocations use the shared affine paint path to form an ellipse. An unbounded Rectangle has zero intrinsic width; supply finite allocation. Width must be finite and nonnegative.

Wave lasts 3 seconds and Pulse 1 second, using CSS ease-in-out and a shared absolute clock. Only visible animated items request shared 60 Hz frames. Paused displays a stable initial appearance; it does not preserve the current animation phase. ReducedMotion is also static. Offscreen, static and unmounted items do not perpetually schedule work. Other active widget animations are independent.

Painting uses the official Light/Dark stencil and translucent tokens with existing analytic rounded rectangles, gradients and transforms. It adds no separate CPU image rasterization path. Custom presentations implement static Measure/Paint on `IUiSkeletonPresentation`; retained measurement requires explicit opt-in.

## Accessibility and qualification

The named root exposes indeterminate ProgressBar semantics and Busy=true. Individual placeholders expose neither fake interactive controls nor numeric progress. Independent native clients verified Linux AT-SPI busy and Windows UIA AriaProperties busy=true. Callers replace loading content with actual data.

Focused 2026-11-15 validation passed 43 CPU and 131 generator checks; Windows GPU 396 checks/384 raw image pairs; pinned .NET11 RC3 NativeAOT Linux GPU slots 1 and 3 each 396 checks/384 pairs; Linux AT-SPI 8 client/3 host checks; and Windows managed UIA 6 client/1 host checks. The Observer passed 34 presentation stages covering 31 tabs and 3 SVG camera conditions, plus pointer, idle and restoration checks. Full Fluent conformance and Windows RC3 NativeAOT remain separate and incomplete. At the time of this focused run, standalone Fluent Text lacked native text-node publication. The subsequent [static text correction](fluent2-text.md) verifies actual publication and updates; completed-data fixtures use an actual read-only Korean input control.

The reference is the [official guidance](https://fluent2.microsoft.design/components/web/react/core/skeleton/usage) and Fluent UI commit `f9720c35ba893c9630f4c0389d07dc6d6fd6068d`. Official browser Wave/Pulse behavior and computed styles were inspected; native/browser pixel identity is not claimed. Sources, hashes and focused evidence are under `artifacts/fluent-skeleton-2026-11-15/`. Follow the [Linux Observer guide](../samples/Duxel.Gallery.Linux/README.md) to launch.
