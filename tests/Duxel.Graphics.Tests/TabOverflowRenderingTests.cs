using Duxel.Core;

internal static class TabOverflowRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<UiInputState> setInput,
        Action<bool, string> check, UiAccessibilityTree tree, int width, UiFluentColorScheme scheme)
    {
        var value = new UiState<string?>("one");
        using var tabs = Fluent.TabList("Overflow tabs", value.Binding,
            new UiTabOption[] { new("one", "One"), new("two", "Two", false), new("three", "Three"), new("four", "Four") },
            new() { Overflow = new() });
        using var box = Dux.SizedBox(tabs, width : 300, height : 40);
        using var one = Fluent.Button("One body");
        using var two = Fluent.Button("Two body");
        using var three = Fluent.Button("Three body");
        using var four = Fluent.Button("Four body");
        var rendered = string.Empty;
        void Render(UiImmediateContext ui)
        {
            var context = new UiControlContext(ui);
            context.Frame(new(0, 0, width, 240), scheme == UiFluentColorScheme.Light ? new UiColor(255, 255, 255) : new(32, 32, 32), default, 0, 0);
            ui.EnableRootViewportContentLayout(contentPadding : 0);
            box.Render(ui);
            tabs.RenderPanel(ui, "one", body => { rendered = "one"; one.Render(body); });
            tabs.RenderPanel(ui, "two", body => { rendered = "two"; two.Render(body); });
            tabs.RenderPanel(ui, "three", body => { rendered = "three"; three.Render(body); });
            tabs.RenderPanel(ui, "four", body => { rendered = "four"; four.Render(body); });
        }
        UiInputState Input(UiVector2? point = null, bool down = false, bool pressed = false, bool released = false) =>
            new(point ?? new(-100, -100), down, false, pressed, released, false, false, 0, 0, [], [], default, default);
        UiAccessibilityElement Find(string name, UiAccessibilityRole role)
        {
            UiAccessibilityElement? result = null;
            void Visit(string? id)
            {
                while (id is not null && tree.TryGet(id, out var entry))
                {
                    if (entry.Node.Name == name && entry.Node.Role == role) result = entry;
                    Visit(entry.FirstChild);
                    id = entry.Next;
                }
            }
            Visit(tree.Root.First);
            return result ?? throw new InvalidOperationException("Missing GPU overflow control : " + name);
        }
        void Click(UiAccessibilityElement entry, string name)
        {
            var rect = entry.Node.Bounds;
            var point = new UiVector2(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
            setInput(Input(point, down : true, pressed : true));
            capture(name + "-down", Render);
            setInput(Input(point, released : true));
            capture(name + "-up", Render);
        }
        setInput(Input());
        var all = capture("all", Render);
        check(tabs.OverflowResult is { HiddenCount : 0 } && rendered == "one", "actual GPU fitting headers preserve their selected body");
        box.Options = box.Options with { Width = 140 };
        var narrow = capture("narrow", Render);
        check(tabs.OverflowResult is { HiddenCount : 2 } && !all.AsSpan(0, width * 40 * 4).SequenceEqual(narrow.AsSpan(0, width * 40 * 4)),
            "actual GPU collapse replaces hidden headers with the generated overflow icon");
        Click(Find("2 more tabs", UiAccessibilityRole.Tab), "trigger");
        setInput(Input());
        var open = capture("menu-open", Render);
        check(!open.AsSpan().SequenceEqual(narrow), "actual GPU tab overflow opens the shared Fluent popup surface");
        Click(Find("Four", UiAccessibilityRole.MenuItem), "select-four");
        check(value.Value == "four" && rendered == "four" && Find("Four", UiAccessibilityRole.Tab).Node.Selected == true,
            "actual GPU menu selection updates its header and body in the same release frame");
        setInput(Input());
        var selected = capture("selected", Render);
        check(!selected.AsSpan(width * 40 * 4).SequenceEqual(narrow.AsSpan(width * 40 * 4)),
            "actual GPU selected panel paints different real body glyphs after overflow invocation");
        for (var i = 0; i < 4; i++)
            check(capture("selected-replay-" + i, Render).AsSpan().SequenceEqual(selected),
                "actual GPU promoted header/body survive acquired-image reuse : " + i);
        box.Options = box.Options with { Width = 300 };
        var expanded = capture("expanded", Render);
        check(tabs.OverflowResult is { HiddenCount : 0 } && expanded.AsSpan(width * 40 * 4).SequenceEqual(selected.AsSpan(width * 40 * 4)),
            "actual GPU width expansion removes overflow while preserving selected body pixels");
        box.Options = box.Options with { Width = 140 };
        tabs.Options = tabs.Options with { RightToLeft = true };
        capture("right-to-left", Render);
        check(Find("2 more tabs", UiAccessibilityRole.Tab).Node.Bounds.X < Find("Four", UiAccessibilityRole.Tab).Node.Bounds.X,
            "actual GPU RTL layout retains the selected header after the overflow action");
        tabs.Options = tabs.Options with { Vertical = true, RightToLeft = false };
        box.Options = box.Options with { Width = 120, Height = 110 };
        capture("vertical", Render);
        check(tabs.OverflowResult is { IsOverflowing : true } && rendered == "four",
            "actual GPU vertical overflow preserves the selected body and exposes an action");
    }
}
