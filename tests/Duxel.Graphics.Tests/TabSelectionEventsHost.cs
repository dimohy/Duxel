using System.Diagnostics;
using Duxel.Core;
using Duxel.Platform.Windows;

internal static class TabSelectionEventsHost
{
    internal static void Run(Action<bool, string> check)
    {
        using var platform = new WindowsPlatformBackend(new()
        {
            Width = 640, Height = 360, Title = "Duxel owned tab selection event fixture",
            KeyRepeatSettingsProvider = new WindowsKeyRepeatSettingsProvider(), TrayOptions = new()
        });
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        context.SetAccessibilityTree(platform.AccessibilityTree);
        var value = new UiState<string?>("alpha");
        var icon = UiTabIcon.Create<UiFluentAddIcon, UiFluentAddFilledIcon>();
        using var tabs = Fluent.TabList("Event tabs", value.Binding,
            new UiTabOption[] { new("alpha", "Alpha", Icon : icon), new("beta", "Beta", Icon : icon, IconOnly : true) });
        using var alphaBody = Fluent.Button("Alpha body action");
        using var betaBody = Fluent.Button("Beta body action");
        using var clear = Fluent.Button("Clear selection", () => value.Value = null);
        using var remove = Fluent.Button("Remove selected", () => tabs.SetItems(new UiTabOption[] { new("alpha", "Alpha") }));
        var finish = false;
        using var done = Fluent.Button("Finish", () => finish = true);
        var notifications = 0;
        platform.AccessibilityTree.SelectionChanged += change =>
        {
            notifications++;
            Console.WriteLine($"CoreSelectionEvent : {change.Element.Node.Name} / {change.Previous} / {change.Element.Node.Selected} / {change.Event}");
        };
        var screen = new Screen(ui =>
        {
            ui.EnableRootViewportContentLayout(contentPadding : 16);
            tabs.Render(ui);
            foreach (var item in tabs.Items)
                tabs.RenderPanel(ui, item.Value, item.Value == "alpha" ? alphaBody : betaBody);
            clear.Render(ui);
            remove.Render(ui);
            done.Render(ui);
        });
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var clock = Stopwatch.StartNew();
        var previous = 0d;
        var ready = false;
        while (!finish && !platform.ShouldClose && clock.Elapsed.TotalSeconds < 20)
        {
            platform.PollEvents();
            var now = clock.Elapsed.TotalSeconds;
            context.NewFrame(new((float)(now - previous), new(640, 360), new(1, 1)), idle, new(0, 0, 640, 360), UiTextSettings.Default);
            previous = now;
            context.Render(screen);
            using var draw = context.GetDrawData();
            if (!ready) { ready = true; Console.WriteLine("UIA_TAB_SELECTION_HOST_READY " + platform.WindowHandle); Console.Out.Flush(); }
            Thread.Sleep(1);
        }
        check(finish, "external selection client completes its owned fixture");
        check(notifications == 5, "native fixture produces exactly five committed property/selection records across all idle frames");
        check(value.Value == "beta" && tabs.Items.Length == 1, "removing a selected key preserves the caller's binding without fabricating a replacement");
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
