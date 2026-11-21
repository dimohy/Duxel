# ProgressIndicator

Run `./run-gallery.ps1` → **Fluent controls → Alerts and progress → ProgressIndicator**. Completion, waiting, track visibility and reduced-motion options update the adjacent source of each example.

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

Derive from `UiProgressIndicatorWidget<TBehavior, TPresentation>` to reuse the label, description and accessibility behavior with a different track. The track uses the same `IUiProgressBehavior<TBehavior>` and `IUiProgressPresentation` interfaces as standalone progress bars. The presentation measures and paints the track. Its visual value can vary during a transition while the accessibility range reports the requested completion. The Fluent factory selects the existing Fluent behavior and presentation.

This supports string labels/descriptions and native status metadata. It does not reproduce React nodes, CSS slots or complete browser aria-valuetext semantics, nor qualify all color/motion/external accessibility/modal compositions. Those remain in the full goal.

Evidence: artifacts/fluent-progress-indicator-2026-11-17/result.json. CPU14 and basicGPU8 are reused after matching Core hashes. One fixture incorrectly assumed reduced motion meant frozen output; current official implementation confirms opacity animation, so only corrected CPU1 and additional Vulkan4 were run. Catalog4,GalleryGPU270 and Windows/Linux HOST builds pass. Actual Linux and pinned RC3 NativeAOT remain unrun.

Official API: https://github.com/microsoft/fluentui/blob/master/packages/react/src/components/ProgressIndicator/ProgressIndicator.types.ts
Current official motion implementation: https://github.com/microsoft/fluentui/blob/master/packages/react-components/react-progress/library/src/components/ProgressBar/useProgressBarStyles.styles.ts

## Windows Gallery and official scope

