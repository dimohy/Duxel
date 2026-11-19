using Duxel.Core;
using Duxel.Svg;

internal static class InteractionTagRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check,
        UiContext context, Action<UiInputState> setInput, int width, UiFluentColorScheme scheme)
    {
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        using var store = new SvgStore();
        var assets = Path.Combine(AppContext.BaseDirectory, "Assets", "Svg");
        using var icon = new UiSvgIcon(store.Load(Path.Combine(assets, "regular.svg")), store.Load(Path.Combine(assets, "filled.svg")), UiSvgIconColorMode.MultiplyForeground);
        var primary = 0;
        var removed = 0;
        using var tag = Fluent.InteractionTag(new UiTagContent("SVG 부산", Icon: icon.MenuIcon), () => primary++, () => removed++);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var disabled = false;
        UiWidget current = tag;
        void Draw(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            var paint = new UiControlContext(ui);
            paint.Frame(paint.ClipRect, new(30, 70, 90), default, 0, 0);
            ui.SetCursorScreenPos(new(16, 16));
            ui.BeginDisabled(disabled);
            try
            {
                current.Render(ui);
            }
            finally { ui.EndDisabled(); }
        }
        UiAccessibilityElement Find(string name)
        {
            UiAccessibilityElement? result = null;
            void Visit(string? id)
            {
                while (id is not null && tree.TryGet(id, out var e))
                {
                    if (e.Node.Role == UiAccessibilityRole.Button && e.Node.Name == name)
                        result = e;
                    Visit(e.FirstChild);
                    id = e.Next;
                }
            }
            Visit(tree.Root.First);
            return result ?? throw new InvalidOperationException(name);
        }
        static UiVector2 Center(UiRect b) => new(b.X + b.Width / 2, b.Y + b.Height / 2);
        void Pixel(byte[] pixels, UiRect b, UiColor expected, string name)
        {
            var offset = ((int)(b.Y + 2) * width + (int)(b.X + b.Width / 2)) * 4;
            check(pixels[offset] == (byte)expected.Rgba && pixels[offset + 1] == (byte)(expected.Rgba >> 8) && pixels[offset + 2] == (byte)(expected.Rgba >> 16),
                "GPU InteractionTag surface matches pinned tokens : " + name);
        }
        foreach (var size in Enum.GetValues<UiTagSize>())
            foreach (var appearance in Enum.GetValues<UiTagAppearance>())
                foreach (var shape in Enum.GetValues<UiTagShape>())
                {
                    tag.Options = new(size, appearance, shape, RightToLeft: shape == UiTagShape.Circular);
                    var name = $"{size}-{appearance}-{shape}";
                    setInput(idle);
                    var rest = capture(name + "-rest", Draw);
                    check(rest.AsSpan().SequenceEqual(capture(name + "-replay", Draw)), "GPU InteractionTag independent action geometry replays : " + name);
                    var main = Find("SVG 부산");
                    var secondary = Find("Remove SVG 부산");
                    check(secondary.Node.LabeledBy == main.Node.Id && secondary.Node.Bounds.Width == UiFluentTagPresentation.SecondaryWidth(tag.Options),
                        "GPU InteractionTag publishes its real secondary allocation and primary label : " + name);
                    setInput(idle with
                    {
                        MousePosition = Center(main.Node.Bounds)
                    });
                    var hover = capture(name + "-primary-hover", Draw);
                    Pixel(hover, main.Node.Bounds, UiFluentTagPalette.Resolve(scheme, appearance, UiButtonVisualState.Hover, interactive: true).Background, name + " primary hover");
                    setInput(idle with
                    {
                        MousePosition = Center(secondary.Node.Bounds)
                    });
                    var secondaryHover = capture(name + "-secondary-hover", Draw);
                    Pixel(secondaryHover, secondary.Node.Bounds, UiFluentTagPalette.Resolve(scheme, appearance, UiButtonVisualState.Hover, interactive: true, secondary: true).Background, name + " secondary hover");
                }
        tag.Options = new();
        setInput(idle);
        capture("pointer-layout", Draw);
        var mainBounds = Find("SVG 부산").Node.Bounds;
        var dismissBounds = Find("Remove SVG 부산").Node.Bounds;
        setInput(idle with
        {
            MousePosition = Center(mainBounds),
            LeftMouseDown = true,
            LeftMousePressed = true
        });
        var pressed = capture("primary-pressed", Draw);
        Pixel(pressed, mainBounds, UiFluentTagPalette.Resolve(scheme, UiTagAppearance.Filled, UiButtonVisualState.Pressed, interactive: true).Background, "primary pressed");
        check(primary == 0 && removed == 0, "GPU InteractionTag pointer press does not prematurely activate");
        setInput(idle with
        {
            MousePosition = Center(mainBounds),
            LeftMouseReleased = true
        });
        capture("primary-released", Draw);
        check(primary == 1 && removed == 0, "GPU InteractionTag real primary release does not dismiss");
        setInput(idle with
        {
            MousePosition = Center(dismissBounds),
            LeftMouseDown = true,
            LeftMousePressed = true
        });
        capture("secondary-pressed", Draw);
        setInput(idle with
        {
            MousePosition = Center(dismissBounds),
            LeftMouseReleased = true
        });
        capture("secondary-released", Draw);
        check(primary == 1 && removed == 1, "GPU InteractionTag real secondary release does not run the primary callback");
        disabled = true;
        setInput(idle);
        var disabledPixels = capture("disabled", Draw);
        check(disabledPixels.AsSpan().SequenceEqual(capture("disabled-replay", Draw)) && !Find("SVG 부산").Node.Enabled && !Find("Remove SVG 부산").Node.Enabled,
            "GPU InteractionTag disabled surfaces and both native actions are stable");
        disabled = false;
        var selected = true;
        var accept = true;
        tag.Selected = new(() => selected, next => { if (accept) selected = next; });
        foreach (var size in Enum.GetValues<UiTagSize>())
        {
            foreach (var appearance in Enum.GetValues<UiTagAppearance>())
            {
                tag.Options = new(size, appearance, Shape: UiTagShape.Circular, Selected: false);
                setInput(idle);
                var name = "selected-" + size + "-" + appearance;
                var pixels = capture(name, Draw);
                var main = Find("SVG 부산");
                var secondary = Find("Remove SVG 부산");
                var selectedFill = scheme == UiFluentColorScheme.Dark ? new UiColor(17, 94, 163) : new UiColor(15, 108, 189);
                Pixel(pixels, main.Node.Bounds, selectedFill, name + " primary");
                Pixel(pixels, secondary.Node.Bounds, selectedFill, name + " secondary");
                check(main.Node.ToggleState == UiAccessibilityToggleState.On && secondary.Node.ToggleState is null,
                    "GPU selected rendering and primary-only Toggle capability agree : " + name);
                setInput(idle with
                {
                    MousePosition = Center(main.Node.Bounds)
                });
                var hover = capture(name + "-hover", Draw);
                Pixel(hover, main.Node.Bounds, scheme == UiFluentColorScheme.Dark ? new UiColor(15, 108, 189) : new UiColor(17, 94, 163), name + " selected hover");
                Pixel(hover, secondary.Node.Bounds, selectedFill, name + " secondary stays selected at rest");
            }
        }
        tag.Options = new();
        setInput(idle);
        capture("controlled-layout", Draw);
        var toggle = Find("SVG 부산");
        check(tree.RequestAction(toggle.Node.Id, toggle.RuntimeId, UiAccessibilityAction.Toggle, wait: false) == UiAccessibilityResult.Success,
            "GPU controlled tag accepts a native Toggle request");
        var off = capture("controlled-off", Draw);
        Pixel(off, toggle.Node.Bounds, scheme == UiFluentColorScheme.Dark ? new UiColor(20, 20, 20) : new UiColor(245, 245, 245), "controlled off same frame");
        check(!selected && Find("SVG 부산").Node.ToggleState == UiAccessibilityToggleState.Off,
            "GPU pixels and native selected state commit in the same activation frame");
        accept = false;
        tree.RequestAction(toggle.Node.Id, toggle.RuntimeId, UiAccessibilityAction.Toggle, wait: false);
        var rejected = capture("controlled-rejected", Draw);
        check(!selected && off.AsSpan().SequenceEqual(rejected), "GPU rejected selection preserves the authoritative unselected pixels");
        selected = true;
        disabled = true;
        var disabledSelected = capture("disabled-selected", Draw);
        tag.Selected = null;
        tag.Options = new();
        check(disabledSelected.AsSpan().SequenceEqual(capture("disabled-unselected", Draw)), "GPU disabled styling overrides selected emphasis");
        disabled = false;
        string[] visible = ["a", "b"];
        string[] emphasized = [];
        var acceptGroup = true;
        using var group = new UiFluentTagGroup("GPU emphasis group", new(() => visible, next => visible = next),
            [new("a", "Group SVG") { Icon = icon.MenuIcon }, new("b", "Group Beta")],
            emphasized: new(() => emphasized, next => { if (acceptGroup) emphasized = next; }));
        current = group;
        setInput(idle);
        capture("group-layout", Draw);
        var groupPrimary = Find("Group SVG");
        tree.RequestAction(groupPrimary.Node.Id, groupPrimary.RuntimeId, UiAccessibilityAction.Toggle, wait: false);
        var groupOn = capture("group-selected", Draw);
        var groupFill = scheme == UiFluentColorScheme.Dark ? new UiColor(17, 94, 163) : new UiColor(15, 108, 189);
        Pixel(groupOn, Find("Group SVG").Node.Bounds, groupFill, "group same-frame primary selection");
        Pixel(groupOn, Find("Remove Group SVG").Node.Bounds, groupFill, "group same-frame secondary selection");
        check(visible.SequenceEqual(new[] { "a", "b" }) && emphasized.SequenceEqual(new[] { "a" })
            && Find("Group SVG").Node.ToggleState == UiAccessibilityToggleState.On,
            "GPU group primary selection preserves visible values and publishes actual emphasis");
        acceptGroup = false;
        tree.RequestAction(groupPrimary.Node.Id, groupPrimary.RuntimeId, UiAccessibilityAction.Toggle, wait: false);
        var rejectedGroup = capture("group-rejected", Draw);
        check(groupOn.AsSpan().SequenceEqual(rejectedGroup), "GPU group rejected selection preserves exact accepted pixels");
        var groupDismiss = Find("Remove Group SVG");
        tree.RequestAction(groupDismiss.Node.Id, groupDismiss.RuntimeId, UiAccessibilityAction.Invoke, wait: false);
        capture("group-dismiss-plan", Draw);
        capture("group-dismissed", Draw);
        check(visible.SequenceEqual(new[] { "b" }) && emphasized.SequenceEqual(new[] { "a" })
            && !tree.TryGet(groupPrimary.Node.Id, out _), "GPU group dismissal retires visibility while retaining independent emphasis");
        visible = ["a", "b"];
        var restoredGroup = capture("group-restored", Draw);
        Pixel(restoredGroup, Find("Group SVG").Node.Bounds, groupFill, "group restored selected state");
        check(Find("Group SVG").Node.ToggleState == UiAccessibilityToggleState.On, "GPU restored group child republishes its retained selection");
        emphasized[0] = "b";
        var mutatedGroup = capture("group-mutated-array", Draw);
        Pixel(mutatedGroup, Find("Group Beta").Node.Bounds, groupFill, "group external array mutation");
        check(Find("Group SVG").Node.ToggleState == UiAccessibilityToggleState.Off && Find("Group Beta").Node.ToggleState == UiAccessibilityToggleState.On,
            "GPU group direct array mutation changes only actual emphasis");
        setInput(idle);
        TagMediaRenderingTests.Run(capture, check, context, setInput, width);
    }
}
