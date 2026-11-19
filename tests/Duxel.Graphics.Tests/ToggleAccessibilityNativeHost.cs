using Duxel.Core;
using Duxel.Platform.Windows;
using System.Runtime.InteropServices;

internal static unsafe class ToggleAccessibilityNativeHost
{
    internal static void Run(Action<bool, string> check)
    {
        using var platform = new WindowsPlatformBackend(new()
        {
            Width = 640, Height = 540, Title = "Duxel owned Toggle state fixture",
            KeyRepeatSettingsProvider = new WindowsKeyRepeatSettingsProvider(), TrayOptions = new()
        });
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        context.SetAccessibilityTree(platform.AccessibilityTree);
        CheckFrozenToggle(platform.WindowHandle, check);
        bool? mixed = null;
        var mixedWrites = new List<bool?>();
        var shared = false;
        var sharedWrites = 0;
        var legacy = false;
        var legacyWrites = 0;
        var disabled = false;
        var visible = true;
        var finish = false;
        var changes = 0;
        platform.AccessibilityTree.ToggleChanged += _ => changes++;
        var binding = new UiBinding<bool>(() => shared, next => { shared = next; sharedWrites++; });
        using var checkbox = Fluent.Checkbox("Mixed checkbox", new UiBinding<bool?>(() => mixed, next => { mixed = next; mixedWrites.Add(next); }));
        using var toggle = Fluent.ToggleButton("Shared toggle", binding);
        using var control = Fluent.Switch("Shared switch", binding);
        using var disabledOption = Fluent.Checkbox("Disabled option", new UiState<bool?>(false));
        using var restore = Fluent.Button("Restore mixed", () => mixed = null);
        using var disable = Fluent.Button("Disable mixed", () => disabled = true);
        using var remove = Fluent.Button("Remove mixed", () => visible = false);
        using var done = Fluent.Button("Finish", () => finish = true);
        var screen = new Screen(ui =>
        {
            ui.EnableRootViewportContentLayout(contentPadding : 16);
            ui.BeginDisabled(disabled);
            if (visible) checkbox.Render(ui);
            ui.EndDisabled();
            control.Render(ui); toggle.Render(ui);
            if (ui.Checkbox("Legacy check", ref legacy)) legacyWrites++;
            ui.BeginDisabled(); disabledOption.Render(ui); ui.EndDisabled();
            restore.Render(ui); disable.Render(ui); remove.Render(ui); done.Render(ui);
        });
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var ready = false;
        var deadline = Environment.TickCount64 + 20000;
        while (!finish && !platform.ShouldClose && Environment.TickCount64 < deadline)
        {
            platform.PollEvents();
            context.NewFrame(new(1f / 60, new(640, 540), new(1, 1)), idle, new(0, 0, 640, 540), UiTextSettings.Default);
            context.Render(screen);
            using var draw = context.GetDrawData();
            if (!ready)
            {
                ready = true;
                Console.WriteLine("UIA_TOGGLE_HOST_READY " + platform.WindowHandle);
                Console.Out.Flush();
            }
            Thread.Sleep(1);
        }
        check(finish && disabled && !visible, "external Toggle client completes disabled-state and removal paths on the actual host");
        check(mixed is null && mixedWrites.SequenceEqual(new bool?[] { true, false }) && !shared && sharedWrites == 2 && legacy && legacyWrites == 1,
            "external Toggle actions preserve exact mixed/shared/legacy callback values and counts");
        check(changes == 8, "native Toggle host publishes exactly eight real state changes with no idle duplicates");
    }

    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }

    private static void CheckFrozenToggle(nint window, Action<bool, string> check)
    {
        using var host = new WindowsAccessibility(window);
        host.Tree.BeginFrame();
        host.Tree.Add(new("frozen-toggle", string.Empty, UiAccessibilityRole.Checkbox, "Frozen mixed", new(0, 0, 120, 24), true, false, false,
            ValuePattern : false) { ToggleState = UiAccessibilityToggleState.Indeterminate });
        host.Tree.EndFrame();
        if (!host.Tree.TryGet("frozen-toggle", out var element)) throw new InvalidOperationException("Frozen Toggle source is missing.");
        var snapshot = host.CaptureEventSource(element, closed : true);
        snapshot.GetPatternProvider(10015, out var pattern);
        if (pattern == 0) throw new InvalidOperationException("Captured Toggle capability must expose a read-only native pattern.");
        try
        {
            host.Dispose();
            snapshot.GetPropertyValue(30041, out var available);
            int state = -1;
            var result = ((delegate* unmanaged[Stdcall]<nint, int*, int>)(*(nint**)pattern)[4])(pattern, &state);
            var mutation = ((delegate* unmanaged[Stdcall]<nint, int>)(*(nint**)pattern)[3])(pattern);
            check(result == 0 && state == 2 && available.Data.Boolean != 0 && mutation == UiaNative.Unavailable,
                "captured event Toggle capability remains readable as mixed while rejecting actions after original tree disposal");
        }
        finally { Marshal.Release(pattern); }
    }
}
