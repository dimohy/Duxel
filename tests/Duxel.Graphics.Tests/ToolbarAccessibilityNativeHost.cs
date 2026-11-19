using Duxel.Core;
using Duxel.Platform.Windows;

internal static class ToolbarAccessibilityNativeHost
{
    internal static void Run(Action<bool, string> check)
    {
        using var platform = new WindowsPlatformBackend(new()
        {
            Width = 640,
            Height = 480,
            Title = "Duxel owned Toolbar fixture",
            KeyRepeatSettingsProvider = new WindowsKeyRepeatSettingsProvider(),
            TrayOptions = new()
        });
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        context.SetAccessibilityTree(platform.AccessibilityTree);
        var count = 0;
        var selected = new UiState<bool>(false);
        var hidden = false;
        var finished = false;
        using var status = Fluent.Label("Toolbar count : 0");
        var first = new NativeButton("Native first", () => { });
        first.Activated = () => { count++; status.Text = "Toolbar count : " + count; if (count == 2) first.Disabled = true; };
        using var toolbar = Fluent.Toolbar("Native toolbar", [first, Fluent.ToggleButton("Native toggle", selected.Binding), Fluent.Button("Native last", () => { count++; status.Text = "Toolbar count : " + count; })]);
        using var controls = Dux.Wrap(new(Spacing: 8, RunSpacing: 8),
            Fluent.Button("Vertical toolbar", () => toolbar.Options = toolbar.Options with { Vertical = true }),
            Fluent.Button("Disable toolbar first", () => first.Disabled = true),
            Fluent.Button("Hide toolbar", () => hidden = true), Fluent.Button("Finish", () => finished = true));
        var ready = false;
        var limit = Environment.TickCount64 + 60000;
        while (!finished && !platform.ShouldClose && Environment.TickCount64 < limit)
        {
            platform.PollEvents();
            var input = platform.Input.Snapshot;
            context.NewFrame(new(0, new(640, 480), new(1, 1)),
                new(input.MousePosition, input.LeftMouseDown, input.RightMouseDown, input.LeftMousePressedEvent, input.LeftMouseReleasedEvent,
                    input.RightMousePressedEvent, input.RightMouseReleasedEvent, input.MouseWheel, input.MouseWheelHorizontal,
                    input.KeyEvents, input.CharEvents, input.KeyRepeatSettings, input.Modifiers), new(0, 0, 640, 480), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding: 0);
                controls.Render(ui);
                status.Render(ui);
                if (!hidden)
                    toolbar.Render(ui);
            }));
            using var data = context.GetDrawData();
            if (!ready)
            {
                ready = true;
                Console.WriteLine("UIA_TOOLBAR_HOST_READY " + platform.WindowHandle);
                Console.Out.Flush();
            }
            Thread.Sleep(1);
        }
        check(finished && hidden && first.Disabled && toolbar.Options.Vertical, "external Toolbar client completes actual disable/orientation/retirement transitions");
        check(count == 2 && selected.Value, "external Toolbar client changes actual command and toggle models");
    }
    private sealed class NativeButton(string label, Action action) : UiFluentButton(label, action)
    {
        internal bool Disabled;
        protected override void RenderControl(UiImmediateContext ui)
        {
            ui.BeginDisabled(Disabled);
            try
            {
                base.RenderControl(ui);
            }
            finally { ui.EndDisabled(); }
        }
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
