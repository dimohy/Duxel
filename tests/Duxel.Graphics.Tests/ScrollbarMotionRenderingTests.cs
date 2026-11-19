using Duxel.Core;

internal static class ScrollbarMotionRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<UiInputState> setInput,
        Action<bool, string> check, UiContext context, int width, UiFluentColorScheme scheme)
    {
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var vertical = 0f;
        var horizontal = 0f;
        var visible = true;
        void Background(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            new UiControlContext(ui).Frame(new(0, 0, width, 240),
                scheme == UiFluentColorScheme.Light ? new UiColor(255, 255, 255) : new(32, 32, 32), default, 0, 0);
        }
        void Draw(UiImmediateContext ui)
        {
            Background(ui);
            var clip = visible ? new UiRect(0, 0, width, 240) : default;
            vertical = ui.RenderScrollbarV("gpu-shared-motion-v", new(200, 20, 12, 160), vertical, 1000, 1160, clip);
            horizontal = ui.RenderScrollbarH("gpu-shared-motion-h", new(20, 200, 160, 12), horizontal, 1000, 1160, clip);
        }
        long Distance(byte[] actual, byte[] reference, UiRect rect)
        {
            long total = 0;
            for (var y = (int)rect.Y; y < rect.Y + rect.Height; y++)
                for (var x = (int)rect.X; x < rect.X + rect.Width; x++)
                    for (var channel = 0; channel < 3; channel++)
                    {
                        var index = (y * width + x) * 4 + channel;
                        total += Math.Abs(actual[index] - reference[index]);
                    }
            return total;
        }
        var v = new UiRect(200, 20, 12, 160);
        var h = new UiRect(20, 200, 160, 12);
        setInput(idle);
        var emptyReference = capture("standard-empty", Background);
        var baseline = capture("standard-idle-faint", Draw);
        check(context.State.NextScheduledFrameTimeSeconds is null && Distance(baseline, emptyReference, v) > 0
            && Distance(baseline, emptyReference, h) > 0, "GPU View scrollbars remain faint while waiting for events");
        vertical = horizontal = 20;
        var start = capture("standard-entry-start", Draw);
        context.State.AdvanceTime(.06f);
        var entering = capture("standard-entry-mid", Draw);
        context.State.AdvanceTime(.15f);
        var shown = capture("standard-shown", Draw);
        context.State.AdvanceTime(.2f);
        var leaving = capture("standard-exit-mid", Draw);
        context.State.AdvanceTime(.2f);
        var hidden = capture("standard-exit-end", Draw);
        check(start.AsSpan().SequenceEqual(hidden) && context.State.NextScheduledFrameTimeSeconds is null
            && !context.State.HasActiveAnimations && Distance(hidden, emptyReference, v) > 0 && Distance(hidden, emptyReference, h) > 0,
            "GPU View scrollbar axes settle back to their exact faint state and event wait");
        check(Distance(shown, hidden, v) > Distance(entering, hidden, v) && Distance(entering, hidden, v) > 0
            && Distance(shown, hidden, h) > Distance(entering, hidden, h) && Distance(entering, hidden, h) > 0,
            "GPU vertical and horizontal scrollbars share visible entry interpolation");
        check(Distance(shown, hidden, v) > Distance(leaving, hidden, v) && Distance(leaving, hidden, v) > 0
            && Distance(shown, hidden, h) > Distance(leaving, hidden, h) && Distance(leaving, hidden, h) > 0,
            "GPU vertical and horizontal scrollbars share visible exit interpolation");
        setInput(idle with
        {
            MousePosition = new(206, 30),
            LeftMouseDown = true,
            LeftMousePressed = true
        });
        capture("standard-drag-press", Draw);
        setInput(idle with
        {
            MousePosition = new(206, 100),
            LeftMouseDown = true
        });
        capture("standard-drag-move", Draw);
        context.State.AdvanceTime(1);
        capture("standard-drag-held", Draw);
        check(vertical > 20 && context.State.ActiveId == "gpu-shared-motion-v" && context.State.NextScheduledFrameTimeSeconds is null,
            "GPU actual standard thumb dragging uses shared motion without stationary polling");
        setInput(idle with
        {
            LeftMouseReleased = true
        });
        capture("standard-drag-release", Draw);
        visible = false;
        var clipped = capture("standard-clipped", Draw);
        check(clipped.AsSpan().SequenceEqual(emptyReference) && context.State.NextScheduledFrameTimeSeconds is null,
            "GPU clipped standard scrollbars cancel live motion without retained visible pixels");

        using var tooltip = new UiTooltip<UiHoverFocusTooltipBehavior, TooltipAccessibilityTests.Surface>(
            Fluent.Button("Motion target"), "Both", new()
            {
                ShowScrollbars = true
            })
        {
            Visible = true
        };
        void DrawTooltip(UiImmediateContext ui)
        {
            Background(ui);
            ui.SetCursorScreenPos(new(20, 130));
            tooltip.Render(ui);
        }
        setInput(idle);
        capture("tooltip-idle-faint", DrawTooltip);
        setInput(idle with
        {
            MousePosition = new(60, 110),
            MouseWheel = -1,
            MouseWheelHorizontal = -1
        });
        capture("tooltip-move", DrawTooltip);
        check(TooltipAccessibilityTests.Surface.LastScrollOffset.X > 0 && TooltipAccessibilityTests.Surface.LastScrollOffset.Y > 0,
            "GPU tooltip actually scrolls both axes through its real wheel path");
        setInput(idle);
        context.State.AdvanceTime(.06f);
        var tooltipEntering = capture("tooltip-entry-mid", DrawTooltip);
        context.State.AdvanceTime(.15f);
        var tooltipShown = capture("tooltip-shown", DrawTooltip);
        var tooltipArea = new UiRect(20, 98, 120, 28);
        context.State.AdvanceTime(.2f);
        var tooltipLeaving = capture("tooltip-exit-mid", DrawTooltip);
        context.State.AdvanceTime(.2f);
        var tooltipHidden = capture("tooltip-exit-end", DrawTooltip);
        check(context.State.NextScheduledFrameTimeSeconds is null,
            "GPU tooltip scrollbar settles faint with no animation deadline");
        check(Distance(tooltipShown, tooltipHidden, tooltipArea) > Distance(tooltipEntering, tooltipHidden, tooltipArea)
            && Distance(tooltipEntering, tooltipHidden, tooltipArea) > 0,
            "GPU tooltip scrollbar entry uses the same interpolated policy");
        check(Distance(tooltipShown, tooltipHidden, tooltipArea) > Distance(tooltipLeaving, tooltipHidden, tooltipArea)
            && Distance(tooltipLeaving, tooltipHidden, tooltipArea) > 0,
            "GPU tooltip scrollbar exit uses the same interpolated policy");
        tooltip.Visible = false;
        capture("tooltip-closed", DrawTooltip);
        check(context.State.NextScheduledFrameTimeSeconds is null, "GPU closed tooltip cannot renew its scrollbar motion");
    }
}
