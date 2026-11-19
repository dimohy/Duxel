using Duxel.Core;

internal static class ImageWidgetRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check,
        int width, int samples)
    {
        using var edge = Fluent.Image(new(new(2, 1, new byte[] { 255, 0, 0, 255, 0, 0, 255, 0 })));
        edge.Size = new(64, 32);
        var interpolated = capture("premultiplied-alpha-edge", ui =>
        {
            ui.GetWindowDrawList().AddRectFilled(new(24, 24, 64, 32), new(255, 255, 255), ui.WhiteTextureId);
            ui.SetCursorScreenPos(new(24, 24));
            edge.Render(ui);
        });
        var edgeCorrect = true;
        for (var x = 24; x < 88; x++)
        {
            var pixel = (40 * width + x) * 4;
            edgeCorrect &= interpolated[pixel] == 255 && interpolated[pixel + 1] == interpolated[pixel + 2];
        }
        check(edgeCorrect, "GPU image interpolation preserves red over white without transparent-blue fringes at MSAA " + samples);
        var pixels = new byte[64 * 48 * 4];
        for (var y = 0; y < 24; y++)
            for (var x = 0; x < 32; x++)
            {
                var offset = (y * 64 + x) * 4;
                pixels[offset] = 224;
                pixels[offset + 1] = 48;
                pixels[offset + 2] = 32;
                pixels[offset + 3] = 255;
            }
        using var image = Fluent.Image(new(new(64, 48, pixels)));
        var reference = new UiImageTexture(new((nuint)(90_000_000 + samples)), 64, 48, pixels);
        var statistics = default(UiRasterTextureStatistics);
        void Draw(UiImmediateContext ui)
        {
            ui.SetCursorScreenPos(new(24, 24));
            image.Render(ui);
            reference.Prepare(ui, UiImageEffects.Default);
            ui.SetCursorScreenPos(new(184, 24));
            ui.Image(reference.TextureId, new(64, 48));
            statistics = ui.ImageTextureStatistics;
        }
        int Channel(byte[] data, int x, int y, int channel) => data[(y * width + x) * 4 + channel];
        bool Same(byte[] a, int ax, int ay, byte[] b, int bx, int by) =>
            Enumerable.Range(0, 4).All(c => Channel(a, ax, ay, c) == Channel(b, bx, by, c));
        var empty = capture("empty", static _ => { });
        var initial = capture("initial", Draw);
        check(Math.Abs(Channel(initial, 32, 32, 0) - 224) <= 1 &&
            Math.Abs(Channel(initial, 32, 32, 1) - 48) <= 1 && Same(initial, 80, 64, empty, 80, 64),
            "Image widget GPU readback preserves opaque RGB and transparent pixels at MSAA " + samples);
        var equal = true;
        for (var y = 0; y < 48; y++)
            for (var x = 0; x < 64; x++)
                equal &= Same(initial, 24 + x, 24 + y, initial, 184 + x, 24 + y);
        check(equal, "Image widget matches existing immediate image GPU pixels at MSAA " + samples);
        var uploads = statistics.Uploads;
        for (var frame = 0; frame < 3; frame++)
        {
            var replay = capture("replay-" + frame, Draw);
            check(replay.AsSpan().SequenceEqual(initial) && statistics.Uploads == uploads,
                "Image widget acquired-image replay stays identical without upload at MSAA " + samples + " / " + frame);
        }
        image.Size = new(128, 96);
        var resized = capture("resized", Draw);
        check(Same(resized, 32, 60, initial, 32, 32) && Same(initial, 32, 60, empty, 32, 60) && statistics.Uploads == uploads,
            "Image GPU resizing changes geometry while retaining the native source upload at MSAA " + samples);
        image.Size = default;
        image.Tint = new(0x80FFFFFF);
        var alpha = capture("alpha", Draw);
        var correct = true;
        for (var c = 0; c < 3; c++)
        {
            var expected = (Channel(initial, 32, 32, c) * 128 + Channel(empty, 32, 32, c) * 127) / 255d;
            correct &= Math.Abs(Channel(alpha, 32, 32, c) - expected) <= 2;
        }
        check(correct && statistics.Uploads == uploads, "Image tint alpha blends on GPU without another upload at MSAA " + samples);
        image.Tint = new(0xFFFFFFFF);
        var blue = new byte[pixels.Length];
        for (var i = 0; i < blue.Length; i += 4)
        {
            blue[i + 2] = 230;
            blue[i + 3] = 255;
        }
        image.Source = new(new(64, 48, blue));
        var replaced = capture("source-replaced", Draw);
        check(Channel(replaced, 32, 32, 2) == 230 && statistics.Uploads == uploads + 1,
            "Image source replacement changes actual GPU pixels with one upload at MSAA " + samples);
        for (var frame = 0; frame < 3; frame++)
        {
            var replay = capture("replacement-replay-" + frame, Draw);
            check(replay.AsSpan().SequenceEqual(replaced) && statistics.Uploads == uploads + 1,
                "Image replacement never replays stale acquired-image pixels at MSAA " + samples + " / " + frame);
        }
    }
}
