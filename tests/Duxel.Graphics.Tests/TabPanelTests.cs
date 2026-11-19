using Duxel.Core;

internal static class TabPanelTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        ControlFontFixture.Configure(context);
        var value = new UiState<string?>("one");
        using var tabs = Fluent.TabList("Panel tabs", value.Binding, new UiTabOption[] { new("one", "One"), new("two", "Two") });
        using var one = Fluent.Button("One action");
        using var two = Fluent.Button("Two action");
        using var after = Fluent.Button("After panels");
        var oneCalls = 0;
        Action<UiImmediateContext> renderOne = ui => { oneCalls++; one.Render(ui); };
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(UiInputState? input = null, Action<UiImmediateContext>? body = null, bool rootLayout = true)
        {
            context.NewFrame(new(1f / 60, new(800, 600), new(1, 1)), input ?? idle, new(0, 0, 800, 600), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(rootLayout, contentPadding : 0);
                tabs.Render(ui);
                if (body is not null) body(ui);
                else
                {
                    tabs.RenderPanel(ui, "one", renderOne);
                    tabs.RenderPanel(ui, "two", two);
                    after.Render(ui);
                }
            }));
            using var draw = context.GetDrawData();
        }
        UiAccessibilityElement? Find(string name, UiAccessibilityRole role)
        {
            UiAccessibilityElement? result = null;
            void Visit(string? id)
            {
                while (id is not null && tree.TryGet(id, out var element))
                {
                    if (element.Node.Name == name && element.Node.Role == role) result = element;
                    Visit(element.FirstChild);
                    id = element.Next;
                }
            }
            Visit(tree.Root.First);
            return result;
        }
        void Key(UiKey key, KeyModifiers modifiers = KeyModifiers.None) =>
            Frame(idle with { KeyEvents = new[] { new UiKeyEvent(key, true, modifiers) } });
        Frame();
        var firstTab = Find("One", UiAccessibilityRole.Tab)!.Value;
        var firstPanel = Find("One", UiAccessibilityRole.TabPanel)!.Value;
        check(oneCalls == 1 && Find("Two action", UiAccessibilityRole.Button) is null,
            "only the selected panel body executes and publishes children");
        check(firstPanel.Node.LabeledBy == firstTab.Node.Id && firstTab.Node.ControlledElementId == firstPanel.Node.Id,
            "tab and panel publish reciprocal labeled-by and controller relationships");
        check(Find("One action", UiAccessibilityRole.Button)!.Value.Node.ParentId == firstPanel.Node.Id
            && Find("After panels", UiAccessibilityRole.Button)!.Value.Node.ParentId == string.Empty,
            "panel owns only its body accessibility children and restores the caller's parent");
        check(firstPanel.Node.Bounds.Height > 0 && !firstPanel.Node.Offscreen && firstPanel.Node.Focusable,
            "selected panel exposes actual consumed body bounds and an optional focus stop");
        Key(UiKey.Tab);
        check(Find("One", UiAccessibilityRole.Tab)!.Value.Node.Focused, "Tab enters the selected header before its panel");
        Key(UiKey.Tab);
        check(Find("One", UiAccessibilityRole.TabPanel)!.Value.Node.Focused, "Tab reaches the named panel before its first child");
        Key(UiKey.Tab);
        check(Find("One action", UiAccessibilityRole.Button)!.Value.Node.Focused, "Tab moves from the panel to its actual first child");
        Key(UiKey.Tab, KeyModifiers.Shift);
        check(Find("One", UiAccessibilityRole.TabPanel)!.Value.Node.Focused, "Shift Tab returns from the first child to the panel");
        value.Value = "two";
        Frame();
        check(Find("One", UiAccessibilityRole.TabPanel) is null && Find("Two", UiAccessibilityRole.TabPanel) is not null
            && Find("One", UiAccessibilityRole.Tab)!.Value.Node.ControlledElementId is null,
            "switching panels removes stale bodies and inactive header relationships");
        check(!tree.TryGet(firstPanel.Node.Id, out _), "the previous selected panel is unavailable after switching");
        var secondPanel = Find("Two", UiAccessibilityRole.TabPanel)!.Value;
        check(tree.RequestAction(secondPanel.Node.Id, secondPanel.RuntimeId, UiAccessibilityAction.Focus, wait : false) == UiAccessibilityResult.Success,
            "the selected panel accepts a real queued accessibility focus action");
        Frame();
        check(Find("Two", UiAccessibilityRole.TabPanel)!.Value.Node.Focused,
            "queued accessibility focus reaches the common keyboard navigation owner");
        value.Value = null;
        Frame();
        check(Find("Two", UiAccessibilityRole.TabPanel) is null && Find("One", UiAccessibilityRole.TabPanel) is null,
            "explicit empty selection renders no invented panel");
        value.Value = "one";
        Frame(body : ui => tabs.RenderPanel(ui, "one", one, focusable : false));
        check(!Find("One", UiAccessibilityRole.TabPanel)!.Value.Node.Focusable,
            "callers can omit the panel's own focus stop while retaining body relationships");
        var duplicateRejected = false;
        Frame(body : ui =>
        {
            tabs.RenderPanel(ui, "one", one);
            try { tabs.RenderPanel(ui, "one", one); }
            catch (InvalidOperationException) { duplicateRejected = true; }
        });
        check(duplicateRejected, "a selected panel cannot execute twice in the same header frame");
        var unknownRejected = false;
        Frame(body : ui =>
        {
            try { tabs.RenderPanel(ui, "missing", one); }
            catch (ArgumentException) { unknownRejected = true; }
        });
        check(unknownRejected, "an unknown panel key fails without invoking a body");
        Frame(rootLayout : false);
        check(Find("One", UiAccessibilityRole.TabPanel)!.Value.Node.Bounds.Width > 0,
            "flow-only screens expose a visible panel without enabling the viewport root layout");
        Frame(body : ui =>
        {
            ui.BeginRow();
            tabs.RenderPanel(ui, "one", one);
            ui.EndRow();
        });
        check(Find("One", UiAccessibilityRole.TabPanel)!.Value.Node.Bounds.Height > 0,
            "a body in a horizontal flow row includes its actual row height");
        value.Value = "one";
        Frame(body : ui =>
        {
            value.Value = "two";
            tabs.RenderPanel(ui, "one", one);
            tabs.RenderPanel(ui, "two", two);
        });
        check(Find("One", UiAccessibilityRole.TabPanel) is not null && Find("Two", UiAccessibilityRole.TabPanel) is null,
            "a binding changed after the header does not mismatch its committed panel in the same frame");
        Frame();
        check(Find("Two", UiAccessibilityRole.TabPanel) is not null,
            "the following header frame applies the new binding to its corresponding panel");
        Frame(body : ui =>
        {
            tabs.SetItems(new UiTabOption[] { new("one", "One") });
            tabs.RenderPanel(ui, "two", two);
        });
        check(Find("Two", UiAccessibilityRole.TabPanel) is not null,
            "items replaced after the header preserve its already rendered key and relationship for this frame");
        Frame(body : ui => tabs.RenderPanel(ui, "one", one));
        check(Find("Two", UiAccessibilityRole.Tab) is null && Find("Two", UiAccessibilityRole.TabPanel) is null,
            "the next frame retires a removed selected key without inventing a replacement panel");
        tabs.SetItems(new UiTabOption[] { new("one", "One"), new("two", "Two") });
        value.Value = "one";
        Frame();
        var restored = Find("One", UiAccessibilityRole.TabPanel)!.Value;
        check(restored.Node.Id == firstPanel.Node.Id && restored.RuntimeId != firstPanel.RuntimeId,
            "returning to a key preserves its semantic id but does not revive a removed provider identity");
        var staleRejected = false;
        context.NewFrame(new(1f / 60, new(800, 600), new(1, 1)), idle, new(0, 0, 800, 600), UiTextSettings.Default);
        context.Render(new Screen(ui =>
        {
            ui.EnableRootViewportContentLayout(contentPadding : 0);
            try { tabs.RenderPanel(ui, "one", one); }
            catch (InvalidOperationException) { staleRejected = true; }
        }));
        using (context.GetDrawData()) { }
        check(staleRejected, "a panel rejects a header captured in an earlier frame");
        tabs.Dispose();
        one.Label = "Caller still owns this body";
        check(one.Label == "Caller still owns this body", "disposing the header does not dispose borrowed panel widgets");
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
