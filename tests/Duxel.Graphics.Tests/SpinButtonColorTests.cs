using Duxel.Core;

internal static class SpinButtonColorTests
{
    internal static void Run(UiForcedColorPalette palette, Action<bool, string> check)
    {
        using var fixture = new PresentationReuseTests.Fixture(1, true);
        var number = new UiState<double?>(5);
        using var spin = Fluent.SpinButton("Contrast amount", number.Binding, new(Minimum: 0, Maximum: 10));
        using var expected = new UiSpinButton<ReferencePresentation>("Reference amount", number.Binding, new(Minimum: 0, Maximum: 10));
        var reference = false;
        var disabled = false;
        UiSpinButtonLayout layout = default;
        var parent = new UiColor(11, 22, 33);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false,
            0, 0, [], [], default, default);
        void Draw(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            ui.GetWindowDrawList().AddRectFilled(new(0, 0, fixture.FrameInfo.DisplaySize.X, fixture.FrameInfo.DisplaySize.Y), parent, ui.WhiteTextureId);
            ui.SetCursorScreenPos(new(10, 10));
            ui.BeginDisabled(disabled);
            if (reference)
                expected.Render(ui);
            else
                spin.Render(ui);
            ui.EndDisabled();
            var context = new UiControlContext(ui);
            var size = UiFluentSpinButtonPresentation.Measure(context, spin.Options);
            layout = UiFluentSpinButtonPresentation.Arrange(context, new(10, 10, size.X, size.Y), spin.Options);
        }
        byte[] Frame(UiCompiledDesign design, UiForcedColorPalette colors, UiInputState input)
        {
            fixture.Context.SetTheme(colors.Apply(design.Theme));
            using var drawing = fixture.Build(Draw, input);
            return fixture.Render(drawing);
        }
        var width = (int)fixture.FrameInfo.DisplaySize.X;
        uint Pixel(byte[] pixels, int x, int y) => BitConverter.ToUInt32(pixels, (y * width + x) * 4);
        foreach (var appearance in Enum.GetValues<UiInputAppearance>())
            foreach (var size in Enum.GetValues<UiSpinButtonSize>())
                foreach (var rtl in new[] { false, true })
                {
                    var backgrounds = true;
                    var schemes = true;
                    var roles = true;
                    var roleFailure = string.Empty;
                    foreach (var increment in new[] { true, false })
                        foreach (var state in new[] { "Rest", "Hover", "Held", "Limit", "ReadOnly", "Disabled" })
                        {
                            disabled = state == "Disabled";
                            spin.Options = new(Minimum: 0, Maximum: 10, Size: size, Appearance: appearance,
                                RightToLeft: rtl, ReadOnly: state == "ReadOnly");
                            expected.Options = spin.Options;
                            number.Value = state == "Limit" ? increment ? 10 : 0 : 5;
                            Frame(UiCompiledDesign.Windows11Dark, palette, idle with { LeftMouseReleased = true });
                            var slot = increment ? layout.Increment : layout.Decrement;
                            var input = state == "Rest" ? idle : idle with
                            {
                                MousePosition = new(slot.X + slot.Width / 2, slot.Y + slot.Height / 2),
                                LeftMouseDown = state == "Held",
                                LeftMousePressed = state == "Held"
                            };
                            var dark = Frame(UiCompiledDesign.Windows11Dark, palette, input);
                            input = input with { LeftMousePressed = false };
                            var light = Frame(UiCompiledDesign.Windows11, palette, input);
                            var inactive = state is "Limit" or "ReadOnly" or "Disabled";
                            var expectedBackground = !inactive && state is "Hover" or "Held" ? palette.ButtonFace
                                : disabled || appearance == UiInputAppearance.Underline ? parent : palette.Canvas;
                            backgrounds &= Pixel(dark, (int)slot.X + 21, (int)slot.Y + 3) == expectedBackground.Rgba;
                            var changedPalette = inactive
                                ? palette with { GrayText = new(170, 20, 210) }
                                : palette with { ButtonText = new(170, 20, 210) };
                            var changed = Frame(UiCompiledDesign.Windows11, changedPalette, input);
                            reference = true;
                            var literalRoles = Frame(UiCompiledDesign.Windows11, palette, input);
                            reference = false;
                            var recolored = false;
                            for (var y = (int)slot.Y + 2; y < slot.Y + slot.Height - 2; y++)
                                for (var x = (int)slot.X + 4; x < slot.X + slot.Width - 4; x++)
                                {
                                    schemes &= Pixel(dark, x, y) == Pixel(light, x, y);
                                    recolored |= Pixel(light, x, y) != Pixel(changed, x, y);
                                    var coverage = Pixel(dark, x, y) == Pixel(literalRoles, x, y);
                                    if (!coverage && roleFailure.Length == 0)
                                        roleFailure = $" / {state}, increment={increment}, at={x},{y}, actual={Pixel(dark, x, y):X8}, expected={Pixel(literalRoles, x, y):X8}";
                                    roles &= coverage;
                                }
                            roles &= recolored;
                            Frame(UiCompiledDesign.Windows11, changedPalette, idle with { LeftMouseReleased = true });
                        }
                    var label = $"SpinButton contrast / {appearance}, {size}, RTL={rtl}";
                    check(backgrounds, label + " / both steppers use ButtonFace on hover/hold and preserve transparent rest/disabled surfaces");
                    check(schemes, label + " / all six stepper states are independent of the authored light/dark scheme");
                    check(roles, label + " / glyph pixels respond to ButtonText or disabled GrayText across all six states" + roleFailure);
                }
    }

    private readonly struct ReferencePresentation : IUiSpinButtonPresentation
    {
        public static bool CanRetainMeasurement => true;
        public static UiControlTypographyScope BeginText(UiControlContext context, UiSpinButtonOptions options)
            => UiFluentSpinButtonPresentation.BeginText(context, options);
        public static UiVector2 Measure(UiControlContext context, UiSpinButtonOptions options)
            => UiFluentSpinButtonPresentation.Measure(context, options);
        public static UiSpinButtonLayout Arrange(UiControlContext context, UiRect bounds, UiSpinButtonOptions options)
            => UiFluentSpinButtonPresentation.Arrange(context, bounds, options);

        public static void Paint(UiControlContext context, UiSpinButtonLayout layout, UiInputEditResult edit,
            UiSpinButtonOptions options, UiButtonInteraction increment, UiButtonInteraction decrement, bool canIncrement, bool canDecrement)
        {
            var colors = context.Theme.ForcedColors!.Value;
            var focused = (edit.Active || edit.FocusWithin) && context.IsEnabled;
            var radius = options.Appearance == UiInputAppearance.Underline ? 0 : 4;
            var fieldText = context.IsEnabled ? colors.CanvasText : colors.GrayText;
            var fieldBackground = context.IsEnabled && options.Appearance != UiInputAppearance.Underline ? colors.Canvas : default;
            context.Frame(layout.Input.Field, fieldBackground, default, radius, 0);
            using (context.RoundedClip(layout.Input.Field, radius))
            {
                Step(context, layout.Increment, increment, canIncrement, options, colors, true);
                Step(context, layout.Decrement, decrement, canDecrement, options, colors, false);
            }
            UiFluentInputFrame.Paint(context, layout.Input.Field,
                new(default, fieldText, fieldText, context.IsEnabled ? colors.Highlight : default, fieldText, fieldText),
                focused, options.Appearance);
            context.PaintEditableText(layout.Input, edit, new(fieldText, colors.Highlight, fieldText) { Typography = true });
        }

        private static void Step(UiControlContext context, UiRect bounds, UiButtonInteraction interaction, bool enabled,
            UiSpinButtonOptions options, UiForcedColorPalette colors, bool increment)
        {
            enabled &= context.IsEnabled;
            if (enabled && (interaction.Hovered || interaction.Held))
                context.Frame(bounds, colors.ButtonFace, default, 0, 0);
            var x = bounds.X + (bounds.Width - 16) / 2;
            if (options.Size == UiSpinButtonSize.Small)
                x += options.RightToLeft ? 1 : -1;
            var icon = new UiRect(x, bounds.Y + (bounds.Height - 16) / 2 + (increment ? 1.5f : -1.5f), 16, 16);
            if (increment)
                UiSpinChevronUp.Paint(context, icon, enabled ? colors.ButtonText : colors.GrayText, interaction);
            else
                UiSpinChevronDown.Paint(context, icon, enabled ? colors.ButtonText : colors.GrayText, interaction);
        }
    }
}
