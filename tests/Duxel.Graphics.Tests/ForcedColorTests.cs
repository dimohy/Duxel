using Duxel.App;
using Duxel.Core;
using Duxel.Platform.Windows;

internal static class ForcedColorTests
{
    internal static void Run(Action<bool, string> check)
    {
        var palette = new UiForcedColorPalette(new(4, 8, 16), new(240, 230, 220), new(110, 120, 130),
            new(10, 90, 180), new(250, 240, 230), new(30, 200, 220), new(20, 30, 40), new(210, 220, 230));
        var theme = palette.Apply(UiCompiledDesign.Windows11Dark.Theme);
        check(theme.ForcedColors == palette && theme.WindowBg == palette.Canvas && theme.Text == palette.CanvasText
            && theme.TextDisabled == palette.GrayText && theme.InputSelectionBg == palette.Highlight,
            "Forced colors / shared canvas, text, disabled and selection roles preserve custom system colors");
        check(theme.Button == palette.ButtonFace && theme.ButtonText == palette.ButtonText
            && theme.InputBorder == palette.CanvasText && theme.WindowTitleText == palette.CanvasText,
            "Forced colors / paired button colors and input/chrome roles preserve system contrast");
        var baseDesign = UiCompiledDesign.Windows11Dark;
        using var host = new ThemeHost { ForcedColors = palette };
        var options = new DuxelAppOptions { Screen = new EmptyScreen(), Design = baseDesign };
        var forced = DuxelApp.ResolveEffectiveDesign(options, host);
        check(forced.Theme.ForcedColors == palette && forced.Theme.Text == palette.CanvasText
            && forced.Style == baseDesign.Style && forced.Tokens == baseDesign.Tokens,
            "Forced colors / explicit design retains geometry while system accessibility colors take precedence");
        var optOut = DuxelApp.ResolveEffectiveDesign(options with
        {
            RespectSystemForcedColors = false
        }, host);
        check(optOut.Theme.ForcedColors is null && optOut.Theme.Text == baseDesign.Theme.Text,
            "Forced colors / explicit application opt-out retains the supplied design");
        host.ForcedColors = null;
        var restored = DuxelApp.ResolveEffectiveDesign(options, host);
        check(restored.Theme.ForcedColors is null && restored.Theme.Text == baseDesign.Theme.Text,
            "Forced colors / disabling the system mode restores the original authored design");
        check(((IPlatformThemeProvider)new OrdinaryProvider()).ForcedColors is null,
            "Forced colors / existing platform providers explicitly report no forced-color palette by default");
        var native = WindowsSystemTheme.GetForcedColors();
        check(WindowsSystemTheme.GetAppDesign().Theme.ForcedColors == native,
            "Forced colors / actual read-only Win32 query agrees with the public system design");
        RunRetained(palette, check);
        RunBadges(palette, check, 1);
        RunBadges(palette, check, 4);
        ForcedInputColorTests.Run(palette, check);
        SelectedTextColorTests.Run(palette, check);
        StyledSelectionColorTests.Run(palette, check);
        ImeSelectionColorTests.Run(palette, check);
        StyledContrastTests.Run(palette, check);
        CheckboxContrastTests.Run(null, check);
        SpinButtonColorTests.Run(palette, check);
        MenuContrastTests.Run(palette, check);
        OptionContrastTests.Run(palette, check);
    }

