using Duxel.Core;

internal static class CardHeaderTests
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
            context.NewFrame(new(1f / 60, new(440, 360), new(1, 1)), input ?? idle, new(0, 0, 440, 360), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding: 0); draw(ui); }));
            using var data = context.GetDrawData();
        }
        static bool Throws<T>(Action action) where T : Exception
        { try { action(); return false; } catch (T) { return true; } }
        var title = new Probe(new(40, 20));
        var description = new Probe(new(60, 12));
        var image = new Probe(new(32, 48));
        var action = new Probe(new(24, 24));
        using var header = Fluent.CardHeader(title, description, image, action);
        Frame(ui => check(header.MeasureConstrained(ui, new(0, float.PositiveInfinity, 0, 100)) == new UiVector2(140, 48),
            "Card Header natural size includes independent source image/action margins and stacked text"));
        check(title.Paints == 0 && image.Paints == 0 && description.Paints == 0 && action.Paints == 0,
            "Card Header measurement does not execute child painting");
        using var parent = Dux.SizedBox(header, 200, 80);
        Frame(parent.Render);
        check(image.Bounds == new UiRect(0, 16, 32, 48), "Card Header image spans both centered auto rows");
        check(title.Bounds == new UiRect(44, 12, 120, 20), "Card Header title occupies its centered first row and growing middle column");
        check(description.Bounds == new UiRect(44, 56, 120, 12), "Card Header description occupies its independently centered second row");
        check(action.Bounds == new UiRect(176, 28, 24, 24), "Card Header action spans both rows at the far edge");
        check(ReferenceEquals(header.Image, image) && ReferenceEquals(header.Header, title)
            && ReferenceEquals(header.Description, description) && ReferenceEquals(header.Action, action),
            "Card Header exposes its actual owned slots");
        var measures = title.Measures + description.Measures + image.Measures + action.Measures;
        Frame(parent.Render);
        check(title.Measures + description.Measures + image.Measures + action.Measures == measures,
            "Card Header stable replay retains its actual child measurements");
        image.Resize(new(48, 48));
        Frame(parent.Render);
        check(title.Bounds.X == 60 && title.Bounds.Width == 104 && action.Bounds.X == 176,
            "Card Header image invalidation updates the middle column while retaining the far-edge action");
        using var plainTitle = new Probe(new(40, 20));
        using var plainImage = new Probe(new(32, 48));
        using var plainAction = new Probe(new(24, 24));
        using var plain = Dux.SizedBox(Fluent.CardHeader(plainTitle, image: plainImage, action: plainAction), 200, 80);
        Frame(plain.Render);
        check(plainTitle.Bounds == new UiRect(44, 30, 120, 20) && plainImage.Bounds == new UiRect(0, 16, 32, 48)
            && plainAction.Bounds == new UiRect(176, 28, 24, 24),
            "Card Header without description uses centered flex placement without a phantom second row");
        using var onlyTitle = new Probe(new(40, 20));
        using var minimum = Fluent.CardHeader(onlyTitle);
        Frame(ui => check(minimum.MeasureConstrained(ui, new(200, float.PositiveInfinity, 80, 100)) == new UiVector2(200, 80),
            "Card Header honors an unbounded maximum with explicit minimum dimensions"));
        using var minimumBox = Dux.SizedBox(minimum, 200, 80);
        Frame(minimumBox.Render);
        check(onlyTitle.Bounds == new UiRect(0, 30, 200, 20), "Card Header title grows into an explicit minimum width");
        using var bareImage = new Probe(new(32, 48));
        using var bareAction = new Probe(new(24, 24));
        using var bare = Dux.SizedBox(Fluent.CardHeader(image: bareImage, action: bareAction), 200, 80);
        Frame(bare.Render);
        check(bareAction.Bounds == new UiRect(56, 28, 24, 24),
            "Card Header without text preserves both source margins instead of inventing a growing middle slot");
        using var actionOnly = new Probe(new(24, 24));
        using var actionOnlyBox = Dux.SizedBox(Fluent.CardHeader(action: actionOnly), 200, 80);
        Frame(actionOnlyBox.Render);
        check(actionOnly.Bounds == new UiRect(12, 28, 24, 24), "Card Header action-only preserves its source leading margin");
        for (var mask = 0; mask < 16; mask++)
        {
            var slots = Enumerable.Range(0, 4).Select(i => (mask & (1 << i)) == 0 ? null : new Probe(new(8, 8))).ToArray();
            using var variant = Fluent.CardHeader(slots[1], slots[2], slots[0], slots[3]);
            Frame(variant.Render);
            var ordered = slots.Where(slot => slot is not null).ToArray();
            check(variant.Children.Length == ordered.Length && variant.Children.ToArray().SequenceEqual(ordered),
                "Card Header optional slots preserve image/title/description/action source order : " + mask);
        }
        using var shared = new Probe(new(8, 8));
        check(Throws<ArgumentException>(() => _ = Fluent.CardHeader(shared, image: shared)),
            "Card Header rejects a duplicate across named slots before adoption");
        using var owner = Fluent.CardHeader(shared);
        using var available = new Probe(new(8, 8));
        check(Throws<InvalidOperationException>(() => _ = Fluent.CardHeader(available, action: shared)),
            "Card Header preflights an already owned action before adopting its title");
        using var availableOwner = Fluent.CardHeader(available);
        Frame(ui => check(Throws<InvalidOperationException>(() => header.MeasureConstrained(ui, new(0, 30, 0, 100))),
            "Card Header fails explicitly when image/action margins cannot fit"));
        check(context.State.NextScheduledFrameTimeSeconds is null, "Card Header schedules no periodic frames");
        check(Fluent.ComponentNames.Contains("CardHeader") && Fluent.RequiredComponentNames.Length == 55,
            "Card Header is a generated Card variant without increasing official top-level requirements");
        parent.Dispose();
        Frame(ui => check(Throws<ObjectDisposedException>(() => title.Render(ui))
            && Throws<ObjectDisposedException>(() => _ = header.Action), "Card Header disposal releases slots and rejects access"));
        RunInput(context, tree, idle, Frame, check);
    }

    private static void RunInput(UiContext context, UiAccessibilityTree tree, UiInputState idle,
        Action<Action<UiImmediateContext>, UiInputState?> frame, Action<bool, string> check)
    {
        var rootCalls = 0;
        var actions = 0;
        var selected = false;
        var selectionWrites = 0;
        var text = "Title";
        using var card = Fluent.Card("Header card", Fluent.CardHeader(
            Fluent.Input("Header input", new UiBinding<string>(() => text, value => text = value), 100, 120),
            Fluent.Button("Description action", () => actions += 10),
            Fluent.Button("Image action", () => actions++),
            Fluent.Button("Header action", () => actions += 100)),
            new(Width: 420, Appearance: UiCardAppearance.Outline), activated: () => rootCalls++,
            selection: new(() => selected, value => { selected = value; selectionWrites++; }));
        UiAccessibilityElement Find(string name, UiAccessibilityRole? role = null)
        {
            UiAccessibilityElement? result = null;
            void Visit(string? id)
            {
                while (id is not null && tree.TryGet(id, out var item))
                { if (item.Node.Name == name && (role is null || item.Node.Role == role)) result = item; Visit(item.FirstChild); id = item.Next; }
            }
            Visit(tree.Root.First);
            return result ?? throw new InvalidOperationException("Missing Header input fixture : " + name);
        }
        frame(card.Render, null);
        var action = Find("Header action");
        check(action.Node.ParentId == Find("Header card", UiAccessibilityRole.Card).Node.Id && Find("Header input").Node.ParentId == action.Node.ParentId,
            "Card Header named slots keep actual control providers directly under Card");
        var point = new UiVector2(action.Node.Bounds.X + action.Node.Bounds.Width / 2, action.Node.Bounds.Y + action.Node.Bounds.Height / 2);
        frame(card.Render, idle with { MousePosition = point, LeftMouseDown = true, LeftMousePressed = true });
        frame(card.Render, idle with { MousePosition = point, LeftMouseReleased = true });
        check(actions == 100 && rootCalls == 0, "Card Header action owns its actual pointer without invoking Card");
        var image = Find("Image action");
        tree.RequestAction(image.Node.Id, image.RuntimeId, UiAccessibilityAction.Focus, wait: false);
        frame(card.Render, null);
        foreach (var name in new[] { "Header input", "Description action", "Header action" })
        {
            frame(card.Render, idle with { KeyEvents = [new(UiKey.Tab, true, default)] });
            check(context.State.FocusedId == Find(name).Node.Id, "Card Header Tab order follows source slots : " + name);
        }
        var input = Find("Header input");
        tree.RequestAction(input.Node.Id, input.RuntimeId, UiAccessibilityAction.Focus, wait: false);
        frame(card.Render, null);
        frame(card.Render, idle with { CharEvents = [new('x')] });
        check(text.Contains('x') && rootCalls == 0 && actions == 100 && !selected && selectionWrites == 0,
            "Card Header title slot preserves real text editing independently of Card actions");
    }

    private sealed class Probe(UiVector2 size) : UiControlWidget
    {
        private UiVector2 _size = size;
        internal UiRect Bounds;
        internal int Measures, Paints;
        internal void Resize(UiVector2 value) { _size = value; RequestRebuild(); }
        protected override bool CanRetainMeasurement => true;
        protected override UiVector2 MeasureContent(UiControlContext context) { Measures++; return context.LayoutConstraints.Constrain(_size); }
        protected override void RenderControl(UiImmediateContext ui)
        {
            Paints++;
            var actual = new UiControlContext(ui).ConstrainToLayout(_size);
            var point = ui.GetCursorScreenPos();
            Bounds = new(point.X, point.Y, actual.X, actual.Y);
            ui.Dummy(actual);
        }
    }
    private sealed class Screen(Action<UiImmediateContext> draw) : UiScreen
    { public override void Render(UiImmediateContext ui) => draw(ui); }
}
