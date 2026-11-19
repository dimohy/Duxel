using Duxel.Core;

internal static class ToastViewportTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        context.SetDesign(UiCompiledDesign.Windows11Dark); ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree(); context.SetAccessibilityTree(tree);
        var controller = new UiToastController(); using var toaster = new UiToasterWidget(controller, new(ReducedMotion : true));
        var display = new UiVector2(240, 200); var actions = 0; var textScale = 1f;
        var body = string.Join(" ", Enumerable.Repeat("한글 English mixed long notification content.", 30));
        void Frame()
        {
            context.NewFrame(new(.016f, display, new(1, 1)),
                new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default),
                new(0, 0, display.X, display.Y), UiTextSettings.Default with { Scale = textScale });
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding : 0); toaster.Render(ui); }));
            using var draw = context.GetDrawData();
        }
        UiAccessibilityElement Find(string name, UiAccessibilityRole role) => tree.CaptureSnapshot().Elements.Single(e => e.Node.Name == name && e.Node.Role == role);
        controller.Dispatch(new("Long notification", body) { Timeout = null, CanDismiss = true, CloseLabel = "Close long",
            ActionLabel = "Read details", Action = () => actions++ }); Frame(); Frame();
        var bounds = Find("Long notification", UiAccessibilityRole.Group).Node.Bounds;
        check(bounds.X >= 16 && bounds.Y >= 16 && bounds.X + bounds.Width <= display.X - 16
            && bounds.Y + bounds.Height <= display.Y - 16,
            "long Toast fits the available narrow viewport instead of extending above its top edge");
        check(!Find("Close long", UiAccessibilityRole.Button).Node.Offscreen,
            "long Toast keeps its close control visible while its content scrolls");
        var action = Find("Read details", UiAccessibilityRole.Button);
        check(action.Node.Offscreen && tree.RequestAction(action.Node.Id, action.RuntimeId, UiAccessibilityAction.ScrollIntoView, wait : false) == UiAccessibilityResult.Success,
            "long Toast exposes its offscreen action through the existing scroll reveal contract");
        Frame(); Frame(); Frame();
        action = Find("Read details", UiAccessibilityRole.Button);
        check(!action.Node.Offscreen && !Find("Close long", UiAccessibilityRole.Button).Node.Offscreen,
            "revealing the final action preserves the pinned close button");
        tree.RequestAction(action.Node.Id, action.RuntimeId, UiAccessibilityAction.Invoke, wait : false); Frame();
        check(actions == 1, "revealed long Toast action remains operable");
        controller.DismissAll(); Frame(); Frame();
        for (var i = 0; i < 4; i++) controller.Dispatch(new("Stack " + i, body) { Timeout = null, CanDismiss = true });
        Frame(); Frame();
        var cards = tree.CaptureSnapshot().Elements.Where(e => e.Node.Role == UiAccessibilityRole.Group && e.Node.Name.StartsWith("Stack ", StringComparison.Ordinal)).ToArray();
        check(cards.Length == 3 && controller.Queued.Count == 1
            && cards.All(e => e.Node.Bounds.Height >= 44 && e.Node.Bounds.Y >= 16 && e.Node.Bounds.Y + e.Node.Bounds.Height <= display.Y - 16),
            "short viewports keep usable Toast heights and queue notifications that cannot fit");
        textScale = 2; Frame(); Frame();
        check(controller.Visible.Count == 2 && controller.Queued.Count == 2,
            "viewport capacity accounts for accessibility text scaling instead of clipping enlarged text lines");
        textScale = 1;
        display = new(240, 600); Frame(); Frame();
        check(controller.Visible.Count == 4 && controller.Queued.Count == 0,
            "expanding the viewport promotes deferred notifications without losing content");
        toaster.Options = toaster.Options with { RightToLeft = true }; Frame();
        check(controller.Visible.Count == 4 && toaster.Entries.Values.All(e => e.Content.RightToLeft),
            "RTL keeps constrained Toast contents on the shared mirrored renderer");
        display = new(240, 60); Frame(); Frame();
        check(controller.Visible.Count == 0 && controller.Queued.Count == 4 && !context.State.HasActiveAnimations,
            "a viewport too short for one readable notification defers content without an animation loop");
        controller.DismissAll(); display = new(112, 240);
        controller.Dispatch(new("Compact notification", body) { Timeout = null, CanDismiss = true, CloseLabel = "Close compact" });
        Frame(); Frame();
        var compact = toaster.Entries.Values.Single().Content;
        var compactBounds = Find("Compact notification", UiAccessibilityRole.Group).Node.Bounds;
        var closeBounds = Find("Close compact", UiAccessibilityRole.Button).Node.Bounds;
        check(compactBounds.Width == 80 && compact.Title.VisualLines.EffectiveWidth > 0 && compact.DetailsSize.X == 56
            && closeBounds.X >= compactBounds.X && closeBounds.X + closeBounds.Width <= compactBounds.X + compactBounds.Width,
            "eighty-pixel Toast uses a separate icon/close header and a nonzero full-width text column");
        display = new(96, 240); Frame(); Frame();
        check(controller.Visible.Count == 0 && controller.Queued.Count == 1,
            "width below the complete icon/close header defers notification instead of overlapping or losing text");
        display = new(112, 240); Frame(); Frame();
        check(controller.Visible.Count == 1 && controller.Queued.Count == 0,
            "recovering usable width restores the deferred notification");
    }
    private sealed class Screen(Action<UiImmediateContext> draw) : UiScreen
    { public override void Render(UiImmediateContext ui) => draw(ui); }
}
