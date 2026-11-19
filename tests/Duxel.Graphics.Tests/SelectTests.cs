using Duxel.Core;

internal static class SelectTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        context.SetDesign(UiCompiledDesign.Windows11Dark); ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree(); context.SetAccessibilityTree(tree);
        string? value = null;
        var writes = 0;
        UiSelectOption[] items = [new("x", "Unavailable", false), new("a", "Alpha"), new("b", "Beta"), new("c", "Charlie", false), new("n", "New York")];
        using var select = Fluent.Select("Plain choice",
            new UiBinding<string?>(() => value, next => { value = next; writes++; }), items);
        check(value == "a" && writes == 1, "Select initializes the first enabled option exactly once");
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var disabled = false;
        void Frame(UiInputState? input = null)
        {
            context.NewFrame(new(.016f, new(640, 480), new(1, 1)), input ?? idle, new(0, 0, 640, 480), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding : 0);
                ui.BeginDisabled(disabled);
                try { select.Render(ui); } finally { ui.EndDisabled(); }
            }));
            using var draw = context.GetDrawData();
        }
        UiAccessibilityElement Find(string name, UiAccessibilityRole role) => tree.CaptureSnapshot().Elements.Single(e => e.Node.Name == name && e.Node.Role == role);
        UiAccessibilityResult Send(string name, UiAccessibilityRole role, UiAccessibilityAction action)
        {
            var target = Find(name, role);
            return tree.RequestAction(target.Node.Id, target.RuntimeId, action, wait : false);
        }
        void Key(UiKey key, KeyModifiers modifiers = default) => Frame(idle with { KeyEvents = [new(key, true, modifiers)] });
        Frame(); Frame();
        var combo = Find("Plain choice", UiAccessibilityRole.ComboBox).Node;
        check(combo.Value == "Alpha" && combo.ReadOnly && !combo.CanSelectMultiple && combo.SelectionRequired,
            "Select exposes a readonly required single-selection ComboBox with the selected text");
        check(Find("Plain choice", UiAccessibilityRole.ListBox).Node.SelectionRequired
            && tree.CaptureSnapshot().Elements.Count(e => e.Node.Role == UiAccessibilityRole.Option) == items.Length,
            "Select retains all logical options and required selection while collapsed");
        check(Send("Alpha", UiAccessibilityRole.Option, UiAccessibilityAction.Deselect) == UiAccessibilityResult.InvalidOperation,
            "Select rejects deselecting the only selected option");
        Send("Plain choice", UiAccessibilityRole.ComboBox, UiAccessibilityAction.Focus); Frame();
        Key(UiKey.DownArrow);
        check(value == "b" && !select.IsOpen && Find("Plain choice", UiAccessibilityRole.ComboBox).Node.Focused,
            "closed Select Down commits the next option and retains field focus without opening");
        Key(UiKey.DownArrow);
        check(value == "n" && !select.IsOpen, "closed Select skips disabled options");
        Key(UiKey.DownArrow);
        check(value == "n", "closed Select clamps at the last option instead of wrapping");
        Key(UiKey.Home); check(value == "a" && !select.IsOpen, "closed Select Home chooses the first enabled option");
        Key(UiKey.End); check(value == "n" && !select.IsOpen, "closed Select End chooses the last enabled option");
        Key(UiKey.UpArrow); check(value == "b" && !select.IsOpen, "closed Select Up commits the preceding enabled option");
        Frame(idle with { CharEvents = [new((uint)'n'), new((uint)'e'), new((uint)'w'), new((uint)' '), new((uint)'y')] });
        check(value == "n" && !select.IsOpen, "closed Select incremental name search commits the matching text including spaces");
        Key(UiKey.Home);
        Frame(idle with { CharEvents = [new((uint)'n'), new((uint)'e'), new((uint)'w')] });
        Frame(idle with { KeyEvents = [new(UiKey.Space, true, default)], CharEvents = [new((uint)' ')] });
        Frame(idle with { KeyEvents = [new(UiKey.Space, false, default)] });
        Frame(idle with { CharEvents = [new((uint)'y')] });
        check(value == "n" && !select.IsOpen,
            "real Space press/release within name search stays text instead of opening the popup on release");
        Key(UiKey.DownArrow, KeyModifiers.Alt);
        check(select.IsOpen && !Find("Plain choice", UiAccessibilityRole.ListBox).Node.Offscreen,
            "Select Alt Down opens the shared popup");
        Key(UiKey.Home); Key(UiKey.Enter);
        check(value == "a" && !select.IsOpen, "expanded Select commits Enter and returns to the field");
        var root = Find("Plain choice", UiAccessibilityRole.ComboBox).Node.Bounds;
        var point = new UiVector2(root.X + root.Width - 12, root.Y + root.Height / 2);
        Frame(idle with { MousePosition = point, LeftMouseDown = true, LeftMousePressed = true });
        Frame(idle with { MousePosition = point, LeftMouseReleased = true });
        check(select.IsOpen, "Select chevron pointer click opens the option popup");
        Key(UiKey.End); Key(UiKey.Escape);
        check(value == "a" && !select.IsOpen, "Select Escape cancels an uncommitted popup navigation");
        Send("Beta", UiAccessibilityRole.Option, UiAccessibilityAction.Select); Frame();
        check(value == "b" && !select.IsOpen, "Select accepts explicit accessibility selection while collapsed");
        value = "n"; Frame();
        check(Find("Plain choice", UiAccessibilityRole.ComboBox).Node.Value == "New York", "external binding changes update Select without reopening");
        var previousWrites = writes; Frame(); Frame();
        check(writes == previousWrites && !context.State.HasActiveAnimations, "idle Select performs no default rewrites or animation polling");
        disabled = true; Frame(); Key(UiKey.Home);
        check(value == "n" && !select.IsOpen, "disabled Select blocks closed keyboard changes");
        disabled = false; select.Options = select.Options with { Invalid = true, RightToLeft = true, Width = 120 }; Frame();
        check(Find("Plain choice", UiAccessibilityRole.ComboBox).Node.Invalid
            && Find("Plain choice", UiAccessibilityRole.ComboBox).Node.Bounds.Width == 120,
            "Select options update validation, direction and constrained geometry");
        var rejectedWrites = 0;
        using var rejected = new UiSelect<UiFluentSelectPresentation>("Controlled", new UiBinding<string?>(() => null, _ => rejectedWrites++), items);
        check(rejectedWrites == 1, "controlled Select default proposal is attempted once and respects a rejecting setter");
        var richRejected = false;
        try { using var rich = new UiSelect<UiFluentSelectPresentation>("Rich", new UiState<string?>(null).Binding,
            [new UiSelectOption("a", "Alpha") { Description = "Details" }]); }
        catch (ArgumentException) { richRejected = true; }
        check(richRejected, "Select rejects rich option content rather than silently behaving as Dropdown");
        using var empty = new UiSelect<UiFluentSelectPresentation>("Empty", new UiState<string?>(null).Binding, []);
        check(empty.Items.Length == 0 && !empty.IsOpen, "empty Select remains a valid empty control");
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    { public override void Render(UiImmediateContext ui) => render(ui); }
}
