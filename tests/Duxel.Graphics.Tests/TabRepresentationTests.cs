using Duxel.Core;

internal static class TabRepresentationTests
{
    private readonly record struct IconCall(bool Filled, UiRect Bounds, UiColor Color);
    private static readonly List<IconCall> Icons = [];
    private readonly struct Regular : IUiButtonIcon
    {
        public static void Paint(UiControlContext context, UiRect bounds, UiColor color, UiButtonInteraction interaction) =>
            Icons.Add(new(false, bounds, color));
    }
    private readonly struct Filled : IUiButtonIcon
    {
        public static void Paint(UiControlContext context, UiRect bounds, UiColor color, UiButtonInteraction interaction) =>
            Icons.Add(new(true, bounds, color));
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }

    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        using var tree = new UiAccessibilityTree();
        ControlFontFixture.Configure(context);
        context.SetAccessibilityTree(tree);
        var icon = UiTabIcon.Create<Regular, Filled>();
        var selection = new UiState<string?>("one");
        using var tabs = Fluent.TabList("Icon tabs", selection.Binding, new UiTabOption[]
        {
            new("one", "One accessible name", Icon : icon, IconOnly : true),
            new("two", "Two accessible name", Icon : icon, IconOnly : true),
            new("disabled", "Disabled", false, icon, true)
        });
        var screen = new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding : 0); tabs.Render(ui); });
        void Frame(UiKeyEvent[]? keys = null, UiVector2? pointer = null, bool down = false, bool pressed = false, bool released = false)
        {
            Icons.Clear();
            context.NewFrame(new(1f / 60, new(640, 400), new(1, 1)),
                new(pointer ?? new(-100, -100), down, false, pressed, released, false, false, 0, 0, keys ?? [], [], default, default),
                new(0, 0, 640, 400), UiTextSettings.Default);
            context.Render(screen);
            using var draw = context.GetDrawData();
        }
        UiAccessibilityElement[] Items()
        {
            var result = new List<UiAccessibilityElement>();
            tree.TryGet(tree.Root.First!, out var group);
            var id = group.FirstChild;
            while (id is not null && tree.TryGet(id, out var item)) { result.Add(item); id = item.Next; }
            return result.ToArray();
        }
        Frame();
        check(Items()[0].Node.Name == "One accessible name" && Items()[0].Node.Selected == true && !Items()[2].Node.Enabled,
            "icon-only tabs retain actual accessible names, selection and disabled semantics");
        check(Icons.Count == 3 && Icons[0].Filled && !Icons[1].Filled && !Icons[2].Filled,
            "tab selection chooses the compiled filled glyph while other tabs use regular glyphs");
        var second = Items()[1].Node.Bounds;
        Frame(pointer : new(second.X + second.Width / 2, second.Y + second.Height / 2));
        check(!Icons[1].Filled, "hover alone never selects the filled tab glyph");
        Frame([new(UiKey.Tab, true, KeyModifiers.None)]);
        Frame([new(UiKey.RightArrow, true, KeyModifiers.None)]);
        check(Items()[1].Node.Focused && !Icons[1].Filled && selection.Value == "one",
            "arrow focus remains independent of selected icon and value");
        Frame([new(UiKey.Enter, true, KeyModifiers.None)]);
        check(selection.Value == "two" && !Icons[0].Filled && Icons[1].Filled,
            "keyboard selection changes the actual bound icon state in the same frame");
        foreach (var size in Enum.GetValues<UiTabListSize>())
        foreach (var circular in new[] { false, true })
        foreach (var vertical in new[] { false, true })
        {
            tabs.Options = new(size, vertical, Appearance : circular ? UiTabListAppearance.FilledCircular : UiTabListAppearance.Transparent);
            Frame();
            var items = Items();
            var edge = size == UiTabListSize.Large ? 24 : 20;
            var outerX = size == UiTabListSize.Small ? 6 : 10;
            var outerY = circular ? size == UiTabListSize.Small ? 2 : size == UiTabListSize.Medium ? 6 : 8
                : vertical ? size == UiTabListSize.Small ? 2 : size == UiTabListSize.Medium ? 6 : 8
                : size == UiTabListSize.Small ? 6 : size == UiTabListSize.Medium ? 12 : 16;
            var expectedWidth = edge + 2 * outerX + (circular ? 2 : 0);
            var expectedHeight = edge + 2 * outerY;
            var gap = circular ? size == UiTabListSize.Small ? 6 : 8 : 0;
            var actualGap = vertical ? items[1].Node.Bounds.Y - items[0].Node.Bounds.Y - expectedHeight
                : items[1].Node.Bounds.X - items[0].Node.Bounds.X - expectedWidth;
            check(items.All(item => item.Node.Bounds.Width == expectedWidth && item.Node.Bounds.Height == expectedHeight)
                && actualGap == gap && Icons.All(call => call.Bounds.Width == edge && call.Bounds.Height == edge),
                $"official icon-only extent, glyph size and list gap : {size} / circular {circular} / vertical {vertical}");
        }
        tabs.Options = new(RightToLeft : true);
        Frame();
        check(Items()[0].Node.Bounds.X > Items()[1].Node.Bounds.X,
            "right-to-left icon-only lists reverse actual item placement without reversing stable identities");
        var rejected = 0;
        foreach (var invalid in new[] { new UiTabOption("bad", "Bad", IconOnly : true), new UiTabOption("bad", " ", Icon : icon) })
        {
            try { tabs.SetItems(new[] { invalid }); }
            catch (ArgumentException) { rejected++; }
        }
        check(rejected == 2 && tabs.Items.Length == 3 && selection.Value == "two",
            "invalid icon content fails before replacing items or caller selection");
        var recreated = UiTabIcon.Create<Regular, Filled>();
        check(icon == recreated, "repeated typed icon construction reuses the same cached painter identity");
        tabs.SetItems(new UiTabOption[]
        {
            new("one", "A wider text label", Icon : icon), new("two", "Short", Icon : icon)
        });
        tabs.Options = new(Vertical : true, RightToLeft : true);
        Frame();
        check(Items().Zip(Icons).All(pair => pair.Second.Bounds.X + pair.Second.Bounds.Width ==
            pair.First.Node.Bounds.X + pair.First.Node.Bounds.Width - 10),
            "vertical right-to-left icon/text content anchors its icon to the leading edge of each actual tab");
        tabs.Options = new(Appearance : UiTabListAppearance.SubtleCircular);
        Frame();
        var first = Items()[0].Node.Bounds;
        var beforeGap = selection.Value;
        Frame(pointer : new(first.X + first.Width + 4, first.Y + first.Height / 2), down : true, pressed : true);
        Frame(pointer : new(first.X + first.Width + 4, first.Y + first.Height / 2), released : true);
        check(selection.Value == beforeGap, "the circular inter-tab gap is not an active tab hit target");
    }
}
