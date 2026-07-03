# Declarative Control Skin Roadmap

This is the next-session homework.

## Goal

Let Duxel users write C# declarative UI as conveniently as SwiftUI, Flutter, and Jetpack Compose while allowing apps/designs to freely change the rendered shape of built-in controls.

Duxel already provides:

- `IUiDesign` / `UiCompiledDesign` to lock app design before startup
- `UiTheme` for color palettes
- `UiDesignTokens` for shared shape values such as radius, border width, focus ring thickness, and pressed offset
- `IUiViewStyle`, `.Background(...)`, `.Border(...)`, `.CornerRadius(...)`, and `.Panel(...)` for view-local shape modifiers
- `UiComponent`, `DuxelView.Custom(...)`, and `IUiCustomWidget` for custom widget authoring

The missing piece is an official skin layer that can replace rendering strategy per control type. For example, making every `Button` pill-shaped, every `TextField` underline-based, or every `Segmented` control Material-like is currently spread across tokens, themes, and custom widgets.

## Next Starting Point

1. Design a compiled skin contract named `UiControlSkin` or `UiControlStyleSet`.
2. Add the skin set to `UiCompiledDesign`, while keeping existing apps on the default `Windows11` skin automatically.
3. Limit the first pass to:
   - `Button`
   - `InputText` / `TextField`
   - `Segmented`
   - `Slider`
   - `Checkbox` / `Toggle`
   - `Scrollbar`
4. Refactor each control implementation to resolve through the skin before reading colors/tokens directly.
5. Skins must be lockable through C# types or source-generated values, not only runtime theme parsing.
6. Per-view overrides should be possible.
   - Example: `Dux.Button("Save").ControlStyle(MyButtonStyle.PrimaryPill)`
   - Example: `Dux.TextField("name", name).ControlStyle(MyTextFieldStyle.Underline)`
7. The default `Windows11` / `Windows11Dark` skin should preserve the current improved appearance.

## Design Principles

- Theme owns the color palette, tokens own shared numeric values, and skin owns per-control rendering policy.
- App code should remain short: `Dux.Button(...)`, `Dux.TextField(...)`, `Dux.EnumSegmented(...)`.
- Keep the immediate-mode internals, but expose declarative values/styles to users.
- NativeAOT must work without reflection.
- Skin replacement must not introduce boxing/allocation in hot paths.
- It must compose with existing `UiTheme`, `UiDesignTokens`, and `IUiViewStyle`.

## Draft API

```csharp
public readonly struct ProductDesign : IUiDesign
{
    public static UiCompiledDesign Create()
        => UiCompiledDesign.Windows11Dark with
        {
            Tokens = UiDesignTokens.Windows11 with
            {
                ControlCornerRadius = 8f
            },
            ControlSkins = UiControlSkins.Windows11Dark with
            {
                Button = ProductButtonSkin.Pill,
                TextField = ProductTextFieldSkin.Underline,
                Segmented = ProductSegmentedSkin.Attached
            }
        };
}
```

```csharp
Dux.Button("Save", Save)
    .ControlStyle(ProductButtonStyle.PrimaryPill);

Dux.TextField("name", name)
    .ControlStyle(ProductTextFieldStyle.Underline);
```

## Candidate Implementation Shape

- `UiControlSkins`
  - `UiButtonSkin Button`
  - `UiTextFieldSkin TextField`
  - `UiSegmentedSkin Segmented`
  - `UiSliderSkin Slider`
  - `UiToggleSkin Toggle`
  - `UiScrollbarSkin Scrollbar`
- Start each skin as a `readonly record struct` or readonly struct.
- `UiImmediateContext` computes interaction/layout state; skins return color/radius/padding/border/fill policy.
- Full custom draw delegates should be a later step. Start with value-based skins.

## Verification Samples

Next session should add or update at least two samples:

- `samples/fba/declarative_dashboard_fba.cs`
  - Verify the default Windows 11 dark skin remains polished.
- Candidate new sample: `samples/fba/control_skin_showcase_fba.cs`
  - Render the same surface with `Windows11`, `Pill`, `Underline`, and `Compact` skins.
  - Include Button, TextField, Segmented, Slider, Checkbox, and Scrollbar on one screen.

## Completion Criteria

- `UiCompiledDesign` can specify a per-control skin set.
- At minimum Button/TextField/Segmented can change rendered shape through skins.
- Existing theme/token-based Windows 11 appearance remains intact.
- Per-view style override works for at least one control.
- `dotnet build Duxel.slnx -c Release` passes without warnings.
- FBA samples `declarative_dashboard_fba.cs` and the control skin showcase run with `./run-fba.ps1 ... -NoCache`.

