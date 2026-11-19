using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Duxel.Core;

internal static class LeafLayoutRetentionTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = CreateContext();
        var idle = Idle();
        var settings = UiTextSettings.Default;
        void Frame(Action<UiImmediateContext> render)
        {
            context.NewFrame(new(1f / 60, new(640, 480), new(1, 1)), idle, new(0, 0, 640, 480), settings);
            context.Render(new Screen(ui => { ui.EnableRootViewportContentLayout(contentPadding: 0); render(ui); }));
            using var data = context.GetDrawData();
        }
        var checkbox = new MeasuredCheckbox();
        var slider = new MeasuredSlider();
        var radio = new MeasuredRadio();
        using var parent = Dux.Column(new(Spacing: 4, MainAxisSize: UiMainAxisSize.Min), checkbox, slider, radio);
        var constraints = UiBoxConstraints.Loose(new(400, 400));
        UiVector2 desired = default;
        void Measure(UiImmediateContext ui) => desired = parent.MeasureConstrained(ui, constraints);
        for (var i = 0; i < 3; i++)
            Frame(Measure);
        var counts = checkbox.Measurements + slider.Measurements + radio.Measurements;
        var expected = desired;
        for (var i = 0; i < 10; i++)
            Frame(Measure);
        check(checkbox.Measurements + slider.Measurements + radio.Measurements == counts && desired == expected,
            "unchanged real Checkbox, Slider and RadioGroup allow their shared parent to reuse measurement");
        check(checkbox.Renders == 0 && slider.Renders == 0 && radio.Renders == 0,
            "measurement reuse remains a pure measurement operation without painting or input");
        Frame(parent.Render);
        Frame(parent.Render);
        check(checkbox.Renders == 2 && slider.Renders == 2 && radio.Renders == 2,
            "retained layout still executes every real control render and input path each frame");
        counts = checkbox.Measurements + slider.Measurements + radio.Measurements;
        checkbox.Label = "A changed checkbox label";
        Frame(Measure);
        check(checkbox.Measurements + slider.Measurements + radio.Measurements > counts,
            "real checkbox Label mutation invalidates the owned parent measurement chain");
        counts = slider.Measurements;
        slider.Size = new(300, 48);
        Frame(Measure);
        check(slider.Measurements > counts, "real slider Size mutation invalidates its parent layout");
        counts = radio.Measurements;
        radio.SetItems(new UiRadioOption[] { new("a", "A longer option"), new("b", "Second option") });
        Frame(Measure);
        check(radio.Measurements > counts, "RadioGroup item replacement invalidates its parent layout");
        counts = checkbox.Measurements;
        settings = settings with
        {
            Scale = 2
        };
        Frame(Measure);
        check(checkbox.Measurements > counts, "text scale invalidates the newly participating leaf measurement path");
        counts = slider.Measurements;
        constraints = UiBoxConstraints.Loose(new(360, 400));
        Frame(Measure);
        check(slider.Measurements > counts, "changed parent constraints remeasure real slider geometry");
        var atlasObserverCalls = 0;
        settings = settings with
        {
            MissingGlyphObserver = _ => atlasObserverCalls++
        };
        Frame(Measure);
        counts = checkbox.Measurements;
        Frame(Measure);
        check(checkbox.Measurements == counts && atlasObserverCalls == 0, "native leaf measurement does not consult the atlas missing-glyph observer");
        context.SetDirectTextEnabled(false);
        var rejectedAtlas = false;
        try
        {
            Frame(Measure);
        }
        catch (NotSupportedException) { rejectedAtlas = true; }
        finally { context.SetDirectTextEnabled(true); }
        check(rejectedAtlas, "Fluent leaf measurement rejects unsupported atlas text instead of silently changing typography");
        settings = UiTextSettings.Default;
        using var dynamicToggle = new UiToggle<bool, UiBooleanToggleValue, UiReleaseButtonBehavior, MutableTogglePresentation>("", new UiState<bool>(false));
        using var dynamicParent = Dux.Wrap(default, dynamicToggle);
        UiVector2 first = default;
        MutableTogglePresentation.Edge = 16;
        Frame(ui => first = dynamicParent.MeasureConstrained(ui, constraints));
        MutableTogglePresentation.Edge = 40;
        Frame(ui => desired = dynamicParent.MeasureConstrained(ui, constraints));
        check(desired.X > first.X && !dynamicToggle.RetainsMeasurement,
            "custom Toggle presentations remain dynamic unless they explicitly promise retained measurement");
        using var dynamicSize = new UiToggle<bool, UiBooleanToggleValue, UiReleaseButtonBehavior,
            UiFluentCheckboxPresentation<MutableCheckboxSize, UiFluentSquareCheckboxShape, UiCheckboxLabelAfter>>("", new UiState<bool>(false));
        using var sizeParent = Dux.Wrap(default, dynamicSize);
        MutableCheckboxSize.Edge = 16;
        Frame(ui => first = sizeParent.MeasureConstrained(ui, constraints));
        MutableCheckboxSize.Edge = 40;
        Frame(ui => desired = sizeParent.MeasureConstrained(ui, constraints));
        check(desired.X > first.X && !dynamicSize.RetainsMeasurement,
            "custom checkbox size traits cannot silently inherit a built-in presentation retention promise");
        using var customSlider = new UiSlider<UiFluentSliderBehavior, MutableSliderPresentation>("", new UiState<float>(0), 0, 1);
        using var sliderParent = Dux.Wrap(default, customSlider);
        MutableSliderPresentation.Edge = 16;
        Frame(ui => first = sliderParent.MeasureConstrained(ui, constraints));
        MutableSliderPresentation.Edge = 40;
        Frame(ui => desired = sliderParent.MeasureConstrained(ui, constraints));
        check(desired.X > first.X && !customSlider.RetainsMeasurement,
            "custom Slider presentations remain dynamic without an explicit measurement promise");
        using var customSwitch = new UiToggle<bool, UiBooleanToggleValue, UiReleaseButtonBehavior,
            UiFluentSwitchPresentation<MutableSwitchSize, UiSwitchLabelAfter>>("", new UiState<bool>(false));
        using var switchParent = Dux.Wrap(default, customSwitch);
        MutableSwitchSize.Edge = 16;
        Frame(ui => first = switchParent.MeasureConstrained(ui, constraints));
        MutableSwitchSize.Edge = 40;
        Frame(ui => desired = switchParent.MeasureConstrained(ui, constraints));
        check(desired.X > first.X && !customSwitch.RetainsMeasurement,
            "custom Switch size traits cannot silently inherit a built-in presentation measurement promise");
        using var customRadio = new UiRadioGroup<UiFluentRadioGroupBehavior, UiFluentRadioGroupPresentation<MutableRadioLayout>>(
            "", new UiState<string?>(null), new UiRadioOption[] { new("a", "Option") });
        using var radioParent = Dux.Wrap(default, customRadio);
        MutableRadioLayout.Extra = 0;
        Frame(ui => first = radioParent.MeasureConstrained(ui, constraints));
        MutableRadioLayout.Extra = 25;
        Frame(ui => desired = radioParent.MeasureConstrained(ui, constraints));
        check(desired.X == first.X + 25 && !customRadio.RetainsMeasurement,
            "custom RadioGroup layout traits stay dynamic rather than being frozen by the generic Fluent presentation");
    }

    internal static void Measure(string output)
    {
        const int perFamily = 1000, warmups = 96, samples = 256;
        using var context = CreateContext();
        var checkboxes = new MeasuredCheckbox[perFamily];
        var sliders = new MeasuredSlider[perFamily];
        var radios = new MeasuredRadio[perFamily];
        var children = new UiWidget[perFamily * 3];
        for (var i = 0; i < perFamily; i++)
        {
            children[i * 3] = checkboxes[i] = new();
            children[i * 3 + 1] = sliders[i] = new();
            children[i * 3 + 2] = radios[i] = new();
        }
        using var grid = new UiGrid(children, new(10, MainAxisExtent: 80));
        var constraints = UiBoxConstraints.Loose(new(4000, 30000));
        var ticks = new long[samples];
        long bytes = 0;
        UiVector2 size = default;
        var screen = new Screen(ui => size = grid.MeasureConstrained(ui, constraints));
        void Frame(int sample)
        {
            context.NewFrame(new(1f / 60, new(4000, 30000), new(1, 1)), Idle(), new(0, 0, 4000, 30000), UiTextSettings.Default);
            var allocated = GC.GetAllocatedBytesForCurrentThread();
            var start = Stopwatch.GetTimestamp();
            context.Render(screen);
            var elapsed = Stopwatch.GetTimestamp() - start;
            var delta = GC.GetAllocatedBytesForCurrentThread() - allocated;
            if (sample >= 0)
            {
                ticks[sample] = elapsed;
                bytes += delta;
            }
            using var data = context.GetDrawData();
        }
        long Calls() => checkboxes.Sum(static child => (long)child.Measurements) + sliders.Sum(static child => (long)child.Measurements) + radios.Sum(static child => (long)child.Measurements);
        for (var i = 0; i < warmups; i++)
            Frame(-1);
        var before = Calls();
        for (var i = 0; i < samples; i++)
            Frame(i);
        Array.Sort(ticks);
        double Us(long value) => value * 1_000_000d / Stopwatch.Frequency;
        var result = new LeafLayoutMeasurementResult(RuntimeInformation.FrameworkDescription, perFamily, warmups, samples,
            Calls() - before, bytes, size.X, size.Y, Us(ticks[samples / 2]), Us(ticks[(int)(samples * .95) - 1]), Us(ticks[(int)(samples * .99) - 1]));
        File.WriteAllText(Path.Combine(output, "measurement.json"), JsonSerializer.Serialize(result, LeafLayoutJson.Default.LeafLayoutMeasurementResult));
    }
    private static UiContext CreateContext()
    {
        var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        return context;
    }
    private static UiInputState Idle() => new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
    private sealed class MeasuredCheckbox() : UiToggle<bool?, UiNullableToggleValue, UiReleaseButtonBehavior, UiFluentCheckboxPresentation>("Checkbox", new UiState<bool?>(false))
    {
        internal int Measurements, Renders;
        protected override UiVector2 MeasureContent(UiControlContext context)
        {
            Measurements++;
            return base.MeasureContent(context);
        }
        protected override void RenderControl(UiImmediateContext ui)
        {
            Renders++;
            base.RenderControl(ui);
        }
    }
    private sealed class MeasuredSlider() : UiSlider<UiFluentSliderBehavior, UiFluentSliderPresentation>("Slider", new UiState<float>(.5f), 0, 1)
    {
        internal int Measurements, Renders;
        protected override UiVector2 MeasureContent(UiControlContext context)
        {
            Measurements++;
            return base.MeasureContent(context);
        }
        protected override void RenderControl(UiImmediateContext ui)
        {
            Renders++;
            base.RenderControl(ui);
        }
    }
    private sealed class MeasuredRadio() : UiRadioGroup<UiFluentRadioGroupBehavior, UiFluentRadioGroupPresentation<UiRadioVerticalLayout>>(
        "Choice", new UiState<string?>(null), new UiRadioOption[] { new("a", "Option") })
    {
        internal int Measurements, Renders;
        protected override UiVector2 MeasureContent(UiControlContext context)
        {
            Measurements++;
            return base.MeasureContent(context);
        }
        protected override void RenderControl(UiImmediateContext ui)
        {
            Renders++;
            base.RenderControl(ui);
        }
    }
    private readonly struct MutableTogglePresentation : IUiTogglePresentation
    {
        internal static float Edge;
        public static UiToggleLayout Measure(UiControlContext context, UiVector2 textSize, bool hasLabel) => new(new(Edge, Edge), default, default);
        public static void Paint(UiControlContext context, string text, UiVector2 textSize, UiRect bounds, UiToggleLayout layout, UiButtonInteraction interaction, bool? value)
        {
        }
    }
    private readonly struct MutableCheckboxSize : IUiCheckboxSize
    {
        internal static float Edge;
        public static float IndicatorSize => Edge;
        public static float GlyphSize => 12;
        public static void PaintMark(UiControlContext context, UiRect bounds, UiColor color, UiButtonInteraction interaction, bool mixed, bool circular)
        {
        }
    }
    private readonly struct MutableRadioLayout : IUiRadioGroupLayout
    {
        internal static float Extra;
        public static bool LabelBelow => false;
        public static float OptionWidth(float availableWidth, int count) => availableWidth;
        public static UiVector2 Place(Span<UiRadioOptionLayout> options, float labelHeight, float labelWidth)
        {
            var size = UiRadioVerticalLayout.Place(options, labelHeight, labelWidth);
            return size with
            {
                X = size.X + Extra
            };
        }
    }
    private readonly struct MutableSliderPresentation : IUiSliderPresentation
    {
        internal static float Edge;
        public static UiSliderLayout Measure(UiControlContext context, UiSliderOptions options, UiVector2 requestedSize) => new(new(Edge, Edge), default, 0, 0);
        public static void Paint(UiControlContext context, UiRect bounds, UiSliderLayout layout, UiSliderOptions options, UiSliderInteraction interaction)
        {
        }
    }
    private readonly struct MutableSwitchSize : IUiSwitchSize
    {
        internal static float Edge;
        public static UiVector2 TrackSize => new(Edge, 16);
        public static UiControlTypographyScope BeginText(UiControlContext context, bool hasText) => default;
    }
}
internal sealed record LeafLayoutMeasurementResult(string Runtime, int ChildrenPerFamily, int Warmups, int Samples,
    long ChildMeasurements, long AllocatedBytes, float Width, float Height, double MedianUs, double P95Us, double P99Us);
[JsonSerializable(typeof(LeafLayoutMeasurementResult))]
internal partial class LeafLayoutJson : JsonSerializerContext;
