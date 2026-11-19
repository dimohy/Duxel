using Duxel.Core;
using Duxel.Svg;

internal static class SvgFeatureRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check,
        int width, string output, int samples)
    {
        var path = Path.Combine(output, "gpu-features-" + samples + ".svg");
        File.WriteAllText(path, """
            <svg xmlns="http://www.w3.org/2000/svg" width="64" height="48">
              <style>/* Documentation only: @import 'unused.css'; url(https://example.invalid/unused.png) */</style>
              <defs>
                <pattern id="p" patternUnits="userSpaceOnUse" width="8" height="8"><rect width="4" height="8" fill="#20c060"/></pattern>
                <symbol id="s" viewBox="0 0 8 8"><rect width="8" height="8" fill="currentColor"/></symbol>
                <mask id="m" maskUnits="userSpaceOnUse" x="0" y="0" width="64" height="48"><rect x="48" y="24" width="8" height="16" fill="white"/></mask>
              </defs>
              <rect width="24" height="48" fill="url(#p)"/>
              <use href="#s" x="32" y="8" width="16" height="16" color="#e03020"/>
              <rect x="48" y="24" width="16" height="16" fill="#2080e0" mask="url(#m)"/>
            </svg>
            """);
        using var store = new SvgStore();
        using var widget = new UiSvg(store.Load(path));
        var textures = default(UiSvgTextureStatistics);
        void Draw(UiImmediateContext ui)
        {
            ui.GetWindowDrawList().AddRectFilled(new(24, 24, 64, 48), new(20, 50, 80), ui.WhiteTextureId);
            ui.SetCursorScreenPos(new(24, 24));
            widget.Render(ui);
            textures = ui.SvgTextureStatistics;
        }
        var first = capture("reference", Draw);
        bool Pixel(int x, int y, int r, int g, int b)
        {
            var offset = ((24 + y) * width + 24 + x) * 4;
            return Math.Abs(first[offset] - r) <= 1 && Math.Abs(first[offset + 1] - g) <= 1 && Math.Abs(first[offset + 2] - b) <= 1;
        }
        check(Pixel(36, 12, 224, 48, 32), "SVG GPU symbol/use preserves currentColor and its viewport at MSAA " + samples);
        check(Pixel(2, 12, 32, 192, 96) && Pixel(6, 12, 20, 50, 80) && Pixel(10, 12, 32, 192, 96),
            "SVG GPU pattern repeats authored color and transparent backdrop at MSAA " + samples);
        check(Pixel(52, 32, 32, 128, 224) && Pixel(60, 32, 20, 50, 80),
            "SVG GPU mask preserves visible and excluded geometry at MSAA " + samples);
        var source = store.Statistics;
        var uploads = textures.GpuRenderRequests;
        var equal = true;
        for (var replay = 0; replay < 3; replay++)
            equal &= capture("replay-" + replay, Draw).AsSpan().SequenceEqual(first);
        check(equal && textures.GpuRenderRequests == uploads && store.Statistics == source,
            "SVG GPU feature replay reuses scene, raster and uploads across acquired images at MSAA " + samples);
        foreach (var name in new[] { "Red_Gallardo", "TheStructorr_Lamborghini_Gallardo" })
        {
            var document = store.Load(Path.Combine(AppContext.BaseDirectory, "Assets", "Svg", "Browser", name + ".svg"));
            using var complex = new UiSvg(document) { Size = new(192, 96) };
            var expected = document.Rasterize(192, 96);
            void Complex(UiImmediateContext ui)
            {
                ui.GetWindowDrawList().AddRectFilled(new(24, 24, 192, 96), new(255, 255, 255), ui.WhiteTextureId);
                ui.SetCursorScreenPos(new(24, 24));
                complex.Render(ui);
            }
            var pixels = capture(name, Complex);
            var matches = true;
            var differingPixels = 0;
            var maximumDifference = 0d;
            for (var y = 0; y < 96; y++)
                for (var x = 0; x < 192; x++)
                {
                    var input = (y * 192 + x) * 4;
                    var pixel = ((24 + y) * width + 24 + x) * 4;
                    var alpha = expected.Pixels.Span[input + 3];
                    var pixelMatches = true;
                    for (var channel = 0; channel < 3; channel++)
                    {
                        var value = (expected.Pixels.Span[input + channel] * alpha + 255 * (255 - alpha)) / 255d;
                        var difference = Math.Abs(pixels[pixel + channel] - value);
                        maximumDifference = Math.Max(maximumDifference, difference);
                        pixelMatches &= difference <= 1;
                    }
                    if (!pixelMatches)
                        differingPixels++;
                    matches &= pixelMatches;
                }
            Console.WriteLine($"SVG PIXEL ORACLE {name}: {differingPixels} differing pixels, maximum channel difference {maximumDifference:F4}");
            File.WriteAllBytes(Path.Combine(output, $"msaa{samples}-{name}-scalar-reference.rgba"), expected.Pixels.ToArray());
            check(matches, "Complex SVG GPU output preserves every retained raster pixel at MSAA " + samples + ": " + name);
            var beforeReplay = store.Statistics;
            check(capture(name + "-replay", Complex).AsSpan().SequenceEqual(pixels) && store.Statistics == beforeReplay,
                "Complex SVG GPU replay keeps exact acquired-image pixels and cached scene: " + name);
        }
    }
}
