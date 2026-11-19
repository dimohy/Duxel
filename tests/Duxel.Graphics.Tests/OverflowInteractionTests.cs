using Duxel.Core;

internal static class OverflowInteractionTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        using var tree = new UiAccessibilityTree();
        ControlFontFixture.Configure(context);
        context.SetAccessibilityTree(tree);
        var clicks = 0;
        var first = Fluent.Button("One : 0");
        first.Activated = () => first.Label = $"One : {++clicks}";
        first.Size = new(60, 24);
        var second = Fluent.Button("Two");
        second.Size = new(60, 24);
        var indicator = Fluent.Button("More");
        indicator.Size = new(30, 24);
        var overflow = Dux.Overflow([first, second], indicator, new(Spacing : 8));
        using var root = Dux.SizedBox(overflow, width : 160, height : 40);
        var screen = new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding : 0); root.Render(ui); });
        void Frame()
        {
            context.NewFrame(new(1f / 60, new(640, 400), new(1, 1)),
                new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default),
                new(0, 0, 640, 400), UiTextSettings.Default);
            context.Render(screen);
            using var draw = context.GetDrawData();
        }
        Frame();
        tree.TryGet(tree.Root.First!, out var old);
        tree.RequestAction(old.Node.Id, old.RuntimeId, UiAccessibilityAction.Invoke, wait : false);
        Frame();
        check(clicks == 1 && first.Label == "One : 1", "a visible overflow child executes its actual bound callback");
        tree.RequestAction(old.Node.Id, old.RuntimeId, UiAccessibilityAction.Focus, wait : false);
        Frame();
        check(tree.Root.Focus == old.Node.Id, "a visible overflow child can acquire actual logical focus");
        root.Options = root.Options with { Width = 110 };
        overflow.SetPriorities([0, 2]);
        tree.RequestAction(old.Node.Id, old.RuntimeId, UiAccessibilityAction.Invoke, wait : false);
        Frame();
        check(!tree.TryGet(old.Node.Id, out _) && tree.Root.Focus is null && clicks == 1,
            "hiding a child removes its semantics, clears focus and does not execute a queued hidden activation");
        check(tree.RequestAction(old.Node.Id, old.RuntimeId, UiAccessibilityAction.Invoke, wait : false) == UiAccessibilityResult.Unavailable,
            "a hidden overflow child's retired identity rejects later actions");
        root.Options = root.Options with { Width = 160 };
        Frame();
        tree.TryGet(old.Node.Id, out var restored);
        check(restored.Node.Name == "One : 1" && restored.RuntimeId != old.RuntimeId && ReferenceEquals(overflow.Items[0], first),
            "restoring overflow reuses the same widget/model with a fresh live accessibility identity");
        tree.RequestAction(restored.Node.Id, restored.RuntimeId, UiAccessibilityAction.Invoke, wait : false);
        Frame();
        check(clicks == 2, "restored overflow children remain callable through their current identity");
    }

    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
