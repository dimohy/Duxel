using Duxel.Core;
using Duxel.Platform.Windows;

internal static class TabOverflowNativeHost
{
    internal static void Run(Action<bool, string> check)
    {
        using var platform = new WindowsPlatformBackend(new()
        {
            Width = 640, Height = 400, Title = "Duxel owned tab overflow fixture",
            KeyRepeatSettingsProvider = new WindowsKeyRepeatSettingsProvider(), TrayOptions = new()
        });
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        context.SetAccessibilityTree(platform.AccessibilityTree);
        var value = new UiState<string?>("one");
        using var tabs = Fluent.TabList("Overflow tabs", value.Binding,
            new UiTabOption[] { new("one", "One"), new("two", "Two", false), new("three", "Three"), new("four", "Four") },
            new() { Overflow = new() });
        using var box = Dux.SizedBox(tabs, width : 140, height : 40);
        var finish = false;
        using var first = Fluent.Button("One body");
        using var last = new BodyButton(() => finish = true);
        var screen = new Screen(ui =>
        {
            ui.EnableRootViewportContentLayout(contentPadding : 16);
            box.Render(ui);
            tabs.RenderPanel(ui, "one", first);
            tabs.RenderPanel(ui, "four", last);
        });
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var deadline = Environment.TickCount64 + 20000;
        var ready = false;
        while (!finish && !platform.ShouldClose && Environment.TickCount64 < deadline)
        {
            platform.PollEvents();
            context.NewFrame(new(1f / 60, new(640, 400), new(1, 1)), idle, new(0, 0, 640, 400), UiTextSettings.Default);
            context.Render(screen);
            using var draw = context.GetDrawData();
            if (!ready)
            {
                ready = true;
                Console.WriteLine("UIA_TAB_OVERFLOW_HOST_READY " + platform.WindowHandle);
                Console.Out.Flush();
            }
            Thread.Sleep(1);
        }
        check(finish && value.Value == "four", "external UIA selects the hidden actual tab and invokes its actual panel child");
        check(tabs.OverflowResult is { IsOverflowing : true } && last.Disposals == 0,
            "tab overflow preserves its actual selected panel widget until owner disposal");
    }
    private sealed class BodyButton : UiFluentButton
    {
        internal int Disposals;
        internal BodyButton(Action activated) : base("Finish overflow") => Activated = activated;
        protected override void OnDispose()
        {
            Disposals++;
            base.OnDispose();
        }
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
