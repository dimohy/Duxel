using Duxel.Core;

internal static class InteractionTagTests
{
    internal static void Run(Action<bool, string> check)
    {
        check(Fluent.ComponentNames.Contains("InteractionTag") && !Fluent.RequiredComponentNames.Contains("InteractionTag"),
            "InteractionTag is exposed as a Tag variant without inflating the top-level component scope");
        var unsupported = false;
        try
        {
            using var rejected = new UiInteractionTag<BasicOnlyPresentation>("Unsupported", () => { });
        }
        catch (NotSupportedException) { unsupported = true; }
        check(unsupported, "InteractionTag rejects a basic-only presentation before layout or drawing");
        check(UiFluentTagPresentation.SecondaryWidth(new(Size: UiTagSize.ExtraSmall)) == 24,
            "Extra-small InteractionTag secondary width includes the pinned icon padding and borders");
        check(UiFluentTagPalette.Resolve(UiFluentColorScheme.Light, UiTagAppearance.Brand, UiButtonVisualState.Rest).Foreground == new UiColor(17, 94, 163)
            && UiFluentTagPalette.Resolve(UiFluentColorScheme.Dark, UiTagAppearance.Brand, UiButtonVisualState.Rest).Foreground == new UiColor(98, 171, 245),
            "Tag brand foreground follows pinned web brand70 and brand110 tokens");
        check(UiFluentTagPalette.Resolve(UiFluentColorScheme.Light, UiTagAppearance.Filled, UiButtonVisualState.Hover, interactive: true).Background == new UiColor(235, 235, 235)
            && UiFluentTagPalette.Resolve(UiFluentColorScheme.Dark, UiTagAppearance.Filled, UiButtonVisualState.Pressed, interactive: true).Background == new UiColor(10, 10, 10),
            "InteractionTag neutral hover and pressed surfaces use the official Background3 aliases");
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        var primary = 0;
        var removed = 0;
        using var tag = Fluent.InteractionTag(new UiTagContent("정##세일", "Details"), () => primary++, () => removed++);
        using var following = Fluent.Button("After interactive tags", () => { });
        UiWidget current = tag;
        var disabled = false;
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(UiInputState? input = null)
        {
            context.NewFrame(new(0, new(640, 480), new(1, 1)), input ?? idle, new(0, 0, 640, 480), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding: 0);
                ui.BeginDisabled(disabled);
                try
                {
                    current.Render(ui);
                }
                finally { ui.EndDisabled(); }
                following.Render(ui);
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
        UiAccessibilityElement Find(string name, UiAccessibilityRole role = UiAccessibilityRole.Button)
            => Elements().Single(e => e.Node.Name == name && e.Node.Role == role);
        void Send(string name, UiAccessibilityAction action)
        {
            var e = Find(name);
            check(tree.RequestAction(e.Node.Id, e.RuntimeId, action, wait: false) == UiAccessibilityResult.Success,
                "InteractionTag accepts native action : " + action + " / " + name);
            Frame();
        }
        void Key(UiKey key, bool down = true) => Frame(idle with
        {
            KeyEvents = [new(key, down, default)]
        });
        void Click(string name)
        {
            var b = Find(name).Node.Bounds;
            var point = new UiVector2(b.X + b.Width / 2, b.Y + b.Height / 2);
            Frame(idle with
            {
                MousePosition = point,
                LeftMouseDown = true,
                LeftMousePressed = true
            });
            Frame(idle with
            {
                MousePosition = point,
                LeftMouseReleased = true
            });
            Frame();
        }
        Frame();
        var root = Find("정##세일, Details", UiAccessibilityRole.Group);
        var main = Find("정##세일, Details");
        var dismiss = Find("Remove 정##세일, Details");
        check(main.Node.ParentId == root.Node.Id && dismiss.Node.ParentId == root.Node.Id
            && dismiss.Node.LabeledBy == main.Node.Id && !root.Node.SupportsInvoke && !root.Node.Focusable,
            "InteractionTag exposes two independent buttons with a primary label relationship under one passive group");
        Send(main.Node.Name, UiAccessibilityAction.Invoke);
        check(primary == 1 && removed == 0, "InteractionTag primary Invoke never dismisses");
        Send(dismiss.Node.Name, UiAccessibilityAction.Invoke);
        check(primary == 1 && removed == 1, "InteractionTag secondary Invoke never runs the primary callback");
        Click(main.Node.Name);
        Click(dismiss.Node.Name);
        check(primary == 2 && removed == 2, "InteractionTag actual pointer regions activate exactly their own callbacks");
        Send(main.Node.Name, UiAccessibilityAction.Focus);
        Key(UiKey.Delete);
        check(primary == 2 && removed == 2, "InteractionTag Delete on the primary action does not dismiss");
        Key(UiKey.Enter);
        check(primary == 3 && removed == 2, "InteractionTag primary Enter invokes only the main action");
        Send(dismiss.Node.Name, UiAccessibilityAction.Focus);
        Key(UiKey.Delete);
        Key(UiKey.Backspace);
        Key(UiKey.Space);
        Key(UiKey.Space, false);
        check(primary == 3 && removed == 5, "InteractionTag secondary Delete Backspace and released Space dismiss once each");
        disabled = true;
        Frame();
        foreach (var name in new[] { main.Node.Name, dismiss.Node.Name })
        {
            var node = Find(name);
            check(tree.RequestAction(node.Node.Id, node.RuntimeId, UiAccessibilityAction.Invoke, wait: false) == UiAccessibilityResult.Disabled,
                "InteractionTag disabled child rejects Invoke : " + name);
        }
        Key(UiKey.Enter);
        check(primary == 3 && removed == 5, "InteractionTag disabled keyboard cannot invoke either action");
        disabled = false;
        tag.Options = new(Size: UiTagSize.Small, Shape: UiTagShape.Circular, RightToLeft: true);
        Frame();
        check(Find(main.Node.Name).Node.Bounds.X > Find(dismiss.Node.Name).Node.Bounds.X
            && Find(dismiss.Node.Name).Node.Bounds.Width == 24,
            "InteractionTag RTL mirrors distinct action allocations with the correct secondary width");
        using var only = Fluent.InteractionTag("Primary only", () => primary++);
        current = only;
        Frame();
        var onlyRoot = Find("Primary only", UiAccessibilityRole.Group);
        check(Elements().Count(e => e.Node.ParentId == onlyRoot.Node.Id) == 1, "InteractionTag without dismissal has exactly one real child action");
        var selected = false;
        var accept = true;
        var writes = 0;
        var toggled = 0;
        using var controlled = Fluent.InteractionTag("Controlled", () => toggled++, () => removed++);
        controlled.Selected = new(() => selected, value => { writes++; if (accept) selected = value; });
        current = controlled;
        Frame();
        check(Find("Controlled").Node.ToggleState == UiAccessibilityToggleState.Off
            && !Find("Controlled").Node.SupportsInvoke && Find("Remove Controlled").Node.ToggleState is null,
            "Only the bound primary exposes Toggle; the secondary remains an independent Invoke button");
        Send("Controlled", UiAccessibilityAction.Toggle);
        check(selected && toggled == 1 && writes == 1 && Find("Controlled").Node.ToggleState == UiAccessibilityToggleState.On,
            "Controlled InteractionTag publishes the accepted value in the activation frame");
        var removedBefore = removed;
        Send("Remove Controlled", UiAccessibilityAction.Invoke);
        check(selected && toggled == 1 && writes == 1 && removed == removedBefore + 1,
            "Secondary dismissal never toggles or writes the independent selected binding");
        accept = false;
        Send("Controlled", UiAccessibilityAction.Toggle);
        check(selected && toggled == 2 && writes == 2 && Find("Controlled").Node.ToggleState == UiAccessibilityToggleState.On,
            "A rejected requested value is read back before native state publication");
        accept = true;
        selected = false;
        Frame();
        check(Find("Controlled").Node.ToggleState == UiAccessibilityToggleState.Off,
            "External selected changes are reflected without requiring a primary action");
        Click("Controlled");
        check(selected && toggled == 3 && writes == 3, "Controlled pointer release writes exactly once");
        disabled = true;
        Frame();
        var disabledPrimary = Find("Controlled");
        check(tree.RequestAction(disabledPrimary.Node.Id, disabledPrimary.RuntimeId, UiAccessibilityAction.Toggle, wait: false) == UiAccessibilityResult.Disabled,
            "Disabled controlled tags reject native Toggle");
        disabled = false;
        controlled.Selected = null;
        controlled.Options = controlled.Options with
        {
            Selected = true
        };
        Frame();
        check(Find("Controlled").Node.ToggleState is null && Find("Controlled").Node.SupportsInvoke,
            "Visual-only emphasis does not invent a native Toggle capability");
        foreach (var scheme in Enum.GetValues<UiFluentColorScheme>())
        {
            var dark = scheme == UiFluentColorScheme.Dark;
            var paint = UiFluentTagPalette.Resolve(scheme, UiTagAppearance.Outline, UiButtonVisualState.Rest, selected: true);
            check(paint.Background == (dark ? new UiColor(17, 94, 163) : new UiColor(15, 108, 189))
                && paint.Foreground == new UiColor(255, 255, 255)
                && paint.Border == (dark ? new UiColor(71, 158, 245) : new UiColor(15, 108, 189)),
                "Selected colors resolve independent pinned brand aliases : " + scheme);
            check(UiFluentTagPalette.Resolve(scheme, UiTagAppearance.Filled, UiButtonVisualState.Pressed, interactive: true, selected: true).Background == new UiColor(12, 59, 94)
                && UiFluentTagPalette.Resolve(scheme, UiTagAppearance.Filled, UiButtonVisualState.Disabled, selected: true)
                == UiFluentTagPalette.Resolve(scheme, UiTagAppearance.Filled, UiButtonVisualState.Disabled),
                "Pressed selection uses brand40 and disabled overrides selection : " + scheme);
        }
        string[] values = ["a", "b", "d"];
        var activated = new List<string>();
        UiSelectOption[] items = [new("a", "Alpha"), new("b", "Beta"), new("d", "Disabled", false)];
        using var group = new UiFluentTagGroup("Interactive group", new(() => values, next => values = next), items, activated: activated.Add);
        current = group;
        Frame();
        Send("Alpha", UiAccessibilityAction.Invoke);
        check(activated.SequenceEqual(new[] { "a" }) && values.SequenceEqual(new[] { "a", "b", "d" }),
            "InteractionTag group primary callback receives the canonical value without changing selection");
        var surviving = Find("Alpha");
        Send("Alpha", UiAccessibilityAction.Focus);
        Key(UiKey.RightArrow);
        check(Find("Remove Alpha").Node.Focused, "InteractionTag group arrow reaches the secondary action of the same value");
        Key(UiKey.RightArrow);
        check(Find("Beta").Node.Focused, "InteractionTag group arrow reaches the following primary action");
        Key(UiKey.RightArrow);
        Key(UiKey.Delete);
        Frame();
        check(values.SequenceEqual(new[] { "a", "d" }) && Find("Remove Alpha").Node.Focused
            && Find("Alpha").RuntimeId == surviving.RuntimeId,
            "InteractionTag group dismissal skips both disabled actions and preserves the surviving primary provider");
        using var noDismiss = new UiFluentTagGroup("Primary-only group", new(() => values, next => values = next), items,
            dismissible: false, activated: activated.Add);
        current = noDismiss;
        Frame();
        Send("Alpha", UiAccessibilityAction.Focus);
        Key(UiKey.Enter);
        check(activated.SequenceEqual(new[] { "a", "a" }) && Elements().All(e => !e.Node.Name.StartsWith("Remove ", StringComparison.Ordinal)),
            "InteractionTag groups retain primary keyboard actions when dismissal is unavailable");
        values = ["a", "b", "d"];
        string[] emphasized = [];
        var emphasisWrites = 0;
        var acceptEmphasis = true;
        activated.Clear();
        using var selectedGroup = new UiFluentTagGroup("Emphasized group", new(() => values, next => values = next), items,
            activated: activated.Add, emphasized: new(() => emphasized, next => { emphasisWrites++; if (acceptEmphasis) emphasized = next; }));
        current = selectedGroup;
        Frame();
        Send("Beta", UiAccessibilityAction.Toggle);
        check(emphasized.SequenceEqual(new[] { "b" }) && values.SequenceEqual(new[] { "a", "b", "d" })
            && activated.SequenceEqual(new[] { "b" }) && Find("Beta").Node.ToggleState == UiAccessibilityToggleState.On,
            "Group primary Toggle changes only independent emphasis and reports the canonical value");
        var alphaRuntime = Find("Alpha").RuntimeId;
        Send("Remove Beta", UiAccessibilityAction.Invoke);
        Frame();
        check(values.SequenceEqual(new[] { "a", "d" }) && emphasized.SequenceEqual(new[] { "b" })
            && emphasisWrites == 1 && Find("Alpha").RuntimeId == alphaRuntime,
            "Group dismissal removes visibility only and retains hidden emphasis without rewriting it");
        values = ["a", "b", "d"];
        Frame();
        check(Find("Beta").Node.ToggleState == UiAccessibilityToggleState.On,
            "Restoring a dismissed value restores its independent emphasis");
        acceptEmphasis = false;
        Send("Beta", UiAccessibilityAction.Toggle);
        check(emphasized.SequenceEqual(new[] { "b" }) && Find("Beta").Node.ToggleState == UiAccessibilityToggleState.On
            && emphasisWrites == 2, "Group rejected emphasis request preserves the authoritative on state");
        emphasized[0] = "a";
        Frame();
        check(Find("Alpha").Node.ToggleState == UiAccessibilityToggleState.On && Find("Beta").Node.ToggleState == UiAccessibilityToggleState.Off,
            "Group emphasis detects direct bound array mutation without replacing the array");
        Send("Beta", UiAccessibilityAction.Focus);
        Key(UiKey.RightArrow);
        check(Find("Remove Beta").Node.Focused, "Emphasis groups preserve independent-action roving focus");
        selectedGroup.TagOptions = new(RightToLeft: true, Size: UiTagSize.Small);
        Frame();
        check(Find("Alpha").Node.Bounds.X > Find("Remove Alpha").Node.Bounds.X && Find("Alpha").Node.ToggleState == UiAccessibilityToggleState.On,
            "Group size and RTL changes preserve the controlled emphasis capability and value");
        check(tree.RequestAction(Find("Disabled").Node.Id, Find("Disabled").RuntimeId, UiAccessibilityAction.Toggle, wait: false) == UiAccessibilityResult.Disabled,
            "Disabled emphasized options reject Toggle");
        using var emphasisOnly = new UiFluentTagGroup("Emphasis without callback or dismissal", new(() => values, next => values = next), items,
            dismissible: false, emphasized: new(() => emphasized, next => emphasized = next));
        current = emphasisOnly;
        Frame();
        Send("Beta", UiAccessibilityAction.Focus);
        Key(UiKey.Enter);
        check(emphasized.SequenceEqual(new[] { "a", "b" }) && Elements().All(e => !e.Node.Name.StartsWith("Remove ", StringComparison.Ordinal)),
            "Emphasis alone creates primary Toggle actions without requiring a callback or dismissal");
        Key(UiKey.Space);
        Key(UiKey.Space, false);
        check(emphasized.SequenceEqual(new[] { "a" }), "Emphasis-only keyboard Space commits one toggle on release");
    }
    private sealed class Screen(Action<UiImmediateContext> draw) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => draw(ui);
    }
    private readonly struct BasicOnlyPresentation : IUiTagPresentation
    {
        public static UiVector2 Measure(UiControlContext context, UiTagContent content, UiTagOptions options, bool dismissible)
            => throw new InvalidOperationException("An unsupported InteractionTag must fail before measurement.");
        public static void Paint(UiControlContext context, UiTagContent content, UiRect bounds, UiTagOptions options,
            UiButtonInteraction interaction, bool dismissible)
            => throw new InvalidOperationException("An unsupported InteractionTag must fail before drawing.");
    }
}
