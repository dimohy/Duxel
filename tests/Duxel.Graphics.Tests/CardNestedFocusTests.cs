using Duxel.Core;

internal static class CardNestedFocusTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        var ime = new Composition();
        context.SetImeHandler(ime);
        var text = "original";
        var calls = 0;
        var menu = Fluent.Menu("Inner menu", [new("execute", "Execute item")], _ => calls++);
        var inner = Fluent.Card("Inner group", Dux.Column(new(Spacing: 4, MainAxisSize: UiMainAxisSize.Min),
            Fluent.Input("Nested input", new UiBinding<string>(() => text, value => text = value), 100, 160),
            menu, Fluent.Button("Inner last", () => calls++)),
            new(Width: 220, FocusMode: UiFocusGroupMode.NoTab, Appearance: UiCardAppearance.Outline));
        using var outer = Fluent.Card("Outer group", Dux.Column(new(Spacing: 4, MainAxisSize: UiMainAxisSize.Min),
            inner, Fluent.Button("Outer last", () => calls++)),
            new(Width: 260, FocusMode: UiFocusGroupMode.NoTab, Appearance: UiCardAppearance.Outline));
        using var after = Fluent.Button("Outside action", () => calls++);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(UiInputState? input = null)
        {
            context.NewFrame(new(1f / 60, new(440, 520), new(1, 1)), input ?? idle, new(0, 0, 440, 520), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding: 0); outer.Render(ui); after.Render(ui); }));
            using var draw = context.GetDrawData();
        }
        UiAccessibilityElement Find(string name)
        {
            UiAccessibilityElement? found = null;
            void Visit(string? id)
            {
                while (id is not null && tree.TryGet(id, out var item))
                {
                    if (item.Node.Name == name && item.Node.Role != UiAccessibilityRole.Menu)
                        found = item;
                    Visit(item.FirstChild);
                    id = item.Next;
                }
            }
            Visit(tree.Root.First);
            return found ?? throw new InvalidOperationException("Nested Card fixture missing : " + name);
        }
        void Focus(string name)
        {
            var node = Find(name);
            if (tree.RequestAction(node.Node.Id, node.RuntimeId, UiAccessibilityAction.Focus, wait: false) != UiAccessibilityResult.Success)
                throw new InvalidOperationException("Nested Card focus request failed : " + name);
            Frame();
        }
        void Key(UiKey key, KeyModifiers modifiers = default)
        {
            Frame(idle with
            {
                KeyEvents = [new(key, true, modifiers)],
                Modifiers = modifiers
            });
            Frame(idle with
            {
                KeyEvents = [new(key, false, modifiers)]
            });
        }
        bool Focused(string name) => context.State.FocusedId == Find(name).Node.Id;
        Frame();
        Focus("Outer group");
        Key(UiKey.Enter);
        check(Focused("Inner group") && calls == 0, "nested Card Enter first visits the inner root without entering both scopes");
        Key(UiKey.Tab);
        check(Focused("Outer last"), "collapsed nested Card hides only its own children within the entered outer scope");
        Key(UiKey.Tab);
        check(Focused("Inner group"), "outer no-tab group wraps through the collapsed inner root");
        Key(UiKey.Enter);
        check(Focused("Nested input"), "nested Card Enter crosses only its selected inner scope boundary");
        Key(UiKey.Tab, KeyModifiers.Shift);
        check(Focused("Inner last"), "nested reverse Tab wraps in the innermost entered no-tab group");
        Key(UiKey.Tab);
        check(Focused("Nested input"), "nested forward Tab wraps in the innermost entered no-tab group");
        ime.Preview = "한";
        check(ime.Owner == Find("Nested input").Node.Id, "nested Card keyboard entry assigns composition to its real input owner");
        Frame();
        Key(UiKey.Escape);
        check(Focused("Nested input") && text == "original", "Card Escape leaves active IME composition with its actual input");
        ime.Preview = null;
        ime.Pending = "한";
        Frame();
        check(text == "original한", "nested Card keeps actual IME commit delivery on its owned input");
        Key(UiKey.Escape);
        check(Focused("Inner group"), "after IME composition ends Escape returns only to the nearest Card root");
        Key(UiKey.Escape);
        check(Focused("Outer group"), "second Escape returns the inner root to its enclosing Card root");
        Focus("Inner menu");
        Key(UiKey.DownArrow);
        check(menu.IsOpen && calls == 0, "a real menu inside nested Card opens without ancestor activation");
        Key(UiKey.Escape);
        check(!menu.IsOpen && Focused("Inner menu"), "menu Escape closes only the popup and restores its trigger inside Card");
        Key(UiKey.Escape);
        check(Focused("Inner group"), "Card Escape resumes after popup dismissal on a subsequent key");
        Focus("Inner menu");
        Key(UiKey.DownArrow);
        Key(UiKey.Enter);
        check(!menu.IsOpen && calls == 1, "nested Card does not consume a menu item Enter or duplicate its real action");
        Focus("Outer group");
        Key(UiKey.Tab);
        check(Focused("Outside action"), "Tab from a collapsed outer root skips every nested member");
    }

    private sealed class Composition : IUiImeHandler
    {
        internal string? Preview, Pending, Owner;
        public string? ConsumeCommittedText(string inputId)
        {
            if (inputId != Owner)
                return null;
            var result = Pending;
            Pending = null;
            return result;
        }
        public void ReleaseInput(string inputId) => _ = ConsumeCommittedText(inputId);
        public string? ConsumeRecentCommittedText() => null;
        public string? GetCompositionText() => Owner is null ? null : Preview;
        public void SetCaretRect(UiRect caretRect, UiRect inputRect, float fontPixelHeight, float fontPixelWidth)
        {
        }
        public void SetCompositionOwner(string? inputId) => Owner = inputId;
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
