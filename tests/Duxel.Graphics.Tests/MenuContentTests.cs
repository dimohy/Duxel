using System.Text.Json;
using Duxel.Core;

internal static class MenuContentReference
{
    internal static (UiColor Icon, UiColor Secondary, UiColor SecondaryHover) Resolve(UiFluentColorScheme scheme, UiButtonVisualState state)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "MenuContentPalette.json")));
        var entry = document.RootElement.EnumerateArray().Single(value => value.GetProperty("scheme").GetString() == scheme.ToString()
            && value.GetProperty("state").GetString() == state.ToString());
        UiColor Color(string name)
        {
            var rgb = entry.GetProperty(name).EnumerateArray().Select(value => value.GetByte()).ToArray();
            return new(rgb[0], rgb[1], rgb[2]);
        }
        return (Color("icon"), Color("secondary"), Color("secondaryHover"));
    }
}

internal static class MenuContentTests
{
    internal static void Run(Action<bool, string> check)
    {
        foreach (var scheme in Enum.GetValues<UiFluentColorScheme>())
            foreach (var state in Enum.GetValues<UiButtonVisualState>())
            {
                var expected = MenuContentReference.Resolve(scheme, state);
                check(UiFluentMenuPalette.Icon(scheme, state) == expected.Icon
                    && UiFluentMenuPalette.Secondary(scheme, false, state != UiButtonVisualState.Disabled) == expected.Secondary
                    && UiFluentMenuPalette.Secondary(scheme, true, state != UiButtonVisualState.Disabled) == expected.SecondaryHover,
                    "menu icon and local secondary hover match independent pinned tokens : " + scheme + " / " + state);
            }
        Run<UiThemeMenuPresentation>(check);
        Run<UiFluentMenuPresentation>(check);
    }
    private static void Run<TPresentation>(Action<bool, string> check) where TPresentation : IUiMenuPresentation
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        using var tree = new UiAccessibilityTree();
        ControlFontFixture.Configure(context);
        context.SetAccessibilityTree(tree);
        var icon = UiMenuIcon.Create<MenuRegularProbe, MenuFilledProbe>();
        var calls = new List<string>();
        using var menu = new UiMenu<TPresentation>("Content", [new("open", "Open") { Icon = icon, SecondaryContent = "ZIP" },
            new("plain", "Plain"), new("disabled", "Disabled", false) { Icon = icon, SecondaryContent = "Unavailable" }], calls.Add);
        UiMenuContentLayout layout = default;
        float secondaryWidth = 0;
        var fixture = new UiMenuItemContent("Label", true, true) { HasIcons = true, Icon = icon, SecondaryContent = "ZIP" };
        void Frame(UiInputState? input = null)
        {
            MenuIconProbe.Calls.Clear();
            context.NewFrame(new(1f / 60, new(640, 400), new(1, 1)), input ?? new(new(-100, -100), false, false,
                false, false, false, false, 0, 0, [], [], default, default), new(0, 0, 640, 400), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding : 8);
                menu.Render(ui);
                var control = new UiControlContext(ui);
                using (control.BeginTypography<UiRegularTypeface, UiMenuSecondaryMetrics>()) secondaryWidth = control.MeasureText("ZIP").X;
                layout = UiMenuContentLayout.Arrange(control, fixture, new(10, 20, 240, 32), menu.Options.RightToLeft, 8, 6);
            }));
            using var draw = context.GetDrawData();
        }
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
            throw new InvalidOperationException("Missing content menu row : " + name);
        }
        void Open()
        {
            Frame();
            var trigger = Find("Content", UiAccessibilityRole.Button);
            tree.RequestAction(trigger.Node.Id, trigger.RuntimeId, UiAccessibilityAction.Expand, wait : false);
            Frame();
        }
        Open();
        check(MenuIconProbe.Calls.Count == 2 && MenuIconProbe.Calls.All(call => !call.Filled && call.Bounds.Width == 20 && call.Bounds.Height == 20),
            "actual menu gives compiled regular icons a 20px slot and does not paint a missing icon");
        check(Find("Open").Node.Name == "Open" && Find("Open").Node.GetHelpText() == "ZIP"
            && Find("Disabled").Node.GetHelpText() == "Unavailable",
            "secondary menu text is discoverable HelpText without replacing the action name");
        check(layout.Checkmark == new UiRect(16, 28, 16, 16) && layout.Icon == new UiRect(36, 26, 20, 20)
            && layout.Label.X == 62 && MathF.Abs(layout.Label.Width - (172 - secondaryWidth)) < .01f
            && MathF.Abs(layout.Secondary.X - (242 - secondaryWidth)) < .01f,
            "checkmark, icon, label and trailing columns follow independent Fluent slot/inset/gap geometry");
        var row = Find("Open").Node.Bounds;
        Frame(new(new(row.X + 10, row.Y + 16), false, false, false, false, false, false, 0, 0, [], [], default, default));
        check(MenuIconProbe.Calls[0].Filled && !MenuIconProbe.Calls[1].Filled,
            "enabled hover selects the filled icon while the disabled row keeps its regular glyph");
        var focused = context.State.FocusedId;
        Frame();
        check(context.State.FocusedId == focused && calls.Count == 0 && context.State.NextScheduledFrameTimeSeconds is null,
            "icon hover and secondary hit queries do not invoke, change focus or request an animation deadline");
        Frame(new(new(-100, -100), false, false, false, false, false, false, 0, 0, [new(UiKey.End, true, 0)], [], default, default));
        Frame(new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [new('z')], default, default));
        check(Find("Plain").Node.Focused, "first-character navigation uses the primary label rather than secondary text");
        menu.Options = menu.Options with { RightToLeft = true };
        Frame();
        check(layout.Checkmark.X == 228 && layout.Icon.X == 204 && layout.Label.X + layout.Label.Width == 198
            && layout.Secondary.X == 18, "RTL mirrors complete menu content columns in logical order");
        var old = Find("Open");
        menu.SetItems([new("open", "Open") { Icon = icon, SecondaryContent = "PNG" }, new("plain", "Plain")]);
        Frame();
        check(Find("Open").RuntimeId == old.RuntimeId && Find("Open").Node.GetHelpText() == "PNG",
            "secondary content updates retain the actual item identity and refresh HelpText");
        var rejected = 0;
        foreach (var invalid in new[] { "", "  ", "two\nlines", "tab\ttext" })
        {
            try { menu.SetItems([new("bad", "Bad") { SecondaryContent = invalid }]); }
            catch (ArgumentException) { rejected++; }
        }
        check(rejected == 4 && menu.Items.Length == 2, "invalid secondary strings fail before replacing valid menu entries");
        var open = Find("Open").Node.Bounds;
        var pointer = new UiVector2(open.X + open.Width - 10, open.Y + 16);
        Frame(new(pointer, true, false, true, false, false, false, 0, 0, [], [], default, default));
        Frame(new(pointer, false, false, false, true, false, false, 0, 0, [], [], default, default));
        check(!menu.IsOpen && calls.SequenceEqual(["open"]), "secondary region pointer release invokes the ordinary menu action once");
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}

internal static class MenuIconProbe
{
    internal static readonly List<(bool Filled, UiRect Bounds, UiColor Color)> Calls = [];
    internal static void Paint(UiControlContext context, UiRect bounds, UiColor color, bool filled)
    {
        Calls.Add((filled, bounds, color));
        context.Frame(new(bounds.X + 6, bounds.Y + 6, 8, 8), color, default, 0, 0);
    }
}
internal readonly struct MenuRegularProbe : IUiButtonIcon
{
    public static void Paint(UiControlContext context, UiRect bounds, UiColor color, UiButtonInteraction interaction) => MenuIconProbe.Paint(context, bounds, color, false);
}
internal readonly struct MenuFilledProbe : IUiButtonIcon
{
    public static void Paint(UiControlContext context, UiRect bounds, UiColor color, UiButtonInteraction interaction) => MenuIconProbe.Paint(context, bounds, color, true);
}
