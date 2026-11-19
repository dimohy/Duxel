using Duxel.Core;

internal static class SliderDirectionPixelFixture
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture,
        Action<bool, string> check, int stride, UiFluentColorScheme scheme)
    {
        const int x = 20, y = 20, width = 200, height = 32;
        var value = new UiState<float>(30);
        var slider = Fluent.Slider("Directional Slider", value.Binding, 0, 100, 15);
        slider.Size = new(width, height);
        using var provider = Fluent.FluentProvider(slider, options : new(Width : width), paintBackground : false);
        void Draw(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding : 0);
            ui.SetCursorScreenPos(new(x, y));
            provider.Render(ui);
        }
        foreach (var reversed in new[] { false, true })
        {
            slider.Options = slider.Options with { Reversed = reversed };
            provider.Direction = UiFlowDirection.LeftToRight;
            var leading = capture($"direction-{reversed}-ltr", Draw);
            provider.Direction = UiFlowDirection.RightToLeft;
            var trailing = capture($"direction-{reversed}-rtl", Draw);
            var maxError = 0;
            for (var row = y; row < y + height; row++)
            {
                for (var offset = 0; offset < width; offset++)
                {
                    var left = (row * stride + x + offset) * 4;
                    var right = (row * stride + x + width - 1 - offset) * 4;
                    for (var channel = 0; channel < 4; channel++)
                        maxError = Math.Max(maxError, Math.Abs(leading[left + channel] - trailing[right + channel]));
                }
            }
            check(maxError <= 1 && !leading.AsSpan().SequenceEqual(trailing),
                $"Slider Vulkan pixels mirror at unchanged value : {scheme} / Reversed {reversed} / max channel error {maxError}");
        }
        check(value.Value == 30, $"direction changes keep the Slider binding unchanged : {scheme}");
    }
}
