using Duxel.Core;

internal static class LinkWidgetTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var visited = new List<Uri>();
        var destination = new Uri("/details", UriKind.Relative);
        using var widget = new InheritedLink("Details", destination, visited.Add);
        using var outside = Fluent.Button("Outside");
        var settings = UiTextSettings.Default;
        var disabled = false;
        var hidden = false;
        var focused = false;
        var cursor = UiMouseCursor.Arrow;
        void Step(UiInputState? input = null, Action<UiImmediateContext>? render = null)
        {
            context.NewFrame(new(1f / 60, new(320, 240), new(1, 1)), input ?? idle, new(0, 0, 320, 240), settings);
            context.Render(new Screen(render ?? (ui =>
            {
                if (!hidden)
                {
                    ui.SetCursorScreenPos(new(10, 10));
                    ui.BeginDisabled(disabled);
                    widget.Render(ui);
                    ui.EndDisabled();
                    focused = ui.IsItemFocused();
                    cursor = ui.GetMouseCursor();
                }
                ui.SetCursorScreenPos(new(200, 10));
                outside.Render(ui);
            })));
            using var data = context.GetDrawData();
        }
        void Key(UiKey key, KeyModifiers modifiers = default)
        {
            Step(idle with
            {
                KeyEvents = [new(key, true, modifiers)]
            });
            Step(idle with
            {
                KeyEvents = [new(key, false, modifiers)]
            });
        }
        Step();
        Step();
        check(visited.Count == 0, "Link idle frames never navigate");
        Step(idle with
        {
            MousePosition = new(20, 20)
        });
        check(cursor == UiMouseCursor.Hand, "Link requests a hand cursor over its interactive text");
        Step(idle with
        {
            MousePosition = new(20, 20),
            LeftMouseDown = true,
            LeftMousePressed = true
        });
        check(visited.Count == 0, "Link pointer press does not navigate early");
        widget.Label = "Renamed";
        Step(idle with
        {
            MousePosition = new(20, 20),
            LeftMouseReleased = true
        });
        Step();
        check(visited.Count == 1 && visited[0] == destination && widget.Navigations == 1, "generated inherited Link preserves pointer identity across label rename and navigates once");
        Key(UiKey.Space);
        check(visited.Count == 1, "navigation Link does not treat Space as button activation");
        Key(UiKey.Enter);
        check(visited.Count == 2 && visited[1] == destination, "Enter navigates the focused Link exactly once");
        Key(UiKey.Tab);
        check(!focused, "Tab leaves Link as one focus stop");
        Key(UiKey.Enter);
        check(visited.Count == 2, "Enter on another control does not navigate Link");
        Key(UiKey.Tab, KeyModifiers.Shift);
        check(focused, "Shift+Tab returns to Link");
        widget.Destination = new Uri("https://fluent2.microsoft.design/");
        Key(UiKey.Enter);
        check(visited.Count == 3 && visited[^1] == widget.Destination, "Link navigates to its current controlled destination without starting an external process");
        disabled = true;
        Step(idle with
        {
            MousePosition = new(20, 20),
            LeftMouseDown = true,
            LeftMousePressed = true
        });
        Step(idle with
        {
            MousePosition = new(20, 20),
            LeftMouseReleased = true
        });
        Key(UiKey.Enter);
        check(visited.Count == 3, "disabled Link ignores pointer and keyboard navigation");
        disabled = false;
        Step(idle with
        {
            MousePosition = new(20, 20),
            LeftMouseDown = true,
            LeftMousePressed = true
        });
        Step(idle with
        {
            MousePosition = new(300, 200),
            LeftMouseReleased = true
        });
        check(visited.Count == 3, "Link release outside does not navigate");
        Step(idle with
        {
            MousePosition = new(20, 20),
            LeftMouseDown = true,
            LeftMousePressed = true
        });
        hidden = true;
        Step();
        check(context.State.ActiveId is null, "removing Link cancels its owned capture");
        using var content = new UiLinkContent<UiFluentLinkPresentation<UiFluentDefaultLinkAppearance, UiInlineLinkUnderline>>();
        var label = "Long label\nSecond line";
        var width = 100f;
        var dimensions = default(UiVector2);
        void Direct(UiImmediateContext ui)
        {
            ui.SetCursorScreenPos(new(10, 10));
            ui.Link<UiLinkNavigationBehavior, UiFluentLinkPresentation<UiFluentDefaultLinkAppearance, UiInlineLinkUnderline>>(label, destination, visited.Add, content, width);
            dimensions = ui.GetItemRectSize();
        }
        Step(render: Direct);
        var builds = content.LayoutBuildCount;
        Step(render: Direct);
        check(content.LayoutBuildCount == builds && dimensions.Y >= 40, "Link retains multiline text layout across unchanged frames");
        width = 55;
        Step(render: Direct);
        check(content.LayoutBuildCount == builds + 1, "Link width changes invalidate retained wrapping");
        label = "Changed";
        Step(render: Direct);
        check(content.LayoutBuildCount == builds + 2, "Link text changes invalidate retained lines");
        width = 0;
        label = new string('x', 1100);
        Step(render: Direct);
        check(content.RetainedCharacters == 0, "oversized Link text does not create additional retained layout storage");
        label = "Cached again";
        Step(render: Direct);
        check(content.RetainedCharacters > 0, "small Link layout can be admitted after an oversized input");
        var atlasObserverCalls = 0;
        builds = content.LayoutBuildCount;
        settings = settings with
        {
            MissingGlyphObserver = _ => atlasObserverCalls++
        };
        Step(render: Direct);
        Step(render: Direct);
        check(content.LayoutBuildCount == builds && content.RetainedCharacters > 0 && atlasObserverCalls == 0,
            "native Link retains its layout while preserving an unused atlas missing-glyph observer");
        settings = UiTextSettings.Default;
        Step(render: Direct);
        builds = content.LayoutBuildCount;
        var fonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        context.SetTypographyFonts(new(Path.Combine(fonts, "seguisb.ttf"), Path.Combine(fonts, "seguisb.ttf"), Path.Combine(fonts, "segoeuib.ttf")));
        Step(render: Direct);
        check(content.LayoutBuildCount == builds + 1, "actual regular font changes invalidate Link layout");
        ControlFontFixture.Configure(context);
        using var other = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(other);
        builds = content.LayoutBuildCount;
        other.NewFrame(new(1f / 60, new(320, 240), new(1, 1)), idle, new(0, 0, 320, 240), UiTextSettings.Default);
        other.Render(new Screen(Direct));
        other.GetDrawData().ReleasePooled();
        check(content.LayoutBuildCount == builds + 1, "another UI context cannot reuse Link layout through equal numeric generations");
        label = new string('\n', 300);
        Step(render: Direct);
        check(content.RetainedCharacters == 0, "empty Link lines are bounded by retained array slots rather than character count alone");
        using (var oversized = new UiLinkContent<OversizedLinkPresentation>())
        {
            Step(render: ui => ui.Link<UiLinkNavigationBehavior, OversizedLinkPresentation>("Small source", destination, visited.Add, oversized));
            check(oversized.RetainedCharacters == 0, "custom Link presentation cannot retain an oversized replacement single line through a zero derived-character count");
        }
        content.Dispose();
        check(content.RetainedCharacters == 0, "disposing direct Link content releases retained line strings");
        var rejected = false;
        try
        {
            Step(render: Direct);
        }
        catch (ObjectDisposedException) { rejected = true; }
        check(rejected, "disposed Link scratch cannot be reused silently");
        using var standard = Fluent.Link("Docs", destination, _ => { });
        check(standard is UiFluentLink && Fluent.ComponentNames.Contains("Link"), "standard Link catalog factory constructs the generated navigation widget");
        var invalid = false;
        try
        {
            standard.Width = float.NaN;
        }
        catch (ArgumentOutOfRangeException) { invalid = true; }
        check(invalid && standard.Width == 0, "invalid Link width is rejected without changing previous state");
        standard.Dispose();
        invalid = false;
        try
        {
            _ = standard.Navigate;
        }
        catch (ObjectDisposedException) { invalid = true; }
        check(invalid, "Link disposal releases and rejects navigation callback access");
        foreach (var scheme in Enum.GetValues<UiFluentColorScheme>())
            foreach (var state in Enum.GetValues<UiButtonVisualState>())
            {
                var defaultColor = UiFluentLinkPalette.Resolve(scheme, state, UiLinkAppearance.Default);
                var subtle = UiFluentLinkPalette.Resolve(scheme, state, UiLinkAppearance.Subtle);
                var inverted = UiFluentLinkPalette.Resolve(scheme, state, UiLinkAppearance.Inverted);
                var brand = UiFluentLinkPalette.Resolve(scheme, state, UiLinkAppearance.OnBrand);
                check(state == UiButtonVisualState.Disabled ? defaultColor == subtle && subtle == inverted && inverted == brand : defaultColor != subtle,
                    "Link appearance colors separate enabled semantics and share disabled foreground: " + scheme + "/" + state);
            }
    }
    private sealed class Screen(Action<UiImmediateContext> draw) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => draw(ui);
    }
    private sealed class InheritedLink(string label, Uri destination, Action<Uri> navigate) : UiFluentLink(label, destination, navigate)
    {
        internal int Navigations;
        protected override void OnNavigate(Uri destination)
        {
            Navigations++;
            base.OnNavigate(destination);
        }
    }
}

internal readonly struct OversizedLinkPresentation : IUiLinkPresentation
{
    private static readonly string Replacement = new('x', 5000);
    public static bool ReuseLayout => true;
    public static UiControlTypographyScope BeginText(UiControlContext context) => context.BeginTypography<UiRegularTypeface, UiFluentType300>();
    public static UiLinkLayout Measure(UiControlContext context, string text, float width)
    {
        var block = context.MeasureTextBlock(Replacement);
        return new(block.Size, block);
    }
    public static void Paint(UiControlContext context, in UiLinkLayout layout, UiRect bounds, UiButtonInteraction interaction) => context.TypographyTextBlock(layout.Text, bounds, new(255, 255, 255));
}
