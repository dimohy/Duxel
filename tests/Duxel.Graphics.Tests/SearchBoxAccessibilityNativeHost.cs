using Duxel.Core;
using Duxel.Platform.Windows;

internal static class SearchBoxAccessibilityNativeHost
{
    internal static void Run(Action<bool, string> check)
    {
        using var platform = new WindowsPlatformBackend(new()
        {
            Width = 640,
            Height = 480,
            Title = "Duxel owned SearchBox fixture",
            KeyRepeatSettingsProvider = new WindowsKeyRepeatSettingsProvider(),
            TrayOptions = new()
        });
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        context.SetAccessibilityTree(platform.AccessibilityTree);
        var text = "Initial";
        var reject = false;
        using var search = Fluent.SearchBox("Native search", new UiBinding<string>(() => text, value => { if (!reject) text = value; }), 32);
        var finished = false;
        using var controls = Dux.Wrap(new(Spacing: 8, RunSpacing: 8),
            Fluent.Button("Reject search", () => reject = true),
            Fluent.Button("Readonly search", () => search.Options = search.Options with { ReadOnly = true }),
            Fluent.Button("Finish", () => finished = true));
        var ready = false;
        var limit = Environment.TickCount64 + 60000;
        var previousTime = System.Diagnostics.Stopwatch.GetTimestamp();
        while (!finished && !platform.ShouldClose && Environment.TickCount64 < limit)
        {
            platform.PollEvents();
            var input = platform.Input.Snapshot;
            var timestamp = System.Diagnostics.Stopwatch.GetTimestamp();
            var elapsed = (float)System.Diagnostics.Stopwatch.GetElapsedTime(previousTime, timestamp).TotalSeconds;
            previousTime = timestamp;
            context.NewFrame(new(elapsed, new(640, 480), new(1, 1)),
                new(input.MousePosition, input.LeftMouseDown, input.RightMouseDown, input.LeftMousePressedEvent, input.LeftMouseReleasedEvent,
                    input.RightMousePressedEvent, input.RightMouseReleasedEvent, input.MouseWheel, input.MouseWheelHorizontal,
                    input.KeyEvents, input.CharEvents, input.KeyRepeatSettings, input.Modifiers), new(0, 0, 640, 480), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding: 0);
                controls.Render(ui);
                search.Render(ui);
            }));
            using var data = context.GetDrawData();
            if (!ready)
            {
                ready = true;
                Console.WriteLine("UIA_SEARCHBOX_HOST_READY " + platform.WindowHandle);
                Console.Out.Flush();
            }
            Thread.Sleep(1);
        }
        check(finished && reject && search.Options.ReadOnly, "external Search client completes actual readonly and rejected-write transitions");
        check(text == "한글 search", "external Search client commits the accepted model and retains it after rejection");
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
