using System.Diagnostics;
using Duxel.Core;
using Duxel.Platform.Windows;
using Duxel.Validation;

// Actual HWND, input and UIA provider; GPU composition is qualified separately.
internal static class DialogAccessibilityNativeHost
{
    internal static void Run(Action<bool, string> check)
    {
        using var platform = new WindowsPlatformBackend(new()
        {
            Width = 900,
            Height = 600,
            Title = "Duxel owned Dialog fixture",
            KeyRepeatSettingsProvider = new WindowsKeyRepeatSettingsProvider(),
            TrayOptions = new()
        });
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        context.SetAccessibilityTree(platform.AccessibilityTree);
        using var screen = new DialogAccessibilityScreen();
        var limit = Environment.TickCount64 + 30000;
        var previous = Stopwatch.GetTimestamp();
        while (!screen.Finished && !platform.ShouldClose && Environment.TickCount64 < limit)
        {
            platform.PollEvents();
            var input = platform.Input.Snapshot;
            var now = Stopwatch.GetTimestamp();
            screen.Frame++;
            context.NewFrame(new((float)Stopwatch.GetElapsedTime(previous, now).TotalSeconds, new(900, 600), new(1, 1)),
                new(input.MousePosition, input.LeftMouseDown, input.RightMouseDown, input.LeftMousePressedEvent, input.LeftMouseReleasedEvent,
                    input.RightMousePressedEvent, input.RightMouseReleasedEvent, input.MouseWheel, input.MouseWheelHorizontal,
                    input.KeyEvents, input.CharEvents, input.KeyRepeatSettings, input.Modifiers), new(0, 0, 900, 600), UiTextSettings.Default);
            previous = now;
            context.Render(screen);
            using var draw = context.GetDrawData();
            if (screen.Frame == 1)
            {
                Console.WriteLine("UIA_DIALOG_HOST_READY " + platform.WindowHandle);
                Console.Out.Flush();
            }
            Thread.Sleep(1);
        }
        check(screen.Finished && !screen.Dialog.IsOpen, "external Dialog client completes the owned native fixture with its surface closed");
        check(screen.Name.Value == "정세일 UIA", "external native Value writes persist in the actual Dialog editing model");
    }
}
