using Duxel.Core;
using Duxel.Platform.Windows;
using Duxel.Svg;

internal static class ImageWidgetTests
{
    internal static void Run(string output, Action<bool, string> check)
    {
        static UiContext Context() => new(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        using var context = Context();
        var requests = 0;
        context.SetRequestFrameCallback(() => requests++);
        var pixels = new byte[] { 220, 10, 20, 255, 20, 180, 30, 255 };
        var source = new UiImageSource(new(2, 1, pixels));
        pixels[0] = 0;
        using var image = Fluent.Image(source);
        UiDrawData Frame(UiContext target, Action<UiImmediateContext> action)
        {
            target.NewFrame(new(1f / 60, new(320, 200), new(1, 1)),
                new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default),
                new(0, 0, 320, 200), UiTextSettings.Default);
            target.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding : 0); action(ui); }));
            return target.GetDrawData();
        }
        using (var measured = Frame(context, ui => check(image.Measure(ui, new(100, 100)) == new UiVector2(2, 1),
            "image intrinsic measurement uses source dimensions")))
            check(measured.TextureUpdates.Count == 0, "image measurement never uploads a texture");
        using var first = Frame(context, image.Render);
        var upload = first.TextureUpdates.Single();
        check(upload.Kind == UiTextureUpdateKind.Create && upload.Width == 2 && upload.Height == 1 &&
            upload.RgbaPixels.Span[0] == 220 && upload.IsColorImage,
            "image source snapshots caller pixels and uploads native color dimensions once");
        var firstId = upload.TextureId;
        using (var clippedContext = Context())
        using (var clipped = Frame(clippedContext, ui => { ui.SetCursorScreenPos(new(-1000, -1000)); image.Render(ui); }))
            check(clipped.TextureUpdates.Count == 0, "fully clipped images do not upload pixels");
        var stable = requests;
        using (var repeated = Frame(context, image.Render))
            check(repeated.TextureUpdates.Count == 0 && requests == stable, "unchanged image reuses its upload without requesting frames");
        image.Size = new(80, 40);
        image.Tint = new(100, 120, 140);
        check(requests > stable, "image viewport and tint changes wake event-wait rendering");
        using (var resized = Frame(context, image.Render))
        {
            check(resized.TextureUpdates.Count == 0, "viewport resizing and tint never rerasterize or reupload bitmap pixels");
            var vertices = resized.DrawLists.SelectMany(list => list.Vertices).ToArray();
            check(vertices.Max(v => v.Position.X) - vertices.Min(v => v.Position.X) == 80 &&
                vertices.Max(v => v.Position.Y) - vertices.Min(v => v.Position.Y) == 40 && vertices.All(v => v.Color == image.Tint),
                "image viewport and tint are applied to the current quad");
        }
        using (var sibling = Fluent.Image(source))
        using (var shared = Frame(context, sibling.Render))
            check(shared.TextureUpdates.Count == 0, "widgets sharing the same source share a context texture");
        using (var otherContext = Context())
        using (var other = Frame(otherContext, image.Render))
            check(other.TextureUpdates.Count == 1, "each context receives its own upload for the same image widget");
        using (var back = Frame(context, image.Render))
            check(back.TextureUpdates.Count == 0, "context teardown does not invalidate another context's live image lease");
        var replacement = new UiImageSource(new(1, 1, [10, 20, 230, 255]));
        image.Source = replacement;
        using (var replaced = Frame(context, image.Render))
            check(replaced.TextureUpdates.Single().RgbaPixels.Span[2] == 230 &&
                replaced.TextureUpdates.Single().TextureId != firstId && upload.RgbaPixels.Span[0] == 220,
                "source replacement uses a new texture and preserves previously queued pixel memory");
        stable = requests;
        image.Source = image.Source;
        image.Size = image.Size;
        image.Tint = image.Tint;
        check(requests == stable, "unchanged image properties do not request frames");
        using (var allocated = Dux.SizedBox(Fluent.Image(source), 64, 48))
        using (var drawn = Frame(context, allocated.Render))
        {
            var vertices = drawn.DrawLists.SelectMany(list => list.Vertices).ToArray();
            check(vertices.Max(v => v.Position.X) - vertices.Min(v => v.Position.X) == 64 &&
                vertices.Max(v => v.Position.Y) - vertices.Min(v => v.Position.Y) == 48,
                "image uses the actual parent allocation");
        }
        static bool Throws<T>(Action action) where T : Exception
        {
            try { action(); return false; }
            catch (T) { return true; }
        }
        check(Throws<ArgumentException>(() => _ = new UiImageSource(new(2, 2, [1]))) &&
            Throws<ArgumentOutOfRangeException>(() => image.Size = new(float.NaN, 10)), "image validates pixels and finite positive viewports");
        check(Throws<ArgumentOutOfRangeException>(() => _ = new UiImageSource(new(4097, 4096, []))),
            "oversized image sources fail before copying pixels");
        var previousDecoder = UiImageTexture.ImageDecoder;
        using var store = new SvgStore();
        var decoder = new CountingDecoder(new SvgImageDecoder(store, WindowsUiImageDecoder.Shared));
        try
        {
            UiImageTexture.ImageDecoder = decoder;
            var svgPath = Path.Combine(output, "image.svg");
            File.WriteAllText(svgPath, "<svg xmlns='http://www.w3.org/2000/svg' width='2' height='1'><rect width='2' height='1' fill='#dc0a14'/></svg>");
            var svgSource = UiImageSource.LoadFromFile(svgPath);
            using var svgImage = Fluent.Image(svgSource);
            using (var svgFirst = Frame(context, svgImage.Render))
                check(svgFirst.TextureUpdates.Single().RgbaPixels.Span[0] == 220, "SVG file input decodes into an ordinary image source");
            using (var svgAgain = Frame(context, svgImage.Render)) { }
            check(decoder.Decodes == 1 && store.Statistics.XmlParses == 1,
                "image rendering does not repeat SVG decoding or XML parsing");
            var pngPath = Path.Combine(output, "image.png");
            File.WriteAllBytes(pngPath, Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a0WQAAAAASUVORK5CYII="));
            var pngSource = UiImageSource.LoadFromFile(pngPath);
            check(pngSource.Width == 1 && pngSource.Height == 1 && decoder.Decodes == 2,
                "PNG input uses the same explicit registered decoder contract");
        }
        finally { UiImageTexture.ImageDecoder = previousDecoder; }
        using (var survivorContext = Context())
        using (var survivingImage = Frame(survivorContext, image.Render))
            check(survivingImage.TextureUpdates.Count == 1, "a surviving image can upload again after previous context teardown");
        image.Dispose();
        check(Throws<ObjectDisposedException>(() => _ = image.Source), "disposed image releases its source reference and rejects access");
        using var limitedContext = Context();
        var pinned = Enumerable.Range(0, 128).Select(_ => Fluent.Image(new(new(1, 1, [10, 20, 30, 255])))).ToArray();
        using var overflow = Fluent.Image(new(new(1, 1, [40, 50, 60, 255])));
        try
        {
            using (var all = Frame(limitedContext, ui =>
            {
                foreach (var item in pinned) { ui.SetCursorScreenPos(new(10, 10)); item.Render(ui); }
            })) { }
            check(Throws<InvalidOperationException>(() =>
            {
                using var rejected = Frame(limitedContext, ui => { ui.SetCursorScreenPos(new(10, 10)); overflow.Render(ui); });
            }), "image cache rejects a 129th pinned texture instead of evicting a live dependency");
            pinned[0].Dispose();
            using var recovered = Frame(limitedContext, ui => { ui.SetCursorScreenPos(new(10, 10)); overflow.Render(ui); });
            check(recovered.TextureUpdates.Count(update => update.Kind == UiTextureUpdateKind.Destroy) == 1 &&
                recovered.TextureUpdates.Count(update => update.Kind == UiTextureUpdateKind.Create) == 1,
                "releasing one image lease permits bounded retirement and a later successful upload");
        }
        finally { foreach (var item in pinned) item.Dispose(); }
    }

    private sealed class CountingDecoder(IUiImageDecoder inner) : IUiImageDecoder
    {
        internal int Decodes;
        public bool CanDecode(string path) => inner.CanDecode(path);
        public UiImageData Decode(string path) { Decodes++; return inner.Decode(path); }
    }
    private sealed class Screen(Action<UiImmediateContext> action) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => action(ui);
    }
}
