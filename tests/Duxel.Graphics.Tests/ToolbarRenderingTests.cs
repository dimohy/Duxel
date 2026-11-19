using Duxel.Core;

internal static class ToolbarRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check, UiContext context)
    {
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        var selected = new UiState<bool>(false);
        var count = 0;
        using var toolbar = Fluent.Toolbar("GPU tools", [Fluent.Button("New", () => count++), Fluent.ToggleButton("Bold", selected.Binding), Fluent.Button("Save", () => count++)]);
        void Draw(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            var paint = new UiControlContext(ui);
            paint.Frame(paint.ClipRect, new(30, 70, 90), default, 0, 0);
            ui.SetCursorScreenPos(new(16, 16));
            toolbar.Render(ui);
        }
        UiAccessibilityElement Node() => tree.TryGet(tree.Root.First!, out var node) ? node : throw new InvalidOperationException("Missing Toolbar");
        foreach (var size in Enum.GetValues<UiToolbarSize>())
        {
            foreach (var vertical in new[] { false, true })
            {
                foreach (var rtl in new[] { false, true })
                {
                    toolbar.Options = new(size, vertical, rtl);
                    var name = $"{size}-{vertical}-{rtl}";
                    var pixels = capture(name, Draw);
                    check(pixels.AsSpan().SequenceEqual(capture(name + "-replay", Draw)), "GPU Toolbar unchanged child pixels replay " + name);
                    check(Node().Node.Orientation == (vertical ? UiAccessibilityOrientation.Vertical : UiAccessibilityOrientation.Horizontal), "GPU Toolbar publishes actual orientation");
                }
            }
        }
        toolbar.Options = new();
        var original = capture("original", Draw);
        selected.Value = true;
        var transitionStart = capture("checked-start", Draw);
        check(original.AsSpan().SequenceEqual(transitionStart) && context.State.NextScheduledFrameTimeSeconds.HasValue,
            "GPU Toolbar bound child toggle schedules a transition from the current pixels");
        context.State.AdvanceTime(UiDisclosureMotion.DurationSeconds);
        var checkedPixels = capture("checked", Draw);
        check(!original.AsSpan().SequenceEqual(checkedPixels), "GPU Toolbar bound child toggle changes actual pixels");
        toolbar.Options = new(RightToLeft: true);
        var reversed = capture("rtl", Draw);
        check(!checkedPixels.AsSpan().SequenceEqual(reversed), "GPU Toolbar RTL moves actual child pixels");
        var first = Node().FirstChild!;
        if (!tree.TryGet(first, out var child))
            throw new InvalidOperationException("Missing Toolbar child");
        check(tree.RequestAction(first, child.RuntimeId, UiAccessibilityAction.Invoke, wait: false) == UiAccessibilityResult.Success, "GPU Toolbar child accepts actual Invoke");
        capture("invoked", Draw);
        check(count == 1, "GPU Toolbar actual command executes once");
        check(context.State.NextScheduledFrameTimeSeconds is null, "GPU Toolbar idle controls schedule no periodic frames");
    }
}
