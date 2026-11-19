using Duxel.Core;
using Duxel.Platform.Windows;

internal static class RadioAccessibilityTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        using var native = new WindowsAccessibility(0);
        var tree = native.Tree;
        context.SetAccessibilityTree(tree);
        ControlFontFixture.Configure(context);
        string? value = null;
        var writes = new List<string?>();
        UiRadioOption[] options = [new("a", "Alpha", Description : "First option"), new("b", "Beta"), new("c", "Disabled", false)];
        using var group = Fluent.RadioGroup("Choice", new UiBinding<string?>(() => value, next => { value = next; writes.Add(next); }), options);
        var disabled = false;
        var clipped = false;
        var visible = true;
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var changes = new List<UiAccessibilitySelectionChange>();
        var committed = true;
        tree.SelectionChanged += change =>
        {
            changes.Add(change);
            committed &= tree.TryGet(change.Element.Node.Id, out var current) && current == change.Element;
        };
        var screen = new Screen(ui =>
        {
            ui.EnableRootViewportContentLayout();
            ui.BeginDisabled(disabled);
            if (clipped) ui.PushClipRect(new(900, 900, 1, 1), true);
            if (visible) group.Render(ui);
            if (clipped) ui.PopClipRect();
            ui.EndDisabled();
        });
        void Frame(UiInputState? input = null)
        {
            context.NewFrame(new(1f / 60, new(640, 400), new(1, 1)), input ?? idle, new(0, 0, 640, 400), UiTextSettings.Default);
            context.Render(screen);
            using var draw = context.GetDrawData();
        }
        UiAccessibilityElement Find(string name)
        {
            var stack = new Stack<string>();
            if (tree.Root.First is { } first) stack.Push(first);
            while (stack.TryPop(out var id))
            {
                if (!tree.TryGet(id, out var node)) throw new InvalidOperationException("Missing radio node.");
                if (node.Node.Name == name) return node;
                if (node.Next is { } next) stack.Push(next);
                if (node.FirstChild is { } child) stack.Push(child);
            }
            throw new InvalidOperationException("Radio node missing : " + name);
        }
        UiAccessibilityResult Request(string name, UiAccessibilityAction action)
        {
            var item = Find(name);
            return tree.RequestAction(item.Node.Id, item.RuntimeId, action, wait : false);
        }
        Frame();
        check(tree.Count == 4 && Find("Choice").Node is { Role : UiAccessibilityRole.RadioGroup, SupportsSelection : true,
            SelectionRequired : true, Focusable : false, ValuePattern : false }, "real generated RadioGroup publishes one required single-selection owner");
        var alpha = Find("Alpha");
        var beta = Find("Beta");
        var provider = native.Find(alpha.Node.Id)!;
        provider.GetPropertyValue(30003, out var role);
        check(alpha.Node is { Role : UiAccessibilityRole.RadioButton, Selected : false, SupportsSelectionItem : true, ToggleState : null,
            SupportsInvoke : false, Hint : "First option" } && alpha.Node.ParentId == Find("Choice").Node.Id && role.Data.Integer == 50013,
            "radio options expose RadioButton50013 and SelectionItem, never Toggle or Invoke, with their real group and description");
        check(provider.GetSelectionContainer(out var container) == 0 && container != 0, "native radio SelectionContainer identifies its actual group");
        System.Runtime.InteropServices.Marshal.Release(container);
        var revision = tree.Revision;
        Frame();
        check(tree.Revision == revision && changes.Count == 0 && writes.Count == 0, "idle radio publication produces no revision, selection event or callback");
        check(Request("Beta", UiAccessibilityAction.Select) == UiAccessibilityResult.Success, "unselected radio accepts the common Select queue");
        Frame();
        check(value == "b" && writes.SequenceEqual(new[] { "b" }) && Find("Beta").Node.Selected == true && changes.Count == 1 && committed,
            "radio Select changes the actual caller binding once and publishes its immutable committed selection");
        Request("Beta", UiAccessibilityAction.Select); Frame();
        check(writes.Count == 1 && changes.Count == 1, "reselecting a radio is idempotent");
        check(Request("Beta", UiAccessibilityAction.Deselect) == UiAccessibilityResult.InvalidOperation &&
            Request("Beta", UiAccessibilityAction.Toggle) == UiAccessibilityResult.InvalidOperation &&
            Request("Beta", UiAccessibilityAction.Invoke) == UiAccessibilityResult.InvalidArgument,
            "selected radio cannot be deselected, toggled or invoked through unrelated patterns");
        Request("Alpha", UiAccessibilityAction.Select); disabled = true; Frame();
        check(value == "b" && writes.Count == 1 && Request("Alpha", UiAccessibilityAction.Select) == UiAccessibilityResult.Disabled,
            "accepted radio selection revalidates current group disabled state before mutation");
        disabled = false; Frame(); Request("Alpha", UiAccessibilityAction.Select); clipped = true; Frame();
        check(value == "b" && writes.Count == 1 && Find("Alpha").Node.Offscreen, "accepted radio selection revalidates current clipping before mutation");
        clipped = false; Frame();
        check(Request("Disabled", UiAccessibilityAction.Select) == UiAccessibilityResult.Disabled, "disabled radio option rejects selection independently of its group");
        Request("Alpha", UiAccessibilityAction.Focus); Frame();
        check(value == "b" && Find("Alpha").Node.Focused && !Find("Beta").Node.Focused, "radio Focus moves real roving focus without selecting the option");
        Frame();
        check(Find("Alpha").Node.Focused && value == "b", "unselected radio focus survives an idle frame");
        Frame(idle with { KeyEvents = [new(UiKey.RightArrow, true, default)] });
        check(value == "b" && Find("Beta").Node.Focused, "radio arrow navigation continues from the programmatically focused option");
        Frame(idle with { KeyEvents = [new(UiKey.RightArrow, false, default), new(UiKey.LeftArrow, true, default)] });
        check(value == "a" && writes.Count == 2 && Find("Alpha").Node.Focused, "existing arrow selection and callback order remain connected after native focus");
        group.SetItems(new UiRadioOption[] { options[1], options[0], options[2] }); Frame();
        check(Find("Alpha").RuntimeId == alpha.RuntimeId && Find("Beta").RuntimeId == beta.RuntimeId,
            "radio item runtime identity follows immutable option value across reorder");
        group.SetItems(new UiRadioOption[] { options[0], options[2] }); Frame();
        check(!tree.TryGet(beta.Node.Id, out _) && tree.RequestAction(beta.Node.Id, beta.RuntimeId, UiAccessibilityAction.Select, wait : false) == UiAccessibilityResult.Unavailable,
            "removed radio identity rejects retained selection requests");
        visible = false; Frame();
        check(tree.Count == 0 && provider.GetIsSelected(out _) == UiaNative.Unavailable, "removing the group retires all options and retained native getters");
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
