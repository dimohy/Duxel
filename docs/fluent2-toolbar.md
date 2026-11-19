# Toolbar

```csharp
var bold = new UiState<bool>(false);
var toolbar = Fluent.Toolbar("Document tools", [
    Fluent.Button("Save", Save),
    Fluent.ToggleButton("Bold", bold.Binding),
    Fluent.Button("Print", Print)
]);
toolbar.Options = new(UiToolbarSize.Large, Vertical: true, RightToLeft: false);
```

Toolbar owns ordinary widgets in one non-wrapping row or column. It uses the existing arranged-child layout, control input, focus and accessibility paths. Root size affects root padding, while child typography, size, appearance and bindings remain explicit. Use `UiButton<UiReleaseButtonBehavior, UiFluentButtonPresentation<UiFluentSubtleButtonAppearance>>` for a subtle command, as in the Observer sample. Button, ToggleButton, Input, Slider and layout descendants retain their own real semantics; the root adds no Invoke/Value/focus pattern. A child instance belongs to one owner, and disposing the Toolbar disposes its children.

Tab enters one remembered available child and then exits the Toolbar. Arrow keys move circularly on both axes, matching the pinned hook even in a horizontal Toolbar; Home/End choose first/last. RTL reverses horizontal arrows and child placement. Pointer/native child focus becomes the remembered entry. Disabled/unavailable children do not register a stop; removing or disabling the focused member restores focus to an available child. A fully disabled or empty Toolbar has no child entry. Controls registered through the shared range/editor key policy retain their caret/range arrow keys; place such controls deliberately, since full Tabster boundary-aware navigation among mixed editors/composite groups is not qualified. Nested Toolbars explicitly throw; use ordinary child layout groups instead.

Horizontal Small/Medium/Large padding is 0×4,4×8,4×20 (vertical×horizontal), respectively. Vertical padding remains4×8 for every size, matching the pinned styles. Children center on the cross axis, preserve logical order in RTL and clip at finite parent bounds. No automatic wrap, overflow menu or arbitrary flex-parent-data contract is supplied. `IUiToolbarPresentation.Padding` supports custom finite nonnegative insets. Options invalidate ancestor measurement; unchanged arrangement is retained through the existing layout cache. Membership/key buffers are reused; no new renderer, shader, native import, parser, timer or numerical performance claim is introduced.

UIA exposes native Toolbar50021, `Duxel.Toolbar`, ARIA `toolbar` and live readable orientation1/2, with actual child action/Toggle/Value providers. This initial surface does not implement native orientation property-change events, root checked-value maps, dedicated Toolbar child variants, physical screen-reader/forced-colors qualification or full CSS/Tabster conformance. Use existing child bindings, including mapped caller state, for selection.

Run `./run-gallery.ps1` → **도구 모음** (header overflow if hidden). Change direction, root padding size and RTL; invoke commands and toggle Bold. Try Tab/Shift+Tab, arrows, Home/End, Enter and Space; state persists across tabs. Evidence: `artifacts/toolbar-2026-11-13/`; large GPU readbacks: `L:/Cache/Duxel/toolbar-2026-11-13-evidence/`. Managed proof is separate from fixed RC3 NativeAOT/runtime-image/device/performance gates.

Pinned sources: [behavior](https://raw.githubusercontent.com/microsoft/fluentui/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-toolbar/library/src/components/Toolbar/useToolbar.ts), [styles](https://raw.githubusercontent.com/microsoft/fluentui/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-toolbar/library/src/components/Toolbar/useToolbarStyles.styles.ts), [button defaults](https://raw.githubusercontent.com/microsoft/fluentui/8830c10bfb2f4c1bdd540287b09713b9679cd1ad/packages/react-components/react-toolbar/library/src/components/ToolbarButton/useToolbarButton.ts).
