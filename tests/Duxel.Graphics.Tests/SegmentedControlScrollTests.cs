using Duxel.Core;

internal static class SegmentedControlScrollTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        context.SetDesign(UiCompiledDesign.Windows11Dark);
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        var value = new UiState<string?>("0");
        var writes = 0;
        var binding = new UiBinding<string?>(() => value.Value, next => { writes++; value.Value = next; });
        var items = Enumerable.Range(0, 8).Select(i => new UiSegmentOption(i.ToString(), "Segment " + i)).ToArray();
        using var widget = new UiSegmentedControlWidget("Scrolling segments", binding, items,
            new(Width : 220, ReducedMotion : true, IsFixedWidth : false));
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(UiInputState? input = null)
        {
            context.NewFrame(new(.016f, new(500, 300), new(1, 1)), input ?? idle, new(0, 0, 500, 300), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding : 0); widget.Render(ui); }));
            using var draw = context.GetDrawData();
        }
        UiAccessibilityElement Item(string name) => tree.CaptureSnapshot().Elements.Single(e => e.Node.Name == name);
        Frame();
        check(widget.MaximumScrollOffset > 400 && widget.ScrollOffset == 0,
            "scrolling segments preserve intrinsic content wider than their viewport");
        check(!widget.Layouts[0].LabelBlock.GetLine(0).Text.EndsWith("…", StringComparison.Ordinal),
            "scrolling segments retain full natural labels instead of squeezing all labels into the viewport");
        var builds = widget.LayoutBuildCount;
        Frame(idle with { MousePosition = new(100, 15), MouseWheelHorizontal = 1 });
        check(widget.ScrollOffset == 48 && widget.LayoutBuildCount == builds,
            "horizontal wheel moves the viewport without rebuilding text layout");
        Frame(idle with { MousePosition = new(100, 15), MouseWheel = -1 });
        check(widget.ScrollOffset == 96, "vertical wheel is routed to horizontal overflow");
        var selected = value.Value;
        Frame(idle with { MousePosition = new(160, 15), LeftMouseDown = true, LeftMousePressed = true });
        Frame(idle with { MousePosition = new(90, 15), LeftMouseDown = true });
        Frame(idle with { MousePosition = new(90, 15), LeftMouseReleased = true });
        check(widget.ScrollOffset == 166 && value.Value == selected,
            "dragging scrolls content and release does not activate the segment beneath the pointer");
        value.Value = "7"; Frame();
        check(widget.ScrollOffset == widget.MaximumScrollOffset && Item("Segment 7").Node.Bounds.Width > 0,
            "programmatic selection reveals the final segment");
        value.Value = "0"; Frame();
        check(widget.ScrollOffset == 0 && Item("Segment 0").Node.Bounds.X >= 0,
            "programmatic selection reveals the first segment");
        var first = Item("Segment 0");
        tree.RequestAction(first.Node.Id, first.RuntimeId, UiAccessibilityAction.Focus, wait : false); Frame();
        for (var i = 0; i < 7; i++)
        {
            Frame(idle with { KeyEvents = [new(UiKey.RightArrow, true, default)] });
            Frame(idle with { KeyEvents = [new(UiKey.RightArrow, false, default)] });
        }
        check(writes == 7, "each keyboard selection invokes the controlled binding exactly once");
        check(value.Value == "7" && Item("Segment 7").Node.Bounds.Width > 0,
            "keyboard navigation reveals offscreen selection before semantic publication");
        widget.Options = widget.Options with { RightToLeft = true }; value.Value = "0"; Frame();
        check(widget.ScrollOffset == widget.MaximumScrollOffset && Item("Segment 0").Node.Bounds.Width > 0,
            "RTL places and reveals the first logical segment at the physical right edge");
        Frame(idle with { MousePosition = new(100, 15), MouseWheelHorizontal = 100 });
        check(widget.ScrollOffset == widget.MaximumScrollOffset, "wheel scrolling clamps at the physical right boundary");
        Frame(); Frame();
        check(!context.State.HasActiveAnimations, "idle scrolling with reduced motion schedules no animation frames");
        widget.Options = widget.Options with { IsFixedWidth = true }; Frame();
        check(widget.MaximumScrollOffset == 0 && widget.ScrollOffset == 0 && widget.ContentWidth == 220,
            "fixed-width mode restores the original fitted layout and clears overflow");
        widget.SetItems(ReadOnlyMemory<UiSegmentOption>.Empty); Frame();
        check(widget.ContentWidth == 0 && widget.MaximumScrollOffset == 0,
            "empty scrolling content clears stale geometry");
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
