using Duxel.Core;

internal static class ToastModalTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        context.SetDesign(UiCompiledDesign.Windows11Dark); ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree(); context.SetAccessibilityTree(tree);
        var rootController = new UiToastController(); var localController = new UiToastController();
        var rootActions = 0;
        using var rootHost = new UiToasterWidget(rootController, new(Position : UiToastPosition.TopEnd, ReducedMotion : true));
        using var localHost = new UiToasterWidget(localController, new(ReducedMotion : true));
        using var child = Fluent.Dialog("Child modal", Fluent.Button("Child control"), [], new(MotionEnabled : false));
        using var outer = Fluent.Dialog("Outer modal", Dux.Column(new(MainAxisSize : UiMainAxisSize.Min),
            Fluent.Button("Modal control"), Fluent.Button("Open child", child.Show), child, localHost), [], new(MotionEnabled : false));
        using var trigger = Fluent.Button("Open outer", outer.Show);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(UiInputState? input = null)
        {
            context.NewFrame(new(.016f, new(800, 600), new(1, 1)), input ?? idle, new(0, 0, 800, 600), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding : 0); trigger.Render(ui); outer.Render(ui); rootHost.Render(ui); }));
            using var draw = context.GetDrawData();
        }
        UiAccessibilityElement Button(string name) => tree.CaptureSnapshot().Elements.Single(e => e.Node.Role == UiAccessibilityRole.Button && e.Node.Name == name);
        void Invoke(string name)
        {
            var button = Button(name);
            check(tree.RequestAction(button.Node.Id, button.RuntimeId, UiAccessibilityAction.Invoke, wait : false) == UiAccessibilityResult.Success,
                "modal combination accepts actual invoke : " + name);
            Frame(); Frame();
        }
        void Key(UiKey key)
        {
            Frame(idle with { KeyEvents = [new(key, true, default)] });
            Frame(idle with { KeyEvents = [new(key, false, default)] });
        }
        void Click(string name)
        {
            var bounds = Button(name).Node.Bounds;
            var point = new UiVector2(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
            Frame(idle with { MousePosition = point, LeftMouseDown = true, LeftMousePressed = true });
            Frame(idle with { MousePosition = point, LeftMouseReleased = true });
            Frame(); Frame();
        }
        void F6() => Key(UiKey.F6);
        rootController.Dispatch(new("Root notification") { Timeout = null, ActionLabel = "Root action", Action = () => rootActions++ });
        Frame(); Invoke("Open outer"); F6();
        check(!Button("Root action").Node.Focused && Button("Modal control").Node.Focused,
            "root Toast F6 cannot steal focus from a modal dialog");
        Click("Root action");
        check(rootActions == 0 && !outer.IsOpen, "backdrop dismissal does not click through to a root Toast action behind the modal");
        Invoke("Open outer");
        localController.Dispatch(new("Modal notification") { Timeout = null, CanDismiss = true, CloseLabel = "Close local", ActionLabel = "Local action", Action = child.Show });
        Frame(); Frame(); F6();
        check(Button("Local action").Node.Focused, "F6 can enter a Toast owned by the current modal");
        F6(); Frame();
        check(Button("Modal control").Node.Focused, "modal Toast F6 returns to the previous modal control");
        Invoke("Open child"); F6();
        check(Button("Child control").Node.Focused && !Button("Local action").Node.Focused && !Button("Root action").Node.Focused,
            "ancestor Toast shortcuts cannot cross a nested modal focus boundary");
        Key(UiKey.Escape); Frame();
        check(!child.IsOpen && outer.IsOpen && localController.Visible.Count == 1,
            "Escape closes only the focused nested modal and preserves its ancestor Toast and outer dialog");
        Click("Local action");
        check(child.IsOpen && Button("Child control").Node.Focused,
            "a pointer action on a modal-owned Toast opens its child dialog without light-dismissing the owner");
        Key(UiKey.Escape); Frame();
        check(!child.IsOpen && Button("Local action").Node.Focused,
            "closing a Toast-triggered child modal returns focus to that Toast action");
        Key(UiKey.Escape); Frame(); Frame();
        check(outer.IsOpen && localController.Visible.Count == 0 && rootController.Visible.Count == 1,
            "Escape from the focused modal Toast dismisses that host's notifications without closing the dialog or root host");
        localController.Dispatch(new("Pointer dismissal") { Timeout = null, CanDismiss = true, CloseLabel = "Pointer close" });
        Frame(); Frame(); Click("Pointer close");
        check(outer.IsOpen && localController.Visible.Count == 0,
            "the actual close pointer path dismisses a modal Toast while preserving its owner dialog");
        localController.Dispatch(new("Owner closing") { Timeout = null, CanDismiss = true }); Frame(); Frame();
        outer.Close(); Frame(); Frame();
        check(!tree.CaptureSnapshot().Elements.Any(e => e.Node.Role == UiAccessibilityRole.Group && e.Node.Name == "Owner closing"),
            "closing a parent dialog removes child Toast semantics from the active screen");
        Click("Root action");
        check(rootActions == 1, "closing the modal releases pointer routing back to the surviving root Toast");
    }
    private sealed class Screen(Action<UiImmediateContext> draw) : UiScreen
    { public override void Render(UiImmediateContext ui) => draw(ui); }
}
