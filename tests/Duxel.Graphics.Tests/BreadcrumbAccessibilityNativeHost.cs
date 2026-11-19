using Duxel.Core;
using Duxel.Platform.Windows;

internal static class BreadcrumbAccessibilityNativeHost
{
    internal static void Run(Action<bool, string> check)
    {
        using var platform = new WindowsPlatformBackend(new()
        {
            Width = 640,
            Height = 480,
            Title = "Duxel owned Breadcrumb fixture",
            KeyRepeatSettingsProvider = new WindowsKeyRepeatSettingsProvider(),
            TrayOptions = new()
        });
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        context.SetAccessibilityTree(platform.AccessibilityTree);
        var calls = new List<string>();
        var hidden = false;
        var finished = false;
        var collapsed = false;
        var opened = false;
        var narrow = 0f;
        using var path = Fluent.Breadcrumb("Native breadcrumb", [new("root", "Root"), new("disabled", "Disabled", false),
            new("folder", "Folder"), new("current", "Current")], calls.Add);
        using var root = Dux.ConstrainedBox(path, new(0, 600, 0, 300));
        using var controls = Dux.Wrap(new(Spacing: 8, RunSpacing: 8),
            Fluent.Button("Collapse path", () => { root.Options = root.Options with { MaxWidth = narrow }; collapsed = true; }),
            Fluent.Button("Change current status", () => path.Options = path.Options with { CurrentLabel = "Current project location" }),
            Fluent.Button("Hide path", () => hidden = true), Fluent.Button("Finish", () => finished = true));
        var ready = false;
        var limit = Environment.TickCount64 + 60000;
        while (!finished && !platform.ShouldClose && Environment.TickCount64 < limit)
        {
            platform.PollEvents();
            context.NewFrame(new(1f / 60, new(640, 480), new(1, 1)),
                new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default), new(0, 0, 640, 480), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding: 0); controls.Render(ui); if (!hidden) root.Render(ui); }));
            using var data = context.GetDrawData();
            opened |= path.IsOverflowOpen;
            if (!ready)
            {
                float first = 0, last = 0;
                void Visit(string? id)
                {
                    while (id is not null && platform.AccessibilityTree.TryGet(id, out var item))
                    {
                        if (item.Node.Name == "Root" && item.Node.Role == UiAccessibilityRole.Button)
                            first = item.Node.Bounds.Width;
                        if (item.Node.Name == "Current" && item.Node.Role == UiAccessibilityRole.Label)
                            last = item.Node.Bounds.Width;
                        Visit(item.FirstChild);
                        id = item.Next;
                    }
                }
                Visit(platform.AccessibilityTree.Root.First);
                narrow = first + last + 65;
                check(first > 0 && last > 0, "native Breadcrumb fixture measures actual first/current level widths");
                ready = true;
                Console.WriteLine("UIA_BREADCRUMB_HOST_READY " + platform.WindowHandle);
                Console.Out.Flush();
            }
            Thread.Sleep(1);
        }
        check(finished && hidden && collapsed && opened, "external Breadcrumb client opens overflow and removes the actual path");
        check(calls.SequenceEqual(new[] { "root", "folder" }), "external Breadcrumb client invokes enabled levels and never its disabled/current entries");
    }
    private sealed class Screen(Action<UiImmediateContext> draw) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => draw(ui);
    }
}
