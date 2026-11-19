using Duxel.Core;

internal static class TagPickerTests
{
    internal static void Run(Action<bool, string> check)
    {
        RunPopupPointerSelection(check);
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        var query = string.Empty;
        string[] selected = ["a"];
        var accept = true;
        UiSelectOption[] items = [new("a", "Alpha"), new("b", "Beta"), new("k", "디모이"), new("d", "Disabled", false)];
        using var picker = Fluent.TagPicker("Recipients", new UiBinding<string>(() => query, value => query = value),
            new UiBinding<string[]>(() => selected, value => { if (accept) selected = value; }), items,
            new(Field: new(Width: 320), EmptyText: "No people", DismissLabel: "Remove"));
        using var label = Fluent.Label("People", picker);
        using var next = Fluent.Button("Following", () => { });
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(UiInputState? input = null)
        {
            context.NewFrame(new(.016f, new(640, 700), new(1, 1)), input ?? idle, new(0, 0, 640, 700), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding: 0); label.Render(ui); picker.Render(ui); next.Render(ui); }));
            using var data = context.GetDrawData();
        }
        UiAccessibilityElement Find(string name, UiAccessibilityRole role) => tree.CaptureSnapshot().Elements.Single(e => e.Node.Name == name && e.Node.Role == role);
        void Send(string name, UiAccessibilityRole role, UiAccessibilityAction action, string? value = null)
        {
            var node = Find(name, role);
            check(tree.RequestAction(node.Node.Id, node.RuntimeId, action, value, wait: false) == UiAccessibilityResult.Success,
                "TagPicker native request accepted : " + action);
            Frame(); Frame();
        }
        void Key(UiKey key)
        {
            Frame(idle with { KeyEvents = [new(key, true, default)] });
            Frame(idle with { KeyEvents = [new(key, false, default)] });
        }
        Frame();
        check(Find("Remove Alpha", UiAccessibilityRole.Button).Node.Bounds.Width > 0, "TagPicker renders its selected value as a dismissible tag");
        check(ReferenceEquals(label.Target, picker.LabelControl), "compound TagPicker label delegates to its owned input");
        Send("People", UiAccessibilityRole.ComboBox, UiAccessibilityAction.Focus);
        Send("People", UiAccessibilityRole.ComboBox, UiAccessibilityAction.SetValue, "Be");
        Key(UiKey.Enter); Frame();
        check(selected.SequenceEqual(["a", "b"]) && query.Length == 0 && !picker.IsOpen,
            "partial query Enter appends its first match, clears query and closes the popup");
        Send("People", UiAccessibilityRole.ComboBox, UiAccessibilityAction.Expand);
        check(Find("Alpha", UiAccessibilityRole.Option).Node.Offscreen,
            "already selected choices remain accessible to selection but are excluded from visible suggestions");
        Key(UiKey.Escape); Frame();
        Key(UiKey.Backspace); Frame();
        check(Find("Remove Beta", UiAccessibilityRole.Button).Node.Focused, "empty-input Backspace focuses the last selected tag");
        Key(UiKey.Delete); Frame();
        check(selected.SequenceEqual(["a"]), "Delete dismisses the focused selected tag");
        accept = false;
        Send("People", UiAccessibilityRole.ComboBox, UiAccessibilityAction.Focus);
        Send("People", UiAccessibilityRole.ComboBox, UiAccessibilityAction.SetValue, "Be");
        Key(UiKey.Enter); Frame();
        Console.WriteLine($"Rejected selection state : query=[{query}] / values=[{string.Join(",", selected)}] / open={picker.IsOpen} / focused={string.Join(",", tree.CaptureSnapshot().Elements.Where(element => element.Node.Focused).Select(element => element.Node.Role + ":" + element.Node.Name))}");
        check(selected.SequenceEqual(["a"]) && query == "Be", "rejected controlled selection preserves the query and current tags");
        accept = true;
        selected = ["k", "b", "a"];
        query = string.Empty;
        Frame();
        check(Find("Remove 디모이", UiAccessibilityRole.Button).Node.Bounds.X < Find("Remove Beta", UiAccessibilityRole.Button).Node.Bounds.X,
            "external selected-value ordering controls the visual tag order");
        Send("People", UiAccessibilityRole.ComboBox, UiAccessibilityAction.Focus);
        Key(UiKey.Home);
        Key(UiKey.Backspace); Frame();
        check(Find("Remove Alpha", UiAccessibilityRole.Button).Node.Focused,
            "backward navigation follows the authoritative selected order instead of option order");
        Send("People", UiAccessibilityRole.ComboBox, UiAccessibilityAction.Focus);
        Key(UiKey.End);
        Frame(idle with { CharEvents = [new((uint)'Z')] });
        Key(UiKey.Backspace); Frame();
        check(query.Length == 0 && Find("Recipients", UiAccessibilityRole.Edit).Node.Focused,
            "Backspace deleting input text does not transfer focus to a tag");
        selected = [];
        Key(UiKey.Escape); Frame();
        Send("Following", UiAccessibilityRole.Button, UiAccessibilityAction.Focus);
        Frame();
        check(!context.State.HasActiveAnimations, "TagPicker disclosure does not request continuous rendering");
        for (var frame = 0; frame < 12; frame++)
            Frame();
        check(!context.State.HasActiveAnimations && context.State.NextScheduledFrameTimeSeconds is null,
            "empty unfocused TagPicker has no periodic animation schedule after its disclosure settles");
    }
    private static void RunPopupPointerSelection(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        var query = string.Empty;
        string[] selected = ["dimoi"];
        UiSelectOption[] people = [new("dimoi", "디모이"), new("kim", "김민수"), new("park", "박지연"), new("guest", "손님", false)];
        using var picker = Fluent.TagPicker("받는 사람", new UiBinding<string>(() => query, value => query = value),
            new UiBinding<string[]>(() => selected, value => selected = value), people, new(Field : new(Width : 520)));
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(UiInputState? input = null)
        {
            context.NewFrame(new(.016f, new(800, 700), new(1, 1)), input ?? idle, new(0, 0, 800, 700), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding : 0); picker.Render(ui); }));
            using var data = context.GetDrawData();
        }
        void Click(UiRect bounds)
        {
            var point = new UiVector2(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
            Frame(new(point, true, false, true, false, false, false, 0, 0, [], [], default, default));
            Frame(new(point, false, false, false, true, false, false, 0, 0, [], [], default, default));
            Frame();
        }
        Frame();
        foreach (var name in new[] { "김민수", "박지연" })
        {
            var expand = tree.CaptureSnapshot().Elements.Single(element => element.Node.Role == UiAccessibilityRole.Button
                && element.Node.Id.EndsWith("##expand", StringComparison.Ordinal));
            Click(expand.Node.Bounds);
            var option = tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == name && element.Node.Role == UiAccessibilityRole.Option);
            check(picker.IsOpen && !option.Node.Offscreen, "TagPicker actual pointer opens the next unselected suggestion : " + name);
            Click(option.Node.Bounds);
            check(!picker.IsOpen && query.Length == 0 && selected.Contains(name == "김민수" ? "kim" : "park"),
                "TagPicker pointer selection commits,clears and closes without duplicate accessibility IDs : " + name);
            var root = tree.CaptureSnapshot().Elements.Single(element => element.Node.Role == UiAccessibilityRole.ComboBox).Node;
            var options = tree.CaptureSnapshot().Elements.Where(element => element.Node.ParentId == root.ControlledElementId
                && element.Node.Role == UiAccessibilityRole.Option).ToArray();
            check(options.Length == people.Length && options.All(element => element.Node.Offscreen)
                && options.Count(element => element.Node.Selected == true) == selected.Length,
                "TagPicker closed canonical options publish once with the committed selection : " + name);
        }
    }
    private sealed class Screen(Action<UiImmediateContext> draw) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => draw(ui);
    }
}
