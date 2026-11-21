using System.Text.Json;
using Duxel.Core;

internal static class FluentMenuTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "MenuPalette.json")));
        foreach (var entry in fixture.RootElement.EnumerateArray())
        {
            var scheme = Enum.Parse<UiFluentColorScheme>(entry.GetProperty("scheme").GetString()!);
            var state = Enum.Parse<UiButtonVisualState>(entry.GetProperty("state").GetString()!);
            UiColor Color(string name)
            {
                var values = entry.GetProperty(name).EnumerateArray().Select(value => value.GetByte()).ToArray();
                return new(values[0], values[1], values[2]);
            }
            var actual = UiFluentMenuPalette.Resolve(scheme, state);
            check(actual.Background == Color("background") && actual.Foreground == Color("foreground"),
                "Fluent menu colors match the independent pinned official token fixture : " + scheme + " / " + state);
        }
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        using var tree = new UiAccessibilityTree();
        ControlFontFixture.Configure(context);
        context.SetAccessibilityTree(tree);
        using var menu = Fluent.Menu("Menu", [new("one", "One"), new("two", "Two"), new("three", "Three")], static _ => { });
        UiWidget current = menu;
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(UiInputState? input = null)
        {
            context.NewFrame(new(1f / 60, new(640, 400), new(1, 1)), input ?? idle,
                new(0, 0, 640, 400), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding : 0); current.Render(ui); }));
            using var draw = context.GetDrawData();
        }
        Frame();
        tree.TryGet(tree.Root.First!, out var trigger);
        tree.RequestAction(trigger.Node.Id, trigger.RuntimeId, UiAccessibilityAction.Invoke, wait : false);
        Frame();
        tree.TryGet(trigger.Node.Id, out trigger);
        tree.TryGet(trigger.Next!, out var surface);
        check(menu is UiFluentMenu && Fluent.ComponentNames.Contains("Menu") && !Fluent.MissingComponentNames.Contains("Menu"),
            "the generated Fluent Menu factory and exposed catalog identify the actual compiled presentation");
        check(surface.Node.Bounds.Width == 138 && surface.Node.Bounds.Height == 110,
            "actual Fluent menu geometry follows 138 minimum width, 5 inset, 32 rows and 2 gaps");
        tree.TryGet(surface.FirstChild!, out var first);
        tree.TryGet(first.Next!, out var second);
        check(first.Node.Bounds.X == surface.Node.Bounds.X + 5 && first.Node.Bounds.Y == surface.Node.Bounds.Y + 5
            && second.Node.Bounds.Y - first.Node.Bounds.Y == 34, "actual Fluent menu child allocations include independent official insets and list gap");
        menu.SetItems([new("wide", new string('W', 200))]);
        Frame();
        tree.TryGet(trigger.Next!, out surface);
        check(surface.Node.Bounds.Width == 300, "actual Fluent long-label menu respects the official maximum surface width");
        var invalid = false;
        try { menu.Options = menu.Options with { ItemSpacing = -1 }; }
        catch (ArgumentOutOfRangeException) { invalid = true; }
        check(invalid && menu.Options.ItemSpacing == 2, "invalid item spacing fails before replacing valid menu options");
        using var menuButton = Fluent.MenuButton("Export", [new("pdf", "PDF")], static _ => { });
        check(menuButton is UiFluentMenuButton && Fluent.ComponentNames.Contains("MenuButton")
            && !Fluent.RequiredComponentNames.Contains("MenuButton"),
            "the generated MenuButton factory exposes the Fluent trigger as a Button variant");

        var invocations = new List<string>();
        using var custom = new UiMenu<PressPopupBehavior, UiFluentMenuPresentation>("Custom menu", [new("run", "Run")], invocations.Add);
        current = custom;
        Frame();
        UiAccessibilityElement Find(string name, UiAccessibilityRole role)
            => tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == name && element.Node.Role == role);
        UiVector2 Center(UiAccessibilityElement element)
        {
            var bounds = element.Node.Bounds;
            return new(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
        }
        var point = Center(Find("Custom menu", UiAccessibilityRole.Button));
        Frame(idle with { MousePosition = point, LeftMousePressed = true, LeftMouseReleased = true });
        check(custom.IsOpen && invocations.Count == 0, "Custom Menu opens on a single-frame press and release");
        var command = Center(Find("Run", UiAccessibilityRole.MenuItem));
        Frame(idle with { MousePosition = command, LeftMousePressed = true, LeftMouseDown = true });
        check(!custom.IsOpen && invocations.SequenceEqual(["run"]), "Custom Menu invokes and closes on row press");
        Frame(idle with { MousePosition = command, LeftMouseReleased = true });
        check(!custom.IsOpen && invocations.SequenceEqual(["run"]), "Custom Menu does not invoke again on row release");
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}

internal readonly struct PressPopupBehavior : IUiMenuBehavior
{
    public static UiButtonActivation TriggerActivation => UiButtonActivation.Press;
    public static UiButtonActivation OptionActivation => UiButtonActivation.Press;
}
