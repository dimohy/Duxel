# Fluent menu presentation and shared popup

System contrast colors now apply to Fluent menu surfaces, rows, checked marks, icons and secondary text. Rest uses Canvas/CanvasText, disabled items use GrayText, and hover/press uses Highlight/HighlightText, including locally hovered secondary text. The one-pixel surface border uses CanvasText. Context-aware `UiFluentMenuPalette.Resolve`, `Icon` and `Secondary` overloads retain the original scheme-only APIs.

`IUiMenuPresentation.PaintFocus` is an optional overlay callback. The common popup renderer invokes it after all item drawing and after the item viewport clip closes, while retaining the outer popup clip. Fluent contrast focus uses the official two-pixel outer Highlight ring, so it stays visible when the focused item is also hovered. Existing presentations inherit an empty callback; ordinary Fluent focus keeps its previous paint path. References: [MenuItem forced colors](https://raw.githubusercontent.com/microsoft/fluentui/master/packages/react-components/react-menu/library/src/components/MenuItem/useMenuItemStyles.styles.ts), [outer focus geometry](https://raw.githubusercontent.com/microsoft/fluentui/master/packages/react-components/react-tabster/src/focus/createFocusOutlineStyle.ts), [surface border](https://raw.githubusercontent.com/microsoft/fluentui/master/packages/react-components/react-menu/library/src/components/MenuPopover/useMenuPopoverStyles.styles.ts).

The 132 GPU contracts cover LTR/RTL, checked/unchecked content, eight states including secondary hover and simultaneous focus/hover/press, exact literal-role output and actual popup accessibility actions. Final validation passes 445 including the previous 313. Ordinary Menu 216, Dropdown 41, Combobox 84 and scrollbar-motion 44 checks pass; all 281 ordinary RGB captures match the pre-change baseline. The long-menu geometry replay now uses the existing `CaptureGeometry` clock-freezing helper for the two frames being compared, preserving exact whole-frame comparison and animation elsewhere. Evidence: `artifacts/menu-contrast-2026-11-16/verification.json`. Dropdown/Combobox option rows now have a separate system-color implementation, retaining Canvas hover instead of copying Menu Highlight. See [option roles](fluent2-dropdown.md). Full browser/native/OS acceptance and shadow16 remain open.

Current tab composition: [TabList overflow](fluent2-tab-overflow.md) now consumes the shared trigger/popup engine. The remaining full Menu conformance items below still apply.

Last synced: 2026-11-16

`Fluent.Menu` creates `UiFluentMenu`, using the same input, dismissal, focus and accessibility engine as `Dux.Menu`. Trigger rendering delegates to the shared popup method; there is one popup implementation. The initial Menu connection recorded 36 exposed names (17 official top-level names plus 19 variants), leaving 38 of 55 top-level names unconnected at that time. See the [current catalog scope](fluent2-scope.md) for later connections. Factory connection does not establish full state, appearance or accessibility conformance.

```csharp
using var menu = Fluent.Menu("Actions",
    [new("open", "Open"), new("disabled", "Unavailable", false), new("save", "Save")],
    value => Console.WriteLine(value));
```

The pinned reference is Fluent UI `8830c10bfb2f4c1bdd540287b09713b9679cd1ad`: [MenuItem](https://github.com/microsoft/fluentui/blob/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-menu/library/src/components/MenuItem/useMenuItemStyles.styles.ts), [MenuPopover](https://github.com/microsoft/fluentui/blob/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-menu/library/src/components/MenuPopover/useMenuPopoverStyles.styles.ts) and [MenuList](https://github.com/microsoft/fluentui/blob/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-menu/library/src/components/MenuList/useMenuListStyles.styles.ts). The implementation uses regular Type300 text, radius 4, minimum surface width 138, maximum 300, 5px row inset (4px surface padding plus 1px transparent border), 32px rows, 2px row gaps and 8px text inset (6px item padding plus 2px content padding). Light/dark rest, hover, pressed and disabled colors come from the independent pinned token fixture. RTL options affect logical placement and text alignment.

`UiMenuOptions.ItemSpacing` and `MaximumWidth` also support custom presentations. Invalid spacing and widths fail before mutation. `IUiMenuPresentation.DefaultOptions` selects presentation defaults; `PaintItem` receives those options explicitly. An open menu resolves all queued item focus requests before painting or publishing items, so one frame cannot paint two focused rows because of a later focus request. A disabled owner closes without restoring focus to its disabled trigger.

Run `./run-gallery.ps1`, choose **컨테이너**, scroll to **Menu 공통 동작**, and use **Fluent 메뉴 열기**. The common theme menu remains alongside it for comparison; both use actual callbacks and the selected-action text. Coverage maps the new factory to this page and validates the mapping at startup.

Evidence: `artifacts/menu-composition-2026-11-11/verification.json`. CPU 57 checks cover both presentations, official color data, actual geometry, maximum width and generated factory discovery. GPU 96 checks cover both presentations, both color schemes, MSAA 1/4, glyph visibility, acquired-image reuse, removal and independent pixel checks for actual Fluent hover/pressed/disabled backgrounds. Each presentation also has an external Windows UIA client with 4 checks and an owned host with 2 checks. Observer 53 and focused accessibility/scheduling/tab/overflow/widget-tree regressions remain separate evidence. Exact managed SDK 11.0.100/runtime 11.0.0 are used; NativeAOT remains unqualified.

Remaining scope includes exact shadow16, actual OS contrast transitions and full focus fidelity, nested items and multiline subtext. The later sections describe checkbox/radio items, icon/secondary content, first-character navigation, large-menu scrolling, trigger expansion and menu automation events. The pinned MenuList uses first-character cycling rather than cumulative typeahead. TabList overflow integration is documented separately. No approximation is substituted for the missing shadow, and no full Fluent completion or speed improvement is claimed. See [common menu interaction](menu-foundation.md) for keyboard and lifetime contracts.

## Icons and trailing information — 2026-11-11

`UiMenuOption.Icon` accepts `UiMenuIcon.Create<TRegular, TFilled>()`, reusing compiled Fluent TTF glyphs. Enabled hover selects the filled glyph and official brand icon color; disabled rows retain regular glyphs. `SecondaryContent` is nonempty single-line trailing information, exposed as HelpText while the action Name and first-character navigation remain based on Label. It does not register a shortcut. Checkbox/radio factories return records, so use `with { Icon = icon, SecondaryContent = "PNG" }` to decorate their result.

The shared layout reserves a 16px check slot plus4px gap and a20px icon slot plus4px gap across all rows when those columns are present. Fluent uses6px root inset,2px content inset and12px secondary text with20px line height. Secondary text has its own local hover color; hovering the label does not change that color. Label and secondary text have separate clips, with intrinsic trailing width and remaining primary width. RTL mirrors the columns. Both theme/Fluent presentations use this layout. Custom presentations must implement the structured UiMenuItemContent overloads; missing support fails explicitly. UiControlContext.IsHovered is a read-only clipped pointer query, without registering input/focus or requesting frames.

Run `./run-gallery.ps1` → **Menu** → **Menu 공통 동작** → **아이콘 메뉴 열기**; **메뉴 방향 : LTR/RTL** switches direction. The popup includes plain, checkable, missing-icon and disabled rows. Evidence: artifacts/menu-content-2026-11-11/verification.json. Independent GPU references draw the real pinned glyph and12px/20px caption separately for pixel comparisons, including local hover. Existing tab icons share the compiled pair cache. This unit uses icon fonts; the separately requested SVG image/widget goal is tracked in [SVG support](svg-support.md).

## Long menus and first-character navigation — 2026-11-11

Menu surfaces fit the viewport height and optionally accept `MaximumHeight`. Overflow rows use content clipping plus the existing vertical scrollbar and wheel processing. Arrows, Home, End and character navigation reveal their destination in the same frame. Reordering items preserves focus by stable value and reveals its new position.

```csharp
var menu = Fluent.Menu("Actions", items, Select,
    new UiMenuOptions(138, 32, 5, ItemSpacing : 2, MaximumWidth : 300)
    { MaximumHeight = 210 });
```

Printable Unicode input moves to the next item with a matching initial character, ignoring case. Repeated input cycles matches; Korean and supplementary-plane characters are supported. Ctrl, Alt and Super modified input is excluded. Space keeps its activation behavior. This is first-character navigation, without a cumulative search buffer. Navigation does not invoke items and cancels a pending Space press. Stationary menus schedule neither periodic frames nor search timers.

The shared behavior covers theme menus, Fluent menus and tab overflow. In the observer use Containers → Menu common behavior → 긴 메뉴·문자 탐색, then wheel, scrollbar, Z and Enter. The optional character navigation follows the [WAI menu pattern](https://www.w3.org/WAI/ARIA/apg/patterns/menubar/). Full Fluent visuals, submenus, checked items and complete expansion/scroll UIA contracts remain pending.

## Menu scrolling accessibility — 2026-11-11

Overflowing menus publish shared `UiAccessibilityScrollInfo` and Windows Scroll support for percentages, relative steps and measured view sizes. Rows expose ScrollItem so offscreen items can request `ScrollIntoView`. Runtime identity is preserved and required follow-up frames complete reveal. Existing scroll property notifications report changed positions and suppress unchanged replay.

A popup uses independent screen coordinates even when its logical parent is a ScrollView. The common reveal path now accepts an independent scroll boundary, preventing menu reveal from moving its outer page. Shrinking the list to fit withdraws Scroll and row ScrollItem capabilities and publishes canonical native -1%/100 view-size values.

Direct retired-provider reads and external mutation requests return element unavailable. On the current Windows UIA client a retained pattern getter was observed to return S_OK/0. External reads alone do not prove provider lifetime. The first expectation failure is preserved; direct provider return values, external mutation rejection and reopened-popup reveal are checked separately. This does not qualify screen-reader narration or full menu conformance.

Evidence: artifacts/menu-scroll-uia-2026-11-11/verification.json. The separate client verifies actual first/last MenuItem reveal, percentage-change notifications, invocation, rejected retired mutations and reopening. References: [Scroll pattern](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-implementingscroll), [ScrollItem](https://learn.microsoft.com/en-us/windows/win32/api/uiautomationclient/nf-uiautomationclient-iuiautomationscrollitempattern-scrollintoview). Menu expansion/open-close automation events and remaining Fluent contracts remain pending.

## Expansion state and menu events — 2026-11-11

Actual menu buttons and tab-overflow triggers publish Collapsed/Expanded through `UiAccessibilityNode.Expansion`. Ordinary tabs do not gain this capability. `UiAccessibilityAction.Expand` and Collapse are idempotent; repeated requests neither toggle menus nor invoke actions. Disabled, retired and leaf sources reject requests with the corresponding error.

`UiAccessibilityTree.ExpansionChanged` reports retained final state/capability changes after publication, outside locks. Initial discovery and unchanged frames do not fabricate change notifications. Success with `wait : false` means accepted; inspect the next frame/state notification for application. Windows ExpandCollapse methods use the existing queue and wait for publication. Synchronous requests from the render producer return Busy.

MenuOpened follows publication; MenuClosed precedes unpublication while the previous source remains queryable. Menus and Tooltip share lifecycle calculation. Windows provides pattern10005, availability30028, state30070 and menu events20003/20007. Closed-event caches retain source name/identity and offscreen state, including HWND shutdown. This does not qualify full menu conformance or screen-reader narration.

Evidence: artifacts/menu-expansion-2026-11-11/verification.json. Coverage includes repeat requests, invalid enum/leaf, disable/removal/tree shutdown, the actual observer, separate Fluent-menu/tab-overflow clients and Tooltip shutdown regression. References: [ExpandCollapse contract](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-implementingexpandcollapse), [Menu events](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-supportmenucontroltype).

## Checkbox items — 2026-11-11

`UiMenuOption.Checkbox(value, label, UiBinding<bool>, enabled : true)` borrows caller-owned state. Pointer release, Enter, Space release, Invoke and Toggle update it before the menu callback. A checkbox keeps the popup open; an ordinary action still closes it. Shared binding instances are read once per open frame and all peers publish the final state in that frame. Closed menus do not read checkbox bindings. Unchanged replay neither writes state nor schedules animation or repeats toggle notifications.

Both built-in presentations reserve a shared 20px check column for all rows when any checkbox exists. Fluent uses the official Checkmark16Filled glyph at logical start, including RTL. A custom `IUiMenuPresentation` must implement the `UiMenuItemContent` measurement/paint overloads to support checkbox menus; unsupported content fails explicitly. Existing label-only menus keep their original contract.

`UiAccessibilityNode.Checked` is nullable: null means no Toggle capability. `ToggleChanged` reports retained final state/capability changes after publication, outside locks. Windows exposes pattern10015, availability30041 and state30086, with generated COM `IToggleProvider` (Toggle/get_ToggleState). Disabled, hidden and retired mutations are rejected through the existing request queue. Synchronous calls from the producer thread return Busy.

Run `./run-gallery.ps1` → **Menu** → **Menu 공통 동작** → **체크 메뉴 열기**. Toggle details/grid, use **설정 마침** to close, and navigate away/back to check retained state. Submenus, secondary text, shadow16, forced colors and full narration remain unqualified. Reference: [pinned checkbox hook](https://github.com/microsoft/fluentui/blob/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-menu/library/src/components/MenuItemCheckbox/useMenuItemCheckbox.tsx). Evidence: artifacts/menu-checkbox-2026-11-11/verification.json.

## Radio groups — 2026-11-11

```csharp
var selection = new UiState<string?>("name");
var sorting = new UiMenuRadioGroup("Sort by", selection);
using var menu = Fluent.Menu("Sort",
    [UiMenuOption.Radio("name", "Name", sorting), UiMenuOption.Radio("date", "Date", sorting)],
    value => Console.WriteLine(value));
```

A group borrows one string binding. Its contiguous rows are mutually exclusive, and separate groups retain independent selections. Group names are unique within a menu; separate group instances cannot reuse a binding. Invalid composition fails before replacing valid items. A binding value absent from the current rows produces an empty group without substituting a default. Each group binding is read once per open frame. Radio marks reuse the official checkmark column, including RTL; radio items expose Selected, not checkbox Toggle.

Pointer release, Enter, Space release, Invoke and Select commit peer state before callback and close the popup, following the pinned default [radio hook](https://github.com/microsoft/fluentui/blob/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-menu/library/src/components/MenuItemRadio/useMenuItemRadio.tsx). An already selected action still closes and invokes once, without a redundant binding setter. Deselect of a selected radio and AddToSelection beside another selected peer fail; Deselect of an unselected row is a no-op. Checkbox items retain their distinct keep-open behavior.

The accessibility tree contains named MenuGroup containers (Windows Group50026), with Selection10001 and child MenuItem SelectionItem10010. SelectionContainer resolves the real group. CanSelectMultiple is false; IsSelectionRequired reflects whether the current rows contain the bound selection. Final selection/property notifications reuse the shared selection publication engine. Transient menu notifications use immutable event sources and read-only selection state so asynchronous UIA caches can finish after dismissal; event snapshots reject mutations. The live providers still retire on close.

Run `./run-gallery.ps1` → **Menu** → **Menu 공통 동작** → **라디오 메뉴 열기**. Select sorting and view independently, reopen after each change, and leave/return to the page. Evidence and remaining scope: artifacts/menu-radio-2026-11-11/verification.json. Exact NativeAOT and full narration remain separate gates.


## Parent measurement retention — 2026-11-12

IUiMenuPresentation.CanRetainTriggerMeasurement promises deterministic trigger geometry only. Built-in Theme/Fluent presentations opt in; custom presentations default false. This does not cache popup content or suppress checked/radio binding reads, scrolling, selection or actions. Actual Observer checkbox activation is verified while its parent trigger measurement remains retained.

Evidence: artifacts/form-layout-retention-2026-11-12/verification.json. Observer: ./run-gallery.ps1 → Measurement reuse (측정 재사용).
