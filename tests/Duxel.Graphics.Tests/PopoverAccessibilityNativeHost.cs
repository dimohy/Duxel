using Duxel.Core;
using Duxel.Platform.Windows;

internal static class PopoverAccessibilityNativeHost
{
    internal static void Run(Action<bool, string> check)
    {
        using var platform = new WindowsPlatformBackend(new()
        {
            Width = 900,
            Height = 600,
            Title = "Duxel owned Popover fixture",
            KeyRepeatSettingsProvider = new WindowsKeyRepeatSettingsProvider(),
            TrayOptions = new()
        });
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        context.SetAccessibilityTree(platform.AccessibilityTree);
        using var page = new Duxel.Gallery.PopoverPage();
        var finished = false;
        using var finish = Fluent.Button("Finish Popover", () => finished = true);
        var ready = false;
        var limit = Environment.TickCount64 + 60000;
        var previousTime = System.Diagnostics.Stopwatch.GetTimestamp();
        var frames = 0;
        var traceLines = 0;
        string? previousState = null;
        void Trace(string message)
        {
            if (++traceLines > 128)
                throw new InvalidOperationException("POPOVER_TRACE_BUDGET: diagnostic transition log exceeded128lines.");
            Console.WriteLine($"POPOVER_TRACE tick={Environment.TickCount64} frame={frames} {message}");
            Console.Out.Flush();
        }
        string Snapshot()
        {
            UiAccessibilityNode? editor = null;
            void Visit(string? id)
            {
                while (id is not null && platform.AccessibilityTree.TryGet(id, out var element))
                {
                    if (element.Node.Role == UiAccessibilityRole.Edit && element.Node.Name == "이름")
                        editor = element.Node;
                    Visit(element.FirstChild);
                    id = element.Next;
                }
            }
            Visit(platform.AccessibilityTree.Root.First);
            var model = page.ModelName;
            return $"open={page.Preview?.IsOpen} mode={page.Preview?.Options.TriggerMode} focus={context.State.FocusedId} active={context.State.ActiveId} "
                + $"editor={editor is not null} editorFocus={editor?.Focused} editorLength={editor?.Value?.Length} editorInitial={editor?.Value == "정세일"} editorUpdated={editor?.Value == "한글 Popover"} "
                + $"modelLength={model?.Length} modelInitial={model == "정세일"} modelUpdated={model == "한글 Popover"}";
        }
        while (!finished && !platform.ShouldClose && Environment.TickCount64 < limit)
        {
            platform.PollEvents();
            var input = platform.Input.Snapshot;
            if (input.KeyEvents.Count > 0 || input.CharEvents.Count > 0 || input.LeftMousePressedEvent || input.LeftMouseReleasedEvent)
                Trace($"input modifiers={input.Modifiers} keys={string.Join(',', input.KeyEvents.Select(k => $"{k.Key}:{k.IsDown}:{k.Modifiers}"))} chars={input.CharEvents.Count} leftDown={input.LeftMouseDown} leftPress={input.LeftMousePressedEvent} leftRelease={input.LeftMouseReleasedEvent} before={Snapshot()}");
            var timestamp = System.Diagnostics.Stopwatch.GetTimestamp();
            var elapsed = (float)System.Diagnostics.Stopwatch.GetElapsedTime(previousTime, timestamp).TotalSeconds;
            previousTime = timestamp;
            context.NewFrame(new(elapsed, new(900, 600), new(1, 1)),
                new(input.MousePosition, input.LeftMouseDown, input.RightMouseDown, input.LeftMousePressedEvent, input.LeftMouseReleasedEvent,
                    input.RightMousePressedEvent, input.RightMouseReleasedEvent, input.MouseWheel, input.MouseWheelHorizontal,
                    input.KeyEvents, input.CharEvents, input.KeyRepeatSettings, input.Modifiers), new(0, 0, 900, 600), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding: 0);
                finish.Render(ui);
                page.Render(ui);
            }));
            using var data = context.GetDrawData();
            var state = Snapshot();
            if (state != previousState)
                Trace("after=" + state);
            previousState = state;
            frames++;
            if (!ready)
            {
                ready = true;
                Console.WriteLine("UIA_POPOVER_HOST_READY " + platform.WindowHandle);
                Console.Out.Flush();
            }
            Thread.Sleep(1);
        }
        check(finished && page.Preview is { IsOpen: false }, "external Popover client closes actual owned surface and finishes");
        check(!page.Preview!.Options.TrapFocus && page.Preview.Options.WithArrow && page.Preview.Options.CloseOnFocusLeave,
            "external Popover client leaves nonmodal focus-leave and arrow options enabled");
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
