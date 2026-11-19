using Duxel.Core;

internal static class ToastSurfaceRenderingTests
{
    internal static void Run(string output, Action<bool, string> check)
    {
        using var fixture = new PresentationReuseTests.Fixture(1, true, PresentationTestPlatform.Create(640, 400));
        foreach (var samples in new[] { 1, 4 })
            foreach (var dark in new[] { false, true })
            {
                fixture.Renderer.SetMsaaSamples(samples);
                fixture.Context.SetDesign(dark ? UiCompiledDesign.Windows11Dark : UiCompiledDesign.Windows11);
                var clock = new Clock(); var controller = new UiToastController(clock : clock);
                using var toaster = new UiToasterWidget(controller);
                UiImmediateContext? current = null;
                void Draw(UiImmediateContext ui)
                {
                    current = ui;
                    ui.EnableRootViewportContentLayout(contentPadding : 0);
                    new UiControlContext(ui).Frame(new(0, 0, fixture.FrameInfo.DisplaySize.X, fixture.FrameInfo.DisplaySize.Y),
                        dark ? new(16, 16, 16) : new(235, 235, 235), default, 0, 0);
                    toaster.Render(ui);
                }
                var tag = "msaa" + samples + (dark ? "-dark" : "-light");
                var empty = fixture.Capture(Draw);
                var id = controller.Dispatch(new("한글 저장됨", "실제 알림 표면을 확인합니다", UiToastIntent.Success)
                    { CanDismiss = true, CloseLabel = "알림 닫기" });
                var initial = fixture.Capture(Draw);
                check(initial.AsSpan().SequenceEqual(empty) && controller.Visible[0].Status == UiToastStatus.Entering,
                    tag + " toast begins transparently without prematurely starting its visible lifetime");
                fixture.Context.State.AdvanceTime(.1f); var middle = fixture.Capture(Draw);
                fixture.Context.State.AdvanceTime(.3f); var shown = fixture.Capture(Draw);
                check(!empty.AsSpan().SequenceEqual(middle) && !middle.AsSpan().SequenceEqual(shown)
                    && controller.Visible[0].Status == UiToastStatus.Visible,
                    tag + " finite entry motion reaches a stable visible toast");
                var entry = toaster.Entries[id];
                check(entry.Input.SurfacePaint!.For(current!).Content!.TextMode == UiPaintGroupTextMode.PhysicalLcd,
                    tag + " settled toast returns to physical LCD text rather than keeping projected animation text");
                fixture.Capture(Draw); var stable = fixture.Capture(Draw);
                check(stable.AsSpan().SequenceEqual(shown) && fixture.Renderer.LastFrameMetrics.PaintGroupPasses == 0,
                    tag + " idle surface reuses its GPU paint without changing pixels");
                var bounds = entry.Input.Bounds;
                var pixel = (((int)(bounds.Y + bounds.Height / 2)) * fixture.LastCaptureSize.Width + (int)(bounds.X + 5)) * 4;
                var expected = dark ? 41 : 255;
                check(stable[pixel] == expected && stable[pixel + 1] == expected && stable[pixel + 2] == expected,
                    tag + " neutral toast surface preserves the configured literal RGB fill");
                controller.Dismiss(id); var closing = fixture.Capture(Draw);
                fixture.Context.State.AdvanceTime(.1f); var exitMiddle = fixture.Capture(Draw);
                check(!closing.AsSpan().SequenceEqual(exitMiddle) && !empty.AsSpan().SequenceEqual(exitMiddle),
                    tag + " exiting toast fades its frozen GPU snapshot after interactive ownership ends");
                fixture.Context.State.AdvanceTime(.3f); var removed = fixture.Capture(Draw);
                check(removed.AsSpan().SequenceEqual(empty) && controller.Visible.Count == 0 && toaster.Entries.Count == 0,
                    tag + " completed exit removes all toast pixels and owned surface resources");
                fixture.WriteCapturePng(Path.Combine(output, tag + "-entry.png"), middle);
                fixture.WriteCapturePng(Path.Combine(output, tag + "-shown.png"), shown);
                fixture.WriteCapturePng(Path.Combine(output, tag + "-exit.png"), exitMiddle);
            }
        fixture.Renderer.SetMsaaSamples(1);
        fixture.Context.SetDesign(UiCompiledDesign.Windows11Dark);
        using var progress = new UiToastWidget(new("Upload", "Shared composition")
            { Timeout = null, Progress = new(.25f, "25%") });
        UiWidget inlineContent = progress;
        UiColor inlineBackground = new(16, 16, 16);
        void Inline(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding : 16);
            new UiControlContext(ui).Frame(new(0, 0, fixture.FrameInfo.DisplaySize.X, fixture.FrameInfo.DisplaySize.Y),
                inlineBackground, default, 0, 0);
            inlineContent.Render(ui);
        }
        var quarter = fixture.Capture(Inline);
        var unchanged = fixture.Capture(Inline);
        check(quarter.AsSpan().SequenceEqual(unchanged) && !fixture.Context.State.HasActiveAnimations,
            "inline determinate progress remains pixel-stable without an animation loop");
        progress.Options = progress.Options with { Progress = new(.75f, "75%") };
        var threeQuarters = fixture.Capture(Inline);
        check(!quarter.AsSpan().SequenceEqual(threeQuarters), "inline numeric progress updates actual GPU pixels");
        progress.Options = progress.Options with { Progress = new(null, "Working") };
        var spinnerStart = fixture.Capture(Inline);
        fixture.Context.State.AdvanceTime(.13f); var spinnerNext = fixture.Capture(Inline);
        check(!spinnerStart.AsSpan().SequenceEqual(spinnerNext) && fixture.Context.State.HasActiveAnimations,
            "inline indeterminate progress animates its shared spinner on the GPU");
        progress.AnimationPaused = true; fixture.Capture(Inline);
        fixture.Context.State.AdvanceTime(.13f); var paused = fixture.Capture(Inline);
        fixture.Context.State.AdvanceTime(.13f); var pausedAgain = fixture.Capture(Inline);
        check(paused.AsSpan().SequenceEqual(pausedAgain) && !fixture.Context.State.HasActiveAnimations,
            "pausing inline progress freezes actual pixels and releases animation scheduling");
        progress.Options = new("Complete", "Ready", UiToastIntent.Success);
        var complete = fixture.Capture(Inline);
        check(!complete.AsSpan().SequenceEqual(paused) && !fixture.Context.State.HasActiveAnimations,
            "completion replaces progress GPU content without retaining spinner animation");
        fixture.WriteCapturePng(Path.Combine(output, "inline-complete.png"), complete);
        using var tree = new UiAccessibilityTree(); fixture.Context.SetAccessibilityTree(tree);
        using var column = Dux.Column(new(Spacing : 32, MainAxisSize : UiMainAxisSize.Min), progress, Fluent.Body1("Following content"));
        inlineContent = column; inlineBackground = new(235, 235, 235);
        var allocated = fixture.Capture(Inline);
        var body = tree.CaptureSnapshot().Elements.Single(element => element.Node.Role == UiAccessibilityRole.Group
            && element.Node.Name == "Complete").Node.Bounds;
        var shadowPixel = (((int)MathF.Ceiling(body.Y + body.Height) + 3) * fixture.LastCaptureSize.Width
            + (int)(body.X + body.Width / 2)) * 4;
        check(allocated[shadowPixel] < 235 && allocated[shadowPixel + 1] < 235 && allocated[shadowPixel + 2] < 235,
            "allocated Toast shadow paints beyond its layout box into the parent gap");
        fixture.WriteCapturePng(Path.Combine(output, "inline-parent-shadow.png"), allocated);
        var longController = new UiToastController();
        using var longHost = new UiToasterWidget(longController, new(Width : 220, ReducedMotion : true));
        var actionCalls = 0;
        longController.Dispatch(new("Long GPU notification", string.Join(" ", Enumerable.Repeat("한글 English long text", 40)))
            { Timeout = null, CanDismiss = true, CloseLabel = "Close GPU notification", ActionLabel = "GPU details", Action = () => actionCalls++ });
        void Long(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding : 0);
            new UiControlContext(ui).Frame(new(0, 0, fixture.FrameInfo.DisplaySize.X, fixture.FrameInfo.DisplaySize.Y),
                new(235, 235, 235), default, 0, 0);
            longHost.Render(ui);
        }
        var longStart = fixture.Capture(Long);
        UiAccessibilityElement LongAction() => tree.CaptureSnapshot().Elements.Single(e => e.Node.Role == UiAccessibilityRole.Button && e.Node.Name == "GPU details");
        var longAction = LongAction();
        check(longAction.Node.Offscreen && tree.RequestAction(longAction.Node.Id, longAction.RuntimeId, UiAccessibilityAction.ScrollIntoView, wait : false) == UiAccessibilityResult.Success,
            "long GPU Toast accepts reveal of its offscreen action through its content viewport");
        fixture.Capture(Long); fixture.Context.State.AdvanceTime(1); var longScrolled = fixture.Capture(Long);
        check(!longStart.AsSpan().SequenceEqual(longScrolled) && !LongAction().Node.Offscreen,
            "long Toast scrolling changes actual GPU pixels and reveals the action");
        longAction = LongAction(); tree.RequestAction(longAction.Node.Id, longAction.RuntimeId, UiAccessibilityAction.Invoke, wait : false);
        fixture.Capture(Long);
        check(actionCalls == 1 && !tree.CaptureSnapshot().Elements.Single(e => e.Node.Name == "Close GPU notification").Node.Offscreen,
            "scrolled GPU Toast action works while its close control remains pinned");
        fixture.WriteCapturePng(Path.Combine(output, "long-scrolled.png"), longScrolled);
        inlineContent = progress; inlineBackground = new(16, 16, 16);
        progress.Options = new("אבג English 한글 123", "English مرحبا אבג 한글")
            { Timeout = null, Progress = new(.25f, "התקדמות 25%"), CanDismiss = true, CloseLabel = "Close mixed" };
        progress.ReducedMotion = true; progress.RightToLeft = false;
        var ltr = fixture.Capture(Inline);
        progress.RightToLeft = true;
        var rtl = fixture.Capture(Inline);
        var mixed = tree.CaptureSnapshot().Elements.Single(e => e.Node.Role == UiAccessibilityRole.Group && e.Node.Name == progress.Options.Title).Node.Bounds;
        var textChanged = false;
        for (var row = (int)(mixed.Y + 12); row < mixed.Y + 12 + progress.TitleSize.Y; row++)
            for (var col = (int)(mixed.X + 40); col < mixed.X + mixed.Width - 40; col++)
            {
                var pixel = (row * fixture.LastCaptureSize.Width + col) * 4;
                if (!ltr.AsSpan(pixel, 4).SequenceEqual(rtl.AsSpan(pixel, 4))) textChanged = true;
            }
        check(textChanged, "RTL changes actual mixed-language title pixels within the unchanged text column");
        int LastTitleInk(byte[] pixels, UiRect bounds, float inset, float top)
        {
            var last = -1;
            for (var row = (int)(bounds.Y + top); row < bounds.Y + top + progress.Title.VisualLines.LineHeight(0); row++)
                for (var col = (int)(bounds.X + inset); col < bounds.X + bounds.Width - inset; col++)
                {
                    var pixel = (row * fixture.LastCaptureSize.Width + col) * 4;
                    if (pixels[pixel] > 100 || pixels[pixel + 1] > 100 || pixels[pixel + 2] > 100) last = Math.Max(last, col);
                }
            return last;
        }
        check(LastTitleInk(rtl, mixed, 40, 12) >= mixed.X + mixed.Width - 44
            && LastTitleInk(ltr, mixed, 40, 12) < mixed.X + mixed.Width - 48,
            "RTL title ink reaches its physical right-leading edge instead of remaining left-aligned");
        var track = tree.CaptureSnapshot().Elements.Single(e => e.Node.Role == UiAccessibilityRole.ProgressBar).Node.Bounds;
        bool Brand(byte[] pixels, float fraction)
        {
            var at = ((int)(track.Y + track.Height / 2) * fixture.LastCaptureSize.Width + (int)(track.X + track.Width * fraction)) * 4;
            return pixels[at + 2] > pixels[at] + 10;
        }
        check(Brand(ltr, .1f) && !Brand(ltr, .9f) && !Brand(rtl, .1f) && Brand(rtl, .9f),
            "shared Fluent progress fill follows logical start in both LTR and RTL Toasts");
        fixture.WriteCapturePng(Path.Combine(output, "rtl-paragraph.png"), rtl);
        progress.Width = 80;
        var compact = fixture.Capture(Inline);
        var close = tree.CaptureSnapshot().Elements.Single(e => e.Node.Role == UiAccessibilityRole.Button && e.Node.Name == "Close mixed").Node.Bounds;
        var compactBody = tree.CaptureSnapshot().Elements.Single(e => e.Node.Role == UiAccessibilityRole.Group && e.Node.Name == progress.Options.Title).Node.Bounds;
        check(close.X >= compactBody.X && close.X + close.Width <= compactBody.X + compactBody.Width
            && progress.Title.VisualLines.EffectiveWidth > 0,
            "compact RTL Toast renders a complete close header and a nonempty native paragraph column on the GPU");
        check(LastTitleInk(compact, compactBody, 12, 40) >= compactBody.X + compactBody.Width - 16,
            "compact RTL first-line ink follows the full-width text column's right-leading edge");
        fixture.WriteCapturePng(Path.Combine(output, "compact-rtl.png"), compact);
        var mixedAction = "אבג English 한글 123";
        var mixedCalls = 0;
        progress.Width = 292;
        progress.Options = new(mixedAction, "Mixed action label")
            { CanDismiss = true, ActionLabel = mixedAction, Action = () => mixedCalls++ };
        var actionPixels = fixture.Capture(Inline);
        var actionButton = tree.CaptureSnapshot().Elements.Single(e => e.Node.Role == UiAccessibilityRole.Button && e.Node.Name == mixedAction);
        check(!actionButton.Node.Offscreen && progress.Action.Label == mixedAction,
            "mixed-language Toast action preserves its original accessible label and finite visible allocation");
        var titleRuns = progress.Title.VisualLines.NativeParagraph!.Runs.Span;
        var actionRuns = progress.Action.Text.VisualLines.NativeParagraph!.Runs.Span;
        check(titleRuns.Length == actionRuns.Length && titleRuns.ToArray().Select(run => run.BidiLevel)
            .SequenceEqual(actionRuns.ToArray().Select(run => run.BidiLevel)),
            "mixed-language Toast action uses the same native bidi run order as its title");
        tree.RequestAction(actionButton.Node.Id, actionButton.RuntimeId, UiAccessibilityAction.Invoke, wait : false); fixture.Capture(Inline);
        check(mixedCalls == 1, "mixed-language Toast action invokes the current callback");
        fixture.WriteCapturePng(Path.Combine(output, "mixed-action-label.png"), actionPixels);
    }
    private sealed class Clock : TimeProvider
    {
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => 0;
    }
}
