using Duxel.Core;

internal static class MenuTests
{
    internal static void Run(Action<bool, string> check)
    {
        Run<UiThemeMenuPresentation>(check);
        Run<UiFluentMenuPresentation>(check);
        MenuScrollTests.Run(check);
        MenuExpansionTests.Run(check);
        MenuCheckboxTests.Run(check);
        MenuRadioTests.Run(check);
        MenuContentTests.Run(check);
    }
    private static void Run<TPresentation>(Action<bool, string> check) where TPresentation : IUiMenuPresentation
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        using var tree = new UiAccessibilityTree();
        ControlFontFixture.Configure(context);
        context.SetAccessibilityTree(tree);
        var calls = new List<string>();
        UiMenuOption[] items = [new("one", "One"), new("disabled", "Disabled", false), new("three", "Three")];
        using var menu = new UiMenu<TPresentation>("Actions", items, calls.Add);
        using var after = Fluent.Button("After");
        var show = true;
        UiInputState Input(UiVector2? pointer = null, bool down = false, bool pressed = false, bool released = false,
            UiKeyEvent[]? keys = null) => new(pointer ?? new(-100, -100), down, false, pressed, released, false, false,
                0, 0, keys ?? [], [], default, default);
        void Frame(UiInputState? input = null, float deltaSeconds = 1f / 60)
        {
            context.NewFrame(new(deltaSeconds, new(640, 400), new(1, 1)), input ?? Input(), new(0, 0, 640, 400), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding : 0); if (show) menu.Render(ui); after.Render(ui); }));
            using var draw = context.GetDrawData();
        }
        UiAccessibilityElement Find(string name, UiAccessibilityRole? role = null)
        {
            UiAccessibilityElement? found = null;
            void Visit(string? id)
            {
                while (id is not null && tree.TryGet(id, out var entry))
                {
                    if (entry.Node.Name == name && (role is null || entry.Node.Role == role)) found = entry;
                    Visit(entry.FirstChild);
                    id = entry.Next;
                }
            }
            Visit(tree.Root.First);
            return found ?? throw new InvalidOperationException("Missing menu control : " + name);
        }
        void Key(UiKey key, bool down = true, KeyModifiers modifiers = KeyModifiers.None) => Frame(Input(keys : [new(key, down, modifiers)]));
        void Click(string name, UiAccessibilityRole? role = null)
        {
            var node = Find(name, role).Node;
            var point = new UiVector2(node.Bounds.X + node.Bounds.Width / 2, node.Bounds.Y + node.Bounds.Height / 2);
            Frame(Input(point, down : true, pressed : true));
            Frame(Input(point, released : true));
            Frame();
        }
        Frame();
        check(!menu.IsOpen && context.State.NextScheduledFrameTimeSeconds is null, "menu starts closed with no scheduled animation");
        Key(UiKey.Tab);
        Key(UiKey.DownArrow);
        check(menu.IsOpen && Find("One").Node.Focused && Find("Actions", UiAccessibilityRole.Menu).Node.Role == UiAccessibilityRole.Menu,
            "Down opens the actual composite menu and focuses its first enabled item");
        check(!Find("Disabled").Node.Enabled, "disabled menu items retain disabled accessibility semantics");
        var third = Find("Three");
        tree.RequestAction(third.Node.Id, third.RuntimeId, UiAccessibilityAction.Focus, wait : false);
        Frame();
        check(Find("Three").Node.Focused && !Find("One").Node.Focused,
            "actual queued menu focus is resolved before any item publishes its focused state");
        Key(UiKey.Home);
        Key(UiKey.DownArrow);
        check(Find("Three").Node.Focused && calls.Count == 0, "menu arrows skip disabled rows without invoking actions");
        Key(UiKey.DownArrow);
        check(Find("One").Node.Focused, "menu arrow navigation wraps across enabled rows");
        Key(UiKey.End);
        Key(UiKey.Space);
        check(calls.Count == 0, "menu Space down does not invoke an action");
        Key(UiKey.Space, down : false);
        Frame();
        check(!menu.IsOpen && calls.SequenceEqual(["three"]) && Find("Actions", UiAccessibilityRole.Button).Node.Focused,
            "menu Space release invokes once, closes and restores the real trigger focus");
        Key(UiKey.UpArrow);
        check(Find("Three").Node.Focused, "Up opens the actual menu on its last enabled item");
        Key(UiKey.Escape);
        Frame();
        check(!menu.IsOpen && Find("Actions", UiAccessibilityRole.Button).Node.Focused, "Escape dismisses and restores trigger focus");
        Key(UiKey.DownArrow);
        Key(UiKey.Space);
        Key(UiKey.Escape);
        Key(UiKey.Space, down : false);
        Frame();
        check(!menu.IsOpen && calls.Count == 1, "Escape cancels a pending menu Space activation");
        Click("Actions", UiAccessibilityRole.Button);
        var retired = Find("One");
        Click("Three");
        check(!menu.IsOpen && calls.Count == 2 && calls[^1] == "three", "real pointer release invokes and closes the actual menu");
        check(tree.RequestAction(retired.Node.Id, retired.RuntimeId, UiAccessibilityAction.Invoke, wait : false) == UiAccessibilityResult.Unavailable,
            "closed menu item identities reject stale accessibility actions");
        Click("Actions", UiAccessibilityRole.Button);
        var one = Find("One");
        check(one.RuntimeId != retired.RuntimeId, "reopening a menu publishes fresh runtime identities");
        tree.RequestAction(one.Node.Id, one.RuntimeId, UiAccessibilityAction.Invoke, wait : false);
        Frame();
        Frame();
        check(!menu.IsOpen && calls.Count == 3 && calls[^1] == "one", "actual queued UIA invocation reaches the menu callback once");
        Key(UiKey.DownArrow);
        Key(UiKey.Tab);
        Frame();
        check(!menu.IsOpen && Find("After").Node.Focused, "Tab dismisses the menu and proceeds past its trigger");
        Click("Actions", UiAccessibilityRole.Button);
        var disabled = Find("Disabled");
        check(tree.RequestAction(disabled.Node.Id, disabled.RuntimeId, UiAccessibilityAction.Invoke, wait : false) == UiAccessibilityResult.Disabled,
            "disabled menu items reject real queued UIA invocation before enqueue");
        Click("Actions", UiAccessibilityRole.Button);
        check(!menu.IsOpen, "the real trigger toggles an already open menu closed");
        Click("Actions", UiAccessibilityRole.Button);
        Frame(Input(new(600, 350), down : true, pressed : true));
        Frame(Input(new(600, 350), released : true));
        Frame();
        check(!menu.IsOpen, "outside pointer input dismisses the transient menu");
        Click("Actions", UiAccessibilityRole.Button);
        menu.SetItems([new("three", "Three"), new("one", "One")]);
        Frame();
        check(menu.IsOpen && menu.Items.Length == 2, "menu items can be replaced without replacing their owning control");
        show = false;
        Frame();
        check(!menu.IsOpen, "removing the owner closes its active popup at frame completion");
        show = true;
        Frame();
        var invalid = false;
        try { menu.SetItems([new("same", "A"), new("same", "B")]); }
        catch (ArgumentException) { invalid = true; }
        check(invalid && menu.Items.Length == 2, "duplicate menu values fail before replacing valid items");
        ControlFontFixture.ConfigureEmoji(context, korean : true);
        UiMenuOption[] many = [new("apple", "Apple"), new("disabled-a", "Apricot disabled", false), new("apricot", "Apricot"),
            new("banana", "Banana"), new("korean", "한글 동작"), new("unicode", "🚀 Launch"),
            .. Enumerable.Range(0, 24).Select(i => new UiMenuOption("item-" + i, "Item " + i)), new("zulu", "Zulu")];
        menu.SetItems(many);
        menu.Options = menu.Options with { MaximumHeight = 138 };
        Click("Actions", UiAccessibilityRole.Button);
        check(menu.IsOpen && Find("Actions", UiAccessibilityRole.Menu).Node.Bounds.Height == 138
            && Find("Zulu").Node.Offscreen, "long menu uses a bounded surface and clips hidden rows");
        void Text(uint rune) => Frame(Input() with { CharEvents = new UiCharEvent[] { new(rune) } });
        Text('a');
        check(Find("Apricot").Node.Focused, "menu printable-character search skips disabled matches");
        Text('A');
        check(Find("Apple").Node.Focused, "repeated menu letters cycle case-insensitive matches");
        Text('한');
        check(Find("한글 동작").Node.Focused && !Find("한글 동작").Node.Offscreen,
            "menu Unicode search reveals a matching Korean row in the same frame");
        Text(0x1f680);
        check(Find("🚀 Launch").Node.Focused, "menu search accepts supplementary-plane Unicode characters");
        Text('z');
        check(Find("Zulu").Node.Focused && !Find("Zulu").Node.Offscreen && Find("Apple").Node.Offscreen,
            "typeahead scrolls the real last row into view while clipping previous rows");
        check(!context.State.HasActiveAnimations && context.State.NextScheduledFrameTimeSeconds is not null,
            "typeahead scrolling schedules only the finite scrollbar transition");
        Frame(deltaSeconds: 1);
        Frame();
        check(!context.State.HasActiveAnimations && context.State.NextScheduledFrameTimeSeconds is null,
            "stationary long menu returns to event wait after its scrollbar transition");
        Key(UiKey.Home);
        check(Find("Apple").Node.Focused && !Find("Apple").Node.Offscreen, "Home reveals the first long-menu row");
        Key(UiKey.End);
        check(Find("Zulu").Node.Focused && !Find("Zulu").Node.Offscreen, "End reveals the last long-menu row");
        Key(UiKey.DownArrow);
        check(Find("Apple").Node.Focused && !Find("Apple").Node.Offscreen, "wrapped arrow navigation reveals its long-menu destination");
        var longBounds = Find("Actions", UiAccessibilityRole.Menu).Node.Bounds;
        Frame(Input(new(longBounds.X + 20, longBounds.Y + 20)) with { MouseWheel = -20 });
        check(!Find("Zulu").Node.Offscreen && Find("Apple").Node.Offscreen, "long menu wheel uses the shared scroll range");
        Key(UiKey.End);
        Key(UiKey.Enter);
        Frame();
        check(!menu.IsOpen && calls[^1] == "zulu", "keyboard invokes a revealed long-menu item exactly once");
        var badHeight = false;
        try { menu.Options = menu.Options with { MaximumHeight = 1 }; }
        catch (ArgumentOutOfRangeException) { badHeight = true; }
        check(badHeight && menu.Options.MaximumHeight == 138, "invalid menu maximum height fails before changing options");
        Click("Actions", UiAccessibilityRole.Button);
        Key(UiKey.End);
        menu.SetItems([many[^1], .. many[..^1]]);
        Frame();
        check(Find("Zulu").Node.Focused && !Find("Zulu").Node.Offscreen,
            "replacing long-menu order preserves and reveals the focused stable value");
        var oldCalls = calls.Count;
        Key(UiKey.Space);
        Text('b');
        Key(UiKey.Space, down : false);
        check(menu.IsOpen && calls.Count == oldCalls && Find("Banana").Node.Focused,
            "character navigation cancels pending Space before moving focus");
        Frame(Input() with { Modifiers = KeyModifiers.Ctrl, CharEvents = new UiCharEvent[] { new('z') } });
        check(Find("Banana").Node.Focused, "modified text does not navigate the menu");
        Text('x');
        check(Find("Banana").Node.Focused, "unmatched printable input leaves real focus unchanged");
        Key(UiKey.Escape);
        menu.Options = menu.Options with { MaximumHeight = null };
        Click("Actions", UiAccessibilityRole.Button);
        check(Find("Actions", UiAccessibilityRole.Menu).Node.Bounds.Height <= 400 && Find("Item 23").Node.Offscreen,
            "default long menu height is bounded by its viewport without an explicit maximum");
        Key(UiKey.Escape);
        menu.SetItems(many);
        menu.Options = menu.Options with { MaximumHeight = 138 };
        Click("Actions", UiAccessibilityRole.Button);
        var menuBounds = Find("Actions", UiAccessibilityRole.Menu).Node.Bounds;
        var firstBounds = Find("Apple").Node.Bounds;
        var trackX = (firstBounds.X + firstBounds.Width + menuBounds.X + menuBounds.Width - menu.Options.Padding) / 2;
        var thumb = new UiVector2(trackX, menuBounds.Y + menu.Options.Padding + 8);
        Frame(Input(thumb, down : true, pressed : true));
        Frame(Input(new(trackX, menuBounds.Y + menuBounds.Height - menu.Options.Padding - 8), down : true));
        Frame(Input(new(trackX, menuBounds.Y + menuBounds.Height - menu.Options.Padding - 8), released : true));
        check(menu.IsOpen && !Find("Zulu").Node.Offscreen && calls.Count == oldCalls,
            "shared scrollbar drag reaches the last menu row without invoking any item");
        Key(UiKey.Escape);
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
