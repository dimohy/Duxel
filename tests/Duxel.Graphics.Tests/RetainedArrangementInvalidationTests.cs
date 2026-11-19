using Duxel.Core;

internal static class RetainedArrangementInvalidationTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var first = new RetainedProbe();
        var second = new RetainedProbe();
        using var grid = new UiGrid([first, second], new(2, MainAxisExtent : 20));
        var bounds = new UiRect(0, 0, 100, 100);
        void Frame(Action<UiImmediateContext> draw)
        {
            context.NewFrame(new(1f / 60, new(100, 100), new(1, 1)), idle, bounds, UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding : 0);
                draw(ui);
            }));
            using var data = context.GetDrawData();
        }
        void Render(UiImmediateContext ui) => ui.RenderLayoutChild(grid, bounds, bounds);

        Frame(Render);
        check(second.Position == new UiVector2(50, 0) && second.Size == new UiVector2(50, 20),
            "assigned retained Grid initially lays out two columns");
        grid.Options = grid.Options with { CrossAxisCount = 1 };
        Frame(Render);
        check(second.Position == new UiVector2(0, 20) && second.Size == new UiVector2(100, 20),
            "assigned retained Grid re-arranges invalidated options on the next frame without a parent measurement");

        grid.Options = grid.Options with { CrossAxisCount = 2 };
        Frame(ui =>
        {
            grid.MeasureConstrained(ui, UiBoxConstraints.Tight(new(100, 100)));
            grid.Options = grid.Options with { CrossAxisCount = 1 };
            Render(ui);
        });
        check(second.Position == new UiVector2(50, 0),
            "same-frame assigned rendering preserves geometry already measured by its parent");
        Frame(Render);
        check(second.Position == new UiVector2(0, 20),
            "same-frame deferred arrangement invalidation is applied on the following frame");
        var measured = second.Measurements;
        Frame(Render);
        check(second.Measurements == measured,
            "unchanged assigned Grid still reuses retained child measurements across frames");

        var stackChild = new RetainedProbe();
        using var stack = new UiStack([stackChild], new(Alignment : UiAlignment.TopLeft));
        Frame(ui => ui.RenderLayoutChild(stack, bounds, bounds));
        stack.Options = new(Alignment : UiAlignment.BottomRight);
        Frame(ui => ui.RenderLayoutChild(stack, bounds, bounds));
        check(stackChild.Position == new UiVector2(90, 90),
            "assigned retained Stack applies alignment invalidation on the next frame");

        var boxChild = new RetainedProbe();
        using var box = new UiContainer(boxChild, new(Alignment : UiAlignment.TopLeft));
        Frame(ui => ui.RenderLayoutChild(box, bounds, bounds));
        box.Options = new(Alignment : UiAlignment.BottomRight);
        Frame(ui => ui.RenderLayoutChild(box, bounds, bounds));
        check(boxChild.Position == new UiVector2(90, 90),
            "assigned retained single-child Container applies alignment invalidation on the next frame");
    }

    private sealed class RetainedProbe : UiControlWidget
    {
        internal UiVector2 Position, Size;
        internal int Measurements;
        protected override bool CanRetainMeasurement => true;
        protected override UiVector2 MeasureContent(UiControlContext context)
        {
            Measurements++;
            return context.ConstrainToLayout(new(10, 10));
        }
        protected override void RenderControl(UiImmediateContext ui)
        {
            Position = ui.GetCursorScreenPos();
            Size = new UiControlContext(ui).ConstrainToLayout(new(10, 10));
            ui.Dummy(Size);
        }
    }

    private sealed class Screen(Action<UiImmediateContext> draw) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => draw(ui);
    }
}
