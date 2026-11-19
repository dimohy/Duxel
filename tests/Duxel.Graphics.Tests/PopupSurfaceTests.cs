using Duxel.Core;

internal static class PopupSurfaceTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        using var surface = new UiPopupSurface();
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        UiRasterTextureStatistics statistics = default;
        UiTextureUpdate[] Frame(Action<UiControlContext> draw)
        {
            context.NewFrame(new(1f / 60, new(640, 480), new(1, 1)), idle, new(0, 0, 640, 480), UiTextSettings.Default);
            context.Render(new Screen(ui => { draw(new(ui)); statistics = ui.PopupSurfaceTextureStatistics; }));
            using var data = context.GetDrawData();
            return data.TextureUpdates.ToArray();
        }
        var fill = new UiColor(240, 120, 30);
        var target = new UiRect(240, 200, 40, 32);
        UiPopupLayout Arrange(UiPopupPosition position, float arrowHeight) => UiPopupPlacement.Arrange(target, new(100, 60), new(0, 0, 640, 480),
            new()
            {
                Position = position,
                Pinned = true,
                ArrowHeight = arrowHeight,
                ArrowWidth = arrowHeight * 1.414f,
                ArrowPadding = 4
            });
        UiTextureUpdate Created(UiTextureUpdate[] updates) => updates.Single(e => e.Kind == UiTextureUpdateKind.Create && e.Width > 100);
        var layout = Arrange(UiPopupPosition.Above, 6);
        var first = Created(Frame(paint => paint.FluentPopupSurface(surface, layout, fill)));
        check(first.IsColorImage && statistics.Uploads == 1, "Popup surface creates an ordinary bounded color raster once");
        Frame(paint => paint.FluentPopupSurface(surface, layout, fill));
        check(statistics.Uploads == 1, "Popup surface unchanged raster is reused");
        UiPopupLayout Move(UiPopupLayout source, float x)
        {
            var arrow = source.Arrow!.Value;
            return source with
            {
                Bounds = source.Bounds with
                {
                    X = source.Bounds.X + x
                },
                Arrow = new UiPopupArrow(
                arrow.Tip with
                {
                    X = arrow.Tip.X + x
                }, arrow.BaseStart with
                {
                    X = arrow.BaseStart.X + x
                }, arrow.BaseEnd with
                {
                    X = arrow.BaseEnd.X + x
                })
            };
        }
        Frame(paint => paint.FluentPopupSurface(surface, Move(layout, 13), fill));
        check(statistics.Uploads == 1, "Popup surface integral body and arrow movement reuses raster");
        Frame(paint => paint.FluentPopupSurface(surface, Move(layout, .5f), fill));
        check(statistics.Uploads == 2, "Popup surface fractional pixel phase replaces raster once");
        foreach (var height in new[] { 6f, 8f })
        {
            foreach (var position in new[] { UiPopupPosition.Above, UiPopupPosition.Below, UiPopupPosition.Before, UiPopupPosition.After })
            {
                layout = Arrange(position, height);
                var upload = Created(Frame(paint => paint.FluentPopupSurface(surface, layout, fill)));
                var margin = (int)(72 + height);
                var arrow = layout.Arrow!.Value;
                var center = new UiVector2((arrow.BaseStart.X + arrow.BaseEnd.X) / 2, (arrow.BaseStart.Y + arrow.BaseEnd.Y) / 2);
                var sample = new UiVector2(center.X + (arrow.Tip.X - center.X) * .4f, center.Y + (arrow.Tip.Y - center.Y) * .4f);
                var offset = ((int)(sample.Y - layout.Bounds.Y + margin) * upload.Width + (int)(sample.X - layout.Bounds.X + margin)) * 4;
                var pixels = upload.RgbaPixels.Span;
                check(pixels[offset] >= 220 && pixels[offset + 3] == 255, "Popup combined body/arrow has opaque colored interior : " + position + "/" + height);
                var antialiased = false;
                for (var y = (int)(center.Y - height); y <= center.Y + height; y++)
                {
                    for (var x = (int)(center.X - height); x <= center.X + height; x++)
                    {
                        if (x >= layout.Bounds.X && x < layout.Bounds.X + layout.Bounds.Width && y >= layout.Bounds.Y && y < layout.Bounds.Y + layout.Bounds.Height)
                            continue;
                        var index = ((int)(y - layout.Bounds.Y + margin) * upload.Width + (int)(x - layout.Bounds.X + margin)) * 4;
                        antialiased |= pixels[index] > 0 && pixels[index + 3] is > 0 and < 255;
                    }
                }
                check(antialiased, "Popup arrow edge has fractional colored coverage : " + position + "/" + height);
            }
        }
        var scaleUploads = statistics.Uploads;
        context.SetContentScale(1.5f);
        Frame(paint => paint.FluentPopupSurface(surface, layout, fill));
        Frame(paint => paint.FluentPopupSurface(surface, layout, fill));
        check(statistics.Uploads == scaleUploads + 1, "Popup scale change creates once and subsequent frames reuse physical raster");
        context.SetContentScale(1);
        using var second = new UiPopupSurface();
        var multiple = Frame(paint =>
        {
            paint.FluentPopupSurface(surface, Arrange(UiPopupPosition.Above, 6), fill);
            paint.FluentPopupSurface(second, Arrange(UiPopupPosition.After, 8), new(20, 80, 160));
        });
        check(multiple.Count(e => e.Kind == UiTextureUpdateKind.Create && e.Width > 100) == 2, "Independent popup owners may paint different shapes in the same frame");
        var before = statistics.Uploads;
        var refused = false;
        Frame(paint =>
        {
            try
            {
                paint.FluentPopupSurface(second, new(new(0, 0, 5000, 1), UiPopupPosition.Above), fill);
            }
            catch (NotSupportedException) { refused = true; }
        });
        check(refused && statistics.Uploads == before, "Popup oversized raster fails before allocation/upload");
        second.Dispose();
        var disposed = false;
        Frame(paint =>
        {
            try
            {
                paint.FluentPopupSurface(second, layout, fill);
            }
            catch (ObjectDisposedException) { disposed = true; }
        });
        check(disposed, "Popup disposed surface rejects painting");
    }
    private sealed class Screen(Action<UiImmediateContext> draw) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => draw(ui);
    }
}
