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
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(UiInputState? input = null)
        {
            context.NewFrame(new(1f / 60, new(440, 360), new(1, 1)), input ?? idle, new(0, 0, 440, 360), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding: 0); card.Render(ui); }));
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
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    { public override void Render(UiImmediateContext ui) => render(ui); }
}
