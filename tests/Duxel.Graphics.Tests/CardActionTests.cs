using Duxel.Core;

internal static class CardActionTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        var roots = 0;
        var children = 0;
        using var card = Fluent.Card("Action card", Fluent.Button("Owned action", () => children++),
            new(Width: 240, Height: 100), activated: () => roots++);
        UiWidget current = card;
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(UiInputState? input = null)
        {
            context.NewFrame(new(1f / 60, new(440, 360), new(1, 1)), input ?? idle, new(0, 0, 440, 360), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding: 0); current.Render(ui); }));
            using var draw = context.GetDrawData();
        }
        UiAccessibilityElement Find(string name)
        {
            UiAccessibilityElement? found = null;
            void Visit(string? id)
            {
                while (id is not null && tree.TryGet(id, out var item))
                { if (item.Node.Name == name) found = item; Visit(item.FirstChild); id = item.Next; }
            }
            Visit(tree.Root.First); return found ?? throw new InvalidOperationException("Action Card fixture missing : " + name);
        }
        void Pointer(UiVector2 point, bool press)
            => Frame(idle with { MousePosition = point, LeftMouseDown = press, LeftMousePressed = press, LeftMouseReleased = !press });
        void Click(UiVector2 point) { Pointer(point, true); Pointer(point, false); }
        Frame();
        var body = Find("Action card").Node.Bounds;
        var background = new UiVector2(body.X + body.Width - 5, body.Y + body.Height - 5);
        var child = Find("Owned action").Node.Bounds;
        var childPoint = new UiVector2(child.X + child.Width / 2, child.Y + child.Height / 2);
        check(Find("Action card").Node.Focusable && Find("Action card").Node.SupportsInvoke,
            "Card callback supplies automatic no-tab focus and a native Invoke capability");
        Frame(idle with { MousePosition = background });
        check(Find("Action card").Node.Bounds == body, "interactive Card hover keeps its reserved shadow body bounds stable");
        Click(background);
        check(roots == 1 && children == 0, "Card background activates its actual root callback once on release");
        Click(childPoint);
        check(roots == 1 && children == 1, "Card owned button executes without bubbling root activation");
        Pointer(background, true); Pointer(childPoint, false);
        check(roots == 1 && children == 1 && context.State.ActiveId is null,
            "Card background drag release over its child cancels root capture without activating either action");
        Pointer(childPoint, true); Pointer(background, false);
        check(roots == 1 && children == 1 && context.State.ActiveId is null,
            "Card child drag release over the background cannot activate the ancestor");
        var root = Find("Action card");
        check(tree.RequestAction(root.Node.Id, root.RuntimeId, UiAccessibilityAction.Invoke, wait: false) == UiAccessibilityResult.Success,
            "Card action accepts the common accessibility Invoke request");
        Frame();
        check(roots == 2 && children == 1, "Card accessibility Invoke reaches only its real root callback");
        root = Find("Action card");
        tree.RequestAction(root.Node.Id, root.RuntimeId, UiAccessibilityAction.Focus, wait: false); Frame();
        Frame(idle with { KeyEvents = [new(UiKey.Enter, true, default)] });
        check(context.State.FocusedId == Find("Owned action").Node.Id && roots == 2 && children == 1,
            "interactive Card Enter enters its first child without activating root or child");
        card.Options = card.Options with { FocusMode = UiFocusGroupMode.Off };
        Frame(); Click(childPoint);
        check(!Find("Action card").Node.Focusable && roots == 2 && children == 2,
            "explicit Off overrides automatic Card focus while keeping child pointer ownership");
        card.Options = card.Options with { Disabled = true };
        Frame(); Click(background); Click(childPoint);
        root = Find("Action card");
        check(roots == 2 && children == 3 && !root.Node.Enabled,
            "disabled interactive Card blocks root click while retaining child actions");
        check(tree.RequestAction(root.Node.Id, root.RuntimeId, UiAccessibilityAction.Invoke, wait: false) == UiAccessibilityResult.Disabled,
            "disabled Card rejects native root Invoke before queueing");
        card.Options = card.Options with { Disabled = false, FocusMode = null };
        card.Activated = null;
        Frame();
        check(!Find("Action card").Node.SupportsInvoke && !Find("Action card").Node.Focusable,
            "removing Card callback restores automatic passive focus and withdraws Invoke");

        var selected = new UiState<bool>(false);
        var surfaceCalls = 0;
        var childCalls = 0;
        var floatingCalls = 0;
        using var press = new UiCard<UiPressButtonBehavior, UiFluentCardPresentation>("Press card",
            Fluent.Button("Press child", () => childCalls++), new(Width: 240, Height: 100),
            activated: () => surfaceCalls++, selection: selected.Binding,
            floatingAction: Fluent.Button("Press floating", () =>
            {
                floatingCalls++;
                selected.Value = !selected.Value;
            }));
        current = press;
        Frame();
        UiVector2 Center(string name)
        {
            var bounds = Find(name).Node.Bounds;
            return new(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
        }
        void QuickClick(UiVector2 point)
            => Frame(idle with { MousePosition = point, LeftMousePressed = true, LeftMouseReleased = true });
        var pressBody = Find("Press card").Node.Bounds;
        var pressSurface = new UiVector2(pressBody.X + pressBody.Width - 8, pressBody.Y + pressBody.Height - 8);
        var pressChild = Center("Press child");
        Pointer(pressSurface, true);
        check(surfaceCalls == 1 && selected.Value, "Press Card activates and writes selection on the down edge");
        Frame(idle with { MousePosition = pressSurface, LeftMouseDown = true });
        Pointer(pressChild, false);
        check(surfaceCalls == 1 && selected.Value && childCalls == 0 && context.State.ActiveId is null,
            "Press Card hold and release over a child do not activate either command again");
        Pointer(pressChild, true);
        check(surfaceCalls == 1 && childCalls == 0 && selected.Value,
            "Press Card does not claim its release child action on the down edge");
        Pointer(pressChild, false);
        QuickClick(pressChild);
        check(surfaceCalls == 1 && childCalls == 2 && selected.Value,
            "Press Card leaves separate-edge and same-frame clicks with the owned button");
        Click(Center("Press floating"));
        check(surfaceCalls == 1 && floatingCalls == 1 && !selected.Value,
            "Press Card floating action keeps pointer ownership and its selection write");
        QuickClick(pressSurface);
        check(surfaceCalls == 2 && selected.Value, "Press Card activates once when both pointer edges arrive in one frame");
        var pressRoot = Find("Press card");
        check(tree.RequestAction(pressRoot.Node.Id, pressRoot.RuntimeId, UiAccessibilityAction.Invoke, wait: false) == UiAccessibilityResult.Success,
            "Press Card still accepts accessibility Invoke");
        Frame();
        check(surfaceCalls == 3 && !selected.Value, "Press Card accessibility Invoke still reaches the root and selection binding");
        press.Options = press.Options with { Disabled = true };
        Frame();
        Click(pressSurface);
        Click(Center("Press floating"));
        check(surfaceCalls == 3 && floatingCalls == 2 && selected.Value,
            "Disabled Press Card blocks the surface and preserves its independent floating action");
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    { public override void Render(UiImmediateContext ui) => render(ui); }
}
