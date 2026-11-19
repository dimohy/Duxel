using Duxel.Core;

internal static class CardClippingTests
{
    internal static void Run(Action<bool, string> check)
    {
        foreach (var mode in Enum.GetValues<UiFocusGroupMode>()) RunControls(mode, check);
        RunSelection(check);
        RunProxyFallthrough(check);
    }
    private static void RunControls(UiFocusGroupMode mode, Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        var childCalls = 0; var innerCalls = 0; var outerCalls = 0;
        var button = Fluent.Button("Rounded child", () => childCalls++);
        button.Size = new(120, 100);
        var inner = new UiCard<Square>("Inner square", button, new(Width: 120, Height: 100, FocusMode: UiFocusGroupMode.NoTab),
            activated: () => innerCalls++);
        using var card = new UiCard<Flush>("Rounded card", inner, new(Width: 120, Height: 100, FocusMode: mode),
            activated: () => outerCalls++);
        using var before = Fluent.Button("Before rounded");
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(UiInputState? input = null)
        {
            context.NewFrame(new(1f / 60, new(360, 300), new(1, 1)), input ?? idle, new(0, 0, 360, 300), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding: 0); before.Render(ui); ui.SetCursorScreenPos(new(20, 60)); card.Render(ui); }));
            using var data = context.GetDrawData();
        }
        UiAccessibilityElement Find(string name)
        {
            UiAccessibilityElement? found = null;
            void Visit(string? id)
            {
                while (id is not null && tree.TryGet(id, out var item))
                { if (item.Node.Name == name) found = item; Visit(item.FirstChild); id = item.Next; }
            }
            Visit(tree.Root.First);
            return found ?? throw new InvalidOperationException(name);
        }
        void Click(UiVector2 point)
        {
            Frame(idle with { MousePosition = point, LeftMouseDown = true, LeftMousePressed = true });
            Frame(idle with { MousePosition = point, LeftMouseReleased = true });
        }
        Frame();
        var beforeNode = Find("Before rounded");
        tree.RequestAction(beforeNode.Node.Id, beforeNode.RuntimeId, UiAccessibilityAction.Focus, wait: false); Frame();
        check(context.State.FocusedId == beforeNode.Node.Id, "rounded Card fixture establishes external native focus : " + mode);
        foreach (var point in new[] { new UiVector2(21, 61), new(139, 61), new(21, 159), new(139, 159) }) Click(point);
        check(childCalls == 0 && innerCalls == 0 && outerCalls == 0 && context.State.FocusedId is null,
            "rounded Card corners cannot focus or activate nested child/root groups : " + mode);
        Click(new(80, 110));
        check(childCalls == 1 && innerCalls == 0 && outerCalls == 0 && context.State.FocusedId == Find("Rounded child").Node.Id,
            "rounded Card interior preserves independent actual child pointer action : " + mode);
        var corner = new UiVector2(21, 61);
        var root = Find("Rounded card");
        tree.RequestAction(root.Node.Id, root.RuntimeId, UiAccessibilityAction.Invoke, wait: false); Frame(idle with { MousePosition = corner });
        check(outerCalls == 1 && innerCalls == 0 && childCalls == 1,
            "rounded pointer clipping leaves the native root Invoke capability independent : " + mode);
        var child = Find("Rounded child");
        tree.RequestAction(child.Node.Id, child.RuntimeId, UiAccessibilityAction.Focus, wait: false); Frame();
        Frame(idle with { MousePosition = corner, KeyEvents = [new(UiKey.Space, true, KeyModifiers.None)] });
        Frame(idle with { MousePosition = corner, KeyEvents = [new(UiKey.Space, false, KeyModifiers.None)] });
        check(childCalls == 2 && outerCalls == 1 && innerCalls == 0,
            "rounded clipping preserves keyboard activation while pointer stays outside the visible curve : " + mode);
        Frame(idle with { MousePosition = new(80, 110), LeftMouseDown = true, LeftMousePressed = true });
        Frame(idle with { MousePosition = corner, LeftMouseReleased = true });
        check(childCalls == 2 && context.State.ActiveId is null,
            "drag release into a clipped Card corner cancels its actual child capture : " + mode);
    }
    private static void RunSelection(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree(); context.SetAccessibilityTree(tree);
        var selected = false; var writes = 0; var calls = 0;
        var binding = new UiBinding<bool>(() => selected, value => { selected = value; writes++; });
        var checkbox = Fluent.Checkbox("##rounded-selection", binding); checkbox.AccessibleName = "Rounded selection";
        using var card = new UiCard<Flush>("Rounded selectable", new Empty(), new(Width: 120, Height: 100),
            activated: () => calls++, selection: binding, floatingAction: checkbox);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(UiInputState? input = null)
        {
            context.NewFrame(new(1f / 60, new(320, 240), new(1, 1)), input ?? idle, new(0, 0, 320, 240), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding: 0); ui.SetCursorScreenPos(new(20, 60)); card.Render(ui); }));
            using var data = context.GetDrawData();
        }
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
        void Pointer(UiVector2 point, bool down)
            => Frame(idle with { MousePosition = point, LeftMouseDown = down, LeftMousePressed = down, LeftMouseReleased = !down });
        void Click(UiVector2 point) { Pointer(point, true); Pointer(point, false); }
        Frame();
        var floating = Find("Rounded selection"); var rect = floating.Node.Bounds;
        var clippedCorner = new UiVector2(rect.X + rect.Width - 1, rect.Y + 1);
        Click(clippedCorner);
        check(!selected && writes == 0 && calls == 0, "rounded Card clips the actual floating checkbox pointer area at its curved corner");
        Click(new(rect.X + rect.Width / 2, rect.Y + rect.Height / 2));
        check(selected && writes == 1 && calls == 0, "visible floating checkbox portion changes selection once without root invocation");
        Click(new(80, 110));
        check(!selected && writes == 2 && calls == 1, "visible rounded Card background keeps one bound selection and root invocation");
        var root = Find("Rounded selectable");
        tree.RequestAction(root.Node.Id, root.RuntimeId, UiAccessibilityAction.Invoke, wait: false); Frame(idle with { MousePosition = clippedCorner });
        floating = Find("Rounded selection");
        tree.RequestAction(floating.Node.Id, floating.RuntimeId, UiAccessibilityAction.Toggle, wait: false); Frame(idle with { MousePosition = clippedCorner });
        check(!selected && writes == 4 && calls == 2, "curved pointer clip preserves independent actual Group Invoke and Checkbox Toggle semantics");
        Pointer(new(80, 110), true); Pointer(clippedCorner, false);
        check(writes == 4 && calls == 2 && context.State.ActiveId is null, "root drag release outside rounded Card cancels background selection capture");
        Pointer(clippedCorner, true); Pointer(new(80, 110), false);
        check(writes == 4 && calls == 2, "press outside a rounded Card cannot acquire root activation on release inside");
    }
    private static void RunProxyFallthrough(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        var writes = 0; var calls = 0;
        var inner = new UiCard<Flush>("Inner selected curve", new Empty(), new(Width: 120, Height: 100),
            selection: new UiBinding<bool>(() => false, _ => writes++));
        using var outer = new UiCard<Square>("Parent background", inner, new(Width: 160, Height: 140), activated: () => calls++);
        var corner = new UiVector2(21, 61);
        void Frame(bool down, bool pressed = false, bool released = false)
        {
            context.NewFrame(new(1f / 60, new(360, 300), new(1, 1)),
                new(corner, down, false, pressed, released, false, false, 0, 0, [], [], default, default), new(0, 0, 360, 300), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding: 0); ui.SetCursorScreenPos(new(20, 60)); outer.Render(ui); }));
            using var data = context.GetDrawData();
        }
        Frame(false); Frame(true, pressed: true); Frame(false, released: true);
        check(calls == 1 && writes == 0 && context.State.ActiveId is null,
            "a clipped hidden selection proxy cannot steal its visible ancestor Card background click");
    }
    private readonly struct Flush : IUiCardPresentation
    {
        public static UiCardMetrics Measure(UiCardOptions options) => new(0, 32);
        public static void Paint(UiControlContext context, UiRect bounds, UiCardOptions options, UiCardMetrics metrics, UiButtonInteraction interaction) { }
    }
    private readonly struct Square : IUiCardPresentation
    {
        public static UiCardMetrics Measure(UiCardOptions options) => new(0, 0);
        public static void Paint(UiControlContext context, UiRect bounds, UiCardOptions options, UiCardMetrics metrics, UiButtonInteraction interaction) { }
    }
    private sealed class Empty : UiControlWidget
    {
        protected override UiVector2 MeasureContent(UiControlContext context) => context.LayoutConstraints.Constrain(new(20, 20));
        protected override void RenderControl(UiImmediateContext ui) => ui.Dummy(new UiControlContext(ui).ConstrainToLayout(new(20, 20)));
    }
    private sealed class Screen(Action<UiImmediateContext> draw) : UiScreen { public override void Render(UiImmediateContext ui) => draw(ui); }
}
