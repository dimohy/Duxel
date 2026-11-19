using Duxel.Core;
using Duxel.Gallery;

internal static class FluentProviderTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        context.SetDesign(UiCompiledDesign.Windows11);
        ControlFontFixture.Configure(context);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        void Frame(Action<UiImmediateContext> render, UiInputState? input = null)
        {
            context.NewFrame(new(.016f, new(1280, 1800), new(1, 1)), input ?? idle, new(0, 0, 1280, 1800), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding : 0); render(ui); }));
            using var draw = context.GetDrawData();
        }
        var inner = new Probe();
        var inherited = new Probe();
        var outside = new Probe();
        var tokens = UiDesignTokens.Windows11 with { ControlCornerRadius = 17 };
        using var light = Fluent.FluentProvider(inner, UiFluentProviderTheme.Light);
        using var dark = Fluent.FluentProvider(Dux.Column(new(MainAxisSize : UiMainAxisSize.Min), inherited, light),
            new(UiCompiledDesign.Windows11Dark.Theme, tokens));
        using var root = Dux.Column(new(MainAxisSize : UiMainAxisSize.Min), dark, outside);
        Frame(root.Render);
        check(inherited.Theme.FluentColorScheme == UiFluentColorScheme.Dark && inner.Theme.FluentColorScheme == UiFluentColorScheme.Light
            && outside.Theme.FluentColorScheme == UiFluentColorScheme.Light,
            "Provider nests local palette overrides and restores the outside theme");
        check(inherited.Tokens.ControlCornerRadius == 17 && inner.Tokens.ControlCornerRadius == 17
            && outside.Tokens == UiDesignTokens.Windows11,
            "nested palette overrides inherit geometry tokens while outside tokens remain unchanged");
        check(inner.MeasuredTokens == inner.Tokens && inherited.MeasuredTokens == inherited.Tokens,
            "Provider measurement and rendering observe the same geometry environment");
        // Retention consent propagates from children after their first arrangement.
        // Settle this four-container fixture before checking steady-state reuse.
        Frame(root.Render); Frame(root.Render); Frame(root.Render);
        var measurements = (inner.Measurements, inherited.Measurements, outside.Measurements);
        Frame(root.Render); Frame(root.Render);
        check(measurements == (inner.Measurements, inherited.Measurements, outside.Measurements),
            "unchanged nested Providers retain child layout measurements");
        light.Theme = null;
        Frame(root.Render);
        check(inner.Theme.FluentColorScheme == UiFluentColorScheme.Dark,
            "null Provider theme inherits the current ancestor rather than a copied app default");
        dark.Theme = new(UiCompiledDesign.Windows11Dark.Theme, tokens with { ControlCornerRadius = 23 });
        Frame(root.Render);
        check(inner.MeasuredTokens.ControlCornerRadius == 23 && inherited.MeasuredTokens.ControlCornerRadius == 23,
            "changed scoped geometry invalidates retained child measurements");
        Frame(ui =>
        {
            ui.PushStyleColor(UiStyleColor.Text, new UiColor(12, 34, 56));
            root.Render(ui);
            check(ui.GetStyle().Text == new UiColor(12, 34, 56), "Provider restores enclosing style-color overrides");
            ui.PopStyleColor();
        });
        var failing = new Probe { Fail = true };
        using var throwing = Fluent.FluentProvider(failing, UiFluentProviderTheme.Dark);
        var caught = false;
        Frame(ui =>
        {
            try { throwing.Render(ui); }
            catch (SentinelException) { caught = true; }
            check(ui.GetStyle().FluentColorScheme == UiFluentColorScheme.Light && ui.GetDesignTokens() == UiDesignTokens.Windows11,
                "Provider restores theme and tokens when a child measurement throws");
        });
        check(caught, "Provider exception test actually reaches the failing child");
        var forced = new UiForcedColorPalette(new(1, 2, 3), new(4, 5, 6), new(7, 8, 9), new(10, 11, 12),
            new(13, 14, 15), new(16, 17, 18), new(19, 20, 21), new(22, 23, 24));
        context.SetTheme(forced.Apply(UiCompiledDesign.Windows11.Theme));
        Frame(root.Render);
        check(inner.Theme.ForcedColors == forced && inner.Theme.Text == forced.CanvasText && inner.Theme.WindowBg == forced.Canvas,
            "system forced colors remain authoritative through local app palettes");
        context.SetTheme(UiCompiledDesign.Windows11.Theme);
        Frame(ui =>
        {
            ui.BeginDisabled();
            root.Render(ui);
            ui.EndDisabled();
        });
        check((inherited.Theme.Text.Rgba >> 24) == (UiFluentProviderTheme.Dark.Palette.Text.Rgba >> 24) / 2,
            "local palettes preserve the enclosing disabled-opacity scope");
        using var tooltip = new UiTooltip<UiHoverFocusTooltipBehavior, ThemeSurface>(Fluent.Button("Scoped Tooltip"), "Theme probe",
            new() { ShowDelaySeconds = 0, HideDelaySeconds = 0 }) { Visible = true };
        using var tooltipProvider = Fluent.FluentProvider(tooltip, new(UiCompiledDesign.Windows11Dark.Theme, tokens));
        ThemeSurface.Paints = 0;
        Frame(tooltipProvider.Render); Frame(tooltipProvider.Render);
        check(ThemeSurface.Paints > 0 && ThemeSurface.Scheme == UiFluentColorScheme.Dark && ThemeSurface.Tokens == tokens,
            "deferred controlled Tooltip painting inherits its Provider palette and tokens");
        tooltipProvider.Dispose();
        using var automatic = new UiTooltip<UiHoverFocusTooltipBehavior, ThemeSurface>(Fluent.Button("Automatic scoped Tooltip"), "Automatic theme probe",
            new() { ShowDelaySeconds = 0, HideDelaySeconds = 0 });
        using var automaticProvider = Fluent.FluentProvider(automatic, new(UiCompiledDesign.Windows11Dark.Theme, tokens));
        ThemeSurface.Paints = 0;
        Frame(automaticProvider.Render);
        var hover = idle with { MousePosition = new(12, 12) };
        Frame(automaticProvider.Render, hover);
        context.State.AdvanceTime(.5f);
        Frame(automaticProvider.Render, hover);
        check(ThemeSurface.Paints > 0 && ThemeSurface.Scheme == UiFluentColorScheme.Dark && ThemeSurface.Tokens == tokens,
            "automatic Tooltip handover also retains the Provider environment");
        var rejected = false;
        try { _ = new UiFluentProviderTheme(UiCompiledDesign.Windows11.Theme, tokens with { ControlCornerRadius = -1 }); }
        catch (ArgumentOutOfRangeException) { rejected = true; }
        check(rejected, "Provider rejects invalid geometry before owning or rendering a subtree");
        check(Fluent.ComponentNames.Contains("FluentProvider") && !Fluent.MissingComponentNames.Contains("FluentProvider"),
            "Provider has a generated public factory and leaves the pending catalog");

        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        using var page = new FluentProviderPage();
        using var pageFrame = new GalleryPageFrame(page);
        Frame(pageFrame.Render); Frame(pageFrame.Render);
        check(page.Samples.Count == 4 && tree.CaptureSnapshot().Elements.Count(element => element.Node.Name == "C# 예제 소스"
            && element.Node.Role == UiAccessibilityRole.Edit) == 4,
            "Provider Gallery owns four live inline source blocks");
        void Invoke(string name)
        {
            var node = tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == name && element.Node.Role == UiAccessibilityRole.Button);
            check(tree.RequestAction(node.Node.Id, node.RuntimeId, UiAccessibilityAction.Invoke, wait : false) == UiAccessibilityResult.Success,
                "Provider Gallery accepts actual option activation : " + name);
            Frame(pageFrame.Render); Frame(pageFrame.Render);
        }
        Invoke("첫 번째 영역 테마 전환");
        check(page.Samples[0].Source.Contains("theme = UiFluentProviderTheme.Dark", StringComparison.Ordinal),
            "Provider Gallery source follows a local theme change");
        Invoke("첫 번째 영역 배경 전환");
        check(page.Samples[0].Source.Contains("paintBackground = false", StringComparison.Ordinal),
            "Provider Gallery source follows background visibility");
        var editor = tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == "첫 번째 영역 입력" && element.Node.Role == UiAccessibilityRole.Edit);
        check(tree.RequestAction(editor.Node.Id, editor.RuntimeId, UiAccessibilityAction.SetValue, "테마 속 입력", wait : false) == UiAccessibilityResult.Success,
            "Provider retains ordinary text input routing");
        Frame(pageFrame.Render); Frame(pageFrame.Render);
        check(page.Samples.Take(3).All(sample => sample.Source.Contains("테마 속 입력", StringComparison.Ordinal)),
            "all theme snippets reflect their shared actual input value");
    }
    internal static void RunRetention(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        context.SetDesign(UiCompiledDesign.Windows11);
        ControlFontFixture.Configure(context);
        var inner = new Probe();
        var inherited = new Probe();
        var outside = new Probe();
        using var light = Fluent.FluentProvider(inner, UiFluentProviderTheme.Light);
        using var dark = Fluent.FluentProvider(Dux.Column(new(MainAxisSize : UiMainAxisSize.Min), inherited, light),
            new(UiCompiledDesign.Windows11Dark.Theme, UiDesignTokens.Windows11 with { ControlCornerRadius = 17 }));
        using var root = Dux.Column(new(MainAxisSize : UiMainAxisSize.Min), dark, outside);
        void Frame()
        {
            context.NewFrame(new(.016f, new(1280, 1800), new(1, 1)),
                new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default),
                new(0, 0, 1280, 1800), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding : 0); root.Render(ui); }));
            using var draw = context.GetDrawData();
        }
        for (var i = 0; i < 4; i++) Frame();
        var before = (inner.Measurements, inherited.Measurements, outside.Measurements);
        Frame(); Frame();
        check(before == (inner.Measurements, inherited.Measurements, outside.Measurements),
            "unchanged nested Providers retain child layout measurements");
    }
    internal static void RunDirection(string output, Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        var value = new UiState<string?>("one");
        var tabs = Fluent.TabList("Tabs", value.Binding, new UiTabOption[] { new("one", "One"), new("two", "Two") });
        var read = new Probe();
        var outside = new Probe();
        var popupBody = new Probe();
        var popup = Fluent.Popover("More", popupBody, new(Width : 200));
        var controlled = new UiTooltip<UiHoverFocusTooltipBehavior, DirectionSurface>(
            Fluent.Button("Tooltip target"), "Tip", new()
            {
                Positioning = new() { Position = UiPopupPosition.BelowStart }, ShowDelaySeconds = 0
            }) { Visible = true };
        var ltr = Fluent.FluentProvider(Fluent.NavigationBar("Local override", "Title",
            leading : Fluent.Button("Local start"), trailing : Fluent.Button("Local end")));
        ltr.Direction = UiFlowDirection.LeftToRight;
        var rtl = Fluent.FluentProvider(Dux.Column(new(MainAxisSize : UiMainAxisSize.Min),
            read, ltr, tabs, controlled, popup), UiFluentProviderTheme.Dark);
        rtl.Direction = UiFlowDirection.RightToLeft;
        using var root = Dux.Column(new(MainAxisSize : UiMainAxisSize.Min), rtl, outside);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        Action<UiImmediateContext> render = root.Render;
        void Frame(UiInputState? input = null, Action<UiDrawData>? inspect = null)
        {
            context.NewFrame(new(.016f, new(1000, 2400), new(1, 1)), input ?? idle, new(0, 0, 1000, 2400), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding : 0); render(ui); }));
            using var draw = context.GetDrawData();
            inspect?.Invoke(draw);
        }
        UiAccessibilityElement Find(string name, UiAccessibilityRole role) =>
            tree.CaptureSnapshot().Elements.Single(e => e.Node.Name == name && e.Node.Role == role);
        Frame();
        check(read.MeasuredRtl && read.PaintedRtl && !outside.MeasuredRtl && !outside.PaintedRtl,
            "Provider direction is consistent in measurement and paint and restores the sibling direction");
        check(Find("Local start", UiAccessibilityRole.Button).Node.Bounds.X
            < Find("Local end", UiAccessibilityRole.Button).Node.Bounds.X,
            "a nested LTR provider overrides RTL for its navigation bar");
        check(Find("One", UiAccessibilityRole.Tab).Node.Bounds.X > Find("Two", UiAccessibilityRole.Tab).Node.Bounds.X,
            "TabList geometry inherits the direction");
        var first = Find("One", UiAccessibilityRole.Tab);
        tree.RequestAction(first.Node.Id, first.RuntimeId, UiAccessibilityAction.Focus, wait : false);
        Frame();
        Frame(idle with { KeyEvents = [new(UiKey.LeftArrow, true, default)] });
        Frame(idle with { KeyEvents = [new(UiKey.LeftArrow, false, default)] });
        check(Find("Two", UiAccessibilityRole.Tab).Node.Focused && value.Value == "one",
            "TabList uses mirrored keyboard navigation and keeps manual selection stable");
        check(DirectionSurface.ArrangedRtl && DirectionSurface.PaintedRtl && DirectionSurface.PositionedRtl,
            "controlled Tooltip captures inherited direction for deferred placement and drawing");
        var before = read.Measurements;
        rtl.Direction = UiFlowDirection.LeftToRight;
        Frame();
        check(read.Measurements > before && !read.MeasuredRtl && !DirectionSurface.PaintedRtl
            && Find("One", UiAccessibilityRole.Tab).Node.Bounds.X < Find("Two", UiAccessibilityRole.Tab).Node.Bounds.X,
            "changing Provider direction rebuilds cached geometry and updates an already-open Tooltip");
        rtl.Direction = UiFlowDirection.RightToLeft;
        controlled.Visible = false;
        popup.Show();
        Frame();
        check(popupBody.PaintedRtl, "owned Popover body is painted in the provider direction");
        popup.Close();
        Frame();

        using var automatic = new UiTooltip<UiHoverFocusTooltipBehavior, DirectionSurface>(Fluent.Button("Automatic target"), "Automatic tip",
            new() { ShowDelaySeconds = 0, Positioning = new() { Position = UiPopupPosition.BelowStart } });
        using var automaticProvider = Fluent.FluentProvider(automatic);
        automaticProvider.Direction = UiFlowDirection.RightToLeft;
        render = automaticProvider.Render;
        Frame(idle with { MousePosition = new(12, 12) });
        check(DirectionSurface.ArrangedRtl && DirectionSurface.PaintedRtl && DirectionSurface.PositionedRtl,
            "automatic Tooltip also carries the provider direction into deferred paint");

        using var page = new FluentProviderPage();
        using var pageFrame = new GalleryPageFrame(page);
        render = pageFrame.Render;
        Frame();
        var marker = Find("배치 방향 바꾸기", UiAccessibilityRole.Button);
        check(!marker.Node.Offscreen && tree.RequestAction(marker.Node.Id, marker.RuntimeId, UiAccessibilityAction.Invoke, wait : false)
            == UiAccessibilityResult.Success, "the Gallery direction switch can be operated through its actual action");
        Frame(); Frame();
        var sample = page.Samples[^1];
        check(sample.Source.Contains("example.Direction = UiFlowDirection.LeftToRight;", StringComparison.Ordinal),
            "the Gallery source follows the active provider direction");
        var volume = Find("알림 음량", UiAccessibilityRole.Slider);
        check(tree.RequestRangeValue(volume.Node.Id, volume.RuntimeId, 65, wait : false) == UiAccessibilityResult.Success,
            "the Gallery Slider exposes a numeric range action");
        Frame(); Frame();
        check(sample.Source.Contains("var volume = new UiState<float>(65f);", StringComparison.Ordinal),
            "the Gallery source updates with the live Slider value");
        File.WriteAllText(Path.Combine(output, "provider-direction-example.cs"),
            "using Duxel.Core;\nUiImmediateContext ui = null!;\n" + sample.Source);

        var query = new UiState<string>(string.Empty);
        var selected = new UiState<string?>(null);
        var expanded = new UiState<string[]>([]);
        var tagQuery = new UiState<string>(string.Empty);
        var tags = new UiState<string[]>(["tag"]);
        var search = new UiState<string>("Current search");
        var radioValue = new UiState<string?>("daily");
        var combo = Fluent.Combobox("Scoped combo", query.Binding, selected.Binding, [new("city", "City A")],
            new(Field : new(ExpandLabel : "Scoped arrow")));
        using var composite = Fluent.FluentProvider(Dux.Column(new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min),
            combo,
            Fluent.Tree("Scoped tree", expanded.Binding, [new("branch", null, "Branch"), new("leaf", "branch", "Leaf")],
                new(Width : 260) { ReducedMotion = true }),
            Fluent.TagPicker("Scoped tags", tagQuery.Binding, tags.Binding, [new("tag", "Token")]),
            Fluent.Toolbar("Scoped tools", [Fluent.Button("First tool"), Fluent.Button("Second tool")]),
            Fluent.SearchBox("Scoped search", search.Binding, options : new(ClearLabel : "Scoped clear")),
            new UiRadioGroup<UiFluentRadioGroupBehavior, UiFluentRadioGroupPresentation<UiRadioHorizontalLayout>>(
                "Scoped frequency", radioValue.Binding,
                new UiRadioOption[] { new("daily", "Daily"), new("weekly", "Weekly") })));
        composite.Direction = UiFlowDirection.RightToLeft;
        render = composite.Render;
        Frame();
        UiAccessibilityResult Act(string name, UiAccessibilityRole role, UiAccessibilityAction action)
        {
            var node = Find(name, role);
            var result = tree.RequestAction(node.Node.Id, node.RuntimeId, action, wait : false);
            Frame();
            return result;
        }
        void Key(UiKey key)
        {
            Frame(idle with { KeyEvents = [new(key, true, default)] });
            Frame(idle with { KeyEvents = [new(key, false, default)] });
        }
        var arrow = Find("Scoped arrow", UiAccessibilityRole.Button).Node;
        var editor = Find("Scoped combo", UiAccessibilityRole.Edit).Node;
        check(arrow.Bounds.X + arrow.Bounds.Width <= editor.Bounds.X,
            "the Combobox expansion button and editor mirror together under the provider");
        check(Act("Scoped arrow", UiAccessibilityRole.Button, UiAccessibilityAction.Invoke) == UiAccessibilityResult.Success
            && combo.IsOpen && !Find("City A", UiAccessibilityRole.Option).Node.Offscreen,
            "the mirrored expansion button opens the real option popup");
        Key(UiKey.DownArrow);
        Key(UiKey.Enter);
        check(selected.Value == "city" && query.Value == "City A" && !combo.IsOpen,
            "the mirrored Combobox preserves keyboard selection and closes its popup");
        check(Act("Branch", UiAccessibilityRole.TreeItem, UiAccessibilityAction.Focus) == UiAccessibilityResult.Success,
            "the tree branch remains a focus target under the provider");
        Key(UiKey.LeftArrow);
        check(expanded.Value.SequenceEqual(["branch"]), "RTL tree expands through the physical Left key");
        Key(UiKey.LeftArrow);
        check(Find("Leaf", UiAccessibilityRole.TreeItem).Node.Focused, "RTL tree moves inward through the physical Left key");
        Key(UiKey.RightArrow);
        check(Find("Branch", UiAccessibilityRole.TreeItem).Node.Focused, "RTL tree returns to its parent through the physical Right key");
        var leaf = Find("Leaf", UiAccessibilityRole.Label).Node;
        var tag = Find("Remove Token", UiAccessibilityRole.Button).Node;
        var tagEditor = Find("Scoped tags", UiAccessibilityRole.Edit).Node;
        check(tag.Bounds.X > tagEditor.Bounds.X, "TagPicker mirrors its selected tokens and editor together");
        Act("Scoped tags", UiAccessibilityRole.Edit, UiAccessibilityAction.Focus);
        Key(UiKey.RightArrow);
        check(Find("Remove Token", UiAccessibilityRole.Button).Node.Focused,
            "the physical Right key moves from an empty RTL editor to its selected token");
        var firstTool = Find("First tool", UiAccessibilityRole.Button).Node;
        var secondTool = Find("Second tool", UiAccessibilityRole.Button).Node;
        check(firstTool.Bounds.X > secondTool.Bounds.X, "RTL toolbar puts the first tool on the right");
        Act("First tool", UiAccessibilityRole.Button, UiAccessibilityAction.Focus);
        Key(UiKey.LeftArrow);
        check(Find("Second tool", UiAccessibilityRole.Button).Node.Focused, "RTL toolbar moves to the second tool through Left");
        Act("Scoped search", UiAccessibilityRole.SearchBox, UiAccessibilityAction.Focus);
        var field = Find("Scoped search", UiAccessibilityRole.SearchBox).Node;
        var leftClear = Find("Scoped clear", UiAccessibilityRole.Button).Node;
        check(leftClear.Bounds.X < field.Bounds.X + field.Bounds.Width / 2, "RTL search puts the clear action on the left");
        check(Find("Daily", UiAccessibilityRole.RadioButton).Node.Bounds.X > Find("Weekly", UiAccessibilityRole.RadioButton).Node.Bounds.X,
            "RTL RadioGroup puts the first option on the right");
        check(Act("Daily", UiAccessibilityRole.RadioButton, UiAccessibilityAction.Focus) == UiAccessibilityResult.Success,
            "RTL RadioGroup exposes the option as a focus target");
        Key(UiKey.LeftArrow);
        check(radioValue.Value == "weekly" && Find("Weekly", UiAccessibilityRole.RadioButton).Node.Focused,
            "the physical Left key selects the next option in RTL RadioGroup");
        Key(UiKey.RightArrow);
        check(radioValue.Value == "daily", "the physical Right key selects the previous option in RTL RadioGroup");
        Act("Scoped search", UiAccessibilityRole.SearchBox, UiAccessibilityAction.Focus);
        composite.Direction = UiFlowDirection.LeftToRight;
        Frame();
        check(Find("Daily", UiAccessibilityRole.RadioButton).Node.Bounds.X < Find("Weekly", UiAccessibilityRole.RadioButton).Node.Bounds.X
            && radioValue.Value == "daily", "changing Provider direction mirrors retained RadioGroup bounds and preserves selection");
        check(Find("Leaf", UiAccessibilityRole.Label).Node.Bounds.X < leaf.Bounds.X
            && Find("Scoped arrow", UiAccessibilityRole.Button).Node.Bounds.X > editor.Bounds.X
            && selected.Value == "city" && expanded.Value.SequenceEqual(["branch"]),
            "switching the scope recalculates placements and retains the selected and expanded values");
        var rightClear = Find("Scoped clear", UiAccessibilityRole.Button).Node;
        check(rightClear.Bounds.X > field.Bounds.X + field.Bounds.Width / 2 && search.Value == "Current search"
            && Find("First tool", UiAccessibilityRole.Button).Node.Bounds.X < Find("Second tool", UiAccessibilityRole.Button).Node.Bounds.X,
            "LTR reorders the toolbar and search adornments while retaining the focused editor and its value");
        check(Act("Scoped clear", UiAccessibilityRole.Button, UiAccessibilityAction.Invoke) == UiAccessibilityResult.Success
            && search.Value.Length == 0 && Find("Scoped search", UiAccessibilityRole.SearchBox).Node.Focused,
            "the mirrored search clear action edits its binding and returns focus");

        var checkboxValue = new UiState<bool>(false);
        var switchValue = new UiState<bool>(false);
        using var toggles = Fluent.FluentProvider(Dux.Column(new(Spacing : 8, MainAxisSize : UiMainAxisSize.Min),
            Fluent.Checkbox("Scoped checkbox", checkboxValue.Binding), Fluent.Switch("Scoped switch", switchValue.Binding)));
        render = toggles.Render;
        (UiRect Checkbox, UiRect Switch) TogglePaint()
        {
            UiRect[] frames = [];
            Frame(inspect : data => frames = data.DrawLists.SelectMany(list => list.RectFilledPrimitives?.AsSpan().ToArray() ?? [])
                .Select(primitive => primitive.Rect).ToArray());
            return (frames.First(rect => rect.Width == 16 && rect.Height == 16),
                frames.First(rect => rect.Width == 40 && rect.Height == 20));
        }
        toggles.Direction = UiFlowDirection.LeftToRight;
        var leading = TogglePaint();
        var checkBounds = Find("Scoped checkbox", UiAccessibilityRole.Checkbox).Node.Bounds;
        var switchBounds = Find("Scoped switch", UiAccessibilityRole.Switch).Node.Bounds;
        toggles.Direction = UiFlowDirection.RightToLeft;
        var trailing = TogglePaint();
        bool Mirrored(UiRect before, UiRect after, UiRect bounds) => MathF.Abs(before.X + after.X + before.Width - 2 * bounds.X - bounds.Width) < .01f
            && before.Y == after.Y;
        check(Mirrored(leading.Checkbox, trailing.Checkbox, checkBounds) && Mirrored(leading.Switch, trailing.Switch, switchBounds),
            "Provider mirrors the Checkbox and Switch indicators inside their unchanged vertical allocations");
        check(Act("Scoped checkbox", UiAccessibilityRole.Checkbox, UiAccessibilityAction.Toggle) == UiAccessibilityResult.Success
            && Act("Scoped switch", UiAccessibilityRole.Switch, UiAccessibilityAction.Toggle) == UiAccessibilityResult.Success
            && checkboxValue.Value && switchValue.Value, "mirrored toggles remain actionable through the published accessibility controls");
        toggles.Direction = UiFlowDirection.LeftToRight;
        Frame();
        check(Find("Scoped checkbox", UiAccessibilityRole.Checkbox).Node.Checked == true
            && Find("Scoped switch", UiAccessibilityRole.Switch).Node.Checked == true,
            "returning to LTR preserves both toggled values");

        var level = new UiState<float>(30);
        var slider = Fluent.Slider("Scoped volume", level.Binding, 0, 100, 15);
        using var sliderScope = Fluent.FluentProvider(slider, options : new(Width : 240), paintBackground : false);
        render = sliderScope.Render;
        UiRect[] SliderPaint()
        {
            UiRect[] rectangles = [];
            Frame(inspect : draw => rectangles = draw.DrawLists
                .SelectMany(list => list.RectFilledPrimitives?.AsSpan().ToArray() ?? [])
                .Select(primitive => primitive.Rect).ToArray());
            return rectangles;
        }
        var leftSlider = SliderPaint();
        var sliderBounds = Find("Scoped volume", UiAccessibilityRole.Slider).Node.Bounds;
        sliderScope.Direction = UiFlowDirection.RightToLeft;
        var rightSlider = SliderPaint();
        var leftMarks = leftSlider.Where(rect => rect.Width == 1 && rect.Height == 4).ToArray();
        var rightMarks = rightSlider.Where(rect => rect.Width == 1 && rect.Height == 4).ToArray();
        check(leftMarks.Length == 6 && rightMarks.Length == 6
            && leftMarks.Zip(rightMarks).All(pair => Mirrored(pair.First, pair.Second, sliderBounds))
            && Mirrored(leftSlider.Single(rect => rect.Width == 12 && rect.Height == 12),
                rightSlider.Single(rect => rect.Width == 12 && rect.Height == 12), sliderBounds)
            && Mirrored(leftSlider.Single(rect => rect.Width == (sliderBounds.Width - 20) * .3f && rect.Height == 4),
                rightSlider.Single(rect => rect.Width == (sliderBounds.Width - 20) * .3f && rect.Height == 4), sliderBounds),
            "Provider mirrors horizontal Slider markers, thumb and filled rail without changing bounds");
        Act("Scoped volume", UiAccessibilityRole.Slider, UiAccessibilityAction.Focus);
        Key(UiKey.LeftArrow);
        check(level.Value == 45, "RTL Slider increases on the physical Left key");
        Key(UiKey.RightArrow);
        check(level.Value == 30, "RTL Slider decreases on the physical Right key");
        Key(UiKey.UpArrow);
        check(level.Value == 45, "RTL Slider increases on Up");
        var leftEnd = new UiVector2(sliderBounds.X + 10, sliderBounds.Y + sliderBounds.Height / 2);
        Frame(idle with { MousePosition = leftEnd, LeftMousePressed = true, LeftMouseDown = true });
        check(level.Value == 90, "RTL pointer press at the left end selects the highest grid value");
        Frame(idle with { MousePosition = leftEnd, LeftMouseReleased = true });
        slider.Options = slider.Options with { Reversed = true };
        Frame(idle with { MousePosition = leftEnd, LeftMousePressed = true, LeftMouseDown = true });
        Frame(idle with { MousePosition = leftEnd, LeftMouseReleased = true });
        check(level.Value == 0, "explicit reversal composes with RTL on the horizontal axis");
        slider.Options = slider.Options with { Reversed = false, Vertical = true };
        var verticalRight = SliderPaint();
        sliderScope.Direction = UiFlowDirection.LeftToRight;
        var verticalLeft = SliderPaint();
        check(verticalRight.SequenceEqual(verticalLeft) && level.Value == 0,
            "switching Provider direction preserves vertical Slider paint and the bound value");
    }

    private readonly struct DirectionSurface : IUiTooltipPresentation
    {
        internal static bool ArrangedRtl, PaintedRtl, PositionedRtl;
        public static UiPopupLayout Arrange(UiControlContext context, string text, UiRect target, UiRect viewport,
            UiTooltipOptions options)
        {
            ArrangedRtl = context.RightToLeft;
            PositionedRtl = options.Positioning.RightToLeft;
            return UiFluentTooltipPresentation.Arrange(context, text, target, viewport, options);
        }
        public static void Paint(UiControlContext context, string text, UiPopupLayout layout)
        {
            PaintedRtl = context.RightToLeft;
            UiFluentTooltipPresentation.Paint(context, text, layout);
        }
    }
    private sealed class SentinelException : Exception;
    private readonly struct ThemeSurface : IUiTooltipPresentation
    {
        internal static int Paints;
        internal static UiFluentColorScheme Scheme;
        internal static UiDesignTokens Tokens;
        public static UiPopupLayout Arrange(UiControlContext context, string text, UiRect target, UiRect viewport,
            UiTooltipOptions options) => new(new(20, 80, 160, 32), UiPopupPosition.Below);
        public static void Paint(UiControlContext context, string text, UiPopupLayout layout)
        {
            Paints++;
            Scheme = context.FluentColorScheme;
            Tokens = context.Tokens;
            context.Frame(layout.Bounds, context.Theme.WindowBg, default, 0, 0);
        }
    }
    private sealed class Probe : UiControlWidget
    {
        internal bool Fail;
        internal UiTheme Theme;
        internal UiDesignTokens Tokens, MeasuredTokens;
        internal int Measurements;
        internal bool MeasuredRtl, PaintedRtl;
        protected override bool CanRetainMeasurement => true;
        protected override UiVector2 MeasureContent(UiControlContext context)
        {
            if (Fail) throw new SentinelException();
            Measurements++;
            MeasuredTokens = context.Tokens;
            MeasuredRtl = context.RightToLeft;
            return new(40 + context.Tokens.ControlCornerRadius, 24);
        }
        protected override void RenderControl(UiImmediateContext ui)
        {
            Theme = ui.GetStyle();
            Tokens = ui.GetDesignTokens();
            PaintedRtl = ui.RightToLeft;
            ui.Dummy(new(40 + Tokens.ControlCornerRadius, 24));
        }
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
