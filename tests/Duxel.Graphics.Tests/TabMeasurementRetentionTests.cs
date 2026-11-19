using Duxel.Core;

internal static class TabMeasurementRetentionTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        var settings = UiTextSettings.Default;
        void Frame(Action<UiImmediateContext> render)
        {
            context.NewFrame(new(1f / 60, new(640, 480), new(1, 1)),
                new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default),
                new(0, 0, 640, 480), settings);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding: 0); render(ui); }));
            using var data = context.GetDrawData();
        }
        var value = new UiState<string?>("a");
        using var tabs = new Tabs(value.Binding);
        using var box = Dux.SizedBox(tabs, 400, 40);
        Counting.Calls = 0;
        for (var i = 0; i < 3; i++)
            Frame(box.Render);
        var calls = Counting.Calls;
        for (var i = 0; i < 8; i++)
            Frame(box.Render);
        check(Counting.Calls == calls, "unchanged TabList renders reuse intrinsic tab metrics without suppressing paint or layout");
        value.Value = "b";
        Frame(box.Render);
        check(Counting.Calls == calls && Counting.Selected == "Beta",
            "reserved tab selection changes actual painting without repeating intrinsic measurement");
        check(tabs.RetainsMeasurement, "reserved nonoverflow TabList participates in existing ancestor measurement retention");
        tabs.Options = tabs.Options with
        {
            ReserveSelectedSpace = false
        };
        Frame(box.Render);
        calls = Counting.Calls;
        value.Value = "a";
        Frame(box.Render);
        check(Counting.Calls > calls && !tabs.RetainsMeasurement,
            "nonreserved selection invalidates intrinsic metrics and remains dynamic for ancestors");
        tabs.Options = tabs.Options with
        {
            ReserveSelectedSpace = true,
            Overflow = new()
        };
        Frame(box.Render);
        calls = Counting.Calls;
        box.Options = box.Options with
        {
            Width = 80
        };
        Frame(box.Render);
        check(Counting.Calls == calls && tabs.OverflowResult is { IsOverflowing: true } && !tabs.RetainsMeasurement,
            "overflow allocation rebuilds positions and visibility while reusing constraint-independent intrinsic metrics");
        value.Value = "c";
        Frame(box.Render);
        check(Counting.Calls == calls && Counting.Selected == "Gamma",
            "overflow selection promotion rebuilds the real header without remeasuring reserved text");
        box.Options = box.Options with
        {
            Width = 400
        };
        Frame(box.Render);
        check(Counting.Calls == calls && tabs.OverflowResult is { IsOverflowing: false },
            "a widened header restores hidden tab geometry from intrinsic sizes rather than overflow-mutated rectangles");
        settings = settings with
        {
            Scale = 1.5f
        };
        Frame(box.Render);
        check(Counting.Calls > calls, "text scale invalidates retained tab metrics");
        calls = Counting.Calls;
        var fonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        context.SetTypographyFonts(new(Path.Combine(fonts, "consola.ttf"), Path.Combine(fonts, "segoeui.ttf"), Path.Combine(fonts, "segoeuib.ttf")));
        Frame(box.Render);
        check(Counting.Calls > calls, "configured typography resources invalidate retained tab metrics");
        var atlasObserverCalls = 0;
        settings = UiTextSettings.Default with
        {
            MissingGlyphObserver = _ => atlasObserverCalls++
        };
        Frame(box.Render);
        calls = Counting.Calls;
        Frame(box.Render);
        check(Counting.Calls == calls && atlasObserverCalls == 0, "native intrinsic tab measurement retains reuse without atlas observer callbacks");
        settings = UiTextSettings.Default;
        tabs.SetItems(new UiTabOption[] { new("a", "Changed Alpha"), new("c", "Gamma") });
        calls = Counting.Calls;
        Frame(box.Render);
        check(Counting.Calls > calls, "item replacement invalidates the old intrinsic size payload");
        using var custom = new UiTabList<UiFluentTabListBehavior, Dynamic>("Dynamic", value.Binding, Items());
        Frame(custom.Render);
        calls = Counting.Calls;
        Frame(custom.Render);
        check(Counting.Calls > calls && !custom.RetainsMeasurement, "unknown custom tab presentations remain dynamically measured by default");
        tabs.SetItems(Enumerable.Range(0, 257).Select(i => new UiTabOption(i.ToString(), "Tab")).ToArray());
        Frame(box.Render);
        calls = Counting.Calls;
        Frame(box.Render);
        check(Counting.Calls > calls, "more than 256 tab entries do not retain an unbounded intrinsic buffer");
        tabs.SetItems(Items());
        tabs.Options = tabs.Options with
        {
            Overflow = null
        };
        void MeasureOnly(UiImmediateContext ui) => tabs.MeasureConstrained(ui, UiBoxConstraints.Loose(new(400, 400)));
        Counting.OnMeasure = () => { Counting.OnMeasure = null; tabs.Refresh(); };
        Frame(MeasureOnly);
        calls = Counting.Calls;
        Frame(MeasureOnly);
        check(Counting.Calls > calls, "invalidation during measurement prevents committing a stale intrinsic cache attempt");
        calls = Counting.Calls;
        Frame(MeasureOnly);
        check(Counting.Calls == calls, "the next clean measurement attempt can retain tab metrics after an invalidated attempt");
        tabs.Refresh();
        Counting.OnMeasure = static () => throw new InvalidOperationException("Expected tab metric failure");
        var rejected = false;
        Frame(ui =>
        {
            try
            {
                MeasureOnly(ui);
            }
            catch (InvalidOperationException exception) when (exception.Message == "Expected tab metric failure") { rejected = true; }
        });
        Counting.OnMeasure = null;
        calls = Counting.Calls;
        Frame(MeasureOnly);
        check(rejected && Counting.Calls > calls, "failed tab measurement is not published as a reusable cache entry");
        tabs.Dispose();
        var disposedRejected = false;
        Frame(ui => { try { tabs.Render(ui); } catch (ObjectDisposedException) { disposedRejected = true; } });
        check(disposedRejected, "disposed tab widgets reject rendering after releasing intrinsic metric storage");
    }
    private static UiTabOption[] Items() => [new("a", "Alpha"), new("b", "Beta"), new("c", "Gamma")];
    private sealed class Tabs(UiBinding<string?> value) : UiTabList<UiFluentTabListBehavior, Counting>("Counted tabs", value, Items())
    {
        internal void Refresh() => RequestRebuild();
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
    internal readonly struct Counting : IUiTabListPresentation
    {
        public static bool CanRetainMeasurement => true;
        internal static int Calls;
        internal static string? Selected;
        internal static Action? OnMeasure;
        public static UiVector2 MeasureTab(UiControlContext context, string label, bool selected, UiTabListOptions options)
        {
            Calls++;
            OnMeasure?.Invoke();
            return UiFluentTabListPresentation.MeasureTab(context, label, selected, options);
        }
        public static void PaintTab(UiControlContext context, string label, UiRect bounds, bool selected, UiButtonInteraction interaction, UiTabListOptions options)
        {
            if (selected)
                Selected = label;
            UiFluentTabListPresentation.PaintTab(context, label, bounds, selected, interaction, options);
        }
        public static UiVector2 MeasureOverflow(UiControlContext context, UiTabListOptions options) => UiFluentTabListPresentation.MeasureOverflow(context, options);
        public static void PaintOverflow(UiControlContext context, UiRect bounds, UiButtonInteraction interaction, UiTabListOptions options) => UiFluentTabListPresentation.PaintOverflow(context, bounds, interaction, options);
    }
    private readonly struct Dynamic : IUiTabListPresentation
    {
        public static UiVector2 MeasureTab(UiControlContext context, string label, bool selected, UiTabListOptions options) => Counting.MeasureTab(context, label, selected, options);
        public static void PaintTab(UiControlContext context, string label, UiRect bounds, bool selected, UiButtonInteraction interaction, UiTabListOptions options) => Counting.PaintTab(context, label, bounds, selected, interaction, options);
    }
}
