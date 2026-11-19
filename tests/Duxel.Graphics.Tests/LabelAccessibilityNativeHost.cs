using Duxel.Core;
using Duxel.Platform.Windows;

internal static class LabelAccessibilityNativeHost
{
    internal static void Run(Action<bool, string> check)
    {
        using var platform = new WindowsPlatformBackend(new()
        {
            Width = 640, Height = 480, Title = "Duxel owned standalone Label fixture",
            KeyRepeatSettingsProvider = new WindowsKeyRepeatSettingsProvider(), TrayOptions = new()
        });
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        context.SetAccessibilityTree(platform.AccessibilityTree);
        var value = new UiState<string>("Initial");
        using var input = Fluent.Input("Original label source", value.Binding, 100, 220);
        using var label = Fluent.Label("Native label target", input, new(Required : true));
        var renamed = false;
        var detached = false;
        var disabled = false;
        var enabledAgain = false;
        var finish = false;
        var presses = 0;
        var sawLabelFocus = false;
        using var anchor = Fluent.Button("Label focus anchor");
        using var rename = Fluent.Button("Rename label", () =>
        {
            label.Text = "Renamed native label";
            label.Options = label.Options with { Required = false };
            renamed = true;
        });
        using var disableLabel = Fluent.Button("Disable label", () => label.Options = label.Options with { Disabled = true });
        using var detach = Fluent.Button("Detach label", () =>
        {
            label.Target = null;
            label.Options = label.Options with { Disabled = false };
            detached = true;
        });
        using var disableTarget = Fluent.Button("Disable label target", () => { label.Target = input; disabled = true; });
        using var enableTarget = Fluent.Button("Enable label target", () => { disabled = false; enabledAgain = true; });
        using var pointer = Fluent.Button("Pointer presses : 0");
        using var done = Fluent.Button("Finish", () => finish = true);
        var screen = new Screen(ui =>
        {
            ui.EnableRootViewportContentLayout(contentPadding : 0);
            ui.SetCursorScreenPos(new(20, 20)); label.Render(ui);
            ui.SetCursorScreenPos(new(20, 70));
            ui.BeginDisabled(disabled); input.Render(ui); ui.EndDisabled();
            ui.SetCursorScreenPos(new(20, 120));
            anchor.Render(ui); rename.Render(ui); disableLabel.Render(ui); detach.Render(ui);
            disableTarget.Render(ui); enableTarget.Render(ui); pointer.Render(ui); done.Render(ui);
        });
        var ready = false;
        var previousDown = false;
        var deadline = Environment.TickCount64 + 60000;
        while (!finish && !platform.ShouldClose && Environment.TickCount64 < deadline)
        {
            platform.PollEvents();
            var snapshot = platform.Input.Snapshot;
            var scale = platform.ContentScale;
            var pressed = snapshot.LeftMousePressedEvent || snapshot.LeftMouseDown && !previousDown;
            var released = snapshot.LeftMouseReleasedEvent || !snapshot.LeftMouseDown && previousDown;
            previousDown = snapshot.LeftMouseDown;
            if (pressed) { presses++; pointer.Label = "Pointer presses : " + presses; }
            var size = platform.FramebufferSize;
            var logical = new UiVector2(size.Width / scale, size.Height / scale);
            context.NewFrame(new(1f / 60, logical, new(scale, scale)),
                new(new(snapshot.MousePosition.X / scale, snapshot.MousePosition.Y / scale), snapshot.LeftMouseDown,
                    snapshot.RightMouseDown, pressed, released, snapshot.RightMousePressedEvent, snapshot.RightMouseReleasedEvent,
                    snapshot.MouseWheel, snapshot.MouseWheelHorizontal, snapshot.KeyEvents, snapshot.CharEvents,
                    snapshot.KeyRepeatSettings, snapshot.Modifiers), new(0, 0, logical.X, logical.Y), UiTextSettings.Default);
            context.Render(screen);
            using var draw = context.GetDrawData();
            for (var id = platform.AccessibilityTree.Root.First; id is not null;)
            {
                if (!platform.AccessibilityTree.TryGet(id, out var node)) break;
                sawLabelFocus |= node.Node.Focused && node.Node.Role == UiAccessibilityRole.Edit;
                id = node.Next;
            }
            if (!ready)
            {
                ready = true;
                Console.WriteLine("UIA_LABEL_HOST_READY " + platform.WindowHandle);
                Console.Out.Flush();
            }
            Thread.Sleep(1);
        }
        check(finish && renamed && detached && enabledAgain && !disabled, "external Label client completes rename detach and disable/enable paths");
        check(value.Value == "Native edited value", "external Label client edits the actual bound input");
        check(presses >= 4 && sawLabelFocus, "owned native pointer messages reach Label focus through the real platform input backend");
    }
    private sealed class Screen(Action<UiImmediateContext> action) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => action(ui);
    }
}
