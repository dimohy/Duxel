using Duxel.Core;

internal static class PopoverMotionRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check,
        UiContext context, Action<UiInputState> setInput, Action<float?> setDelta)
    {
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        var calls = 0;
        using var patch = new UiButton<UiConfiguredButtonBehavior, Overlap>("Motion patch", () => calls++);
        using var popover = Fluent.Popover("GPU motion", patch, options: new(Width: 200, DisableAutoFocus: true,
            WithArrow: true, Positioning: new()
            {
                Position = UiPopupPosition.BelowStart,
                Pinned = true
            }));
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Draw(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            new UiControlContext(ui).Frame(new(0, 0, 320, 240), new(20, 40, 80), default, 0, 0);
            ui.SetCursorScreenPos(new(30, 40));
            popover.Render(ui);
        }
        UiAccessibilityElement Find(string name, UiAccessibilityRole role)
        {
            UiAccessibilityElement? found = null;
            void Visit(string? id)
            {
                while (id is not null && tree.TryGet(id, out var e))
                {
                    if (e.Node.Name == name && e.Node.Role == role)
                        found = e;
                    Visit(e.FirstChild);
                    id = e.Next;
                }
            }
            Visit(tree.Root.First);
            return found ?? throw new InvalidOperationException("GPU motion node missing : " + name);
        }
        setDelta(0);
        setInput(idle);
        try
        {
            var closed = capture("motion-closed", Draw);
            popover.Show();
            var start = capture("motion-start", Draw);
            check(start.AsSpan().SequenceEqual(closed), "GPU entry starts fully transparent as one composited group");
            var initial = Find("GPU motion", UiAccessibilityRole.Group).Node.Bounds;
            setDelta(.05f);
            var half = capture("motion-half", Draw);
            var bounds = Find("Motion patch", UiAccessibilityRole.Button).Node.Bounds;
            var index = ((int)(bounds.Y + 25) * 320 + (int)(bounds.X + 30)) * 4;
            check(Math.Abs(half[index] - 10) <= 2 && Math.Abs(half[index + 1] - 120) <= 2 && Math.Abs(half[index + 2] - 40) <= 2,
                "GPU overlapping opaque children fade together at0.5 instead of accumulating primitive alpha");
            check(MathF.Abs(Find("GPU motion", UiAccessibilityRole.Group).Node.Bounds.Y - initial.Y - 5) < .001f,
                "GPU actual accessibility geometry follows official direction-aware10px slide");
            setDelta(0);
            check(half.AsSpan().SequenceEqual(capture("motion-half-replay", Draw)), "GPU paused entry replays exactly identical pixels");
            var pointer = idle with
            {
                MousePosition = new(bounds.X + 30, bounds.Y + 25)
            };
            setInput(pointer with
            {
                LeftMouseDown = true,
                LeftMousePressed = true
            });
            capture("motion-press", Draw);
            setInput(pointer with
            {
                LeftMouseReleased = true
            });
            capture("motion-release", Draw);
            check(calls == 1 && popover.IsOpen, "GPU moving content handles actual pointer input once at its displayed location");
            setInput(idle);
            setDelta(.351f);
            var settled = capture("motion-settled", Draw);
            setDelta(0);
            check(settled.AsSpan().SequenceEqual(capture("motion-settled-replay", Draw))
                && context.State.NextScheduledFrameTimeSeconds is null, "GPU completed motion returns to stable unscheduled event wait");
            popover.Close();
            popover.Show();
            capture("motion-reopen-start", Draw);
            setDelta(.05f);
            capture("motion-reopen-half", Draw);
            popover.Close();
            setDelta(0);
            var immediate = capture("motion-immediate-close", Draw);
            var trigger = Find("GPU motion", UiAccessibilityRole.Button).Node.Bounds;
            var sameOutsideTrigger = true;
            for (var y = 0; y < 240; y++)
            {
                for (var x = 0; x < 320; x++)
                {
                    if (x >= trigger.X - 2 && x < trigger.X + trigger.Width + 2
                        && y >= trigger.Y - 2 && y < trigger.Y + trigger.Height + 2)
                        continue;
                    var offset = (y * 320 + x) * 4;
                    sameOutsideTrigger &= immediate.AsSpan(offset, 4).SequenceEqual(closed.AsSpan(offset, 4));
                }
            }
            check(sameOutsideTrigger && !popover.IsOpen && context.State.NextScheduledFrameTimeSeconds is null,
                "GPU close during entry removes every popup pixel immediately while restoring trigger focus");
        }
        finally { setDelta(null); setInput(idle); }
    }

    private readonly struct Overlap : IUiButtonPresentation
    {
        public static UiVector2 Measure(UiControlContext context, UiVector2 textSize, UiVector2 requestedSize) => new(140, 60);
        public static void Paint(UiControlContext context, string text, UiVector2 textSize, UiRect bounds, UiButtonInteraction interaction)
        {
            context.Frame(new(bounds.X, bounds.Y, 70, 50), new(200, 0, 0), default, 0, 0);
            context.Frame(new(bounds.X + 20, bounds.Y + 15, 40, 30), new(0, 200, 0), default, 0, 0);
        }
    }
}
