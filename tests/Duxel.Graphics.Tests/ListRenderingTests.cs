using Duxel.Core;

internal static class ListRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check,
        Action<UiInputState> setInput, UiContext context, UiFluentColorScheme scheme)
    {
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        var selected = new UiState<string?>("a");
        using var list = new UiList<UiFluentListPresentation>("Documents", selected.Binding,
        [
            new("a", "First document", Fluent.Body1("First document"), selectionLabel: "Select first"),
            new("b", "Second document", Fluent.Body1("Second document"), selectionLabel: "Select second")
        ], new(Width: 240));
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Draw(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            new UiControlContext(ui).Frame(new(0, 0, 320, 240),
                scheme == UiFluentColorScheme.Light ? new(255, 255, 255) : new(32, 32, 32), default, 0, 0);
            ui.SetCursorScreenPos(new(20, 40));
            list.Render(ui);
        }
        UiAccessibilityElement Find(string name, UiAccessibilityRole role)
        {
            UiAccessibilityElement? found = null;
            void Visit(string? id)
            {
                while (id is not null && tree.TryGet(id, out var node))
                {
                    if (node.Node.Name == name && node.Node.Role == role)
                        found = node;
                    Visit(node.FirstChild);
                    id = node.Next;
                }
            }
            Visit(tree.Root.First);
            return found ?? throw new InvalidOperationException("Missing List fixture node: " + name);
        }
        setInput(idle);
        capture("warmup", Draw);
        var baseline = capture("baseline", Draw);
        check(baseline.AsSpan().SequenceEqual(capture("baseline-replay", Draw)), "GPU List unchanged selection replays exact pixels");
        var second = Find("Second document", UiAccessibilityRole.Option);
        tree.RequestAction(second.Node.Id, second.RuntimeId, UiAccessibilityAction.Select, wait: false);
        var changed = capture("select-later", Draw);
        check(selected.Value == "b" && Find("Select first", UiAccessibilityRole.Checkbox).Node.ToggleState == UiAccessibilityToggleState.Off
            && Find("Select second", UiAccessibilityRole.Checkbox).Node.ToggleState == UiAccessibilityToggleState.On,
            "GPU List same-frame native row and checkmark states read the committed selection");
        var replay = capture("select-later-replay", Draw);
        check(changed.AsSpan().SequenceEqual(replay),
            "GPU List selecting a later row paints the final selection on earlier rows in the same frame");
        var first = Find("First document", UiAccessibilityRole.Option);
        tree.RequestAction(first.Node.Id, first.RuntimeId, UiAccessibilityAction.Select, wait: false);
        var earlier = capture("select-earlier", Draw);
        check(earlier.AsSpan().SequenceEqual(capture("select-earlier-replay", Draw)),
            "GPU List selecting an earlier row also paints one coherent frame");
        check(!baseline.AsSpan().SequenceEqual(changed), "GPU List selection changes the actual checkmark pixels");
        tree.RequestAction(first.Node.Id, first.RuntimeId, UiAccessibilityAction.Focus, wait: false);
        capture("focus-first", Draw);
        var focusedFirst = capture("focus-first-replay", Draw);
        tree.RequestAction(second.Node.Id, second.RuntimeId, UiAccessibilityAction.Focus, wait: false);
        var focusedLater = capture("focus-later", Draw);
        check(Find("Second document", UiAccessibilityRole.Option).Node.Focused
            && !Find("First document", UiAccessibilityRole.Option).Node.Focused,
            "GPU List publishes the final native focus after a request to a later row");
        check(focusedLater.AsSpan().SequenceEqual(capture("focus-later-replay", Draw)),
            "GPU List moving focus to a later row removes the earlier outline in the same frame");
        check(!focusedFirst.AsSpan().SequenceEqual(focusedLater), "GPU List focus moves the actual outline pixels");
        tree.RequestAction(first.Node.Id, first.RuntimeId, UiAccessibilityAction.Focus, wait: false);
        var focusedEarlier = capture("focus-earlier", Draw);
        check(focusedEarlier.AsSpan().SequenceEqual(capture("focus-earlier-replay", Draw)),
            "GPU List moving focus to an earlier row also paints one coherent frame");
        context.SetAccessibilityTree(null);
    }
}
