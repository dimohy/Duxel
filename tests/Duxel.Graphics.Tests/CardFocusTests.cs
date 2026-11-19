using Duxel.Core;

internal static class CardFocusTests
{
    internal static void Run(Action<bool, string> check)
    {
        foreach (var mode in Enum.GetValues<UiFocusGroupMode>())
        {
            using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
            ControlFontFixture.Configure(context);
            using var tree = new UiAccessibilityTree();
            context.SetAccessibilityTree(tree);
            var activations = 0;
            var memo = "original";
            using var before = Fluent.Button("Before");
            using var after = Fluent.Button("After");
            using var card = Fluent.Card("Focus card", Dux.Column(new(Spacing: 8, MainAxisSize: UiMainAxisSize.Min),
                Fluent.Button("First action", () => activations++),
                Fluent.Input("Inner input", new UiBinding<string>(() => memo, value => memo = value), 100, 160),
                Fluent.Button("Last action", () => activations++)),
                new(Width: 240, Appearance: UiCardAppearance.Outline, FocusMode: mode));
            var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
            void Frame(UiInputState? input = null, bool show = true)
            {
                context.NewFrame(new(1f / 60, new(400, 360), new(1, 1)), input ?? idle, new(0, 0, 400, 360), UiTextSettings.Default);
                context.Render(new Screen(ui =>
                {
                    ui.EnableRootViewportContentLayout(contentPadding: 0);
                    before.Render(ui); if (show) card.Render(ui); after.Render(ui);
                }));
                using var data = context.GetDrawData();
            }
            UiAccessibilityElement Find(string label)
            {
                UiAccessibilityElement? found = null;
                void Visit(string? id)
                {
                    while (id is not null && tree.TryGet(id, out var item))
                    { if (item.Node.Name == label) found = item; Visit(item.FirstChild); id = item.Next; }
                }
                Visit(tree.Root.First);
                return found ?? throw new InvalidOperationException("Focus fixture missing : " + label);
            }
            void Key(UiKey key, KeyModifiers modifiers = default)
            { Frame(idle with { KeyEvents = [new(key, true, modifiers)], Modifiers = modifiers }); Frame(idle with { KeyEvents = [new(key, false, modifiers)] }); }
            void Focus(string name)
            {
                var node = Find(name);
                check(tree.RequestAction(node.Node.Id, node.RuntimeId, UiAccessibilityAction.Focus, wait: false) == UiAccessibilityResult.Success,
                    "Card fixture accepts real accessibility focus : " + mode + " " + name);
                Frame();
            }
            void Click(string name)
            {
                var bounds = Find(name).Node.Bounds;
                var point = new UiVector2(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
                Frame(idle with { MousePosition = point, LeftMouseDown = true, LeftMousePressed = true });
                Frame(idle with { MousePosition = point, LeftMouseReleased = true });
            }
            Frame();
            Focus("Before"); Key(UiKey.Tab);
            check(context.State.FocusedId == Find(mode == UiFocusGroupMode.Off ? "First action" : "Focus card").Node.Id,
                "Card outer Tab entry matches official focus mode : " + mode);
            if (mode != UiFocusGroupMode.Off)
            {
                Key(UiKey.Enter);
                check(context.State.FocusedId == Find("First action").Node.Id && activations == 0,
                    "entering Card focuses its first child without also activating it : " + mode);
                Key(UiKey.Escape);
                check(context.State.FocusedId == Find("Focus card").Node.Id,
                    "Card Escape returns from a child to its focusable root : " + mode);
                Key(UiKey.Tab);
                check(context.State.FocusedId == Find(mode == UiFocusGroupMode.TabOnly ? "First action" : "After").Node.Id,
                    "Card collapsed Tab behavior matches its official mode : " + mode);
            }
            Click("Last action");
            check(activations == 1 && context.State.FocusedId == Find("Last action").Node.Id,
                "Card does not steal pointer focus or activation from its real child : " + mode);
            Key(UiKey.Tab);
            check(context.State.FocusedId == Find(mode == UiFocusGroupMode.NoTab ? "First action" : "After").Node.Id,
                "Card Tab traps only in no-tab mode and exits the others : " + mode);
            Focus("First action"); Key(UiKey.Tab, KeyModifiers.Shift);
            check(context.State.FocusedId == Find(mode == UiFocusGroupMode.NoTab ? "Last action"
                : mode == UiFocusGroupMode.Off ? "Before" : "Focus card").Node.Id,
                "Card reverse Tab cycles the trap or reaches its real outer boundary : " + mode);
            Click("Inner input"); Key(UiKey.End, KeyModifiers.Ctrl);
            Frame(idle with { CharEvents = [new('x')] });
            check(memo == "originalx" && context.State.FocusedId == Find("Inner input").Node.Id,
                "Card group preserves actual child typing and focus : " + mode);
            card.Options = card.Options with { Disabled = true };
            Frame();
            check(!Find("Focus card").Node.Enabled && !Find("Focus card").Node.Focusable
                && Find("First action").Node.Enabled && Find("Inner input").Node.Enabled,
                "disabling Card root leaves its independent children enabled : " + mode);
            Click("First action");
            check(activations == 2, "disabled Card root does not disable child activation : " + mode);
            Frame(show: false);
            check(context.State.FocusedId is null && context.State.NextScheduledFrameTimeSeconds is null,
                "omitting Card removes child focus without scheduling periodic frames : " + mode);
        }
        RunInTabPanel(check);
        RunStorageLifetime(check);
        CardNestedFocusTests.Run(check);
        CardActionTests.Run(check);
        CardSelectionTests.Run(check);
    }
    private static void RunStorageLifetime(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var card = Fluent.Card("Large group", Dux.Column(new(MainAxisSize: UiMainAxisSize.Min),
            Enumerable.Range(0, 100).Select(index => (UiWidget)Fluent.Button("Member " + index)).ToArray()),
            new(FocusMode: UiFocusGroupMode.NoTab, Appearance: UiCardAppearance.Outline));
        UiImmediateContext? observed = null;
        void Frame(bool show)
        {
            context.NewFrame(new(1f / 60, new(400, 10000), new(1, 1)),
                new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default),
                new(0, 0, 400, 10000), UiTextSettings.Default);
            context.Render(new Screen(ui => { observed = ui; ui.EnableRootViewportContentLayout(contentPadding: 0); if (show) card.Render(ui); }));
            using var draw = context.GetDrawData();
        }
        Frame(true);
        check(observed!.ControlFocusGroupStorage.References == 2 && observed.ControlFocusGroupStorage.Capacity >= 100,
            "focus group stores only current scope anchors after releasing the per-frame stop map references");
        Frame(false);
        check(observed.ControlFocusGroupStorage.References == 0, "omitted focus groups release all scope anchor references");
        for (var frame = 0; frame < 128; frame++) Frame(false);
        check(observed.ControlFocusGroupStorage.Capacity <= 32, "sparse focus navigation trims its former large metadata capacity");
    }
    private static void RunInTabPanel(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        var selected = new UiState<string?>("body");
        using var tabs = Fluent.TabList("Outer tabs", selected.Binding, new UiTabOption[] { new("body", "Body") });
        var activations = 0;
        using var card = Fluent.Card("Nested card", Fluent.Button("Nested action", () => activations++),
            new(FocusMode: UiFocusGroupMode.NoTab, Appearance: UiCardAppearance.Outline));
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(UiInputState? input = null)
        {
            context.NewFrame(new(1f / 60, new(400, 300), new(1, 1)), input ?? idle, new(0, 0, 400, 300), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding: 0);
                tabs.Render(ui); tabs.RenderPanel(ui, "body", card);
            }));
            using var draw = context.GetDrawData();
        }
        UiAccessibilityElement Find(string label)
        {
            UiAccessibilityElement? found = null;
            void Visit(string? id)
            {
                while (id is not null && tree.TryGet(id, out var item))
                { if (item.Node.Name == label) found = item; Visit(item.FirstChild); id = item.Next; }
            }
            Visit(tree.Root.First); return found ?? throw new InvalidOperationException("Nested focus node is missing : " + label);
        }
        Frame();
        var root = Find("Nested card");
        tree.RequestAction(root.Node.Id, root.RuntimeId, UiAccessibilityAction.Focus, wait: false);
        Frame();
        Frame(idle with { KeyEvents = [new(UiKey.Enter, true, default)] });
        check(context.State.FocusedId == Find("Nested action").Node.Id && activations == 0,
            "Card group anchors survive a TabPanel inserting its own focus stop before the rendered children");
        Frame(idle with { KeyEvents = [new(UiKey.Tab, true, default)] });
        check(context.State.FocusedId == Find("Nested action").Node.Id,
            "one-child Card trap stays within its own member after outer tab-stop reordering");
        Frame(idle with { KeyEvents = [new(UiKey.Escape, true, default)] });
        check(context.State.FocusedId == root.Node.Id, "nested Card Escape returns to the actual Card rather than the outer panel");
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen { public override void Render(UiImmediateContext ui) => render(ui); }
}
