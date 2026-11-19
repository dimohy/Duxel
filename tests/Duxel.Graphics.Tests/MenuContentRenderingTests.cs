using System.Text.Json;
using Duxel.Core;

internal static class MenuContentRenderingTests
{
    private readonly struct CaptionReference : IUiTypographyMetrics
    {
        public static float FontSize => 12;
        public static float LineHeight => 20;
    }
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<UiInputState> setInput,
        Action<bool, string> check, UiAccessibilityTree tree, int width, UiFluentColorScheme scheme)
    {
        var icon = UiMenuIcon.Create<UiFluentAddIcon, UiFluentAddFilledIcon>();
        var state = new UiState<bool>(true);
        var calls = new List<string>();
        UiMenuOption First(bool enabled) => new("open", "Open", enabled) { Icon = icon, SecondaryContent = "PNG" };
        using var menu = Fluent.Menu("Rich actions", [First(true), UiMenuOption.Checkbox("track", "Track", state), new("plain", "Plain")], calls.Add,
            new UiMenuOptions(260, 32, 5, ItemSpacing : 2, MaximumWidth : 260));
        var row = default(UiRect);
        var phase = UiButtonVisualState.Rest;
        var secondaryHover = false;
        UiRect iconRect = default, secondaryRect = default;
        UiAccessibilityElement Find(string name, UiAccessibilityRole role = UiAccessibilityRole.MenuItem)
        {
            tree.TryGet(tree.Root.First!, out var trigger);
            if (role == UiAccessibilityRole.Button) return trigger;
            tree.TryGet(trigger.Next!, out var surface);
            var id = surface.FirstChild;
            while (id is not null && tree.TryGet(id, out var item))
            {
                if (item.Node.Name == name) return item;
                id = item.Next;
            }
            throw new InvalidOperationException("Missing GPU content menu item : " + name);
        }
        UiColor Background(UiButtonVisualState visual)
        {
            using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "MenuPalette.json")));
            var entry = document.RootElement.EnumerateArray().Single(value => value.GetProperty("scheme").GetString() == scheme.ToString()
                && value.GetProperty("state").GetString() == visual.ToString());
            var rgb = entry.GetProperty("background").EnumerateArray().Select(value => value.GetByte()).ToArray();
            return new(rgb[0], rgb[1], rgb[2]);
        }
        void Render(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding : 8);
            menu.Render(ui);
            if (row.Height == 0) return;
            var control = new UiControlContext(ui);
            var expected = MenuContentReference.Resolve(scheme, phase);
            var rtl = menu.Options.RightToLeft;
            // Independent pinned geometry: root6, check16+gap4, icon20, caption12/line20, text inset8.
            iconRect = new(rtl ? row.X + row.Width - 46 : row.X + 26, row.Y + 6, 20, 20);
            using var typography = control.BeginTypography<UiRegularTypeface, CaptionReference>();
            var text = control.MeasureTextBlock("PNG");
            var advance = control.MeasureText("PNG").X;
            secondaryRect = new(rtl ? row.X + 8 : row.X + row.Width - 8 - advance, row.Y + 6, advance, 20);
            control.Frame(row with { Y = row.Y + 144 }, Background(phase), default, 0, 0);
            var expectedIcon = iconRect with { Y = iconRect.Y + 144 };
            if (phase is UiButtonVisualState.Hover or UiButtonVisualState.Pressed)
                UiFluentAddFilledIcon.Paint(control, expectedIcon, expected.Icon, default);
            else UiFluentAddIcon.Paint(control, expectedIcon, expected.Icon, default);
            var textRect = secondaryRect with { Y = secondaryRect.Y + 144 };
            control.TypographyTextBlock(text, textRect, secondaryHover ? expected.SecondaryHover : expected.Secondary,
                rtl ? UiItemHorizontalAlign.Left : UiItemHorizontalAlign.Right);
        }
        bool SameReference(byte[] pixels, UiRect rect)
        {
            for (var y = (int)MathF.Floor(rect.Y); y < MathF.Ceiling(rect.Y + rect.Height); y++)
                for (var x = (int)MathF.Floor(rect.X); x < MathF.Ceiling(rect.X + rect.Width); x++)
                    if (!pixels.AsSpan((y * width + x) * 4, 4).SequenceEqual(pixels.AsSpan(((y + 144) * width + x) * 4, 4))) return false;
            return true;
        }
        UiInputState Input(UiVector2? pointer = null, bool down = false, bool pressed = false) =>
            new(pointer ?? new(-100, -100), down, false, pressed, false, false, false, 0, 0, [], [], default, default);
        setInput(Input());
        capture("closed", Render);
        var trigger = Find("Rich actions", UiAccessibilityRole.Button);
        tree.RequestAction(trigger.Node.Id, trigger.RuntimeId, UiAccessibilityAction.Expand, wait : false);
        capture("open", Render);
        row = Find("Open").Node.Bounds;
        foreach (var test in new[] { ("rest", UiButtonVisualState.Rest, false), ("hover-label", UiButtonVisualState.Hover, false),
            ("hover-secondary", UiButtonVisualState.Hover, true), ("pressed", UiButtonVisualState.Pressed, true), ("disabled", UiButtonVisualState.Disabled, false) })
        {
            (var name, phase, secondaryHover) = test;
            if (phase == UiButtonVisualState.Disabled) menu.SetItems([First(false), UiMenuOption.Checkbox("track", "Track", state), new("plain", "Plain")]);
            var pointer = phase == UiButtonVisualState.Rest ? new UiVector2(-100, -100)
                : new UiVector2(secondaryHover ? row.X + row.Width - 16 : row.X + 75, row.Y + 16);
            setInput(Input(pointer, phase == UiButtonVisualState.Pressed, phase == UiButtonVisualState.Pressed));
            var pixels = capture(name, Render);
            check(SameReference(pixels, iconRect), "actual menu icon pixels match the independent regular/filled glyph and pinned color : " + name);
            check(SameReference(pixels, secondaryRect), "actual secondary pixels match independent 12px/20px typography and local hover color : " + name);
        }
        menu.SetItems([First(true), UiMenuOption.Checkbox("track", "Track", state), new("plain", "Plain")]);
        menu.Options = menu.Options with { RightToLeft = true };
        phase = UiButtonVisualState.Rest;
        secondaryHover = false;
        setInput(Input());
        var rtlPixels = capture("rtl", Render);
        check(SameReference(rtlPixels, iconRect) && SameReference(rtlPixels, secondaryRect),
            "actual RTL icon and trailing caption mirror independent logical column references");
        check(capture("rtl-replay", Render).AsSpan().SequenceEqual(rtlPixels) && calls.Count == 0,
            "rich menu idle pixels replay without callbacks");
        trigger = Find("Rich actions", UiAccessibilityRole.Button);
        tree.RequestAction(trigger.Node.Id, trigger.RuntimeId, UiAccessibilityAction.Collapse, wait : false);
        capture("cleanup", Render);
    }
}
