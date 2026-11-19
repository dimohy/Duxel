# Fluent 2 Checkbox

Checkbox and Markdown task indicators now share `UiFluentCheckboxPalette.Resolve(context, state, value)`. In system contrast mode, enabled labels/borders/marks use CanvasText and disabled ones use GrayText; checked backgrounds use Canvas while unchecked/mixed backgrounds remain transparent. Hover and press retain those system roles. The context disabled state also applies to Markdown tasks, correcting four ordinary-theme disabled-task captures that previously retained active marks. The existing scheme-only overload keeps its authored palette contract. Four hundred exact GPU comparisons cover three values, four states, two sizes/shapes/label positions, light/dark bases and actual Markdown tasks. The same final test DLL fails204 checks with the previous product assemblies. Of200 ordinary captures,196 are unchanged and only the four disabled-task fixes differ;677 existing Markdown captures match exactly. The full suite passes1387 checks. An isolated Chromium CSS diagnostic supports the color-role choice from the [official Checkbox styles](https://github.com/microsoft/fluentui/blob/master/packages/react-components/react-checkbox/library/src/components/Checkbox/useCheckboxStyles.styles.ts); it is not a live Fluent component or complete browser pixel comparison. Evidence: `artifacts/checkbox-system-colors-2026-11-17/verification.json`.

2026-11-11 [Common Toggle accessibility](widget-accessibility.md): actual Checkbox/Switch/ToggleButton roles and Off/On/Indeterminate states use one canonical ToggleState, existing action/binding policy and Windows Toggle property events. Checked remains a two-state projection. Core21/Observer134/generators115, external Toggle11/host4 and checkbox-menu6/host3 pass;47GPU checks and73 unchanged bodies preserve rendering. Earlier shared peers update on the requested next frame. Full narration/forced colors/NativeAOT remain open. Evidence: artifacts/toggle-accessibility-2026-11-11/verification.json.

2026-11-06. This improves the initial presentation of an already connected Checkbox; it does not add a new top-level component count. The [official usage page](https://fluent2.microsoft.design/components/web/react/core/checkbox/usage) currently labels Checkbox Preview. Implementation references reuse Microsoft Fluent UI commit `8830c10bfb2f4c1bdd540287b09713b9679cd1ad`. Sources and hashes are in `artifacts/fluent-checkbox-2026-11-06/sources.json`.

Generated bool/bool? `Fluent.Checkbox(label, binding)` and `Dux.Checkbox` share the `UiToggle` input, state and lifetime implementation. Null is mixed; activation selects true, followed by normal true/false toggles. Labels and indicators are clickable; Tab/Space operate the control and Enter does not toggle. Legacy immediate Checkbox/numeric skins remain; complete default-API migration is still open.

## Compile-time composition

The default is medium, square, label after. Closed generic implementations select16/20px indicators, exact12/16px marks, square/circular frame and mixed-mark algorithms, and before/after layout. Users can replace these implementations through the same interfaces.

```csharp
[UiWidgetStyle(typeof(UiToggle<,,,>), typeof(bool?),
    typeof(UiNullableToggleValue), typeof(UiSpaceButtonBehavior),
    typeof(UiFluentCheckboxPresentation<UiFluentLargeCheckboxSize,
        UiFluentCircularCheckboxShape, UiCheckboxLabelBefore>))]
public partial class ProductCheckbox;
```

The generated class can be inherited and `OnChanged` extended. Behavior and layout/painting remain separate; no runtime theme registry or string lookup was added.

`IUiTogglePresentation.BeginText` defaults to an empty scope, preserving custom/legacy policies. Fluent Checkbox uses Regular14px/20px typography and restores fonts on exit or exceptions. It requires an explicit DirectText/metrics backend and TypographyFonts; atlas-only low-level consumers must configure them. No silent font substitution is introduced.

## States and icons

Unchecked/mixed backgrounds are transparent; checked uses compound brand fill. Mixed uses a brand border and square/circular mark, replacing the old filled background and horizontal dash. Disabled uses transparent fill and disabled stroke/foreground. Hover/pressed select their own brand/neutral tokens. An independent24-state fixture resolves the actual `grey`/`brandWeb` tables, not stale comments or arithmetic approximations.

`[UiFluentIcon("checkmark", UiFluentIconStyle.Filled, 12)]` pins an exact glyph at generation time. Fixed-size output directly calls one glyph/font; size0 preserves the existing20/24 selection. Missing glyphs or unsupported sizes produce DUXWGT008 without substitution. The existing two-argument attribute constructor remains available. Six Checkbox marks reuse bundled fonts; no new font or runtime JSON was added.

## Running and validation

```powershell
$run = @{
    Path = 'samples/fba/widget_tree_theme_fba.cs'
    Managed = $true
    NoCache = $true
    DotnetPath = 'C:\Users\dimohy\.cache\duxel-dotnet-10.0.301\dotnet.exe'
}
.\run-fba.ps1 @run -ExtraArgs @('--checkbox')
```

The page exposes eight combinations plus mixed/checked/unchecked and disabled state. Rendering tests support `--checkbox-only` for input/callback/typography/token checks before GPU execution. GPU tests compare whole images for eight generated user styles, light/dark, MSAA1/4 and raster/vector, using both generated and immediate paths. No-label negative controls remove marks to prove that actual glyphs are present. Current counts, hashes and regression-image evidence are in `artifacts/fluent-checkbox-2026-11-06/verification.json`.

Official browser pixel parity, UIA, logical RTL, forced colors, official long-label wrapping/Field, and full focus/motion fidelity remain open. This does not complete Checkbox or the full Fluent goal. Startup/first-submit/auto-exit are separate from manual visual/input acceptance. No new FPS/Private/Pi speedup is measured or claimed.

2026-11-12 — Known deterministic leaf presentations now participate in the existing parent's measurement cache while rendering/input continue. Custom traits remain opt-in; label/options/size/items and environment changes invalidate layout. See [common leaf measurement contract](widget-cpu-retention.md) and artifacts/leaf-layout-retention-2026-11-11/verification.json. Full NativeAOT/performance gates remain open.
