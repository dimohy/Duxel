using Duxel.Core;
using Duxel.Platform.Windows;

internal static class AvatarAccessibilityNativeHost
{
    internal static void Run(Action<bool, string> check)
    {
        using var platform = new WindowsPlatformBackend(new()
        {
            Width = 640, Height = 480, Title = "Duxel owned Avatar fixture",
            KeyRepeatSettingsProvider = new WindowsKeyRepeatSettingsProvider(), TrayOptions = new()
        });
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        context.SetAccessibilityTree(platform.AccessibilityTree);
        using var avatar = Fluent.Avatar("Native Avatar");
        var hidden = false;
        var disabled = false;
        var enabledAgain = false;
        var finished = false;
        using var rename = Fluent.Button("Rename avatar", () => avatar.Name = "Renamed Native Avatar");
        using var activity = Fluent.Button("Toggle avatar activity", () =>
        {
            avatar.ActivityText = "활동 중";
            avatar.Options = avatar.Options with { ShowActivityRing = !avatar.Options.ShowActivityRing };
        });
        using var shadow = Fluent.Button("Toggle avatar shadow", () =>
            avatar.Options = avatar.Options with { ShowActivityShadow = !avatar.Options.ShowActivityShadow });
        using var inactive = Fluent.Button("Toggle avatar inactive", () =>
            avatar.Options = avatar.Options with { IsInactive = !avatar.Options.IsInactive });
        using var presence = Fluent.Button("Toggle avatar presence", () =>
        {
            var show = avatar.Options.Presence is null;
            avatar.PresenceText = show ? "사용 가능" : null;
            avatar.Options = avatar.Options with { Size = show ? UiAvatarSize.Size16 : UiAvatarSize.Size32,
                Presence = show ? new(Size : UiBadgeSize.ExtraLarge) : null };
        });
        using var changePresence = Fluent.Button("Change avatar presence", () =>
        {
            avatar.PresenceText = "다른 업무 중 외근";
            avatar.Options = avatar.Options with { Presence = new(UiPresenceStatus.Busy, UiBadgeSize.ExtraLarge, true) };
        });
        using var disable = Fluent.Button("Disable avatar", () => disabled = true);
        using var enable = Fluent.Button("Enable avatar", () => { disabled = false; enabledAgain = true; });
        using var hide = Fluent.Button("Hide avatar", () => hidden = true);
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
                rename.Render(ui); activity.Render(ui); shadow.Render(ui); inactive.Render(ui); presence.Render(ui); changePresence.Render(ui); disable.Render(ui); enable.Render(ui); hide.Render(ui); done.Render(ui);
                if (!hidden)
                {
                    ui.BeginDisabled(disabled);
                    avatar.Render(ui);
                    ui.EndDisabled();
                }
            }));
            using var data = context.GetDrawData();
            if (!ready)
            {
                ready = true;
                Console.WriteLine("UIA_AVATAR_HOST_READY " + platform.WindowHandle);
                Console.Out.Flush();
            }
            Thread.Sleep(1);
        }
        check(finished && avatar.Name == "Renamed Native Avatar", "external Avatar client changes the actual name and completes");
        check(hidden && enabledAgain && !disabled, "external Avatar client completes disable enable and provider removal");
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
