# Fluent 2 Switch

Fixed a Windows regression where transparent rows above native raster ink made switch labels appear too low after baseline preservation. Vertical raster bounds now cover actual ink while retaining the original baseline placement. All 80 checks pass within 0.5px pixel-grid parity: five labels including V-Sync and Event-driven mode, two sizes, before/after placement, raster/vector and MSAA1/4. [Evidence](../artifacts/fluent-messagebar-2026-11-15/windows-switch-vertical-verification.json).

2026-11-11 [Common Toggle accessibility](widget-accessibility.md): actual Checkbox/Switch/ToggleButton roles and Off/On/Indeterminate states use one canonical ToggleState, existing action/binding policy and Windows Toggle property events. Checked remains a two-state projection. Core21/Observer134/generators115, external Toggle11/host4 and checkbox-menu6/host3 pass;47GPU checks and73 unchanged bodies preserve rendering. Earlier shared peers update on the requested next frame. Full narration/forced colors/NativeAOT remain open. Evidence: artifacts/toggle-accessibility-2026-11-11/verification.json.

Last synced: 2026-11-16

Default `Fluent.Switch` consumes the same inherited `UiToggle`, binding and generated path available to user widgets. The legacy immediate `Switch` compatibility path is preserved. Use the generated widget or a closed generic direct call for this presentation.

```csharp
var enabled = new UiState<bool>(false);
var control = Fluent.Switch("Notifications", enabled.Binding);

[UiWidgetStyle(typeof(UiToggle<,,,>), typeof(bool), typeof(UiBooleanToggleValue),
    typeof(UiSpaceButtonBehavior),
    typeof(UiFluentSwitchPresentation<UiFluentSmallSwitchSize, UiSwitchLabelAbove>))]
public partial class CompactSetting;
```

Select `UiFluentMediumSwitchSize`/`UiFluentSmallSwitchSize` and `UiSwitchLabelAfter`/`UiSwitchLabelBefore`/`UiSwitchLabelAbove`. Size and label composition use closed implementation types with no runtime style registry. The default medium/after wrapper delegates to this implementation. Inherit the generated class to extend `OnChanged`. Runtime state and callbacks remain active; idle frames do not write bindings.

Medium uses a40×20 track and Regular14/20; small uses32×16 and Regular12/16. Source generation binds the existing official Filled circle20 glyph and paints it in18/14px thumb boxes. Before/after/above layouts have separate measurement algorithms. Unlabelled root height does not depend on ambient text height. Existing font assets are reused.

Pointer release and Space release toggle selection. Enter does not activate and disabled controls reject clicks. Switch takes bool bindings; indeterminate state belongs to Checkbox. External bindings are not disposed by the widget.

The [official usage](https://fluent2.microsoft.design/components/web/react/core/switch/usage) is marked Preview at retrieval. The pinned React reference is8830c10bfb2f4c1bdd540287b09713b9679cd1ad; originals and hashes live in `artifacts/fluent-switch-2026-11-06/plan.json`. Shared brand/neutral colors reuse the Checkbox palette while Switch separately resolves transparent borders, invariant enabled label color and the disabled checked surface.

The `--switch` gallery exposes six generated styles, shared selection and a disabled example. Managed .NET10 and fixed .NET11 RC3 NativeAOT are separate. See that artifact directory's `verification.json`. Initial test placement clipped states beyond the final resized window. Two-column placement and per-state whole-bound/actual-ink checks correct this; generated/direct equivalence alone is not correct-screen proof.

Official browser pixel equivalence, motion/reduced-motion, UIA/required/Field/disabledFocusable, RTL/forced colors, wrapped labels and indicator-only hover hit testing remain open. Pointer input currently uses the common root bounds. Improved colors/size/composition do not establish full Switch or Fluent completion. No new FPS/allocation/Private/Pi improvement is claimed.

2026-11-12 — Known deterministic leaf presentations now participate in the existing parent's measurement cache while rendering/input continue. Custom traits remain opt-in; label/options/size/items and environment changes invalidate layout. See [common leaf measurement contract](widget-cpu-retention.md) and artifacts/leaf-layout-retention-2026-11-11/verification.json. Full NativeAOT/performance gates remain open.
