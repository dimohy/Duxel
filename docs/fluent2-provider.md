# FluentProvider

The **Scoped layout direction** Gallery example includes a horizontal RadioGroup. It mirrors option order, indicator and label placement. Left and Right follow the displayed order and switching direction preserves the selection.

The example's Checkbox and Switch mirror their indicator and label positions in a right-to-left scope. The Switch thumb travels in the opposite direction, and both controls preserve their current value when the scope changes.

The **Notification volume** Slider mirrors its fill, thumb and step marks along with pointer tracking and Left/Right navigation. The current value stays the same when the scope changes. Vertical sliders keep their axis, and an explicitly reversed horizontal slider composes its reversal with the scope.

On Windows with .NET 11, 42 direction checks, 3,155 rendering checks and 104 Gallery scenarios passed. All five existing Slider images matched the previous run byte-for-byte. The exported example compiled with the updated value. See `W:/Cache/Duxel/gallery-source-20261117/slider-direction-1956/verification.json`.

Windows x64 managed and NativeAOT each passed 168 Slider checks. Eight LTR/RTL Vulkan mirror comparisons per runtime had zero channel error across light/dark, explicit reversal and MSAA 1×/4×. All 16 direction images matched between runtimes. The WSLg x64 NativeAOT Gallery also passed 104 presentation scenarios and the independent AT-SPI client verified that the Slider accepts a new value in RTL and retains it in LTR. See `W:/Cache/Duxel/gallery-source-20261117/slider-native-1957/verification.json`.

The Linux platform test runs the same direction fixture as Windows. Invoke the Linux test executable with a fresh output directory followed by `slider-direction`. On WSLg Ubuntu x64 with Mesa lavapipe and an unmapped X11 window, .NET 11 managed and NativeAOT each passed all 12 direction checks. The 16 complete frame images matched the Windows managed and NativeAOT references byte-for-byte. See `W:/Cache/Duxel/gallery-source-20261117/slider-cross-host-1963/verification.json`.

On the WSLg Linux x64 NativeAOT Gallery, a separate AT-SPI client toggled both controls, changed the Provider direction and verified the retained states and reversed RadioGroup bounds. All 104 standard Gallery presentation scenarios passed on the same image. Evidence is in `W:/Cache/Duxel/gallery-source-20261117/toggle-native-1955/provider/result.json` and `linux-full/result.json` under the same run root.

The Windows x64 NativeAOT graphics suite passed all 33 Provider direction checks. The same run root contains `windows-direction/result.json` and the combined `verification.json`.

Run `./run-gallery.ps1` → **테마와 설정 → FluentProvider**. Compare light/dark regions and nested themes; change each region's theme or background. Inputs and adjacent code stay synchronized. The fourth example toggles local monospace typography using the fonts already selected for the current platform. NEW follows the Gallery latest addition batch.

```csharp
var name = new UiState<string>(string.Empty);
var provider = Fluent.FluentProvider(
    Fluent.Input("Name", name.Binding),
    UiFluentProviderTheme.Dark,
    new(Padding : new(16), Width : 400));
provider.Theme = null; // Inherit the enclosing theme.
provider.PaintBackground = false;
```

UiThemeProvider is the owning implementation; UiFluentProvider is its generated DSL adapter. Theme accepts an immutable UiFluentProviderTheme. Reuse Light/Dark or construct one from a UiTheme palette, optional UiDesignTokens and optional UiTypographyFonts. Null inherits the current ancestor. Omitting geometry tokens preserves ancestor tokens. Options uses the ordinary UiContainerOptions layout contract.

Measurement and painting share the same scope, restoring parent values on completion and exceptions. Geometry tokens and the full font-role set participate in the common measurement key, including a semibold-only change with an unchanged regular face. As with existing containers, retention follows the child's first prepared arrangement. System forced colors and enclosing disabled opacity remain authoritative. Automatic and controlled Tooltip preserve palette, tokens and font paths through deferred painting. The app theme/title bar are not changed.

