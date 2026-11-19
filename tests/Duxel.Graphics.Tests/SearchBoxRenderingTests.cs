using Duxel.Core;

internal static class SearchBoxRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check, UiContext context, Action<UiInputState> setInput)
    {
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        var text = "정세일";
        var reject = false;
        using var search = Fluent.SearchBox("Search pixels", new UiBinding<string>(() => text, value => { if (!reject) text = value; }));
        void Draw(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            var paint = new UiControlContext(ui);
            paint.Frame(paint.ClipRect, new(30, 70, 90), default, 0, 0);
            ui.SetCursorScreenPos(new(16, 16));
            search.Render(ui);
        }
        UiAccessibilityElement Node() => tree.TryGet(tree.Root.First!, out var node) ? node : throw new InvalidOperationException("Missing SearchBox");
        void Request(UiAccessibilityAction action, string? value = null)
        {
            var node = Node();
            check(tree.RequestAction(node.Node.Id, node.RuntimeId, action, value, wait: false) == UiAccessibilityResult.Success, "GPU Search accepts native " + action);
        }
        foreach (var size in Enum.GetValues<UiSearchBoxSize>())
        {
            foreach (var appearance in Enum.GetValues<UiInputAppearance>())
            {
                foreach (var rtl in new[] { false, true })
                {
                    search.Options = new(Size: size, Appearance: appearance, RightToLeft: rtl);
                    var name = $"{size}-{appearance}-{rtl}";
                    var pixels = capture(name, Draw);
                    check(pixels.AsSpan().SequenceEqual(capture(name + "-replay", Draw)), "GPU Search unchanged input pixels replay " + name);
                }
            }
        }
        search.Options = new(Placeholder: "Search placeholder");
        var original = capture("original", Draw);
        Request(UiAccessibilityAction.SetValue, "Updated");
        var changed = capture("changed", Draw);
        check(!original.AsSpan().SequenceEqual(changed) && text == "Updated", "GPU Search actual bound text changes pixels");
        reject = true;
        Request(UiAccessibilityAction.SetValue, "rejected");
        var rejected = capture("rejected", Draw);
        check(rejected.AsSpan().SequenceEqual(capture("rejected-replay", Draw)) && Node().Node.Value == "Updated", "GPU Search rejected edit publishes and paints the accepted text");
        reject = false;
        text = "";
        var placeholder = capture("placeholder", Draw);
        check(!changed.AsSpan().SequenceEqual(placeholder), "GPU Search empty input paints actual placeholder");
        Request(UiAccessibilityAction.Focus);
        var focused = capture("focused", Draw);
        check(!focused.AsSpan().SequenceEqual(placeholder) && Node().FirstChild is not null, "GPU Search focus paints frame and dismiss icon with actual child provider");
        search.Options = search.Options with
        {
            ReadOnly = true
        };
        var readOnly = capture("readonly", Draw);
        check(!focused.AsSpan().SequenceEqual(readOnly) && Node().Node.ReadOnly && Node().FirstChild is null, "GPU Search readonly removes actual dismiss pixels and action");
        search.Options = search.Options with
        {
            ReadOnly = false
        };
        text = "정세일";
        Request(UiAccessibilityAction.Focus);
        capture("held-ready", Draw);
        if (!tree.TryGet(Node().FirstChild!, out var clear))
            throw new InvalidOperationException("GPU Search clear button is missing.");
        var clearBounds = clear.Node.Bounds;
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var pointer = idle with
        {
            MousePosition = new(clearBounds.X + clearBounds.Width / 2, clearBounds.Y + clearBounds.Height / 2)
        };
        try
        {
            setInput(pointer with
            {
                LeftMouseDown = true,
                LeftMousePressed = true
            });
            capture("held-press", Draw);
            setInput(pointer with
            {
                LeftMouseDown = true
            });
            var held = capture("held-frame", Draw);
            check(held.AsSpan().SequenceEqual(capture("held-replay", Draw)) && Node().Node.Focused && text == "정세일",
                "GPU held clear keeps the editor and dismiss icon stable across real input frames");
            setInput(pointer with
            {
                LeftMouseReleased = true
            });
            var cleared = capture("held-release", Draw);
            check(text == "" && Node().Node.Value == "" && Node().Node.Focused && !held.AsSpan().SequenceEqual(cleared),
                "GPU held clear release clears both bound value and actual rendered text");
            setInput(idle with
            {
                CharEvents = [new(0xAC00)]
            });
            var typed = capture("held-korean", Draw);
            check(text == "가" && Node().Node.Value == "가" && !cleared.AsSpan().SequenceEqual(typed),
                "GPU Korean typing immediately updates pixels after held clear");
        }
        finally { setInput(idle); }
    }
}
