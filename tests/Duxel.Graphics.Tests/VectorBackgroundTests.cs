using System.Numerics;
using Duxel.Core;
using Duxel.Svg;

internal static class VectorBackgroundTests
{
    internal static void Run(string output, Action<bool, string> check)
    {
        VectorSceneShaderContractTests.Run(check);
        using var fixture = new PresentationReuseTests.Fixture(1, true);
        var texture = new UiTextureId(2_300_000_090);
        byte[] Read(UiGpuVectorScene scene, UiColor background, int width = 8, int height = 8, UiGpuVectorCamera? camera = null)
        {
            using var drawing = fixture.Build(ui =>
            {
                fixture.Context.QueueTextureUpdate(new(UiTextureUpdateKind.RenderVectorScene, texture,
                    UiTextureFormat.Rgba8Unorm, width, height, ReadOnlyMemory<byte>.Empty)
                {
                    VectorScene = scene, VectorBackground = background, VectorCamera = camera, IsColorImage = true
                });
                ui.GetWindowDrawList().AddImage(texture, default, new(width, height), default, new(1, 1), new(255, 255, 255));
            });
            return fixture.Renderer.RenderVectorAndReadback(drawing, texture, width, height);
        }
        var scene = GpuVectorTestScenes.Rectangles(new(8, 8), (new(2, 2, 4, 4), new Vector4(1, 0, 0, .25f)));
        UiColor[] backgrounds = [default, new(255, 255, 255), new(16, 32, 48, 128), new(80, 40, 20, 0)];
        foreach (var background in backgrounds)
        {
            var pixels = Read(scene, background);
            var packed = background.Rgba;
            var alpha = (int)(packed >> 24);
            int[] clear = [(int)((packed & 255) * alpha + 127) / 255,
                (int)(((packed >> 8) & 255) * alpha + 127) / 255,
                (int)(((packed >> 16) & 255) * alpha + 127) / 255, alpha];
            var exact = true;
            for (var y = 0; y < 8; y++)
                for (var x = 0; x < 8; x++)
                    for (var channel = 0; channel < 4; channel++)
                    {
                        var expected = clear[channel];
                        if (x is >= 2 and < 6 && y is >= 2 and < 6)
                            expected = (int)Math.Round(((channel is 0 or 3 ? 255 : 0) + 3 * clear[channel]) / 4d);
                        exact &= pixels[(y * 8 + x) * 4 + channel] == expected;
                    }
            check(exact, $"Vector target background / independent integer clear and quarter-alpha source-over match every pixel for {packed:X8}");
            var work = fixture.Renderer.VectorSceneDiagnostics;
            check(Read(scene, background).AsSpan().SequenceEqual(pixels) && fixture.Renderer.VectorSceneDiagnostics == work,
                "Vector target background / identical scene, camera and background reuse output without redispatch");
        }
        var white = new UiColor(255, 255, 255);
        var initial = Read(scene, white);
        var before = fixture.Renderer.VectorSceneDiagnostics;
        var transparent = Read(scene, default);
        var changed = fixture.Renderer.VectorSceneDiagnostics;
        check(!initial.AsSpan().SequenceEqual(transparent) && changed.Dispatches == before.Dispatches + 1 && changed.Uploads == before.Uploads,
            "Vector target background / changing only background dispatches once and retains the uploaded scene");
        var letterbox = Read(GpuVectorTestScenes.Solid(new(8, 4)), white);
        check(letterbox.AsSpan(0, 8 * 2 * 4).ToArray().All(value => value == 255)
            && letterbox.AsSpan(8 * 6 * 4).ToArray().All(value => value == 255),
            "Vector target background / intrinsic viewport letterbox keeps the target clear color");
        var outside = Read(scene, white, camera: new(new(8, 8), new(256, 0)));
        check(outside.All(value => value == 255), "Vector target background / a camera tile outside the SVG retains its background");
        var path = Path.Combine(output, "pattern.svg");
        File.WriteAllText(path, "<svg xmlns='http://www.w3.org/2000/svg' width='8' height='8'><defs><pattern id='p' width='4' height='4' patternUnits='userSpaceOnUse'><rect width='2' height='4' fill='red'/></pattern></defs><rect width='8' height='8' fill='url(#p)'/></svg>");
        using var store = new SvgStore();
        var pattern = Read(store.Load(path).GpuScene, white);
        var holes = true;
        for (var y = 0; y < 8; y++)
            for (var x = 0; x < 8; x++)
            {
                var at = (y * 8 + x) * 4;
                var expected = x % 4 < 2 ? new byte[] { 255, 0, 0, 255 } : new byte[] { 255, 255, 255, 255 };
                holes &= pattern.AsSpan(at, 4).SequenceEqual(expected);
            }
        check(holes, "Vector target background / pattern dependencies stay transparent and their holes show the root background");
        check(store.Statistics.Rasterizations == 0 && store.Statistics.XmlParses == 1,
            "Vector target background / SVG dependencies use GPU pixels and parse their source once");
        UiGpuVectorScene Svg(string name, string body)
        {
            var source = Path.Combine(output, name + ".svg");
            File.WriteAllText(source, "<svg xmlns='http://www.w3.org/2000/svg' width='8' height='8'>" + body + "</svg>");
            return store.Load(source).GpuScene;
        }
        var group = Read(Svg("group", "<g opacity='.5'><rect width='8' height='8' fill='red'/></g>"), white);
        var groupExact = true;
        for (var pixel = 0; pixel < group.Length; pixel += 4)
            groupExact &= group.AsSpan(pixel, 4).SequenceEqual(new byte[] { 255, 128, 128, 255 });
        check(groupExact, "Vector target background / isolated group opacity excludes the target background");
        var mask = Read(Svg("mask", "<defs><mask id='m' maskUnits='userSpaceOnUse' x='0' y='0' width='8' height='8'><rect width='8' height='8' fill='black'/></mask></defs><rect width='8' height='8' fill='red' mask='url(#m)'/>"), white);
        check(mask.All(value => value == 255), "Vector target background / a fully masked source leaves the target background intact");
        var filtered = Read(Svg("filter", "<defs><filter id='f' filterUnits='userSpaceOnUse' x='0' y='0' width='8' height='8'><feGaussianBlur stdDeviation='.5'/></filter></defs><rect x='3' y='3' width='2' height='2' fill='blue' filter='url(#f)'/>"), new(0, 255, 0));
        check(filtered.AsSpan(0, 4).SequenceEqual(new byte[] { 0, 255, 0, 255 }) && filtered[(3 * 8 + 3) * 4] == 0
            && filtered[(3 * 8 + 3) * 4 + 2] > 0,
            "Vector target background / Gaussian dependencies remain transparent over a nonwhite root target");
        fixture.Capture(_ => fixture.Context.QueueTextureUpdate(new(UiTextureUpdateKind.Destroy, texture,
            UiTextureFormat.Rgba8Unorm, 0, 0, ReadOnlyMemory<byte>.Empty)));
        check(fixture.Renderer.VectorSceneDiagnostics is { Scenes: 0, Outputs: 0 },
            "Vector target background / output replacement and destruction release the complete dependency tree");
        using var widget = new UiSvg(store.Load(path)) { Size = new(8, 8), Background = white };
        UiSvgTextureStatistics statistics = default;
        byte[] CaptureWidget() => fixture.Capture(ui =>
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            widget.Render(ui);
            statistics = ui.SvgTextureStatistics;
        });
        var widgetWhite = CaptureWidget();
        var warm = statistics;
        check(CaptureWidget().AsSpan().SequenceEqual(widgetWhite) && statistics == warm,
            "Vector target background / actual SVG widget reuses its background-specific texture");
        widget.Background = default;
        var widgetTransparent = CaptureWidget();
        check(!widgetTransparent.AsSpan().SequenceEqual(widgetWhite) && statistics.GpuRenderRequests == warm.GpuRenderRequests + 1,
            "Vector target background / actual SVG widget changes output when only Background changes");
        var afterChange = statistics;
        widget.Background = white;
        check(CaptureWidget().AsSpan().SequenceEqual(widgetWhite) && statistics == afterChange,
            "Vector target background / returning to a cached widget background requires no new GPU work");
        check(store.Statistics.XmlParses == 4 && store.Statistics.Rasterizations == 0,
            "Vector target background / widget background changes neither reparse SVG nor create CPU raster pixels");
    }
}
