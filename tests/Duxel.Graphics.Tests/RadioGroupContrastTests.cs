using Duxel.Core;

internal static class RadioGroupContrastTests
{
    internal static void Run(string output, Action<bool, string> check)
    {
        var colors = new UiForcedColorPalette(new(5, 10, 15), new(240, 230, 220), new(140, 150, 155),
            new(20, 90, 180), new(250, 240, 230), new(30, 200, 220), new(35, 45, 55), new(210, 220, 230));
        var background = new UiColor(10, 19, 28);
        (string Suffix, bool Hovered, bool Held)[] pointerStates = [("", false, false), ("-hover", true, false), ("-pressed", true, true)];
        using var fixture = new PresentationReuseTests.Fixture(1, true);
        Variant<UiRadioVerticalLayout>("vertical");
        Variant<UiRadioHorizontalLayout>("horizontal");
        Variant<UiRadioHorizontalStackedLayout>("stacked");

        void Variant<TLayout>(string layoutName) where TLayout : IUiRadioGroupLayout
        {
            var options = new UiRadioOption[] { new("key", "Label", Description: "Description") };
            var rows = new UiRadioOptionLayout[1];
            foreach (var forced in new[] { false, true })
                foreach (var enabled in new[] { true, false })
                    foreach (var selected in new[] { false, true })
                    foreach (var (suffix, hovered, held) in pointerStates.AsSpan(0, forced && enabled && selected ? pointerStates.Length : 1))
                    {
                        var theme = UiCompiledDesign.Windows11Dark.Theme;
                        fixture.Context.SetTheme(forced ? colors.Apply(theme) : theme);
                        var item = default(UiRadioOptionLayout);
                        var pixels = fixture.Capture(ui =>
                        {
                            ui.EnableRootViewportContentLayout(contentPadding: 0);
                            var context = new UiControlContext(ui);
                            context.Frame(new(0, 0, fixture.FrameInfo.DisplaySize.X, fixture.FrameInfo.DisplaySize.Y),
                                background, default, 0, 0);
                            ui.BeginDisabled(!enabled);
                            using var text = UiFluentRadioGroupPresentation<TLayout>.BeginText(context);
                            UiFluentRadioGroupPresentation<TLayout>.Measure(context, string.Empty, options, rows, 240);
                            item = rows[0] with { Bounds = rows[0].Bounds with { X = 20, Y = 20 } };
                            UiFluentRadioGroupPresentation<TLayout>.PaintOption(context, item, selected,
                                new() { FocusVisible = enabled, Hovered = hovered, Held = held });
                            ui.EndDisabled();
                        });
                        var name = $"{(forced ? "forced" : "ordinary")}-{layoutName}-{enabled}-{selected}{suffix}";
                        fixture.WriteCapturePng(Path.Combine(output, name + ".png"), pixels);
                        if (!forced)
                            continue;
                        var line = enabled ? selected && !hovered ? colors.Highlight : colors.ButtonText : colors.GrayText;
                        var dot = enabled ? colors.Highlight : colors.GrayText;
                        var foreground = enabled ? colors.CanvasText : colors.GrayText;
                        var indicatorX = (int)(item.Bounds.X + item.Indicator.X) + 8;
                        var indicatorY = (int)(item.Bounds.Y + item.Indicator.Y);
                        bool Matches(int x, int y, UiColor color)
                        {
                            var at = (y * fixture.LastCaptureSize.Width + x) * 4;
                            var rgba = color.Rgba;
                            return pixels[at] == (byte)rgba && pixels[at + 1] == (byte)(rgba >> 8) && pixels[at + 2] == (byte)(rgba >> 16);
                        }
                        bool HasInk(UiRect area)
                        {
                            for (var y = (int)(item.Bounds.Y + area.Y) - 1; y < (int)(item.Bounds.Y + area.Y + area.Height) + 1; y++)
                                for (var x = (int)(item.Bounds.X + area.X); x < (int)(item.Bounds.X + area.X + area.Width); x++)
                                    if (Matches(x, y, foreground))
                                        return true;
                            return false;
                        }
                        // The one-pixel circle stroke is antialiased even at its top center.
                        var at = (indicatorY * fixture.LastCaptureSize.Width + indicatorX) * 4;
                        var rgba = line.Rgba;
                        var coverage = (pixels[at + 2] - 28f) / ((byte)(rgba >> 16) - 28f);
                        check(coverage is >= .75f and <= 1f
                            && MathF.Abs(pixels[at] - (10 + ((byte)rgba - 10) * coverage)) <= 3
                            && MathF.Abs(pixels[at + 1] - (19 + ((byte)(rgba >> 8) - 19) * coverage)) <= 3,
                            $"RadioGroup contrast: {name} ring");
                        check(Matches(indicatorX, indicatorY + 8, selected ? dot : background), $"RadioGroup contrast: {name} dot");
                        check(Matches((int)(item.Bounds.X + item.Bounds.Width / 2), 22, enabled ? colors.Highlight : background),
                            $"RadioGroup contrast: {name} focus");
                        check(HasInk(item.LabelBounds), $"RadioGroup contrast: {name} label");
                        check(HasInk(item.DescriptionBounds), $"RadioGroup contrast: {name} description");
                    }
        }
    }
}
