using Duxel.Core;
using Duxel.Platform.Windows;

internal static class LegacySliderRangeNativeHost
{
    internal static void Run(Action<bool, string> check)
    {
        using var platform = new WindowsPlatformBackend(new()
        {
            Width = 640, Height = 480, Title = "Duxel owned legacy Slider RangeValue fixture",
            KeyRepeatSettingsProvider = new WindowsKeyRepeatSettingsProvider(), TrayOptions = new()
        });
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        context.SetAccessibilityTree(platform.AccessibilityTree);
        ControlFontFixture.Configure(context);
        var value = 10f;
        var integer = 10;
        var vertical = 10f;
        var writes = 0;
        var disabled = false;
        var visible = true;
        var finish = false;
        using var disable = Fluent.Button("Disable legacy", () => disabled = true);
        using var remove = Fluent.Button("Remove legacy", () => visible = false);
        using var done = Fluent.Button("Finish", () => finish = true);
        var screen = new Screen(ui =>
        {
            ui.EnableRootViewportContentLayout(contentPadding : 16);
            ui.BeginDisabled(disabled);
            if (visible && ui.SliderFloat("Legacy continuous", ref value, 0, 100, 5, "0.00")) writes++;
            ui.EndDisabled();
            if (ui.SliderInt("Legacy integer", ref integer, 0, 100)) writes++;
            if (ui.VSliderFloat("Legacy vertical", new(40, 100), ref vertical, 0, 100)) writes++;
            disable.Render(ui); remove.Render(ui); done.Render(ui);
        });
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var ready = false;
        var deadline = Environment.TickCount64 + 20000;
        while (!finish && !platform.ShouldClose && Environment.TickCount64 < deadline)
        {
            platform.PollEvents();
            context.NewFrame(new(1f / 60, new(640, 480), new(1, 1)), idle, new(0, 0, 640, 480), UiTextSettings.Default);
            context.Render(screen);
            using var draw = context.GetDrawData();
            if (!ready)
            {
                ready = true;
                Console.WriteLine("UIA_SLIDER_RANGE_HOST_READY " + platform.WindowHandle);
                Console.Out.Flush();
            }
            Thread.Sleep(1);
        }
        check(finish, "external numeric client completes the actual legacy Slider host");
        check(value == 12.25f && integer == 12 && vertical == 16.75f && writes == 3,
            "legacy native mutations preserve continuous values, ties-to-even rounding and exact change counts");
        check(disabled && !visible, "legacy native fixture exercises disabled state and retained-provider removal");
    }

    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
