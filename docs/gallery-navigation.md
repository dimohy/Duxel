# Duxel Gallery navigation

HeadsUpDisplay now provides the centered portal and five live-source examples, carrying NEW. Linkage is47/55 with8missing. New single/range Calendar work is a Duxel extension and is not counted as a completed menu before its UI is connected.

Run `./run-gallery.ps1` on Windows, or `./run-gallery-linux.ps1` with the existing explicit Linux font/runtime arguments. Existing observer launchers and internal project/cache paths remain compatible. The Windows launcher writes build progress and errors to the invoking console.

## Gallery language

Open the **Settings** gear, then select **English**, **Korean**, **Japanese**, or **Simplified Chinese**. Select **Apply** to switch languages, or **Cancel** to keep the current language. The selected radio button marks the current language when Settings opens. The Gallery starts in the matching system language on first launch and uses English for unsupported system languages, including Traditional Chinese. It saves the choice independently of the system language.

The interface redraws with the selected language while staying on the same destination. The current theme and toolbar preferences carry over. Interactive example state is recreated when switching languages. Fluent component names stay recognizable as C# API names. English phrases serve as source keys and fallbacks. The embedded Gallery catalog provides Korean, Japanese, and Simplified Chinese translations to both hosts. Standalone component pages, Breadcrumb, DataGrid, NavigationBar, SegmentedControl, Select, SearchBox, SpinButton, TextField, Popover, Tooltip, InfoLabel, Pop-up input, Shimmer, ProgressIndicator, ActivityIndicator, Skeleton, Toast, HeadsUpDisplay, MessageBar, CardNudge, and Carousel use that catalog for the controls and the display strings copied with their C# examples.

The Menu example includes the export button, its PDF and PNG commands, and the matching C# source. The localization audit collects text both inside and outside the viewport and checks copied C# for every registered page. Open menus and change example state to verify text that appears after interaction.

Applications can use the same localization API from `Duxel.Core`. Supply a culture, source string, and translation for each entry. Resolve text or format arguments using the application's selected culture.

```csharp
using System.Globalization;
using Duxel.Core;

const string source = "Selected files: {0:N0}";
var catalog = new UiLocalization([("fr", source, "Fichiers sélectionnés : {0:N0}")]);
var culture = CultureInfo.GetCultureInfo("fr-CA");
var caption = catalog.Format(source, culture, 1234);
```

The catalog checks the exact culture followed by its parent cultures. The source string is used when a translation has not been supplied. Locale storage, translation data, and settings UI belong to the app. Gallery bundles [Noto Sans CJK](../samples/Duxel.Gallery/Assets/Fonts/NotoSansCJK/README.md) for the four interface languages.

## Where to find a feature

| Menu | Interactive examples |
| --- | --- |
| Start | Overview and Feature index. Select any listed example to open it directly. |
| Fluent controls | Actions and navigation, input and selection, content and lists, people, alerts and progress, text and graphics, and themes. |
| Duxel widgets | Date and time, SVG, Markdown, localization, layouts, transforms, virtual lists and grids, and pop-up input. |
| System integration | Open, save, and folder-selection dialogs. |
| Development tools | Rendering, repaint regions, retained measurements, and the immediate-mode API examples. |

The Overview starts with a feature-index action, followed by cards that open representative controls, graphics, and document examples. The feature index reads the same routes as the sidebar. It marks planned Fluent components and also lists the available Fluent factories by example. Rendering, transforms, text input, and other APIs share examples when they are used together.

The fixed sidebar header contains the gallery title and component/missing counts. The **Light theme** switch belongs to the fixed right-hand toolbar alongside statistics, V-Sync, event-wait and repaint highlighting. Off selects dark; on selects light. The menu and page content scroll independently; very narrow toolbars scroll horizontally.

The menu lists the55 canonical component names from the source catalog inside seven functional groups, alphabetically within each group. Start, additional samples and development tools remain separate. A Fluent vector icon precedes each group heading instead of repeating on every child entry. All13 missing factories remain in their normal category with a **Planned** badge; selecting one shows **Planned component**. Badge status is also accessibility help text and does not disable selection.

Theme and status options remain fixed at the upper-right while the content scrolls.

