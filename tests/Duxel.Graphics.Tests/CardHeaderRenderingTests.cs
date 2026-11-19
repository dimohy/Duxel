using Duxel.Core;

internal static class CardHeaderRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check, int stride)
    {
        using var card = Fluent.Card("Header pixels", Fluent.CardHeader(
            new Probe(new(40, 20), new(0, 255, 0)), new Probe(new(60, 12), new(255, 255, 0)),
            new Probe(new(32, 48), new(255, 0, 0)), new Probe(new(24, 24), new(0, 0, 255))),
            new(Appearance: UiCardAppearance.Outline, Width: 176, Height: 112));
        UiWidget current = card;
        void Draw(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            var paint = new UiControlContext(ui);
            paint.Frame(paint.ClipRect, new(30, 70, 90), default, 0, 0);
            ui.SetCursorScreenPos(new(16, 16));
            current.Render(ui);
        }
        bool Pixel(byte[] pixels, int x, int y, byte r, byte g, byte b)
            => pixels.AsSpan((y * stride + x) * 4, 3).SequenceEqual(new byte[] { r, g, b });
        var first = capture("header-grid", Draw);
        check(Pixel(first, 30, 40, 255, 0, 0) && Pixel(first, 75, 35, 0, 255, 0),
            "GPU Card Header image and centered first auto row use the pinned source columns");
        check(Pixel(first, 75, 65, 255, 255, 0) && Pixel(first, 160, 45, 0, 0, 255),
            "GPU Card Header second auto row and spanning action occupy their actual slots");
        check(Pixel(first, 65, 40, 30, 70, 90), "GPU Card Header image margin stays unpainted");
        check(first.AsSpan().SequenceEqual(capture("header-grid-replay", Draw)),
            "GPU Card Header unchanged replay preserves exact child pixels");
        card.Options = card.Options with { Width = 208 };
        var wider = capture("header-grid-wider", Draw);
        check(Pixel(wider, 190, 45, 0, 0, 255) && Pixel(wider, 160, 45, 0, 255, 0)
            && Pixel(wider, 30, 40, 255, 0, 0),
            "GPU Card Header width change grows its middle column and moves the far-edge action");
        using var flex = Fluent.Card("Header flex pixels", Fluent.CardHeader(
            new Probe(new(40, 20), new(0, 255, 0)), image: new Probe(new(32, 48), new(255, 0, 0)),
            action: new Probe(new(24, 24), new(0, 0, 255))),
            new(Appearance: UiCardAppearance.Outline, Width: 176, Height: 112));
        current = flex;
        var noDescription = capture("header-flex", Draw);
        check(Pixel(noDescription, 75, 43, 0, 255, 0) && Pixel(noDescription, 75, 65, 30, 70, 90)
            && Pixel(noDescription, 160, 45, 0, 0, 255),
            "GPU Card Header without description centers its title in the flex row");
        using var emptyMiddle = Fluent.Card("Header margins pixels", Fluent.CardHeader(
            image: new Probe(new(32, 48), new(255, 0, 0)), action: new Probe(new(24, 24), new(0, 0, 255))),
            new(Appearance: UiCardAppearance.Outline, Width: 176, Height: 112));
        current = emptyMiddle;
        var margins = capture("header-margins", Draw);
        check(Pixel(margins, 86, 45, 0, 0, 255) && Pixel(margins, 160, 45, 30, 70, 90),
            "GPU Card Header preserves both margins when its growing text slot is absent");
    }
    private sealed class Probe(UiVector2 size, UiColor color) : UiControlWidget
    {
        protected override bool CanRetainMeasurement => true;
        protected override UiVector2 MeasureContent(UiControlContext context) => context.LayoutConstraints.Constrain(size);
        protected override void RenderControl(UiImmediateContext ui)
        {
            var paint = new UiControlContext(ui);
            var actual = paint.ConstrainToLayout(size);
            var origin = ui.GetCursorScreenPos();
            paint.Frame(new(origin.X, origin.Y, actual.X, actual.Y), color, default, 0, 0);
            ui.Dummy(actual);
        }
    }
}
