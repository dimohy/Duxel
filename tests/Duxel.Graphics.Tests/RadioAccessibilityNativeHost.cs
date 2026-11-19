using Duxel.Core;
using Duxel.Platform.Windows;

internal static class RadioAccessibilityNativeHost
{
    internal static void Run(Action<bool, string> check)
    {
        using var platform = new WindowsPlatformBackend(new()
        {
            Width = 640, Height = 480, Title = "Duxel owned RadioGroup selection fixture",
            KeyRepeatSettingsProvider = new WindowsKeyRepeatSettingsProvider(), TrayOptions = new()
        });
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        context.SetAccessibilityTree(platform.AccessibilityTree);
        string? value = null;
        var writes = new List<string?>();
        var changes = 0;
        platform.AccessibilityTree.SelectionChanged += _ => changes++;
        UiRadioOption[] options = [new("a", "Radio alpha"), new("b", "Radio beta"), new("c", "Radio disabled", false)];
        using var group = Fluent.RadioGroup("Native radio choice", new UiBinding<string?>(() => value, next => { value = next; writes.Add(next); }), options);
        var disabled = false;
        var visible = true;
        var finish = false;
        using var disable = Fluent.Button("Disable radio choice", () => disabled = true);
        using var remove = Fluent.Button("Remove radio choice", () => visible = false);
        using var done = Fluent.Button("Finish", () => finish = true);
        var screen = new Screen(ui =>
        {
            ui.EnableRootViewportContentLayout(contentPadding : 16);
            ui.BeginDisabled(disabled);
            if (visible) group.Render(ui);
            ui.EndDisabled();
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
                Console.WriteLine("UIA_RADIO_HOST_READY " + platform.WindowHandle);
                Console.Out.Flush();
            }
            Thread.Sleep(1);
        }
        check(finish && disabled && !visible, "external RadioGroup client completes actual disabled and removal paths");
        check(value == "a" && writes.SequenceEqual(new[] { "b", "a" }), "external exclusive Select preserves exact real binding values and callback order");
        check(changes == 3, "RadioGroup emits exactly three real selection property changes before removal without idle duplicates");
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
