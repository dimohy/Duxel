using Duxel.Core;

internal static class CardFloatingActionTests
{
    internal static void Run(Action<bool, string> check)
    {
        RunGeometry(check);
        foreach (var mode in Enum.GetValues<UiFocusGroupMode>())
            RunInput(mode, (condition, name) => check(condition, name + " : " + mode));
    }

    private static void RunGeometry(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(Action<UiImmediateContext> draw)
        {
            context.NewFrame(new(1f / 60, new(320, 240), new(1, 1)), idle, new(0, 0, 320, 240), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding: 0); draw(ui); }));
            using var data = context.GetDrawData();
        }
        static bool Throws<T>(Action action) where T : Exception
        { try { action(); return false; } catch (T) { return true; } }
        var primary = new Probe(new(40, 20));
        var overlay = new Probe(new(100, 60));
        using var card = Fluent.Card("Floating geometry", primary, new(Appearance: UiCardAppearance.Outline), floatingAction: overlay);
        Frame(ui => check(card.Measure(ui, new(300, 200)) == new UiVector2(64, 44) && overlay.Measures == 0,
            "Card floating action does not contribute to intrinsic size or execute measurement during primary measurement"));
        Frame(card.Render);
        check(overlay.Bounds == new UiRect(4, 4, 56, 36) && primary.Bounds == new UiRect(12, 12, 40, 20),
            "Card floating action uses the source4px body inset and its own bounded allocation");
        check(ReferenceEquals(card.Child, primary) && ReferenceEquals(card.FloatingAction, overlay),
            "Card exposes original owned content and a distinct floating action");
        var measurements = primary.Measures + overlay.Measures;
        Frame(card.Render);
        check(primary.Measures + overlay.Measures == measurements, "Card floating action reuses stable owned-child measurement");
        overlay.Resize(new(20, 20));
        Frame(card.Render);
        check(overlay.Bounds == new UiRect(40, 4, 20, 20), "Card floating child invalidation updates its top-right allocation without growing the body");
        card.Options = card.Options with { Width = 6, Height = 6 };
        Frame(card.Render);
        check(overlay.Bounds.Width == 0 && overlay.Bounds.Height == 0, "Card floating action receives an empty allocation when body insets consume all space");
        using var duplicate = new Probe(new(10, 10));
        check(Throws<ArgumentException>(() => _ = Fluent.Card("Duplicate", duplicate, floatingAction: duplicate)),
            "Card rejects sharing its content and floating slot before either is adopted");
        using var duplicateOwner = Fluent.Card("Recovered", duplicate);
        using var available = new Probe(new(10, 10));
        check(Throws<InvalidOperationException>(() => _ = Fluent.Card("Invalid owner", available, floatingAction: overlay)),
            "Card preflights an already-owned floating slot before adopting otherwise valid content");
        using var availableOwner = Fluent.Card("Recovered content", available);
        using var custom = new UiCard<LargeCorner>("Custom", new Probe(new(20, 20)), new(Width: 160, Height: 120),
            floatingAction: new Probe(new(20, 20)));
        Frame(custom.Render);
        check(custom.FloatingAction is Probe { Bounds: { X: 136, Y: 4, Width: 20, Height: 20 } },
            "Card preserves the actual4px floating allocation under a custom rounded clip without changing metrics");
        card.Dispose();
        Frame(ui => check(Throws<ObjectDisposedException>(() => overlay.Render(ui)), "Card disposal releases its actual floating child"));
    }

    private static void RunInput(UiFocusGroupMode mode, Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        var selected = false;
        var values = new List<bool>();
        var roots = 0;
        var children = 0;
        var menuCalls = 0;
        var text = "original";
        var binding = new UiBinding<bool>(() => selected, value => { selected = value; values.Add(value); });
        var primary = Fluent.Button("Content action", () => children++);
        primary.Size = new(200, 80);
        var floating = Fluent.Checkbox("Float selection", binding);
        using var card = Fluent.Card("Floating card", Dux.Column(new(Spacing: 8, MainAxisSize: UiMainAxisSize.Min),
            primary, Fluent.Input("Content input", new UiBinding<string>(() => text, value => text = value), 100, 180),
            Fluent.Menu("Main menu", Enumerable.Range(0, 8).Select(i => new UiMenuOption(i.ToString(), "Overlapping popup action " + i)).ToArray(), _ => menuCalls++)),
            new(Width: 240, Height: 220, Appearance: UiCardAppearance.Outline, FocusMode: mode),
            activated: () => roots++, selection: binding, floatingAction: floating);
        using var before = Fluent.Button("Before floating card");
        using var after = Fluent.Button("After floating card");
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(UiInputState? input = null, bool show = true)
        {
            context.NewFrame(new(1f / 60, new(440, 320), new(1, 1)), input ?? idle, new(0, 0, 440, 320), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding: 0); before.Render(ui); if (show) card.Render(ui); after.Render(ui); }));
            using var data = context.GetDrawData();
        }
        List<UiAccessibilityElement> All()
        {
            var items = new List<UiAccessibilityElement>();
            void Visit(string? id)
            {
                while (id is not null && tree.TryGet(id, out var item))
                { items.Add(item); Visit(item.FirstChild); id = item.Next; }
            }
            Visit(tree.Root.First);
            return items;
        }
        UiAccessibilityElement Find(string name, UiAccessibilityRole? role = null) => All().Single(item => item.Node.Name == name && (role is null || item.Node.Role == role));
        static UiVector2 Center(UiRect b) => new(b.X + b.Width / 2, b.Y + b.Height / 2);
        void Pointer(UiVector2 p, bool down) => Frame(idle with { MousePosition = p, LeftMouseDown = down, LeftMousePressed = down, LeftMouseReleased = !down });
        void Click(UiVector2 p) { Pointer(p, true); Pointer(p, false); }
        void Key(UiKey key) { Frame(idle with { KeyEvents = [new(key, true, default)] }); Frame(idle with { KeyEvents = [new(key, false, default)] }); }
        void Focus(UiAccessibilityElement item) { tree.RequestAction(item.Node.Id, item.RuntimeId, UiAccessibilityAction.Focus, wait: false); Frame(); }
        Frame();
        var root = Find("Floating card", UiAccessibilityRole.Card);
        var action = Find("Float selection", UiAccessibilityRole.Checkbox);
        check(!root.Node.Focusable && action.Node.ParentId == root.Node.Id
            && All().Count(item => item.Node.Role == UiAccessibilityRole.Checkbox) == 1,
            "Card floating action replaces the hidden selection proxy with one actual checkbox");
        check(tree.TryGet(root.FirstChild!, out var firstChild) && firstChild.Node.Id == action.Node.Id,
            "Card native source order places floating action before primary content");
        check(MathF.Abs(action.Node.Bounds.X + action.Node.Bounds.Width - root.Node.Bounds.X - root.Node.Bounds.Width + 4) < .001f
            && action.Node.Bounds.Y == root.Node.Bounds.Y + 4,
            "Card real floating control occupies the pinned top-right inset");
        Click(Center(action.Node.Bounds));
        check(selected && values.Count == 1 && roots == 0 && children == 0,
            "overlapping floating checkbox writes once without activating obscured content or Card");
        Focus(Find("Before floating card"));
        foreach (var name in new[] { "Float selection", "Content action", "Content input", "Main menu", "After floating card" })
        {
            Key(UiKey.Tab);
            check(context.State.FocusedId == Find(name).Node.Id, "Card floating-first Tab order reaches actual control : " + name);
        }
        Focus(Find("Float selection"));
        Key(UiKey.Space);
        check(!selected && values.Count == 2 && roots == 0, "floating checkbox Space changes its real binding without a duplicate root write");
        root = Find("Floating card", UiAccessibilityRole.Card);
        Click(new(root.Node.Bounds.X + root.Node.Bounds.Width - 10, root.Node.Bounds.Y + root.Node.Bounds.Height - 10));
        check(selected && values.Count == 3 && roots == 1, "Card background selection still commits once with a floating action");
        var button = Find("Content action");
        Click(new(button.Node.Bounds.X + 5, button.Node.Bounds.Y + 60));
        check(children == 1 && roots == 1 && values.Count == 3, "unobscured primary content remains independent of the floating slot");
        Focus(Find("Content input"));
        Frame(idle with { CharEvents = [new('x')] });
        check(text.Contains('x') && values.Count == 3, "floating Card preserves actual child text editing");
        Click(Center(Find("Main menu").Node.Bounds));
        Frame();
        var overlayBounds = Find("Float selection").Node.Bounds;
        var overlappingItem = All().Where(item => item.Node.Role == UiAccessibilityRole.MenuItem).Select(item => item.Node.Bounds)
            .Where(b => b.X < overlayBounds.X + overlayBounds.Width && b.X + b.Width > overlayBounds.X
                && b.Y < overlayBounds.Y + overlayBounds.Height && b.Y + b.Height > overlayBounds.Y).First();
        var intersection = new UiRect(MathF.Max(overlappingItem.X, overlayBounds.X), MathF.Max(overlappingItem.Y, overlayBounds.Y),
            MathF.Min(overlappingItem.X + overlappingItem.Width, overlayBounds.X + overlayBounds.Width) - MathF.Max(overlappingItem.X, overlayBounds.X),
            MathF.Min(overlappingItem.Y + overlappingItem.Height, overlayBounds.Y + overlayBounds.Height) - MathF.Max(overlappingItem.Y, overlayBounds.Y));
        Click(Center(intersection));
        check(menuCalls == 1 && values.Count == 3 && roots == 1,
            "primary content popup stays interactive above a floating slot at the same pointer position");
        Frame();
        root = Find("Floating card", UiAccessibilityRole.Card);
        Pointer(new(root.Node.Bounds.X + root.Node.Bounds.Width - 10, root.Node.Bounds.Y + root.Node.Bounds.Height - 10), true);
        Frame(idle with { LeftMouseDown = true }, show: false);
        check(context.State.ActiveId is null, "omitting a floating Card releases a held background capture");
        Frame();
        Pointer(Center(Find("Float selection").Node.Bounds), true);
        Frame(idle with { LeftMouseDown = true }, show: false);
        check(context.State.ActiveId is null, "omitting a floating Card releases a held actual checkbox capture");
        Frame();
        card.Selection = null;
        card.Options = card.Options with { FocusMode = UiFocusGroupMode.NoTab };
        Frame();
        Focus(Find("Floating card", UiAccessibilityRole.Card));
        Key(UiKey.Enter);
        check(context.State.FocusedId == Find("Float selection").Node.Id,
            "interactive Card Enter uses final floating-first focus-group anchors");
        Key(UiKey.Tab);
        check(context.State.FocusedId == Find("Content action").Node.Id, "Card focus group preserves floating-to-content Tab order");
        Key(UiKey.Escape);
        check(context.State.FocusedId == Find("Floating card", UiAccessibilityRole.Card).Node.Id, "Card Escape restores the group after floating-first navigation");
        card.Selection = binding;
        card.Options = card.Options with { Disabled = true };
        Frame();
        action = Find("Float selection");
        var writesBefore = values.Count;
        check(tree.RequestAction(action.Node.Id, action.RuntimeId, UiAccessibilityAction.Toggle, wait: false) == UiAccessibilityResult.Success,
            "root-only disabled Card leaves caller-owned floating checkbox enabled");
        Frame();
        check(values.Count == writesBefore + 1, "disabled Card floating Toggle reaches its binding exactly once");
        card.Options = card.Options with { Disabled = false };
        Frame();
        var initial = selected;
        writesBefore = values.Count;
        var rootsBefore = roots;
        root = Find("Floating card", UiAccessibilityRole.Card);
        action = Find("Float selection");
        tree.RequestAction(root.Node.Id, root.RuntimeId, UiAccessibilityAction.Invoke, wait: false);
        tree.RequestAction(action.Node.Id, action.RuntimeId, UiAccessibilityAction.Toggle, wait: false);
        Frame(); Frame();
        check(values.Count == writesBefore + 2 && values[^2] == !initial && values[^1] == initial && selected == initial && roots == rootsBefore + 1,
            "queued floating Toggle and root Invoke commit in child-then-root order using the latest model");
        primary.Activated = () => card.Selection = null;
        Frame();
        button = Find("Content action");
        var point = new UiVector2(button.Node.Bounds.X + 5, button.Node.Bounds.Y + 60);
        Pointer(point, true);
        root = Find("Floating card", UiAccessibilityRole.Card);
        tree.RequestAction(root.Node.Id, root.RuntimeId, UiAccessibilityAction.Invoke, wait: false);
        writesBefore = values.Count;
        Pointer(point, false);
        check(card.Selection is null && values.Count == writesBefore + 1,
            "Card commits the captured frame binding when an actual child removes selection during root Invoke");
    }

    private readonly struct LargeCorner : IUiCardPresentation
    {
        public static UiCardMetrics Measure(UiCardOptions options) => new(20, 20);
        public static void Paint(UiControlContext context, UiRect bounds, UiCardOptions options, UiCardMetrics metrics, UiButtonInteraction interaction)
            => context.Frame(bounds, new(255, 0, 0), default, 20, 0);
    }
    private sealed class Probe(UiVector2 size) : UiControlWidget
    {
        private UiVector2 _size = size;
        internal UiRect Bounds;
        internal int Measures;
        internal void Resize(UiVector2 value) { _size = value; RequestRebuild(); }
        protected override bool CanRetainMeasurement => true;
        protected override UiVector2 MeasureContent(UiControlContext context) { Measures++; return context.LayoutConstraints.Constrain(_size); }
        protected override void RenderControl(UiImmediateContext ui)
        {
            var actual = new UiControlContext(ui).ConstrainToLayout(_size);
            var origin = ui.GetCursorScreenPos();
            Bounds = new(origin.X, origin.Y, actual.X, actual.Y);
            ui.Dummy(actual);
        }
    }
    private sealed class Screen(Action<UiImmediateContext> draw) : UiScreen
    { public override void Render(UiImmediateContext ui) => draw(ui); }
}