The Windows Gallery verifies the determinate and indeterminate linear tracks described by the [Fluent Web Progress bar](https://fluent2.microsoft.design/components/web/react/core/progressbar/usage/) guide. Eleven external UI Automation and mouse checks passed. Increasing progress from 25% to 50% filled 90 and then 180 pixels of a 360-pixel track. Switching to indeterminate progress preserved the status text and removed the numeric range. Hiding the track removed its accessibility element and kept the task label and description visible. Determinate progress exposed a read-only range and rejected external value changes. [Microsoft UI Automation contract](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-supportprogressbarcontroltype)

Windows .NET 10.0.15 and .NET 11.0.0 each passed 19 ProgressIndicator checks and 227 shared control checks. Four captures and the test records are in `L:/Cache/Duxel/fluent-progress-live-20261120/verification.json`.

The [Fluent Android Progress indicator](https://fluent2.microsoft.design/components/android/core/progressindicator/usage) defines both linear and circular tracks. The preceding Windows check covered the linear track. The Windows ring example is documented below. Android devices, TalkBack and browser pixel comparisons require separate verification. The Duxel example for Web guidance is described below.

## Circular progress

`Fluent.CircularProgressIndicator` fills from the top for a determinate value. A `null` value rotates a 270-degree arc with a one-second period. Determinate changes interpolate over 750 ms while accessibility reports the requested value immediately. The default diameter is 12. Set `Size` to select another diameter. With both axes set, the smaller axis sets the diameter. The default color is the theme's neutral color and can be changed through `Color`. `ReducedMotion` applies determinate values immediately and keeps the indeterminate arc stationary while exposing the busy state. [Pinned Android source](https://github.com/microsoft/fluentui-android/blob/428d208f50dcce27a8825fb60ddfdaa517eeb7b1/fluentui_progress/src/main/java/com/microsoft/fluentui/tokenized/progress/CircularProgressIndicator.kt)

```csharp
var ring = Fluent.CircularProgressIndicator(.25f);
ring.AccessibleName = "File synchronization";
ring.Size = new(40, 40);
ring.Value = .5f;
ring.Value = null;
ring.ReducedMotion = true;
```

In Gallery, open **Fluent controls → Alerts and progress → ProgressIndicator** and scroll to the circular example. The Windows run checked mouse input, the read-only range and range removal in the waiting state. It also compared the ring region in 25%, 50%, waiting, and reduced-motion captures. See the verification record at `L:/Cache/Duxel/fluent-circular-progress-20261120/verification.json`.

## Web progress transitions and errors

Set `FinishIndeterminateCycle = true` to keep the current waiting animation visible until the cycle ends when a determinate value arrives. Accessibility reports the requested value immediately. The default is `false`. Error and reduced-motion states switch immediately. The setting is available on `UiProgressIndicatorOptions` and `UiProgressValue`.

When a task fails with a known value, retain that value and set `Color = UiProgressColor.Error`. Put recovery instructions in `Description` and a short status in `ValueText`. If no value is known, `ProgressHidden = true` hides the track while keeping the description. [Fluent Web progress guidance](https://fluent2.microsoft.design/components/web/react/core/progressbar/usage/)

Open **Fluent controls → Alerts and progress → ProgressIndicator** in Gallery and scroll to the Web transition and error example. External UI Automation and mouse checks covered an error at 25%, failure without a known value, retry and completion during a waiting cycle. See `L:/Cache/Duxel/fluent-progress-web-20261120/verification.json`.

## Forced colors

With system forced colors active, linear progress draws its track in `CanvasText` and its fill in `Highlight`. Determinate, waiting, and reduced-motion states share these colors. The status and description continue to provide the error and recovery instructions. Circular progress uses the system foreground color `CanvasText`. [Pinned Fluent source](https://github.com/microsoft/fluentui/blob/bdcf886547c910b749eab2aa9c22c91152dfe6ab/packages/react-components/react-progress/library/src/components/ProgressBar/useProgressBarStyles.styles.ts)

Verification applied two explicit light and dark palettes in a fixed-size Windows Vulkan window. Each capture matched across .NET 10 and 11. The Duxel accessibility tree retained the read-only range and error recovery text in both palettes. See `L:/Cache/Duxel/fluent-progress-contrast-20261120/verification.json`.

## Official Storybook pixel comparison

The [official Storybook](https://storybooks.fluentui.dev/react/?path=/docs/components-progressbar--docs) and Windows Vulkan used the same backdrop, width of 160, 50% progress, and 75% error state. The Storybook 50% fill was `#0F6CBD` in Web Light and `#479EF5` in Web Dark. The Duxel build used in that comparison had `#005FB8` and `#60CDFF`, respectively. The dark track was `#333333` in Storybook and `#3A3A3A` in Duxel.

Matching the Storybook palette in the test window reduced the square bar's difference from 324 light and 640 dark pixels to 8 of 640 in each theme. The rounded bar went from 328 light and 640 dark pixels to 20 of 640 in each. The remaining differences clustered at the ends and fill boundary. The two-pixel-high error bar differed across all 320 pixels in each theme because of browser coverage and compositing, with the same error fill token. The captures and conditions are recorded in `L:/Cache/Duxel/fluent-progress-browser-20261120/verification.json`.

### Theme defaults and custom colors

The Windows 11 theme uses `#0F6CBD` for progress fill in light mode and `#479EF5` in dark mode. The tracks use `#E6E6E6` and `#333333`, respectively. Progress controls on the same theme share these defaults. Set `UiTheme.ProgressBarBg` and `UiTheme.ProgressBarFill` to change the ordinary palette. The fill role supplies the brand and waiting states. With forced colors active, the linear track and fill take the system's `CanvasText` and `Highlight` roles.

Both native windows using the new defaults matched the earlier palette-control captures. The light, dark, custom, and forced palette checks passed on .NET 10 and 11. Of 4,046 fixed-size widget images, 4,040 matched the previous build and six changed only in the progress region. The browser-edge differences match the earlier comparison. See `L:/Cache/Duxel/fluent-progress-palette-20261120/verification.json`.

### Placement checks for thin bars

The official Storybook 75% error bar was captured with the same colors and width at device scales 1 and 2, with controlled positions, corners, and clipping. In three placements at scale 1, 162 of 320 pixels differed from the archived Windows capture in each theme. The earlier placement differed at all 320 pixels.

Four Storybook crops matched the minimal CSS control at both scales. The square and rounded controls at identical positions had equal center rows in six of eight pairs. Changing only the corner setting changed the center-edge rows in the other two pairs. Thus the thin-bar difference count depends on placement and browser rendering. The Duxel comparison reused the hash-verified native capture. See `L:/Cache/Duxel/fluent-progress-edges-20261120/verification.json`.
