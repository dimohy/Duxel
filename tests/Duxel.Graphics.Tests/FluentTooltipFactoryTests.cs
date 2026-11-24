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
        check(!tooltip.IsOpen && target.Node.GetHelpText() == tooltip.Text && target.FirstChild is null,
            "click dismissal removes the tooltip surface and keeps the updated help text");
        Frame(.01f, new(-100, -100));
        Frame(.01f, new(50, 110));
        Frame(.26f, new(50, 110));
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
        VerifyExpandedMenuTrigger(check);
        VerifyExpandedPopoverTrigger(check, trapFocus : false);
        VerifyExpandedPopoverTrigger(check, trapFocus : true);
        VerifyDialogFocusRestoration(check);
        VerifyExplicitFocusRequests(check);
    }

    private static void VerifyExpandedMenuTrigger(Action<bool, string> check)
    {
        using var tree = new UiAccessibilityTree();
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        context.SetAccessibilityTree(tree);
        var menu = Fluent.Menu("More actions", [new("open", "Open")], static _ => { });
        using var tooltip = Fluent.Tooltip(menu, "Show actions", new() { ShowDelaySeconds = 0 });
        var screen = new Screen(ui =>
        {
            ui.EnableRootViewportContentLayout(contentPadding : 0);
            ui.SetCursorScreenPos(new(40, 100));
            tooltip.Render(ui);
        });
        void Frame(UiInputState input)
        {
            context.NewFrame(new(.02f, new(320, 240), new(1, 1)), input, new(0, 0, 320, 240), UiTextSettings.Default);
            context.Render(screen);
            using var draw = context.GetDrawData();
        }
        var pointer = new UiInputState(new(50, 110), false, false, false, false, false, false,
            0, 0, [], [], default, default);
        Frame(pointer);
        if (!tree.TryGet(tree.Root.First!, out var trigger))
            throw new InvalidOperationException("The menu trigger was not published.");
        check(tooltip.IsOpen && !menu.IsOpen && trigger.Node.Expansion == UiAccessibilityExpansionState.Collapsed,
            "Tooltip shows for its collapsed Fluent Menu trigger");
        check(tree.RequestAction(trigger.Node.Id, trigger.RuntimeId, UiAccessibilityAction.Expand, wait : false)
            == UiAccessibilityResult.Success, "Tooltip's real menu trigger accepts Expand");
        Frame(pointer);
        check(menu.IsOpen && !tooltip.IsOpen && tree.TryGet(trigger.Node.Id, out var expanded)
            && expanded.Node.Expansion == UiAccessibilityExpansionState.Expanded,
            "Opening the wrapped Fluent Menu removes its tooltip in the same frame");
        Frame(pointer);
        check(menu.IsOpen && !tooltip.IsOpen, "Steady pointer hover does not show a tooltip over its expanded menu trigger");
        Frame(pointer with { KeyEvents = [new(UiKey.Escape, true, default)] });
        check(!menu.IsOpen, "Escape reaches the expanded Fluent Menu while its tooltip is hidden");
        Frame(pointer with { KeyEvents = [new(UiKey.Escape, false, default)] });
        check(tooltip.IsOpen, "The collapsed Fluent Menu trigger can show its tooltip again");
    }

    private static void VerifyExpandedPopoverTrigger(Action<bool, string> check, bool trapFocus)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        var popover = Fluent.Popover("Options", Fluent.Button("Popover action"),
            options : new(MotionEnabled : false, TrapFocus : trapFocus));
        using var tooltip = Fluent.Tooltip(popover, "Show options", new() { ShowDelaySeconds = 0 });
        var screen = new Screen(ui =>
        {
            ui.EnableRootViewportContentLayout(contentPadding : 0);
            ui.SetCursorScreenPos(new(40, 100));
            tooltip.Render(ui);
        });
        void Frame(UiInputState input)
        {
            context.NewFrame(new(.02f, new(320, 240), new(1, 1)), input, new(0, 0, 320, 240), UiTextSettings.Default);
            context.Render(screen);
            using var draw = context.GetDrawData();
        }
        var pointer = new UiInputState(new(50, 110), false, false, false, false, false, false,
            0, 0, [], [], default, default);
        var mode = trapFocus ? "modal" : "nonmodal";
        Frame(pointer);
        check(tooltip.IsOpen && !popover.IsOpen, $"Tooltip shows for its closed {mode} Fluent Popover trigger");
        Frame(pointer with { LeftMouseDown = true, LeftMousePressed = true });
        Frame(pointer with { LeftMouseReleased = true });
        check(popover.IsOpen && !tooltip.IsOpen, $"Opening the {mode} Fluent Popover removes its tooltip in the same frame");
        Frame(pointer);
        check(popover.IsOpen && !tooltip.IsOpen, $"Steady hover keeps the tooltip hidden while the {mode} Popover is open");
        Frame(pointer with { KeyEvents = [new(UiKey.Escape, true, default)] });
        check(!popover.IsOpen, $"Escape reaches the open {mode} Fluent Popover while its tooltip is hidden");
        Frame(pointer with { KeyEvents = [new(UiKey.Escape, false, default)] });
        check(!tooltip.IsOpen, $"Closing the {mode} Popover keeps the clicked trigger tooltip hidden while the pointer stays");
        Frame(pointer with { MousePosition = new(-100, -100) });
        Frame(pointer);
        check(tooltip.IsOpen, $"The closed {mode} Popover trigger shows its tooltip after pointer reentry");
    }

    private static void VerifyDialogFocusRestoration(Action<bool, string> check)
    {
        using var tree = new UiAccessibilityTree();
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        context.SetAccessibilityTree(tree);
        using var dialog = Fluent.Dialog("Settings", Fluent.Button("Dialog action"), options : new(MotionEnabled : false));
        using var tooltip = Fluent.Tooltip(Fluent.Button("Open settings", dialog.Show), "Show settings",
            new() { ShowDelaySeconds = 0, HideDelaySeconds = 0 });
        using var other = Fluent.Button("Another control");
        var screen = new Screen(ui =>
        {
            ui.EnableRootViewportContentLayout(contentPadding : 0);
            ui.SetCursorScreenPos(new(40, 100));
            tooltip.Render(ui);
            dialog.Render(ui);
            other.Render(ui);
        });
        void Frame(UiInputState input)
        {
            context.NewFrame(new(.02f, new(640, 480), new(1, 1)), input, new(0, 0, 640, 480), UiTextSettings.Default);
            context.Render(screen);
            using var draw = context.GetDrawData();
        }
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false,
            0, 0, [], [], default, default);
        var pointer = idle with { MousePosition = new(50, 110) };
        Frame(idle);
        Frame(pointer with { LeftMouseDown = true, LeftMousePressed = true });
        Frame(pointer with { LeftMouseReleased = true });
        Frame(idle);
        check(dialog.IsOpen && !tooltip.IsOpen, "Pointer activation opens the real Dialog and hides its trigger tooltip");
        check(dialog.Close(), "The pointer-opened Dialog accepts programmatic Close");
        Frame(idle);
        Frame(idle);
        var trigger = tree.CaptureSnapshot().Elements.Single(item => item.Node.Name == "Open settings");
        check(trigger.Node.Focused && !context.State.NavCursorVisible,
            "Programmatic Dialog close restores its trigger without turning on keyboard navigation");
        check(!tooltip.IsOpen, "Programmatic Dialog focus restoration without keyboard navigation does not open the tooltip");
        Frame(pointer);
        check(tooltip.IsOpen, "Pointer hover still opens the restored Dialog trigger's tooltip");
        Frame(idle);
        check(!tooltip.IsOpen, "Leaving the restored Dialog trigger closes its tooltip despite retaining focus");
        Frame(idle with { KeyEvents = [new(UiKey.Tab, true, default)] });
        Frame(idle with { KeyEvents = [new(UiKey.Tab, false, default)] });
        Frame(idle with { KeyEvents = [new(UiKey.Tab, true, KeyModifiers.Shift)] });
        Frame(idle);
        check(tree.TryGet(trigger.Node.Id, out trigger) && trigger.Node.Focused && tooltip.IsOpen,
            "Keyboard navigation back to the Dialog trigger opens its tooltip");
    }

    private static void VerifyExplicitFocusRequests(Action<bool, string> check)
    {
        using var tree = new UiAccessibilityTree();
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        context.SetAccessibilityTree(tree);
        var button = new FocusRequestButton();
        using var tooltip = Fluent.Tooltip(button, "Focus help", new() { ShowDelaySeconds = 0, HideDelaySeconds = 0 });
        var screen = new Screen(ui =>
        {
            ui.EnableRootViewportContentLayout(contentPadding : 0);
            tooltip.Render(ui);
        });
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false,
            0, 0, [], [], default, default);
        void Frame()
        {
            context.NewFrame(new(.02f, new(320, 240), new(1, 1)), idle, new(0, 0, 320, 240), UiTextSettings.Default);
            context.Render(screen);
            using var draw = context.GetDrawData();
        }
        button.AfterRender = static ui => ui.SetItemDefaultFocus();
        Frame();
        button.AfterRender = null;
        Frame();
        var target = tree.CaptureSnapshot().Elements.Single(item => item.Node.Name == "Focus target");
        check(target.Node.Focused && !context.State.NavCursorVisible && !tooltip.IsOpen,
            "Default item focus outside keyboard navigation keeps the tooltip closed");
        button.AfterRender = static ui => ui.SetKeyboardFocusHere();
        Frame();
        button.AfterRender = null;
        check(tooltip.IsOpen && context.State.NavCursorVisible,
            "Explicit keyboard focus can show the tooltip on the same target");
        context.State.FocusedId = null;
        context.State.NavCursorVisible = false;
        Frame();
        check(!tooltip.IsOpen, "Clearing explicit focus closes the tooltip");
        check(tree.RequestAction(target.Node.Id, target.RuntimeId, UiAccessibilityAction.Focus, wait : false)
            == UiAccessibilityResult.Success, "Wrapped button accepts an accessibility focus request");
        Frame();
        check(tooltip.IsOpen && tree.TryGet(target.Node.Id, out target) && target.Node.Focused,
            "Accessibility focus outside an overlay still shows the tooltip");
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

    private sealed class FocusRequestButton() : UiFluentButton("Focus target")
    {
        internal Action<UiImmediateContext>? AfterRender { get; set; }
        protected override void RenderControl(UiImmediateContext ui)
        {
            base.RenderControl(ui);
            AfterRender?.Invoke(ui);
        }
    }

    private sealed class InheritedTooltip(UiWidget target, string text) : UiFluentTooltip(target, text);
    private sealed class Screen(Action<UiImmediateContext> draw) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => draw(ui);
    }
}
