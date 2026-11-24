using Duxel.Core;

internal static class TagPickerTests
{
    internal static void Run(Action<bool, string> check)
    {
        RunPopupPointerSelection(check);
        RunSecondaryAction(check, rightToLeft : false);
        RunSecondaryAction(check, rightToLeft : true);
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

    private static void RunSecondaryAction(Action<bool, string> check, bool rightToLeft)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        using var tree = new UiAccessibilityTree();
        ControlFontFixture.Configure(context);
        context.SetAccessibilityTree(tree);
        var query = string.Empty;
        string[] selected = ["a", "b", "c"];
        var actions = 0;
        UiSelectOption[] items = [new("a", "Alex Morgan"), new("b", "Cameron Lee"), new("c", "Jordan Patel"), new("d", "Available")];
        using var picker = Fluent.TagPicker("Recipients", new UiBinding<string>(() => query, value => query = value),
            new UiBinding<string[]>(() => selected, value => selected = value), items,
            new(Field : new(Width : 290, RightToLeft : rightToLeft))
            {
                SecondaryAction = Fluent.Button("Clear", () => actions++)
            });
        using var following = Fluent.Button("Following", () => { });
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var disabled = false;
        var layout = new Screen(ui =>
        {
            ui.EnableRootViewportContentLayout(contentPadding : 0);
            ui.BeginDisabled(disabled);
            try { picker.Render(ui); }
            finally { ui.EndDisabled(); }
            following.Render(ui);
        });
        var direction = rightToLeft ? "RTL" : "LTR";

        void Frame(UiInputState? input = null)
        {
            context.NewFrame(new(.016f, new(640, 700), new(1, 1)), input ?? idle, new(0, 0, 640, 700), UiTextSettings.Default);
            context.Render(layout);
            using var data = context.GetDrawData();
        }
        UiAccessibilityElement[] Snapshot() => tree.CaptureSnapshot().Elements.ToArray();
        UiAccessibilityNode Find(string name) => Snapshot().Single(element => element.Node.Name == name).Node;
        void Key(UiKey key)
        {
            Frame(idle with { KeyEvents = [new(key, true, default)] });
            Frame(idle with { KeyEvents = [new(key, false, default)] });
        }
        void Click(UiRect bounds)
        {
            var point = new UiVector2(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
            Frame(idle with { MousePosition = point, LeftMouseDown = true, LeftMousePressed = true });
            Frame(idle with { MousePosition = point, LeftMouseReleased = true });
            Frame();
        }

        Frame();
        var nodes = Snapshot();
        var group = nodes.Single(element => element.Node.Role == UiAccessibilityRole.ComboBox);
        var editor = nodes.Single(element => element.Node.Role == UiAccessibilityRole.Edit).Node;
        var action = Find("Clear");
        var arrow = nodes.Single(element => element.Node.Id.EndsWith("##expand", StringComparison.Ordinal)).Node;
        var tags = nodes.Where(element => element.Node.Name.StartsWith("Remove ", StringComparison.Ordinal)).Select(element => element.Node.Bounds).ToArray();
        bool Before(UiRect from, UiRect to) => rightToLeft ? from.X >= to.X + to.Width : from.X + from.Width <= to.X;
        check(tags.Select(tag => tag.Y).Distinct().Count() > 1 && tags.All(tag => Before(tag, action.Bounds)),
            $"{direction}: wrapped tags reserve the action lane on every row");
        check(Before(editor.Bounds, action.Bounds) && Before(action.Bounds, arrow.Bounds) && !action.Offscreen,
            $"{direction}: the editor, secondary action and chevron follow the reading direction");
        check(action.ParentId == group.Node.Id && MathF.Abs(action.Bounds.Y + action.Bounds.Height / 2
            - (group.Node.Bounds.Y + group.Node.Bounds.Height / 2)) < 1,
            $"{direction}: the action is a child of the combobox and centered in the full field");

        Click(action.Bounds);
        check(actions == 1 && !picker.IsOpen && selected.Length == 3, $"{direction}: the secondary action click does not open the list or change tags");
        check(tree.RequestAction(group.Node.Id, group.RuntimeId, UiAccessibilityAction.Focus, wait : false) == UiAccessibilityResult.Success,
            $"{direction}: the editor accepts focus");
        Frame(); Frame();
        Key(UiKey.Tab);
        check(Find("Clear").Focused, $"{direction}: Tab moves from the editor to the secondary action");
        Key(UiKey.Enter);
        check(actions == 2 && !picker.IsOpen, $"{direction}: Enter invokes the focused action once");
        Key(UiKey.Tab);
        check(Find("Following").Focused, $"{direction}: a second Tab leaves the picker");
        Click(arrow.Bounds);
        check(picker.IsOpen && !Find("Available").Offscreen, $"{direction}: the adjacent chevron still opens suggestions");
        Key(UiKey.Escape);
        disabled = true;
        Frame();
        Click(action.Bounds);
        check(actions == 2 && !Find("Clear").Enabled, $"{direction}: a disabled picker disables its owned action");
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
