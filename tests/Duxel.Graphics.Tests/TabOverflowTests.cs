using Duxel.Core;

internal static class TabOverflowTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        using var tree = new UiAccessibilityTree();
        ControlFontFixture.Configure(context);
        context.SetAccessibilityTree(tree);
        var value = new UiState<string?>("one");
        UiTabOption[] items = [new("one", "One"), new("two", "Two", false), new("three", "Three"), new("four", "Four")];
        using var tabs = Fluent.TabList("Overflow tabs", value.Binding, items, new() { Overflow = new() });
        using var box = Dux.SizedBox(tabs, width : 400, height : 40);
        using var one = Fluent.Button("One action");
        using var two = Fluent.Button("Two action");
        using var three = Fluent.Button("Three action");
        using var four = Fluent.Button("Four action");
        UiInputState Input(UiVector2? pointer = null, bool down = false, bool pressed = false, bool released = false, UiKeyEvent[]? keys = null) =>
            new(pointer ?? new(-100, -100), down, false, pressed, released, false, false, 0, 0, keys ?? [], [], default, default);
        void Frame(UiInputState? input = null)
        {
            context.NewFrame(new(1f / 60, new(640, 400), new(1, 1)), input ?? Input(), new(0, 0, 640, 400), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding : 0);
                box.Render(ui);
                tabs.RenderPanel(ui, "one", one);
                tabs.RenderPanel(ui, "two", two);
                tabs.RenderPanel(ui, "three", three);
                tabs.RenderPanel(ui, "four", four);
            }));
            using var draw = context.GetDrawData();
        }
        UiAccessibilityElement? Find(string name, UiAccessibilityRole role)
        {
            UiAccessibilityElement? result = null;
            void Visit(string? id)
            {
                while (id is not null && tree.TryGet(id, out var entry))
                {
                    if (entry.Node.Name == name && entry.Node.Role == role) result = entry;
                    Visit(entry.FirstChild);
                    id = entry.Next;
                }
            }
            Visit(tree.Root.First);
            return result;
        }
        UiAccessibilityElement Trigger()
        {
            var group = Find("Overflow tabs", UiAccessibilityRole.TabList)!.Value;
            var id = group.FirstChild;
            while (id is not null && tree.TryGet(id, out var child))
            {
                if (child.Node.SupportsInvoke) return child;
                id = child.Next;
            }
            throw new InvalidOperationException("Overflow trigger missing");
        }
        void Click(UiAccessibilityElement element)
        {
            var bounds = element.Node.Bounds;
            var point = new UiVector2(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
            Frame(Input(point, down : true, pressed : true));
            Frame(Input(point, released : true));
        }
        Frame();
        var oldThree = Find("Three", UiAccessibilityRole.Tab)!.Value;
        check(tabs.OverflowResult is { HiddenCount : 0 } && !oldThree.Node.SupportsInvoke,
            "enabled overflow leaves a fitting real tab list intact without adding Invoke to ordinary tabs");
        box.Options = box.Options with { Width = 140 };
        Frame();
        check(tabs.OverflowResult is { HiddenCount : 2, Fits : true } && Find("One", UiAccessibilityRole.Tab)!.Value.Node.Selected == true
            && Find("Three", UiAccessibilityRole.Tab) is null && Find("Four", UiAccessibilityRole.Tab) is null,
            "actual priority overflow retains the selected header and removes hidden header semantics");
        check(tree.RequestAction(oldThree.Node.Id, oldThree.RuntimeId, UiAccessibilityAction.Select, wait : false) == UiAccessibilityResult.Unavailable,
            "a hidden tab's old runtime identity cannot change selection");
        var trigger = Trigger();
        check(trigger.Node.Expansion == UiAccessibilityExpansionState.Collapsed,
            "actual overflow action publishes its collapsed menu capability");
        check(trigger.Node.Role == UiAccessibilityRole.Tab && trigger.Node.Selected == false && trigger.Node.SupportsInvoke,
            "overflow trigger is an unselected actionable tab within the real tab-list parent");
        tree.RequestAction(trigger.Node.Id, trigger.RuntimeId, UiAccessibilityAction.Expand, wait : false);
        Frame();
        check(Trigger().Node.Expansion == UiAccessibilityExpansionState.Expanded && Find("Four", UiAccessibilityRole.MenuItem) is not null,
            "queued overflow Expand opens the same real hidden-tab menu");
        tree.RequestAction(trigger.Node.Id, trigger.RuntimeId, UiAccessibilityAction.Collapse, wait : false);
        Frame();
        check(Trigger().Node.Expansion == UiAccessibilityExpansionState.Collapsed && Find("Four", UiAccessibilityRole.MenuItem) is null,
            "queued overflow Collapse removes its menu without changing the selected body");
        Click(trigger);
        check(Trigger().Node.Expansion == UiAccessibilityExpansionState.Expanded,
            "actual overflow action publishes expanded state after its shared popup opens");
        check(Find("Three", UiAccessibilityRole.MenuItem) is not null && Find("Four", UiAccessibilityRole.MenuItem) is not null
            && Find("One", UiAccessibilityRole.MenuItem) is null,
            "actual shared popup contains only the hidden tab entries");
        Click(Find("Four", UiAccessibilityRole.MenuItem)!.Value);
        check(value.Value == "four" && Find("Four", UiAccessibilityRole.Tab)!.Value.Node.Selected == true
            && Find("Four action", UiAccessibilityRole.Button) is not null && Find("One action", UiAccessibilityRole.Button) is null,
            "menu pointer selection promotes the actual header and renders its actual panel in the same frame");
        Frame();
        var selectedTab = Find("Four", UiAccessibilityRole.Tab)!.Value;
        var panel = Find("Four", UiAccessibilityRole.TabPanel)!.Value;
        check(panel.Node.LabeledBy == selectedTab.Node.Id && selectedTab.Node.ControlledElementId == panel.Node.Id,
            "promoted overflow selection preserves real reciprocal tab-panel relationships");
        check(Trigger().Node.Focused, "closing the overflow menu restores focus to the actual overflow trigger");
        Frame(Input(keys : [new(UiKey.LeftArrow, true, KeyModifiers.None)]));
        check(Find("Four", UiAccessibilityRole.Tab)!.Value.Node.Focused && value.Value == "four",
            "a physical arrow from the overflow action returns to the visible enabled header without selecting another body");
        Frame(Input(keys : [new(UiKey.RightArrow, true, KeyModifiers.None)]));
        check(Trigger().Node.Focused && value.Value == "four", "roving focus includes the overflow action at the logical end");
        Frame(Input(keys : [new(UiKey.Tab, true, KeyModifiers.None)]));
        check(Find("Four", UiAccessibilityRole.TabPanel)!.Value.Node.Focused,
            "the entire overflowing tab list remains one Tab stop before its selected panel");
        Frame(Input(keys : [new(UiKey.Tab, true, KeyModifiers.Shift)]));
        check(Find("Four", UiAccessibilityRole.Tab)!.Value.Node.Focused, "reverse Tab reenters the tab group on its selected header");
        Frame(Input(keys : [new(UiKey.RightArrow, true, KeyModifiers.None)]));
        Frame(Input(keys : [new(UiKey.Space, true, KeyModifiers.None)]));
        Frame(Input(keys : [new(UiKey.Space, false, KeyModifiers.None)]));
        check(Find("Three", UiAccessibilityRole.MenuItem) is not null,
            "Space release on the roving overflow action opens its shared popup through the group owner");
        Frame(Input(keys : [new(UiKey.Escape, true, KeyModifiers.None)]));
        Frame();
        trigger = Trigger();
        tree.RequestAction(trigger.Node.Id, trigger.RuntimeId, UiAccessibilityAction.Select, wait : false);
        Frame();
        var disabled = Find("Two", UiAccessibilityRole.MenuItem)!.Value;
        check(!disabled.Node.Enabled && value.Value == "four", "UIA selection of the action tab opens the menu without selecting it as a body");
        var target = Find("Three", UiAccessibilityRole.MenuItem)!.Value;
        tree.RequestAction(target.Node.Id, target.RuntimeId, UiAccessibilityAction.Invoke, wait : false);
        Frame();
        check(value.Value == "three" && Find("Three action", UiAccessibilityRole.Button) is not null,
            "actual overflow MenuItem Invoke reaches the original tab binding and selected panel");
        Frame();
        box.Options = box.Options with { Width = 400 };
        Frame();
        var restored = Find("Three", UiAccessibilityRole.Tab)!.Value;
        check(tabs.OverflowResult is { HiddenCount : 0 } && restored.Node.Selected == true && restored.Node.Focused && restored.RuntimeId != oldThree.RuntimeId,
            "expanding the actual parent restores headers with fresh identities while retaining selection");
        box.Options = box.Options with { Width = 140 };
        tabs.Options = tabs.Options with { RightToLeft = true };
        Frame();
        check(Trigger().Node.Bounds.X < Find("Three", UiAccessibilityRole.Tab)!.Value.Node.Bounds.X,
            "actual RTL overflow puts the trigger at the logical end of the visible tabs");
        Click(Trigger());
        box.Options = box.Options with { Width = 400 };
        Frame();
        check(Find("Three", UiAccessibilityRole.Tab)!.Value.Node.Focused && Find("Four", UiAccessibilityRole.MenuItem) is null,
            "removing overflow while its popup is open closes it and transfers logical focus to the selected header");
        tabs.Options = tabs.Options with { RightToLeft = false };
        var clippedFocusTarget = Find("One", UiAccessibilityRole.Tab)!.Value;
        tree.RequestAction(clippedFocusTarget.Node.Id, clippedFocusTarget.RuntimeId, UiAccessibilityAction.Focus, wait : false);
        box.Options = box.Options with { Width = 30 };
        Frame();
        check(tabs.OverflowResult is { Fits : false, IndicatorClipped : true } && Trigger().Node.Bounds.Width == 30 && Trigger().Node.Focused,
            "an infeasible minimum-visible allocation reports clipping while preserving the actual overflow action");
        tabs.Options = tabs.Options with { Overflow = null };
        Frame();
        check(tabs.OverflowResult is null && Find("Four", UiAccessibilityRole.Tab) is not null,
            "turning overflow off restores the original tab-list publication contract");
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
