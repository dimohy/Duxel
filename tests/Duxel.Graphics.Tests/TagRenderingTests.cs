using Duxel.Core;
using Duxel.Svg;

internal static class TagRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check, UiContext context)
    {
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        using var store = new SvgStore();
        var assets = Path.Combine(AppContext.BaseDirectory, "Assets", "Svg");
        using var icon = new UiSvgIcon(store.Load(Path.Combine(assets, "regular.svg")), store.Load(Path.Combine(assets, "filled.svg")), UiSvgIconColorMode.MultiplyForeground);
        var calls = 0;
        using var tag = Fluent.Tag(new UiTagContent("정세일 SVG", Icon: icon.MenuIcon), () => calls++);
        using var secondary = Fluent.Tag(new UiTagContent("정세일", "Secondary"), () => calls++);
        using var passive = Fluent.Tag(new UiTagContent("읽기 전용"));
        string[] values = ["a", "b", "n"];
        using var group = new UiFluentTagGroup("GPU selected tags", new(() => values, value => values = value),
            [new("a", "Alpha"), new("b", "부산") { Icon = icon.MenuIcon }, new("n", "New York"), new("d", "Disabled", false)]);
        using var allocated = Dux.SizedBox(group, 180);
        UiWidget current = tag;
        var disabled = false;
        void Draw(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            var paint = new UiControlContext(ui);
            paint.Frame(paint.ClipRect, new(30, 70, 90), default, 0, 0);
            ui.SetCursorScreenPos(new(16, 16));
            ui.BeginDisabled(disabled);
            try
            {
                current.Render(ui);
            }
            finally { ui.EndDisabled(); }
        }
        UiAccessibilityElement Find(string name)
        {
            UiAccessibilityElement? result = null;
            void Visit(string? id)
            {
                while (id is not null && tree.TryGet(id, out var item))
                {
                    if (item.Node.Name == name)
                        result = item;
                    Visit(item.FirstChild);
                    id = item.Next;
                }
            }
            Visit(tree.Root.First);
            return result ?? throw new InvalidOperationException(name);
        }
        foreach (var size in Enum.GetValues<UiTagSize>())
            foreach (var appearance in Enum.GetValues<UiTagAppearance>())
                foreach (var shape in Enum.GetValues<UiTagShape>())
                {
                    tag.Options = new(size, appearance, shape, RightToLeft: shape == UiTagShape.Circular);
                    var name = $"{size}-{appearance}-{shape}";
                    var pixels = capture(name, Draw);
                    check(pixels.AsSpan().SequenceEqual(capture(name + "-replay", Draw)), "GPU Tag variant replays exactly : " + name);
                    var expected = size == UiTagSize.Medium ? 32 : size == UiTagSize.Small ? 24 : 20;
                    check(Find("Remove 정세일 SVG").Node.Bounds.Height == expected, "GPU Tag actual geometry matches its size : " + name);
                }
        current = secondary;
        var secondaryPixels = capture("secondary", Draw);
        check(secondaryPixels.AsSpan().SequenceEqual(capture("secondary-replay", Draw)), "GPU Tag two-line typography replays exactly");
        current = passive;
        capture("passive", Draw);
        check(Find("읽기 전용").Node is { Role: UiAccessibilityRole.Label, Focusable: false }, "GPU passive Tag exposes no interaction state");
        current = allocated;
        var initial = capture("selected-group", Draw);
        check(initial.AsSpan().SequenceEqual(capture("selected-group-replay", Draw)), "GPU selected SVG TagGroup replays exactly");
        var removed = Find("Remove 부산");
        check(tree.RequestAction(removed.Node.Id, removed.RuntimeId, UiAccessibilityAction.Invoke, wait: false) == UiAccessibilityResult.Success,
            "GPU selected Tag accepts a real native dismiss request");
        capture("dismiss-request", Draw);
        var remaining = capture("dismissed", Draw);
        check(values.SequenceEqual(new[] { "a", "n" }) && !tree.TryGet(removed.Node.Id, out _)
            && !initial.AsSpan().SequenceEqual(remaining), "GPU Tag dismissal updates selection, geometry and visible pixels");
        check(remaining.AsSpan().SequenceEqual(capture("dismissed-replay", Draw)), "GPU Tag removal replays exactly across acquired images");
        group.TagOptions = new(RightToLeft: true);
        capture("rtl-group", Draw);
        check(Find("Remove Alpha").Node.Bounds.X > Find("Remove New York").Node.Bounds.X,
            "GPU TagGroup mirrors actual positions for RTL");
        disabled = true;
        current = tag;
        var disabledPixels = capture("disabled", Draw);
        check(disabledPixels.AsSpan().SequenceEqual(capture("disabled-replay", Draw)) && !Find("Remove 정세일 SVG").Node.Enabled,
            "GPU disabled Tag has stable disabled paint and native state");
        check(calls == 0, "GPU Tag variants never invoke dismissal while only rendering");
    }
}
