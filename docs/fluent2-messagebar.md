# MessageBar — work in progress (12/13)

Linux now shares the styled paragraph capability with the literal Core consumer;NativeAOT351/334/114 and Observer41/delivered38 pass with delivery `e529777d`. This prerequisite does not complete contract007: MessageBar's owned atomic flow,public direction and whole integration remain open. [Current scope](styled-paragraphs.md).

2026-11-16: Linux uniform-style lines now use private explicit-font Pango shaping and native geometry. Native334, baseline114 and Observer41/delivered38 passed; Windows paragraph29, shaping52, multiline2560 and Markdown312 also pass. Delivery is `7746d0b7`. Contract007 still requires complete mixed-style paragraph composition and wrapping/base-direction integration. [Current scope](linux-text-shaping.md).

Latest line geometry regression:all4 kerning/mixed-bidi caret defects are fixed for uniform-style Windows rows.29 checks cover actual carets,hits,selection fragments and owned child bounds. Full mixed-style/cross-font paragraph shaping and Linux geometry remain pending;contract007 is still incomplete. [Evidence](../artifacts/fluent-messagebar-2026-11-15/paragraph-line-geometry-verification.json).

Current qualification (2026-11-15): constrained-height page crash repair and shared inline line alignment passed155 CPU checks,176 GPU pairs,38 Windows scenarios,41 Linux NativeAOT scenarios and38 delivered-launcher scenarios. [Focused evidence](../artifacts/fluent-messagebar-2026-11-15/inline-alignment-verification.json).

`UiInlineFlowWidget.HorizontalAlignment` provides physical Left/Center/Right alignment. Per-line remaining-width offsets apply consistently to paint, actual child positions and text-span bounds. Wrapping, natural measurement, source/glyph order and existing allocation clipping are preserved. MessageBar construction and Options updates set an actual `MessageBarBody` to Left for LTR or Right for RTL. Arbitrary custom body containers retain their internal alignment.

Paragraph bidi and per-character native accessibility geometry remain unqualified. A shared DirectWrite module fixes all six original Windows shaping failures;37 expanded checks,8 independent TextLayout cases and all11172 modern Hangul syllables pass. Original UTF16 positions are preserved without Windows HarfBuzz dependencies. [Shaping architecture and scope](windows-text-shaping.md). Complete RTL contract007 remains incomplete.

`Fluent.MessageBar`, `MessageBarBody`, `MessageBarTitle`, `MessageBarActions` and `MessageBarGroup` own real child widgets under one official MessageBar parent requirement. Complete paragraph RTL/reflow remains pending. Current Observer options and callbacks are qualified through actual control input and presentation. Windows NativeAOT is a separate original project gate.

## Construction and layout

```csharp
var retryCount = 0;
using var message = Fluent.MessageBar(
    "Connection status",
    Fluent.MessageBarBody("Your work is preserved.", Fluent.MessageBarTitle("Check the connection.")),
    Fluent.MessageBarActions([Fluent.Button("Retry", () => retryCount++)]),
    options: new(Intent: UiMessageBarIntent.Warning, Width: 560));
// Create once; call inside UiScreen.Render.
message.Render(ui);
```

Pass an actual close button or other widget through `containerAction`. Source navigation order is body, secondary actions, container action. The body must honor its allocation. `MessageBarBody(text, title, trailingContent)` combines real title/body/link/input children through shared `UiInlineFlowWidget`. Title weight is 600 and body 400; equal faces kern across child boundaries. Margin, Transform, clipping and ownership use common widget contracts.

`Layout` defaults to `Auto`, comparing real natural and assigned widths before moving secondary actions below the body. `MultiLine` retains that arrangement at wide widths. Impossible `SingleLine`, fixed-slot/padding capacity and required-height allocations fail explicitly rather than silently losing content.

Info, Success, Warning and Error use pinned Fluent Light/Dark colors and 20px InfoFilled, CheckmarkCircleFilled, WarningFilled and DiamondDismissFilled glyphs. `Shape` is Rounded (4px) or Square. `HideIcon` removes the icon and its gap.

`RightToLeft` mirrors root slot positions. Construction and Options updates also apply common `UiWrapOptions.RightToLeft` to a real MessageBarActions slot. Wrap mirrors child X positions across assigned width while retaining child arrays, accessibility navigation, vertical row order and glyph geometry. Vertical Wrap mirrors its horizontal cross axis. Arbitrary child internals and full paragraph bidi are not guaranteed. Existing positional Wrap constructor/deconstruction arguments are preserved.

## Presence, removal and motion

```csharp
using var first = Fluent.MessageBar("Connection", Fluent.MessageBarBody("Connection state"));
using var second = Fluent.MessageBar("Sync", Fluent.MessageBarBody("Sync state"));
using var group = Fluent.MessageBarGroup([first, second], new(Gap: 10), keys: ["network", "sync"]);
group.SetVisible("network", false);
group.SetVisible("network", true);
group.SetItems([new("sync", second), new("network", first)]);
group.SetItems([new("sync", second)]);
group.Render(ui);
```

`SetVisible(index/key, bool)` retains the same child while hiding/reopening it. Call it from dismissal callbacks instead of disposing the child directly. `SetItems(ReadOnlySpan<UiKeyedWidget>)` handles actual insertion/removal/reordering. All keys, instances, types and owners are validated before immediate adoption. The prepared current frame remains coherent; new traversal begins at the next prepared frame. `Items` is the requested collection; `Children` may temporarily include exits.