This API scopes native UiTheme, geometry tokens and font-family roles. Omitting Fonts inherits the current parent font set. A supplied set selects regular, semibold, bold, optional secondary faces and monospace. Plain control text also uses the scoped regular face. Parent fonts and text paths are restored on exit and exceptions. Local scope entry keeps font caches and global measurement resources. DPI and operating-system text scaling remain authoritative. Full brand and typography token overrides and web CSS/DOM semantics remain open.

Evidence: artifacts/fluent-provider-2026-11-17/result.json. Passed checks are reused after module-hash comparison; only the corrected retention setup and stale Nav/Drawer catalog expectations are rechecked. Actual Linux execution and pinned RC3 NativeAOT remain unrun.

Primary reference: https://fluent2.microsoft.design/components/web/react/core/fluentprovider/usage

## Local font roles

Use installed font paths already selected by the application. The native platform text backend loads them; no additional rendering library is introduced.

```csharp
UiTypographyFonts fonts = appFontOptions.Typography;
var mono = fonts.Monospace ?? throw new InvalidOperationException("Configure a monospace font.");
var localFonts = fonts with { Regular = mono, Semibold = mono, Bold = mono };
provider.Theme = new(UiCompiledDesign.Windows11Dark.Theme, fonts : localFonts);
```

Font-scope evidence: `artifacts/fluent-provider-fonts-2026-11-17/result.json`. Focused inheritance/restoration/cache/Tooltip checks, live Gallery source checks and actual Vulkan glyph comparisons are separate scopes. This is not a full FluentProvider conformance claim.


`provider.Fonts = localFonts` scopes typography without replacing Theme. Null restores Theme.Fonts or inherited fonts; explicit Fonts wins over Theme.Fonts. Measurement/paint/exception restoration remains shared. Thai calendars and editable pickers reuse this scope.

## Local layout direction

`provider.Direction` defaults to `UiFlowDirection.Inherit`. Set `RightToLeft` or `LeftToRight` to override the surrounding scope. A nested LTR provider can override an RTL parent. Direction is part of the layout measurement key. Changing direction recomputes mirrored layout and preserves the widget instance, selected value and input state.

NavigationBar, Nav, Breadcrumb, TabList, Carousel, SegmentedControl, Wrap, ScrollView, Dropdown, Select, Combobox, Tree, Tag, InteractionTag, TagGroup, TagPicker, menu popups, Popover and Tooltip consume this scope. Where these controls expose a `RightToLeft` flag, `true` requests RTL and the default `false` follows the provider. Use a nested LTR provider to keep a region LTR. Deferred Tooltips retain the source direction for placement and drawing. Tree mirrors expand/collapse arrows, and an empty TagPicker field uses the mirrored arrow to reach its adjacent tag. Paragraph shaping and caret movement in editable text use their own direction settings.

Toolbar mirrors the order and horizontal arrow navigation of its actions. SearchBox mirrors its adornments and aligns the placeholder to the leading edge. Open **영역별 배치 방향** at the end of the FluentProvider page. Switch direction, use the toolbar arrow keys, then enter and clear a search. The C# source reflects the current selection, search and direction. [FluentProvider reference implementation](https://raw.githubusercontent.com/microsoft/fluentui/master/packages/react-components/react-provider/library/src/components/FluentProvider/useFluentProvider.ts)

With the fixed .NET 11 RC3 runtime, both managed and NativeAOT Windows x64 runs passed 25 direction checks, 120 Toolbar checks and 144 SearchBox checks. The Toolbar and SearchBox suites matched all 362 RGB GPU readback pairs at MSAA 1 and 4. The saved example source compiles and the Windows Gallery passed 100 presentation scenarios. The Linux target compiles. Evidence: `W:/Cache/Duxel/gallery-source-20261117/provider-rc3-1904/comparison.json`.
