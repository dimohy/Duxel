using Duxel.Core;

internal static partial class PopoverPaintBoundsTests
{
    internal static void RunRendering(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check,
        UiContext context, Action<UiInputState> setInput, Action<float?> setDelta)
    {
        RenderCase<DefaultSurface>("default", capture, check, context, setInput, setDelta);
        RenderCase<BoundedSurface<WideExtent>>("explicit", capture, check, context, setInput, setDelta);
    }

    private static void RenderCase<T>(string name, Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check,
        UiContext context, Action<UiInputState> setInput, Action<float?> setDelta) where T : IUiPopoverPresentation
    {
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        var calls = 0;
        using var popover = new UiPopover<T>("Bounds owner", Fluent.Button("Bounds action", () => calls++),
            options: new(Width: 200, DisableAutoFocus: true, Positioning: new()
            {
                Position = UiPopupPosition.BelowStart,
                Pinned = true
            }));
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Draw(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            new UiControlContext(ui).Frame(new(0, 0, 320, 240), new(20, 40, 80), default, 0, 0);
            ui.SetCursorScreenPos(new(170, 40));
            popover.Render(ui);
        }
        byte[] Capture(string phase) => capture("paint-bounds-" + name + "-" + phase, Draw);
        int MarkerIndex()
        {
            var marker = Marker(Find(tree, "Bounds owner", UiAccessibilityRole.Group).Node.Bounds);
            return ((int)(marker.Y + 8) * 320 + (int)(marker.X + 8)) * 4;
        }
        setDelta(0);
        setInput(idle);
        DefaultSurface.Paints = 0;
        try
        {
            Capture("closed");
            popover.Show();
            var start = Capture("start");
            var index = MarkerIndex();
            check(start[index] == 20 && start[index + 1] == 40 && start[index + 2] == 80,
                "Custom decoration starts transparent with its whole surface : " + name);
            setDelta(.05f);
            var half = Capture("half");
            index = MarkerIndex();
            check(Math.Abs(half[index] - 110) <= 2 && Math.Abs(half[index + 1] - 20) <= 2 && Math.Abs(half[index + 2] - 40) <= 2,
                "GPU custom decoration beyond88px fades without cropping : " + name);
            setDelta(0);
            check(half.AsSpan().SequenceEqual(Capture("half-replay")) && DefaultSurface.Paints == 3,
                "Custom paint extent replays identical pixels and runs live content once per frame : " + name);
            var action = Find(tree, "Bounds action", UiAccessibilityRole.Button).Node.Bounds;
            var pointer = idle with
            {
                MousePosition = new(action.X + action.Width / 2, action.Y + action.Height / 2)
            };
            setInput(pointer with
            {
                LeftMouseDown = true,
                LeftMousePressed = true
            });
            Capture("press");
            setInput(pointer with
            {
                LeftMouseReleased = true
            });
            Capture("release");
            check(calls == 1 && popover.IsOpen, "Custom animated surface preserves real child pointer input : " + name);
            setInput(idle);
            setDelta(.351f);
            var settled = Capture("settled");
            index = MarkerIndex();
            check(settled[index] == 200 && settled[index + 1] == 0 && settled[index + 2] == 0
                && context.State.NextScheduledFrameTimeSeconds is null, "Custom decoration remains complete when motion settles into event wait : " + name);
            setDelta(0);
            check(settled.AsSpan().SequenceEqual(Capture("settled-replay")), "Settled custom surface stays pixel stable : " + name);
            var decoration = Marker(Find(tree, "Bounds owner", UiAccessibilityRole.Group).Node.Bounds);
            setInput(idle with
            {
                MousePosition = new(decoration.X + 8, decoration.Y + 8),
                LeftMouseDown = true,
                LeftMousePressed = true
            });
            var dismissed = Capture("decoration-dismiss");
            check(!popover.IsOpen && calls == 1 && context.State.NextScheduledFrameTimeSeconds is null
                && dismissed[index] == 20 && dismissed[index + 1] == 40 && dismissed[index + 2] == 80,
                "Paint extent does not turn visual decoration into an input-blocking popup body : " + name);
        }
        finally { setDelta(null); setInput(idle); }
    }
}
