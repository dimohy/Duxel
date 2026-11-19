using Duxel.Core;

internal static class ToastCompositionTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        context.SetDesign(UiCompiledDesign.Windows11Dark); ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree(); context.SetAccessibilityTree(tree);
        var announcements = new List<UiAccessibilityAnnouncement>(); tree.Announcement += announcements.Add;
        var dismissed = 0;
        using var widget = new UiToastWidget(new("Upload", "Preparing") { Timeout = null, Progress = new(null) }, () => dismissed++);
        var controller = new UiToastController();
        using var host = new UiToasterWidget(controller, new(ReducedMotion : true));
        var portal = false;
        var measured = default(UiVector2);
        void Frame()
        {
            context.NewFrame(new(.016f, new(800, 600), new(1, 1)),
                new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default),
                new(0, 0, 800, 600), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding : 0);
                measured = new UiControlContext(ui).MeasureConstrained(widget, new(0, 500, 0, float.PositiveInfinity));
                if (portal) host.Render(ui); else widget.Render(ui);
            }));
            using var draw = context.GetDrawData();
        }
        UiAccessibilityNode[] Progress() => tree.CaptureSnapshot().Elements.Where(e => e.Node.Role == UiAccessibilityRole.ProgressBar).Select(e => e.Node).ToArray();
        UiAccessibilityNode Group(string name) => tree.CaptureSnapshot().Elements.Single(e => e.Node.Role == UiAccessibilityRole.Group && e.Node.Name == name).Node;
        Frame();
        check(widget.IsOpen && measured.X == 292 && Group("Upload").Busy, "standalone Toast reserves its natural layout and publishes a busy accessible group");
        check(Progress().Length == 1 && Progress()[0].Range is null && context.State.HasActiveAnimations,
            "indeterminate Toast shows exactly one animated progress role instead of a simultaneous bar and spinner");
        widget.ReducedMotion = true; Frame(); Frame();
        check(!context.State.HasActiveAnimations, "reduced-motion standalone progress freezes its spinner and stops requesting animation frames");
        widget.Options = widget.Options with { Progress = new(.25f, "One of four") }; Frame();
        check(Progress().Length == 1 && Progress()[0].Range?.Value == .25 && Progress()[0].Range?.Maximum == 1,
            "determinate Toast replaces its spinner with the common read-only progress range");
        check(tree.CaptureSnapshot().Elements.Any(e => e.Node.Name == "One of four") && !context.State.HasActiveAnimations,
            "determinate status text is visible and a settled value does not animate");
        var spoken = announcements.Count;
        widget.Options = widget.Options with { Progress = new(.75f, "Three of four") }; Frame();
        check(Progress()[0].Range?.Value == .75 && announcements.Count == spoken,
            "numeric progress updates change range semantics without repeating the unchanged spoken message");
        widget.Options = new("Complete", "Ready", UiToastIntent.Success) { CanDismiss = true, CloseLabel = "Close inline" }; Frame();
        check(Progress().Length == 0 && !Group("Complete").Busy && announcements.Count == spoken + 1,
            "completion removes progress semantics and announces the new confirmation once");
        var close = tree.CaptureSnapshot().Elements.Single(e => e.Node.Role == UiAccessibilityRole.Button && e.Node.Name == "Close inline");
        tree.RequestAction(close.Node.Id, close.RuntimeId, UiAccessibilityAction.Invoke, wait : false); Frame(); Frame();
        check(!widget.IsOpen && dismissed == 1 && measured == default
            && !tree.CaptureSnapshot().Elements.Any(e => e.Node.Name == "Complete"),
            "standalone close hides its layout and semantics while notifying its owner once");
        widget.Hide(); check(dismissed == 1, "standalone Hide is idempotent");
        widget.Show(); Frame();
        check(widget.IsOpen && announcements.Count == spoken + 2, "showing a closed standalone Toast announces a new appearance");
        widget.RightToLeft = true; Frame();
        close = tree.CaptureSnapshot().Elements.Single(e => e.Node.Role == UiAccessibilityRole.Button && e.Node.Name == "Close inline");
        check(close.Node.Bounds.X == Group("Complete").Bounds.X + 12, "RTL composition mirrors the close control to the physical left");
        var beforeQuiet = announcements.Count;
        widget.Options = widget.Options with { Title = "Quiet", DisableAnnouncement = true }; Frame();
        check(announcements.Count == beforeQuiet, "explicitly disabled live announcements still render the component without speaking");
        widget.Options = widget.Options with { DisableAnnouncement = false }; Frame();
        check(announcements.Count == beforeQuiet + 1, "reenabling announcements publishes the current message once");
        var prior = widget.Options;
        try { widget.Options = new("Invalid") { Timeout = null, Progress = new(float.NaN) }; check(false, "nonfinite progress fails before options mutate"); }
        catch (ArgumentException) { check(widget.Options == prior, "nonfinite progress fails before options mutate"); }
        try { widget.Options = new("Invalid") { Progress = new(.5f) }; check(false, "progress requires an explicit persistent lifetime"); }
        catch (ArgumentException) { check(widget.Options == prior, "progress requires an explicit persistent lifetime"); }
        try { widget.Width = 0; check(false, "standalone width rejects a nonpositive value"); }
        catch (ArgumentException) { check(widget.Width == 292, "standalone width rejects a nonpositive value"); }
        portal = true; announcements.Clear();
        var id = controller.Dispatch(new("Host upload", "Sending") { Timeout = null, Progress = new(.25f, "One of four") }); Frame();
        check(Progress().Length == 1 && Progress()[0].Range?.Value == .25 && Group("Host upload").Busy,
            "portal host uses the same composed progress semantics without a duplicate root group");
        spoken = announcements.Count;
        controller.Update(id, controller.Visible[0].Options with { Progress = new(.5f, "Two of four") }); Frame();
        check(Progress()[0].Range?.Value == .5 && announcements.Count == spoken,
            "hosted numeric progress does not flood the shared announcement channel");
        controller.Update(id, new("Host complete", Intent : UiToastIntent.Success)); Frame();
        check(Progress().Length == 0 && announcements.Count == spoken + 1 && controller.NextTimeout is not null,
            "host completion switches to a timed confirmation using the existing lifecycle controller");
    }
    private sealed class Screen(Action<UiImmediateContext> draw) : UiScreen { public override void Render(UiImmediateContext ui) => draw(ui); }
}
