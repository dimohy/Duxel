# Shimmer

Run `./run-gallery.ps1` → **알림과 진행 상태 → Shimmer**. Toggle loading/completion and reduced motion. The adjacent source follows the actual options. Shimmer is the latest menu addition and owns NEW.

```csharp
var placeholder = Fluent.Skeleton(Fluent.SkeletonItem(new(Width : 240)));
placeholder.AccessibleName = "Loading content";
var example = Fluent.Shimmer(Fluent.Body1("Ready content"), placeholder);
example.IsDataLoaded = true;
example.Options = new(ReducedMotion : true);
```

UiShimmerWidget owns both placeholder and real content; UiFluentShimmer is its generated adapter. The optional UiSkeletonWidget placeholder composes existing circles, lines and layout gaps; omission creates a default line. Colors, shapes and wave rendering reuse Skeleton and support custom SkeletonItem presentations.

While loading, real content is not rendered or included in input, tab traversal or accessibility. Loading completion activates content and fades a frozen final placeholder snapshot over 200ms. Reversing retires content input/focus and reuses its final paint. Retargeting continues from the sampled opacity. Shared UiFloatTransition and UiPresencePaint provide finite scheduling without a new timer or endless loop. Settled loaded content stops the placeholder wave and requests no extra frames.

Options.Transition configures timing, curve and pacing. ReducedMotion snaps the transition and freezes inherited Skeleton motion; an explicit custom placeholder setting follows existing child-over-parent Skeleton precedence. Layout reserves the maximum child size to prevent surrounding content jumping. Both owned children survive toggles, preserving their state.

This native control combines Microsoft Fluent v8 Shimmer loading-to-content semantics with the existing Fluent Skeleton engine. It does not reproduce CSS length strings/DOM options or establish complete Fluent visual, state or accessibility conformance. Canonical linkage is 44/55 with 11 unlinked.

Evidence: artifacts/fluent-shimmer-2026-11-17/result.json. Focused CPU14, Vulkan8 at MSAA1/4, catalog4 and GalleryGPU270 pass. Windows/Linux HOST builds pass; actual Linux execution and pinned RC3 NativeAOT remain unrun. Unchanged typography/Tooltip/common-engine suites are reused rather than repeated.

Official API: https://github.com/microsoft/fluentui/blob/master/packages/react/src/components/Shimmer/Shimmer.types.ts
Official transition: https://github.com/microsoft/fluentui/blob/master/packages/react/src/components/Shimmer/Shimmer.base.tsx
