using Duxel.Core;

internal static class OverflowWidgetTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        var probes = new[] { new Probe(new(60, 20)), new Probe(new(60, 20)), new Probe(new(60, 20)), new Probe(new(30, 16)) };
        var priorities = new[] { 0, 2, 0 };
        var overflow = Dux.Overflow(probes.Take(3).Cast<UiWidget>().ToArray(), probes[3], new(Spacing : 10), priorities);
        using var root = Dux.SizedBox(overflow, width : 150, height : 48);
        var screen = new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding : 0); root.Render(ui); });
        void Frame()
        {
            context.NewFrame(new(1f / 60, new(640, 400), new(1, 1)),
                new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default),
                new(0, 0, 640, 400), UiTextSettings.Default);
            context.Render(screen);
            using var draw = context.GetDrawData();
        }
        check(overflow.Result is null && overflow.Visibility.IsEmpty, "overflow has no invented visibility before its first layout");
        priorities[0] = 99;
        Frame();
        check(probes[0].Renders == 0 && probes[1].Renders == 1 && probes[2].Renders == 0 && probes[3].Renders == 1,
            "overflow renders only the retained child and indicator with copied constructor priorities");
        check(probes[1].Bounds == new UiRect(0, 14, 60, 20) && probes[3].Bounds == new UiRect(70, 16, 30, 16),
            "overflow assigns packed main-axis positions and centers each actual child on the cross axis");
        overflow.SetPriorities([3, 0, 0]);
        Frame();
        check(probes[0].Renders == 1 && probes[1].Renders == 1 && probes.All(probe => probe.Disposals == 0),
            "priority updates switch visibility without disposing hidden owned children");
        overflow.Options = overflow.Options with { RightToLeft = true };
        Frame();
        check(probes[0].Bounds.X == 90 && probes[3].Bounds.X == 50,
            "right-to-left overflow packs source-leading content from the parent's right edge");
        root.Options = root.Options with { Width = 220 };
        var indicatorRenders = probes[3].Renders;
        Frame();
        check(overflow.Result is { HiddenCount : 0 } && probes[0].Renders > 1 && probes[2].Renders == 1 && probes[3].Renders == indicatorRenders,
            "expanding capacity restores the same child instances and stops rendering the unnecessary indicator");
        probes[2].Resize(new(140, 20));
        Frame();
        check(overflow.Result is { HiddenCount : > 0 }, "a hidden/restored child's size invalidation recomputes retained overflow layout");
        root.Dispose();
        check(probes.All(probe => probe.Disposals == 1), "overflow disposes visible, hidden and indicator children exactly once");

        var verticalProbes = new[] { new Probe(new(30, 40)), new Probe(new(50, 40)), new Probe(new(20, 20)) };
        using var vertical = Dux.VerticalOverflow(verticalProbes.Take(2).Cast<UiWidget>().ToArray(), verticalProbes[2], new(Spacing : 4));
        using var verticalRoot = Dux.SizedBox(vertical, width : 70, height : 70);
        var verticalScreen = new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding : 0); verticalRoot.Render(ui); });
        context.NewFrame(new(1f / 60, new(640, 400), new(1, 1)),
            new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default),
            new(0, 0, 640, 400), UiTextSettings.Default);
        context.Render(verticalScreen);
        using var verticalDraw = context.GetDrawData();
        check(vertical.Result is { VisibleCount : 1, HiddenCount : 1 } && verticalProbes[0].Bounds == new UiRect(20, 0, 30, 40)
            && verticalProbes[2].Bounds == new UiRect(25, 44, 20, 20),
            "the same overflow contract applies to bounded vertical layout and actual cross-axis allocation");
    }

    private sealed class Probe(UiVector2 desired) : UiControlWidget
    {
        internal UiRect Bounds;
        internal int Renders, Disposals;
        private UiVector2 _desired = desired;
        protected override bool CanRetainMeasurement => true;
        internal void Resize(UiVector2 size) { _desired = size; RequestRebuild(); }
        protected override UiVector2 MeasureContent(UiControlContext context) => context.LayoutConstraints.Constrain(_desired);
        protected override void RenderControl(UiImmediateContext ui)
        {
            Renders++;
            var size = new UiControlContext(ui).ConstrainToLayout(_desired);
            var origin = ui.AllocateLayoutBox(size);
            Bounds = new(origin.X, origin.Y, size.X, size.Y);
            ui.FinishLayoutBox(origin, size);
        }
        protected override void OnDispose() { Disposals++; base.OnDispose(); }
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
