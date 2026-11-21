using Duxel.Core;

internal static class FluentTooltipFactoryTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var tree = new UiAccessibilityTree();
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        context.SetAccessibilityTree(tree);
        var clicks = 0;
        var button = Fluent.Button("Run", () => clicks++);
        button.Size = new(100, 32);
        using var tooltip = Fluent.Tooltip(button, "Run the current action");
        check(tooltip is UiTooltip<UiHoverFocusTooltipBehavior, UiFluentTooltipPresentation>
            && ReferenceEquals(tooltip.Target, button),
            "Fluent Tooltip factory statically connects the existing behavior, presentation and owned target");
        var include = true;
        void Frame(float seconds, UiVector2 pointer, bool down = false, bool pressed = false, bool released = false)
        {
            var input = new UiInputState(pointer, down, false, pressed, released, false, false, 0, 0, [], [], default, default);
            context.NewFrame(new(seconds, new(320, 240), new(1, 1)), input, new(0, 0, 320, 240), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding : 0);
                ui.SetCursorScreenPos(new(40, 100));
                if (include)
                    tooltip.Render(ui);
            }));
            using var draw = context.GetDrawData();
        }
        Frame(.01f, new(-100, -100));
        tree.TryGet(tree.Root.First!, out var target);
        check(!tooltip.IsOpen && target.Node.GetHelpText() == tooltip.Text && target.FirstChild is null,
            "generated Fluent Tooltip keeps hidden description semantics without publishing a surface");
        Frame(.01f, new(50, 110));
        check(!tooltip.IsOpen, "generated Fluent Tooltip uses the default hover delay");
        Frame(.26f, new(50, 110));
        tree.TryGet(target.Node.Id, out target);
        tree.TryGet(target.FirstChild!, out var surface);
        check(tooltip.IsOpen && surface.Node.Role == UiAccessibilityRole.Tooltip && surface.Node.Name == tooltip.Text,
            "generated Fluent Tooltip publishes its real owned ToolTip surface after the deadline");
        Frame(.01f, new(50, 110), down : true, pressed : true);
        Frame(.01f, new(50, 110), released : true);
        check(clicks == 1, "generated Fluent Tooltip preserves the real target pointer callback exactly once");
        tooltip.Text = "Updated action help";
        Frame(.01f, new(50, 110));
        tree.TryGet(target.Node.Id, out target);
        tree.TryGet(target.FirstChild!, out surface);
        check(target.Node.GetHelpText() == tooltip.Text && surface.Node.Name == tooltip.Text,
            "generated Fluent Tooltip updates the target description and visible surface together");
        include = false;
        Frame(.01f, new(-100, -100));
        check(!tooltip.IsOpen && tree.Count == 0,
            "removing the generated Tooltip from its frame releases its visible and accessible surfaces");
        tooltip.Dispose();
        var disposed = false;
        try { button.RequestRebuild(); }
        catch (ObjectDisposedException) { disposed = true; }
        check(disposed, "disposing the generated Tooltip disposes its exclusively owned target");

        using var inherited = new InheritedTooltip(Fluent.Button("Inherited"), "Inherited help");
        check(inherited.Target is UiFluentButton && inherited.Text == "Inherited help",
            "the generated Fluent Tooltip class remains inheritable through its concrete constructor");

        VerifyHandoverDelay(check, controlled : false);
        VerifyHandoverDelay(check, controlled : true);
    }

    private static void VerifyHandoverDelay(Action<bool, string> check, bool controlled)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var first = Fluent.Tooltip(Fluent.Button("First"), "First help", new() { ShowDelaySeconds = 0 });
        using var next = Fluent.Tooltip(Fluent.Button("Next"), "Next help",
            new() { ShowDelaySeconds = .75, UseShowDelayOnHandover = true });
        var requested = false;
        if (controlled)
        {
            next.Visible = false;
            next.VisibleChangeRequested = change => requested = change.Visible;
        }

        var screen = new Screen(ui =>
        {
            ui.EnableRootViewportContentLayout(contentPadding : 0);
            ui.SetCursorScreenPos(new(32, 32));
            first.Render(ui);
            ui.SetCursorScreenPos(new(180, 32));
            next.Render(ui);
        });
        var source = new UiVector2(48, 46);
        var destination = new UiVector2(192, 46);
        bool Opened() => controlled ? requested : next.IsOpen;
        void Frame(float seconds, UiVector2 point)
        {
            var input = new UiInputState(point, false, false, false, false, false, false, 0, 0, [], [], default, default);
            context.NewFrame(new(seconds, new(320, 240), new(1, 1)), input, new(0, 0, 320, 240), UiTextSettings.Default);
            context.Render(screen);
            using var draw = context.GetDrawData();
        }

        Frame(.01f, source);
        check(first.IsOpen, "Tooltip handover begins with a visible source tooltip");
        Frame(.01f, destination);
        Frame(.74f, destination);
        check(!Opened(), $"The delayed {(controlled ? "controlled request" : "automatic tooltip")} waits after a handover");
        Frame(.02f, destination);
        check(Opened(), $"The delayed {(controlled ? "controlled request" : "automatic tooltip")} opens at its deadline");
    }

    private sealed class InheritedTooltip(UiWidget target, string text) : UiFluentTooltip(target, text);
    private sealed class Screen(Action<UiImmediateContext> draw) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => draw(ui);
    }
}
