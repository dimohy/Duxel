using Duxel.Core;

internal static class InfoLabelRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check,
        Action<UiInputState> setInput, UiContext context, UiFluentColorScheme scheme)
    {
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        using var label = new UiInfoLabel("Name", Fluent.Button("Info action"), informationLabel: "information");
        label.PopoverOptions = label.PopoverOptions with { MotionEnabled = false };
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var clip = new UiRect(0, 0, 320, 240);
        void Draw(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            var control = new UiControlContext(ui);
            control.Frame(new(0, 0, 320, 240), scheme == UiFluentColorScheme.Light ? new(255, 255, 255) : new(32, 32, 32), default, 0, 0);
            var size = label.MeasureConstrained(ui, new(0, 200, 0, 200));
            ui.RenderLayoutChild(label, new(20, 100, size.X, size.Y), clip);
        }
        List<UiAccessibilityElement> Nodes()
        {
            var nodes = new List<UiAccessibilityElement>();
            void Visit(string? id)
            {
                while (id is not null && tree.TryGet(id, out var element))
                {
                    nodes.Add(element);
                    Visit(element.FirstChild);
                    id = element.Next;
                }
            }
            Visit(tree.Root.First);
            return nodes;
        }
        foreach (var size in Enum.GetValues<UiLabelSize>())
        {
            label.Options = new(Size: size);
            setInput(idle);
            var rest = capture(size + "-rest", Draw);
            check(rest.AsSpan().SequenceEqual(capture(size + "-replay", Draw)), "GPU InfoLabel stable replay preserves exact pixels : " + size);
            var trigger = Nodes().Single(e => e.Node.Role == UiAccessibilityRole.Button && e.Node.Name == "Name information");
            var edge = size == UiLabelSize.Small ? 20 : 24;
            check(trigger.Node.Bounds.Width == edge && trigger.Node.Bounds.Height == edge,
                "GPU InfoLabel preserves its complete official button target : " + size);
            var pointer = idle with { MousePosition = new(trigger.Node.Bounds.X + edge / 2, 110) };
            setInput(pointer);
            var hovered = capture(size + "-hover", Draw);
            check(!rest.AsSpan().SequenceEqual(hovered) && context.State.MouseCursor == UiMouseCursor.Hand,
                "GPU InfoLabel hover switches to the filled icon, brand color and pointer cursor : " + size);
            setInput(pointer with { LeftMouseDown = true, LeftMousePressed = true });
            capture(size + "-press", Draw);
            check(!label.IsOpen, "GPU InfoLabel press waits for release : " + size);
            setInput(pointer with { LeftMouseReleased = true });
            var opened = capture(size + "-open", Draw);
            check(label.IsOpen && Nodes().Any(e => e.Node.Role == UiAccessibilityRole.Note)
                && Nodes().Any(e => e.Node.Name == "Info action") && !opened.AsSpan().SequenceEqual(hovered),
                "GPU InfoLabel release renders the real Note, arrow, shadow and content : " + size);
            setInput(idle with { KeyEvents = [new(UiKey.Escape, true, default)] });
            capture(size + "-escape", Draw);
            check(!label.IsOpen && !Nodes().Any(e => e.Node.Role == UiAccessibilityRole.Note),
                "GPU InfoLabel Escape retires its actual supplemental surface : " + size);
            setInput(idle);
            var closed = capture(size + "-closed", Draw);
            check(closed.AsSpan().SequenceEqual(capture(size + "-closed-replay", Draw))
                && context.State.NextScheduledFrameTimeSeconds is null && !context.State.HasActiveAnimations,
                "GPU InfoLabel settles to exact static pixels and event wait : " + size);
        }
        clip = default;
        setInput(idle with { MousePosition = new(60, 110) });
        capture("fully-clipped", Draw);
        check(context.State.MouseCursor == UiMouseCursor.Arrow && Nodes().Where(e => e.Node.Role == UiAccessibilityRole.Button).All(e => e.Node.Offscreen),
            "GPU clipped InfoLabel neither reveals a live hover nor a visible information target");
        context.SetAccessibilityTree(null);
        setInput(idle);
    }
}
