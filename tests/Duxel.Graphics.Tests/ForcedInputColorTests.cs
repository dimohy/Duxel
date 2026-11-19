using Duxel.Core;

internal static class ForcedInputColorTests
{
    internal static void Run(UiForcedColorPalette palette, Action<bool, string> check)
    {
        using var fixture = new PresentationReuseTests.Fixture(1, true);
        using var outline = Create<UiFluentOutlineInput>();
        using var underline = Create<UiFluentUnderlineInput>();
        using var darker = Create<UiFluentFilledDarkerInput>();
        using var lighter = Create<UiFluentFilledLighterInput>();
        UiWidget[] fields = [outline, underline, darker, lighter];
        var parent = new UiColor(11, 22, 33);
        var disabled = false;
        void Draw(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            ui.GetWindowDrawList().AddRectFilled(new(0, 0, 320, 240), parent, ui.WhiteTextureId);
            ui.BeginDisabled(disabled);
            for (var i = 0; i < fields.Length; i++)
            {
                ui.SetCursorScreenPos(new(10, 10 + i * 42));
                fields[i].Render(ui);
            }
            ui.EndDisabled();
        }
        byte[] Render(UiCompiledDesign design)
        {
            fixture.Context.SetTheme(palette.Apply(design.Theme));
            using var data = fixture.Build(Draw);
            return fixture.Render(data);
        }
        var width = (int)fixture.FrameInfo.DisplaySize.X;
        bool Pixel(byte[] pixels, int x, int y, UiColor color)
            => BitConverter.ToUInt32(pixels, (y * width + x) * 4) == color.Rgba;
        foreach (var isDisabled in new[] { false, true })
        {
            disabled = isDisabled;
            var dark = Render(UiCompiledDesign.Windows11Dark);
            var light = Render(UiCompiledDesign.Windows11);
            for (var i = 0; i < fields.Length; i++)
            {
                var y = 10 + i * 42;
                var text = isDisabled ? palette.GrayText : palette.CanvasText;
                var background = isDisabled || i == 1 ? parent : palette.Canvas;
                var border = i == 1
                    ? Pixel(dark, 50, y + 31, text)
                    : Pixel(dark, 10, y + 16, text) || Pixel(dark, 11, y + 16, text);
                var glyph = false;
                for (var dy = 5; dy < 27; dy++)
                    for (var dx = 20; dx < 42; dx++)
                        glyph |= Pixel(dark, dx, y + dy, text);
                check(border && glyph && Pixel(dark, 60, y + 5, background),
                    $"Forced colors / Input {(UiInputAppearance)i} disabled={isDisabled} uses system text, border and background in actual GPU output");
            }
            check(dark.AsSpan().SequenceEqual(light),
                $"Forced colors / Input disabled={isDisabled} is independent of the authored light/dark palette");
        }
        disabled = false;
        fixture.Context.SetTheme(palette.Apply(UiCompiledDesign.Windows11Dark.Theme));
        for (var i = 0; i < fields.Length; i++)
        {
            var input = new UiInputState(new(60, 26 + i * 42), false, false, false, false, false, false,
                0, 0, [], [], default, default)
            {
                LeftMouseDown = true,
                LeftMousePressed = true
            };
            using (var pressed = fixture.Build(Draw, input))
                fixture.Render(pressed);
            using var released = fixture.Build(Draw, input with
            {
                LeftMouseDown = false,
                LeftMousePressed = false,
                LeftMouseReleased = true
            });
            var pixels = fixture.Render(released);
            check(Pixel(pixels, 50, 40 + i * 42, palette.Highlight),
                $"Forced colors / Input {(UiInputAppearance)i} pointer focus uses the system Highlight bottom edge");
        }
    }

    private static UiWidget Create<TAppearance>() where TAppearance : IUiInputAppearance
        => Dux.SizedBox(new UiInput<UiFluentTextInputBehavior,
            UiFluentInputPresentation<UiFluentMediumInputSize, TAppearance>>(
                string.Empty, new UiState<string>("A").Binding, 64), 100, 32);
}
