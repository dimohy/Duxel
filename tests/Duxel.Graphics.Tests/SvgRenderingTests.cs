using Duxel.Core;
using Duxel.Platform.Windows;
using Duxel.Svg;

internal static class SvgRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check,
        int width, string output, int samples)
    {
        var path = Path.Combine(output, "gpu-svg-msaa" + samples + ".svg");
        const string source = "<svg xmlns='http://www.w3.org/2000/svg' width='64' height='48'><rect width='32' height='24' fill='#e03020'/></svg>";
        File.WriteAllText(path, source);
        using var store = new SvgStore();
        var document = store.Load(path);
        using var widget = new UiSvg(document);
        using var peer = new UiSvg(document);
        var decoder = new SvgImageDecoder(store, WindowsUiImageDecoder.Shared);
        var decoded = decoder.Decode(path);
        var bitmap = new UiImageTexture(new((nuint)(80_000_000 + samples)), decoded.Width, decoded.Height, decoded.RgbaPixels);
        var drawPeer = false;
        var bitmapMode = 0;
        var widgetBackground = false;
        var statistics = default(UiSvgTextureStatistics);
        void Draw(UiImmediateContext ui)
        {
            ui.SetCursorScreenPos(new(24, 24));
            if (widgetBackground)
                ui.GetWindowDrawList().AddRectFilled(new(24, 24, 64, 48), new(20, 50, 80), ui.WhiteTextureId);
            widget.Render(ui);
            bitmap.Prepare(ui, UiImageEffects.Default);
            ui.SetCursorScreenPos(new(120, 24));
            if (bitmapMode == 1) ui.ImageWithBg(bitmap.TextureId, new(64, 48), new(20, 50, 80));
            else if (bitmapMode == 2) ui.ImageButton("svg-bitmap-button", bitmap.TextureId, new(64, 48));
            else ui.Image(bitmap.TextureId, new(64, 48));
            if (drawPeer)
            {
                ui.SetCursorScreenPos(new(224, 24));
                peer.Render(ui);
            }
            statistics = ui.SvgTextureStatistics;
        }
        int Channel(byte[] pixels, int x, int y, int channel) => pixels[(y * width + x) * 4 + channel];
        bool SamePixel(byte[] a, int ax, int ay, byte[] b, int bx, int by) =>
            Enumerable.Range(0, 4).All(channel => Channel(a, ax, ay, channel) == Channel(b, bx, by, channel));
        var background = capture("empty", static _ => { });
        var initial = capture("original", Draw);
        check(Math.Abs(Channel(initial, 32, 32, 0) - 224) <= 1 && Math.Abs(Channel(initial, 32, 32, 1) - 48) <= 1 &&
            Math.Abs(Channel(initial, 32, 32, 2) - 32) <= 1,
            "SVG actual GPU readback preserves opaque authored RGB at MSAA " + samples);
        check(SamePixel(initial, 80, 64, background, 80, 64),
            "SVG actual GPU readback preserves transparent exterior at MSAA " + samples);
        var sameBitmap = true;
        for (var y = 0; y < 48; y++)
            for (var x = 0; x < 64; x++) sameBitmap &= SamePixel(initial, 24 + x, 24 + y, initial, 120 + x, 24 + y);
        check(sameBitmap, "SVG ordinary bitmap decoder and dedicated widget produce equal GPU pixels at MSAA " + samples);
        bitmapMode = 1;
        var withBackground = capture("image-with-background", Draw);
        check(SamePixel(withBackground, 128, 32, initial, 32, 32) &&
            Math.Abs(Channel(withBackground, 176, 64, 0) - 20) <= 1 &&
            Math.Abs(Channel(withBackground, 176, 64, 1) - 50) <= 1 &&
            Math.Abs(Channel(withBackground, 176, 64, 2) - 80) <= 1,
            "ImageWithBg maps the complete SVG bitmap and preserves its transparent background at MSAA " + samples);
        bitmapMode = 2;
        var button = capture("image-button", Draw);
        check(SamePixel(button, 128, 32, initial, 32, 32) && !SamePixel(button, 176, 64, initial, 32, 32),
            "ImageButton maps the complete SVG bitmap instead of one constant UV sample at MSAA " + samples);
        bitmapMode = 0;
        var uploads = statistics.GpuRenderRequests;
        for (var frame = 0; frame < 3; frame++)
        {
            var replay = capture("replay-" + frame, Draw);
            check(replay.AsSpan().SequenceEqual(initial) && statistics.GpuRenderRequests == uploads,
                "SVG acquired-image replay is pixel-identical without new texture upload at MSAA " + samples + " / " + frame);
        }
        widget.RotationDegrees = 90;
        var rotated = capture("rotated", Draw);
        check(SamePixel(rotated, 68, 32, initial, 32, 32) && SamePixel(rotated, 32, 32, background, 32, 32) &&
            SamePixel(rotated, 90, 32, background, 90, 32) && statistics.GpuRenderRequests == uploads,
            "SVG clockwise geometry rotation moves actual GPU pixels and preserves viewport clipping without upload at MSAA " + samples);
        widget.RotationDegrees = 0;
        widgetBackground = true;
        var colorImage = capture("svg-color-image-sampling", Draw);
        check(SamePixel(colorImage, 32, 32, initial, 32, 32) && Channel(colorImage, 80, 64, 0) == 20 &&
            Channel(colorImage, 80, 64, 1) == 50 && Channel(colorImage, 80, 64, 2) == 80,
            "SVG high runtime texture IDs use explicit color-image sampling over a colored background at MSAA " + samples);
        widgetBackground = false;
        widget.Offset = new(8, 4);
        var translated = capture("translated", Draw);
        check(SamePixel(translated, 40, 36, initial, 32, 32) && SamePixel(translated, 28, 28, background, 28, 28) &&
            statistics.GpuRenderRequests == uploads,
            "SVG geometry translation moves actual GPU pixels without upload at MSAA " + samples);
        widget.Offset = default;
        widget.Tint = new(0x80FFFFFF);
        var translucent = capture("translucent", Draw);
        var correctAlpha = true;
        for (var channel = 0; channel < 3; channel++)
        {
            var expected = (Channel(initial, 32, 32, channel) * 128 + Channel(background, 32, 32, channel) * 127) / 255d;
            correctAlpha &= Math.Abs(Channel(translucent, 32, 32, channel) - expected) <= 2;
        }
        check(correctAlpha && statistics.GpuRenderRequests == uploads,
            "SVG tint alpha blends actual GPU pixels while reusing straight-alpha raster at MSAA " + samples);
        widget.Tint = new(0xFFFFFFFF);
        widget.Zoom = 2;
        var zoomed = capture("zoomed", Draw);
        check(SamePixel(zoomed, 32, 32, initial, 32, 32) && statistics.GpuRenderRequests == uploads + 1 &&
            store.Statistics is { XmlParses : 1, SceneCompiles : 1, Rasterizations : 1 },
            "SVG zoom uses a new GPU resolution from the retained scene with one XML parse at MSAA " + samples);
        widget.Zoom = 1;
        var returned = capture("returned", Draw);
        check(returned.AsSpan().SequenceEqual(initial) && statistics.GpuRenderRequests == uploads + 1,
            "SVG returning zoom reuses the prior texture and exact actual GPU pixels at MSAA " + samples);
        File.WriteAllText(path, source.Replace("#e03020", "#20c060", StringComparison.Ordinal));
        store.Reload(path);
        var changed = capture("changed", Draw);
        check(Math.Abs(Channel(changed, 32, 32, 1) - 192) <= 1 && Math.Abs(Channel(changed, 32, 32, 0) - 32) <= 1 &&
            statistics.GpuRenderRequests == uploads + 2,
            "SVG source reload replaces actual GPU content without stale acquired-image pixels at MSAA " + samples);
        drawPeer = true;
        var shared = capture("shared", Draw);
        check(SamePixel(shared, 232, 32, changed, 32, 32) && statistics.GpuRenderRequests == uploads + 2,
            "two SVG widgets share uploaded content and draw matching actual GPU pixels at MSAA " + samples);
        File.WriteAllText(path, source.Replace("width='32' height='24'", "width='64' height='48'", StringComparison.Ordinal)
            .Replace("#e03020", "#20c060", StringComparison.Ordinal));
        store.Reload(path);
        widget.RotationDegrees = 90;
        UiVector2 following = default;
        void DrawFlow(UiImmediateContext ui)
        {
            ui.SetCursorScreenPos(new(24, 24));
            widget.Render(ui);
            following = ui.GetCursorScreenPos();
            ui.Image(ui.WhiteTextureId, new(8, 8), new UiColor(20, 40, 240));
        }
        var flowing = capture("rotated-layout-flow", DrawFlow);
        check(Math.Abs(Channel(flowing, 32, 80, 1) - 192) <= 1 && following.Y >= 88 &&
            Channel(flowing, (int)following.X + 4, (int)following.Y + 4, 2) == 240,
            "SVG actual GPU quarter-turn content extends beyond the former viewport and reserves space for the next widget at MSAA " + samples);
        widget.RotationDegrees = 45;
        var diagonal = capture("diagonal-layout-bounds", DrawFlow);
        check(new[] { (59, 29), (98, 68), (68, 98), (29, 59) }.All(point =>
            Math.Abs(Channel(diagonal, point.Item1, point.Item2, 1) - 192) <= 1),
            "SVG actual GPU arbitrary-angle corners remain visible inside their transformed layout bounds at MSAA " + samples);
    }
}
