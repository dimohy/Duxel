using Duxel.Core;
using Duxel.Vulkan;

internal static class RasterTextScrollRenderingTests
{
    internal static void Run(VulkanRendererBackend renderer, Func<string, Action<UiImmediateContext>, byte[]> capture,
        Action<bool, string> check, int width, int height, Action<bool> setPixelSnap, Action<float> setTextScale)
    {
        var rawUnstable = 0;
        foreach (var textScale in new[] { 1f, 1.14f })
        {
            setTextScale(textScale);
            foreach (var snap in new[] { false, true })
            {
                setPixelSnap(snap);
                foreach (var samples in new[] { 1, 4 })
                    foreach (var style in new[] { 0, 1, 2 })
                    {
                        renderer.SetMsaaSamples(samples);
                        byte[]? reference = null;
                        var stable = true;
                        for (var step = 0; step < 8; step++)
                        {
                            var pixels = capture($"scroll-text-{textScale}-{snap}-{samples}-{style}-{step}", ui =>
                            {
                                var clip = new UiRect(0, 0, width, height);
                                ui.GetWindowDrawList().AddRectFilled(clip, new(32, 32, 32), ui.WhiteTextureId, clip);
                                ui.SetCursorScreenPos(new(20, 30 + step / 8f));
                                if (style == 0)
                                    ui.Text("한글 English Agyp : 스크롤 중 글자");
                                else if (style == 1)
                                    ui.RenderTypography<UiRegularTypeface, UiFluentType300>("본문 English Agyp", new(255, 255, 255));
                                else
                                    ui.RenderTypography<UiSemiboldTypeface, UiFluentType600>("Duxel 구현 관찰", new(255, 255, 255));
                            });
                            var top = height;
                            var bottom = -1;
                            for (var y = 0; y < height; y++)
                                for (var x = 0; x < width; x++)
                                    if (pixels[(y * width + x) * 4] != 32 || pixels[(y * width + x) * 4 + 1] != 32 || pixels[(y * width + x) * 4 + 2] != 32)
                                    {
                                        top = Math.Min(top, y);
                                        bottom = Math.Max(bottom, y);
                                    }
                            if (bottom < top)
                            {
                                stable = false;
                                continue;
                            }
                            var cropped = pixels.AsSpan(top * width * 4, (bottom - top + 1) * width * 4).ToArray();
                            reference ??= cropped;
                            stable &= reference.AsSpan().SequenceEqual(cropped);
                        }
                        if (snap)
                            check(stable, $"Fractional scrolling preserves native raster coverage after whole-pixel translation : text={textScale}, style={style}, MSAA{samples}");
                        else if (!stable)
                            rawUnstable++;
                    }
            }
        }
        Console.WriteLine($"Raw fractional placement unstable cohorts : {rawUnstable}/12");
        check(rawUnstable > 0, "The unsnapped control reproduces fractional-scroll coverage instability");
        setPixelSnap(true);
        setTextScale(1);
        renderer.SetMsaaSamples(1);
    }
}
