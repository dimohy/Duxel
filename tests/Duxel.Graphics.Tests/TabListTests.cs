using Duxel.Core;

internal static class TabListTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        using var tree = new UiAccessibilityTree();
        ControlFontFixture.Configure(context);
        context.SetDesign(UiCompiledDesign.Windows11Dark);
        context.SetAccessibilityTree(tree);
        var value = new UiState<string?>("one");
        UiTabOption[] items = [new("one", "One"), new("disabled", "Disabled", false), new("three", "Three")];
        using var tabs = Fluent.TabList("Example tabs", value.Binding, items);
        using var after = Fluent.Button("After tabs");
        UiInputState Input(UiVector2? pointer = null, bool down = false, bool pressed = false, bool released = false, UiKeyEvent[]? keys = null) =>
            new(pointer ?? new(-100, -100), down, false, pressed, released, false, false, 0, 0, keys ?? [], [], default, default);
        UiRect selectionInk = default;
        void Frame(UiInputState? input = null)
        {
            context.NewFrame(new(1f / 60, new(800, 500), new(1, 1)), input ?? Input(), new(0, 0, 800, 500), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding : 0); tabs.Render(ui); after.Render(ui); }));
            using var draw = context.GetDrawData();
            var color = UiFluentTabListPalette.Resolve(UiFluentColorScheme.Dark, UiButtonVisualState.Rest,
                UiTabListAppearance.Transparent, true).Indicator;
            selectionInk = default;
            foreach (var list in draw.DrawLists)
                if (list.RectFilledPrimitives is { } primitives)
                    foreach (var primitive in primitives)
                        if (primitive.Color == color && primitive.Rect.Height == 3)
                            selectionInk = primitive.Rect;
        }
        UiAccessibilityElement Find(string name)
        {
            tree.TryGet(tree.Root.First!, out var group);
            if (group.Node.Name == name) return group;
            var id = group.FirstChild;
            while (id is not null && tree.TryGet(id, out var item))
            {
                if (item.Node.Name == name) return item;
                id = item.Next;
            }
            throw new InvalidOperationException("Missing tab : " + name);
        }
        void Key(UiKey key, bool down = true) => Frame(Input(keys : [new(key, down, KeyModifiers.None)]));
        Frame();
        check(Find("Example tabs").Node.Role == UiAccessibilityRole.TabList && Find("One").Node.Selected == true
            && Find("Three").Node.Selected == false, "generated TabList publishes one actual selected tab under its named tab-list owner");
        check(!Find("Disabled").Node.Enabled, "TabList preserves disabled child semantics");
        Key(UiKey.Tab);
        check(Find("One").Node.Focused, "Tab enters the selected item as one group stop");
        check(!Find("Example tabs").Node.Focused,
            "the nonfocusable tab-list parent does not claim its focused child's keyboard focus");
        Key(UiKey.RightArrow);
        check(Find("Three").Node.Focused && value.Value == "one", "manual TabList arrow focus skips disabled items without changing selection");
        Key(UiKey.Enter);
        check(value.Value == "three" && Find("Three").Node.Selected == true, "Enter selects the focused tab through the actual binding");
        Key(UiKey.RightArrow);
        check(Find("One").Node.Focused, "horizontal TabList focus wraps around enabled items");
        Key(UiKey.Space);
        check(value.Value == "three", "Space down alone does not select a tab");
        Key(UiKey.Space, down : false);
        check(value.Value == "one", "Space release selects the focused tab");
        Key(UiKey.End);
        check(Find("Three").Node.Focused, "End moves focus to the final enabled tab");
        Key(UiKey.Home);
        check(Find("One").Node.Focused, "Home moves focus to the first enabled tab");
        tabs.Options = tabs.Options with { SelectOnFocus = true };
        Key(UiKey.RightArrow);
        check(value.Value == "three", "select-on-focus is independent from the default manual activation mode");
        tabs.Options = tabs.Options with { Vertical = true, SelectOnFocus = false };
        Key(UiKey.UpArrow);
        check(Find("One").Node.Focused && value.Value == "three", "vertical navigation uses its vertical axis without implicit selection");
        Key(UiKey.RightArrow);
        check(Find("One").Node.Focused, "vertical TabList does not intercept its perpendicular arrow axis");
        var one = Find("One");
        var three = Find("Three");
        check(tree.RequestAction(one.Node.Id, one.RuntimeId, UiAccessibilityAction.Select, wait : false) == UiAccessibilityResult.Success,
            "actual tab exposes a queued selection action");
        Frame();
        check(value.Value == "one", "queued tab selection updates its actual selection binding");
        check(tree.RequestAction(three.Node.Id, three.RuntimeId, UiAccessibilityAction.Focus, wait : false) == UiAccessibilityResult.Success,
            "actual tab exposes its independent focus action");
        Frame();
        check(value.Value == "one" && Find("Three").Node.Focused, "accessibility focus preserves selection in manual mode");
        check(tree.RequestAction(one.Node.Id, one.RuntimeId, UiAccessibilityAction.Deselect, wait : false) == UiAccessibilityResult.Success,
            "nullable TabList accepts a queued deselection action");
        Frame();
        check(value.Value is null && Find("One").Node.Selected == false, "deselection is explicit and does not invent a replacement selected value");
        var invalid = false;
        try { tabs.SetItems(new UiTabOption[] { new("same", "A"), new("same", "B") }); }
        catch (ArgumentException) { invalid = true; }
        check(invalid && tabs.Items.Length == 3, "duplicate tab keys reject before changing valid items");
        tabs.SetItems(new UiTabOption[] { new("three", "Three"), new("one", "One") });
        Frame();
        check(Find("Three").Node.Focused, "tab focus follows its stable value through item reordering");
        tabs.Options = tabs.Options with { Vertical = false, RightToLeft = true };
        Key(UiKey.LeftArrow);
        check(Find("One").Node.Focused, "RTL horizontal navigation reverses its physical arrow direction");
        tabs.SetItems(items);
        tabs.Options = new();
        value.Value = "one";
        Frame();
        Key(UiKey.RightArrow);
        Key(UiKey.Tab);
        Key(UiKey.Tab);
        check(Find("One").Node.Focused, "Tab re-entry returns to the selected item rather than a stale unselected focus");
        var width = Find("One").Node.Bounds.Width;
        value.Value = "three";
        Frame();
        check(Find("One").Node.Bounds.Width == width, "reserved selected text space keeps header geometry stable across selection changes");
        value.Value = "one";
        Frame();
        var rect = Find("Three").Node.Bounds;
        var pointer = new UiVector2(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
        Frame(Input(pointer, down : true, pressed : true));
        Frame(Input(pointer, released : true));
        check(value.Value == "three", "a real pointer press and release selects the tab under that pointer");
        value.Value = "one";
        Frame();
        Frame(Input(pointer, down : true, pressed : true));
        Frame(Input(pointer, down : true, keys : [new(UiKey.Escape, true, KeyModifiers.None)]));
        Frame(Input(pointer, released : true));
        check(value.Value == "one", "Escape cancels a pending tab pointer activation");
        rect = Find("Disabled").Node.Bounds;
        pointer = new(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
        Frame(Input(pointer, down : true, pressed : true));
        Frame(Input(pointer, released : true));
        check(value.Value == "one", "disabled tab pointer input cannot select or replace an existing selection");
        context.State.AdvanceTime(.2f);
        Frame();
        var start = selectionInk;
        value.Value = "three";
        Frame();
        check(selectionInk == start && context.State.NextScheduledFrameTimeSeconds is not null,
            "tab selection changes immediately while the indicator starts from its previous geometry");
        Frame();
        var mid = selectionInk;
        context.State.AdvanceTime(.2f);
        Frame();
        check(start.Width > 0 && start.X < mid.X && mid.X < selectionInk.X
            && context.State.NextScheduledFrameTimeSeconds is null, "the selected underline passes through intermediate pixels then stops scheduling");
        tabs.Options = tabs.Options with { ReducedMotion = true };
        value.Value = "one";
        Frame();
        check(selectionInk == start && context.State.NextScheduledFrameTimeSeconds is null,
            "reduced motion applies the selected underline geometry immediately");
    }
    private sealed class Screen(Action<UiImmediateContext> draw) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => draw(ui);
    }
}
