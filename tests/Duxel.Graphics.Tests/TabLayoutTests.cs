using Duxel.Core;

internal static class TabLayoutTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        UiVector2 first = default, second = default, secondHeader = default, after = default;
        var firstBodies = 0;
        var secondBodies = 0;
        void Frame(UiInputState input)
        {
            firstBodies = secondBodies = 0;
            context.NewFrame(new(1f / 60, new(640, 480), new(1, 1)), input, new(0, 0, 640, 480), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding : 0);
                ui.BeginTabBar("layout-tabs");
                if (ui.BeginTabItem("First"))
                {
                    firstBodies++;
                    first = ui.GetCursorScreenPos();
                    ui.Dummy(new(100, 24));
                    second = ui.GetCursorScreenPos();
                    ui.Dummy(new(80, 30));
                    ui.EndTabItem();
                }
                secondHeader = ui.GetCursorScreenPos();
                if (ui.BeginTabItem("Second"))
                {
                    secondBodies++;
                    ui.BeginTabBar("nested-tabs");
                    if (ui.BeginTabItem("Nested"))
                    {
                        ui.Dummy(new(60, 40));
                        ui.EndTabItem();
                    }
                    ui.EndTabBar();
                    ui.EndTabItem();
                }
                ui.TabItemButton("Action");
                ui.EndTabBar();
                after = ui.GetCursorScreenPos();
                ui.Dummy(new(10, 10));
            }));
            using var draw = context.GetDrawData();
        }
        UiInputState Input(UiVector2 position, bool down = false, bool pressed = false, bool released = false) =>
            new(position, down, false, pressed, released, false, false, 0, 0, [], [], default, default);
        Frame(Input(new(-100, -100)));
        check(firstBodies == 1 && secondBodies == 0, "only the selected tab body renders");
        check(first.X == 0 && second.X == first.X && second.Y >= first.Y + 24,
            "tab body controls use vertical layout independently of horizontal headers");
        check(secondHeader.Y < first.Y && secondHeader.X > 0,
            "subsequent tab headers stay above the active body on the header row");
        check(after.Y >= second.Y + 30 && after.X == 0,
            "following controls start below the complete tab body");
        var target = new UiVector2(secondHeader.X + 5, secondHeader.Y + 5);
        Frame(Input(target, down : true, pressed : true));
        Frame(Input(target, released : true));
        check(firstBodies + secondBodies == 1, "a tab click never renders two bodies in one frame");
        check(context.State.NextScheduledFrameTimeSeconds == context.State.TimeSeconds,
            "changing a tab schedules its deferred selection even when the app waits for events");
        Frame(Input(target));
        check(firstBodies == 0 && secondBodies == 1 && after.Y > secondHeader.Y + 40,
            "tab selection supports nested bars and restores the outer layout");
    }

    private sealed class Screen(Action<UiImmediateContext> draw) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => draw(ui);
    }
}
