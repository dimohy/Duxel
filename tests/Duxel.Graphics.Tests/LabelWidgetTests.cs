using Duxel.Core;
using Duxel.Platform.Windows;

internal static class LabelWidgetTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var native = new WindowsAccessibility(0);
        context.SetAccessibilityTree(native.Tree);
        var textChanges = new List<UiAccessibilityTextChange>();
        var textCommitted = true;
        native.Tree.TextPropertyChanged += change =>
        {
            textCommitted &= native.Tree.TryGet(change.Element.Node.Id, out var current) && current == change.Element;
            if (change.Element.Node.Role != UiAccessibilityRole.Label) textChanges.Add(change);
        };
        var requests = 0;
        context.SetRequestFrameCallback(() => requests++);
        var value = new UiState<string>("Initial");
        using var input = Fluent.Input("Original name", value.Binding, 100, 220);
        using var label = Fluent.Label("User name", input, new(Required : true));
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var before = true;
        var inputDisabled = false;
        UiDrawVertex[] vertices = [];
        void Frame(UiInputState? state = null, Action<UiImmediateContext>? custom = null)
        {
            context.NewFrame(new(1f / 60, new(480, 320), new(1, 1)), state ?? idle, new(0, 0, 480, 320), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding : 0);
                if (custom is not null) { custom(ui); return; }
                void Label() { ui.SetCursorScreenPos(new(10, 10)); label.Render(ui); }
                if (before) Label();
                ui.SetCursorScreenPos(new(10, 70));
                ui.BeginDisabled(inputDisabled);
                input.Render(ui);
                ui.EndDisabled();
                if (!before) Label();
            }));
            using var data = context.GetDrawData();
            vertices = data.DrawLists.SelectMany(list => list.Vertices).ToArray();
        }
        UiAccessibilityElement Element()
        {
            for (var id = native.Tree.Root.First; id is not null;)
            {
                if (!native.Tree.TryGet(id, out var node)) break;
                if (node.Node.Role == UiAccessibilityRole.Edit) return node;
                id = node.Next;
            }
            throw new InvalidOperationException("Input node unavailable.");
        }
        Frame();
        check(Element().Node.Name == "User name" && Element().Node.Required,
            "standalone label publishes associated control name and required metadata without wrapping the control");
        var stable = requests;
        Frame();
        check(requests == stable, "unchanged label does not schedule periodic rendering");
        Frame(idle with { MousePosition = new(20, 20), LeftMousePressed = true, LeftMouseDown = true });
        check(context.State.FocusedId == Element().Node.Id && context.State.ActiveId == Element().Node.Id,
            "label before input focuses and activates that input on pointer press");
        Frame();
        before = false;
        Frame(idle with { MousePosition = new(20, 20), LeftMousePressed = true, LeftMouseDown = true });
        Frame();
        check(context.State.FocusedId == Element().Node.Id && context.State.ActiveId == Element().Node.Id,
            "label after input requests focus for the next event-driven frame");
        label.Options = label.Options with { Disabled = true };
        Frame(idle with { MousePosition = new(20, 20), LeftMousePressed = true, LeftMouseDown = true });
        Frame();
        check(context.State.FocusedId != Element().Node.Id, "disabled label cannot focus its target");
        label.Options = label.Options with { Disabled = false };
        inputDisabled = true;
        Frame(idle with { MousePosition = new(20, 20), LeftMousePressed = true, LeftMouseDown = true });
        Frame();
        inputDisabled = false;
        Frame();
        check(context.State.FocusedId != Element().Node.Id, "a disabled target consumes a label focus request without delaying it until enabled");
        input.AccessibleName = "Explicit name";
        label.Text = "Changed label";
        Frame();
        check(Element().Node.Name == "Explicit name", "explicit target accessibility name takes precedence over its standalone label");
        input.AccessibleName = null;
        textChanges.Clear();
        Frame();
        check(Element().Node.Name == "Changed label", "label text changes update target metadata without changing target identity");
        check(textCommitted && textChanges.Count == 1 && textChanges[0] is
            { Property : UiAccessibilityTextProperty.Name, OldValue : "Explicit name", NewValue : "Changed label" },
            "associated label name changes emit one event after the new target snapshot is published");
        Frame();
        check(textChanges.Count == 1, "unchanged associated labels do not repeat name events");
        Frame(custom : ui =>
        {
            using var longLabel = Fluent.Label("This label wraps onto multiple lines without truncating its full text", options : new(Required : true));
            var wide = longLabel.MeasureConstrained(ui, new(200, 200, 0, 1000));
            var narrow = longLabel.MeasureConstrained(ui, new(80, 80, 0, 1000));
            check(narrow.Y > wide.Y && longLabel.Text.EndsWith("full text", StringComparison.Ordinal),
                "label wraps to parent width and preserves complete source text");
            using var small = Fluent.Label("Text", options : new(Size : UiLabelSize.Small));
            using var large = Fluent.Label("Text", options : new(Size : UiLabelSize.Large));
            check(small.Measure(ui, new(300, 300)).Y == 16 && large.Measure(ui, new(300, 300)).Y == 22,
                "small and large label use the pinned Fluent line heights");
        });
        static bool Throws<T>(Action action) where T : Exception
        {
            try { action(); return false; }
            catch (T) { return true; }
        }
        check(Throws<InvalidOperationException>(() => { using var duplicate = Fluent.Label("Duplicate", input); }),
            "a second live standalone label for one target is rejected");
        check(Throws<ArgumentOutOfRangeException>(() => label.Options = new(Size : (UiLabelSize)99)),
            "unknown label sizes are rejected before mutation");
        using var unsupported = Fluent.Text("Not a focus target");
        check(Throws<ArgumentException>(() => label.Target = unsupported) && ReferenceEquals(label.Target, input),
            "unsupported label targets are rejected while preserving the previous association");
        label.Target = null;
        Frame();
        check(Element().Node.Name == "Original name" && !Element().Node.Required,
            "detaching a label restores the target's own name and required metadata");
        label.Target = input;
        before = false;
        Frame(idle with { MousePosition = new(20, 20), LeftMousePressed = true, LeftMouseDown = true });
        label.Target = null;
        Frame();
        check(context.State.FocusedId != Element().Node.Id, "detaching a label cancels its pending focus request");
        Frame(custom : ui =>
        {
            using var group = Fluent.RadioGroup("Original group", new UiState<string?>(null).Binding,
                new UiRadioOption[] { new("a", "Alpha"), new("b", "Beta") });
            using var groupLabel = Fluent.Label("Group naming", group, new(Required : true));
            groupLabel.Render(ui);
            group.Render(ui);
        });
        var rootId = native.Tree.Root.First!;
        native.Tree.TryGet(rootId, out var rootNode);
        if (rootNode.Node.Role == UiAccessibilityRole.Label) rootId = rootNode.Next!;
        native.Tree.TryGet(rootId, out var groupNode);
        native.Tree.TryGet(groupNode.FirstChild!, out var firstOption);
        check(groupNode.Node.Name == "Group naming" && firstOption.Node.Name == "Alpha",
            "group label names only the primary group and preserves individual option names");
        using (var prepared = Fluent.Label("BBBB"))
        {
            Frame(custom : ui => { ui.SetCursorScreenPos(new(0, 1)); prepared.Render(ui); });
            var expected = vertices;
            prepared.Text = "AAAA";
            using var layout = Dux.SizedBox(Dux.Column(new(Spacing : 0, MainAxisSize : UiMainAxisSize.Min),
                new Mutator(() => prepared.Text = "BBBB"), prepared), 120, 21);
            Frame(custom : layout.Render);
            check(vertices.AsSpan().SequenceEqual(expected),
                "label text changed after parent measurement paints the new glyphs in that same frame");
        }
        label.Target = input;
        label.Dispose();
        Frame(custom : ui => { ui.SetCursorScreenPos(new(10, 70)); input.Render(ui); });
        check(Element().Node.Name == "Original name", "disposing a label unregisters metadata without disposing the borrowed input");
        check(Fluent.ComponentNames.Contains("Label"), "Label factory is exposed through the compiled catalog");
        using var button = Fluent.Button("Original button");
        using var field = Fluent.Field(button);
        field.Width = 300;
        field.Options = new() { Hint = "Original hint" };
        Frame(custom : field.Render);
        textChanges.Clear();
        button.AccessibleName = "Renamed button";
        field.Options = new() { Hint = "Updated hint", ValidationMessage = "Required value" };
        Frame(custom : field.Render);
        check(textCommitted && textChanges.Count == 2 && textChanges[0] is
            { Property : UiAccessibilityTextProperty.Name, OldValue : "Original button", NewValue : "Renamed button" }
            && textChanges[1] is { Property : UiAccessibilityTextProperty.HelpText,
                OldValue : "Original hint", NewValue : "Updated hint Required value" },
            "ordinary controls publish committed Name and field HelpText changes without requiring a Tooltip");
        Frame(custom : field.Render);
        check(textChanges.Count == 2, "unchanged ordinary control text emits no duplicate property events");
        field.Options = default;
        textChanges.Clear();
        Frame(custom : field.Render);
        check(textChanges.Count == 1 && textChanges[0] is
            { Property : UiAccessibilityTextProperty.HelpText, OldValue : "Updated hint Required value", NewValue : "" },
            "removing ordinary field help emits an explicit empty HelpText value");
        LabelRelationshipTests.Run(check);
    }
    private sealed class Screen(Action<UiImmediateContext> action) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => action(ui);
    }
    private sealed class Mutator(Action action) : UiControlWidget
    {
        protected override UiVector2 MeasureContent(UiControlContext context) => new(1, 1);
        protected override void RenderControl(UiImmediateContext ui) { action(); ui.Dummy(new(1, 1)); }
    }
}
