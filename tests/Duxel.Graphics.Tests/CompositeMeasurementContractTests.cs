using Duxel.Core;

internal static class CompositeMeasurementContractTests
{
    internal static void Run(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        var fonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        var regular = Path.Combine(fonts, "consola.ttf");
        var semibold = Path.Combine(fonts, "segoeui.ttf");
        if (!File.Exists(regular) || !File.Exists(semibold))
            throw new FileNotFoundException("The unequal-width font fixture requires Consolas and Segoe UI.");
        context.SetTypographyFonts(new(regular, semibold, semibold));
        context.NewFrame(new(1f / 60, new(640, 480), new(1, 1)),
            new(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default),
            new(0, 0, 640, 480), UiTextSettings.Default);
        context.Render(new Screen(ui =>
        {
            var control = new UiControlContext(ui);
            var options = new UiFieldOptions { Label = "Field" };
            using var dynamicSize = new UiFieldContent<UiFluentFieldPresentation<MutableSize, UiVerticalFieldLayout>>();
            MutableSize.Padding = 2;
            dynamicSize.Measure(ui, control, options, new(120, 32), default, null, true);
            var initial = dynamicSize.Layout;
            MutableSize.Padding = 10;
            dynamicSize.Measure(ui, control, options, new(120, 32), default, null, true);
            check(dynamicSize.Layout.Control.Y == initial.Control.Y + 16 && dynamicSize.BuildCount == 2,
                "Field remeasures mutable custom size traits without an implicit cache promise");
            using var dynamicOrientation = new UiFieldContent<UiFluentFieldPresentation<UiFluentMediumFieldSize, MutableOrientation>>();
            MutableOrientation.IsHorizontal = false;
            dynamicOrientation.Measure(ui, control, options, new(120, 32), default, null, true);
            MutableOrientation.IsHorizontal = true;
            dynamicOrientation.Measure(ui, control, options, new(120, 32), default, null, true);
            check(dynamicOrientation.Layout.Control.X > 0 && dynamicOrientation.BuildCount == 2,
                "Field remeasures mutable custom orientation without retaining the previous column geometry");
            using var known = new UiFieldContent<UiFluentFieldPresentation<UiFluentMediumFieldSize, UiVerticalFieldLayout>>();
            known.Measure(ui, control, options, new(120, 32), default, null, true);
            known.Measure(ui, control, options, new(120, 32), default, null, true);
            check(known.BuildCount == 1, "known Fluent Field traits preserve bounded layout reuse");

            var item = new UiTabOption("wide", "iiiiiiiiiiii");
            var natural = new UiTabListOptions(ReserveSelectedSpace : false);
            var plain = UiFluentTabListPresentation.MeasureTab(control, item, false, natural);
            var selected = UiFluentTabListPresentation.MeasureTab(control, item, true, natural);
            check(plain.X > selected.X, "the real font fixture has regular text wider than selected semibold text");
            foreach (var size in Enum.GetValues<UiTabListSize>())
            foreach (var appearance in Enum.GetValues<UiTabListAppearance>())
            foreach (var vertical in new[] { false, true })
            {
                var reserved = new UiTabListOptions(Size : size, Vertical : vertical, Appearance : appearance);
                var a = UiFluentTabListPresentation.MeasureTab(control, item, false, reserved);
                var b = UiFluentTabListPresentation.MeasureTab(control, item, true, reserved);
                check(a == b, $"reserved TabList geometry is selection independent : {size}/{appearance}/{vertical}");
            }
        }));
        using var draw = context.GetDrawData();
    }

    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
    private readonly struct MutableSize : IUiFieldSize
    {
        internal static float Padding;
        public static float VerticalPadding => Padding;
        public static float VerticalMargin => 2;
        public static float HorizontalPadding => 6;
        public static UiControlTypographyScope BeginLabel(UiControlContext context) =>
            context.BeginTypography<UiRegularTypeface, UiFluentType300>();
    }
    private readonly struct MutableOrientation : IUiFieldOrientation
    {
        internal static bool IsHorizontal;
        public static bool Horizontal => IsHorizontal;
    }
}
