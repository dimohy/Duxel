using Duxel.Core;
using Duxel.Platform.Windows;

internal static class MarkdownAccessibilityNativeHost
{
    internal static void Run(Action<bool, string> check)
    {
        using var platform = new WindowsPlatformBackend(new()
        {
            Width = 640,
            Height = 480,
            Title = "Duxel owned Markdown link fixture",
            KeyRepeatSettingsProvider = new WindowsKeyRepeatSettingsProvider(),
            TrayOptions = new()
        });
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        context.SetAccessibilityTree(platform.AccessibilityTree);
        var source = "[Native first link](https://example.test/first)\n\n[Native wrapped link with enough text to occupy several visual lines](https://example.test/wrapped)\n\n"
            + string.Concat(Enumerable.Repeat("Body text\n\n", 20)) + "[Native last link](https://example.test/last)";
        using var view = Dux.MarkdownView("Native Markdown viewer", new UiState<string>(source).Binding, new(Width: 240, Height: 180));
        var count = 0;
        var path = string.Empty;
        view.Navigate = uri => { count++; path = uri.AbsolutePath; };
        var hidden = false;
        var disabled = false;
        var finished = false;
        var counter = Fluent.Input("Native navigation count", new UiBinding<string>(() => "Navigation count : " + count,
            _ => throw new InvalidOperationException("The navigation count is read-only.")), 100, 240);
        counter.Options = counter.Options with
        {
            Flags = UiInputTextFlags.ReadOnly
        };
        using var controls = Dux.Column(new(Spacing: 8, MainAxisSize: UiMainAxisSize.Min),
            Dux.Wrap(new(Spacing: 8),
                Fluent.Button("Disable Markdown", () => disabled = true),
                Fluent.Button("Hide Markdown", () => hidden = true),
                Fluent.Button("Finish", () => finished = true)),
            counter);
        var ready = false;
        var pointerTraceCount = 0;
        var previousPointer = new UiVector2(-1, -1);
        var previousDown = false;
        var limit = Environment.TickCount64 + 60000;
        var previous = System.Diagnostics.Stopwatch.GetTimestamp();
        while (!finished && !platform.ShouldClose && Environment.TickCount64 < limit)
        {
            platform.PollEvents();
            var input = platform.Input.Snapshot;
            var now = System.Diagnostics.Stopwatch.GetTimestamp();
            var elapsed = (float)System.Diagnostics.Stopwatch.GetElapsedTime(previous, now).TotalSeconds;
            previous = now;
            var scale = platform.ContentScale;
            var size = platform.FramebufferSize;
            var logical = new UiVector2(size.Width / scale, size.Height / scale);
            if (pointerTraceCount < 24 && (input.LeftMousePressedEvent || input.LeftMouseReleasedEvent
                || input.LeftMouseDown != previousDown || input.LeftMouseDown && input.MousePosition != previousPointer))
            {
                Console.WriteLine($"MARKDOWN_POINTER x={input.MousePosition.X} y={input.MousePosition.Y} scale={scale} down={input.LeftMouseDown} pressed={input.LeftMousePressedEvent} released={input.LeftMouseReleasedEvent} count={count}");
                pointerTraceCount++;
            }
            previousPointer = input.MousePosition;
            previousDown = input.LeftMouseDown;
            context.NewFrame(new(elapsed, logical, new(scale, scale)),
                new(new(input.MousePosition.X / scale, input.MousePosition.Y / scale), input.LeftMouseDown, input.RightMouseDown, input.LeftMousePressedEvent, input.LeftMouseReleasedEvent,
                    input.RightMousePressedEvent, input.RightMouseReleasedEvent, input.MouseWheel, input.MouseWheelHorizontal,
                    input.KeyEvents, input.CharEvents, input.KeyRepeatSettings, input.Modifiers), new(0, 0, logical.X, logical.Y), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding: 0);
                controls.Render(ui);
                if (!hidden)
                {
                    ui.BeginDisabled(disabled);
                    try
                    {
                        view.Render(ui);
                    }
                    finally { ui.EndDisabled(); }
                }
            }));
            using var data = context.GetDrawData();
            if (!ready)
            {
                ready = true;
                Console.WriteLine("UIA_MARKDOWN_HOST_READY " + platform.WindowHandle);
                Console.Out.Flush();
            }
            Thread.Sleep(1);
        }
        check(finished && hidden && disabled, "external Markdown client completes disable, omission and finish transitions");
        check(count == 3 && path == "/last", "external Markdown Invoke, real wrapped-link pointer and keyboard Enter reach exactly three targets");
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
