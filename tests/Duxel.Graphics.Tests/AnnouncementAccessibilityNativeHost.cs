using System.Diagnostics;
using Duxel.Core;
using Duxel.Platform.Windows;

internal static class AnnouncementAccessibilityNativeHost
{
    internal static void Run(Action<bool, string> check)
    {
        using var scene = new NativeAnnouncementScene();
        RunScene(check, scene, () => scene.Finished, () => scene.Queued == 4);
    }

    internal static void RunMessageBars(Action<bool, string> check)
    {
        using var scene = new NativeMessageBarScene();
        RunScene(check, scene, () => scene.Finished, () => scene.Announcements == 4);
    }

    private static void RunScene(Action<bool, string> check, UiScreen scene, Func<bool> finished, Func<bool> complete)
    {
        using var platform = new WindowsPlatformBackend(new()
        {
            Width = 640,
            Height = 420,
            Title = "Duxel owned announcement fixture",
            KeyRepeatSettingsProvider = new WindowsKeyRepeatSettingsProvider(),
            TrayOptions = new()
        });
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        context.SetAccessibilityTree(platform.AccessibilityTree);
        var end = Environment.TickCount64 + 20000;
        var previous = Stopwatch.GetTimestamp();
        var ready = false;
        while (!finished() && !platform.ShouldClose && Environment.TickCount64 < end)
        {
            platform.PollEvents();
            var now = Stopwatch.GetTimestamp();
            context.NewFrame(new((float)Stopwatch.GetElapsedTime(previous, now).TotalSeconds, new(640, 420), new(1, 1)),
                new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default),
                new(0, 0, 640, 420), UiTextSettings.Default);
            previous = now;
            context.Render(scene);
            using var data = context.GetDrawData();
            if (!ready)
            {
                ready = true;
                Console.WriteLine("UIA_ANNOUNCEMENT_HOST_READY " + platform.WindowHandle);
                Console.Out.Flush();
            }
            Thread.Sleep(1);
        }
        check(finished() && complete(), "Independent UIA client completes the actual four-announcement fixture");
    }
}