Each component destination has its own persistent example. Twenty-one formerly shared destinations now have independent factories; combined regression examples are grouped separately. There are 55 canonical components, 71 existing/planned page objects and 71 distinct routes. This does not mark missing factories or full component fidelity as implemented.

The theme switch preserves page selection, actual counter state and active system forced colors. `GetStyle(includeRequested: true)` reads the latest queued theme so two clicks in one ordered input packet are both processed. The default `GetStyle()` remains the applied scoped palette. App render work captures the current applied background instead of the startup background.

2026-11-17 validation:556 gallery contracts (including66 focused Nav checks),4,642 general widget checks,378 menu checks and197 existing tab contracts pass. The final normal Windows run acknowledges68 presented scenarios including actual secondary-action and theme invocation. Linux host compilation has zero warnings/errors. Presentation acknowledgement is not pixel equality or physical OS keyboard proof. Native Linux, original pinned NativeAOT and full Fluent qualification remain open. [Verification](../artifacts/drawer-2026-11-17/verification.json).

The official [Fluent React gallery](https://storybooks.fluentui.dev/react/?path=/docs/components-nav--docs) is the reference for component-name navigation. The functional grouping is this gallery's arrangement. The gallery now consumes the shared `Fluent.Nav` and `ScrollView`. Category headings collapse/expand and retain their descriptions. Open `Actions and navigation → Nav` for the independent example.
## Existing example names and component menus

| Existing example page | Menu entries |
| --- | --- |
| Fluent | Integration examples → Fluent · combined. Individual controls have their own Fluent destinations. |
| Typography | Text |
| Input and IME | Field, Input, Label, Textarea |
| Rating | Rating |
| Avatar / Avatar group / Persona | Avatar / AvatarGroup / Persona |
| Dropdown | Combobox, Dropdown, Tag |
| Accordion / List / Card / Breadcrumb | Accordion / List / Card / Breadcrumb |
| Search box / Spin button / Toolbar | SearchBox / SpinButton / Toolbar |
| Info label / Message bar / Skeleton | InfoLabel / MessageBar / Skeleton |
| SVG / Markdown / Widget transforms / Virtual list / Pop-up input | Additional samples |
| Rendering / Measurement reuse / Repaint observer / Implementation scope / Legacy API | Development tools |

## Shared Nav API

```csharp
var selected = new UiState<string?>("home");
var nav = Fluent.Nav("App navigation", selected, new UiNavGroup[]
{
    UiNavGroup.SectionHeader("controls", "Controls"),
    new("start", "Start", new UiNavItem[]
    {
        new("home", "Home"),
        new("planned", "Planned feature", Badge : new("Planned"))
    }, Description : "Introduction and examples")
});
```

`UiNavGroup` provides one category level. Nav retains expansion, controllable through `SetExpanded` and `IsExpanded`. It copies menu data and borrows icon painters and page bodies. Up/Down and Home/End move focus; Enter or a click selects a destination. Left/Right collapse, expand and enter categories, with reversed horizontal meaning in RTL. The common ScrollView reveals offscreen keyboard targets. Collapsing a category preserves its selected destination and model.

Call `nav.Render(ui)` followed by `nav.RenderPage(ui, "home", render)` in the same frame to render the selected borrowed page once and associate accessibility nodes. Nav exposes Navigation, destinations Link, disclosure categories Button with expansion state, and the body Pane. `RenderPage` is optional when the host manages content independently. Existing TabList Tab/TabPanel contracts are unchanged.

Long labels and category descriptions wrap in narrow menus. Measurement is retained by width, typography, scale and expansion; changing selection alone does not remeasure. Providing a factory does not qualify complete official fidelity. At widths of 640 UI units or less, the Gallery uses an overlay Drawer opened by **Open navigation**; choosing a different destination closes it. Wider layouts keep the fixed sidebar. [Official Nav usage](https://fluent2.microsoft.design/components/web/react/core/nav/usage).

## Destination secondary actions

Set `UiNavItem.SecondaryAction` to one `UiNavSecondaryAction` for an operation independent of page navigation. Supply an explicit icon, accessible name and callback. Categories cannot have secondary actions.

```csharp
var pin = new UiNavSecondaryAction("Pin Home", () => Console.WriteLine("Pinned"),
    UiMenuIcon.Create<PinIcon, PinFilledIcon>());
var home = new UiNavItem("home", "Home", SecondaryAction : pin);

[UiFluentIcon("pin", UiFluentIconStyle.Regular, 20)]
readonly partial struct PinIcon;
[UiFluentIcon("pin", UiFluentIconStyle.Filled, 20)]
readonly partial struct PinFilledIcon;
```

The button paints on row hover or row/button focus. It remains an accessible Button with its name, Invoke and Expand/Collapse while visually idle. Primary and action accessibility bounds match their separate pointer targets. The direct callback and contextual menu do not change Nav selection.

Tab moves destination → action → outside Nav; Shift+Tab returns from action to destination. The outgoing horizontal arrow enters the action and the incoming arrow returns, reversed for RTL. Up/Down/Home/End navigate destinations from the action as well. Enter, Space and accessible Invoke execute the same callback.

Right-click a row or press Shift+F10/the context-menu key while focused to open the shared Fluent menu containing that action. Accessible Expand on the action button opens this menu too. Escape or invocation restores its calling destination/action focus. Collapsing the category closes the menu and restores category focus. `IsContextMenuOpen` reports logical open state rather than the last painted menu node; a dismissed menu accessibility subtree retires in the following frame.

Example: `./run-gallery.ps1` → **Actions and navigation → Nav** → **Pin Overview**. Compare pointer, Tab, arrows, right-click and Shift+F10; the displayed execution count increments once and survives theme/page changes. The caller owns icon painters and callbacks; Nav releases its menu and Tooltip state. Custom `IUiNavPresentation` implementations must also arrange and paint secondary actions.


## Drawer and compact navigation

`Fluent.Drawer(title, content, options : new(...))` owns its content and optional footer actions. `Open`, `OpenChanged`, `Show()` and `Close()` control visibility. `UiDrawerOptions` supports overlay/inline hosting, logical Start/End/Bottom placement, RTL, 320/592/940/full standard extents, explicit extent, modal and light-dismiss behavior, and fixed header/footer with a scrolling body. Inline hosting requires finite cross-axis space or `InlineCrossExtent`.

Overlay entry/exit translates retained content without scaling it; completed motion stops requesting frames. Inline allocation currently changes immediately; animated inline reflow and full platform conformance remain pending. `ScrollBody : false` allows an already scrolling, finite-height body such as Gallery navigation. Hidden navigation hosts call `RenderPage(..., navigationVisible : false)` so the named Pane does not reference an absent Link.

Run `./run-gallery.ps1` → **Alerts and progress → Drawer** for overlay and inline examples. The compact Gallery keeps the theme control and groups inside its navigation Drawer. Input models survive dismissal and theme changes. Official guidance: [Fluent Drawer](https://fluent2.microsoft.design/components/web/react/core/drawer/usage). Verification: [Drawer record](../artifacts/drawer-2026-11-17/verification.json).


2026-11-17 Drawer hosting verification: 564 Gallery checks include eight additional GPU/interaction checks. Captures cover compact navigation closed/open in dark/light themes, selection, explicit dismissal and both Drawer sample modes. The normal Windows runner passes 72 presented scenarios, including open/close/reopen/save with actual accessible commands, visible focus inside the overlay and focus restoration after close. Linux host compilation passes; native Linux was not rerun. Product modules are byte-identical to the prior Drawer build, so prior core regression results are reused. Inline allocation animation remains pending. [Evidence](../artifacts/drawer-hosting-2026-11-17/verification.json).


## Independent examples and equivalent source

Every Gallery route has a distinct persistent page; duplicate page destinations fail startup validation. Twenty-one formerly shared component destinations now have standalone examples. Combined regression pages remain under **Combined examples**. Each page displays a selectable, read-only C# source pane below its live example, with independent vertical/horizontal scrolling.

Standalone source comes from `CallerArgumentExpression` on the exact executed factory. Existing complex examples display their actual `.cs` file embedded at build time. This requires no repository files at runtime. Complex files use the Gallery project's supporting types and references. Planned components explicitly report that executable source is unavailable.

`UiViewHost` borrows an `IUiView` and hosts sequential rendering inside finite declarative bounds without assigning the entire allocation to each leaf. Child clips, popup tiers and nested windows retain their own geometry. The caller owns the borrowed view. Inline Drawer uses the shared `UiFloatTransition` for allocation and clipping, preserves full-size content, reverses from the sampled extent and retires interaction immediately on close. Initial open inline placement is immediate; later changes animate unless `MotionEnabled` is false. [Verification](../artifacts/gallery-source-2026-11-17/verification.json).

## Shared page headings and application name

Every destination uses its sidebar label as the page heading. A short description sits 6 UI units below the title, and 28 units separate the header from the body. Each example keeps its settings, status and preview together, followed by the source for that example. Overview, Scope and unimplemented pages omit source.

Gallery branding and component counts form the fixed header above the left navigation. The content area has its own top header for theme, diagnostics and options. Navigation scrolls independently. Overview provides category cards with working example links. Windows project and executable names are `Duxel.Gallery`; Linux uses `Duxel.Gallery.Linux`. Run `./run-gallery.ps1` or `./run-gallery-linux.ps1`; existing launchers remain supported.

## Section titles and categories

`UiNavGroup.SectionHeader(value, label)` creates a visual label without a destination. Headers cannot contain items, icons, descriptions or disclosure behavior. They do not accept clicks or keyboard focus and remain passive accessibility labels. Categories retain their icons and disclosure controls. Subitems use 46 UI units of leading inset, mirrored in RTL. Fluent section titles use Caption1Strong with 8 units of top/bottom margin and honor light, dark and forced colors. Sections do not add another destination nesting level.

Sources: [official Nav usage](https://fluent2.microsoft.design/components/web/react/core/nav/usage/), [section header styles](https://raw.githubusercontent.com/microsoft/fluentui/master/packages/react-components/react-nav/library/src/components/NavSectionHeader/useNavSectionHeaderStyles.styles.ts), [subitem styles](https://raw.githubusercontent.com/microsoft/fluentui/master/packages/react-components/react-nav/library/src/components/NavSubItem/useNavSubItemStyles.styles.ts).

The `gallery-localization-audit` graphics test opens each registered Gallery page in all four languages. Its JSON Lines output records the accessible text and C# regions that contain Hangul, along with the number of source regions reached. Use the report to locate mixed-language content and intentional multilingual examples.

## Current values and example code

All22 standalone examples share GallerySample and UiCodeView. The16 interactive factories update current initializers; the6 fixed examples retain their code. Factory bodies come from the lambdas actually compiled. Unchanged values reuse strings and syntax. TagPicker tracks query and selected array; Combobox tracks query and selected value. The standalone examples use the selected Gallery language for descriptions, controls, and displayed C# literals. Factory names and sample identifiers stay stable across languages.

The gallery-component-source-only mode accepts optional comma-separated page names in its third argument, such as TagPicker,Combobox. Omitting it runs all22. Each finished page writes completed-pages.txt and source snapshots. Selection is explicit, not a global automatic test cache.

This qualification reuses13 successful cases with unchanged product source, then verifies8 previously unverified cases and1 corrected TagPicker request separately. AddToSelection preserves existing tags; Select replaces selection. Close suggestions before invoking an outside dismiss button. A Label-linked input uses the displayed label as its accessible name. Separate GPU presentation passes268 checks. Evidence: artifacts/gallery-factory-state-2026-11-17/coverage-and-reuse.json.

2026-11-17: FluentProvider is the latest addition; NEW moves from TagPicker to this destination. Open the theme/settings group for three live-code examples. [API](fluent2-provider.md).


Latest menu addition batch: Shimmer only. NEW moves from FluentProvider to Shimmer. The notifications/status group contains the live loading-to-content example and adjacent source.


Expanded the desktop sidebar maximum from224 to280 logical pixels. It uses280 in the default Gallery window and scales with available width below that. The existing drawer breakpoint at640 is unchanged.


Latest addition batch: ProgressIndicator only. NEW moves from Shimmer to ProgressIndicator. The notifications/status group has separate determinate/waiting examples and source.


Latest addition batch: ActivityIndicator only. NEW moves from ProgressIndicator to ActivityIndicator. Each size owns its adjacent source.


The latest batch is DatePicker, TimePicker, DateTimePicker and Holiday provider. Those four own NEW. Duxel date/time navigation also includes Calendar/RangeCalendar; every example has adjacent source matching current settings.

Rendering now ends with the horizontal opacity-mask/cached-camera example. Backdrop, mask and camera controls retain the capture count and update adjacent live source. Rendering contains6 independent source examples.
