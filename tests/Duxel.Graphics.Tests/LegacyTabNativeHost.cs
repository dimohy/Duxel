using Duxel.Core;
using Duxel.Platform.Windows;

internal static class LegacyTabNativeHost
{
    internal static void Run(Action<bool, string> check)
    {
        using var platform = new WindowsPlatformBackend(new()
        {
            Width = 640, Height = 360, Title = "Duxel owned legacy Tab selection fixture",
            KeyRepeatSettingsProvider = new WindowsKeyRepeatSettingsProvider(), TrayOptions = new()
        });
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        context.SetAccessibilityTree(platform.AccessibilityTree);
        var showBeta = true;
        var finish = false;
        var actions = 0;
        var notifications = 0;
        var alphaCalls = 0;
        var betaCalls = 0;
        var oneBody = true;
        platform.AccessibilityTree.SelectionChanged += _ => notifications++;
        using var remove = Fluent.Button("Remove selected", () => showBeta = false);
        using var done = Fluent.Button("Finish", () => finish = true);
        var screen = new Screen(ui =>
        {
            alphaCalls = betaCalls = 0;
            ui.EnableRootViewportContentLayout(contentPadding : 16);
            ui.BeginTabBar("Legacy event tabs");
            if (ui.BeginTabItem("Alpha")) { alphaCalls++; ui.Button("Alpha body action"); ui.EndTabItem(); }
            if (showBeta && ui.BeginTabItem("Beta")) { betaCalls++; ui.Button("Beta body action"); ui.EndTabItem(); }
            if (ui.TabItemButton("Tab action")) actions++;
            ui.EndTabBar();
            remove.Render(ui); done.Render(ui);
            oneBody &= alphaCalls + betaCalls <= 1;
        });
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var ready = false;
        var deadline = Environment.TickCount64 + 20000;
        while (!finish && !platform.ShouldClose && Environment.TickCount64 < deadline)
        {
            platform.PollEvents();
            context.NewFrame(new(1f / 60, new(640, 360), new(1, 1)), idle, new(0, 0, 640, 360), UiTextSettings.Default);
            context.Render(screen);
            using var draw = context.GetDrawData();
            if (!ready) { ready = true; Console.WriteLine("UIA_TAB_SELECTION_HOST_READY " + platform.WindowHandle); Console.Out.Flush(); }
            Thread.Sleep(1);
        }
        check(finish && !showBeta && actions == 1, "external legacy tab client completes selection, action and removal");
        check(oneBody && alphaCalls == 1 && betaCalls == 0 && context.State.GetTextBuffer("Legacy event tabs##active", string.Empty) == "Alpha",
            "external legacy navigation renders one body per frame and commits the final selected caller state");
        check(notifications == 4, "legacy native fixture emits exactly four committed selection records without idle duplicates");
    }

    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
