using System.Security.Cryptography;
using System.Text.Json;
using Duxel.Core;

internal static class CheckboxContrastTests
{
    internal static void Run(string? output, Action<bool, string> check)
    {
        var colors = new UiForcedColorPalette(new(4, 8, 16), new(240, 230, 220), new(110, 120, 130),
            new(10, 90, 180), new(250, 240, 230), new(30, 200, 220), new(20, 30, 40), new(210, 220, 230));
        using var fixture = new PresentationReuseTests.Fixture(1, true);
        var hashes = new Dictionary<string, string>();
        var savedFailure = false;
        void Compare(string name, byte[] expected, byte[] actual)
        {
            var equal = expected.AsSpan().SequenceEqual(actual);
            hashes.Add(name, Convert.ToHexString(SHA256.HashData(actual)));
            check(equal, "Checkbox contrast / " + name + " / exact independent GPU reference");
            if (!equal && output is not null && !savedFailure)
            {
                File.WriteAllBytes(Path.Combine(output, "first.reference.rgba"), expected);
                File.WriteAllBytes(Path.Combine(output, "first.actual.rgba"), actual);
                savedFailure = true;
            }
        }
        void Begin(UiImmediateContext ui, bool disabled)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            new UiControlContext(ui).Frame(new(0, 0, fixture.FrameInfo.DisplaySize.X, fixture.FrameInfo.DisplaySize.Y),
                new(11, 22, 33), default, 0, 0);
            ui.BeginDisabled(disabled);
        }
        void Variant<TSize, TShape, TLabel>()
            where TSize : IUiCheckboxSize where TShape : IUiCheckboxShape where TLabel : IUiCheckboxLabelPosition
        {
            foreach (var forced in new[] { false, true })
                foreach (var dark in new[] { false, true })
                    foreach (var value in new bool?[] { false, true, null })
                        foreach (var state in Enum.GetValues<UiButtonVisualState>())
                        {
                            var theme = (dark ? UiCompiledDesign.Windows11Dark : UiCompiledDesign.Windows11).Theme;
                            fixture.Context.SetTheme(forced ? colors.Apply(theme) : theme);
                            var disabled = state == UiButtonVisualState.Disabled;
                            var interaction = new UiButtonInteraction(false, state is UiButtonVisualState.Hover or UiButtonVisualState.Pressed,
                                state == UiButtonVisualState.Pressed);
                            void Draw(UiImmediateContext ui, bool reference)
                            {
                                Begin(ui, disabled);
                                var context = new UiControlContext(ui);
                                using var typography = UiFluentCheckboxPresentation<TSize, TShape, TLabel>.BeginText(context, true);
                                var textSize = ui.CalcTextSize("Label");
                                var bounds = new UiRect(20, 20, 200, 40);
                                var layout = UiFluentCheckboxPresentation<TSize, TShape, TLabel>.Measure(context, textSize, true);
                                layout = UiFluentCheckboxPresentation<TSize, TShape, TLabel>.Arrange(context, layout, new(bounds.Width, bounds.Height));
                                if (!reference)
                                    UiFluentCheckboxPresentation<TSize, TShape, TLabel>.Paint(context, "Label", textSize, bounds, layout, interaction, value);
                                else
                                {
                                    var foreground = disabled ? colors.GrayText : colors.CanvasText;
                                    var paint = forced ? new UiCheckboxPaint(value == true ? colors.Canvas : default,
                                        foreground, value == false ? default : foreground, foreground)
                                        : UiFluentCheckboxPalette.Resolve(context.FluentColorScheme, state, value);
                                    var indicator = layout.Indicator with
                                    {
                                        X = bounds.X + layout.Indicator.X,
                                        Y = bounds.Y + (bounds.Height - layout.Indicator.Height) / 2
                                    };
                                    TShape.Paint(context, indicator, paint.Background, paint.Border);
                                    if (value != false)
                                    {
                                        using var clip = context.Clip(indicator);
                                        var inset = (indicator.Height - TSize.GlyphSize) / 2;
                                        TSize.PaintMark(context, new(indicator.X + inset, indicator.Y + inset, TSize.GlyphSize, TSize.GlyphSize),
                                            paint.Indicator, interaction, value is null, TShape.Circular);
                                    }
                                    context.TypographyIndicatorText("Label", new(bounds.X + layout.LabelOffset.X, bounds.Y,
                                        textSize.X, bounds.Height), textSize, paint.Label);
                                }
                                ui.EndDisabled();
                            }
                            var expected = fixture.Capture(ui => Draw(ui, true));
                            var actual = fixture.Capture(ui => Draw(ui, false));
                            Compare($"forced={forced}/dark={dark}/{typeof(TSize).Name}/{typeof(TShape).Name}/{typeof(TLabel).Name}/value={value?.ToString() ?? "mixed"}/{state}", expected, actual);
                        }
        }
        Variant<UiFluentMediumCheckboxSize, UiFluentSquareCheckboxShape, UiCheckboxLabelAfter>();
        Variant<UiFluentMediumCheckboxSize, UiFluentSquareCheckboxShape, UiCheckboxLabelBefore>();
        Variant<UiFluentMediumCheckboxSize, UiFluentCircularCheckboxShape, UiCheckboxLabelAfter>();
        Variant<UiFluentMediumCheckboxSize, UiFluentCircularCheckboxShape, UiCheckboxLabelBefore>();
        Variant<UiFluentLargeCheckboxSize, UiFluentSquareCheckboxShape, UiCheckboxLabelAfter>();
        Variant<UiFluentLargeCheckboxSize, UiFluentSquareCheckboxShape, UiCheckboxLabelBefore>();
        Variant<UiFluentLargeCheckboxSize, UiFluentCircularCheckboxShape, UiCheckboxLabelAfter>();
        Variant<UiFluentLargeCheckboxSize, UiFluentCircularCheckboxShape, UiCheckboxLabelBefore>();
        foreach (var forced in new[] { false, true })
            foreach (var dark in new[] { false, true })
                foreach (var disabled in new[] { false, true })
                    foreach (var selected in new[] { false, true })
                    {
                        var theme = (dark ? UiCompiledDesign.Windows11Dark : UiCompiledDesign.Windows11).Theme;
                        fixture.Context.SetTheme(forced ? colors.Apply(theme) : theme);
                        using var viewer = Dux.MarkdownView("Task contrast", new UiState<string>((selected ? "- [x]" : "- [ ]") + " body").Binding,
                            new(Width: 264, Height: 200, ShowBorder: false));
                        var foreground = disabled ? colors.GrayText : colors.CanvasText;
                        var state = disabled ? UiButtonVisualState.Disabled : UiButtonVisualState.Rest;
                        var authored = UiFluentCheckboxPalette.Resolve(dark ? UiFluentColorScheme.Dark : UiFluentColorScheme.Light, state, selected);
                        var expected = fixture.Capture(ui =>
                        {
                            Begin(ui, disabled);
                            var text = UiFluentInputPalette.Resolve(new UiControlContext(ui), state, UiInputAppearance.Outline, false).Text;
                            StyledParagraphTests.PaintMarkdownObjectReference(fixture.Context, ui, "\uFFFC body", 0, new(16, 16), 240,
                                text, (paint, bounds) =>
                                {
                                    UiFluentSquareCheckboxShape.Paint(paint, bounds, forced ? selected ? colors.Canvas : default : authored.Background,
                                        forced ? foreground : authored.Border);
                                    if (selected)
                                        UiFluentCheckboxCheck12.Paint(paint, new(bounds.X + 2, bounds.Y + 2, 12, 12),
                                            forced ? foreground : authored.Indicator, default);
                                });
                            ui.EndDisabled();
                        });
                        var actual = fixture.Capture(ui =>
                        {
                            Begin(ui, disabled);
                            ui.SetCursorScreenPos(new(8, 10));
                            viewer.Render(ui);
                            ui.EndDisabled();
                        });
                        Compare($"task/forced={forced}/dark={dark}/disabled={disabled}/checked={selected}", expected, actual);
                    }
        if (output is not null)
            File.WriteAllText(Path.Combine(output, "pixel-hashes.json"), JsonSerializer.Serialize(hashes, GraphicsJson.Default.CheckboxPixelHashes));
    }
}
