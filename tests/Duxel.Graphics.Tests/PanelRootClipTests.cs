using Duxel.Core;

internal static class PanelRootClipTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        UiRect button = default;
        var clicks = 0;
        void Frame(UiInputState input)
        {
            context.NewFrame(new(1f / 60, new(640, 480), new(1, 1)), input, new(0, 0, 640, 480), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding : 0);
                ui.SetCursorScreenPos(new(0, 240));
                ui.BeginChild("panel-caller", new(120, 120), true);
                ui.SetNextPanelPos(new(180, 20));
                ui.SetNextPanelSize(new(260, 180));
                ui.BeginPanel("Floating outside caller");
                if (ui.Button("Panel body action"))
                    clicks++;
                var position = ui.GetItemRectMin();
                var size = ui.GetItemRectSize();
                button = new(position.X, position.Y, size.X, size.Y);
                ui.EndPanel();
                ui.EndChild();
            }));
            using var draw = context.GetDrawData();
        }
        UiInputState Input(UiVector2 pointer, bool down = false, bool pressed = false, bool released = false) =>
            new(pointer, down, false, pressed, released, false, false, 0, 0, [], [], default, default);
        Frame(Input(new(-100, -100)));
        check(button.Y < 240 && button.X >= 180, "floating panel content is positioned independently of its calling child");
        var center = new UiVector2(button.X + button.Width / 2, button.Y + button.Height / 2);
        Frame(Input(center, down : true, pressed : true));
        Frame(Input(center, released : true));
        check(clicks == 1, "floating panel content outside the parent child clip still receives its real pointer action");
    }

    private sealed class Screen(Action<UiImmediateContext> draw) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => draw(ui);
    }
}
