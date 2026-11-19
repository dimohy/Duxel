using Duxel.Core;

internal static class CardSelectionTests
{
    internal static void Run(Action<bool, string> check)
    {
        foreach (var mode in Enum.GetValues<UiFocusGroupMode>()) RunMode(mode, check);
        RunDerivedLayout(check);
    }

    private static void RunMode(UiFocusGroupMode mode, Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        var selected = false;
        var writes = 0;
        var rootCalls = 0;
        var childCalls = 0;
        var text = "original";
        var binding = new UiBinding<bool>(() => selected, value => { selected = value; writes++; });
        var menu = Fluent.Menu("Child menu", [new("execute", "Execute item")], _ => childCalls++);
        using var card = Fluent.Card("Selection card", Dux.Column(new(Spacing: 8, MainAxisSize: UiMainAxisSize.Min),
            Fluent.Button("Child action", () => childCalls++),
            Fluent.Input("Child input", new UiBinding<string>(() => text, value => text = value), 100, 180), menu),
            new(Width: 260, FocusMode: mode), activated: () => rootCalls++, selection: binding);
        using var before = Fluent.Button("Before");
        using var after = Fluent.Button("After");
        var changes = new List<UiAccessibilityToggleChange>();
        tree.ToggleChanged += change => changes.Add(change);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(UiInputState? input = null, bool show = true)
        {
            context.NewFrame(new(1f / 60, new(440, 520), new(1, 1)), input ?? idle, new(0, 0, 440, 520), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding: 0); before.Render(ui); if (show) card.Render(ui); after.Render(ui); }));
            using var draw = context.GetDrawData();
        }
        UiAccessibilityElement Find(string name, UiAccessibilityRole? role = null)
        {
            UiAccessibilityElement? found = null;
            void Visit(string? id)
            {
                while (id is not null && tree.TryGet(id, out var item))
                { if (item.Node.Name == name && (role is null || item.Node.Role == role)) found = item; Visit(item.FirstChild); id = item.Next; }
            }
            Visit(tree.Root.First); return found ?? throw new InvalidOperationException("Selection fixture missing : " + name);
        }
        void Focus(string name, UiAccessibilityRole? role = null)
        {
            var node = Find(name, role);
            if (tree.RequestAction(node.Node.Id, node.RuntimeId, UiAccessibilityAction.Focus, wait: false) != UiAccessibilityResult.Success)
                throw new InvalidOperationException("Selection fixture focus failed : " + name);
            Frame();
        }
        void Key(UiKey key)
        {
            Frame(idle with { KeyEvents = [new(key, true, default)] });
            Frame(idle with { KeyEvents = [new(key, false, default)] });
        }
        UiVector2 Center(string name)
        {
            var b = Find(name).Node.Bounds; return new(b.X + b.Width / 2, b.Y + b.Height / 2);
        }
        void Press(UiVector2 p) => Frame(idle with { MousePosition = p, LeftMouseDown = true, LeftMousePressed = true });
        void Release(UiVector2 p) => Frame(idle with { MousePosition = p, LeftMouseReleased = true });
        void Click(UiVector2 p) { Press(p); Release(p); Frame(); }
        void Verify(bool condition, string message) => check(condition, message + " : " + mode);
        Frame();
        var group = Find("Selection card", UiAccessibilityRole.Card);
        var checkbox = Find("Selection card", UiAccessibilityRole.Checkbox);
        Verify(!group.Node.Focusable && checkbox.Node.Focusable && checkbox.Node.ParentId == group.Node.Id
            && checkbox.Node.ToggleState == UiAccessibilityToggleState.Off && group.FirstChild == checkbox.Node.Id,
            "selectable Card exposes the actual first child checkbox rather than a focus-group root");
        Focus("Before"); Key(UiKey.Tab);
        Verify(context.State.FocusedId == checkbox.Node.Id, "outer Tab enters the Card checkbox for every requested focus mode");
        Key(UiKey.Tab); Key(UiKey.Enter);
        Verify(context.State.FocusedId == Find("Child action").Node.Id && childCalls == 1 && writes == 0 && rootCalls == 0,
            "selectable Card child Enter invokes only the real child");
        Focus("Selection card", UiAccessibilityRole.Checkbox);
        changes.Clear();
        Frame(idle with { KeyEvents = [new(UiKey.Space, true, default)] });
        Verify(!selected && writes == 0, "Card checkbox Space waits for release");
        Frame(idle with { KeyEvents = [new(UiKey.Space, false, default)] });
        Verify(selected && writes == 1 && rootCalls == 0 && changes.Count == 1
            && Find("Selection card", UiAccessibilityRole.Checkbox).Node.ToggleState == UiAccessibilityToggleState.On,
            "Card checkbox Space commits one binding write and one published Toggle change");
        Key(UiKey.Enter);
        Verify(!selected && writes == 2 && rootCalls == 0, "Card checkbox Enter toggles once without invoking the root action");
        var bounds = group.Node.Bounds;
        var background = new UiVector2(bounds.X + bounds.Width - 5, bounds.Y + bounds.Height - 5);
        Click(background);
        Verify(selected && writes == 3 && rootCalls == 1 && context.State.FocusedId == checkbox.Node.Id,
            "Card background release toggles and invokes once while focusing its checkbox");
        Click(Center("Child action"));
        Verify(writes == 3 && rootCalls == 1 && childCalls == 2, "Card child pointer activation cannot also toggle selection");
        Click(Center("Child input"));
        Frame(idle with { KeyEvents = [new(UiKey.End, true, KeyModifiers.Ctrl)], Modifiers = KeyModifiers.Ctrl });
        Frame(idle with { CharEvents = [new('x')] });
        Key(UiKey.Enter);
        Verify(text == "originalx" && writes == 3 && rootCalls == 1 && context.State.FocusedId == Find("Child input").Node.Id,
            "selectable Card preserves actual child typing and Enter without changing root selection");
        Press(background); Release(Center("Child action")); Frame();
        Verify(writes == 3 && rootCalls == 1 && childCalls == 2 && context.State.ActiveId is null,
            "Card selection capture cancels when released over an owned child");
        Focus("Child menu"); Key(UiKey.DownArrow); Key(UiKey.Enter);
        Verify(!menu.IsOpen && childCalls == 3 && writes == 3 && rootCalls == 1,
            "Card selection leaves real menu Enter and popup dismissal with its child");
        checkbox = Find("Selection card", UiAccessibilityRole.Checkbox);
        Verify(tree.RequestAction(checkbox.Node.Id, checkbox.RuntimeId, UiAccessibilityAction.Toggle, wait: false) == UiAccessibilityResult.Success,
            "Card checkbox accepts an actual automation Toggle request");
        Frame();
        Verify(!selected && writes == 4 && rootCalls == 1, "automation Toggle reaches the Card selection binding once");
        selected = true; changes.Clear(); Frame();
        Verify(writes == 4 && changes.Count == 1 && Find("Selection card", UiAccessibilityRole.Checkbox).Node.ToggleState == UiAccessibilityToggleState.On,
            "external selection mutation publishes the actual model state without calling its setter");
        group = Find("Selection card", UiAccessibilityRole.Card);
        Verify(tree.RequestAction(group.Node.Id, group.RuntimeId, UiAccessibilityAction.Invoke, wait: false) == UiAccessibilityResult.Success,
            "selectable Card group accepts its explicit native background Invoke");
        Frame();
        Verify(!selected && writes == 5 && rootCalls == 2,
            "selectable Card group Invoke commits one selection and one root action");
        card.Options = card.Options with { Disabled = true };
        Frame(); Click(background); Click(Center("Child action"));
        checkbox = Find("Selection card", UiAccessibilityRole.Checkbox);
        Verify(!checkbox.Node.Enabled && !checkbox.Node.Focusable && Find("Child action").Node.Enabled
            && childCalls == 4 && writes == 5 && rootCalls == 2,
            "disabled Card blocks root selection while preserving independent child activation");
        Verify(tree.RequestAction(checkbox.Node.Id, checkbox.RuntimeId, UiAccessibilityAction.Toggle, wait: false) == UiAccessibilityResult.Disabled,
            "disabled Card checkbox rejects automation Toggle before queueing");
        card.Options = card.Options with { Disabled = false };
        Frame(); Press(background); Frame(show: false);
        Verify(context.State.FocusedId is null && context.State.ActiveId is null,
            "omitting a pressed selectable Card releases its checkbox focus and pointer capture");
    }
    private static void RunDerivedLayout(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        var selected = false;
        var requests = 0;
        context.SetRequestFrameCallback(() => requests++);
        var probe = new Probe();
        var card = new UiCard<SelectionMetrics>("Derived layout", probe, new(Width: 160),
            selection: new(() => selected, value => selected = value));
        using var parent = Dux.Column(new(MainAxisSize: UiMainAxisSize.Min), card);
        void Frame()
        {
            context.NewFrame(new(1f / 60, new(400, 300), new(1, 1)),
                new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default),
                new(0, 0, 400, 300), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding: 0); parent.Render(ui); }));
            using var draw = context.GetDrawData();
        }
        Frame();
        var original = probe.Bounds;
        var measured = probe.Measures;
        Frame();
        check(probe.Measures == measured, "unchanged custom selectable Card retains its actual owned-child measurement through a parent");
        var requested = requests;
        selected = true;
        Frame();
        check(probe.Bounds == original && requests > requested,
            "external Card selection preserves the current prepared allocation and requests an ancestor relayout frame");
        Frame();
        check(probe.Bounds == original with { X = original.X + 8, Y = original.Y + 8 } && probe.Measures > measured,
            "external selected model change invalidates the retained ancestor and custom Card derived layout inputs");
        measured = probe.Measures;
        Frame();
        check(probe.Measures == measured, "custom Card selected layout becomes retained again after the external model change");
    }

    private readonly struct SelectionMetrics : IUiCardPresentation
    {
        public static bool CanRetainMeasurement => true;
        public static UiCardMetrics Measure(UiCardOptions options) => new(options.IsSelected ? 14 : 6, 2);
        public static void Paint(UiControlContext context, UiRect bounds, UiCardOptions options, UiCardMetrics metrics,
            UiButtonInteraction interaction) => context.Frame(bounds, new(0, 128, 0), default, metrics.CornerRadius, 0);
    }
    private sealed class Probe : UiControlWidget
    {
        internal int Measures;
        internal UiRect Bounds;
        protected override bool CanRetainMeasurement => true;
        protected override UiVector2 MeasureContent(UiControlContext context) { Measures++; return context.LayoutConstraints.Constrain(new(40, 20)); }
        protected override void RenderControl(UiImmediateContext ui)
        {
            var size = new UiControlContext(ui).ConstrainToLayout(new(40, 20));
            var point = ui.GetCursorScreenPos(); Bounds = new(point.X, point.Y, size.X, size.Y); ui.Dummy(size);
        }
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    { public override void Render(UiImmediateContext ui) => render(ui); }
}
