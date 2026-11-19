using Duxel.Core;
using Duxel.Platform.Windows;

internal static class PresenceAccessibilityNativeHost
{
    internal static void Run(Action<bool, string> check)
    {
        using var platform = new WindowsPlatformBackend(new()
        {
            Width = 640, Height = 480, Title = "Duxel owned presence fixture",
            KeyRepeatSettingsProvider = new WindowsKeyRepeatSettingsProvider(), TrayOptions = new()
        });
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        context.SetAccessibilityTree(platform.AccessibilityTree);
        using var badge = Fluent.PresenceBadge();
        var hidden = false;
        var finished = false;
        using var change = Fluent.Button("Change presence", () => badge.Options = badge.Options with { Status = UiPresenceStatus.Away });
        using var oof = Fluent.Button("Set out of office", () => badge.Options = badge.Options with { OutOfOffice = true });
        using var localize = Fluent.Button("Localize presence", () => badge.AccessibleName = "자리 비움 / 외근");
        using var hide = Fluent.Button("Hide presence", () => hidden = true);
        using var done = Fluent.Button("Finish", () => finished = true);
        var ready = false;
        var deadline = Environment.TickCount64 + 60000;
        while (!finished && !platform.ShouldClose && Environment.TickCount64 < deadline)
        {
            platform.PollEvents();
            context.NewFrame(new(1f / 60, new(640, 480), new(1, 1)),
                new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default),
                new(0, 0, 640, 480), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding : 0);
                change.Render(ui); oof.Render(ui); localize.Render(ui); hide.Render(ui); done.Render(ui);
                if (!hidden) badge.Render(ui);
            }));
            using var data = context.GetDrawData();
            if (!ready)
            {
                ready = true;
                Console.WriteLine("UIA_PRESENCE_HOST_READY " + platform.WindowHandle);
                Console.Out.Flush();
            }
            Thread.Sleep(1);
        }
        check(finished && hidden, "external presence client completes provider removal and the owned host");
        check(badge.Options.Status == UiPresenceStatus.Away && badge.Options.OutOfOffice
            && badge.AccessibleName == "자리 비움 / 외근", "external presence client updates the actual status model and localized name");
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
