using Duxel.Core;

internal static class SegmentedControlTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        context.SetDesign(UiCompiledDesign.Windows11Dark); ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree(); context.SetAccessibilityTree(tree);
        var value = new UiState<string?>(null);
        using var widget = new UiSegmentedControlWidget("Period", value.Binding,
            new UiSegmentOption[] { new("day", "Day"), new("week", "Week", false), new("month", "Month", Unread : true) });
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        UiVector2 Frame(UiInputState? input = null)
        {
            context.NewFrame(new(.016f, new(800, 500), new(1, 1)), input ?? idle, new(0, 0, 800, 500), UiTextSettings.Default);
            var size = default(UiVector2);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding : 0); widget.Render(ui); size = ui.GetItemRectSize(); }));
            using var draw = context.GetDrawData(); return size;
        }
        UiAccessibilityElement Item(string label) => tree.CaptureSnapshot().Elements.Single(e => e.Node.Role == UiAccessibilityRole.RadioButton && e.Node.Name == label);
        void Action(string label, UiAccessibilityAction action)
        {
            var item = Item(label); tree.RequestAction(item.Node.Id, item.RuntimeId, action, wait : false); Frame(); Frame();
        }
        void Key(UiKey key)
        {
            Frame(idle with { KeyEvents = [new(key, true, default)] });
            Frame(idle with { KeyEvents = [new(key, false, default)] });
        }
        var size = Frame();
        check(size.X == 320 && size.Y >= 32 && value.Value is null, "segmented body reserves its natural pill without implicitly selecting a value");
        check(widget.Layouts[0].Bounds.Width == widget.Layouts[2].Bounds.Width
            && MathF.Abs(Item("Day").Node.Bounds.Width - Item("Month").Node.Bounds.Width) <= 1,
            "equal-width segments share the actual allocated width");
        check(!Item("Week").Node.Enabled && Item("Month").Node.Hint == "Unread", "disabled and unread states reach the semantic tree");
        Action("Month", UiAccessibilityAction.Select);
        check(value.Value == "month" && Item("Month").Node.Selected == true && !widget.Items[2].Unread,
            "native selection updates the binding and clears the selected unread marker");
        Action("Day", UiAccessibilityAction.Focus); Key(UiKey.RightArrow);
        check(value.Value == "month", "keyboard navigation skips disabled segments");
        Key(UiKey.RightArrow);
        check(value.Value == "day", "keyboard navigation wraps the single selection");
        widget.Options = widget.Options with { RightToLeft = true }; Frame();
        check(Item("Day").Node.Bounds.X > Item("Month").Node.Bounds.X, "right-to-left segments reverse their physical placement");
        Action("Day", UiAccessibilityAction.Focus); Key(UiKey.LeftArrow);
        check(value.Value == "month", "left arrow follows the physical direction under RTL");
        widget.Options = widget.Options with { EqualWidth = false, Width = 0, ReducedMotion = true }; Frame();
        check(Item("Month").Node.Bounds.Width > Item("Day").Node.Bounds.Width,
            "content-sized segments reserve distinct text widths");
        var target = Item("Day").Node.Bounds;
        var point = new UiVector2(target.X + target.Width / 2, target.Y + target.Height / 2);
        Frame(idle with { MousePosition = point, LeftMouseDown = true, LeftMousePressed = true });
        Frame(idle with { MousePosition = point, LeftMouseReleased = true }); Frame();
        check(value.Value == "day", "pointer release selects the segment hit by both press and release");
        Frame(); check(!context.State.HasActiveAnimations, "reduced-motion selection schedules no perpetual animation");
        var previous = widget.Items.ToArray();
        try { widget.SetItems(new UiSegmentOption[] { new("a", "A"), new("a", "B") }); check(false, "duplicate segment values fail before state mutation"); }
        catch (ArgumentException) { check(widget.Items.SequenceEqual(previous), "duplicate segment values fail before state mutation"); }
        widget.SetItems(new UiSegmentOption[] { new("month", "Month"), new("day", "Day") }); Frame();
        check(value.Value == "day" && Item("Day").Node.Selected == true, "reordering preserves the selected stable value");
        widget.SetItems(ReadOnlyMemory<UiSegmentOption>.Empty);
        check(Frame() == default && !tree.CaptureSnapshot().Elements.Any(e => e.Node.Role == UiAccessibilityRole.RadioButton),
            "empty segments produce neither paint area nor stale semantic items");
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
    internal static void RunControlled(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        context.SetDesign(UiCompiledDesign.Windows11Dark); ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree(); context.SetAccessibilityTree(tree);
        var current = "a"; var accept = false;
        using var widget = new UiSegmentedControlWidget("Controlled", new(() => current, value => { if (accept) current = value!; }),
            new UiSegmentOption[] { new("a", "A"), new("b", "B", Unread : true) }, new(ReducedMotion : true));
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(UiInputState? input = null)
        {
            context.NewFrame(new(.016f, new(500, 300), new(1, 1)), input ?? idle, new(0, 0, 500, 300), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding : 0); widget.Render(ui); }));
            using var draw = context.GetDrawData();
        }
        UiAccessibilityElement B() => tree.CaptureSnapshot().Elements.Single(e => e.Node.Role == UiAccessibilityRole.RadioButton && e.Node.Name == "B");
        Frame(); var b = B(); tree.RequestAction(b.Node.Id, b.RuntimeId, UiAccessibilityAction.Select, wait : false); Frame();
        check(current == "a" && B().Node.Selected == false && widget.Items[1].Unread,
            "rejected native selection preserves accepted semantics and unread state in the same frame");
        var rect = B().Node.Bounds; var point = new UiVector2(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
        Frame(idle with { MousePosition = point, LeftMouseDown = true, LeftMousePressed = true });
        Frame(idle with { MousePosition = point, LeftMouseReleased = true });
        check(current == "a" && B().Node.Selected == false, "rejected pointer selection publishes no transient selected item");
        accept = true; b = B(); tree.RequestAction(b.Node.Id, b.RuntimeId, UiAccessibilityAction.Select, wait : false); Frame();
        check(current == "b" && B().Node.Selected == true && !widget.Items[1].Unread && B().Node.Hint is null,
            "accepted native selection paints the accepted value and clears unread semantics in the same frame");
        var builds = widget.LayoutBuildCount; Frame(); Frame();
        check(widget.LayoutBuildCount == builds && !context.State.HasActiveAnimations,
            "controlled idle selection reuses measured content and stops scheduling frames");
        widget.SetItems(new UiSegmentOption[] { new("a", "A"), new("b", "A very long segment with e\u0301 and 한글") });
        widget.Options = widget.Options with { Width = 160 }; Frame();
        var block = widget.Layouts[1].LabelBlock;
        check(block.LineCount == 1 && block.GetLine(0).Text.EndsWith("…", StringComparison.Ordinal)
            && block.Size.X <= widget.Layouts[1].Bounds.Width - 32
            && tree.CaptureSnapshot().Elements.Any(e => e.Node.Name == "A very long segment with e\u0301 and 한글"),
            "narrow segment labels retain one ellipsized line and their full semantic names");
    }
}
