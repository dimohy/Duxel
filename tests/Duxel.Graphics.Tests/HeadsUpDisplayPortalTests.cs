using Duxel.Core;

internal static class HeadsUpDisplayPortalTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        var backgroundCalls = 0;
        var taps = 0;
        using var background = Fluent.Button("Background", () => backgroundCalls++);
        using var hud = Fluent.HeadsUpDisplay(new(Label : "저장 중"), new(ReducedMotion : true));
        var changes = new List<bool>();
        hud.OpenChanged = changes.Add;
        var visible = true;
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(float delta = 0, UiInputState? input = null)
        {
            context.NewFrame(new(delta, new(800, 600), new(1, 1)), input ?? idle, new(0, 0, 800, 600), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding : 0);
                background.Render(ui);
                if (visible) hud.Render(ui);
            }));
            using var data = context.GetDrawData();
        }
        UiAccessibilityNode Surface() => tree.CaptureSnapshot().Elements.Single(element => element.Node.Role == UiAccessibilityRole.Dialog).Node;
        bool HasSurface() => tree.CaptureSnapshot().Elements.Any(element => element.Node.Role == UiAccessibilityRole.Dialog);
        void Click(UiVector2 point)
        {
            Frame(input : idle with { MousePosition = point, LeftMouseDown = true, LeftMousePressed = true });
            Frame(input : idle with { MousePosition = point, LeftMouseReleased = true });
            Frame();
        }
        Frame();
        var outside = tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == "Background").Node.Bounds;
        var point = new UiVector2(outside.X + outside.Width / 2, outside.Y + outside.Height / 2);
        check(!hud.IsOpen && !HasSurface(), "HUD remains absent until explicitly shown");
        hud.Show(); Frame(); Frame();
        var surface = Surface();
        check(hud.IsOpen && surface.Modal && surface.Busy
            && MathF.Abs(surface.Bounds.X + surface.Bounds.Width / 2 - 400) < .01f
            && MathF.Abs(surface.Bounds.Y + surface.Bounds.Height / 2 - 300) < .01f,
            "default HUD centers its natural square and exposes a modal busy surface");
        check(!context.State.HasActiveAnimations && hud.Input.MotionNextAt is null,
            "portal reduced motion also suppresses its activity body and entry requests");
        Click(point);
        check(backgroundCalls == 0 && hud.IsOpen, "blocking HUD prevents background activation without light dismissing itself");
        Frame(input : idle with { KeyEvents = [new(UiKey.Escape, true, default)] }); Frame();
        check(hud.IsOpen, "persistent HUD does not silently cancel work on Escape");
        hud.Options = hud.Options with { IsBlocking = false };
        Frame(); Frame(); Click(point);
        check(backgroundCalls == 1 && !Surface().Modal, "nonblocking HUD forwards background interaction");
        hud.Content.Tapped = () => taps++;
        Frame();
        var body = tree.CaptureSnapshot().Elements.Single(element => element.Node.Role == UiAccessibilityRole.Button && element.Node.Name == "저장 중");
        tree.RequestAction(body.Node.Id, body.RuntimeId, UiAccessibilityAction.Invoke, wait : false);
        Frame();
        check(taps == 1 && hud.IsOpen, "tapping the HUD invokes its application callback without closing or cancelling it");
        hud.Options = hud.Options with { KeyboardHeight = 200 };
        Frame();
        surface = Surface();
        check(MathF.Abs(surface.Bounds.Y + surface.Bounds.Height / 2 - 175) < .01f,
            "host keyboard height recenters HUD above the official50pixel keyboard margin");
        hud.Hide(); Frame();
        check(!hud.IsOpen && !HasSurface() && changes.SequenceEqual(new[] { true, false }),
            "Hide retires its surface and notifies visibility once per accepted transition");
        hud.ShowSuccess("Saved"); Frame();
        check(hud.IsOpen && !Surface().Busy && !hud.Options.IsPersistent,
            "success convenience selects completion and the official temporary lifetime");
        Frame(.99f);
        check(hud.IsOpen, "temporary HUD remains open before its one-second deadline");
        Frame(.02f);
        check(!hud.IsOpen && !HasSurface(), "temporary HUD closes after its one-second deadline");
        var desired = true;
        var rejected = 0;
        hud.Open = new UiBinding<bool>(() => desired, _ => rejected++);
        Frame(); Frame(1.01f);
        var atRejection = rejected;
        Frame(2); Frame(2);
        check(hud.IsOpen && rejected == atRejection && rejected == 1 && hud.Input.AutoDismissAt is null,
            "rejected controlled auto-dismiss is requested once without a repeated deadline loop");
        desired = false; Frame();
        check(!hud.IsOpen && !HasSurface(), "controlled owner can subsequently close its HUD");
        hud.Open = null;
        hud.Options = new(ReducedMotion : true);
        hud.Content.Options = new(Label : "Loading");
        hud.Show(); Frame();
        visible = false; Frame();
        check(!hud.IsOpen && !HasSurface(), "omitted HUD releases its overlay ownership");
        visible = true;
        var previous = hud.Options;
        var invalid = false;
        try { hud.Options = previous with { KeyboardHeight = float.NaN }; }
        catch (ArgumentOutOfRangeException) { invalid = true; }
        check(invalid && hud.Options == previous, "invalid keyboard geometry fails before replacing live HUD options");

        hud.Options = new();
        hud.Content.Options = new(Kind : UiHeadsUpDisplayKind.Success, Label : "Saved");
        hud.Show(); Frame(); Frame(.2f);
        var paint = hud.Input.SurfacePaint ?? throw new InvalidOperationException("HUD must capture its exit source.");
        check(hud.Input.MotionNextAt is null, "settled HUD entry stops requesting transition frames");
        hud.Hide(); Frame();
        check(!hud.IsOpen && !HasSurface() && hud.Input.SurfacePaint is null
            && context.State.NextScheduledFrameTimeSeconds is not null,
            "HUD exit retires interaction immediately and schedules frozen paint only");
        Frame(.2f);
        var disposed = false;
        try { paint.ValidateOwner(); }
        catch (ObjectDisposedException) { disposed = true; }
        check(disposed && context.State.NextScheduledFrameTimeSeconds is null,
            "HUD exit completes after200ms and releases its paint and deadline");
        hud.Options = new(KeyboardHeight : 1000);
        hud.Show(); Frame();
        check(hud.IsOpen && hud.Input.SurfacePaint is null && hud.Input.MotionNextAt is null
            && !context.State.HasActiveAnimations,
            "fully occluded keyboard viewport avoids empty paint groups and transition requests");
    }
    internal static void RunGpu(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check,
        PlatformSize framebuffer,
        UiContext context, Action<float?> setDelta)
    {
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        using var hud = Fluent.HeadsUpDisplay(new(Kind : UiHeadsUpDisplayKind.Success, Label : "Saved"));
        void Draw(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding : 0);
            var paint = new UiControlContext(ui);
            paint.Frame(new(0, 0, framebuffer.Width, framebuffer.Height), new(12, 28, 44), default, 0, 0);
            hud.Render(ui);
        }
        UiRect Bounds() => tree.CaptureSnapshot().Elements.Single(element => element.Node.Role == UiAccessibilityRole.Dialog).Node.Bounds;
        setDelta(0);
        try
        {
            var empty = capture("hud-portal-empty", Draw);
            hud.Show();
            var beginning = capture("hud-portal-entry-start", Draw);
            var firstWidth = Bounds().Width;
            check(beginning.AsSpan().SequenceEqual(empty), "HUD entry starts fully transparent");
            setDelta(.1f);
            var middle = capture("hud-portal-entry-middle", Draw);
            var middleBounds = Bounds();
            var final = capture("hud-portal-entry-end", Draw);
            var fullBounds = Bounds();
            check(MathF.Abs(firstWidth / fullBounds.Width - 1.3f) < .001f
                && MathF.Abs(middleBounds.Width / fullBounds.Width - 1.15f) < .001f,
                "HUD entry follows130to100percent scale with a linear midpoint");
            var index = ((int)(middleBounds.Y + 8) * framebuffer.Width + (int)(middleBounds.X + 8)) * 4;
            check(Math.Abs(middle[index] - (12 + 61) / 2f) < 3
                && !middle.AsSpan().SequenceEqual(final), "HUD entry blends the actual dark backplate at half opacity");
            check(context.State.NextScheduledFrameTimeSeconds is null, "settled static HUD has no transition deadline");
            hud.Hide();
            setDelta(0);
            var exitStart = capture("hud-portal-exit-start", Draw);
            check(exitStart.AsSpan().SequenceEqual(final)
                && !tree.CaptureSnapshot().Elements.Any(element => element.Node.Role == UiAccessibilityRole.Dialog),
                "HUD exit preserves exact last pixels while retiring semantics immediately");
            hud.Content.Options = hud.Content.Options with { Label = "Not read during frozen exit" };
            var frozen = capture("hud-portal-exit-frozen", Draw);
            check(frozen.AsSpan().SequenceEqual(exitStart), "HUD exit does not reread changed live content");
            setDelta(.1f);
            var exitMiddle = capture("hud-portal-exit-middle", Draw);
            var exitEnd = capture("hud-portal-exit-end", Draw);
            check(!exitMiddle.AsSpan().SequenceEqual(exitStart) && !exitMiddle.AsSpan().SequenceEqual(empty)
                && exitEnd.AsSpan().SequenceEqual(empty) && context.State.NextScheduledFrameTimeSeconds is null,
                "HUD exit completes a finite200ms shrink and fade without residual pixels");
            hud.Options = hud.Options with { ReducedMotion = true };
            hud.Content.Options = new(Label : "Loading");
            hud.Show(); setDelta(0);
            var reduced = capture("hud-portal-reduced", Draw);
            setDelta(.4f);
            var reducedLater = capture("hud-portal-reduced-later", Draw);
            check(reduced.AsSpan().SequenceEqual(reducedLater) && !context.State.HasActiveAnimations,
                "portal reduced motion freezes the actual activity body without repeated frame requests");
            hud.Hide();
            check(capture("hud-portal-reduced-hidden", Draw).AsSpan().SequenceEqual(empty),
                "reduced-motion HUD hides immediately without an exit animation");
            hud.Options = hud.Options with { ReducedMotion = false, IsPersistent = false };
            hud.Content.Options = new(Kind : UiHeadsUpDisplayKind.Success, Label : "Done");
            hud.Show(); setDelta(0); capture("hud-portal-temporary-start", Draw);
            setDelta(.2f); capture("hud-portal-temporary-visible", Draw);
            setDelta(.81f); capture("hud-portal-temporary-closing", Draw);
            setDelta(.2f); var gone = capture("hud-portal-temporary-gone", Draw);
            check(!hud.IsOpen && gone.AsSpan().SequenceEqual(empty) && context.State.NextScheduledFrameTimeSeconds is null,
                "temporary HUD auto-dismisses and releases all finite presentation deadlines");
            hud.Options = new(KeyboardHeight : 1000);
            hud.Show(); setDelta(0);
            var occluded = capture("hud-portal-occluded", Draw);
            check(occluded.AsSpan().SequenceEqual(empty) && !context.State.HasActiveAnimations,
                "fully occluded HUD preserves exact background pixels without a capture allocation");
        }
        finally
        {
            setDelta(null);
            context.SetAccessibilityTree(null);
        }
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
