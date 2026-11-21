# Fluent SegmentedControl

The normal Linux Gallery launch exposed **Fluent Controls → Input and Selection → SegmentedControl**. Both managed and NativeAOT passed seven external AT-SPI and X11 checks in a fixed 1280×900 window. Right Arrow skipped the disabled Week option. Horizontal dragging retained item 1, and a new click after release selected item 2. The two captures differed only in the live status text. The same builds moved and restored the source viewport after releasing and immediately re-grabbing the SVG thumb on the Stroke Bounds and Arc Comparison tabs. [Live verification](L:/Cache/Duxel/linux-gallery-segmented-live-20261121/verification.json)

`Fluent.SegmentedControl` provides single selection with four standard styles, keyboard navigation, RTL, icons, unread markers and horizontal scrolling.

```csharp
var value = new UiState<string?>("day");
UiSegmentOption[] items = [new("day", "Day"), new("week", "Week"), new("month", "Month", Unread : true)];
var control = Fluent.SegmentedControl("Period", value.Binding, items,
    new(Width : 320, EqualWidth : true, RightToLeft : false, ReducedMotion : false));
```

## Selection and layout

Stable values preserve selection across reordering. Duplicates fail before mutation; replacing items cancels the current press. Only accepted selection clears unread markers. Rejected controlled changes preserve accepted selection and semantics. Each input invokes the binding once.

EqualWidth uses equal widths; false distributes fitted width proportionally to intrinsic widths. Width0 uses natural width. Fixed-width labels ellipsize to one line while retaining complete accessible names. Items can display20px icons instead. Navigation skips disabled items; RTL mirrors physical placement and horizontal arrow direction.

## Custom behavior and painting

Derive from `UiSegmentedControlWidget<TBehavior, TPresentation>` to select an input policy and a painter at compile time. An `IUiSegmentedControlBehavior` can call `input.Process(UiButtonActivation.Press)` to commit on pointer down. The shared code still publishes the accepted selection and accessibility state. An `IUiSegmentedControlPresentation` receives the measured items, control bounds and selection state and can draw the entire control. The Fluent factory selects `UiSegmentedControlReleaseBehavior` and `UiFluentSegmentedControlPresentation`.

The built-in painter reuses the scrollable paint snapshot. Custom painters refresh it on every rendered frame by default, so they can draw from data outside the widget. Set `CanRetainPaint` only if paint depends on the same layout, palette, interaction and icon inputs as the built-in painter.

## Horizontal scrolling and GPU cache

IsFixedWidth defaults to true. False retains intrinsic content widths within the Width viewport. Drag, horizontal wheel and vertical wheel pan content. Drag release does not activate the segment below the pointer. Programmatic and arrow-key selection reveal the accepted item before semantic publication within the same frame. TabList and SegmentedControl share horizontal input.

The widget owns a UiPaintGroup snapshot. A UV camera reads the visible region; a linear opacity mask spans one quarter of each edge only when content is hidden on that physical side. It does not cover content with an assumed canvas color. Masking applies once after internal overlap composition, preserving arbitrary backgrounds and RGB text coverage. Forced colors disable fading.

Offset, UV and real-background changes do not recapture content or remeasure text. Fonts, layout, palette, enabled/unread/focus state and animated selection poses invalidate actual paint content. Icons report content changes through WithContentVersion or their provider. Unversioned custom painters cannot promise static content and are not treated as immutable snapshots. Widget disposal releases the owned cache.

## Styles and motion

Style supports NeutralOverNavBarPill(default), NeutralOverCanvasPill, BrandOverNavBarPill and BrandOverCanvasPill. Standard light/dark enabled/disabled tokens cover16 combinations; authoritative forced colors take precedence. Deprecated primaryPill/onBrandPill aliases, iOS darkElevated and complete custom-brand token overrides are not exposed.

Metrics are font15, line20, minimum height32, external horizontal inset16, content padding16/6, corner16 and unread dot6 with6/3 offsets. HorizontalInset and UnreadLabel are configurable. Selection moves over200ms with cubic-bezier(0,0,.58,1). ReducedMotion snaps immediately; settled transitions schedule no animation frames.

## Running and qualification

Run `.\run-gallery.ps1` from the repository root and open **Fluent controls → Input and selection → SegmentedControl**. Four style samples and a fifth horizontal-scroll sample each show source reflecting current options.

The normal Windows Gallery launch was verified through its navigation. An external UI Automation client selected Month and verified that Right Arrow skips the disabled Week option. In the fifth sample, a horizontal drag preserved selection and a subsequent click selected a visible option. A separate NativeAOT window on Linux x64 WSLg verified selection transitions for 36 segmented controls. Windows NativeAOT and UIKit screenshot equivalence remain open.

[Cache/mask evidence](../artifacts/fluent-segmented-cache-2026-11-18/result.json), [input evidence](../artifacts/fluent-segmented-scroll-2026-11-18/result.json), [shared mask/UV contract](horizontal-opacity-mask.md).

Official pinned reference: [Microsoft implementation](https://github.com/microsoft/fluentui-apple/blob/03928222b0fde3621fd5b6ea5c7ff1aeefdc1f95/Sources/FluentUI_iOS/Components/SegmentedControl/SegmentedControl.swift), [tokens](https://github.com/microsoft/fluentui-apple/blob/03928222b0fde3621fd5b6ea5c7ff1aeefdc1f95/Sources/FluentUI_iOS/Components/SegmentedControl/SegmentedControlTokenSet.swift). Previously captured official sources were reused.
