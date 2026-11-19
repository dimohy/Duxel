# Fluent SegmentedControl

`Fluent.SegmentedControl` provides single selection with four official standard styles, keyboard navigation, RTL, icons, unread markers and horizontal scrolling. Its Gallery entry has no unimplemented badge. The current NEW batch belongs to Select. Factory linkage is50/55; this does not claim full Fluent conformance or UIKit pixel equivalence.

```csharp
var value = new UiState<string?>("day");
UiSegmentOption[] items = [new("day", "Day"), new("week", "Week"), new("month", "Month", Unread : true)];
var control = Fluent.SegmentedControl("Period", value.Binding, items,
    new(Width : 320, EqualWidth : true, RightToLeft : false, ReducedMotion : false));
```

## Selection and layout

Stable values preserve selection across reordering. Duplicates fail before mutation; replacing items cancels the current press. Only accepted selection clears unread markers. Rejected controlled changes preserve accepted selection and semantics. Each input invokes the binding once.

EqualWidth uses equal widths; false distributes fitted width proportionally to intrinsic widths. Width0 uses natural width. Fixed-width labels ellipsize to one line while retaining complete accessible names. Items can display20px icons instead. Navigation skips disabled items; RTL mirrors physical placement and horizontal arrow direction.

## Horizontal scrolling and GPU cache

IsFixedWidth defaults to true. False retains intrinsic content widths within the Width viewport. Drag, horizontal wheel and vertical wheel pan content. Drag release does not activate the segment below the pointer. Programmatic and arrow-key selection reveal the accepted item before semantic publication within the same frame. TabList and SegmentedControl share horizontal input.

The widget owns a UiPaintGroup snapshot. A UV camera reads the visible region; a linear opacity mask spans one quarter of each edge only when content is hidden on that physical side. It does not cover content with an assumed canvas color. Masking applies once after internal overlap composition, preserving arbitrary backgrounds and RGB text coverage. Forced colors disable fading.

Offset, UV and real-background changes do not recapture content or remeasure text. Fonts, layout, palette, enabled/unread/focus state and animated selection poses invalidate actual paint content. Icons report content changes through WithContentVersion or their provider. Unversioned custom painters cannot promise static content and are not treated as immutable snapshots. Widget disposal releases the owned cache.

## Styles and motion

Style supports NeutralOverNavBarPill(default), NeutralOverCanvasPill, BrandOverNavBarPill and BrandOverCanvasPill. Standard light/dark enabled/disabled tokens cover16 combinations; authoritative forced colors take precedence. Deprecated primaryPill/onBrandPill aliases, iOS darkElevated and complete custom-brand token overrides are not exposed.

Metrics are font15, line20, minimum height32, external horizontal inset16, content padding16/6, corner16 and unread dot6 with6/3 offsets. HorizontalInset and UnreadLabel are configurable. Selection moves over200ms with cubic-bezier(0,0,.58,1). ReducedMotion snaps immediately; settled transitions schedule no animation frames.

## Running and qualification

Run `.\run-gallery.ps1` from the repository root and open **Fluent controls → Input and selection → SegmentedControl**. Four style samples and a fifth horizontal-scroll sample each show source reflecting current options.

[Cache/mask evidence](../artifacts/fluent-segmented-cache-2026-11-18/result.json), [input evidence](../artifacts/fluent-segmented-scroll-2026-11-18/result.json), [shared mask/UV contract](horizontal-opacity-mask.md). Actual Linux execution, pinned RC3 NativeAOT, external assistive technology and UIKit screenshot equivalence remain unverified.

Official pinned reference: [Microsoft implementation](https://github.com/microsoft/fluentui-apple/blob/03928222b0fde3621fd5b6ea5c7ff1aeefdc1f95/Sources/FluentUI_iOS/Components/SegmentedControl/SegmentedControl.swift), [tokens](https://github.com/microsoft/fluentui-apple/blob/03928222b0fde3621fd5b6ea5c7ff1aeefdc1f95/Sources/FluentUI_iOS/Components/SegmentedControl/SegmentedControlTokenSet.swift). Previously captured official sources were reused.
