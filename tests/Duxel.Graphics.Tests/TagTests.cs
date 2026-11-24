using Duxel.Core;

internal static class TagTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        var dismissals = 0;
        using var passive = Fluent.Tag(new UiTagContent("정세일", "읽기 전용"));
        using var simple = Fluent.Tag("Simple tag");
        using var literal = Fluent.Tag("정##세일");
        check(simple.Content.Text == "Simple tag", "Tag supports the concise string factory without a separate content object");
        using var action = Fluent.Tag(new UiTagContent("Action"), () => dismissals++);
        string[] values = ["n", "a", "d"];
        UiSelectOption[] items = [new("a", "Alpha"), new("b", "Beta"), new("n", "New York"), new("d", "Disabled", false)];
        using var group = new UiFluentTagGroup("Selected tags", new(() => values, value => values = value), items);
        var query = string.Empty;
        var showLinked = false;
        using var linked = Fluent.Combobox("Linked tags", new UiBinding<string>(() => query, value => query = value),
            new UiBinding<string[]>(() => values, value => values = value), items);
        using var allocated = Dux.SizedBox(group, 140);
        using var following = Fluent.Button("Following tag group", () => { });
        UiWidget current = passive;
        var disabled = false;
        var followingFirst = false;
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(UiInputState? input = null)
        {
            context.NewFrame(new(0, new(640, 480), new(1, 1)), input ?? idle, new(0, 0, 640, 480), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding: 0);
                if (followingFirst)
                    following.Render(ui);
                ui.BeginDisabled(disabled);
                try
                {
                    current.Render(ui);
                }
                finally { ui.EndDisabled(); }
                if (!followingFirst)
                    following.Render(ui);
                if (showLinked)
                    linked.Render(ui);
            }));
            using var data = context.GetDrawData();
        }
        List<UiAccessibilityElement> Elements()
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
        UiAccessibilityElement Find(string name) => Elements().Single(e => e.Node.Name == name);
        void Send(string name, UiAccessibilityAction operation)
        {
            var node = Find(name);
            check(tree.RequestAction(node.Node.Id, node.RuntimeId, operation, wait: false) == UiAccessibilityResult.Success,
                "Tag accepts native action : " + operation + " / " + name);
            Frame();
        }
        void Key(UiKey key) => Frame(idle with
        {
            KeyEvents = [new(key, true, default)]
        });
        Frame();
        check(Find("정세일, 읽기 전용").Node is { Role: UiAccessibilityRole.Label, Focusable: false, SupportsInvoke: false },
            "Passive Tag exposes complete Korean primary and secondary text without an action");
        current = literal;
        Frame();
        check(Elements().Any(e => e.Node.Name == "정##세일"), "Tag accessibility preserves literal hash markers in its content");
        literal.AccessibleName = "명시##이름";
        Frame();
        check(Elements().Any(e => e.Node.Name == "명시##이름"), "Tag literal content preserves explicit accessible name precedence");
        current = action;
        Frame();
        Send("Remove Action", UiAccessibilityAction.Invoke);
        check(dismissals == 1, "Dismissible Tag invokes its real dismissal callback exactly once");
        Frame();
        check(dismissals == 1, "Idle Tag frames do not repeat dismissal callbacks");
        Send("Remove Action", UiAccessibilityAction.Focus);
        Key(UiKey.Delete);
        check(dismissals == 2, "Focused Tag supports Delete dismissal");
        Key(UiKey.Backspace);
        check(dismissals == 3, "Focused Tag supports Backspace dismissal");
        disabled = true;
        Frame();
        var disabledNode = Find("Remove Action");
        check(tree.RequestAction(disabledNode.Node.Id, disabledNode.RuntimeId, UiAccessibilityAction.Invoke, wait: false) == UiAccessibilityResult.Disabled,
            "Disabled Tag rejects native dismissal");
        Key(UiKey.Delete);
        check(dismissals == 3, "Disabled Tag ignores keyboard dismissal");
        disabled = false;
        var pressDismissals = 0;
        using var pressTag = new UiTag<UiPressButtonBehavior, UiFluentTagPresentation>("Press tag", () => pressDismissals++);
        current = pressTag;
        Frame();
        var pressNode = Find("Remove Press tag");
        var bounds = pressNode.Node.Bounds;
        var point = new UiVector2(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
        Frame(idle with { MousePosition = point, LeftMouseDown = true, LeftMousePressed = true });
        check(pressDismissals == 1 && Find("Remove Press tag").Node.Focused,
            "Press Tag dismisses on pointer down and retains the ordinary focus route");
        Frame(idle with { MousePosition = point, LeftMouseDown = true });
        Frame(idle with { MousePosition = point, LeftMouseReleased = true });
        check(pressDismissals == 1, "Press Tag hold and release do not repeat dismissal");
        Frame(idle with { MousePosition = point, LeftMousePressed = true, LeftMouseReleased = true });
        check(pressDismissals == 2, "Press Tag dismisses once when both edges arrive in one frame");
        Key(UiKey.Delete);
        Send("Remove Press tag", UiAccessibilityAction.Invoke);
        check(pressDismissals == 4, "Press Tag preserves keyboard Delete and accessibility Invoke");
        disabled = true;
        Frame(idle with { MousePosition = point, LeftMousePressed = true, LeftMouseReleased = true });
        check(pressDismissals == 4 && !Find("Remove Press tag").Node.Enabled, "Press Tag ignores pointer input when disabled");
        disabled = false;
        using var pressPassive = new UiTag<UiPressButtonBehavior, UiFluentTagPresentation>("Press policy passive");
        current = pressPassive;
        Frame();
        check(Find("Press policy passive").Node is { Role: UiAccessibilityRole.Label, Focusable: false, SupportsInvoke: false },
            "A Tag without a dismissal callback remains passive with the press policy");
        current = allocated;
        Frame();
        var alpha = Find("Remove Alpha");
        check(Find("Selected tags").Node.Role.ToString() == "Toolbar", "TagGroup exposes the toolbar grouping role");
        check(Elements().Count(e => e.Node.ParentId == Find("Selected tags").Node.Id) == 3
            && Elements().All(e => e.Node.Name != "Remove Beta"),
            "TagGroup projects only canonical selected values without hidden placeholder gaps");
        check(Find("Selected tags").Node.Bounds.Height > 32
            && Find("Remove New York").Node.Bounds.Y >= alpha.Node.Bounds.Y + 40,
            "TagGroup reuses wrap layout with the medium eight-pixel inter-run spacing");
        Send("Remove Alpha", UiAccessibilityAction.Focus);
        Key(UiKey.RightArrow);
        check(Find("Remove New York").Node.Focused, "TagGroup arrow navigation follows selected enabled values");
        Key(UiKey.Tab);
        check(Find("Following tag group").Node.Focused, "TagGroup allows Tab to leave from a later focused tag");
        Frame(idle with
        {
            KeyEvents = [new(UiKey.Tab, true, KeyModifiers.Shift)],
            Modifiers = KeyModifiers.Shift
        });
        Frame();
        check(Find("Remove New York").Node.Focused, "TagGroup remembers the last focused tag when Shift Tab reenters");
        Key(UiKey.RightArrow);
        check(Find("Remove Alpha").Node.Focused, "TagGroup circular navigation skips disabled selected tags");
        Key(UiKey.RightArrow);
        Key(UiKey.Delete);
        Frame();
        check(values.SequenceEqual(new[] { "a", "d" }) && Find("Remove Alpha").Node.Focused
            && Elements().All(e => e.Node.Name != "Remove New York"),
            "TagGroup deletion updates the shared binding, retires the tag and restores adjacent focus");
        check(Find("Remove Alpha").RuntimeId == alpha.RuntimeId, "TagGroup removal preserves surviving provider identity");
        Key(UiKey.Tab);
        check(Find("Following tag group").Node.Focused, "TagGroup has one roving Tab stop and then reaches the following control");
        values[0] = "b";
        Frame();
        check(Elements().Any(e => e.Node.Name == "Remove Beta") && Elements().All(e => e.Node.Name != "Remove Alpha"),
            "TagGroup notices in-place external selection array changes");
        check(tree.RequestAction(alpha.Node.Id, alpha.RuntimeId, UiAccessibilityAction.Invoke, wait: false) != UiAccessibilityResult.Success,
            "TagGroup rejects native requests to a retired tag");
        group.TagOptions = new(RightToLeft: true, DismissLabel: "해제");
        values = ["a", "n"];
        Frame();
        Send("해제 Alpha", UiAccessibilityAction.Focus);
        Key(UiKey.LeftArrow);
        check(Find("해제 New York").Node.Focused, "TagGroup reverses horizontal navigation for RTL");
        Send("해제 Alpha", UiAccessibilityAction.Invoke);
        Frame();
        check(values.SequenceEqual(new[] { "n" }), "TagGroup native dismissal changes only its own canonical value");
        showLinked = true;
        Frame();
        check(linked.SelectedText == "New York" && Find("New York").Node.Selected == true,
            "Tag dismissal is reflected by an editable multi-Combobox sharing the same binding");
        var linkedAlpha = Find("Alpha");
        check(tree.RequestAction(linkedAlpha.Node.Id, linkedAlpha.RuntimeId, UiAccessibilityAction.AddToSelection, wait: false) == UiAccessibilityResult.Success,
            "Linked editable Combobox accepts adding a canonical selected value");
        Frame();
        Frame();
        check(values.SequenceEqual(new[] { "a", "n" }) && Find("해제 Alpha").Node.Role == UiAccessibilityRole.Button,
            "Editable Combobox selection publishes the corresponding tag through the same binding");
        showLinked = false;
        values = [];
        Frame();
        check(Elements().All(e => !e.Node.Name.StartsWith("해제 ", StringComparison.Ordinal))
            && context.State.NextScheduledFrameTimeSeconds is null,
            "Empty TagGroup leaves no stale tag providers or periodic work");
        check(Find("Following tag group").Node.Focused, "Removing the last selected tags transfers focus to the following real control");
        using var readOnly = new UiFluentTagGroup("Readonly tags", new(() => new[] { "a" }, _ => { }), items, dismissible: false);
        current = readOnly;
        Frame();
        check(Find("Alpha").Node.Role == UiAccessibilityRole.Label && !Find("Alpha").Node.Focusable,
            "Readonly TagGroup keeps selected values as passive tags");
        followingFirst = true;
        current = allocated;
        values = ["a"];
        Frame();
        Send("해제 Alpha", UiAccessibilityAction.Focus);
        Key(UiKey.Delete);
        Frame();
        Frame();
        check(values.Length == 0 && Find("Following tag group").Node.Focused,
            "Last-tag removal preserves a valid outside target already rendered before the group");
        InteractionTagTests.Run(check);
        TagMediaTests.Run(check);
    }
    private sealed class Screen(Action<UiImmediateContext> draw) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => draw(ui);
    }
}
