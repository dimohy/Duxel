using Duxel.Core;

internal static class MenuRadioRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<UiInputState> setInput,
        Action<bool, string> check, UiAccessibilityTree tree, int width)
    {
        var state = new UiState<string?>("one");
        var sort = new UiMenuRadioGroup("Sort", state);
        var view = new UiMenuRadioGroup("View", new UiState<string?>("compact"));
        var calls = new List<string>();
        using var menu = Fluent.Menu("Radio actions", [UiMenuOption.Radio("one", "One", sort), UiMenuOption.Radio("two", "Two", sort),
            UiMenuOption.Radio("compact", "Compact", view), new("finish", "Finish")], calls.Add);
        void Render(UiImmediateContext ui) { ui.EnableRootViewportContentLayout(contentPadding : 8); menu.Render(ui); }
        UiAccessibilityElement Find(string name, UiAccessibilityRole role = UiAccessibilityRole.MenuItem)
        {
            UiAccessibilityElement? found = null;
            void Visit(string? id)
            {
                while (id is not null && tree.TryGet(id, out var entry))
                {
                    if (entry.Node.Name == name && entry.Node.Role == role) found = entry;
                    Visit(entry.FirstChild);
                    id = entry.Next;
                }
            }
            Visit(tree.Root.First);
            return found ?? throw new InvalidOperationException("Missing GPU radio element : " + name);
        }
        void Request(string name, UiAccessibilityRole role, UiAccessibilityAction action)
        {
            var item = Find(name, role);
            var result = tree.RequestAction(item.Node.Id, item.RuntimeId, action, wait : false);
            if (result != UiAccessibilityResult.Success)
                throw new InvalidOperationException($"GPU radio request rejected : {name} / {action} / {result}");
        }
        int Differences(byte[] first, byte[] last, UiRect bounds)
        {
            var result = 0;
            for (var y = (int)bounds.Y; y < bounds.Y + bounds.Height; y++)
                for (var x = (int)bounds.X; x < bounds.X + bounds.Width; x++)
                {
                    var offset = (y * width + x) * 4;
                    if (!first.AsSpan(offset, 4).SequenceEqual(last.AsSpan(offset, 4))) result++;
                }
            return result;
        }
        setInput(new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default));
        var closed = capture("closed", Render);
        Request("Radio actions", UiAccessibilityRole.Button, UiAccessibilityAction.Expand);
        var initial = capture("one-selected", Render);
        var one = Find("One").Node.Bounds;
        var two = Find("Two").Node.Bounds;
        var compact = Find("Compact").Node.Bounds;
        Request("Two", UiAccessibilityRole.MenuItem, UiAccessibilityAction.Select);
        var changed = capture("two-commit", Render);
        check(!menu.IsOpen && state.Value == "two" && calls.SequenceEqual(["two"])
            && Find("One").Node.Selected == false && Find("Two").Node.Selected == true && Find("Compact").Node.Selected == true,
            "GPU radio Select commits both peer states and preserves the independent group before dismissal");
        check(Differences(initial, changed, new(one.X + 6, one.Y + 8, 16, 16)) > 8
            && Differences(initial, changed, new(two.X + 6, two.Y + 8, 16, 16)) > 8
            && Differences(initial, changed, compact) == 0,
            "GPU radio replacement removes the old checkmark, paints the new one and leaves the other group pixels unchanged");
        var dismissed = capture("dismissed", Render);
        check(dismissed.AsSpan(width * 40 * 4).SequenceEqual(closed.AsSpan(width * 40 * 4)),
            "GPU radio activation clears the complete popup on the following frame");
        Request("Radio actions", UiAccessibilityRole.Button, UiAccessibilityAction.Expand);
        menu.Options = menu.Options with { RightToLeft = true };
        var rtlTwo = capture("rtl-two", Render);
        one = Find("One").Node.Bounds;
        two = Find("Two").Node.Bounds;
        state.Value = "one";
        var rtlOne = capture("rtl-one", Render);
        check(Differences(rtlTwo, rtlOne, new(one.X + one.Width - 22, one.Y + 8, 16, 16)) > 8
            && Differences(rtlTwo, rtlOne, new(two.X + two.Width - 22, two.Y + 8, 16, 16)) > 8 && calls.Count == 1,
            "GPU external radio binding updates both actual RTL checkmarks without invoking a callback");
        check(capture("rtl-replay", Render).AsSpan().SequenceEqual(rtlOne), "GPU unchanged radio menu replays identical pixels");
        Request("Radio actions", UiAccessibilityRole.Button, UiAccessibilityAction.Collapse);
        capture("cleanup", Render);
    }
}
