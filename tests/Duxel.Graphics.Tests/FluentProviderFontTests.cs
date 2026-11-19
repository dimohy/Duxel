using Duxel.Core;

internal static class FluentProviderFontTests
{
    internal static void Run(Action<bool, string> check)
    {
        var directory = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        var original = new UiTypographyFonts(Path.Combine(directory, "segoeui.ttf"), Path.Combine(directory, "seguisb.ttf"),
            Path.Combine(directory, "segoeuib.ttf"), Path.Combine(directory, "malgun.ttf"), Path.Combine(directory, "malgunbd.ttf"),
            Path.Combine(directory, "malgunbd.ttf")) { Monospace = Path.Combine(directory, "consola.ttf") };
        var local = original with { Regular = original.Monospace!, Semibold = original.Monospace!, Bold = original.Monospace! };
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
        var outside = new Probe();
        using var inherited = Fluent.FluentProvider(inner, UiFluentProviderTheme.Dark);
        using var provider = Fluent.FluentProvider(inherited, new(UiCompiledDesign.Windows11.Theme, fonts : local));
        using var root = Dux.Column(new(MainAxisSize : UiMainAxisSize.Min), provider, outside);
        for (var frame = 0; frame < 5; frame++) Frame(root.Render);
        check(inner.Measured == local && inner.Painted == local && outside.Measured == original && outside.Painted == original,
            "local font roles inherit through palette-only nesting and restore outside during measurement and painting");
        var count = inner.Measurements;
        Frame(root.Render); Frame(root.Render);
        check(inner.Measurements == count, "unchanged scoped font sets reuse retained ancestor measurements");
        var changed = local with { Semibold = original.Semibold };
        provider.Theme = new(UiCompiledDesign.Windows11.Theme, fonts : changed);
        Frame(root.Render);
        check(inner.Measurements > count && inner.Measured == changed && inner.Painted == changed,
            "changing only semibold invalidates retained ancestors even when regular font is identical");
        provider.Theme = UiFluentProviderTheme.Light;
        Frame(root.Render);
        check(inner.Measured == original && inner.Painted == original, "omitted font set restores inherited app fonts");
        inner.Fail = true;
        provider.Theme = new(UiCompiledDesign.Windows11.Theme, fonts : local);
        var threw = false;
        Frame(ui =>
        {
            try { provider.Render(ui); }
            catch (SentinelException) { threw = true; }
            _ = new UiControlContext(ui).MeasureTypography<ObservingTypeface, UiFluentType300>("outside");
            check(ObservingTypeface.Last == original, "font scope restores the parent after a child measurement exception");
        });
        check(threw, "font exception fixture reaches the throwing child");
        using var controlled = new UiTooltip<UiHoverFocusTooltipBehavior, FontSurface>(Fluent.Button("Controlled"), "Font probe",
            new() { ShowDelaySeconds = 0, HideDelaySeconds = 0 }) { Visible = true };
        using var tooltipProvider = Fluent.FluentProvider(controlled, new(UiCompiledDesign.Windows11.Theme, fonts : local));
        FontSurface.Last = null;
        Frame(tooltipProvider.Render); Frame(tooltipProvider.Render);
        check(FontSurface.Last == local, "deferred controlled Tooltip observes the owner's local font set");
        tooltipProvider.Dispose();
        using var automatic = new UiTooltip<UiHoverFocusTooltipBehavior, FontSurface>(Fluent.Button("Automatic"), "Font probe",
            new() { ShowDelaySeconds = 0, HideDelaySeconds = 0 });
        using var automaticProvider = Fluent.FluentProvider(automatic, new(UiCompiledDesign.Windows11.Theme, fonts : local));
        FontSurface.Last = null;
        Frame(automaticProvider.Render);
        var hover = idle with { MousePosition = new(12, 12) };
        Frame(automaticProvider.Render, hover);
        context.State.AdvanceTime(.5f);
        Frame(automaticProvider.Render, hover);
        check(FontSurface.Last == local, "automatic Tooltip handover carries the owner's local font set");
        var rejected = false;
        try { _ = new UiFluentProviderTheme(UiCompiledDesign.Windows11.Theme, fonts : local with { Bold = " " }); }
        catch (ArgumentException) { rejected = true; }
        check(rejected, "Provider rejects empty required font paths before rendering");
    }
    internal static void RunOverrides(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        context.SetDesign(UiCompiledDesign.Windows11Dark);
        ControlFontFixture.Configure(context);
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "consola.ttf");
        var local = new UiTypographyFonts(path, path, path);
        var inner = new Probe();
        var outside = new Probe();
        using var provider = Fluent.FluentProvider(inner);
        using var root = Dux.Column(new(MainAxisSize : UiMainAxisSize.Min), provider, outside);
        provider.Fonts = local;
        var paletteKept = false;
        void Frame()
        {
            context.NewFrame(new(.016f, new(600, 500), new(1, 1)),
                new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default),
                new(0, 0, 600, 500), UiTextSettings.Default);
            context.Render(new Screen(ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding : 0);
                var before = ui.ControlFontEnvironment;
                var palette = new UiControlContext(ui).Theme;
                root.Render(ui);
                paletteKept = new UiControlContext(ui).Theme.Text.Equals(palette.Text)
                    && new UiControlContext(ui).Theme.WindowBg.Equals(palette.WindowBg) && ui.ControlFontEnvironment == before;
            }));
            using var data = context.GetDrawData();
        }
        Frame(); Frame();
        check(inner.Measured == local && inner.Painted == local && outside.Painted != local && paletteKept,
            "font-only Provider applies local text without replacing inherited palette or leaking fonts");
        provider.Theme = new(UiCompiledDesign.Windows11.Theme, fonts : outside.Painted);
        Frame();
        check(inner.Painted == local && paletteKept, "explicit Provider Fonts override themed fonts and restore parent state");
        provider.Fonts = null; Frame();
        check(inner.Painted == outside.Painted && paletteKept, "clearing Provider Fonts restores the theme font set");
        var invalid = false;
        try { provider.Fonts = local with { Bold = " " }; }
        catch (ArgumentException) { invalid = true; }
        check(invalid && provider.Fonts is null, "invalid font-only overrides fail before changing the live Provider");
    }
    internal static void RunGallery(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        context.SetDesign(UiCompiledDesign.Windows11);
        ControlFontFixture.Configure(context);
        using var tree = new UiAccessibilityTree();
        context.SetAccessibilityTree(tree);
        using var page = new Duxel.Gallery.FluentProviderPage(new Duxel.App.DuxelFontOptions().Typography);
        using var frame = new Duxel.Gallery.GalleryPageFrame(page);
        void Frame()
        {
            context.NewFrame(new(.016f, new(1280, 6000), new(1, 1)),
                new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default),
                new(0, 0, 1280, 6000), UiTextSettings.Default);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding : 0); frame.Render(ui); }));
            using var draw = context.GetDrawData();
        }
        Frame(); Frame();
        check(page.Samples.Count == 5 && page.Samples[3].Source.Contains("fonts : localFonts", StringComparison.Ordinal),
            "Provider Gallery exposes a fourth local font example with adjacent source");
        var node = tree.CaptureSnapshot().Elements.Single(element => element.Node.Name == "영역 글꼴 전환"
            && element.Node.Role == UiAccessibilityRole.Button);
        check(tree.RequestAction(node.Node.Id, node.RuntimeId, UiAccessibilityAction.Invoke, wait : false) == UiAccessibilityResult.Success,
            "Gallery local font option accepts an actual accessibility action");
        Frame(); Frame();
        check(page.Samples[3].Source.Contains("localTheme = null", StringComparison.Ordinal),
            "local font source follows the actual inherited-font setting");
    }
    private readonly struct ObservingTypeface : IUiTypographyTypeface
    {
        internal static UiTypographyFonts? Last;
        public static bool CanRetainMeasurement => true;
        public static UiTypographyFontPaths Select(UiTypographyFonts fonts)
        {
            Last = fonts;
            return UiSemiboldTypeface.Select(fonts);
        }
    }
    private sealed class Probe : UiControlWidget
    {
        internal UiTypographyFonts? Measured, Painted;
        internal int Measurements;
        internal bool Fail;
        protected override bool CanRetainMeasurement => true;
        protected override UiVector2 MeasureContent(UiControlContext context)
        {
            if (Fail) throw new SentinelException();
            Measurements++;
            var size = context.MeasureTypography<ObservingTypeface, UiFluentType300>("Scoped font sample");
            Measured = ObservingTypeface.Last;
            return size;
        }
        protected override void RenderControl(UiImmediateContext ui)
        {
            new UiControlContext(ui).Typography<ObservingTypeface, UiFluentType300>("Scoped font sample");
            Painted = ObservingTypeface.Last;
        }
    }
    private readonly struct FontSurface : IUiTooltipPresentation
    {
        internal static UiTypographyFonts? Last;
        public static UiPopupLayout Arrange(UiControlContext context, string text, UiRect target, UiRect viewport,
            UiTooltipOptions options)
        {
            _ = context.MeasureTypography<ObservingTypeface, UiFluentType300>(text);
            Last = ObservingTypeface.Last;
            return new(new(20, 80, 200, 32), UiPopupPosition.Below);
        }
        public static void Paint(UiControlContext context, string text, UiPopupLayout layout)
        {
            context.Typography<ObservingTypeface, UiFluentType300>(text);
            Last = ObservingTypeface.Last;
        }
    }
    private sealed class SentinelException : Exception;
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
