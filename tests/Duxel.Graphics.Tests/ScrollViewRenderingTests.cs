using System.Numerics;
using Duxel.Core;

internal static class ScrollViewRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check,
        UiState state, float thickness, bool vector)
    {
        foreach (var axis in Enum.GetValues<UiScrollAxis>())
        foreach (var requested in new[] { 0f, 31f, 10000f })
        foreach (var narrow in new[] { false, true })
        foreach (var rightToLeft in new[] { false, true })
        {
            using var content = new PaintedContent();
            using var actual = Dux.ScrollView(new PaintedContent(), new UiScrollOptions(axis, 100, 60) { RightToLeft = rightToLeft });
            actual.Offset = new(requested, requested);
            var horizontal = axis != UiScrollAxis.Vertical;
            var vertical = axis != UiScrollAxis.Horizontal;
            var viewport = new UiVector2(100 - (vertical ? thickness : 0), 60 - (horizontal ? thickness : 0));
            var contentSize = new UiVector2(horizontal ? 300 : viewport.X, vertical ? 200 : viewport.Y);
            var maximum = new UiVector2(contentSize.X - viewport.X, contentSize.Y - viewport.Y);
            var offset = new UiVector2(Math.Clamp(requested, 0, maximum.X), Math.Clamp(requested, 0, maximum.Y));
            var physicalX = rightToLeft ? maximum.X - offset.X : offset.X;
            var viewportX = rightToLeft && vertical ? 20 + thickness : 20;
            var name = $"owned-scroll-{axis}-{requested}-{narrow}-{vector}" + (rightToLeft ? "-rtl" : "");
            var referenceId = "scroll-reference-" + state.FrameCount;
            void Scene(UiImmediateContext ui, bool compiled)
            {
                ui.EnableRootViewportContentLayout(contentPadding : 0);
                if (vector)
                    ui.BeginLayer("owned-scroll-vector", new(false, 1, default) { Scene3D = new(Matrix4x4.Identity, false) });
                try
                {
                    ui.SetCursorScreenPos(new(20, 20));
                    var root = new UiRect(20, 20, 100, 60);
                    using var clip = new UiControlContext(ui).Clip(narrow ? new(20, 20, 55, 35) : root);
                    if (compiled)
                    {
                        actual.Render(ui);
                        return;
                    }
                    ui.Dummy(new(100, 60));
                    ui.RenderLayoutChild(content, new(viewportX - physicalX, 20 - offset.Y, contentSize.X, contentSize.Y),
                        new(viewportX, 20, viewport.X, viewport.Y));
                    var inherited = new UiControlContext(ui).ClipRect;
                    if (vertical)
                        ui.RenderScrollbarV(referenceId + "-v", new(rightToLeft ? 20 : 20 + viewport.X, 20, thickness, viewport.Y),
                            offset.Y, maximum.Y, contentSize.Y, inherited);
                    if (horizontal)
                        ui.RenderScrollbarH(referenceId + "-h", new(viewportX, 20 + viewport.Y, viewport.X, thickness),
                            physicalX, maximum.X, contentSize.X, inherited);
                }
                finally
                {
                    if (vector)
                        ui.EndLayer();
                }
            }
            var pixels = capture(name, ui => Scene(ui, true));
            check(actual.Offset == offset, name + " clamps owned offsets to independently calculated extents");
            var expected = capture(name + "-reference", ui => Scene(ui, false));
            check(pixels.AsSpan().SequenceEqual(expected), name + " matches explicit child coordinates, clip and scrollbar geometry");
            state.ReleaseScrollbar(referenceId + "-v");
            state.ReleaseScrollbar(referenceId + "-h");
        }
    }

    private sealed class PaintedContent : UiControlWidget
    {
        protected override bool CanRetainMeasurement => true;
        protected override UiVector2 MeasureContent(UiControlContext context) => context.LayoutConstraints.Constrain(new(300, 200));
        protected override void RenderControl(UiImmediateContext ui)
        {
            var context = new UiControlContext(ui);
            var size = context.LayoutConstraints.Constrain(new(300, 200));
            var origin = ui.AllocateLayoutBox(size);
            context.Frame(new(origin.X, origin.Y, size.X, size.Y), new(22, 42, 72), default, 0, 0);
            for (var row = 0; row < 10; row++)
            {
                context.Frame(new(origin.X + row * 5, origin.Y + row * 20, size.X, 10),
                    new((byte)(30 + row * 20), 130, 190), default, 0, 0);
                context.Circle(new(origin.X + 15 + row * 25, origin.Y + 10 + row * 15), 5, new(240, 180, 40));
            }
        }
    }
}
