using Duxel.Core;

internal static class MenuCheckboxRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<UiInputState> setInput,
        Action<bool, string> check, UiAccessibilityTree tree, int width)
    {
        var state = new UiState<bool>(false);
        var calls = new List<string>();
        using var menu = Fluent.Menu("Checkbox actions", [UiMenuOption.Checkbox("check", "Show details", state), new("finish", "Finish")], calls.Add);
        void Render(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding : 8);
            menu.Render(ui);
        }
        UiAccessibilityElement Find(UiAccessibilityRole role)
        {
            tree.TryGet(tree.Root.First!, out var trigger);
            if (role == UiAccessibilityRole.Button) return trigger;
            tree.TryGet(trigger.Next!, out var surface);
            tree.TryGet(surface.FirstChild!, out var item);
            return item;
        }
        void Request(UiAccessibilityAction action, UiAccessibilityRole role = UiAccessibilityRole.MenuItem)
        {
            var item = Find(role);
            if (tree.RequestAction(item.Node.Id, item.RuntimeId, action, wait : false) != UiAccessibilityResult.Success)
                throw new InvalidOperationException("GPU checkbox request rejected.");
        }
        int Differences(byte[] left, byte[] right, UiRect rect)
        {
            var count = 0;
            for (var y = (int)rect.Y; y < rect.Y + rect.Height; y++)
                for (var x = (int)rect.X; x < rect.X + rect.Width; x++)
                {
                    var offset = (y * width + x) * 4;
                    if (!left.AsSpan(offset, 4).SequenceEqual(right.AsSpan(offset, 4))) count++;
                }
            return count;
        }
        setInput(new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default));
        capture("closed", Render);
        Request(UiAccessibilityAction.Expand, UiAccessibilityRole.Button);
        var off = capture("off", Render);
        var row = Find(UiAccessibilityRole.MenuItem).Node.Bounds;
        Request(UiAccessibilityAction.Toggle);
        var on = capture("on", Render);
        check(menu.IsOpen && state.Value && calls.SequenceEqual(["check"]) && Find(UiAccessibilityRole.MenuItem).Node.Checked == true,
            "GPU checkbox Toggle commits state and keeps the actual menu open");
        check(Differences(off, on, new(row.X + 6, row.Y + 8, 16, 16)) > 8
            && Differences(off, on, new(row.X + 28, row.Y + 4, row.Width - 36, 24)) == 0,
            "GPU checkbox paints the actual 16px checkmark in its separate column without moving label glyphs");
        check(capture("on-replay", Render).AsSpan().SequenceEqual(on), "GPU unchanged checkbox menu replays identical pixels");
        menu.Options = menu.Options with { RightToLeft = true };
        var rtlOn = capture("rtl-on", Render);
        row = Find(UiAccessibilityRole.MenuItem).Node.Bounds;
        Request(UiAccessibilityAction.Toggle);
        var rtlOff = capture("rtl-off", Render);
        check(menu.IsOpen && !state.Value && calls.Count == 2
            && Differences(rtlOn, rtlOff, new(row.X + row.Width - 22, row.Y + 8, 16, 16)) > 8
            && Differences(rtlOn, rtlOff, new(row.X + 8, row.Y + 4, row.Width - 36, 24)) == 0,
            "GPU RTL checkmark occupies the logical start column and leaves text unchanged");
        Request(UiAccessibilityAction.Collapse, UiAccessibilityRole.Button);
        capture("cleanup", Render);
    }
}
