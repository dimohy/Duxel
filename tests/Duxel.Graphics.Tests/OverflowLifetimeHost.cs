using Duxel.Core;
using Duxel.Platform.Windows;

internal static class OverflowLifetimeHost
{
    internal static void Run(Action<bool, string> check)
    {
        using var platform = new WindowsPlatformBackend(new()
        {
            Width = 640, Height = 400, Title = "Duxel owned overflow lifetime fixture",
            KeyRepeatSettingsProvider = new WindowsKeyRepeatSettingsProvider(), TrayOptions = new()
        });
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        context.SetAccessibilityTree(platform.AccessibilityTree);
        var transient = new RetainedButton();
        transient.Size = new(100, 32);
        var pinned = Fluent.Button("Pinned");
        pinned.Size = new(100, 32);
        var overflow = Dux.Overflow([transient, pinned], Fluent.Badge("Hidden"), new(Spacing : 8), [0, 2]);
        using var box = Dux.SizedBox(overflow, width : 400, height : 48);
        var hidden = false;
        var restored = false;
        var finish = false;
        using var remove = Fluent.Button("Remove", () => { box.Options = box.Options with { Width = 150 }; hidden = true; });
        using var restore = Fluent.Button("Restore", () => { box.Options = box.Options with { Width = 400 }; restored = true; });
        using var done = Fluent.Button("Finish", () => finish = true);
        var screen = new Screen(ui =>
        {
            ui.EnableRootViewportContentLayout(contentPadding : 16);
            box.Render(ui);
            remove.Render(ui);
            restore.Render(ui);
            done.Render(ui);
        });
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var deadline = Environment.TickCount64 + 20000;
        var ready = false;
        while (!finish && !platform.ShouldClose && Environment.TickCount64 < deadline)
        {
            platform.PollEvents();
            context.NewFrame(new(1f / 60, new(640, 400), new(1, 1)), idle, new(0, 0, 640, 400), UiTextSettings.Default);
            context.Render(screen);
            using var draw = context.GetDrawData();
            if (!ready)
            {
                ready = true;
                Console.WriteLine("UIA_OVERFLOW_HOST_READY " + platform.WindowHandle);
                Console.Out.Flush();
            }
            Thread.Sleep(1);
        }
        check(finish && hidden && restored, "external client completes actual overflow hide and restore through the owned native window");
        check(transient.Disposals == 0 && ReferenceEquals(overflow.Items[0], transient),
            "overflow provider retirement never disposes or replaces the actual hidden widget");
        box.Dispose();
        check(transient.Disposals == 1, "the retained overflow widget is disposed once with its real owner");
    }
    private sealed class RetainedButton() : UiFluentButton("Transient")
    {
        internal int Disposals;
        protected override void OnDispose()
        {
            Disposals++;
            base.OnDispose();
        }
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