    private static void RunBadges(UiForcedColorPalette palette, Action<bool, string> check, int samples)
    {
        using var fixture = new PresentationReuseTests.Fixture(1, true, msaaSamples: samples);
        var counterText = new UiBadgeTextCache();
        var parentColor = new UiColor(11, 22, 33);
        void Draw(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            ui.GetWindowDrawList().AddRectFilled(new(0, 0, fixture.FrameInfo.DisplaySize.X, fixture.FrameInfo.DisplaySize.Y), parentColor, ui.WhiteTextureId);
            for (var row = 0; row < 4; row++)
                for (var column = 0; column < 8; column++)
                {
                    ui.SetCursorScreenPos(new(10 + column * 36, 10 + row * 30));
                    ui.Badge("A", new(UiBadgeSize.Large, UiBadgeShape.Square, (UiBadgeAppearance)row, (UiBadgeColor)column));
                }
            ui.SetCursorScreenPos(new(10, 160));
            ui.CounterBadge(new(7), ref counterText, new(UiBadgeSize.Large, UiBadgeShape.Rounded, UiBadgeAppearance.Ghost));
        }
        byte[] Render(UiCompiledDesign design)
        {
            fixture.Context.SetTheme(palette.Apply(design.Theme));
            using var data = fixture.Build(Draw);
            return fixture.Render(data);
        }
        var dark = Render(UiCompiledDesign.Windows11Dark);
        var light = Render(UiCompiledDesign.Windows11);
        var width = (int)fixture.FrameInfo.DisplaySize.X;
        bool Pixel(int x, int y, UiColor color)
            => BitConverter.ToUInt32(dark, (y * width + x) * 4) == color.Rgba;
        for (var row = 0; row < 4; row++)
        {
            var appearance = (UiBadgeAppearance)row;
            var expectedBorder = appearance != UiBadgeAppearance.Ghost;
            var correct = true;
            for (var column = 0; column < 8; column++)
            {
                var x = 10 + column * 36;
                var y = 10 + row * 30;
                var border = false;
                var text = false;
                for (var dy = 3; dy < 21; dy++)
                {
                    border |= Pixel(x, y + dy, palette.CanvasText) || Pixel(x + 1, y + dy, palette.CanvasText);
                    for (var dx = 4; dx < 20; dx++)
                        text |= Pixel(x + dx, y + dy, palette.CanvasText);
                }
                var background = row is 1 or 2 ? parentColor : palette.Canvas;
                correct &= border == expectedBorder && text && Pixel(x + 2, y + 2, background);
            }
            check(correct, $"Forced colors / Badge {appearance} matches system background, text and border across eight semantic colors at MSAA {samples}");
        }
        check(dark.AsSpan().SequenceEqual(light),
            $"Forced colors / Badge output depends on the system palette at MSAA {samples}");
        check(Pixel(10, 172, parentColor) && Pixel(13, 172, parentColor),
            $"Forced colors / Ghost CounterBadge keeps its transparent background and border at MSAA {samples}");
    }

    private static void RunRetained(UiForcedColorPalette first, Action<bool, string> check)
    {
        using var fixture = new PresentationReuseTests.Fixture(1, true);
        var builds = 0;
        void Draw(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            if (ui.BeginLayer("forced-colors-retained", new(true, 1, default)
            {
                ContentVersion = 1
            }))
            {
                builds++;
                ui.GetWindowDrawList().AddRectFilled(new(0, 0, 60, 40), new UiControlContext(ui).Theme.Text, ui.WhiteTextureId);
            }
            ui.EndLayer();
        }
        fixture.Context.SetTheme(first.Apply(UiCompiledDesign.Windows11Dark.Theme));
        using (var initial = fixture.Build(Draw))
            fixture.Render(initial);
        using (var warm = fixture.Build(Draw))
            fixture.Render(warm);
        var warmBuilds = builds;
        var next = first with
        {
            CanvasText = new(20, 210, 70)
        };
        fixture.Context.SetTheme(next.Apply(UiCompiledDesign.Windows11Dark.Theme));
        using var changed = fixture.Build(Draw);
        var pixels = fixture.Render(changed);
        var pixel = (5 * (int)fixture.FrameInfo.DisplaySize.X + 5) * 4;
        check(builds == warmBuilds + 1 && pixels[pixel] == 20 && pixels[pixel + 1] == 210 && pixels[pixel + 2] == 70,
            "Forced colors / changing the palette inside the same dark scheme invalidates retained paint and reaches actual GPU pixels");
    }

    private sealed class EmptyScreen : UiScreen
    {
        public override void Render(UiImmediateContext ui)
        {
        }
    }
    private sealed class OrdinaryProvider : IPlatformThemeProvider
    {
        public UiSystemColorScheme ColorScheme => UiSystemColorScheme.Dark;
    }
    private sealed class ThemeHost : IPlatformBackend, IPlatformThemeProvider
    {
        public UiSystemColorScheme ColorScheme => UiSystemColorScheme.Dark;
        public UiForcedColorPalette? ForcedColors
        {
            get; set;
        }
        public PlatformSize WindowSize => throw new NotSupportedException();
        public PlatformSize FramebufferSize => throw new NotSupportedException();
        public bool IsInteractingResize => throw new NotSupportedException();
        public bool ShouldClose => throw new NotSupportedException();
        public double TimeSeconds => throw new NotSupportedException();
        public IInputBackend Input => throw new NotSupportedException();
        public IVulkanSurfaceSource? VulkanSurface => throw new NotSupportedException();
        public IPlatformTextBackend? TextBackend => throw new NotSupportedException();
        public void PollEvents() => throw new NotSupportedException();
        public void WaitEvents(int timeoutMilliseconds) => throw new NotSupportedException();
        public void SetMouseCursor(UiMouseCursor cursor) => throw new NotSupportedException();
        public void Dispose()
        {
        }
    }
}
