# SearchBox

```csharp
var query = new UiState<string>("정세일");
var search = Fluent.SearchBox("Search", query.Binding,
    options: new(Width: 360, Placeholder: "Search people", ClearLabel: "Clear search"));
search.Options = search.Options with { Size = UiSearchBoxSize.Large, ReadOnly = true };
```

SearchBox reuses the single-line editor, text-element selection, clipboard, undo and IME path. Its search icon is decorative; the clear button appears while the field has focus and is omitted in read-only mode. Clear restores editor focus, has an actual Invoke provider and adds no Tab stop. Empty values still offer clear while focused, matching the pinned default slot behavior. The model binding is authoritative: rejected or normalized writes are read back before painting and accessibility publication.

Three sizes use 24/32/40px heights and 16/20/24px icons, 6/8/10px root padding and maximum width468. Four existing input appearances and logical RTL icon placement are supported. The existing text editor retains its current caret and bidirectional-text behavior; this is not a claim of complete Unicode bidi layout. Finite/narrow parent allocations clip safely. ReadOnly permits focus/selection and blocks changes; disabled uses the common disabled control path. Invalid and Field/Label association reuse existing contracts. MaxLength and non-null single-line text are validated; an invalid authoritative getter fails explicitly.

UIA exposes an Edit control with class `Duxel.SearchBox`, ARIA `searchbox`, Value/read-only semantics, accepted Value change events and a child clear action. Visible value, edit buffer and published value are reconciled in the editing frame. `IUiSearchBoxPresentation` exposes explicit typography/measure/arrange/paint contracts; no second editor, XML parser, renderer, native import or periodic animation is added. The search/dismiss glyphs use generated Fluent **font icons**, not SVG; existing SVG raster/image and icon widgets remain separate supported paths. Search execution/filtering is supplied by the caller through the binding; arbitrary child slots and submission callbacks are not provided by this initial surface.

Run `./run-gallery.ps1` → **검색 입력**, using the header overflow when hidden. Edit Korean text, clear it, change size/appearance/direction, toggle read-only and rejected writes, then switch away and return. State persists. Source and evidence: `artifacts/searchbox-2026-11-13/`; large readbacks: `L:/Cache/Duxel/searchbox-2026-11-13-evidence/`. Managed qualification does not complete fixed RC3 NativeAOT/runtime-image/device/performance or full Fluent conformance.

Pinned official sources: [behavior](https://raw.githubusercontent.com/microsoft/fluentui/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-search/library/src/components/SearchBox/useSearchBox.tsx), [styles](https://raw.githubusercontent.com/microsoft/fluentui/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-search/library/src/components/SearchBox/useSearchBoxStyles.styles.ts), [render](https://raw.githubusercontent.com/microsoft/fluentui/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-search/library/src/components/SearchBox/renderSearchBox.tsx).

Holding the clear button across multiple frames retains editor focus and Tab registration. Releasing inside clears the accepted value once; releasing outside cancels. Clear is one undoable/redoable edit and Korean typing can resume immediately. Auxiliary pointer capture shares the ordinary editor navigation path. Existing rejected-write and read-only contracts remain. Regression evidence: artifacts/searchbox-clear-2026-11-13/verification.json.
