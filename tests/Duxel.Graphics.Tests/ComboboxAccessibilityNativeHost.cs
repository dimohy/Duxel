using Duxel.Core;
using Duxel.Platform.Windows;

internal static class ComboboxAccessibilityNativeHost
{
    internal static void Run(Action<bool, string> check)
    {
        using var platform = new WindowsPlatformBackend(new()
        {
            Width = 640,
            Height = 480,
            Title = "Duxel owned Combobox fixture",
            KeyRepeatSettingsProvider = new WindowsKeyRepeatSettingsProvider(),
            TrayOptions = new()
        });
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        context.SetAccessibilityTree(platform.AccessibilityTree);
        string? value = null;
        string[] values = [];
        string text = "";
        string multiText = "";
        UiSelectOption[] items = [new("a", "Alpha"), new("b", "Beta"), new("g", "Gamma", false), new("n", "New York")];
        using var single = Fluent.Combobox("Native combobox", new UiBinding<string>(() => text, next => text = next), new UiBinding<string?>(() => value, next => value = next), items);
        using var multiple = Fluent.Combobox("Native multiple", new UiBinding<string>(() => multiText, next => multiText = next), new UiBinding<string[]>(() => values, next => values = next), items);
        var allowOpen = false;
        var allowClose = false;
        var gateOpen = false;
        string gateText = "";
        var gateChanges = new List<bool>();
        using var gated = Fluent.Combobox("Native gated", new UiBinding<string>(() => gateText, next => { gateText = next; if (next == "Alpha") allowClose = true; }),
            new UiBinding<string?>(() => null, _ => { }), items);
        gated.Open = new(() => gateOpen, next => { if (next ? allowOpen : allowClose) gateOpen = next; });
        gated.OpenChanged = gateChanges.Add;
        var hidden = false;
        var finished = false;
        var opened = false;
        using var controls = Dux.Wrap(new(Spacing: 8, RunSpacing: 8),
            Fluent.Button("Hide dropdowns", () => hidden = true), Fluent.Button("Finish", () => finished = true),
            Fluent.Button("Allow opening", () => allowOpen = true));
        var ready = false;
        var limit = Environment.TickCount64 + 60000;
        while (!finished && !platform.ShouldClose && Environment.TickCount64 < limit)
        {
            platform.PollEvents();
            single.Options = single.Options with { Field = new(Invalid: value != "a") };
            context.NewFrame(new(0, new(640, 480), new(1, 1)),
                new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default),
                new(0, 0, 640, 480), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding: 0);
                controls.Render(ui);
                if (!hidden)
                {
                    single.Render(ui);
                    multiple.Render(ui);
                    gated.Render(ui);
                }
            }));
            using var data = context.GetDrawData();
            opened |= single.IsOpen || multiple.IsOpen;
            if (!ready)
            {
                ready = true;
                Console.WriteLine("UIA_COMBOBOX_HOST_READY " + platform.WindowHandle);
                Console.Out.Flush();
            }
            Thread.Sleep(1);
        }
        check(finished && hidden && opened, "external Combobox client opens and removes actual selection controls");
        check(value == "a" && values.SequenceEqual(new[] { "n" }), "external Combobox single and multiple operations commit real bound values");
        check(!gateOpen && gateChanges.SequenceEqual(new[] { true, false }),
            "external controlled Combobox rejects forbidden changes and reports exactly two accepted transitions");
    }
    private sealed class Screen(Action<UiImmediateContext> draw) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => draw(ui);
    }
}
