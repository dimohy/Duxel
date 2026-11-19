# Priority overflow layout

Last synced: 2026-11-11

`Dux.Overflow` and `Dux.VerticalOverflow` own their item widgets and a caller-provided indicator. They render only the subset that fits the bounded main axis. Hidden widgets keep their instances and model values; they do not render, receive input or publish live accessibility nodes. The indicator is an ordinary widget; this layout does not create a menu.

```csharp
var first = Fluent.Button("First");
first.Size = new(100, 32);
var second = Fluent.Button("Second");
second.Size = new(100, 32);
var overflow = Dux.Overflow([first, second], Fluent.Badge("More"),
    new(Spacing : 8), priorities : [2, 0]);
using var root = Dux.SizedBox(overflow, width : 150, height : 48);
// Render root inside the caller's ordinary widget/layout flow.
```

Higher priorities survive first. Equal priorities collapse from the logical end by default; `CollapseFrom = UiOverflowCollapseDirection.Start` reverses that tie break. Surviving items retain their original order. `RightToLeft` reverses horizontal packing without changing priorities or logical order. Vertical packing proceeds downward.

`UiOverflowLayout.Resolve(items, available, indicatorExtent, visible, options)` is the renderer-independent algorithm. It validates inputs before modifying the first `items.Length` visibility slots. `Spacing` must be finite and nonnegative; available space, item extents and indicator extent must also be finite and nonnegative. The widget requires a finite main-axis constraint and measures children intrinsically along that axis.

The indicator consumes space only when items are hidden. It can cause additional items to collapse. `MinimumVisible` preserves up to that many items, even when they cannot fit: `Result.Fits` then reports false and the parent clip limits visible pixels. An oversized indicator is explicitly clipped and reported through `IndicatorClipped`. No hidden item is silently promoted to a menu entry.

`Items` excludes the indicator. `SetPriorities` requires exactly one priority per item and invalidates retained layout. `Options` also invalidates layout when changed. `Visibility` is a borrowed span, empty before the first arrangement; `Result` is initially null. Both describe the latest arrangement, not a promise about a pending options change. The owner disposes each item and the indicator; hiding does not dispose or replace them. A restored accessibility node receives a fresh runtime identity, and old clients remain unavailable.

Run `./run-gallery.ps1`, choose **컨테이너**, and scroll to **Overflow 공통 배치**. Click a counter, expand/collapse width, promote item 4, and toggle RTL. Returning to the page preserves the counters. The badge is a visibility indicator; reusable Menu and TabList overflow integration are documented separately.

Qualification: `artifacts/overflow-layout-2026-11-11/verification.json`. Exact managed SDK 11.0.100/runtime 11.0.0; CPU 26 checks include 1,200 independent deterministic cases and zero steady-state allocation for 16/512-item resolver calls. GPU 66 checks cover MSAA 1/4, placement, priority, RTL, vertical layout, clipping, zero-width clearing and acquired-image reuse. An external Windows UIA client passes 4 checks and its owned native host passes 3: retired providers stay unavailable while restored widgets retain their model and instance. The observer passes 49 checks, including five new actual sample interactions. These checks do not establish full desktop input, narration, Fluent conformance, NativeAOT qualification or a general performance improvement.

The pinned Fluent reference composes Overflow, Menu and TabList separately: [official overflow tab story](https://github.com/microsoft/fluentui/blob/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-tabs/stories/src/Tabs/TabListWithOverflow.stories.tsx). See [tab overflow](fluent2-tab-overflow.md) for the current composition using this foundation.
