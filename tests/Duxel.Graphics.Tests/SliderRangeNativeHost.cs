using Duxel.Core;
using Duxel.Platform.Windows;
using System.Text.Json;

internal static class SliderRangeNativeHost
{
    internal static void Run(Action<bool, string> check)
        => RunCore(check, null);

    internal static void RunRetirementProbe(Action<bool, string> check, string directory)
        => RunCore(check, Path.GetFullPath(directory));

    private static void RunCore(Action<bool, string> check, string? directory)
    {
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
            if (Directory.EnumerateFileSystemEntries(directory).Any())
                throw new InvalidOperationException("The retirement probe requires an empty coordination directory.");
        }
        using var platform = new WindowsPlatformBackend(new WindowsPlatformBackendOptions
        {
            Width = 640, Height = 640, Title = "Duxel owned Slider RangeValue fixture",
            KeyRepeatSettingsProvider = new WindowsKeyRepeatSettingsProvider(), TrayOptions = new()
        });
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        context.SetAccessibilityTree(platform.AccessibilityTree);
        var value = 35f;
        var writes = new List<float>();
        using var slider = Fluent.Slider("Native range", new UiBinding<float>(() => value, next => { value = next; writes.Add(next); }), 10, 110, 5);
        slider.Size = new(220, 32);
        using var disabledSlider = Fluent.Slider("Disabled range", new UiState<float>(50), 0, 100, 1);
        var disabled = false;
        var visible = true;
        var finish = false;
        using var configure = Fluent.Button("Reconfigure range", () => slider.Options = new(0, 200, 10, Vertical : true, Reversed : true));
        using var disable = Fluent.Button("Disable range", () => disabled = true);
        using var remove = Fluent.Button("Remove range", () => visible = false);
        using var done = Fluent.Button("Finish", () => finish = true);
        var screen = new Screen(ui =>
        {
            ui.EnableRootViewportContentLayout(contentPadding : 16);
            if (visible) { ui.BeginDisabled(disabled); slider.Render(ui); ui.EndDisabled(); }
            ui.BeginDisabled(); disabledSlider.Render(ui); ui.EndDisabled();
            configure.Render(ui); disable.Render(ui); remove.Render(ui); done.Render(ui);
        });
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var ready = false;
        var removalSaved = false;
        var drainSaved = false;
        void SaveState(string name)
        {
            var storage = platform.AccessibilityStorage;
            var state = new SliderRetirementState(storage.Cached, storage.Retired, storage.Disconnected, storage.AlreadyUnavailable);
            var path = Path.Combine(directory!, name);
            File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(state, SliderRetirementJson.Default.SliderRetirementState));
            File.Move(path + ".tmp", path);
            Console.WriteLine("SliderRetirementState : " + name + " / " + state);
        }
        var deadline = Environment.TickCount64 + 20000;
        while (!finish && !platform.ShouldClose && Environment.TickCount64 < deadline)
        {
            platform.PollEvents();
            context.NewFrame(new(1f / 60, new(640, 640), new(1, 1)), idle, new(0, 0, 640, 640), UiTextSettings.Default);
            context.Render(screen);
            using var data = context.GetDrawData();
            if (directory is not null && !visible)
            {
                if (!removalSaved) { SaveState("removed.json"); removalSaved = true; }
                var storage = platform.AccessibilityStorage;
                if (!drainSaved && storage.Retired == 0 && storage.Disconnected + storage.AlreadyUnavailable > 0)
                { SaveState("drained.json"); drainSaved = true; }
            }
            if (!ready)
            {
                ready = true;
                Console.WriteLine("UIA_SLIDER_RANGE_HOST_READY " + platform.WindowHandle);
                Console.Out.Flush();
            }
            Thread.Sleep(1);
        }
        check(finish, "external RangeValue client completes the actual native Slider host");
        if (directory is not null)
        {
            check(!visible && writes.Count == 0 && value == 35, "retirement probe changes no numeric binding while removing the actual Slider");
            check(drainSaved, "retirement probe confirms queued native provider disconnection finished");
            return;
        }
        check(value == 70 && writes.SequenceEqual(new float[] { 65, 75, 70 }),
            "external RangeValue actions use exact shared snapping and callback sequence without an idempotent write");
        check(disabled && !visible, "external client exercises live disabled state and provider removal on the real Slider");
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    { public override void Render(UiImmediateContext ui) => render(ui); }
}
