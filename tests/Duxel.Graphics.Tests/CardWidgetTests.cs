using Duxel.Core;

internal static class CardWidgetTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        UiDrawData Frame(Action<UiImmediateContext> draw, UiInputState? input = null)
        {
            context.NewFrame(new(1f / 60, new(320, 240), new(1, 1)), input ?? idle, new(0, 0, 320, 240), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding: 0); draw(ui); }));
            return context.GetDrawData();
        }
        UiAccessibilityElement Find(UiAccessibilityRole role)
        {
            UiAccessibilityElement? found = null;
            void Visit(string? id)
            {
                while (id is not null && tree.TryGet(id, out var item))
                {
                    if (item.Node.Role == role) found = item;
                    Visit(item.FirstChild); id = item.Next;
                }
            }
            Visit(tree.Root.First);
            return found ?? throw new InvalidOperationException("Card fixture accessibility node is missing.");
        }
        var probe = new Probe();
        using var card = Fluent.Card("Information", probe, new(Appearance: UiCardAppearance.Outline));
        using (var measured = Frame(ui => check(card.Measure(ui, new(300, 200)) == new UiVector2(64, 44),
            "Card natural size includes its medium content padding")))
            check(probe.Paints == 0 && measured.TextureUpdates.Count == 0, "Card measurement neither paints nor allocates a shadow texture");
        using (var first = Frame(card.Render))
        {
            var node = Find(UiAccessibilityRole.Card).Node;
            check(node.Name == "Information" && !node.Focusable && !node.ValuePattern,
                "passive Card exposes a named accessibility group without taking a keyboard stop");
            check(probe.Bounds == new UiRect(12, 12, 40, 20), "Card allocates its actual child inside the body padding");
        }
        var measures = probe.Measures;
        using (var replay = Frame(card.Render)) check(probe.Measures == measures && probe.Paints == 2,
            "Card retains stable layout while its actual child rendering continues");
        card.Options = card.Options with { Size = UiCardSize.Small, Width = 100, Height = 60 };
        using (var resized = Frame(card.Render)) check(probe.Bounds.X == 8 && probe.Bounds.Y == 8
            && Find(UiAccessibilityRole.Card).Node.Bounds == new UiRect(0, 0, 100, 60),
            "Card option changes invalidate layout and preserve its explicitly sized body");
        card.Options = card.Options with { Appearance = UiCardAppearance.Filled };
        long uploads = 0;
        using (var shadow = Frame(ui => { card.Render(ui); uploads = ui.ShadowTextureStatistics.Uploads; }))
            check(uploads > 0 && Find(UiAccessibilityRole.Card).Node.Bounds.Width == 100,
                "filled Card reserves a common shadow around its body without changing body width");
        using (var shadowReplay = Frame(ui => { card.Render(ui); check(ui.ShadowTextureStatistics.Uploads == uploads,
            "unchanged Card reuses the common shadow texture"); })) { }
        check(context.State.NextScheduledFrameTimeSeconds is null, "passive Card schedules no periodic frames");
        using (var tight = Dux.SizedBox(Fluent.Card("Tight", new Probe(), new(Width: 200)), 70, 36))
        using (var frame = Frame(tight.Render))
            check(frame.DrawLists.SelectMany(list => list.Commands).All(command => command.ClipRect.X >= 0 && command.ClipRect.Y >= 0
                && command.ClipRect.X + command.ClipRect.Width <= 70 && command.ClipRect.Y + command.ClipRect.Height <= 36),
                "Card body shadow and content stay clipped within the authoritative tight parent allocation");
        var executions = 0;
        using var actionCard = Fluent.Card("Actions", Fluent.Button("Do work", () => executions++), new(Appearance: UiCardAppearance.Outline));
        using (var frame = Frame(actionCard.Render)) { }
        var button = Find(UiAccessibilityRole.Button);
        check(button.Node.ParentId == Find(UiAccessibilityRole.Card).Node.Id, "Card child actions belong to their real accessibility group");
        var point = new UiVector2(button.Node.Bounds.X + button.Node.Bounds.Width / 2, button.Node.Bounds.Y + button.Node.Bounds.Height / 2);
        using (var press = Frame(actionCard.Render, idle with { MousePosition = point, LeftMouseDown = true, LeftMousePressed = true })) { }
        using (var release = Frame(actionCard.Render, idle with { MousePosition = point, LeftMouseReleased = true })) { }
        check(executions == 1, "Card content keeps actual child pointer activation");
        static bool Throws<T>(Action action) where T : Exception
        { try { action(); return false; } catch (T) { return true; } }
        check(Throws<ArgumentOutOfRangeException>(() => card.Options = new(Size: (UiCardSize)99)), "Card rejects undeclared sizes");
        check(Throws<ArgumentOutOfRangeException>(() => card.Options = new(Height: float.NaN)), "Card rejects invalid dimensions before mutation");
        check(Throws<InvalidOperationException>(() => _ = Fluent.Card("Shared", probe)), "Card content ownership cannot be shared between parents");
        card.Dispose();
        using (var disposed = Frame(ui => check(Throws<ObjectDisposedException>(() => probe.Render(ui)),
            "disposing Card releases its owned content"))) { }
        check(Fluent.ComponentNames.Contains("Card"), "Card is exposed through the generated Fluent catalog");
        using var custom = new UiCard<CustomPresentation>("Custom", new Probe());
        using (var measured = Frame(ui => check(custom.Measure(ui, new(300, 200)) == new UiVector2(52, 32),
            "custom Card presentation selects its own independent layout metrics"))) { }
        CustomPresentation.Padding = 12;
        using (var measured = Frame(ui => check(custom.Measure(ui, new(300, 200)) == new UiVector2(64, 44),
            "custom Card metrics without a retention promise are evaluated again"))) { }
        CustomPresentation.Padding = 1;
        using (var flush = Frame(ui => check(custom.Measure(ui, new(300, 200)) == new UiVector2(42, 22),
            "Card accepts custom padding below its radius without altering layout metrics"))) { }
        CustomPresentation.Padding = 6;
    }
    private readonly struct CustomPresentation : IUiCardPresentation
    {
        internal static float Padding = 6;
        public static UiCardMetrics Measure(UiCardOptions options) => new(Padding, 2);
        public static void Paint(UiControlContext context, UiRect bounds, UiCardOptions options, UiCardMetrics metrics, UiButtonInteraction interaction)
            => context.Frame(bounds, new(0, 255, 0), default, metrics.CornerRadius, 0);
    }
    private sealed class Probe : UiControlWidget
    {
        internal int Measures, Paints;
        internal UiRect Bounds;
        protected override bool CanRetainMeasurement => true;
        protected override UiVector2 MeasureContent(UiControlContext context) { Measures++; return context.LayoutConstraints.Constrain(new(40, 20)); }
        protected override void RenderControl(UiImmediateContext ui)
        {
            Paints++;
            var context = new UiControlContext(ui);
            var size = context.ConstrainToLayout(new(40, 20));
            var origin = ui.GetCursorScreenPos();
            Bounds = new(origin.X, origin.Y, size.X, size.Y);
            context.Frame(Bounds, new(255, 0, 0), default, 0, 0);
            ui.Dummy(size);
        }
    }
    private sealed class Screen(Action<UiImmediateContext> draw) : UiScreen { public override void Render(UiImmediateContext ui) => draw(ui); }
}
