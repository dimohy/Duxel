using Duxel.Core;
using Duxel.Svg;

internal static class DropdownRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check,
        UiContext context, int stride)
    {
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        using var store = new SvgStore();
        var directory = Path.Combine(AppContext.BaseDirectory, "Assets", "Svg");
        using var icon = new UiSvgIcon(store.Load(Path.Combine(directory, "regular.svg")),
            store.Load(Path.Combine(directory, "filled.svg")), UiSvgIconColorMode.MultiplyForeground);
        string? selected = null;
        string[] selections = [];
        UiSelectOption[] items = [new("a", "Alpha"), new("b", "Beta") { Icon = icon.MenuIcon },
            new("g", "Gamma", false), new("n", "New York")];
        using var single = Fluent.Dropdown("GPU choice", new UiBinding<string?>(() => selected, value => selected = value), items,
            new(Width: 200, Placeholder: "Choose"));
        using var multiple = Fluent.Dropdown("GPU choices", new UiBinding<string[]>(() => selections, value => selections = value), items,
            new(Width: 120, Placeholder: "Choose", SelectedCountFormat: "{0} picked"));
        UiWidget current = single;
        void Draw(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            var paint = new UiControlContext(ui);
            paint.Frame(paint.ClipRect, new(30, 70, 90), default, 0, 0);
            ui.SetCursorScreenPos(new(8, 16));
            current.Render(ui);
        }
        UiAccessibilityElement Find(string name, UiAccessibilityRole role)
        {
            UiAccessibilityElement? result = null;
            void Visit(string? id)
            {
                while (id is not null && tree.TryGet(id, out var item))
                {
                    if (item.Node.Name == name && item.Node.Role == role)
                        result = item;
                    Visit(item.FirstChild);
                    id = item.Next;
                }
            }
            Visit(tree.Root.First);
            return result ?? throw new InvalidOperationException(name);
        }
        void Send(string name, UiAccessibilityRole role, UiAccessibilityAction action)
        {
            var item = Find(name, role);
            check(tree.RequestAction(item.Node.Id, item.RuntimeId, action, wait: false) == UiAccessibilityResult.Success,
                "GPU Dropdown accepts the actual native action : " + action + " / " + name);
        }
        var closed = capture("closed", Draw);
        check(closed.AsSpan().SequenceEqual(capture("closed-replay", Draw)), "GPU Dropdown unchanged closed field replays exactly");
        Send("GPU choice", UiAccessibilityRole.ComboBox, UiAccessibilityAction.Expand);
        capture("open-plan", Draw);
        context.State.AdvanceTime(.2f);
        var open = capture("open", Draw);
        var list = Find("GPU choice", UiAccessibilityRole.ListBox).Node.Bounds;
        check(list.Y >= 48 && !closed.AsSpan().SequenceEqual(open), "GPU Dropdown listbox paints outside the trigger clip");
        check(open.AsSpan().SequenceEqual(capture("open-replay", Draw)), "GPU Dropdown listbox with borrowed SVG replays exactly");
        Send("Beta", UiAccessibilityRole.Option, UiAccessibilityAction.Select);
        var chosen = capture("chosen", Draw);
        check(selected == "b" && !single.IsOpen && Find("GPU choice", UiAccessibilityRole.ComboBox).Node.Value == "Beta"
            && !chosen.AsSpan().SequenceEqual(closed), "GPU Dropdown selected value updates real pixels and native metadata");
        context.State.AdvanceTime(.2f);
        var settled = capture("chosen-settled", Draw);
        check(settled.AsSpan().SequenceEqual(capture("chosen-replay", Draw)), "GPU Dropdown selected field replays exactly after motion");
        current = multiple;
        capture("multi-closed", Draw);
        Send("GPU choices", UiAccessibilityRole.ComboBox, UiAccessibilityAction.Expand);
        capture("multi-open", Draw);
        context.State.AdvanceTime(.2f);
        Send("Alpha", UiAccessibilityRole.Option, UiAccessibilityAction.Select);
        capture("multi-first", Draw);
        Send("Beta", UiAccessibilityRole.Option, UiAccessibilityAction.AddToSelection);
        var two = capture("multi-two", Draw);
        check(selections.SequenceEqual(new[] { "a", "b" }) && multiple.IsOpen,
            "GPU Dropdown keeps the true multi-select list open with both checked options");
        var alpha = Find("Alpha", UiAccessibilityRole.Option).Node.Bounds;
        var x = (int)alpha.X + 8;
        var y = (int)(alpha.Y + (alpha.Height - 16) * .5f) + 2;
        var pixel = (y * stride + x) * 4;
        var dark = context.GetStyle().FluentColorScheme == UiFluentColorScheme.Dark;
        check(two[pixel] == (dark ? 71 : 15) && two[pixel + 1] == (dark ? 158 : 108) && two[pixel + 2] == (dark ? 245 : 189),
            "GPU Dropdown selected multi-option paints the source brand checkbox color");
        check(two.AsSpan().SequenceEqual(capture("multi-two-replay", Draw)), "GPU Dropdown unchanged multi-select replays exactly");
        Send("GPU choices", UiAccessibilityRole.ComboBox, UiAccessibilityAction.Collapse);
        capture("multi-dismiss", Draw);
        context.State.AdvanceTime(.2f);
        multiple.Options = multiple.Options with
        {
            RightToLeft = true
        };
        var rtl = capture("multi-rtl", Draw);
        check(!rtl.AsSpan().SequenceEqual(two) && Find("GPU choices", UiAccessibilityRole.ComboBox).Node.Value == "Alpha, Beta",
            "GPU Dropdown RTL retains the authoritative selected text while mirroring the field");
        check(rtl.AsSpan().SequenceEqual(capture("multi-rtl-replay", Draw)), "GPU Dropdown unchanged RTL field replays exactly");
        foreach (var size in Enum.GetValues<UiDropdownSize>())
            foreach (var appearance in Enum.GetValues<UiInputAppearance>())
            {
                multiple.Options = multiple.Options with
                {
                    Size = size,
                    Appearance = appearance
                };
                var reference = capture($"face-{size}-{appearance}", Draw);
                check(reference.AsSpan().SequenceEqual(capture($"face-{size}-{appearance}-replay", Draw)),
                    "GPU Dropdown size/appearance replay : " + size + " / " + appearance);
            }
        current = single;
        single.Options = single.Options with
        {
            MaximumPopupHeight = 72
        };
        capture("scroll-closed", Draw);
        Send("GPU choice", UiAccessibilityRole.ComboBox, UiAccessibilityAction.Expand);
        capture("scroll-open", Draw);
        context.State.AdvanceTime(.2f);
        var last = Find("New York", UiAccessibilityRole.Option);
        check(last.Node.Offscreen && last.Node.CanScrollIntoView, "GPU Dropdown clipped options expose the real scroll-item contract");
        check(tree.RequestAction(last.Node.Id, last.RuntimeId, UiAccessibilityAction.ScrollIntoView, wait: false) == UiAccessibilityResult.Success,
            "GPU Dropdown accepts ScrollIntoView for the offscreen option");
        capture("scroll-reveal-plan", Draw);
        capture("scroll-reveal", Draw);
        check(!Find("New York", UiAccessibilityRole.Option).Node.Offscreen, "GPU Dropdown ScrollIntoView reveals the actual option geometry");
        context.State.AdvanceTime(.65f);
        capture("scroll-settled", Draw);
        check(context.State.NextScheduledFrameTimeSeconds is null, "GPU Dropdown returns to event waiting after its scrollbar fades");
    }
}