Input and accessibility retire immediately while the last GPU paint fades linearly for 250ms. Survivors then follow a 250ms OutCubic reflow. Focus moves to the next surviving control, then a previous control, including controls outside the group. Actual removal disposes once after exit paint completes. Requesting the same key/instance before completion cancels retirement. A different instance under a live/exiting key, duplicate keys, shared/foreign-owned children and non-MessageBar items are rejected before adoption. Fully retired keys may own a new instance. Superseded pending children and early group disposal release all adopted resources.

`AnimateEnter` defaults to false. When enabled, later entries slide/fade; initial content never does. `ReducedMotion` applies final values immediately and settled motion requests no frames. `Fade` and `Reflow` use shared `UiTransitionOptions`. General widgets can use underlying `UiPresenceGroupWidget`.

## Title relationship and announcements

The group preserves its explicit name and links `LabeledBy` to its actual owned title. Mounts request Polite for Info and Assertive for other intents. Computed priority changes, remounts and re-enabling request once; mounted edits or intent changes with the same priority do not repeat.

Duxel's default follows the pinned React implementation. The Fluent 2 design guide recommends polite announcements for both Info and Success. To follow the design guide for a Success bar, set `AnnouncementPriority` to `UiAnnouncementPriority.Polite`. [Fluent 2 MessageBar accessibility](https://fluent2.microsoft.design/components/web/react/core/messagebar/usage#accessibility) · [Pinned React implementation](https://github.com/microsoft/fluentui/blob/f9720c35ba893c9630f4c0389d07dc6d6fd6068d/packages/react-components/react-message-bar/library/src/components/MessageBar/useMessageBar.ts#L19-L37)

```csharp
message.Options = message.Options with
{
    AnnouncementPriority = UiAnnouncementPriority.Polite,
    DisableAnnouncement = false,
    AnnouncementText = "Connection status changed."
};
```

Defaults are null, false and null. Null priority derives from intent. Null text uses this frame's prepared accessible body/action leaf names, excluding editable values and aggregate names. Supply explicit text for an exact sentence. Empty actual content invents no fallback; explicit text uses common Unicode validation.

State commits only after successful accessibility publication. Aborted frames do not consume it; removed owners release their state. Stable frames request neither repeated announcements nor rendering. `UiImmediateContext.AccessibilityTree` is borrowed for subscriptions/snapshots: do not Dispose it, and unsubscribe on release. Requests and native reception do not prove screen-reader speech completion.

## Running and evidence

Run `./run-gallery.ps1` on Windows or `./artifacts/linux-present-path-2026-11-15/run-observer.ps1` for the qualified Linux executable, then select **MessageBar**. Controls expose width, multiline, shape, direction, dismissal/reopening, reduced motion and automatic/polite announcements. **Dynamic messages with keys** exposes insertion, removal and reordering. The page scrolls and controls wrap at constrained sizes; announcement diagnostics refresh once after a real request.

Drag the width slider to see the bars reflow. The previews update while you drag, and the displayed sample code takes the final width when you release the slider. Keyboard and accessibility value changes also update the sample code.

See the [plan](../artifacts/fluent-messagebar-2026-11-15/plan.json) and [native evidence](../artifacts/fluent-messagebar-2026-11-15/native-messagebar-verification.json). Windows managed .NET 11 UIA passed 8 client/1 host checks; pinned Linux RC3 NativeAOT AT-SPI passed 9 client/3 host checks. Actual title target identity, Korean/emoji payloads, mount/priority/edit/disable/enable/remount/quiet behavior were verified. Existing explicit announcements passed focused regressions on both platforms; Linux registry retirement was verified. Native frame acknowledgements use a finite test clock, not a throughput benchmark.

The product Core DLL is identical to the prior pixel-qualified cohort, preserving 94 model checks and 152 graphics regression pairs. A fresh 416-check factory/composite profile passed. Linked earlier records cover 424 independent motion GPU pairs and 36 Windows/39 Linux/36 delivered-launcher scenarios. Full RTL, browser pixel identity, Windows NativeAOT, speech completion and physical latency remain unqualified. `artifacts/linux-observer-delivery.json` is the delivery authority; the current delivered executable is 43119a, with the previous 7aa8ce build preserved.

Reproduce Windows checks with `scripts/Test-NativeAccessibility.ps1 -Binary <published Duxel.Graphics.Tests.exe> -Profile MessageBar -OutputDirectory <fresh path>`. See the [native probe guide](../artifacts/fluent-messagebar-2026-11-15/native-probe.md) for Linux commands and explicit fonts. Official source/tokens are pinned in `reference-manifest.json`. AT-SPI relationship ownership follows the [official relation API](https://docs.gtk.org/atspi2/method.Accessible.get_relation_set.html) and [target API](https://docs.gtk.org/atspi2/method.Relation.get_target.html).

Current Observer interaction qualification covers typed width input, six switches, retry and every intent's dismiss/reopen, dynamic insertion/reordering/key removal/first removal, announcement disable/re-enable/priority restoration and settled idle. Fixed and dynamic messages share one option resolver from construction onward, fixing omitted multiline/square settings on dynamic messages. Passed 34 page checks, 38 Windows, 41 pinned RC3 NativeAOT Linux and 38 delivered-launcher scenarios. Presented option on/off steps record width 500/800 and actual layout/shape/direction/motion values while announcement count stays at 7. These are not OS-pointer or new independent pixel qualifications. [Evidence](../artifacts/fluent-messagebar-2026-11-15/observer-controls-verification.json).
