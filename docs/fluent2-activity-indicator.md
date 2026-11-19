# ActivityIndicator

Run `./run-gallery.ps1` → **알림과 진행 상태 → ActivityIndicator**. Each of five sizes has adjacent source. Start/stop, hides-when-stopped, color and reduced-motion options update those snippets. NEW belongs only to ActivityIndicator.

```csharp
var activity = Fluent.ActivityIndicator(new(
    Size : UiActivityIndicatorSize.Medium,
    IsAnimating : true,
    AccessibilityLabel : "Loading document"));
activity.Stop();
activity.Options = activity.Options with { HidesWhenStopped = false };
activity.Start();
```

UiActivityIndicator<TPresentation> owns state/layout; UiFluentActivityIndicator is the generated adapter. Defaults are Medium, stopped and hidden-when-stopped. Hiding preserves the square allocation. Stop resets rotation to0 and stops frame requests; disabling HidesWhenStopped paints the static arc. Start restarts from0.

The Fluent iOS contract supplies a rounded-cap270degree arc and750ms linear cycle. ExtraSmall/Small/Medium/Large/ExtraLarge diameters are12/16/24/32/36 and strokes1/1/2/3/4 logical pixels. Default light/dark colors are#8F8F8F/#B8B8B8. Color overrides are supported; system contrast takes precedence. Unsupported sizes/color schemes fail explicitly.

ReducedMotion is a Duxel extension retaining busy semantics while freezing rotation and stopping requests. The source image accessibility trait maps to Image with Busy, no numeric Range or keyboard focus. AccessibilityLabel and ordinary AccessibleName supply naming; hidden state publishes no semantic element.

This is a native Duxel implementation without UIKit dependency, not an iOS runtime port or a full SwiftUI pixel-parity claim. Complete accessibility/motion/platform acceptance comparison remains open. Canonical linkage is46/55 with9 unlinked.

Evidence: artifacts/fluent-activity-indicator-2026-11-17/result.json. CPU14/Vulkan12/catalog4/GalleryGPU270 pass. The26focused cases qualify supported sizes/palettes. A later explicit unsupported-color-scheme guard preserves supported color values; final Windows/Linux HOST builds pass with0warnings/errors. Unchanged widget/close-A-B/full-native suites were not repeated. Actual Linux and pinned RC3 NativeAOT remain unrun.

Official source: https://github.com/microsoft/fluentui-apple/blob/main/Sources/FluentUI_iOS/Components/ActivityIndicator/ActivityIndicator.swift
Official tokens: https://github.com/microsoft/fluentui-apple/blob/main/Sources/FluentUI_iOS/Components/ActivityIndicator/ActivityIndicatorTokenSet.swift
