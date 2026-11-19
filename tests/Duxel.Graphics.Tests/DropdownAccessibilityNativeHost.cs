using Duxel.Core;
using Duxel.Platform.Windows;

internal static class DropdownAccessibilityNativeHost
{
    internal static void Run(Action<bool, string> check)
    {
        using var platform = new WindowsPlatformBackend(new()
        {
            Width = 640,
            Height = 480,
            Title = "Duxel owned Dropdown fixture",
            KeyRepeatSettingsProvider = new WindowsKeyRepeatSettingsProvider(),
            TrayOptions = new()
        });
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        context.SetAccessibilityTree(platform.AccessibilityTree);
        string? value = null;
        string[] values = [];
        UiSelectOption[] items = [new("a", "Alpha"), new("b", "Beta"), new("g", "Gamma", false), new("n", "New York")];
        using var single = Fluent.Dropdown("Native dropdown", new UiBinding<string?>(() => value, next => value = next), items);
        using var multiple = Fluent.Dropdown("Native multiple", new UiBinding<string[]>(() => values, next => values = next), items);
        using var tags = new UiFluentTagGroup("Native selected tags", new(() => values, next => values = next), items);
        using var literalTag = Fluent.Tag("정##세일");
        string? inspected = null;
        using var status = Fluent.Label("Inspected : none");
        using var interactive = new UiFluentTagGroup("Native interactive tags", new(() => values, next => values = next), items,
            new(DismissLabel: "Dismiss"), activated: next => { inspected = next; status.Text = "Inspected : " + next; });
        string[] emphasizedValues = [];
        using var emphasizedGroup = new UiFluentTagGroup("Native emphasized tags", new(() => values, next => values = next), items,
            new(DismissLabel: "Unlist"), emphasized: new(() => emphasizedValues, next => emphasizedValues = next));
        var emphasized = false;
        using var controlled = Fluent.InteractionTag("Native emphasis", () => { });
        controlled.Selected = new(() => emphasized, next => emphasized = next);
        var hidden = false;
        var finished = false;
        var opened = false;
        using var controls = Dux.Wrap(new(Spacing: 8, RunSpacing: 8),
            Fluent.Button("Hide dropdowns", () => hidden = true), Fluent.Button("Finish", () => finished = true));
        var ready = false;
        var limit = Environment.TickCount64 + 60000;
        while (!finished && !platform.ShouldClose && Environment.TickCount64 < limit)
        {
            platform.PollEvents();
            single.Options = single.Options with
            {
                Invalid = value != "a"
            };
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
                    tags.Render(ui);
                    interactive.Render(ui);
                    literalTag.Render(ui);
                    status.Render(ui);
                    controlled.Render(ui);
                    emphasizedGroup.Render(ui);
                }
            }));
            using var data = context.GetDrawData();
            opened |= single.IsOpen || multiple.IsOpen;
            if (!ready)
            {
                ready = true;
                Console.WriteLine("UIA_DROPDOWN_HOST_READY " + platform.WindowHandle);
                Console.Out.Flush();
            }
            Thread.Sleep(1);
        }
        check(finished && hidden && opened, "external Dropdown client opens and removes actual selection controls");
        check(value == "a" && values.SequenceEqual(new[] { "n" }), "external Dropdown single and multiple operations commit real bound values");
        check(emphasizedValues.SequenceEqual(new[] { "n" }), "external group emphasis survives visibility dismissal and restoration");
        check(emphasized, "external InteractionTag Toggle changes the actual controlled binding");
        check(inspected == "n", "external InteractionTag primary callback receives the canonical value");
    }
    private sealed class Screen(Action<UiImmediateContext> draw) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => draw(ui);
    }
}
