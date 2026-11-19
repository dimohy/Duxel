using System.Text.Json;
using Duxel.Core;
using Duxel.Svg;

internal static class GpuSvgPixelTests
{
    internal static void Run(string output, UiContext context, Func<string, Action<UiImmediateContext>, byte[]> capture,
        Action<bool, string> check, int snapshotWidth, bool strokeOnly = false, bool dashOnly = false)
    {
        (string Name, string Content)[] cases =
        [
            ("cubic", "<path d='M 3 40 C 10 -20 50 65 61 7 L 60 44 Z' fill='#e03020'/>"),
            ("transformed-cubic", "<g transform='translate(3 4) scale(.83)'><path d='M 3 40 C 10 -20 50 65 61 7 L 60 44 Z' fill='#e03020'/></g>"),
            ("linear", "<defs><linearGradient id='p'><stop stop-color='red'/><stop offset='1' stop-color='blue'/></linearGradient></defs><path d='M 3 40 C 10 -20 50 65 61 7 L 60 44 Z' fill='url(#p)'/>"),
            ("radial", "<defs><radialGradient id='p'><stop stop-color='red'/><stop offset='1' stop-color='blue'/></radialGradient></defs><path d='M 3 40 C 10 -20 50 65 61 7 L 60 44 Z' fill='url(#p)'/>"),
            ("opacity", "<g opacity='.43'><path d='M 3 40 C 10 -20 50 65 61 7 L 60 44 Z' fill='#e03020'/><rect x='16' y='16' width='23' height='17' fill='blue'/></g>"),
            ("clip", "<defs><clipPath id='p'><path d='M 3 40 C 10 -20 50 65 61 7 L 60 44 Z'/></clipPath></defs><rect width='64' height='48' fill='red' clip-path='url(#p)'/>"),
            ("mask", "<defs><mask id='p'><path d='M 3 40 C 10 -20 50 65 61 7 L 60 44 Z' fill='white'/></mask></defs><rect width='64' height='48' fill='red' mask='url(#p)'/>"),
            ("rounded", "<rect x='3.2' y='5.3' width='56.4' height='37.8' rx='8' fill='red'/>"),
            ("ellipse", "<ellipse cx='32.2' cy='24.3' rx='28' ry='19' fill='red'/>"),
            ("transformed-rounded", "<g transform='translate(10 3) rotate(13) scale(.7 .8)'><rect x='3.2' y='5.3' width='56.4' height='37.8' rx='8' fill='red'/></g>"),
            ("overlapping-mask", "<defs><mask id='m'><path d='M 2 8 L 62 39 L 12 46 Z' fill='white'/></mask></defs><path d='M 3 40 C 10 -20 50 65 61 7 L 60 44 Z' fill='red' mask='url(#m)'/>"),
            ("fractional-rectangle", "<rect x='3.2' y='5.3' width='56.4' height='37.8' fill='red'/>"),
            ("stroke", "<path d='M 4 36 C 15 -3 35 57 60 10' stroke='red' stroke-width='3.2' stroke-linecap='round' fill='none'/>"),
            ("pattern", "<defs><pattern id='p' patternUnits='userSpaceOnUse' width='7.3' height='8.2'><rect width='3.4' height='8.2' fill='red'/></pattern></defs><rect width='64' height='48' fill='url(#p)'/>"),
            ("blur", "<defs><filter id='p' filterUnits='userSpaceOnUse' x='1.3' y='1.7' width='61' height='44' color-interpolation-filters='sRGB'><feGaussianBlur stdDeviation='1.4'/></filter></defs><path d='M 3 40 C 10 -20 50 65 61 7 L 60 44 Z' fill='red' filter='url(#p)'/>"),
            ("filtered-pattern", "<defs><filter id='b' filterUnits='userSpaceOnUse' x='0' y='0' width='7.3' height='8.2'><feGaussianBlur stdDeviation='.5'/></filter><pattern id='p' patternUnits='userSpaceOnUse' width='7.3' height='8.2'><rect x='2' y='2' width='3.4' height='4.2' fill='red' filter='url(#b)'/></pattern></defs><rect width='64' height='48' fill='url(#p)'/>"),
            ("conic-clip", "<defs><clipPath id='c'><path d='M56 24A24 18 0 1 1 8 24A24 18 0 1 1 56 24Z'/></clipPath></defs><rect width='64' height='48' fill='red' clip-path='url(#c)'/>"),
            ("conic-mask", "<defs><mask id='m'><path d='M56 24A24 18 0 1 1 8 24A24 18 0 1 1 56 24Z' fill='white'/></mask></defs><rect width='64' height='48' fill='red' mask='url(#m)'/>"),
            ("conic-blur", "<defs><filter id='b' color-interpolation-filters='sRGB'><feGaussianBlur stdDeviation='.7'/></filter></defs><path d='M56 24A24 18 0 1 1 8 24A24 18 0 1 1 56 24Z' fill='red' filter='url(#b)'/>")
        ];
        if (strokeOnly)
            cases =
            [
                ("line-butt", "<path d='M5 10L55 35' stroke='red' stroke-width='3.2' fill='none'/>"),
                ("line-round", "<path d='M5 10L55 35' stroke='red' stroke-width='3.2' stroke-linecap='round' fill='none'/>"),
                ("line-square", "<path d='M5 10L55 35' stroke='red' stroke-width='3.2' stroke-linecap='square' fill='none'/>"),
                ("join-round", "<path d='M5 38L23 8L31 38L57 10' stroke='red' stroke-width='5' stroke-linejoin='round' fill='none'/>"),
                ("join-miter", "<path d='M5 38L23 8L31 38L57 10' stroke='red' stroke-width='5' stroke-linejoin='miter' fill='none'/>"),
                ("join-bevel", "<path d='M5 38L23 8L31 38L57 10' stroke='red' stroke-width='5' stroke-linejoin='bevel' fill='none'/>"),
                ("curve-square", "<path d='M4 36C15 -3 35 57 60 10' stroke='red' stroke-width='3.2' stroke-linecap='square' fill='none'/>"),
                ("curve-reflection", "<g transform='translate(62 2) scale(-.9 .83)'><path d='M4 36C15 -3 35 57 60 10' stroke='red' stroke-width='3.2' stroke-linecap='round' fill='none'/></g>"),
                ("closed-curve", "<path d='M10 30C5 2 58 2 54 30C45 47 18 45 10 30Z' stroke='red' stroke-width='3' fill='none'/>"),
                ("arc", "<path d='M8 30A22 15 25 1 1 53 25' stroke='red' stroke-width='3.2' stroke-linecap='round' fill='none'/>"),
                ("reversal", "<path d='M8 24L54 24L8 24' stroke='red' stroke-width='3.2' stroke-linejoin='round' fill='none'/>"),
                ("dot-round", "<path d='M24 24L24 24' stroke='red' stroke-width='5' stroke-linecap='round' fill='none'/>"),
                ("dot-square", "<path d='M24 24L24 24' stroke='red' stroke-width='5' stroke-linecap='square' fill='none'/>"),
                ("dash-line", "<path d='M5 10L55 35' stroke='red' stroke-width='3.2' stroke-dasharray='4 3' stroke-linecap='round' fill='none'/>"),
                ("dash-curve", "<path d='M4 36C15 -3 35 57 60 10' stroke='red' stroke-width='3.2' stroke-dasharray='4 3' stroke-linecap='round' fill='none'/>")
            ];
        using var store = new SvgStore();
        var failed = 0;
        if (dashOnly)
            cases = GpuSvgDashCases.All;
        var comparisons = 0;
        foreach (var (name, content) in cases)
        {
            var path = Path.Combine(output, name + ".svg");
            File.WriteAllText(path, "<svg width='64' height='48'>" + content + "</svg>");
            var asset = store.Load(path);
            var scene = asset.GpuScene;
            foreach (var zoom in new[] { .75f, 1f, 2f })
            {
                var width = (int)(64 * zoom);
                var height = (int)(48 * zoom);
                var texture = new UiTextureId(2_400_000_001);
                var referenceTexture = new UiTextureId(2_400_000_002);
                var beforeRaster = store.Statistics.Rasterizations;
                var pixels = capture(name + "-" + zoom, ui =>
                {
                    context.QueueTextureUpdate(new(UiTextureUpdateKind.RenderVectorScene, texture, UiTextureFormat.Rgba8Unorm,
                        width, height, ReadOnlyMemory<byte>.Empty)
                    {
                        VectorScene = scene,
                        IsColorImage = true
                    });
                    ui.GetWindowDrawList().AddImage(texture, new(8, 8), new(8 + width, 8 + height), default, new(1, 1), new(255, 255, 255));
                });
                check(store.Statistics.Rasterizations == beforeRaster, "GPU feature creates no CPU pixels: " + name + "/" + zoom);
                // The scalar call is an explicit test oracle, outside the widget/GPU rendering path.
                var reference = asset.Rasterize(width, height);
                var expected = capture(name + "-reference-" + zoom, ui =>
                {
                    context.QueueTextureUpdate(new(UiTextureUpdateKind.Create, referenceTexture, UiTextureFormat.Rgba8Unorm,
                        width, height, reference.Pixels)
                    {
                        IsColorImage = true
                    });
                    ui.GetWindowDrawList().AddImage(referenceTexture, new(8, 8), new(8 + width, 8 + height), default, new(1, 1), new(255, 255, 255));
                });
                var differing = 0;
                var maximum = 0;
                for (var y = 0; y < height; y++)
                    for (var x = 0; x < width; x++)
                    {
                        var index = ((y + 8) * snapshotWidth + x + 8) * 4;
                        var difference = 0;
                        for (var channel = 0; channel < 4; channel++)
                            difference = Math.Max(difference, Math.Abs(pixels[index + channel] - expected[index + channel]));
                        if (difference > 1)
                            differing++;
                        maximum = Math.Max(maximum, difference);
                    }
                Console.WriteLine($"SVG FEATURE {name}/{zoom}: differences={differing}, maximum={maximum}");
                using (var stream = File.Create(Path.Combine(output, name + "-" + zoom + "-comparison.json")))
                using (var json = new Utf8JsonWriter(stream))
                {
                    json.WriteStartObject();
                    json.WriteNumber("differingPixels", differing);
                    json.WriteNumber("maximumDifference", maximum);
                    json.WriteEndObject();
                }
                if (differing != 0)
                    failed++;
                comparisons++;
                capture(name + "-destroy-" + zoom, _ =>
                {
                    context.QueueTextureUpdate(new(UiTextureUpdateKind.Destroy, texture, UiTextureFormat.Rgba8Unorm, 0, 0, ReadOnlyMemory<byte>.Empty));
                    context.QueueTextureUpdate(new(UiTextureUpdateKind.Destroy, referenceTexture, UiTextureFormat.Rgba8Unorm, 0, 0, ReadOnlyMemory<byte>.Empty));
                });
            }
        }
        using (var stream = File.Create(Path.Combine(output, "feature-summary.json")))
        using (var json = new Utf8JsonWriter(stream))
        {
            json.WriteStartObject();
            json.WriteNumber("comparisons", comparisons);
            json.WriteNumber("passed", comparisons - failed);
            json.WriteNumber("failed", failed);
            json.WriteNumber("tolerance", 1);
            json.WriteEndObject();
        }
        check(failed == 0, "Every GPU SVG feature matches the independent scalar pixels within one channel value; failed=" + failed);
    }
}
