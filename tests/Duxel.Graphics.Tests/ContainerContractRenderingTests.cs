using Duxel.Core;

internal static class ContainerContractRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check)
    {
        using var container = new UiSingleChildBox<OverflowLayout, int, OverflowPaint>(null, 0);
        var expected = capture("custom-root-reference", ui =>
        {
            Root(ui);
            using var clip = new UiControlContext(ui).Clip(new(20, 20, 100, 60));
            OverflowPaint.Paint(new(ui), new(10, 10, 120, 80));
        });
        for (var frame = 0; frame < 4; frame++)
        {
            var actual = capture("custom-root-" + frame, ui =>
            {
                Root(ui);
                container.Render(ui);
            });
            check(actual.AsSpan().SequenceEqual(expected),
                "custom surface overflow matches independently clipped GPU paint, acquired frame " + frame);
        }
        AssignedInvalidation(capture, check);
        var rootPanel = capture("floating-panel-root-reference", ui => FloatingPanel(ui, nested : false));
        var nestedPanel = capture("floating-panel-child-caller", ui => FloatingPanel(ui, nested : true));
        check(rootPanel.AsSpan().SequenceEqual(nestedPanel),
            "floating panel chrome and content match the root invocation even when called inside a clipped child");
    }

    private static void FloatingPanel(UiImmediateContext ui, bool nested)
    {
        ui.EnableRootViewportContentLayout(contentPadding : 0);
        ui.SetCursorScreenPos(new(0, 160));
        ui.BeginChild("floating-panel-caller", new(120, 70), true);
        if (!nested)
            ui.EndChild();
        ui.SetNextPanelPos(new(150, 20));
        ui.SetNextPanelSize(new(160, 180));
        ui.BeginPanel("Floating panel");
        ui.Text("Visible body");
        ui.Button("Close sample");
        ui.EndPanel();
        if (nested)
            ui.EndChild();
    }

    private static void AssignedInvalidation(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check)
    {
        using var grid = new UiGrid([new RetainedBlock(new(210, 30, 50)), new RetainedBlock(new(20, 130, 220))],
            new(2, MainAxisExtent : 20));
        var bounds = new UiRect(20, 20, 100, 100);
        void Render(UiImmediateContext ui)
        {
            Root(ui);
            ui.RenderLayoutChild(grid, bounds, bounds);
        }
        capture("assigned-grid-warmup", Render);
        grid.Options = grid.Options with { CrossAxisCount = 1 };
        var actual = capture("assigned-grid-invalidated", Render);
        var expected = capture("assigned-grid-reference", ui =>
        {
            Root(ui);
            var draw = ui.GetWindowDrawList();
            draw.AddRectFilled(new(20, 20, 100, 20), new(210, 30, 50), ui.WhiteTextureId);
            draw.AddRectFilled(new(20, 40, 100, 20), new(20, 130, 220), ui.WhiteTextureId);
        });
        check(actual.AsSpan().SequenceEqual(expected),
            "invalidated assigned Grid paints independently placed rows without a parent measurement");
        for (var frame = 0; frame < 3; frame++)
        {
            actual = capture("assigned-grid-replay-" + frame, Render);
            check(actual.AsSpan().SequenceEqual(expected),
                "assigned Grid invalidation remains correct across acquired image reuse " + frame);
        }
    }

    private sealed class RetainedBlock(UiColor color) : UiControlWidget
    {
        protected override bool CanRetainMeasurement => true;
        protected override UiVector2 MeasureContent(UiControlContext context) => context.ConstrainToLayout(new(10, 10));
        protected override void RenderControl(UiImmediateContext ui)
        {
            var position = ui.GetCursorScreenPos();
            var size = new UiControlContext(ui).ConstrainToLayout(new(10, 10));
            ui.GetWindowDrawList().AddRectFilled(new(position.X, position.Y, size.X, size.Y), color, ui.WhiteTextureId);
            ui.Dummy(size);
        }
    }

    private static void Root(UiImmediateContext ui)
    {
        ui.EnableRootViewportContentLayout(contentPadding : 0);
        ui.SetCursorScreenPos(new(20, 20));
    }

    private readonly struct OverflowLayout : IUiSingleChildLayout<int>
    {
        public static void Validate(int options) { }
        public static UiContainerLayoutResult Measure(UiControlContext context, UiWidget? child, int options, UiBoxConstraints constraints)
        {
            var size = constraints.Constrain(new(100, 60));
            var surface = new UiRect(-10, -10, size.X + 20, size.Y + 20);
            return new(size, surface, surface);
        }
    }

    private readonly struct OverflowPaint : IUiContainerPresentation
    {
        public static void Paint(UiControlContext context, UiRect bounds) =>
            context.Frame(bounds, new(220, 40, 30), new(250, 250, 250), 0, 1);
    }
}
