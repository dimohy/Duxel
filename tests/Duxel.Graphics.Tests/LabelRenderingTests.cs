using Duxel.Core;

internal static class LabelRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check,
        int width, UiFluentColorScheme scheme)
    {
        using var input = Fluent.Input("Target", new UiState<string>("Text").Binding, 100, 220);
        using var label = Fluent.Label("User name", input, new(Required : true));
        var markerStart = 0f;
        void Draw(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding : 0);
            using (var scope = UiFluentMediumFieldSize.BeginLabel(new(ui)))
                markerStart = 24 + new UiControlContext(ui).MeasureText("User name").X + 4;
            ui.SetCursorScreenPos(new(24, 24));
            label.Render(ui);
            ui.SetCursorScreenPos(new(24, 100));
            input.Render(ui);
        }
        var initial = capture("required", Draw);
        bool ColorScale(byte[] pixels, byte[] reference, int left, int right, int[] source, int neutral, int tolerance)
        {
            var observed = false;
            for (var y = 24; y < 48; y++)
                for (var x = left; x < right; x++)
                {
                    var offset = (y * width + x) * 4;
                    for (var channel = 0; channel < 3; channel++)
                    {
                        observed |= reference[offset + channel] != 0;
                        if (Math.Abs(pixels[offset + channel] - reference[offset + channel] * neutral / (double)source[channel]) > tolerance)
                            return false;
                    }
                }
            return observed;
        }
        var markerLeft = (int)MathF.Floor(markerStart);
        var markerRight = Math.Min(width, markerLeft + 12);
        for (var index = 0; index < 3; index++)
            check(capture("replay-" + index, Draw).AsSpan().SequenceEqual(initial),
                scheme + " Label acquired-image replay stays pixel-identical / " + index);
        label.Options = label.Options with { Required = false };
        var optional = capture("optional", Draw);
        check(!optional.AsSpan().SequenceEqual(initial),
            scheme + " optional Label removes the required marker without changing the input");
        label.Options = label.Options with { Disabled = true, Required = true };
        var disabled = capture("disabled", Draw);
        var neutral = scheme == UiFluentColorScheme.Dark ? 92 : 189;
        var red = scheme == UiFluentColorScheme.Dark ? new[] { 227, 125, 128 } : new[] { 209, 52, 56 };
        var text = scheme == UiFluentColorScheme.Dark ? 255 : 36;
        // LCD coverage differs per channel; compare the pinned color modulation under identical glyph coverage.
        check(ColorScale(disabled, initial, markerLeft, markerRight, red, neutral, 4),
            scheme + " disabled Label marker uses neutral GPU color with unchanged LCD glyph coverage");
        check(ColorScale(disabled, initial, 24, markerLeft - 4, [text, text, text], neutral, 6),
            scheme + " disabled Label text uses neutral GPU color with unchanged LCD glyph coverage");
        label.Text = "A long label wraps without truncation";
        using var constrained = Dux.ConstrainedBox(Fluent.Label(label.Text), new(90, 90, 0, 200));
        var wrapped = capture("wrapped", ui =>
        {
            ui.EnableRootViewportContentLayout(contentPadding : 0);
            ui.SetCursorScreenPos(new(24, 24));
            constrained.Render(ui);
        });
        var empty = capture("empty", static _ => { });
        var lower = false;
        for (var y = 48; y < 130; y++)
            for (var x = 24; x < 114; x++)
            {
                var offset = (y * width + x) * 4;
                lower |= !wrapped.AsSpan(offset, 4).SequenceEqual(empty.AsSpan(offset, 4));
            }
        check(lower, scheme + " constrained Label renders wrapped glyphs below its first line on GPU");
    }
}
