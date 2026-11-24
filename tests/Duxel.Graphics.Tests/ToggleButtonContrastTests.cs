using Duxel.Core;

internal static class ToggleButtonContrastTests
{
    internal static void Run(string output, Action<bool, string> check)
    {
        var colors = new UiForcedColorPalette(new(5, 10, 15), new(240, 230, 220), new(135, 150, 155),
            new(20, 90, 180), new(250, 240, 230), new(30, 200, 220), new(35, 45, 55), new(210, 220, 230));
        var background = new UiColor(12, 24, 36);
        var bounds = new UiRect(20, 20, 120, 40);
        var rest = default(UiButtonInteraction);
        var hover = new UiButtonInteraction(false, true, false);
        var pressed = new UiButtonInteraction(false, true, true);
        var focus = rest with { Focused = true, FocusVisible = true };
        var keyPressed = focus with { Held = true };
        var cases = new (string Name, bool Checked, bool Enabled, UiButtonInteraction Input,
            UiColor Fill, UiColor Border, UiColor Text, UiColor Outline)[]
        {
            ("unchecked", false, true, rest, colors.ButtonFace, colors.ButtonText, colors.ButtonText, background),
            ("checked", true, true, rest, colors.Highlight, colors.Highlight, colors.HighlightText, background),
            ("unchecked-hover", false, true, hover, colors.HighlightText, colors.Highlight, colors.Highlight, background),
            ("checked-hover", true, true, hover, colors.HighlightText, colors.Highlight, colors.Highlight, background),
            ("checked-pressed", true, true, pressed, colors.HighlightText, colors.Highlight, colors.Highlight, background),
            ("unchecked-focus", false, true, focus, colors.ButtonFace, colors.ButtonText, colors.ButtonText, colors.ButtonText),
            ("checked-focus", true, true, focus, colors.Highlight, colors.HighlightText, colors.HighlightText, colors.Highlight),
            ("checked-key-pressed", true, true, keyPressed, colors.HighlightText, colors.HighlightText, colors.Highlight, colors.Highlight),
            ("disabled-unchecked", false, false, hover, colors.ButtonFace, colors.GrayText, colors.GrayText, background),
            ("disabled-checked", true, false, hover, colors.ButtonFace, colors.GrayText, colors.GrayText, background)
        };
        using var fixture = new PresentationReuseTests.Fixture(1, true);
        byte[] Capture(bool value, bool enabled, UiButtonInteraction interaction) => fixture.Capture(ui =>
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            var context = new UiControlContext(ui);
            context.Frame(new(0, 0, fixture.FrameInfo.DisplaySize.X, fixture.FrameInfo.DisplaySize.Y),
                background, default, 0, 0);
            using var clip = context.Clip(bounds);
            ui.BeginDisabled(!enabled);
            var textSize = context.MeasureText("Toggle");
            UiFluentToggleButtonPresentation.Paint(context, "Toggle", textSize, bounds, default, interaction, value);
            ui.EndDisabled();
        });
        foreach (var design in new[] { UiCompiledDesign.Windows11, UiCompiledDesign.Windows11Dark })
        {
            fixture.Context.SetTheme(design.Theme);
            foreach (var test in cases)
                fixture.WriteCapturePng(Path.Combine(output, $"ordinary-{design.Theme.FluentColorScheme}-{test.Name}.png"),
                    Capture(test.Checked, test.Enabled, test.Input));
        }
        fixture.Context.SetTheme(colors.Apply(UiCompiledDesign.Windows11Dark.Theme));
        foreach (var test in cases)
        {
            var image = Capture(test.Checked, test.Enabled, test.Input);
            fixture.WriteCapturePng(Path.Combine(output, $"forced-{test.Name}.png"), image);
            bool Matches(int x, int y, UiColor color)
            {
                var at = (y * fixture.LastCaptureSize.Width + x) * 4;
                var rgba = color.Rgba;
                return image[at] == (byte)rgba && image[at + 1] == (byte)(rgba >> 8) && image[at + 2] == (byte)(rgba >> 16);
            }
            check(Matches(40, 39, test.Fill), $"ToggleButton contrast: {test.Name} fill");
            check(Matches(80, 20, test.Border), $"ToggleButton contrast: {test.Name} border");
            check(Matches(80, 22, test.Input.FocusVisible ? test.Outline : test.Fill),
                $"ToggleButton contrast: {test.Name} focus outline survives the widget allocation clip");
            var hasInk = Enumerable.Range(29, 22).Any(y => Enumerable.Range(57, 46).Any(x => Matches(x, y, test.Text)));
            check(hasInk, $"ToggleButton contrast: {test.Name} text");
        }
    }
}
