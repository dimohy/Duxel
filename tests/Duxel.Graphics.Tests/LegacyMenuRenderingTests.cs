using Duxel.Core;
using Duxel.Platform.Windows;

internal static class LegacyMenuRenderingTests
{
    internal static void Run(string output, Action<bool, string> check)
    {
        using var fixture = new PresentationReuseTests.Fixture(2, true);
        var font = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "consola.ttf");
        fixture.Context.SetDirectTextPrimaryFontPath(font);
        fixture.Context.SetDirectTextFallbackEnabled(false);
        var unsupportedMark = false;
        try
        {
            WindowsPlatformTextBackend.Instance.TryLayout(new(font, null, "\u2713", 16), out _);
        }
        catch (NotSupportedException error) when (error.Message == "No selected font contains U+2713.")
        {
            unsupportedMark = true;
        }
        check(unsupportedMark, "DirectWrite reproduces the original unavailable checkmark glyph with the fixture font");

        var selected = false;
        var item = default(UiRect);
        void Draw(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding : 0);
            ui.SetCursorScreenPos(new(24, 24));
            ui.MenuItem("Open", selected);
            var min = ui.GetItemRectMin();
            var size = ui.GetItemRectSize();
            item = new(min.X, min.Y, size.X, size.Y);
        }

        foreach (var samples in new[] { 1, 4 })
        {
            fixture.Renderer.SetMsaaSamples(samples);
            foreach (var scheme in new[] { UiFluentColorScheme.Light, UiFluentColorScheme.Dark })
            {
                fixture.Context.SetTheme(scheme == UiFluentColorScheme.Light
                    ? UiCompiledDesign.Windows11.Theme
                    : UiCompiledDesign.Windows11Dark.Theme);
                selected = false;
                var uncheckedPixels = fixture.Capture(Draw);
                var baselineBounds = item;
                selected = true;
                var checkedPixels = fixture.Capture(Draw);
                check(item == baselineBounds, $"Selected and unselected items keep the same bounds : {scheme} / {samples}");

                int MarkPixels(byte[] pixels)
                {
                    var stride = fixture.LastCaptureSize.Width * 4;
                    var background = ((int)item.Y + (int)item.Height / 2) * stride + ((int)item.X + 2) * 4;
                    var pixelsWithInk = 0;
                    for (var y = (int)item.Y + 2; y < item.Y + item.Height - 2; y++)
                    {
                        for (var x = (int)item.X + 4; x < item.X + 22; x++)
                        {
                            var offset = y * stride + x * 4;
                            if (Math.Abs(pixels[offset] - pixels[background]) > 10 ||
                                Math.Abs(pixels[offset + 1] - pixels[background + 1]) > 10 ||
                                Math.Abs(pixels[offset + 2] - pixels[background + 2]) > 10)
                                pixelsWithInk++;
                        }
                    }
                    return pixelsWithInk;
                }

                check(MarkPixels(uncheckedPixels) == 0 && MarkPixels(checkedPixels) > 12,
                    $"Vulkan paints the checkmark in its own slot only for a selected item : {scheme} / {samples}");
                selected = false;
                check(fixture.Capture(Draw).AsSpan().SequenceEqual(uncheckedPixels),
                    $"Deselecting the item clears the checkmark on the next Vulkan frame : {scheme} / {samples}");
                fixture.WriteCapturePng(Path.Combine(output, $"legacy-{scheme}-{samples}-selected.png"), checkedPixels);
                fixture.WriteCapturePng(Path.Combine(output, $"legacy-{scheme}-{samples}-unselected.png"), uncheckedPixels);
            }
        }
    }
}
