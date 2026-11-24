using Duxel.Core;

internal static class ButtonContrastTests
{
    internal static void Run(string output, Action<bool, string> check)
    {
        var colors = new UiForcedColorPalette(new(3, 7, 11), new(242, 232, 222), new(140, 150, 160),
            new(24, 96, 176), new(252, 244, 236), new(30, 205, 220), new(45, 55, 65), new(210, 220, 230));
        var backdrop = new UiColor(11, 22, 33);
        using var fixture = new PresentationReuseTests.Fixture(1, true);
        Variant<UiFluentSecondaryButtonAppearance>("secondary", colors.ButtonFace, colors.HighlightText);
        Variant<UiFluentPrimaryButtonAppearance>("primary", colors.Highlight, colors.HighlightText);
        Variant<UiFluentOutlineButtonAppearance>("outline", backdrop, colors.HighlightText);
        Variant<UiFluentSubtleButtonAppearance>("subtle", backdrop, colors.ButtonFace);
        Variant<UiFluentTransparentButtonAppearance>("transparent", backdrop, backdrop);
        CheckMenuTriggers();

        void Variant<TAppearance>(string name, UiColor restFill, UiColor activeFill)
            where TAppearance : IUiButtonAppearance
        {
            var rest = default(UiButtonInteraction);
            var focus = rest with { Focused = true, FocusVisible = true };
            var cases = new (string Name, UiButtonInteraction Input, bool Enabled, bool Active)[]
            {
                ("rest", rest, true, false),
                ("hover", rest with { Hovered = true }, true, true),
                ("pressed", rest with { Hovered = true, Held = true }, true, true),
                ("focus", focus, true, false),
                ("keyboard-pressed", focus with { Held = true }, true, true),
                ("disabled", rest with { Hovered = true }, false, false)
            };
            var before = UiCompiledDesign.Windows11Dark.Theme;
            foreach (var forced in new[] { false, true })
            {
                fixture.Context.SetTheme(forced ? colors.Apply(before) : before);
                foreach (var test in cases)
                {
                    var pixels = fixture.Capture(ui =>
                    {
                        ui.EnableRootViewportContentLayout(contentPadding: 0);
                        var context = new UiControlContext(ui);
                        context.Frame(new(0, 0, fixture.FrameInfo.DisplaySize.X, fixture.FrameInfo.DisplaySize.Y), backdrop, default, 0, 0);
                        ui.BeginDisabled(!test.Enabled);
                        using var text = UiFluentButtonPresentation.BeginText(context, true);
                        var textSize = context.MeasureText("Button");
                        UiFluentButtonPresentation<TAppearance>.Paint(context, "Button", textSize,
                            new(20, 20, 120, 40), test.Input);
                        UiFluentIconButtonPresentation<UiFluentMediumButtonIconSize, TAppearance, Probe, UiButtonIconBefore>
                            .Paint(context, string.Empty, default, new(160, 20, 48, 40), test.Input);
                        ui.EndDisabled();
                    });
                    var key = $"{(forced ? "forced" : "ordinary")}-{name}-{test.Name}";
                    fixture.WriteCapturePng(Path.Combine(output, key + ".png"), pixels);
                    if (!forced)
                        continue;

                    var primary = name == "primary";
                    var fill = !test.Enabled ? colors.ButtonFace : test.Active ? activeFill : restFill;
                    var textColor = !test.Enabled ? colors.GrayText
                        : test.Active ? colors.Highlight : primary ? colors.HighlightText : colors.ButtonText;
                    var focusColor = primary && !test.Active ? colors.HighlightText : colors.Highlight;
                    var border = test.Input.FocusVisible && name is not ("subtle" or "transparent") ? focusColor : textColor;
                    bool Matches(int x, int y, UiColor color)
                    {
                        var at = (y * fixture.LastCaptureSize.Width + x) * 4;
                        var rgba = color.Rgba;
                        return pixels[at] == (byte)rgba && pixels[at + 1] == (byte)(rgba >> 8) && pixels[at + 2] == (byte)(rgba >> 16);
                    }
                    check(Matches(31, 39, fill), $"Button contrast: {key} fill");
                    check(Matches(80, 20, border), $"Button contrast: {key} border");
                    check(Matches(80, 21, test.Input.FocusVisible ? focusColor : fill), $"Button contrast: {key} inset focus");
                    check(Matches(184, 40, textColor), $"Button contrast: {key} icon");
                    var hasLabelInk = Enumerable.Range(30, 20).Any(y => Enumerable.Range(54, 52).Any(x => Matches(x, y, textColor)));
                    check(hasLabelInk, $"Button contrast: {key} label");
                }
            }
        }

        void CheckMenuTriggers()
        {
            using var split = Fluent.SplitButton("Save", "Formats", static () => { },
                [new("pdf", "PDF")], static _ => { });
            using var menu = Fluent.MenuButton("Export", [new("pdf", "PDF")], static _ => { });
            var dark = UiCompiledDesign.Windows11Dark.Theme;
            var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);

            byte[] Capture(bool disabled, UiInputState input) => fixture.Capture(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding: 0);
                var context = new UiControlContext(ui);
                context.Frame(new(0, 0, fixture.FrameInfo.DisplaySize.X, fixture.FrameInfo.DisplaySize.Y), backdrop, default, 0, 0);
                ui.BeginDisabled(disabled);
                ui.SetCursorScreenPos(new(20, 20));
                split.Render(ui);
                ui.SetCursorScreenPos(new(20, 80));
                menu.Render(ui);
                ui.EndDisabled();
            }, input);

            fixture.Context.SetTheme(dark);
            fixture.WriteCapturePng(Path.Combine(output, "ordinary-menu-triggers.png"), Capture(false, idle));
            fixture.Context.SetTheme(colors.Apply(dark));
            var normal = Capture(false, idle);
            CheckPixel(normal, 116, 31, colors.ButtonText, "SplitButton rest divider");
            CheckPixel(normal, 24, 94, colors.ButtonFace, "MenuButton rest surface");
            var disabled = Capture(true, idle);
            CheckPixel(disabled, 116, 31, colors.GrayText, "SplitButton disabled divider");
            CheckPixel(disabled, 65, 80, colors.GrayText, "MenuButton disabled border");
            var hover = Capture(false, idle with { MousePosition = new(45, 34) });
            CheckPixel(hover, 116, 31, colors.Highlight, "SplitButton primary hover divider");
            var tab = idle with { KeyEvents = [new(UiKey.Tab, true, KeyModifiers.None)] };
            var primaryFocus = Capture(false, tab);
            CheckPixel(primaryFocus, 65, 22, colors.Highlight, "SplitButton primary focus outline");
            var disclosureFocus = Capture(false, tab);
            CheckPixel(disclosureFocus, 129, 22, colors.Highlight, "SplitButton disclosure focus outline");
            var menuFocus = Capture(false, tab);
            CheckPixel(menuFocus, 65, 81, colors.Highlight, "MenuButton focus outline");

            void CheckPixel(byte[] pixels, int x, int y, UiColor color, string label)
            {
                var at = (y * fixture.LastCaptureSize.Width + x) * 4;
                var rgba = color.Rgba;
                check(pixels[at] == (byte)rgba && pixels[at + 1] == (byte)(rgba >> 8) && pixels[at + 2] == (byte)(rgba >> 16), label);
            }
        }
    }

    private readonly struct Probe : IUiButtonIcon
    {
        public static void Paint(UiControlContext context, UiRect bounds, UiColor color, UiButtonInteraction interaction)
            => context.Frame(bounds, color, default, 0, 0);
    }
}
