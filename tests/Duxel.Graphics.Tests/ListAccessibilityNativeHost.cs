using Duxel.Core;
using Duxel.Platform.Windows;

internal static class ListAccessibilityNativeHost
{
    internal static void Run(Action<bool, string> check, bool retirementOnly = false)
    {
        using var platform = new WindowsPlatformBackend(new()
        {
            Width = 640,
            Height = 480,
            Title = "Duxel owned List fixture",
            KeyRepeatSettingsProvider = new WindowsKeyRepeatSettingsProvider(),
            TrayOptions = new()
        });
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        context.SetAccessibilityTree(platform.AccessibilityTree);
        var selected = new UiState<string[]>(["a"]);
        var text = new UiState<string>("Initial list");
        using var list = new UiList<UiFluentListPresentation>("Native list", selected.Binding,
        [
            new("a", "Native row A", Fluent.Body1("First content"), selectionLabel: "Select native A"),
            new("b", "Native row B", Fluent.Input("Native list editor", text.Binding, 100, 180), selectionLabel: "Select native B")
        ], new(Navigation: UiListNavigation.Composite, Width: 400));
        var hidden = false;
        var disabled = false;
        var finished = false;
        using var controls = Dux.Wrap(new(Spacing: 8, RunSpacing: 8),
            Fluent.Button("Disable list", () => disabled = true),
            Fluent.Button("Hide list", () => hidden = true),
            Fluent.Button("Finish", () => finished = true));
        using var marker = Fluent.Button("Retirement probe");
        var ready = false;
        var limit = Environment.TickCount64 + 60000;
        var previous = System.Diagnostics.Stopwatch.GetTimestamp();
        while (!finished && !platform.ShouldClose && Environment.TickCount64 < limit)
        {
            platform.PollEvents();
            var input = platform.Input.Snapshot;
            var now = System.Diagnostics.Stopwatch.GetTimestamp();
            var elapsed = (float)System.Diagnostics.Stopwatch.GetElapsedTime(previous, now).TotalSeconds;
            previous = now;
            context.NewFrame(new(elapsed, new(640, 480), new(1, 1)),
                new(input.MousePosition, input.LeftMouseDown, input.RightMouseDown, input.LeftMousePressedEvent, input.LeftMouseReleasedEvent,
                    input.RightMousePressedEvent, input.RightMouseReleasedEvent, input.MouseWheel, input.MouseWheelHorizontal,
                    input.KeyEvents, input.CharEvents, input.KeyRepeatSettings, input.Modifiers), new(0, 0, 640, 480), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding: 0);
                controls.Render(ui);
                if (retirementOnly)
                    marker.Render(ui);
                if (!hidden)
                {
                    ui.BeginDisabled(disabled);
                    try
                    {
                        list.Render(ui);
                    }
                    finally { ui.EndDisabled(); }
                }
            }));
            using var data = context.GetDrawData();
            if (!ready)
            {
                ready = true;
                Console.WriteLine("UIA_LIST_HOST_READY " + platform.WindowHandle);
                Console.Out.Flush();
            }
            Thread.Sleep(1);
        }
        if (retirementOnly)
            check(finished && hidden, "external List retirement probe completes omission and finish");
        else
        {
            check(finished && hidden && disabled, "external List client completes disable, omission and finish transitions");
            check(selected.Value.SequenceEqual(new[] { "b" }) && text.Value == "native list",
                "external List selection and editor Value reach the authoritative model");
        }
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
