using Duxel.Core;

internal static class CardClippingRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check,
        int stride, UiTextureId white, UiContext context, int imageCount)
    {
        var viewport = new UiRect(0, 0, stride, 240);
        var bounds = new UiRect(16, 16, 120, 96);
        Curved.Radius = 32;
        using var card = new UiCard<Curved>("Flush clipped pixels", Dux.Expand(new ColorChild(new(255, 0, 0))), new(Width: 120, Height: 96));
        UiWidget current = card;
        int At(int x, int y) => (y * stride + x) * 4;
        void Background(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            ui.GetWindowDrawList().AddRectFilled(viewport, new(30, 70, 90), white);
        }
        void Draw(UiImmediateContext ui) { Background(ui); ui.SetCursorScreenPos(new(16, 16)); current.Render(ui); }
        void Reference(UiImmediateContext ui)
        {
            Background(ui);
            ui.GetWindowDrawList().AddRectFilledRounded(bounds, new(255, 0, 0), white, Curved.Radius, viewport);
        }
        static bool ReferenceEqual(byte[] actual, byte[] expected)
        {
            // Absolute and centered varyings can round the common SDF at an AA boundary by one channel unit.
            for (var i = 0; i < actual.Length; i++) if (Math.Abs(actual[i] - expected[i]) > 1) return false;
            return true;
        }
        var first = capture("card-flush-rounded", Draw);
        check(ReferenceEqual(first, capture("card-flush-reference", Reference)),
            "GPU zero-padding Card matches the independent analytic rounded primitive within one AA channel unit");
        check(first[At(17, 17)] == 30 && first[At(76, 64)] == 255,
            "GPU flush Card removes actual child corner paint and preserves its visible interior");
        check(first.AsSpan().SequenceEqual(capture("card-flush-replay", Draw)), "GPU flush Card cached replay is pixel exact");
        for (var frame = 0; frame <= imageCount; frame++) capture("card-flush-warm-" + frame, Draw);
        Curved.Radius = 0;
        var square = capture("card-flush-square", Draw);
        check(ReferenceEqual(square, capture("card-flush-square-reference", Reference)) && square[At(17, 17)] == 255,
            "GPU Card radius-only change removes the old corner clip without altering body allocation");
        for (var frame = 0; frame <= imageCount; frame++)
            check(square.AsSpan().SequenceEqual(capture("card-flush-square-repair-" + frame, Draw)),
                "GPU Card radius change repairs every warmed acquired image : " + frame);
        Curved.Radius = 32;
        bounds = bounds with { Width = 152 };
        card.Options = card.Options with { Width = 152 };
        var wider = capture("card-flush-wider", Draw);
        check(ReferenceEqual(wider, capture("card-flush-wider-reference", Reference)),
            "GPU rounded Card relayout updates its clip bounds with actual body width");
        var floating = new ColorChild(new(0, 255, 0), new(20, 20));
        using var floatingCard = new UiCard<Curved>("Rounded floating pixels", Dux.Expand(new ColorChild(new(255, 0, 0))),
            new(Width: 120, Height: 96), floatingAction: floating);
        current = floatingCard;
        var overlay = capture("card-rounded-float", Draw);
        check(floating.Bounds == new UiRect(112, 20, 20, 20),
            "GPU clipped floating child retains its actual top-right4px allocation");
        check(overlay.AsSpan(At(128, 24), 3).SequenceEqual(new byte[] { 30, 70, 90 })
            && overlay.AsSpan(At(120, 32), 3).SequenceEqual(new byte[] { 0, 255, 0 })
            && overlay.AsSpan(At(76, 64), 3).SequenceEqual(new byte[] { 255, 0, 0 }),
            "GPU floating action paints above primary content while both obey the same custom rounded boundary");
        check(overlay.AsSpan().SequenceEqual(capture("card-rounded-float-replay", Draw)),
            "GPU custom rounded floating action replays exact clipped pixels");
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        var calls = 0;
        using var menuCard = new UiCard<Curved>("Rounded menu pixels", Dux.Expand(Fluent.Menu("Rounded popup",
            [new("first", "Popup outside curve"), new("second", "Other popup item")], _ => calls++)), new(Width: 120, Height: 96));
        UiAccessibilityElement Find(string name)
        {
            UiAccessibilityElement? found = null;
            void Visit(string? id)
            {
                while (id is not null && tree.TryGet(id, out var item))
                { if (item.Node.Name == name) found = item; Visit(item.FirstChild); id = item.Next; }
            }
            Visit(tree.Root.First); return found ?? throw new InvalidOperationException(name);
        }
        try
        {
            current = menuCard;
            var closed = capture("card-rounded-menu-closed", Draw);
            var trigger = Find("Rounded popup");
            tree.RequestAction(trigger.Node.Id, trigger.RuntimeId, UiAccessibilityAction.Expand, wait: false);
            var open = capture("card-rounded-menu-open", Draw);
            var item = Find("Popup outside curve");
            var center = At((int)(item.Node.Bounds.X + item.Node.Bounds.Width / 2), (int)(item.Node.Bounds.Y + item.Node.Bounds.Height / 2));
            check(item.Node.Bounds.Y >= 112 && !open.AsSpan(center, 3).SequenceEqual(closed.AsSpan(center, 3)),
                "GPU actual menu popup paints beyond its rounded Card through the higher independent popup builder");
            tree.RequestAction(item.Node.Id, item.RuntimeId, UiAccessibilityAction.Invoke, wait: false);
            capture("card-rounded-menu-invoked", Draw);
            check(calls == 1, "GPU clipped Card preserves the actual external popup action");
        }
        finally { context.SetAccessibilityTree(null); Curved.Radius = 32; }
    }
    private readonly struct Curved : IUiCardPresentation
    {
        internal static float Radius = 32;
        public static UiCardMetrics Measure(UiCardOptions options) => new(0, Radius);
        public static void Paint(UiControlContext context, UiRect bounds, UiCardOptions options, UiCardMetrics metrics, UiButtonInteraction interaction) { }
    }
    private sealed class ColorChild(UiColor color, UiVector2? desired = null) : UiControlWidget
    {
        internal UiRect Bounds;
        protected override UiVector2 MeasureContent(UiControlContext context) => context.LayoutConstraints.Constrain(desired ?? new(120, 96));
        protected override void RenderControl(UiImmediateContext ui)
        {
            var paint = new UiControlContext(ui); var actual = paint.ConstrainToLayout(desired ?? new(120, 96));
            var origin = ui.GetCursorScreenPos(); Bounds = new(origin.X, origin.Y, actual.X, actual.Y);
            paint.Frame(Bounds, color, default, 0, 0); ui.Dummy(actual);
        }
    }
}
