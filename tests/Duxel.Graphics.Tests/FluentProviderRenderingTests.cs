using Duxel.Core;

internal static class FluentProviderRenderingTests
{
    internal static void RunFonts(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check)
    {
        var fonts = new Duxel.App.DuxelFontOptions().Typography;
        var mono = fonts.Monospace ?? throw new InvalidOperationException("Font GPU fixture requires monospace.");
        using var provider = Fluent.FluentProvider(Dux.Column(new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min),
            Fluent.Subtitle1("Wide WWW and narrow iii"), Fluent.Body1("Native scoped text Agyp 123")),
            new(UiCompiledDesign.Windows11.Theme, fonts : fonts), new(Padding : new(12), Width : 400));
        void Draw(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding : 0);
            provider.Render(ui);
        }
        var proportional = capture("provider-font-proportional", Draw);
        provider.Theme = new(UiCompiledDesign.Windows11.Theme,
            fonts : fonts with { Regular = mono, Semibold = mono, Bold = mono });
        var monospace = capture("provider-font-monospace", Draw);
        check(!proportional.AsSpan().SequenceEqual(monospace), "local font override changes actual Vulkan glyph output");
        var stable = capture("provider-font-stable", Draw);
        check(monospace.AsSpan().SequenceEqual(stable), "unchanged local fonts preserve exact Vulkan pixels");
        provider.Theme = new(UiCompiledDesign.Windows11.Theme, fonts : fonts);
        var restored = capture("provider-font-restored", Draw);
        check(proportional.AsSpan().SequenceEqual(restored), "returning local fonts restores exact original Vulkan pixels");
    }
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check, int width)
    {
        using var left = Fluent.FluentProvider(Fluent.Button("Light"), UiFluentProviderTheme.Light,
            new(Padding : new(12), Width : 132, Height : 180));
        using var right = Fluent.FluentProvider(Fluent.Button("Dark"), UiFluentProviderTheme.Dark,
            new(Padding : new(12), Width : 132, Height : 180));
        UiTheme outside = default, restored = default;
        void Draw(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding : 0);
            outside = ui.GetStyle();
            ui.SetCursorScreenPos(new(8, 8));
            left.Render(ui);
            ui.SetCursorScreenPos(new(168, 8));
            right.Render(ui);
            restored = ui.GetStyle();
        }
        bool Pixel(byte[] pixels, int x, int y, UiColor color)
        {
            var offset = (y * width + x) * 4;
            return pixels[offset] == (byte)color.Rgba && pixels[offset + 1] == (byte)(color.Rgba >> 8)
                && pixels[offset + 2] == (byte)(color.Rgba >> 16);
        }
        var first = capture("provider-light-dark", Draw);
        check(Pixel(first, 12, 160, UiFluentProviderTheme.Light.Palette.WindowBg)
            && Pixel(first, 172, 160, UiFluentProviderTheme.Dark.Palette.WindowBg),
            "Provider GPU paints independent light and dark local surfaces");
        check(Pixel(first, 50, 35, new(255, 255, 255)) && Pixel(first, 210, 35, new(41, 41, 41)),
            "real Fluent children resolve different local schemes in the same frame");
        check(restored.WindowBg == outside.WindowBg && restored.FluentColorScheme == outside.FluentColorScheme,
            "Provider GPU rendering does not mutate the app theme");
        var same = capture("provider-stable", Draw);
        check(first.AsSpan().SequenceEqual(same), "unchanged Provider frames preserve exact pixels");
        left.Theme = UiFluentProviderTheme.Dark;
        var changed = capture("provider-local-change", Draw);
        check(Pixel(changed, 12, 160, UiFluentProviderTheme.Dark.Palette.WindowBg)
            && Pixel(changed, 50, 35, new(41, 41, 41)) && Pixel(changed, 210, 35, new(41, 41, 41)),
            "changing one Provider updates its real child without changing the neighboring scheme");
        left.PaintBackground = false;
        var transparent = capture("provider-transparent", Draw);
        check(!Pixel(transparent, 12, 160, UiFluentProviderTheme.Dark.Palette.WindowBg)
            && Pixel(transparent, 50, 35, new(41, 41, 41)),
            "transparent Provider omits its surface while retaining child theming");
    }
}
