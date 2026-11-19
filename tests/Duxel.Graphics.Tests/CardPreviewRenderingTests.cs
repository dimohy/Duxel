using Duxel.Core;

internal static class CardPreviewRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check, int stride)
    {
        using var card = Fluent.Card("Preview pixels", Dux.CardContent([
            Fluent.CardPreview(new Fill(new(80, 60), new(255, 0, 0)), new Fill(new(40, 40), new(0, 255, 0))),
            new Fill(new(40, 20), new(0, 0, 255))]), new(Appearance: UiCardAppearance.Outline, Width: 160));
        void Draw(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            var paint = new UiControlContext(ui);
            paint.Frame(paint.ClipRect, new(30, 70, 90), default, 0, 0);
            ui.SetCursorScreenPos(new(16, 16));
            card.Render(ui);
        }
        bool Pixel(byte[] bytes, int x, int y, byte r, byte g, byte b)
            => bytes.AsSpan((y * stride + x) * 4, 3).SequenceEqual(new byte[] { r, g, b });
        var first = capture("preview-slots", Draw);
        check(Pixel(first, 20, 40, 255, 0, 0) && Pixel(first, 170, 40, 255, 0, 0),
            "GPU first Card Preview fills both parent margins");
        check(Pixel(first, 30, 35, 0, 255, 0) && Pixel(first, 58, 62, 0, 255, 0) && Pixel(first, 61, 40, 255, 0, 0),
            "GPU Card Preview logo paints above content at fixed32px and left/bottom12px");
        check(Pixel(first, 30, 90, 0, 0, 255) && Pixel(first, 20, 90, 30, 70, 90),
            "GPU ordinary CardContent keeps its source padding after full-bleed Preview");
        check(first.AsSpan().SequenceEqual(capture("preview-slots-replay", Draw)), "GPU Card Preview unchanged replay is pixel exact");
        check(!Pixel(first, 16, 40, 255, 0, 0) && Pixel(first, 16, 16, 30, 70, 90),
            "GPU Card border remains above Preview and rounded corners clip actual child paint");
        card.Options = card.Options with
        {
            Width = 200,
            Size = UiCardSize.Large
        };
        var wider = capture("preview-slots-wider", Draw);
        check(Pixel(wider, 210, 40, 255, 0, 0) && Pixel(wider, 30, 35, 0, 255, 0),
            "GPU Card width and size invalidation preserves full-bleed content and logo offsets");
        check(wider.AsSpan().SequenceEqual(capture("preview-slots-wider-replay", Draw)), "GPU resized Preview retains exact pixels");
        RunMinimumAllocation(capture, check, stride);
    }

    private static void RunMinimumAllocation(Func<string, Action<UiImmediateContext>, byte[]> capture,
        Action<bool, string> check, int stride)
    {
        foreach (var orientation in Enum.GetValues<UiCardOrientation>())
        {
            var media = new Fill(new(80, 60), new(255, 0, 0));
            var heading = new Fill(new(40, 20), new(0, 0, 255));
            using var card = Fluent.Card("Minimum Preview pixels", Dux.CardContent([
                Fluent.CardPreview(media), Fluent.CardHeader(heading)]),
                new(Appearance: UiCardAppearance.Outline, Orientation: orientation));
            using var root = Dux.ConstrainedBox(card, new(176, 260, 160, 200));
            void Draw(UiImmediateContext ui)
            {
                ui.EnableRootViewportContentLayout(contentPadding: 0);
                var paint = new UiControlContext(ui);
                paint.Frame(paint.ClipRect, new(30, 70, 90), default, 0, 0);
                ui.SetCursorScreenPos(new(16, 16));
                root.Render(ui);
            }
            bool Pixel(byte[] pixels, int x, int y, byte r, byte g, byte b)
                => pixels.AsSpan((y * stride + x) * 4, 3).SequenceEqual(new byte[] { r, g, b });
            var first = capture("preview-minimum-" + orientation, Draw);
            check(media.Bounds == (orientation == UiCardOrientation.Vertical ? new UiRect(16, 16, 176, 60) : new(16, 66, 80, 60)),
                "GPU minimum-only Card allocates full-bleed media with actual cross-axis centering : " + orientation);
            check(heading.Bounds == (orientation == UiCardOrientation.Vertical ? new UiRect(28, 88, 152, 20) : new(108, 86, 72, 20)),
                "GPU minimum-only Card allocates the stretched or growing Header : " + orientation);
            check(Pixel(first, orientation == UiCardOrientation.Vertical ? 185 : 20, 70, 255, 0, 0)
                && Pixel(first, 175, 95, 0, 0, 255) && Pixel(first, 205, 95, 30, 70, 90),
                "GPU minimum Card paints the expected media/Header and does not fill its loose maximum : " + orientation);
            check(first.AsSpan().SequenceEqual(capture("preview-minimum-replay-" + orientation, Draw)),
                "GPU minimum Card exact replay retains all body pixels : " + orientation);
            root.Options = root.Options with
            {
                MinWidth = 208
            };
            var changed = capture("preview-minimum-wider-" + orientation, Draw);
            check(Pixel(changed, 207, 95, 0, 0, 255) && heading.Bounds.Width == (orientation == UiCardOrientation.Vertical ? 184 : 104),
                "GPU minimum-width change repairs newly occupied Header pixels : " + orientation);
            check(changed.AsSpan().SequenceEqual(capture("preview-minimum-wider-replay-" + orientation, Draw)),
                "GPU changed minimum Card replays exactly : " + orientation);
        }
    }
    private sealed class Fill(UiVector2 size, UiColor color) : UiControlWidget
    {
        internal UiRect Bounds;
        protected override bool CanRetainMeasurement => true;
        protected override UiVector2 MeasureContent(UiControlContext context) => context.LayoutConstraints.Constrain(size);
        protected override void RenderControl(UiImmediateContext ui)
        {
            var paint = new UiControlContext(ui);
            var actual = paint.ConstrainToLayout(size);
            var point = ui.GetCursorScreenPos();
            Bounds = new(point.X, point.Y, actual.X, actual.Y);
            paint.Frame(new(point.X, point.Y, actual.X, actual.Y), color, default, 0, 0);
            ui.Dummy(actual);
        }
    }
}
