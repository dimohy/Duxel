using Duxel.Core;

internal static class ComboboxTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        string text = "";
        string? selected = "a";
        string multiText = "";
        string[] selectedMany = ["a"];
        UiSelectOption[] items = [new("a", "Alpha"), new("b", "Beta"), new("n", "New York"), new("d", "Disabled", false), new("k", "부산")];
        using var single = Fluent.Combobox("Editable", new UiBinding<string>(() => text, value => text = value),
            new UiBinding<string?>(() => selected, value => selected = value), items);
        using var multiple = Fluent.Combobox("Multiple", new UiBinding<string>(() => multiText, value => multiText = value),
            new UiBinding<string[]>(() => selectedMany, value => selectedMany = value), items);
        UiWidget current = single;
        using var following = Fluent.Button("Following", () => { });
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(UiInputState? input = null)
        {
            context.NewFrame(new(1f / 60, new(640, 480), new(1, 1)), input ?? idle, new(0, 0, 640, 480), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding: 0); current.Render(ui); following.Render(ui); }));
            using var data = context.GetDrawData();
        }
        List<UiAccessibilityElement> Elements()
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
        UiAccessibilityElement Find(string name, UiAccessibilityRole role) => Elements().Single(e => e.Node.Name == name && e.Node.Role == role);
        void Send(string name, UiAccessibilityRole role, UiAccessibilityAction action, string? value = null)
        {
            var item = Find(name, role);
            check(tree.RequestAction(item.Node.Id, item.RuntimeId, action, value, wait: false) == UiAccessibilityResult.Success,
                "Combobox accepts typed native action : " + action + " / " + name);
            Frame();
        }
        void Key(UiKey key, KeyModifiers modifiers = default) => Frame(idle with
        {
            KeyEvents = [new(key, true, modifiers)],
            Modifiers = modifiers
        });
        void Type(string value) => Frame(idle with
        {
            CharEvents = value.EnumerateRunes().Select(r => new UiCharEvent((uint)r.Value)).ToArray()
        });
        Frame();
        var editor = Find("Editable", UiAccessibilityRole.Edit);
        void ArrowClick(bool expectedOpen)
        {
            var bounds = Find("Show options", UiAccessibilityRole.Button).Node.Bounds;
            var point = new UiVector2(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
            Frame(idle with
            {
                MousePosition = point,
                LeftMouseDown = true,
                LeftMousePressed = true
            });
            Frame(idle with
            {
                MousePosition = point,
                LeftMouseDown = true
            });
            Frame(idle with
            {
                MousePosition = point,
                LeftMouseDown = true
            });
            Frame(idle with
            {
                MousePosition = point,
                LeftMouseReleased = true
            });
            check(single.IsOpen == expectedOpen,
                "Combobox actual expander click retains pointer ownership through held frames: " + expectedOpen);
        }
        ArrowClick(true);
        check(!Find("Alpha", UiAccessibilityRole.Option).Node.Offscreen && !Find("Beta", UiAccessibilityRole.Option).Node.Offscreen,
            "Combobox arrow opens the complete option collection without requiring a query");
        ArrowClick(false);
        var arrowOpen = new UiState<bool>(false);
        var arrowTransitions = new List<bool>();
        single.Open = arrowOpen.Binding;
        single.OpenChanged = arrowTransitions.Add;
        ArrowClick(true);
        ArrowClick(false);
        check(!arrowOpen.Value && arrowTransitions.SequenceEqual(new[] { true, false }),
            "Combobox arrow updates the controlled Open binding with one callback per actual transition");
        single.Open = null;
        single.OpenChanged = null;
        text = string.Empty;
        Frame();
        check(!Find("Editable", UiAccessibilityRole.ComboBox).Node.ReadOnly && !editor.Node.ReadOnly
            && editor.Node.ParentId == Find("Editable", UiAccessibilityRole.ComboBox).Node.Id,
            "Combobox exposes one writable root and its actual native text editor");
        Send("Editable", UiAccessibilityRole.ComboBox, UiAccessibilityAction.Focus);
        Type("New ");
        check(text == "New " && single.IsOpen && selected == "a", "Combobox text entry preserves spaces and the previous selection while filtering");
        check(!Find("New York", UiAccessibilityRole.Option).Node.Offscreen
            && Find("Alpha", UiAccessibilityRole.Option).Node.Offscreen && Find("Alpha", UiAccessibilityRole.Option).Node.Selected == true,
            "Combobox filtered rows preserve hidden selected providers with their real selection flags");
        var hiddenAlpha = Find("Alpha", UiAccessibilityRole.Option);
        Type("York");
        Key(UiKey.Enter);
        check(text == "New York" && selected == "n" && !single.IsOpen
            && Find("New York", UiAccessibilityRole.Option).Node.Selected == true
            && Find("Editable", UiAccessibilityRole.ListBox).Node.Offscreen,
            "Combobox exact typed Enter commits the canonical value and closes the single list");
        Send("Editable", UiAccessibilityRole.ComboBox, UiAccessibilityAction.SetValue, "Be");
        Key(UiKey.DownArrow);
        Key(UiKey.Enter);
        check(selected == "b" && text == "Beta" && !single.IsOpen, "Combobox arrow navigation and Enter commit the filtered option");
        Send("Alpha", UiAccessibilityRole.Option, UiAccessibilityAction.Select);
        check(selected == "a" && text == "Alpha", "Combobox hidden logical option selection updates the authoritative input binding");
        check(Find("Alpha", UiAccessibilityRole.Option).RuntimeId == hiddenAlpha.RuntimeId,
            "Combobox filter transitions preserve canonical native option provider identity");
        Send("Editable", UiAccessibilityRole.Edit, UiAccessibilityAction.SetValue, "unknown");
        Key(UiKey.Enter);
        check(text == "Alpha" && selected == "a", "Combobox constrained Enter restores the selected label for an unmatched query");
        single.Options = single.Options with
        {
            Freeform = true
        };
        Send("Editable", UiAccessibilityRole.ComboBox, UiAccessibilityAction.SetValue, "Custom 한글");
        Key(UiKey.Enter);
        check(text == "Custom 한글" && selected is null, "Combobox freeform Enter preserves caller text and clears a stale single option selection");
        current = multiple;
        Frame();
        Send("Multiple", UiAccessibilityRole.ComboBox, UiAccessibilityAction.Focus);
        Type("Be");
        Key(UiKey.DownArrow);
        Key(UiKey.Enter);
        check(selectedMany.SequenceEqual(new[] { "a", "b" }) && multiple.IsOpen,
            "Combobox filtered multiple activation retains an existing hidden selection");
        Send("Alpha", UiAccessibilityRole.Option, UiAccessibilityAction.Deselect);
        check(selectedMany.SequenceEqual(new[] { "b" }), "Combobox hidden multiple options accept canonical Deselect");
        Send("Multiple", UiAccessibilityRole.Edit, UiAccessibilityAction.SetValue, "New York");
        Key(UiKey.Enter);
        check(selectedMany.SequenceEqual(new[] { "b", "n" }) && multiText == "" && multiple.IsOpen
            && Find("New York", UiAccessibilityRole.Option).Node.Selected == true && !Find("Alpha", UiAccessibilityRole.Option).Node.Offscreen,
            "Combobox exact multiple Enter publishes the committed selection and resets its query in the same open frame");
        Key(UiKey.Escape);
        Frame();
        check(!multiple.IsOpen && context.State.ActiveId is not null,
            "Combobox Escape dismisses the popup while retaining the actual editor owner");
        Send("Multiple", UiAccessibilityRole.Edit, UiAccessibilityAction.SetValue, "");
        Type("한글");
        Key(UiKey.Z, KeyModifiers.Ctrl);
        check(multiText == "", "Combobox reuses the actual input undo history after popup dismissal");
        current = single;
        Frame();
        Frame(); // Retire the omitted multiple popup before requesting focus from its replacement page.
        Send("Editable", UiAccessibilityRole.ComboBox, UiAccessibilityAction.Focus);
        Send("Editable", UiAccessibilityRole.Edit, UiAccessibilityAction.SetValue, "");
        var ime = new Ime();
        context.SetImeHandler(ime);
        ime.Composition = "부";
        Key(UiKey.Enter);
        check(text == "" && selected is null && ime.Owner is not null,
            "Combobox IME composition Enter cannot prematurely select a popup option");
        ime.Composition = null;
        ime.Commit = "부산";
        Frame();
        check(text == "부산" && selected is null && single.IsOpen,
            "Combobox consumes a committed Korean composition once and filters the real popup");
        Frame();
        check(text == "부산" && ime.CommitReads == 1, "Combobox retained IME ownership does not duplicate committed text");
        Key(UiKey.Enter);
        check(selected == "k" && text == "부산" && !single.IsOpen,
            "Combobox committed Korean text selects its canonical option with a subsequent Enter");
        Send("Show options", UiAccessibilityRole.Button, UiAccessibilityAction.Invoke);
        check(single.IsOpen && !Find("Alpha", UiAccessibilityRole.Option).Node.Offscreen,
            "Combobox explicit expander restores the complete canonical list after a filtered selection");
        Send("Editable", UiAccessibilityRole.ComboBox, UiAccessibilityAction.Collapse);
        var inputBounds = Find("Editable", UiAccessibilityRole.Edit).Node.Bounds;
        Frame(idle with
        {
            MousePosition = new(inputBounds.X + 4, inputBounds.Y + inputBounds.Height / 2),
            LeftMouseDown = true,
            LeftMousePressed = true
        });
        check(single.IsOpen, "Combobox clicking its actual editor opens the option popup without requiring a text change");
        check(!Find("Following", UiAccessibilityRole.Button).Node.Enabled,
            "Combobox popup blocks subsequent underlying controls in the same frame");
        var betaBounds = Find("Beta", UiAccessibilityRole.Option).Node.Bounds;
        var betaPoint = new UiVector2(betaBounds.X + betaBounds.Width / 2, betaBounds.Y + betaBounds.Height / 2);
        Frame(idle with
        {
            MousePosition = betaPoint,
            LeftMouseDown = true,
            LeftMousePressed = true
        });
        Frame(idle with
        {
            MousePosition = betaPoint,
            LeftMouseReleased = true
        });
        check(selected == "b" && text == "Beta" && !single.IsOpen && Find("Following", UiAccessibilityRole.Button).Node.Enabled,
            "Combobox editor preserves popup pointer capture through release and immediately unblocks following controls");
        Frame();
        Send("Show options", UiAccessibilityRole.Button, UiAccessibilityAction.Invoke);
        Key(UiKey.Tab);
        check(!single.IsOpen && Find("Following", UiAccessibilityRole.Button).Node.Focused,
            "Combobox Tab dismisses the popup and transfers focus to the next real control in the same frame");
        single.Options = single.Options with
        {
            Freeform = false
        };
        Send("Editable", UiAccessibilityRole.ComboBox, UiAccessibilityAction.Focus);
        Send("Editable", UiAccessibilityRole.Edit, UiAccessibilityAction.SetValue, "Al");
        Key(UiKey.Escape);
        check(text == "Beta" && selected == "b" && !single.IsOpen && Find("Editable", UiAccessibilityRole.ComboBox).Node.Value == "Beta",
            "Combobox constrained Escape restores the canonical label in the same published frame");
        Send("Editable", UiAccessibilityRole.Edit, UiAccessibilityAction.SetValue, "temporary");
        check(text == "temporary", "Combobox an empty filtered surface preserves its editable draft until dismissal or blur");
        Key(UiKey.Tab);
        check(text == "Beta" && Find("Following", UiAccessibilityRole.Button).Node.Focused,
            "Combobox collapsed unmatched draft reconciles on actual Tab blur without taking focus back");
        Send("Editable", UiAccessibilityRole.ComboBox, UiAccessibilityAction.Focus);
        Send("Editable", UiAccessibilityRole.Edit, UiAccessibilityAction.SetValue, "Alpha");
        Key(UiKey.Tab);
        check(text == "Beta" && selected == "b", "Combobox dismissing an open matching draft does not select an option implicitly");
        single.Options = single.Options with
        {
            Freeform = true
        };
        Send("Editable", UiAccessibilityRole.ComboBox, UiAccessibilityAction.Focus);
        Send("Editable", UiAccessibilityRole.Edit, UiAccessibilityAction.SetValue, "custom draft");
        Key(UiKey.Tab);
        check(text == "custom draft", "Combobox freeform text remains intact on real focus loss");
        var desiredOpen = false;
        var allowOpen = false;
        var allowClose = false;
        var openRequests = new List<bool>();
        var openChanges = new List<bool>();
        single.Open = new(() => desiredOpen, value => { openRequests.Add(value); if (value ? allowOpen : allowClose) desiredOpen = value; });
        single.OpenChanged = openChanges.Add;
        Frame();
        Send("Editable", UiAccessibilityRole.ComboBox, UiAccessibilityAction.Expand);
        check(!single.IsOpen && !desiredOpen && openRequests.SequenceEqual(new[] { true }) && openChanges.Count == 0,
            "Combobox controlled state may reject opening before any visible or native expansion is published");
        allowOpen = true;
        Send("Editable", UiAccessibilityRole.ComboBox, UiAccessibilityAction.Expand);
        check(single.IsOpen && desiredOpen && openChanges.SequenceEqual(new[] { true }),
            "Combobox accepted controlled opening publishes one actual transition notification");
        Frame();
        Frame();
        check(openChanges.Count == 1, "Combobox stable controlled frames do not repeat transition callbacks");
        Send("Editable", UiAccessibilityRole.ComboBox, UiAccessibilityAction.Collapse);
        check(single.IsOpen && desiredOpen && Find("Editable", UiAccessibilityRole.ComboBox).Node.Expansion == UiAccessibilityExpansionState.Expanded
            && !Find("Editable", UiAccessibilityRole.ListBox).Node.Offscreen && openChanges.Count == 1,
            "Combobox rejected native collapse preserves the actual popup and expansion metadata");
        Key(UiKey.Escape);
        check(single.IsOpen && openChanges.Count == 1, "Combobox controlled rejection also preserves the popup on Escape");
        Send("Beta", UiAccessibilityRole.Option, UiAccessibilityAction.Select);
        check(single.IsOpen && desiredOpen && text == "Beta" && !Find("Beta", UiAccessibilityRole.Option).Node.Offscreen && openChanges.Count == 1,
            "Combobox rejected selection dismissal still updates its query projection to the authoritative selected label");
        allowClose = true;
        Send("Editable", UiAccessibilityRole.ComboBox, UiAccessibilityAction.Collapse);
        check(!single.IsOpen && !desiredOpen && openChanges.SequenceEqual(new[] { true, false }),
            "Combobox accepted collapse synchronizes its binding and one close notification");
        var requestsBeforeExternal = openRequests.Count;
        single.Text.Value = "no matches";
        Frame();
        check(!single.IsOpen && !desiredOpen && openRequests.Count == requestsBeforeExternal,
            "Combobox external query updates filter state without manufacturing a user request to open");
        desiredOpen = true;
        Frame();
        check(single.IsOpen && Find("Editable", UiAccessibilityRole.ListBox).Node.Bounds.Height > 0
            && Find("Alpha", UiAccessibilityRole.Option).Node.Offscreen && text == "no matches",
            "Combobox external open state can display an empty filtered list without discarding the text query");
        check(!Find("No matches", UiAccessibilityRole.Label).Node.Offscreen
            && !Find("No matches", UiAccessibilityRole.Label).Node.SupportsSelectionItem,
            "Combobox empty-result message is readable static text rather than a fake disabled option");
        desiredOpen = false;
        Frame();
        check(!single.IsOpen && openRequests.Count == requestsBeforeExternal && openChanges.SequenceEqual(new[] { true, false, true, false }),
            "Combobox external state transitions do not echo redundant writes back into the model");
        desiredOpen = true;
        Frame();
        Send("Alpha", UiAccessibilityRole.Option, UiAccessibilityAction.Select);
        check(!single.IsOpen && !desiredOpen && text == "Alpha",
            "Combobox accepted controlled selection dismissal reads the updated model instead of reopening from a stale snapshot");

        var query = new UiState<string>("B");
        var selectedOption = new UiState<string?>(null);
        using var custom = new PressCombobox(query.Binding, selectedOption.Binding, items);
        current = custom;
        Frame();
        static UiVector2 Center(UiRect bounds) => new(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
        var expander = Center(Find("Show options", UiAccessibilityRole.Button).Node.Bounds);
        Frame(idle with { MousePosition = expander, LeftMousePressed = true, LeftMouseReleased = true });
        check(custom.IsOpen && query.Value == "B" && !Find("Alpha", UiAccessibilityRole.Option).Node.Offscreen,
            "Custom Combobox opens the complete list on press without changing the editor's query");
        var option = Center(Find("Beta", UiAccessibilityRole.Option).Node.Bounds);
        Frame(idle with { MousePosition = option, LeftMousePressed = true, LeftMouseDown = true });
        check(!custom.IsOpen && query.Value == "Beta" && selectedOption.Value == "b"
            && Find("Custom choice", UiAccessibilityRole.ComboBox).Node.Value == "Beta",
            "Custom Combobox option press commits text and the native selection together");
        Frame(idle with { MousePosition = option, LeftMouseReleased = true });
        check(!custom.IsOpen && selectedOption.Value == "b", "Custom Combobox release does not reopen or repeat the choice");
        query.Value = "";
        Frame();
        Send("Custom choice", UiAccessibilityRole.ComboBox, UiAccessibilityAction.Focus);
        var commitReads = ime.CommitReads;
        ime.Commit = "a";
        Frame();
        check(query.Value == "a" && custom.IsOpen && ime.CommitReads == commitReads + 1,
            "Custom Combobox still accepts committed text and opens its popup for the new query");
        RunLifetimeBoundaries(check);
    }

    private sealed class PressCombobox(UiBinding<string> text, UiBinding<string?> value, UiSelectOption[] items)
        : UiCombobox<PressPopupBehavior, UiFluentComboboxPresentation>("Custom choice", text, value, items);
    private static void RunLifetimeBoundaries(Action<bool, string> check)
    {
        using var first = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        using var second = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(first);
        ControlFontFixture.Configure(second);
        using var firstTree = new UiAccessibilityTree();
        using var secondTree = new UiAccessibilityTree();
        first.SetAccessibilityTree(firstTree);
        second.SetAccessibilityTree(secondTree);
        var text = "Al";
        string? selected = "b";
        using var combo = Fluent.Combobox("Transferred editor", new UiBinding<string>(() => text, value => text = value),
            new UiBinding<string?>(() => selected, value => selected = value), [new("a", "Alpha"), new("b", "Beta")],
            new(Freeform: true));
        using var following = Fluent.Button("Lifetime following", () => { });
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(UiContext context, bool show, UiWidget? replacement = null)
        {
            context.NewFrame(new(0, new(640, 480), new(1, 1)), idle, new(0, 0, 640, 480), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding: 0);
                if (show)
                    (replacement ?? combo).Render(ui);
                following.Render(ui);
            }));
            using var data = context.GetDrawData();
        }
        List<UiAccessibilityElement> Elements(UiAccessibilityTree tree)
        {
            var result = new List<UiAccessibilityElement>();
            void Visit(string? id)
            {
                while (id is not null && tree.TryGet(id, out var item))
                {
                    result.Add(item);
                    Visit(item.FirstChild);
                    id = item.Next;
                }
            }
            Visit(tree.Root.First);
            return result;
        }
        Frame(first, true);
        Frame(second, false);
        var oldRoot = Elements(firstTree).Single(e => e.Node.Role == UiAccessibilityRole.ComboBox);
        firstTree.RequestAction(oldRoot.Node.Id, oldRoot.RuntimeId, UiAccessibilityAction.Expand, wait: false);
        Frame(first, true);
        check(combo.IsOpen, "Combobox lifetime fixture starts with a real expanded popup");
        Frame(second, true);
        var transferred = Elements(secondTree);
        check(!combo.IsOpen && transferred.Count(e => e.Node.Role == UiAccessibilityRole.ListBox) == 1
            && transferred.Count(e => e.Node.Role == UiAccessibilityRole.Option) == 2 && text == "Al" && selected == "b",
            "Combobox same-frame context transfer republishes canonical options and preserves text and selection bindings");
        Frame(first, false);
        check(Elements(firstTree).All(e => e.Node.Role is not (UiAccessibilityRole.ComboBox or UiAccessibilityRole.Edit or UiAccessibilityRole.Option))
            && firstTree.RequestAction(oldRoot.Node.Id, oldRoot.RuntimeId, UiAccessibilityAction.Expand, wait: false) != UiAccessibilityResult.Success,
            "Combobox context transfer retires the previous editor and rejects stale native provider actions");
        var root = transferred.Single(e => e.Node.Role == UiAccessibilityRole.ComboBox);
        secondTree.RequestAction(root.Node.Id, root.RuntimeId, UiAccessibilityAction.Expand, wait: false);
        Frame(second, true);
        check(combo.IsOpen, "Combobox transferred editor can reopen using its new native provider");
        combo.Dispose();
        Frame(second, false);
        var remaining = Elements(secondTree);
        check(!combo.IsOpen && remaining.Count == 1 && remaining[0].Node.Name == "Lifetime following"
            && !remaining[0].Node.Offscreen && second.State.NextScheduledFrameTimeSeconds is null,
            "Combobox disposal closes its popup, retires owned providers and releases following controls without periodic work");
        check(secondTree.RequestAction(root.Node.Id, root.RuntimeId, UiAccessibilityAction.SetValue, "stale", wait: false) != UiAccessibilityResult.Success
            && text == "Al" && selected == "b",
            "Combobox disposal rejects stale text edits without mutating caller-owned bindings");
        using var resized = Fluent.Combobox("Allocated editor", new UiBinding<string>(() => text, value => text = value),
            new UiBinding<string?>(() => selected, value => selected = value), [new("a", "Alpha"), new("b", "Beta")]);
        using var allocated = Dux.SizedBox(resized, 80, 64);
        Frame(second, true, allocated);
        var allocatedRoot = Elements(secondTree).Single(e => e.Node.Role == UiAccessibilityRole.ComboBox);
        check(allocatedRoot.Node.Bounds is { Width: 80, Height: 64 },
            "Combobox uses the parent's actual width and height instead of its preferred field size");
        secondTree.RequestAction(allocatedRoot.Node.Id, allocatedRoot.RuntimeId, UiAccessibilityAction.Expand, wait: false);
        Frame(second, true, allocated);
        check(Elements(secondTree).Single(e => e.Node.Role == UiAccessibilityRole.ListBox).Node.Bounds.Y >= allocatedRoot.Node.Bounds.Y + 64,
            "Combobox popup placement anchors below the parent's full allocation");
        secondTree.RequestAction(allocatedRoot.Node.Id, allocatedRoot.RuntimeId, UiAccessibilityAction.Collapse, wait: false);
        Frame(second, true, allocated);
        allocated.Options = new(120, 24);
        Frame(second, true, allocated);
        var updatedRoot = Elements(secondTree).Single(e => e.Node.Role == UiAccessibilityRole.ComboBox);
        check(updatedRoot.RuntimeId == allocatedRoot.RuntimeId && updatedRoot.Node.Bounds is { Width: 120, Height: 24 },
            "Combobox live parent allocation preserves the native provider identity");
        resized.Options = resized.Options with
        {
            Field = new(Invalid: true)
        };
        Frame(second, true, allocated);
        check(Elements(secondTree).Where(e => e.Node.Role is UiAccessibilityRole.ComboBox or UiAccessibilityRole.Edit).All(e => e.Node.Invalid),
            "Combobox standalone invalid state reaches both the native root and its actual editor");
        resized.Options = resized.Options with
        {
            Field = new(Invalid: false)
        };
        Frame(second, true, allocated);
        check(Elements(secondTree).Where(e => e.Node.Role is UiAccessibilityRole.ComboBox or UiAccessibilityRole.Edit).All(e => !e.Node.Invalid),
            "Combobox clearing standalone invalid state clears both native projections");
    }
    private sealed class Ime : IUiImeHandler
    {
        internal string? Composition;
        internal string? Commit;
        internal string? Owner;
        internal int CommitReads;
        public void SetCaretRect(UiRect caretRect, UiRect inputRect, float fontPixelHeight, float fontPixelWidth)
        {
        }
        public string? GetCompositionText() => Owner is null ? null : Composition;
        public void SetCompositionOwner(string? inputId) => Owner = inputId;
        public string? ConsumeCommittedText(string inputId)
        {
            if (inputId != Owner || Commit is null)
                return null;
            var result = Commit;
            Commit = null;
            CommitReads++;
            return result;
        }
        public void ReleaseInput(string inputId) => _ = ConsumeCommittedText(inputId);
        public string? ConsumeRecentCommittedText() => null;
    }
    private sealed class Screen(Action<UiImmediateContext> draw) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => draw(ui);
    }
}
