using Duxel.Core;

internal static class SearchBoxTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        var valueEvents = new List<UiAccessibilityTextChange>();
        tree.TextPropertyChanged += change => { if (change.Property == UiAccessibilityTextProperty.Value) valueEvents.Add(change); };
        var text = "정세일";
        var reject = false;
        var normalize = false;
        var writes = 0;
        using var search = Fluent.SearchBox("Search", new UiBinding<string>(() => text, value => { writes++; if (!reject) text = normalize ? value.ToUpperInvariant() : value; }), 32);
        using var following = Fluent.Button("Following", () => { });
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(UiInputState? input = null)
        {
            context.NewFrame(new(1f / 60, new(640, 480), new(1, 1)), input ?? idle, new(0, 0, 640, 480), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding: 0); search.Render(ui); following.Render(ui); }));
            using var data = context.GetDrawData();
        }
        List<UiAccessibilityElement> Nodes()
        {
            var all = new List<UiAccessibilityElement>();
            void Visit(string? id)
            {
                while (id is not null && tree.TryGet(id, out var e))
                {
                    all.Add(e);
                    Visit(e.FirstChild);
                    id = e.Next;
                }
            }
            Visit(tree.Root.First);
            return all;
        }
        UiAccessibilityElement Find(string name) => Nodes().Single(e => e.Node.Name == name);
        void Send(string name, UiAccessibilityAction action, string? value = null)
        {
            var e = Find(name);
            check(tree.RequestAction(e.Node.Id, e.RuntimeId, action, value, wait: false) == UiAccessibilityResult.Success, "Search native request " + action);
            Frame();
        }
        void Type(string value) => Frame(idle with
        {
            CharEvents = value.EnumerateRunes().Select(r => new UiCharEvent((uint)r.Value)).ToArray()
        });
        Frame();
        check(Find("Search").Node.Role == UiAccessibilityRole.SearchBox && Find("Search").Node.Value == text && Nodes().All(n => n.Node.Name != "Clear search"), "Search semantic editor, bound Korean value and unfocused clear absence");
        Send("Search", UiAccessibilityAction.Focus);
        Frame();
        check(Nodes().Any(n => n.Node.Name == "Clear search") && !Find("Clear search").Node.Focusable, "Search focused clear is an actual non-tab-stop button");
        search.AccessibleName = "Custom search";
        Frame();
        check(Find("Clear search").Node.Name == "Clear search", "Search explicit accessible name does not leak into clear action");
        search.AccessibleName = null;
        Frame();
        var runtime = Find("Search").RuntimeId;
        Send("Clear search", UiAccessibilityAction.Invoke);
        check(text.Length == 0 && Find("Search").Node.Value == "" && Find("Search").Node.Focused, "Search native clear synchronizes accepted value and restores editor focus");
        Type("한글");
        check(text == "한글", "Search accepts Korean typing immediately after clearing");
        var clear = Find("Clear search").Node.Bounds;
        var point = new UiVector2(clear.X + clear.Width / 2, clear.Y + clear.Height / 2);
        Frame(idle with
        {
            MousePosition = point,
            LeftMouseDown = true,
            LeftMousePressed = true
        });
        Frame(idle with
        {
            MousePosition = point,
            LeftMouseReleased = true
        });
        check(text.Length == 0 && Find("Search").Node.Focused, "Search real pointer press-release clears and restores focus");
        Type("again");
        check(text == "again", "Search remains editable after pointer clear");
        var eventCount = valueEvents.Count;
        reject = true;
        Send("Search", UiAccessibilityAction.SetValue, "rejected");
        check(text == "again" && Find("Search").Node.Value == "again", "Search rejected native edit reads authoritative value in the same frame");
        Send("Clear search", UiAccessibilityAction.Invoke);
        check(text == "again" && Find("Search").Node.Value == "again", "Search rejected clear retains accepted text");
        check(valueEvents.Count == eventCount, "Search rejected native edit and clear emit no unaccepted Value events");
        reject = false;
        normalize = true;
        Send("Search", UiAccessibilityAction.SetValue, "mixed");
        check(valueEvents.Last().NewValue == "MIXED", "Search native Value event publishes accepted normalization");
        check(text == "MIXED" && Find("Search").Node.Value == "MIXED", "Search normalization is published in the editing frame");
        search.Options = search.Options with
        {
            ReadOnly = true
        };
        Frame();
        var read = Find("Search");
        var count = writes;
        check(read.Node.ReadOnly && Nodes().All(n => n.Node.Name != "Clear search") && tree.RequestAction(read.Node.Id, read.RuntimeId, UiAccessibilityAction.SetValue, "x", wait: false) == UiAccessibilityResult.ReadOnly, "Search readonly blocks native writes and hides clear");
        Type("x");
        check(valueEvents.Last().NewValue == "MIXED", "Search native Value event publishes accepted normalization");
        check(text == "MIXED" && writes == count, "Search readonly keyboard cannot change binding");
        search.Options = search.Options with
        {
            ReadOnly = false
        };
        foreach (var size in Enum.GetValues<UiSearchBoxSize>())
        {
            foreach (var appearance in Enum.GetValues<UiInputAppearance>())
            {
                foreach (var rtl in new[] { false, true })
                {
                    search.Options = new(Width: 600, Size: size, Appearance: appearance, RightToLeft: rtl);
                    Frame();
                    var bounds = Find("Search").Node.Bounds;
                    check(bounds.Width == 468 && bounds.Height == (size == UiSearchBoxSize.Small ? 24 : size == UiSearchBoxSize.Large ? 40 : 32) && Find("Search").RuntimeId == runtime,
                        $"Search stable provider, maximum width and size {size}/{appearance}/{rtl}");
                    var button = Find("Clear search").Node.Bounds;
                    check(button.X >= bounds.X && button.X + button.Width <= bounds.X + bounds.Width && (rtl ? button.X < bounds.X + bounds.Width / 2 : button.X > bounds.X + bounds.Width / 2), "Search directional clear stays within field");
                }
            }
        }
        Frame(idle with
        {
            KeyEvents = [new(UiKey.Tab, true, default)]
        });
        check(Find("Following").Node.Focused && Nodes().All(n => n.Node.Name != "Clear search"), "Search Tab skips clear and removes unfocused clear provider");
        search.Options = search.Options with
        {
            Width = 28
        };
        Frame();
        check(Find("Search").Node.Bounds.Width == 28, "Search constrained narrow field retains assigned width without invalid geometry");
        check(Fluent.ComponentNames.Contains("SearchBox") && !Fluent.MissingComponentNames.Contains("SearchBox"), "Search connects catalog factory");
    }
    private sealed class Screen(Action<UiImmediateContext> draw) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => draw(ui);
    }
}
