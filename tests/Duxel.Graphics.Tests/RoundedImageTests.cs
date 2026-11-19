using System.Runtime.CompilerServices;
using Duxel.Core;

internal static class RoundedImageTests
{
    internal static void Run(Action<bool, string> check)
    {
        var clip = new UiRect(0, 0, 160, 120);
        var texture = new UiTextureId(9);
        var builder = new UiDrawListBuilder(clip);
        builder.AddRectFilled(new(1, 1, 4, 4), new(255, 0, 0), new(2));
        var uv = new UiImageUvTransform(new(.25f, .1f), new(.5f, -.2f), new(.1f, .7f));
        builder.AddImageRounded(texture, new(20, 20, 48, 32), uv, new(200, 180, 160, 128), 100, clip);
        var lists = builder.Build();
        try
        {
            var command = lists.SelectMany(list => list.Commands).Single(c => c.IsImagePrimitive);
            var records = lists.Single(list => list.Commands.Contains(command)).RectFilledPrimitives!;
            check(Unsafe.SizeOf<UiRectFilledPrimitive>() == 32 && command.IndexOffset == 1 && command.ElementCount == 2
                && command.PrimitiveInstanceCount == 1 && command.PrimitiveVertexCount == 6 && command.PrimitiveDrawMode == 3,
                "rounded image uses two existing32B records and one six-vertex instance at a nonzero mixed-stream offset");
            check(records[1].CornerRadius == 16 && records[2].Rect == new UiRect(.25f, .1f, .5f, -.2f)
                && records[2].CornerRadius == .1f && records[2].BorderThickness == .7f,
                "rounded image packs full affine UV axes separately from its clamped geometric radius");
            check(command.HasBounds && command.Bounds == new UiRect(20, 20, 48, 32) && command.TextureId == texture,
                "rounded image culling bounds exclude its UV payload and preserve texture identity");
            builder._ResetForNewFrame();
            check(records[1].CornerRadius == 16 && records[2].Rect.X == .25f,
                "later builder resets cannot mutate a queued rounded image record pair");
        }
        finally { foreach (var list in lists) list.ReleasePooled(); }
        var cover = UiImageUvTransform.ForFit(new(2, 1), new(32, 32), UiImageFit.Cover);
        var contain = UiImageUvTransform.ForFit(new(2, 1), new(32, 32), UiImageFit.Contain);
        check(cover == new UiImageUvTransform(new(.25f, 0), new(.5f, 0), new(0, 1)),
            "Cover computes centered source crop without changing source pixels");
        check(contain == new UiImageUvTransform(new(0, -.5f), new(1, 0), new(0, 2)),
            "Contain computes transparent letterbox UVs without a derived bitmap");
        var oddRejected = false;
        try { _ = new UiDrawCommand(clip, texture, 0, 1, (uint)UiRectGeometryVariant.Image,
            Kind : UiDrawCommandKind.RectFilledPrimitives).PrimitiveInstanceCount; }
        catch (InvalidOperationException) { oddRejected = true; }
        check(oddRejected, "incomplete rounded image record pairs fail before GPU consumption");
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        var requests = 0;
        context.SetRequestFrameCallback(() => requests++);
        var source = new UiImageSource(new(2, 1, [255, 0, 0, 255, 0, 255, 0, 255]));
        using var image = Fluent.Image(source);
        image.Size = new(32, 32);
        var imageStatistics = default(UiRasterTextureStatistics);
        UiDrawData Frame(Action<UiImmediateContext> draw)
        {
            context.NewFrame(new(1f / 60, new(160, 120), new(1, 1)),
                new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default), clip, UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding : 0); draw(ui); imageStatistics = ui.ImageTextureStatistics; }));
            return context.GetDrawData();
        }
        using var original = Frame(image.Render);
        check(original.DrawLists.SelectMany(list => list.Commands).All(c => !c.IsImagePrimitive),
            "default zero-radius Stretch image preserves the existing triangle path");
        image.CornerRadius = 16;
        using var rounded = Frame(image.Render);
        var uploadCount = imageStatistics.Uploads;
        check(rounded.DrawLists.SelectMany(list => list.Commands).Count(c => c.IsImagePrimitive) == 1 && rounded.TextureUpdates.Count == 0,
            "changing bitmap corner radius changes only geometry and does not upload a texture");
        image.Fit = UiImageFit.Contain;
        using var contained = Frame(image.Render);
        check(contained.TextureUpdates.Count == 0 && imageStatistics.Uploads == uploadCount,
            "changing bitmap object fit reuses the same native-resolution cached image");
        var stable = requests;
        using var idle = Frame(image.Render);
        check(requests == stable && idle.TextureUpdates.Count == 0,
            "unchanged rounded image requests neither an animation frame nor a resource upload");
        var document = new CountingDocument();
        using var svg = new UiSvg(document) { Size = new(32, 32), CornerRadius = 16 };
        using var svgFirst = Frame(svg.Render);
        svg.RotationDegrees = 90; svg.Offset = new(2, -3); svg.CornerRadius = 12;
        using var svgMoved = Frame(svg.Render);
        check(document.Rasters == 0 && svgFirst.TextureUpdates.Single().Kind == UiTextureUpdateKind.RenderVectorScene && svgMoved.TextureUpdates.Count == 0,
            "SVG radius rotation and translation reuse the GPU document without CPU rasterization");
        check(svgMoved.DrawLists.SelectMany(list => list.Commands).Count(c => c.IsImagePrimitive) == 1,
            "SVG transformed UVs share the same rounded image primitive path");
        check(original.DrawLists.SelectMany(list => list.Commands).All(c => !c.IsImagePrimitive),
            "later masked frames preserve the original queued image geometry");
        RasterLayerLifetimeTests.RunCpu(check, rounded : true);
    }

    private sealed class CountingDocument : IUiGpuSvgDocument
    {
        public UiGpuVectorScene GpuScene { get; } = GpuVectorTestScenes.Solid(new(32, 32));
        public UiVector2 IntrinsicSize => new(32, 32);
        public long Revision => 0;
        public event Action? Changed { add { } remove { } }
        internal int Rasters;
        public UiSvgRaster Rasterize(int width, int height)
        {
            Rasters++;
            var pixels = new byte[checked(width * height * 4)];
            for (var i = 0; i < pixels.Length; i += 4) { pixels[i] = 255; pixels[i + 3] = 255; }
            return new(width, height, pixels);
        }
    }
    private sealed class Screen(Action<UiImmediateContext> draw) : UiScreen
    { public override void Render(UiImmediateContext ui) => draw(ui); }
}
