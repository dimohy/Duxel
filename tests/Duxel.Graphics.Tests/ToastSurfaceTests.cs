using Duxel.Core;

internal static class ToastSurfaceTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        context.SetDesign(UiCompiledDesign.Windows11Dark); ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree(); context.SetAccessibilityTree(tree);
        var announcements = new List<UiAccessibilityAnnouncement>(); tree.Announcement += announcements.Add;
        var clock = new Clock(); var controller = new UiToastController(clock : clock);
        using var toaster = new UiToasterWidget(controller, new(ReducedMotion : true));
        var outsideCalls = 0; var actionCalls = 0;
        using var outside = Fluent.Button("Outside", () => outsideCalls++);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var hostInactive = false;
        void Frame(UiInputState? input = null)
        {
            context.NewFrame(new UiFrameInfo(.016f, new(800, 600), new(1, 1)) { IsWindowInactive = hostInactive }, input ?? idle, new(0, 0, 800, 600), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding : 0); outside.Render(ui); toaster.Render(ui); }));
            using var draw = context.GetDrawData();
        }
        UiAccessibilityElement Node(string name, UiAccessibilityRole role) => tree.CaptureSnapshot().Elements.Single(e => e.Node.Name == name && e.Node.Role == role);
        void Native(string name, UiAccessibilityAction action)
        {
            var node = Node(name, UiAccessibilityRole.Button); tree.RequestAction(node.Node.Id, node.RuntimeId, action, wait : false); Frame();
        }
        void Key(UiKey key)
        {
            Frame(idle with { KeyEvents = [new(key, true, default)] });
            Frame(idle with { KeyEvents = [new(key, false, default)] });
        }
        Frame(); Native("Outside", UiAccessibilityAction.Focus);
        var id = controller.Dispatch(new("Saved", "The document is ready", UiToastIntent.Success)
        { CanDismiss = true, CloseLabel = "Close notification", ActionLabel = "Open document", Action = () => actionCalls++ });
        Frame();
        var surface = Node("Saved", UiAccessibilityRole.Group).Node.Bounds;
        check(surface.Width == 292 && surface.X == 492 && surface.Y + surface.Height == 584,
            "toast portal anchors its natural content at the viewport bottom-end with official width and inset");
        check(Node("Outside", UiAccessibilityRole.Button).Node.Focused && controller.Visible[0].Status == UiToastStatus.Visible,
            "showing a nonmodal toast does not steal focus and reduced motion completes entry immediately");
        check(context.State.NextScheduledFrameTimeSeconds is { } deadline && deadline >= context.State.TimeSeconds + 6.9,
            "a settled timed toast schedules its future expiry rather than an immediate animation frame");
        check(announcements.Count == 1 && announcements[0].Priority == UiAnnouncementPriority.Assertive,
            "visible toast announces its content once with the semantic intent priority");
        Frame(); Frame(); check(announcements.Count == 1, "idle toast frames do not repeat announcements");
        Native("Outside", UiAccessibilityAction.Invoke);
        check(outsideCalls == 1, "nonmodal toast ownership leaves underlying application controls operable");
        Frame(idle with { MousePosition = new(surface.X + 5, surface.Y + 5) });
        clock.Step(20); Frame(idle with { MousePosition = new(surface.X + 5, surface.Y + 5) });
        check(controller.NextTimeout is null && controller.Visible[0].Status == UiToastStatus.Visible,
            "hovering a real toast preserves it past the original deadline");
        Frame(); check(controller.NextTimeout == TimeSpan.FromSeconds(7), "leaving the real toast resumes its remaining duration");
        Key(UiKey.F6);
        check(Node("Open document", UiAccessibilityRole.Button).Node.Focused && controller.NextTimeout is null,
            "F6 enters the first interactive toast control and pauses dismissal while focus is inside");
        Native("Open document", UiAccessibilityAction.Invoke);
        check(actionCalls == 1, "toast action invokes the current application callback");
        Key(UiKey.F6); Frame();
        check(Node("Outside", UiAccessibilityRole.Button).Node.Focused, "F6 returns from the toast to the prior application control");
        controller.Update(id, new("Updated", "New details", UiToastIntent.Info) { CanDismiss = true, CloseLabel = "Close notification" }); Frame();
        check(announcements.Count == 2 && announcements[^1].Priority == UiAnnouncementPriority.Polite
            && Node("Updated", UiAccessibilityRole.Group).Node.Bounds.Width == 292,
            "content updates preserve the portal and announce the new revision with current intent");
        Native("Close notification", UiAccessibilityAction.Focus); Key(UiKey.Delete); Frame();
        check(controller.Visible.Count == 0 && !tree.CaptureSnapshot().Elements.Any(e => e.Node.Name == "Updated")
            && Node("Outside", UiAccessibilityRole.Button).Node.Focused,
            "Delete dismisses the focused toast, retires its semantics and returns focus");
        controller.Dispatch(new("First")); controller.Dispatch(new("Second")); Frame();
        var first = Node("First", UiAccessibilityRole.Group).Node.Bounds;
        var second = Node("Second", UiAccessibilityRole.Group).Node.Bounds;
        check(second.Y > first.Y && second.Y - first.Y - first.Height == 16,
            "bottom-end stacking keeps newest notification at the edge with sixteen logical pixels between surfaces");
        clock.Step(7); Frame(); Frame();
        check(controller.Visible.Count == 0 && toaster.Entries.Count == 0, "deadline wakeup completes reduced-motion exits and releases surface owners");
        toaster.Options = toaster.Options with { Position = UiToastPosition.TopStart, RightToLeft = true, IsWindowActive = false };
        controller.Dispatch(new("Inactive")); Frame();
        check(Node("Inactive", UiAccessibilityRole.Group).Node.Bounds.X == 492 && controller.NextTimeout is null,
            "RTL mirrors logical start and explicit inactive-window state pauses the timeout");
        toaster.Options = toaster.Options with { IsWindowActive = true }; Frame();
        check(controller.NextTimeout == TimeSpan.FromSeconds(7), "reactivating the host resumes the retained timeout");
        clock.Step(2); hostInactive = true; Frame();
        check(controller.NextTimeout is null && controller.Visible[0].PauseReasons.HasFlag(UiToastPauseReason.WindowInactive),
            "captured native frame inactivity pauses Toast without a caller options update");
        clock.Step(20); Frame();
        check(controller.Visible.Count == 1, "inactive native host preserves the toast past its original deadline");
        hostInactive = false; Frame();
        check(controller.NextTimeout == TimeSpan.FromSeconds(5), "native reactivation resumes the exact remaining five seconds");
        clock.Step(5); Frame(); Frame();
        check(controller.Visible.Count == 0, "native reactivation allows normal expiry after the retained interval");
    }
    private sealed class Screen(Action<UiImmediateContext> draw) : UiScreen { public override void Render(UiImmediateContext ui) => draw(ui); }
    private sealed class Clock : TimeProvider
    {
        private long _ticks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => _ticks;
        internal void Step(int seconds) => _ticks += TimeSpan.FromSeconds(seconds).Ticks;
    }
}
