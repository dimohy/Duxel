using Duxel.Core;

internal static class ScrollRoutingTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        var idle = new UiInputState(new(40, 40), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var clip = new UiRect(0, 0, 320, 240);
        void Frame(UiInputState input, Action<UiImmediateContext>? scene = null)
        {
            context.NewFrame(new(1f / 60, new(320, 240), new(1, 1)), input, new(0, 0, 320, 240), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding : 0);
                if (scene is not null)
                {
                    scene(ui);
                    return;
                }
                ui.SetCursorScreenPos(new(20, 20));
                ui.PushClipRect(clip, true);
                ui.BeginChild("outer", new(120, 100));
                ui.BeginChild("inner", new(60, 50));
                ui.Dummy(new(200, 200));
                ui.EndChild();
                ui.Dummy(new(300, 300));
                ui.EndChild();
                ui.PopClipRect();
            }));
            using var data = context.GetDrawData();
        }
        void Reset(float innerX = 0, float innerY = 0, float outerX = 0, float outerY = 0)
        {
            context.State.SetScrollX("inner", innerX);
            context.State.SetScrollY("inner", innerY);
            context.State.SetScrollX("outer", outerX);
            context.State.SetScrollY("outer", outerY);
        }

        Frame(idle);
        Frame(idle with { MouseWheel = -1 });
        check(context.State.GetScrollY("inner") > 0 && context.State.GetScrollY("outer") == 0,
            "the innermost movable child consumes vertical wheel exactly once");
        Reset(innerY : 10000);
        Frame(idle);
        var bottom = context.State.GetScrollY("inner");
        Frame(idle with { MouseWheel = -1 });
        check(context.State.GetScrollY("inner") == bottom && context.State.GetScrollY("outer") > 0,
            "an inner child at its lower boundary passes downward wheel to its parent");
        Reset(outerY : 20);
        Frame(idle with { MousePosition = new(40, 25), MouseWheel = 1 });
        check(context.State.GetScrollY("inner") == 0 && context.State.GetScrollY("outer") < 20,
            "an inner child at its upper boundary passes upward wheel to its parent");
        Reset();
        Frame(idle with { MouseWheelHorizontal = -1 });
        check(context.State.GetScrollX("inner") > 0 && context.State.GetScrollX("outer") == 0,
            "the innermost movable child consumes horizontal wheel exactly once");
        Reset(innerX : 10000);
        Frame(idle);
        var right = context.State.GetScrollX("inner");
        Frame(idle with { MouseWheelHorizontal = -1 });
        check(context.State.GetScrollX("inner") == right && context.State.GetScrollX("outer") > 0,
            "an inner child at its right boundary passes horizontal wheel to its parent");
        Reset(outerX : 20);
        Frame(idle with { MousePosition = new(25, 40), MouseWheelHorizontal = 1 });
        check(context.State.GetScrollX("inner") == 0 && context.State.GetScrollX("outer") < 20,
            "an inner child at its left boundary passes horizontal wheel to its parent");

        Reset(innerY : 10);
        Frame(idle with { MouseWheel = -1, MouseWheelHorizontal = -1 });
        check(context.State.GetScrollY("inner") > 10 && context.State.GetScrollX("inner") > 0
            && context.State.GetScrollY("outer") == 0 && context.State.GetScrollX("outer") == 0,
            "independent wheel axes retain the innermost owner");
        clip = new(0, 0, 10, 10);
        Reset();
        Frame(idle with { MouseWheel = -1, MouseWheelHorizontal = -1 });
        check(context.State.GetScrollX("inner") == 0 && context.State.GetScrollY("inner") == 0
            && context.State.GetScrollX("outer") == 0 && context.State.GetScrollY("outer") == 0,
            "children outside the caller clip cannot consume either wheel axis");
        Reset();
        Frame(idle with { MousePosition = new(73, 27), LeftMousePressed = true, LeftMouseDown = true });
        check(context.State.ActiveId is null, "a clipped vertical scrollbar cannot capture the pointer");
        Frame(idle with { LeftMouseReleased = true });
        Reset();
        Frame(idle with { MousePosition = new(27, 65), LeftMousePressed = true, LeftMouseDown = true });
        check(context.State.ActiveId is null, "a clipped horizontal scrollbar cannot capture the pointer");
        Frame(idle with { LeftMouseReleased = true });
        clip = new(0, 0, 320, 240);
        Reset();
        Frame(idle with { MousePosition = new(73, 27), LeftMousePressed = true, LeftMouseDown = true });
        check(context.State.ActiveId == "inner##childscroll", "the visible vertical scrollbar retains its existing pointer owner");
        clip = new(0, 0, 10, 10);
        Frame(idle with { MousePosition = new(73, 45), LeftMouseDown = true });
        check(context.State.GetScrollY("inner") > 0, "a captured scrollbar drag continues outside the caller clip");
        Frame(idle with { LeftMouseReleased = true });
        check(context.State.ActiveId is null, "scrollbar release clears its existing pointer owner");

        clip = new(0, 0, 320, 240);
        Reset();
        Frame(idle with { MousePosition = new(27, 65), LeftMousePressed = true, LeftMouseDown = true });
        check(context.State.ActiveId == "inner##childscrollx", "the visible horizontal scrollbar captures its pointer owner");
        clip = new(0, 0, 10, 10);
        Frame(idle with { MousePosition = new(45, 65), LeftMouseDown = true });
        check(context.State.GetScrollX("inner") > 0 && context.State.ActiveId == "inner##childscrollx"
            && context.State.ScrollbarMotionCount == 0 && context.State.NextScheduledFrameTimeSeconds is null,
            "clipped horizontal dragging preserves input capture without scheduling invisible motion");
        Frame(idle with { LeftMouseReleased = true });
        check(context.State.ActiveId is null, "clipped horizontal release clears its pointer owner");

        clip = new(40, 40, 0, 0);
        Reset();
        Frame(idle with { MouseWheel = -1, MouseWheelHorizontal = -1 });
        check(context.State.GetScrollX("inner") == 0 && context.State.GetScrollY("inner") == 0
            && context.State.GetScrollX("outer") == 0 && context.State.GetScrollY("outer") == 0,
            "a zero-area caller clip cannot consume either wheel axis");
        clip = new(0, 0, 320, 240);
        Reset();
        context.State.AddPopupBlockingRect(new(20, 20, 80, 80), 1);
        Frame(idle with { MouseWheel = -1, MouseWheelHorizontal = -1 });
        check(context.State.GetScrollY("inner") == 0 && context.State.GetScrollX("inner") == 0
            && context.State.GetScrollY("outer") == 0 && context.State.GetScrollX("outer") == 0,
            "a higher popup tier blocks wheel ownership below it");
        context.State.AddPopupBlockingRect(new(20, 20, 80, 80), 1);
        Frame(idle with { MousePosition = new(73, 27), LeftMousePressed = true, LeftMouseDown = true });
        check(context.State.ActiveId is null, "a higher popup tier blocks scrollbar capture below it");
        Frame(idle with { LeftMouseReleased = true });

        void Window(UiImmediateContext ui)
        {
            ui.SetNextWindowPos(new(10, 10));
            ui.SetNextWindowSize(new(250, 210));
            ui.BeginPanel("wheel-window");
            ui.BeginChild("window-child", new(100, 70));
            ui.Dummy(new(300, 300));
            ui.EndChild();
            ui.Dummy(new(300, 500));
            ui.EndPanel();
        }
        var windowInput = idle with { MousePosition = new(40, 75) };
        Frame(windowInput, Window);
        Frame(windowInput with { MouseWheel = -1, MouseWheelHorizontal = -1 }, Window);
        check(context.State.GetScrollY("window-child") > 0 && context.State.GetScrollY("wheel-window") == 0
            && context.State.GetScrollX("window-child") > 0 && context.State.GetScrollX("wheel-window") == 0,
            "a movable child consumes both wheel axes before its internal window");
        context.State.SetScrollY("window-child", 10000);
        context.State.SetScrollX("window-child", 10000);
        Frame(windowInput, Window);
        var childBottom = context.State.GetScrollY("window-child");
        var childRight = context.State.GetScrollX("window-child");
        Frame(windowInput with { MouseWheel = -1, MouseWheelHorizontal = -1 }, Window);
        check(context.State.GetScrollY("window-child") == childBottom && context.State.GetScrollX("window-child") == childRight
            && context.State.GetScrollY("wheel-window") > 0 && context.State.GetScrollX("wheel-window") > 0,
            "an immovable child passes both wheel axes to its internal window");
    }

    private sealed class Screen(Action<UiImmediateContext> draw) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => draw(ui);
    }
}
