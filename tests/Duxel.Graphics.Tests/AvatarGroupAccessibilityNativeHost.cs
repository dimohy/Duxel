using Duxel.Core;
using Duxel.Platform.Windows;

internal static class AvatarGroupAccessibilityNativeHost
{
    internal static void Run(Action<bool, string> check)
    {
        using var platform = new WindowsPlatformBackend(new()
        {
            Width = 640,
            Height = 480,
            Title = "Duxel owned AvatarGroup fixture",
            KeyRepeatSettingsProvider = new WindowsKeyRepeatSettingsProvider(),
            TrayOptions = new()
        });
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        context.SetAccessibilityTree(platform.AccessibilityTree);
        var calls = new List<string>();
        var hidden = false;
        var finished = false;
        var opened = false;
        var pie = false;
        using var group = Fluent.AvatarGroup("Native avatar group",
            Enumerable.Range(0, 6).Select(i => new UiAvatarGroupItem("value" + i, "Person " + i)).ToArray(), selected: calls.Add);
        using var controls = Dux.Wrap(new(Spacing: 8, RunSpacing: 8),
            Fluent.Button("Show pie", () => { group.Options = group.Options with { Layout = UiAvatarGroupLayout.Pie }; pie = true; }),
            Fluent.Button("Hide group", () => hidden = true), Fluent.Button("Finish", () => finished = true));
        var ready = false;
        var limit = Environment.TickCount64 + 60000;
        while (!finished && !platform.ShouldClose && Environment.TickCount64 < limit)
        {
            platform.PollEvents();
            context.NewFrame(new(0, new(640, 480), new(1, 1)),
                new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default),
                new(0, 0, 640, 480), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding: 0);
                controls.Render(ui);
                if (!hidden)
                    group.Render(ui);
            }));
            using var data = context.GetDrawData();
            opened |= group.IsOverflowOpen;
            if (!ready)
            {
                ready = true;
                Console.WriteLine("UIA_AVATAR_GROUP_HOST_READY " + platform.WindowHandle);
                Console.Out.Flush();
            }
            Thread.Sleep(1);
        }
        check(finished && hidden && opened && pie, "external AvatarGroup client opens actual overflow, changes layout and removes the group");
        check(calls.SequenceEqual(new[] { "value1", "value4" }), "external AvatarGroup selections preserve hidden and pie member values");
    }
    private sealed class Screen(Action<UiImmediateContext> draw) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => draw(ui);
    }
}
