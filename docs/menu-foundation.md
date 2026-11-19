# Common menu interaction

Current presentation checkpoint: [Fluent Menu](fluent2-menu.md) adds a generated factory, checkboxes, scrolling and expansion events on the shared popup path. [TabList overflow](fluent2-tab-overflow.md) consumes that path. The initial qualification below remains historical; full Fluent fidelity is pending.

The immediate `MenuItem(label, selected)` used in Gallery's **기존 API** page reserves a fixed 16-DIP vector checkmark slot with a 4-DIP gap. Selection shows the shared Fluent checkmark, while the menu label continues to use the selected text font. The checkmark works with fonts that lack U+2713.

The Windows .NET 11 Vulkan fixture first reproduces the missing glyph in DirectWrite, then reads back selected, unselected and cleared frames in light and dark with MSAA 1× and 4×. Managed and x64 NativeAOT pass 13 checks each and produce eight matching images. Evidence is in `W:/Cache/Duxel/gallery-source-20261117/legacy-menu-gpu-1961/verification.json`.

Last synced: 2026-11-11

`Dux.Menu` creates an owned menu control with a trigger, stable string-valued entries and one action callback. `UiMenu<TPresentation>` separates static measurement/painting from the shared popup, dismissal, input and accessibility pipeline. `UiThemeMenuPresentation` uses the current theme for its surface and the existing Fluent button for its trigger, which requires DirectText and a font-metrics backend. Fluent styling, checkbox/radio items, icons/secondary content, first-character navigation, large-menu scrolling and TabList overflow now share this infrastructure. Nested menus and multiline subtext remain open. The pinned Fluent MenuList uses first-character cycling.

```csharp
using var menu = Dux.Menu("Actions",
    [new("open", "Open"), new("disabled", "Unavailable", false), new("save", "Save")],
    value => Console.WriteLine(value));
// Render menu inside the caller's ordinary widget flow.
```

Down/Up on the focused trigger opens at the first/last enabled item. Enter/Space also opens. Up/Down wrap and skip disabled items; Home/End target the endpoints. Enter invokes; Space invokes on release. Escape cancels a pending Space action, closes and restores trigger focus. Tab dismisses and proceeds through the pre-open tab sequence. Pointer activation occurs on release; outside press dismisses. Opening another common menu closes the previous one. Removing the owner closes its popup at frame completion. No menu animation or periodic frame timer is introduced.

`SetItems(ReadOnlySpan<UiMenuOption>)` copies and validates entries before replacing state, preserves the focused stable value when possible, and cancels pending activation. Empty lists have no open popup. Duplicate/blank values or labels and invalid geometry fail explicitly. `UiMenuOptions` controls minimum width, row height, padding and logical RTL placement. The popup uses shared positioning and popup drawing tiers, independently of the parent clip. Oversized surfaces use the shared clipped scrollbar. Checkbox bindings and their persistent activation contract are described in the current Fluent menu guide.

Menu/MenuItem semantics use UIA control types 50009/50011 and leaf Invoke support. Disabled items reject actions. Closing retires item providers; reopening creates fresh runtime identities, while stale clients stay unavailable. The current guide qualifies Scroll/ScrollItem, trigger expansion, menu opened/closed events and checkbox Toggle. Complete assistive-technology qualification remains pending. The control types follow [Microsoft's identifiers](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-controltype-ids).

Run `./run-gallery.ps1`, choose **컨테이너**, scroll to **Menu 공통 동작**, and open **공통 메뉴 열기**. Select an enabled action and inspect the selected-value text. The adjacent overflow example remains independent; its badge has not become a tab menu.

Qualification is recorded in `artifacts/menu-2026-11-11/verification.json`. Exact managed SDK 11.0.100/runtime 11.0.0: CPU 21, GPU 20 (MSAA 1/4, popup drawing, real pointer invocation, removal and acquired-image reuse), external Windows UIA client 4/host 2, observer 51. Regressions include accessibility 49, tab list 197, scheduling 35 and overflow 26. Initial compilation, missing typography scope and GPU font setup failures are preserved with the corrected results. Focus regressions exposed stale popup blocking registration and trigger press ownership; the shared registration now retires only the closing menu's current rectangle. No NativeAOT or general performance improvement claim is made.

Visual inspection exposed unreadable menu item text in the earlier geometry-only captures. Menu items now measure and paint in the same regular Type300 typography scope; disabled text uses the theme disabled color. The added glyph-ink assertion rejects the previous placeholder result. Final readable images and comparisons are preserved under L:/Cache/Duxel/menu-2026-11-11/gpu-readable.
