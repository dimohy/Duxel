using Duxel.Core;

internal static class TreePresentationTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        context.SetDesign(UiCompiledDesign.Windows11Dark); ControlFontFixture.Configure(context);
        using var accessibility = new UiAccessibilityTree(); context.SetAccessibilityTree(accessibility);
        var expanded = new UiState<string[]>(["root"]);
        var selected = new UiState<string?>("a");
        UiTreeItem[] Items() => [new("root", null, "Root", Fluent.Body1("Root")),
            new("a", "root", "Alpha", Fluent.Body1("Alpha")),
            new("b1", "root", "Beta file", Fluent.Body1("Beta file")),
            new("b2", "root", "Beta folder", Fluent.Body1("Beta folder")),
            new("ko", "root", "디모이", Fluent.Body1("디모이")),
            new("other", null, "Other", Fluent.Body1("Other")),
            new("hidden", "other", "Beta hidden", Fluent.Body1("Beta hidden"))];
        using var tree = new UiTree<UiFluentTreePresentation>("Public Tree", expanded.Binding, selected.Binding, Items(), new(Width : 340));
        UiWidget current = tree;
        UiControlContext colors = default;
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(UiInputState? input = null, float delta = .016f)
        {
            context.NewFrame(new(delta, new(640, 480), new(1, 1)), input ?? idle, new(0, 0, 640, 480), UiTextSettings.Default);
            context.Render(new Screen(ui => { colors = new(ui); ui.EnableRootViewportContentLayout(contentPadding : 0); current.Render(ui); }));
            using var draw = context.GetDrawData();
        }
        UiAccessibilityElement Find(string name, UiAccessibilityRole role = UiAccessibilityRole.TreeItem)
            => accessibility.CaptureSnapshot().Elements.Single(item => item.Node.Role == role && item.Node.Name == name);
        void Focus(string name)
        {
            var item = Find(name);
            _ = accessibility.RequestAction(item.Node.Id, item.RuntimeId, UiAccessibilityAction.Focus, wait : false);
            Frame();
        }
        void Type(string text) => Frame(idle with { CharEvents = text.EnumerateRunes().Select(rune => new UiCharEvent((uint)rune.Value)).ToArray() });
        void Key(UiKey key) => Frame(idle with { KeyEvents = [new(key, true, default)] });
        Frame();
        check(Find("Alpha").Node.Bounds.Height == 32 && Find("Public Tree", UiAccessibilityRole.Tree).Node.Bounds.Width == 340,
            "public generic Tree uses the Fluent medium metrics and requested width");
        var identity = Find("Alpha").RuntimeId;
        tree.Options = tree.Options with { Size = UiTreeSize.Small, Width = 280, RightToLeft = true };
        Frame();
        check(Find("Alpha").Node.Bounds.Height == 24 && Find("Public Tree", UiAccessibilityRole.Tree).Node.Bounds.Width == 280
            && Find("Alpha").RuntimeId == identity && selected.Value == "a" && expanded.Value.SequenceEqual(["root"]),
            "changing public size/width/RTL options invalidates row measurement while retaining identity and bindings");
        tree.Options = tree.Options with { Size = UiTreeSize.Medium, RightToLeft = false };
        Frame();
        check(Find("Alpha").Node.Bounds.Height == 32, "switching back to medium restores actual row height");
        var invalid = false;
        try { tree.Options = tree.Options with { Size = (UiTreeSize)99 }; } catch (ArgumentOutOfRangeException) { invalid = true; }
        check(invalid && tree.Options.Size == UiTreeSize.Medium, "invalid public options are rejected without mutating the current options");
        invalid = false;
        try { using var rejected = new UiTree<UiFluentTreePresentation>("Null selection", expanded.Binding, (UiBinding<string?>)null!, []); }
        catch (ArgumentNullException) { invalid = true; }
        check(invalid, "a null selected binding cannot silently become informational mode");
        Focus("Alpha"); Type("b");
        check(Find("Beta file").Node.Focused && selected.Value == "a", "committed text searches visible Tree labels without changing selection");
        Type("b");
        check(Find("Beta folder").Node.Focused, "repeated prefix characters cycle between matching visible rows");
        Type("b");
        check(Find("Beta file").Node.Focused && !accessibility.CaptureSnapshot().Elements.Any(item => item.Node.Name == "Beta hidden"),
            "Tree name search wraps over visible matches without expanding hidden branches");
        Type("e"); Type("t"); Type("a");
        Frame(idle with { KeyEvents = [new(UiKey.Space, true, default)], CharEvents = [new((uint)' ')] });
        Type("fo");
        check(Find("Beta folder").Node.Focused && selected.Value == "a", "spaces inside an active name search do not toggle the focused selector");
        Key(UiKey.Home); Type("디");
        check(Find("디모이").Node.Focused && selected.Value == "a", "navigation resets the prefix and Korean committed text participates in name search");
        Frame(delta : 1.1f); Type("a");
        check(Find("Alpha").Node.Focused, "an expired search starts a fresh prefix without requiring a timer frame");
        check(UiFluentTreePresentation.Colors(colors, UiButtonVisualState.Hover, UiTreeAppearance.Subtle).Background == new UiColor(56, 56, 56),
            "dark subtle Tree hover uses its authored Tree token rather than the Button background");
        check(UiFluentTreePresentation.Colors(colors, UiButtonVisualState.Hover, UiTreeAppearance.SubtleAlpha).Background == new UiColor(36, 36, 36, 204),
            "dark subtle-alpha hover preserves the authored translucent token");
        check(UiFluentTreePresentation.Colors(colors, UiButtonVisualState.Pressed, UiTreeAppearance.Transparent).Background == default,
            "transparent Tree appearance does not introduce a pressed background");
        context.SetDesign(UiCompiledDesign.Windows11);
        Frame();
        check(UiFluentTreePresentation.Colors(colors, UiButtonVisualState.Hover, UiTreeAppearance.Subtle).Background == new UiColor(245, 245, 245)
            && UiFluentTreePresentation.Colors(colors, UiButtonVisualState.Hover, UiTreeAppearance.SubtleAlpha).Background == new UiColor(255, 255, 255, 179),
            "light Tree subtle and subtle-alpha appearances resolve their independent authored tokens");
        var leaves = new UiState<string[]>(["a"]);
        using var custom = new UiTree<CustomPresentation>("Custom Tree", new UiState<string[]>(["root"]).Binding, leaves.Binding, Items(), new(Width : 300));
        current = custom; CustomPresentation.Rows = CustomPresentation.Selectors = 0; Frame();
        check(Find("Root").Node.Bounds.Height == 44 && Find("Alpha").Node.Bounds.Height == 44
            && CustomPresentation.Rows == 6 && CustomPresentation.Selectors == 6,
            "a custom static presentation supplies row metrics and both immediate/deferred paint hooks");
        check(Find("Root").Node.ToggleState == UiAccessibilityToggleState.Indeterminate,
            "the public multiple-selection overload retains cascading mixed-state semantics");
        custom.Options = custom.Options with { Size = UiTreeSize.Small }; Frame();
        check(Find("Alpha").Node.Bounds.Height == 36, "custom presentation metrics are remeasured after option changes");

        var pointerExpansion = new UiState<string[]>([]);
        var pointerSelection = new UiState<string?>(null);
        var rowInvocations = 0;
        var childInvocations = 0;
        using var rowPress = new UiTree<PressRow, UiFluentTreePresentation>("Row press", pointerExpansion.Binding, pointerSelection.Binding,
        [
            new("root", null, "Press root", Fluent.Button("Child action", () => childInvocations++), _ => rowInvocations++),
            new("child", "root", "Press leaf")
        ], new UiTreeOptions(Width: 340, Navigation: true) { RowActions = true });
        current = rowPress;
        Frame();
        UiVector2 Point(UiRect bounds, float offset) => new(bounds.X + offset, bounds.Y + bounds.Height / 2);
        void Pointer(UiVector2 point, bool press, bool release) => Frame(idle with
        {
            MousePosition = point,
            LeftMouseDown = press && !release,
            LeftMousePressed = press,
            LeftMouseReleased = release
        });
        var pressRoot = Find("Press root", UiAccessibilityRole.TreeGridRow);
        var rowBounds = pressRoot.Node.Bounds;
        var row = Point(rowBounds, rowBounds.Width - 2);
        var chevron = Point(rowBounds, rowPress.State.Metrics.ChevronWidth / 2);
        var selector = Point(rowBounds, rowPress.State.Metrics.ChevronWidth + rowPress.State.Metrics.SelectorWidth / 2);
        Pointer(row, press: true, release: false);
        check(rowInvocations == 1 && pointerSelection.Value == "root" && pointerExpansion.Value.Length == 0,
            "Tree press row selects and invokes on down without changing disclosure");
        Pointer(chevron, press: false, release: true);
        check(rowInvocations == 1 && pointerExpansion.Value.Length == 0,
            "Tree crossing from press row to release disclosure does not activate a second part");
        Pointer(chevron, press: true, release: false);
        check(pointerExpansion.Value.Length == 0, "Tree release disclosure waits for pointer release");
        Pointer(row, press: false, release: true);
        check(rowInvocations == 1 && pointerExpansion.Value.Length == 0,
            "Tree crossing from release disclosure to press row does not activate either part");
        Pointer(chevron, press: true, release: true);
        Frame();
        check(pointerExpansion.Value.SequenceEqual(["root"]) && rowInvocations == 1
            && Find("Press leaf", UiAccessibilityRole.TreeGridRow).Node.ParentId == pressRoot.Node.Id,
            "Tree release disclosure opens exactly once for same-frame edges and publishes its child row");
        var childBounds = Find("Child action", UiAccessibilityRole.Button).Node.Bounds;
        var child = Point(childBounds, childBounds.Width / 2);
        Pointer(child, press: true, release: true);
        check(childInvocations == 1 && rowInvocations == 1 && Find("Child action", UiAccessibilityRole.Button).Node.Focused,
            "Tree child owns its complete pointer click with the press-row policy");
        Pointer(selector, press: true, release: false);
        check(pointerSelection.Value == "root", "Tree selector still waits for release");
        Pointer(selector, press: false, release: true);
        check(pointerSelection.Value is null && rowInvocations == 1, "Tree selector toggles without invoking the press row");
        check(accessibility.RequestAction(pressRoot.Node.Id, pressRoot.RuntimeId, UiAccessibilityAction.Collapse, wait: false) == UiAccessibilityResult.Success,
            "Tree queues native collapse independently of pointer activation");
        Frame();
        check(pointerExpansion.Value.Length == 0 && rowInvocations == 1, "Tree applies requested native collapse");
        Key(UiKey.Enter);
        check(rowInvocations == 2 && pointerSelection.Value == "root", "Tree Enter still selects and invokes the focused row");

        var rtlExpanded = new UiState<string[]>([]);
        var rtlInvocations = 0;
        using var disclosurePress = new UiTree<PressDisclosure, CustomPresentation>("RTL disclosure press", rtlExpanded.Binding,
        [
            new("root", null, "RTL root", Fluent.Body1("RTL root"), _ => rtlInvocations++),
            new("child", "root", "RTL leaf", Fluent.Body1("RTL leaf"))
        ], new(Width: 300, RightToLeft: true, Navigation: true));
        current = disclosurePress;
        Frame();
        var rtlBounds = Find("RTL root").Node.Bounds;
        var rtlChevron = Point(rtlBounds, rtlBounds.Width - disclosurePress.State.Metrics.ChevronWidth / 2);
        var rtlRow = Point(rtlBounds, 2);
        Pointer(rtlChevron, press: true, release: false);
        check(rtlExpanded.Value.SequenceEqual(["root"]) && rtlInvocations == 0 && rtlBounds.Height == 44,
            "Tree disclosure press follows the RTL chevron slot and uses independent custom presentation metrics");
        Pointer(rtlRow, press: false, release: true);
        check(rtlExpanded.Value.SequenceEqual(["root"]) && rtlInvocations == 0,
            "Tree disclosure press cannot invoke a release row when dragged across it");
        Pointer(rtlRow, press: true, release: false);
        check(rtlInvocations == 0, "Tree row release waits with the press disclosure policy");
        Pointer(rtlRow, press: false, release: true);
        check(rtlInvocations == 1 && rtlExpanded.Value.SequenceEqual(["root"]), "Tree releases the row command independently of disclosure");

        using var broken = new UiTree<InvalidPresentation>("Invalid metrics", new UiState<string[]>([]).Binding, Items());
        current = broken; invalid = false;
        try { Frame(); } catch (InvalidOperationException exception) when (exception.Message.StartsWith("Tree presentation metrics", StringComparison.Ordinal)) { invalid = true; }
        check(invalid, "invalid custom metrics fail at the shared layout boundary");
    }

    private readonly struct PressRow : IUiTreeBehavior
    {
        public static UiButtonActivation RowPointerActivation => UiButtonActivation.Press;
    }
    private readonly struct PressDisclosure : IUiTreeBehavior
    {
        public static UiButtonActivation DisclosurePointerActivation => UiButtonActivation.Press;
    }
    private readonly struct CustomPresentation : IUiTreePresentation
    {
        internal static int Rows, Selectors;
        public static bool CanRetainMeasurement => true;
        public static UiTreeMetrics Measure(UiControlContext context, UiTreeOptions options)
            => new(options.Size == UiTreeSize.Small ? 36 : 44, 16, 30, 20, 3);
        public static void PaintRow(UiControlContext context, UiRect bounds, UiRect chevron, UiButtonInteraction interaction,
            bool branch, bool expanded, bool selected, UiTreeOptions options) { Rows++; }
        public static void PaintSelector(UiControlContext context, UiRect bounds, UiButtonInteraction interaction,
            bool? value, bool multiple, UiTreeOptions options) { Selectors++; }
        public static void PaintFocus(UiControlContext context, UiRect bounds, UiTreeOptions options) { }
    }
    private readonly struct InvalidPresentation : IUiTreePresentation
    {
        public static UiTreeMetrics Measure(UiControlContext context, UiTreeOptions options) => new(float.NaN, 12);
        public static void PaintRow(UiControlContext context, UiRect bounds, UiRect chevron, UiButtonInteraction interaction,
            bool branch, bool expanded, bool selected, UiTreeOptions options) { }
        public static void PaintSelector(UiControlContext context, UiRect bounds, UiButtonInteraction interaction,
            bool? value, bool multiple, UiTreeOptions options) { }
        public static void PaintFocus(UiControlContext context, UiRect bounds, UiTreeOptions options) { }
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    { public override void Render(UiImmediateContext ui) => render(ui); }
}
