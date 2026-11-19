using Duxel.Core;

internal static class CardFooterTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(Action<UiImmediateContext> draw, UiInputState? input = null)
        {
            context.NewFrame(new(1f / 60, new(400, 300), new(1, 1)), input ?? idle, new(0, 0, 400, 300), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding: 0); draw(ui); }));
            using var data = context.GetDrawData();
        }
        static bool Throws<T>(Action action) where T : Exception
        { try { action(); return false; } catch (T) { return true; } }
        var first = new Probe(new(40, 20));
        var second = new Probe(new(30, 30));
        var action = new Probe(new(25, 10));
        using var footer = Fluent.CardFooter([first, second], action);
        Frame(ui => check(footer.MeasureConstrained(ui, new(0, float.PositiveInfinity, 0, 100)) == new UiVector2(119, 30),
            "Card Footer natural width includes source 12px gaps and the action"));
        check(first.Paints == 0 && second.Paints == 0 && action.Paints == 0,
            "Card Footer measurement does not paint children");
        using var box = Dux.SizedBox(footer, 200, 50);
        Frame(box.Render);
        check(first.Bounds == new UiRect(0, 0, 40, 50), "Card Footer stretches content to the allocated cross axis");
        check(second.Bounds == new UiRect(52, 0, 30, 50), "Card Footer keeps adjacent content exactly 12px apart");
        check(action.Bounds == new UiRect(175, 0, 25, 50), "Card Footer action reaches the far edge with spare space before it");
        check(ReferenceEquals(footer.Action, action) && footer.Content.Length == 2 && ReferenceEquals(footer.Content[0], first),
            "Card Footer exposes its actual owned slots");
        var measures = first.Measures + second.Measures + action.Measures;
        Frame(box.Render);
        check(first.Measures + second.Measures + action.Measures == measures,
            "Card Footer stable replay reuses common retained measurement");
        first.Resize(new(60, 20));
        Frame(box.Render);
        check(second.Bounds.X == 72 && action.Bounds.X == 175,
            "Card Footer child invalidation moves siblings while preserving the far-edge action");
        check(context.State.NextScheduledFrameTimeSeconds is null, "Card Footer stable content has no periodic frame request");
        using var plainFirst = new Probe(new(10, 20));
        using var plainSecond = new Probe(new(20, 10));
        using var plain = Fluent.CardFooter([plainFirst, plainSecond]);
        Frame(plain.Render);
        check(plainFirst.Bounds.X == 0 && plainSecond.Bounds.X == 22 && plain.Action is null,
            "Card Footer without an action keeps all content at the start");
        using var aloneProbe = new Probe(new(25, 10));
        using var alone = Fluent.CardFooter([], aloneProbe);
        Frame(alone.Render);
        check(aloneProbe.Bounds.X == 375 && alone.Content.Length == 0,
            "Card Footer action-only layout reaches the far edge without a phantom content gap");
        using var empty = Fluent.CardFooter([]);
        Frame(ui => check(empty.MeasureConstrained(ui, new(0, float.PositiveInfinity, 0, 100)) == default,
            "Card Footer empty unbounded layout has zero natural size"));
        Frame(ui => check(Throws<InvalidOperationException>(() => footer.Measure(ui, new(50, 100))),
            "Card Footer explicitly rejects widths smaller than its content and gaps"));
        using var reusable = new Probe(new(10, 10));
        check(Throws<ArgumentException>(() => _ = Fluent.CardFooter([reusable], reusable)),
            "Card Footer rejects a duplicate across content and action before adoption");
        using var valid = Fluent.CardFooter([reusable]);
        using var spare = new Probe(new(10, 10));
        check(Throws<InvalidOperationException>(() => _ = Fluent.CardFooter([spare], reusable)),
            "Card Footer preflights an already owned action before adopting other content");
        using var spareOwner = Fluent.CardFooter([spare]);
        check(Fluent.ComponentNames.Contains("CardFooter"), "Card Footer is available through the generated Fluent catalog");
        box.Dispose();
        Frame(ui => check(Throws<ObjectDisposedException>(() => action.Render(ui)),
            "Card Footer disposal releases its far-end action with its content"));

        var rootCalls = 0;
        var childCalls = 0;
        using var card = Fluent.Card("Footer card", Fluent.CardFooter(
            [Fluent.Button("Footer primary", () => childCalls++)], Fluent.Button("Footer action", () => childCalls += 10)),
            new(Width: 320, Appearance: UiCardAppearance.Outline), activated: () => rootCalls++);
        UiAccessibilityElement Find(string name)
        {
            UiAccessibilityElement? result = null;
            void Visit(string? id)
            {
                while (id is not null && tree.TryGet(id, out var item))
                { if (item.Node.Name == name) result = item; Visit(item.FirstChild); id = item.Next; }
            }
            Visit(tree.Root.First);
            return result ?? throw new InvalidOperationException("Missing Footer fixture node : " + name);
        }
        Frame(card.Render);
        var primary = Find("Footer primary");
        var last = Find("Footer action");
        check(primary.Node.ParentId == Find("Footer card").Node.Id && last.Node.ParentId == primary.Node.ParentId,
            "Card Footer layout adds no artificial accessibility group between its real controls and Card");
        var point = new UiVector2(last.Node.Bounds.X + last.Node.Bounds.Width / 2, last.Node.Bounds.Y + last.Node.Bounds.Height / 2);
        Frame(card.Render, idle with { MousePosition = point, LeftMouseDown = true, LeftMousePressed = true });
        Frame(card.Render, idle with { MousePosition = point, LeftMouseReleased = true });
        check(childCalls == 10 && rootCalls == 0, "Card Footer real far-edge button owns its pointer without activating Card");
        primary = Find("Footer primary");
        check(tree.RequestAction(primary.Node.Id, primary.RuntimeId, UiAccessibilityAction.Focus, wait: false) == UiAccessibilityResult.Success,
            "Card Footer content accepts common accessibility focus");
        Frame(card.Render);
        Frame(card.Render, idle with { KeyEvents = [new(UiKey.Tab, true, default)] });
        check(context.State.FocusedId == Find("Footer action").Node.Id,
            "Card Footer keyboard order follows content then far-edge action");
    }

    private sealed class Probe(UiVector2 size) : UiControlWidget
    {
        private UiVector2 _size = size;
        internal int Measures, Paints;
        internal UiRect Bounds;
        internal void Resize(UiVector2 value) { _size = value; RequestRebuild(); }
        protected override bool CanRetainMeasurement => true;
        protected override UiVector2 MeasureContent(UiControlContext context) { Measures++; return context.LayoutConstraints.Constrain(_size); }
        protected override void RenderControl(UiImmediateContext ui)
        {
            Paints++;
            var actual = new UiControlContext(ui).ConstrainToLayout(_size);
            var origin = ui.GetCursorScreenPos();
            Bounds = new(origin.X, origin.Y, actual.X, actual.Y);
            ui.Dummy(actual);
        }
    }
    private sealed class Screen(Action<UiImmediateContext> draw) : UiScreen
    { public override void Render(UiImmediateContext ui) => draw(ui); }
}
