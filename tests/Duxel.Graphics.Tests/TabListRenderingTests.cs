using Duxel.Core;

internal static class TabListRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check,
        UiAccessibilityTree tree, int width, UiFluentColorScheme scheme)
    {
        foreach (var size in Enum.GetValues<UiTabListSize>())
        foreach (var vertical in new[] { false, true })
        foreach (var appearance in Enum.GetValues<UiTabListAppearance>())
        {
            var value = new UiState<string?>("b");
            using var tabs = Fluent.TabList("GPU tabs", value.Binding,
                new UiTabOption[] { new("a", "Alpha"), new("b", "Beta"), new("c", "Gamma", false) },
                new(size, vertical, Appearance : appearance));
            void Render(UiImmediateContext ui)
            {
                ui.EnableRootViewportContentLayout(contentPadding : 0);
                ui.SetCursorScreenPos(new(20, 20));
                tabs.Render(ui);
            }
            var name = $"{size}-{vertical}-{appearance}";
            capture(name + "-warmup", Render);
            var expected = capture(name + "-reference", Render);
            tree.TryGet(tree.Root.First!, out var group);
            tree.TryGet(group.FirstChild!, out var first);
            tree.TryGet(first.Next!, out var selected);
            var bounds = selected.Node.Bounds;
            var thickness = size == UiTabListSize.Small && !vertical ? 2 : 3;
            var circular = appearance is UiTabListAppearance.SubtleCircular or UiTabListAppearance.FilledCircular;
            var x = (int)MathF.Floor(circular ? bounds.X + 4 : vertical ? bounds.X + thickness / 2f : bounds.X + bounds.Width / 2);
            var y = (int)MathF.Floor(circular || vertical ? bounds.Y + bounds.Height / 2 : bounds.Y + bounds.Height - thickness / 2f);
            var index = (y * width + x) * 4;
            byte red = scheme == UiFluentColorScheme.Dark ? (byte)71 : (byte)15;
            byte green = scheme == UiFluentColorScheme.Dark ? (byte)158 : (byte)108;
            byte blue = scheme == UiFluentColorScheme.Dark ? (byte)245 : (byte)189;
            if (circular)
            {
                var color = TabAppearanceReference.Load().Single(item => item.Scheme == scheme && item.Appearance == appearance
                    && item.State == UiButtonVisualState.Rest && item.Selected).Paint.Background.Rgba;
                red = (byte)color; green = (byte)(color >> 8); blue = (byte)(color >> 16);
            }
            check(Math.Abs(expected[index] - red) <= 1 && Math.Abs(expected[index + 1] - green) <= 1 && Math.Abs(expected[index + 2] - blue) <= 1,
                "TabList selected indicator or circular fill matches independent reference channels : " + name);
            for (var frame = 0; frame < 3; frame++)
            {
                var actual = capture(name + "-replay-" + frame, Render);
                check(actual.AsSpan().SequenceEqual(expected), "TabList keeps its actual GPU appearance across acquired image reuse : " + name + " / " + frame);
            }
        }
        CheckStates(capture, check, width, scheme);
        CheckFontIcons(capture, check, tree, width, scheme);
    }

    private readonly struct OutlineIcon : IUiButtonIcon
    {
        public static void Paint(UiControlContext context, UiRect bounds, UiColor color, UiButtonInteraction interaction) =>
            context.Frame(new(bounds.X + 4, bounds.Y + 4, bounds.Width - 8, bounds.Height - 8), default, color, 0, 2);
    }
    private readonly struct FilledIcon : IUiButtonIcon
    {
        public static void Paint(UiControlContext context, UiRect bounds, UiColor color, UiButtonInteraction interaction) =>
            context.Frame(new(bounds.X + 4, bounds.Y + 4, bounds.Width - 8, bounds.Height - 8), color, default, 0, 0);
    }

    private static void CheckStates(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check,
        int width, UiFluentColorScheme scheme)
    {
        var baseline = capture("state-baseline", static _ => { });
        var icon = UiTabIcon.Create<OutlineIcon, FilledIcon>();
        var content = new UiTabOption("icon", "Accessible icon name", Icon : icon, IconOnly : true);
        foreach (var item in TabAppearanceReference.Load().Where(item => item.Scheme == scheme))
        {
            void Render(UiImmediateContext ui)
            {
                ui.BeginDisabled(item.State == UiButtonVisualState.Disabled);
                try
                {
                    UiFluentTabListPresentation.PaintTab(new UiControlContext(ui), content, new(20, 20, 96, 48), item.Selected,
                        new(false, item.State is UiButtonVisualState.Hover or UiButtonVisualState.Pressed, item.State == UiButtonVisualState.Pressed),
                        new(Appearance : item.Appearance));
                }
                finally { ui.EndDisabled(); }
            }
            var name = $"state-{item.Appearance}-{item.State}-{item.Selected}";
            var pixels = capture(name, Render);
            check(Pixel(30, 44, item.Paint.Background) && Pixel(68, 44, item.Selected ? item.Paint.Icon : item.Paint.Background),
                "actual GPU fill and regular/filled icon match official state tokens : " + name);
            var circular = item.Appearance is UiTabListAppearance.SubtleCircular or UiTabListAppearance.FilledCircular;
            check(Pixel(68, 66, circular || item.Paint.Indicator == default ? item.Paint.Background : item.Paint.Indicator),
                "actual GPU indicator is present only for the corresponding noncircular state : " + name);
            check(capture(name + "-replay", Render).AsSpan().SequenceEqual(pixels),
                "actual GPU state survives acquired-image reuse : " + name);

            bool Pixel(int x, int y, UiColor color)
            {
                var index = (y * width + x) * 4;
                for (var channel = 0; channel < 3; channel++)
                {
                    var expected = color.Rgba >> 24 == 0 ? baseline[index + channel] : (byte)(color.Rgba >> (channel * 8));
                    if (Math.Abs(pixels[index + channel] - expected) > 1) return false;
                }
                return true;
            }
        }
    }

    private static void CheckFontIcons(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check,
        UiAccessibilityTree tree, int width, UiFluentColorScheme scheme)
    {
        var baseline = capture("font-icon-baseline", static _ => { });
        var home = UiTabIcon.Create<Duxel.Gallery.HomeTabIcon, Duxel.Gallery.HomeTabFilledIcon>();
        var document = UiTabIcon.Create<Duxel.Gallery.DocumentTabIcon, Duxel.Gallery.DocumentTabFilledIcon>();
        var settings = UiTabIcon.Create<Duxel.Gallery.SettingsTabIcon, Duxel.Gallery.SettingsTabFilledIcon>();
        foreach (var appearance in Enum.GetValues<UiTabListAppearance>())
        {
            var selection = new UiState<string?>("home");
            using var tabs = Fluent.TabList("Actual font icons", selection.Binding, new UiTabOption[]
            {
                new("home", "Home", Icon : home, IconOnly : true),
                new("document", "Document", Icon : document, IconOnly : true),
                new("settings", "Settings", false, settings, true)
            }, new(Appearance : appearance));
            void Render(UiImmediateContext ui)
            {
                ui.EnableRootViewportContentLayout(contentPadding : 0);
                ui.SetCursorScreenPos(new(20, 20));
                tabs.Render(ui);
            }
            capture("font-icons-" + appearance + "-warmup", Render);
            var filled = capture("font-icons-" + appearance + "-home-selected", Render);
            tree.TryGet(tree.Root.First!, out var group);
            var items = new List<UiAccessibilityElement>();
            var id = group.FirstChild;
            while (id is not null && tree.TryGet(id, out var item)) { items.Add(item); id = item.Next; }
            var fixtures = TabAppearanceReference.Load().Where(item => item.Scheme == scheme && item.Appearance == appearance).ToArray();
            var filledBackground = fixtures.Single(item => item.Selected && item.State == UiButtonVisualState.Rest).Paint.Background;
            var regularBackground = fixtures.Single(item => !item.Selected && item.State == UiButtonVisualState.Rest).Paint.Background;
            var disabledBackground = fixtures.Single(item => !item.Selected && item.State == UiButtonVisualState.Disabled).Paint.Background;
            check(Ink(filled, items[0].Node.Bounds, filledBackground).Count(value => value) > 4
                && Ink(filled, items[1].Node.Bounds, regularBackground).Count(value => value) > 4
                && Ink(filled, items[2].Node.Bounds, disabledBackground).Count(value => value) > 4,
                "actual Home/Document/Settings font assets render nonempty selected, regular and disabled glyphs : " + appearance);
            selection.Value = "document";
            var regular = capture("font-icons-" + appearance + "-document-selected", Render);
            check(Ink(filled, items[0].Node.Bounds, filledBackground).Zip(Ink(regular, items[0].Node.Bounds, regularBackground))
                .Count(pair => pair.First != pair.Second) > 8,
                "actual home glyph changes geometry between selected filled and unselected regular exports : " + appearance);
        }

        bool[] Ink(byte[] pixels, UiRect bounds, UiColor background)
        {
            var result = new bool[400];
            var startX = (int)(bounds.X + (bounds.Width - 20) / 2);
            var startY = (int)(bounds.Y + (bounds.Height - 20) / 2);
            for (var y = 0; y < 20; y++)
            for (var x = 0; x < 20; x++)
            {
                var index = ((startY + y) * width + startX + x) * 4;
                for (var channel = 0; channel < 3; channel++)
                {
                    var value = background.Rgba >> 24 == 0 ? baseline[index + channel] : (byte)(background.Rgba >> (channel * 8));
                    result[y * 20 + x] |= Math.Abs(pixels[index + channel] - value) > 2;
                }
            }
            return result;
        }
    }
}
