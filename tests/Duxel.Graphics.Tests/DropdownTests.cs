using Duxel.Core;

internal static class DropdownTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        string? value = null;
        string[] values = [];
        UiSelectOption[] items = [new("a", "Alpha"), new("b", "Beta"), new("g", "Gamma", false), new("n", "New York")];
        using var single = Fluent.Dropdown("Choice", new UiBinding<string?>(() => value, next => value = next), items);
        using var multiple = Fluent.Dropdown("Choices", new UiBinding<string[]>(() => values, next => values = next), items);
        UiWidget current = single;
        Action<UiImmediateContext>? renderOverride = null;
        var disabled = false;
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var events = new List<UiAccessibilitySelectionChange>();
        tree.SelectionChanged += events.Add;
        var texts = new List<UiAccessibilityTextChange>();
        tree.TextPropertyChanged += texts.Add;
        void Frame(UiInputState? input = null)
        {
            context.NewFrame(new(0, new(640, 480), new(1, 1)), input ?? idle, new(0, 0, 640, 480), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding: 0);
                ui.BeginDisabled(disabled);
                try
                {
                    if (renderOverride is null)
                        current.Render(ui);
                    else
                        renderOverride(ui);
                }
                finally { ui.EndDisabled(); }
            }));
            using var data = context.GetDrawData();
        }
        UiAccessibilityElement[] Elements()
        {
            var all = new List<UiAccessibilityElement>();
            void Visit(string? id)
            {
                while (id is not null && tree.TryGet(id, out var element))
                {
                    all.Add(element);
                    Visit(element.FirstChild);
                    id = element.Next;
                }
            }
            Visit(tree.Root.First);
            return all.ToArray();
        }
        UiAccessibilityElement Find(string name, UiAccessibilityRole role) => Elements().Single(e => e.Node.Name == name && e.Node.Role == role);
        UiAccessibilityResult Send(string name, UiAccessibilityRole role, UiAccessibilityAction action)
        {
            var e = Find(name, role);
            return tree.RequestAction(e.Node.Id, e.RuntimeId, action, wait: false);
        }
        void Key(UiKey key) => Frame(idle with
        {
            KeyEvents = [new(key, true, default)]
        });
        Frame();
        var root = Find("Choice", UiAccessibilityRole.ComboBox);
        check(root.Node.Value == "" && root.Node.ReadOnly && root.Node.SupportsSelection
            && !root.Node.CanSelectMultiple && !root.Node.SupportsInvoke,
            "Dropdown exposes a readonly named ComboBox with real Selection and ExpandCollapse semantics");
        check(Find("Choice", UiAccessibilityRole.ListBox).Node.Offscreen && Elements().Count(e => e.Node.Role == UiAccessibilityRole.Option) == 4,
            "Dropdown retains its logical option providers while collapsed");
        check(Send("Beta", UiAccessibilityRole.Option, UiAccessibilityAction.Select) == UiAccessibilityResult.Success,
            "Dropdown closed logical options accept explicit native selection actions");
        Frame();
        check(value == "b" && !single.IsOpen && Find("Choice", UiAccessibilityRole.ComboBox).Node.Value == "Beta",
            "Dropdown native selection works while collapsed and updates the actual bound value");
        check(texts.Any(e => e.Property == UiAccessibilityTextProperty.Value && e.NewValue == "Beta")
            && !texts.Any(e => e.Property == UiAccessibilityTextProperty.Name && e.NewValue == "Beta"),
            "Dropdown selection publishes Value changes without replacing its accessible Name");
        Send("Choice", UiAccessibilityRole.ComboBox, UiAccessibilityAction.Focus);
        Frame();
        Key(UiKey.DownArrow);
        check(single.IsOpen && !Find("Choice", UiAccessibilityRole.ListBox).Node.Offscreen
            && Find("Beta", UiAccessibilityRole.Option).Node.Focused,
            "Dropdown keyboard opening focuses the selected enabled option");
        Frame(idle with
        {
            CharEvents = [new((uint)'n'), new((uint)'e'), new((uint)'w'), new((uint)' '), new((uint)'y')]
        });
        check(Find("New York", UiAccessibilityRole.Option).Node.Focused && value == "b",
            "Dropdown incremental typeahead preserves spaces and does not commit while searching");
        Key(UiKey.Home);
        Key(UiKey.Enter);
        check(value == "a" && !single.IsOpen, "Dropdown keyboard selection closes single-select and commits its source value");
        Frame();
        Send("Choice", UiAccessibilityRole.ComboBox, UiAccessibilityAction.Expand);
        Frame();
        Key(UiKey.End);
        Key(UiKey.UpArrow);
        check(Find("Beta", UiAccessibilityRole.Option).Node.Focused, "Dropdown arrow navigation skips disabled options");
        Send("New York", UiAccessibilityRole.Option, UiAccessibilityAction.Select);
        Frame();
        check(value == "n" && !single.IsOpen, "Dropdown native selection also dismisses an expanded single-select list");
        Frame();
        Frame(idle with
        {
            CharEvents = [new((uint)'b')]
        });
        check(single.IsOpen && Find("Beta", UiAccessibilityRole.Option).Node.Focused && value == "n",
            "Dropdown typeahead opens the collapsed field without replacing the selection");
        Frame(idle with
        {
            KeyEvents = [new(UiKey.UpArrow, true, KeyModifiers.Alt)]
        });
        Frame();
        check(!single.IsOpen, "Dropdown Alt Up dismisses without committing the active search result");
        current = multiple;
        Frame();
        check(Find("Choices", UiAccessibilityRole.ComboBox).Node.CanSelectMultiple
            && Find("Choices", UiAccessibilityRole.ListBox).Node.CanSelectMultiple,
            "Dropdown multi-select exposes matching root and listbox selection policies");
        Send("Choices", UiAccessibilityRole.ComboBox, UiAccessibilityAction.Expand);
        Frame();
        Send("Alpha", UiAccessibilityRole.Option, UiAccessibilityAction.Select);
        Frame();
        check(Send("Beta", UiAccessibilityRole.Option, UiAccessibilityAction.AddToSelection) == UiAccessibilityResult.Success,
            "Dropdown supports the distinct AddToSelection action contract");
        Frame();
        check(values.SequenceEqual(new[] { "a", "b" }) && multiple.IsOpen,
            "Dropdown AddToSelection preserves existing selections and keeps the multi-select popup open");
        check(events.Any(e => e.Element.Node.Name == "Beta" && e.Event == UiAccessibilitySelectionEventKind.Added),
            "Dropdown multiple additions publish the Added selection event");
        Send("New York", UiAccessibilityRole.Option, UiAccessibilityAction.Select);
        Frame();
        check(values.SequenceEqual(new[] { "n" }) && multiple.IsOpen,
            "Dropdown native Select replaces multiple selection instead of acting like AddToSelection");
        Send("New York", UiAccessibilityRole.Option, UiAccessibilityAction.Deselect);
        Frame();
        check(values.Length == 0 && events.Any(e => e.Element.Node.Name == "New York" && e.Event == UiAccessibilitySelectionEventKind.Removed),
            "Dropdown optional multiple selection permits clearing its last selected item");
        check(Send("Gamma", UiAccessibilityRole.Option, UiAccessibilityAction.Select) == UiAccessibilityResult.Disabled,
            "Dropdown disabled options reject native selection");
        Key(UiKey.Escape);
        Frame();
        check(!multiple.IsOpen && Find("Choices", UiAccessibilityRole.ComboBox).Node.Focused,
            "Dropdown Escape restores focus to its combobox trigger");
        disabled = true;
        Frame();
        check(Send("Choices", UiAccessibilityRole.ComboBox, UiAccessibilityAction.Expand) == UiAccessibilityResult.Disabled,
            "Dropdown parent disabled blocks expansion");
        check(context.State.NextScheduledFrameTimeSeconds is null, "Dropdown idle rendering schedules no periodic frames");
        disabled = false;
        values = ["a", "b"];
        Frame();
        values[0] = "n";
        Frame();
        check(Find("Choices", UiAccessibilityRole.ComboBox).Node.Value == "Beta, New York"
            && !Find("Alpha", UiAccessibilityRole.Option).Node.Selected!.Value,
            "Dropdown observes an externally mutated selection array without relying on reference changes");
        Send("Choices", UiAccessibilityRole.ComboBox, UiAccessibilityAction.Focus);
        Frame();
        Key(UiKey.DownArrow);
        Key(UiKey.Home);
        Frame(idle with
        {
            KeyEvents = [new(UiKey.Space, true, default)]
        });
        check(!values.Contains("a"), "Dropdown multi-select Space waits for key release before committing");
        Frame(idle with
        {
            KeyEvents = [new(UiKey.Space, false, default)]
        });
        check(values.SequenceEqual(new[] { "a", "b", "n" }) && multiple.IsOpen,
            "Dropdown multi-select Space adds one option without replacing existing selections");
        Frame(idle with
        {
            KeyEvents = [new(UiKey.Space, true, default)]
        });
        Frame(idle with
        {
            KeyEvents = [new(UiKey.Space, false, default)]
        });
        check(values.SequenceEqual(new[] { "b", "n" }), "Dropdown multi-select repeated Space removes the focused option");
        Key(UiKey.Escape);
        Frame();

        string? leftValue = null;
        string? rightValue = null;
        using var left = Fluent.Dropdown("Left choice", new UiBinding<string?>(() => leftValue, next => leftValue = next),
            items, identity: UiWidgetIdentity.Parent);
        using var right = Fluent.Dropdown("Right choice", new UiBinding<string?>(() => rightValue, next => rightValue = next),
            items, identity: UiWidgetIdentity.Parent);
        renderOverride = ui => { left.Render(ui); right.Render(ui); };
        Frame();
        var leftList = Find("Left choice", UiAccessibilityRole.ListBox);
        var rightList = Find("Right choice", UiAccessibilityRole.ListBox);
        var siblings = Elements().Where(e => e.Node.Role == UiAccessibilityRole.Option && e.Node.Name == "Beta").ToArray();
        check(siblings.Length == 2 && siblings[0].Node.Id != siblings[1].Node.Id && leftList.Node.Id != rightList.Node.Id,
            "Dropdown Parent identities isolate repeated option values and internal listbox identifiers");
        var rightBeta = siblings.Single(e => e.Node.ParentId == rightList.Node.Id);
        tree.RequestAction(rightBeta.Node.Id, rightBeta.RuntimeId, UiAccessibilityAction.Select, wait: false);
        Frame();
        check(leftValue is null && rightValue == "b", "Dropdown a sibling option action changes only its owning binding");
        Send("Left choice", UiAccessibilityRole.ComboBox, UiAccessibilityAction.Expand);
        Frame();
        check(left.IsOpen && !right.IsOpen && Find("Right choice", UiAccessibilityRole.ListBox).Node.Offscreen,
            "Dropdown sibling expansion keeps independent logical lists and opens only one popup");
        Send("Left choice", UiAccessibilityRole.ComboBox, UiAccessibilityAction.Collapse);
        Frame();
        check(Find("Right choice", UiAccessibilityRole.ListBox).RuntimeId == rightList.RuntimeId,
            "Dropdown sibling popup transitions preserve the other list provider identity");

        using var allocated = Dux.SizedBox(single, 80, 64);
        renderOverride = null;
        current = allocated;
        Frame();
        var allocatedRoot = Find("Choice", UiAccessibilityRole.ComboBox);
        check(allocatedRoot.Node.Bounds.Width == 80 && allocatedRoot.Node.Bounds.Height == 64,
            "Dropdown accepts the parent's actual width and height allocation");
        Send("Choice", UiAccessibilityRole.ComboBox, UiAccessibilityAction.Expand);
        Frame();
        check(Find("Choice", UiAccessibilityRole.ListBox).Node.Bounds.Y >= allocatedRoot.Node.Bounds.Y + 64,
            "Dropdown popup placement anchors below the full parent allocation");
        Send("Choice", UiAccessibilityRole.ComboBox, UiAccessibilityAction.Collapse);
        Frame();
        allocated.Options = new(120, 24);
        Frame();
        check(Find("Choice", UiAccessibilityRole.ComboBox).RuntimeId == allocatedRoot.RuntimeId
            && Find("Choice", UiAccessibilityRole.ComboBox).Node.Bounds is { Width: 120, Height: 24 },
            "Dropdown live parent allocation changes preserve the original logical provider");

        string? normalized = null;
        using var normalizing = Fluent.Dropdown("Normalized", new UiBinding<string?>(() => normalized, next => normalized = next == "b" ? "a" : next), items);
        current = normalizing;
        Frame();
        Send("Beta", UiAccessibilityRole.Option, UiAccessibilityAction.Select);
        Frame();
        check(normalized == "a" && Find("Normalized", UiAccessibilityRole.ComboBox).Node.Value == "Alpha"
            && Find("Alpha", UiAccessibilityRole.Option).Node.Selected == true && Find("Beta", UiAccessibilityRole.Option).Node.Selected == false,
            "Dropdown paints and publishes the binding setter's normalized value in the same frame");
        var originalItems = new UiSelectOption[] { new("one", "Original") };
        using var copied = Fluent.Dropdown("Copied", new UiBinding<string?>(() => null, _ => { }), originalItems);
        originalItems[0] = new("two", "Replaced");
        check(copied.Items.Length == 1 && copied.Items[0].Text == "Original", "Dropdown retains its immutable option snapshot after caller array mutation");
        using var empty = Fluent.Dropdown("Empty", new UiBinding<string?>(() => null, _ => { }), []);
        current = empty;
        Frame();
        Send("Empty", UiAccessibilityRole.ComboBox, UiAccessibilityAction.Expand);
        Frame();
        check(!empty.IsOpen && Find("Empty", UiAccessibilityRole.ListBox).Node.Offscreen,
            "Dropdown empty option collections remain collapsed without periodic work");
        current = single;
        single.Options = single.Options with { Invalid = true };
        Frame();
        check(Find("Choice", UiAccessibilityRole.ComboBox).Node.Invalid,
            "Dropdown standalone invalid state reaches the native ComboBox");
        single.Options = single.Options with { Invalid = false };
        Frame();
        check(!Find("Choice", UiAccessibilityRole.ComboBox).Node.Invalid,
            "Dropdown clearing standalone invalid state clears its native projection");

        UiVector2 Center(string name, UiAccessibilityRole role)
        {
            var bounds = Find(name, role).Node.Bounds;
            return new(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
        }
        void Press(UiVector2 position)
            => Frame(idle with { MousePosition = position, LeftMousePressed = true, LeftMouseDown = true });
        void Release(UiVector2 position)
            => Frame(idle with { MousePosition = position, LeftMouseReleased = true });

        string[] customValues = ["a"];
        using var customDropdown = new PressDropdown(new(() => customValues, next => customValues = next), items);
        current = customDropdown;
        Frame();
        var trigger = Center("Press choices", UiAccessibilityRole.ComboBox);
        Press(trigger);
        check(customDropdown.IsOpen && customValues.SequenceEqual(["a"]),
            "Custom Dropdown opens on press without changing the existing selection");
        Release(trigger);
        check(customDropdown.IsOpen, "Custom Dropdown release does not toggle the popup again");
        var beta = Center("Beta", UiAccessibilityRole.Option);
        Press(beta);
        check(customDropdown.IsOpen && customValues.SequenceEqual(["a", "b"])
            && Find("Beta", UiAccessibilityRole.Option).Node.Selected == true,
            "Custom Dropdown selects on press and publishes multiple selection while keeping the popup open");
        Release(beta);
        check(customValues.SequenceEqual(["a", "b"]), "Custom Dropdown release does not select the option again");
        Send("Beta", UiAccessibilityRole.Option, UiAccessibilityAction.Deselect);
        Frame();
        check(customValues.SequenceEqual(["a"]) && customDropdown.IsOpen,
            "Custom Dropdown still accepts a native deselection independently of pointer timing");
        Frame(idle with { MousePosition = beta, LeftMousePressed = true, LeftMouseReleased = true });
        check(customValues.SequenceEqual(["a", "b"]) && customDropdown.IsOpen,
            "Custom Dropdown handles one fast option click whose press and release arrive in the same frame");
        Send("Press choices", UiAccessibilityRole.ComboBox, UiAccessibilityAction.Collapse);
        Frame();

        var attempted = 0;
        using var customSelect = new PressSelect(new(() => "a", _ => attempted++), items);
        current = customSelect;
        Frame();
        trigger = Center("Press choice", UiAccessibilityRole.ComboBox);
        Press(trigger);
        check(customSelect.IsOpen, "Custom Select uses the same press-to-open policy");
        Release(trigger);
        beta = Center("Beta", UiAccessibilityRole.Option);
        Press(beta);
        check(customSelect.IsOpen && attempted == 0,
            "Custom Select can keep release-to-select while opening on press");
        Release(beta);
        check(attempted == 1 && !customSelect.IsOpen
            && Find("Press choice", UiAccessibilityRole.ComboBox).Node.Value == "Alpha"
            && Find("Alpha", UiAccessibilityRole.Option).Node.Selected == true,
            "Custom Select presents the binding's retained value after a rejected selection");
        Frame(idle with { MousePosition = trigger, LeftMousePressed = true, LeftMouseReleased = true });
        check(customSelect.IsOpen && attempted == 1,
            "Custom Select can open when press and release arrive in the same frame");
        RunContextTransfer(check);
    }

    private sealed class PressDropdown(UiBinding<string[]> value, UiSelectOption[] items)
        : UiDropdown<PressSelectionBehavior, UiFluentSelectPresentation>("Press choices", value, items);

    private sealed class PressSelect(UiBinding<string?> value, UiSelectOption[] items)
        : UiSelect<PressTriggerBehavior, UiFluentDropdownPresentation>("Press choice", value, items);

    private readonly struct PressSelectionBehavior : IUiDropdownBehavior
    {
        public static UiButtonActivation TriggerActivation => UiButtonActivation.Press;
        public static UiButtonActivation OptionActivation => UiButtonActivation.Press;
    }

    private readonly struct PressTriggerBehavior : IUiDropdownBehavior
    {
        public static UiButtonActivation TriggerActivation => UiButtonActivation.Press;
        public static UiButtonActivation OptionActivation => UiButtonActivation.Release;
    }

    private static void RunContextTransfer(Action<bool, string> check)
    {
        using var first = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        using var second = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(first);
        ControlFontFixture.Configure(second);
        using var firstTree = new UiAccessibilityTree();
        first.SetAccessibilityTree(firstTree);
        using var secondTree = new UiAccessibilityTree();
        second.SetAccessibilityTree(secondTree);
        using var dropdown = Fluent.Dropdown("Transferred", new UiBinding<string?>(() => null, _ => { }), [new("a", "Alpha")]);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(UiContext context, bool show)
        {
            context.NewFrame(new(0, new(640, 480), new(1, 1)), idle, new(0, 0, 640, 480), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding: 0); if (show) dropdown.Render(ui); }));
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
        var root = Elements(firstTree).Single(e => e.Node.Role == UiAccessibilityRole.ComboBox);
        firstTree.RequestAction(root.Node.Id, root.RuntimeId, UiAccessibilityAction.Expand, wait: false);
        Frame(first, true);
        check(dropdown.IsOpen, "Dropdown context transfer fixture starts with a published expanded popup");
        Frame(second, true);
        var transferred = Elements(secondTree);
        check(!dropdown.IsOpen && transferred.Count(e => e.Node.Role == UiAccessibilityRole.ListBox) == 1
            && transferred.Count(e => e.Node.Role == UiAccessibilityRole.Option) == 1,
            "Dropdown context transfer republishes the closed logical list even at the same frame number");
        Frame(first, false);
        check(Elements(firstTree).Count == 0 && second.State.NextScheduledFrameTimeSeconds is null,
            "Dropdown context transfer retires the previous providers without scheduling periodic work");
    }
    private sealed class Screen(Action<UiImmediateContext> draw) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => draw(ui);
    }
}
