using Duxel.Core;

internal static class LegacyGalleryTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        var sample = new SampleScreen { UseEmbeddedMenuBar = true };
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        context.State.SetTextBuffer("sample-pages##active", "Immediate");
        for (var frame = 0; frame < 3; frame++)
        {
            context.NewFrame(new(1f / 60, new(1280, 900), new(1, 1)), idle, new(0, 0, 1280, 900), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding : 0);
                ui.Dummy(new(100, 80));
                ui.BeginTabBar("observer-host");
                if (ui.BeginTabItem("Legacy"))
                {
                    ui.BeginChild("observer-page", new(1200, 700), true);
                    sample.Render(ui);
                    check(ui.GetMainViewport().WorkPos.Y == 0,
                        "embedded Immediate menu does not change the application viewport inset, frame " + frame);
                    ui.EndChild();
                    ui.EndTabItem();
                }
                ui.EndTabBar();
            }));
            using var draw = context.GetDrawData();
        }
    }

    private sealed class Screen(Action<UiImmediateContext> draw) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => draw(ui);
    }
}
