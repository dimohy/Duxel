using Duxel.Core;
using Duxel.Validation;

internal static class DialogAccessibilityContractTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        using var scene = new DialogAccessibilityScreen();
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(UiKey? key = null)
        {
            context.NewFrame(new(1f / 60, new(900, 600), new(1, 1)), key is { } value
                ? idle with
                {
                    KeyEvents = [new(value, true, 0), new(value, false, 0)]
                } : idle,
                new(0, 0, 900, 600), UiTextSettings.Default);
            scene.Frame++;
            context.Render(scene);
            using var data = context.GetDrawData();
        }
        UiAccessibilityElement Find(string name) => tree.CaptureSnapshot().Elements.Single(x => x.Node.Name == name);
        void Request(string name, UiAccessibilityAction action)
        {
            var item = Find(name);
            check(tree.RequestAction(item.Node.Id, item.RuntimeId, action, wait: false) == UiAccessibilityResult.Success,
                "Dialog queues actual accessibility action : " + name + "/" + action);
            Frame();
            Frame();
        }
        Frame();
        Request("Finish Dialog", UiAccessibilityAction.Focus);
        check(Find("Finish Dialog").Node.Focused, "Dialog accessibility precondition focuses a different control");
        Request("Open modal", UiAccessibilityAction.Invoke);
        check(Find("Dialog name").Node.Focused, "Accessibility invocation autofocuses the real Dialog editor");
        Frame(UiKey.Tab);
        Frame(UiKey.Tab);
        Frame(UiKey.Tab);
        check(Find("Dialog name").Node.Focused, "Accessibility-opened modal Tab wraps to its editor");
        Frame(UiKey.Escape);
        for (var i = 0; i < 20; i++)
            Frame();
        check(!scene.Dialog.IsOpen && !tree.CaptureSnapshot().Elements.Any(x => x.Node.Name == "Dialog name"),
            "Accessibility-opened Dialog Escape retires the actual tree");
        var focus = string.Join(",", tree.CaptureSnapshot().Elements.Where(x => x.Node.Focused).Select(x => x.Node.Name));
        check(Find("Open modal").Node.Focused,
            "Accessibility-opened Dialog restores the actual invoking opener; observed focus=" + focus);
        Request("Finish Dialog", UiAccessibilityAction.Invoke);
        check(scene.Finished && Find("Open modal").Node.Focused,
            "Ordinary accessibility invocation executes without stealing another control's focus");
        Request("Finish Dialog", UiAccessibilityAction.Focus);
        scene.Dialog.Show();
        Frame();
        Frame();
        Frame(UiKey.Escape);
        for (var i = 0; i < 20; i++)
            Frame();
        check(Find("Finish Dialog").Node.Focused,
            "Programmatic Dialog opening restores prior focus without leaking an earlier callback origin");
        var (pending, retiredContext) = CreateUnmountedDialog();
        try
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            check(!retiredContext.TryGetTarget(out _),
                "An activated but unmounted Dialog does not retain its disposed UI context");
        }
        finally { pending.Dispose(); }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static (UiFluentDialog, WeakReference<UiImmediateContext>) CreateUnmountedDialog()
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        var pending = Fluent.Dialog("Not mounted", Fluent.Body1("Pending content"));
        using var button = Fluent.Button("Open unmounted", pending.Show);
        WeakReference<UiImmediateContext>? observed = null;
        var view = new PendingScreen(ui =>
        {
            observed = new(ui);
            button.Render(ui);
        });
        void Frame()
        {
            var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
            context.NewFrame(new(1f / 60, new(900, 600), new(1, 1)), idle, new(0, 0, 900, 600), UiTextSettings.Default);
            context.Render(view);
            using var data = context.GetDrawData();
        }
        Frame();
        var opener = tree.CaptureSnapshot().Elements.Single(x => x.Node.Name == "Open unmounted");
        if (tree.RequestAction(opener.Node.Id, opener.RuntimeId, UiAccessibilityAction.Invoke, wait: false) != UiAccessibilityResult.Success)
            throw new InvalidOperationException("The unmounted Dialog activation was not accepted.");
        Frame();
        return (pending, observed ?? throw new InvalidOperationException("The UI context was not observed."));
    }

    private sealed class PendingScreen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
