using System.Globalization;
using Duxel.Core;

internal static class RatingDisplayTests
{
    internal static void Run(Action<bool, string> check)
    {
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            RunDisplay(check);
            RunGpu(check);
        }
        finally { CultureInfo.CurrentCulture = culture; }
    }
    private static void RunDisplay(Action<bool, string> check)
    {
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        using var tree = new UiAccessibilityTree();
        ControlFontFixture.Configure(context);
        context.SetAccessibilityTree(tree);
        using var display = Fluent.RatingDisplay("Average", new UiRatingDisplayValue(3.5f, 1234));
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        var frame = new UiFrameInfo(.016f, new(400, 300), new(1, 1));
        UiWidget current = display;
        UiRect bounds = default;
        var screen = new Screen(ui =>
        {
            ui.EnableRootViewportContentLayout(contentPadding: 0);
            ui.SetCursorScreenPos(new(10, 10));
            current.Render(ui);
            var start = ui.GetItemRectMin();
            var end = ui.GetItemRectMax();
            bounds = new(start.X, start.Y, end.X - start.X, end.Y - start.Y);
        });
        void Frame(UiInputState? input = null)
        {
            context.NewFrame(frame, input ?? idle, new(0, 0, 400, 300), UiTextSettings.Default);
            context.Render(screen);
            using var data = context.GetDrawData();
        }
        Frame();
        var image = tree.CaptureSnapshot().Elements.Single().Node;
        check(image is { Role: UiAccessibilityRole.Image, Focusable: false, ValuePattern: false }
            && image.Name == "Average 3.5 1,234" && image.ImageInfo?.Description == image.Name,
            "RatingDisplay / one noninteractive image publishes localized score/count without radio children");
        var full = bounds;
        var revision = tree.Revision;
        Frame();
        check(tree.Revision == revision && bounds == full, "RatingDisplay / unchanged content keeps geometry and accessibility revision stable");
        Frame(idle with
        {
            MousePosition = new(30, 15),
            LeftMouseDown = true,
            LeftMousePressed = true
        });
        Frame(idle with
        {
            MousePosition = new(30, 15),
            LeftMouseReleased = true
        });
        check(context.State.FocusedId is null && display.Value.Value == 3.5f, "RatingDisplay / clicking the image creates no focus or selection");
        display.Options = display.Options with
        {
            Compact = true
        };
        Frame();
        check(bounds.Width < full.Width && bounds.Height == full.Height && tree.CaptureSnapshot().Elements.Count == 1,
            "RatingDisplay / compact layout uses one icon while preserving the complete image meaning");
        display.Value = new(4.5f, 10000);
        Frame();
        check(tree.CaptureSnapshot().Elements.Single().Node.Name == "Average 4.5 10,000", "RatingDisplay / static value mutation updates numeric text and image description");
        display.Options = display.Options with
        {
            ShowValue = false,
            ShowCount = false
        };
        Frame();
        check(bounds.Width == 16 && tree.CaptureSnapshot().Elements.Single().Node.Name.Contains("4.5 10,000", StringComparison.Ordinal),
            "RatingDisplay / hidden visual labels retain the score and count in the accessible image name");
        display.Options = display.Options with
        {
            ShowValue = true,
            ShowCount = true,
            RightToLeft = true
        };
        display.Value = new(4.5f, 12, "좋음", "후기 12개", "사용자 지정 평가");
        Frame();
        check(tree.CaptureSnapshot().Elements.Single().Node.Name == "사용자 지정 평가", "RatingDisplay / explicit localized labels and accessible description are preserved literally");
        var rejected = 0;
        foreach (var bad in new[] { new UiRatingDisplayValue(float.NaN), new(6), new(3, -1) })
            try
            {
                display.Value = bad;
            }
            catch (ArgumentOutOfRangeException) { rejected++; }
        try
        {
            display.Options = display.Options with
            {
                Maximum = 1
            };
        }
        catch (ArgumentOutOfRangeException) { rejected++; }
        try
        {
            display.Options = display.Options with
            {
                Color = (UiRatingColor)99
            };
        }
        catch (ArgumentOutOfRangeException) { rejected++; }
        check(rejected == 5 && display.Value.AccessibleLabel == "사용자 지정 평가", "RatingDisplay / invalid values/options fail before replacing valid state");
        var live = new UiRatingDisplayValue(1.5f, 20);
        using var dynamic = Fluent.RatingDisplay("Live", () => live);
        current = dynamic;
        Frame();
        live = new(4.5f, 2000);
        Frame();
        check(tree.CaptureSnapshot().Elements.Single().Node.Name == "Live 4.5 2,000", "RatingDisplay / getter source reads current value without a writable binding");
        var calls = TrackingPresentation.Layouts;
        using var retained = new UiRatingDisplay<TrackingPresentation>("Culture", new UiRatingDisplayValue(3.5f, 1234));
        using var parent = Dux.Column(new(Spacing: 0, MainAxisSize: UiMainAxisSize.Min), retained);
        current = parent;
        Frame();
        Frame();
        var stableCalls = TrackingPresentation.Layouts;
        Frame();
        check(stableCalls > calls && TrackingPresentation.Layouts == stableCalls, "RatingDisplay / static parent and typography layout reuse unchanged measurements");
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
        Frame();
        check(TrackingPresentation.Layouts > stableCalls && tree.CaptureSnapshot().Elements.Single().Node.Name.Contains("3,5", StringComparison.Ordinal),
            "RatingDisplay / culture change invalidates retained parent geometry before publishing localized text");
        var localizedCalls = TrackingPresentation.Layouts;
        var customCulture = (CultureInfo)CultureInfo.CurrentCulture.Clone();
        customCulture.NumberFormat.NumberDecimalSeparator = ":";
        CultureInfo.CurrentCulture = CultureInfo.ReadOnly(customCulture);
        Frame();
        check(TrackingPresentation.Layouts > localizedCalls && tree.CaptureSnapshot().Elements.Single().Node.Name.Contains("3:5", StringComparison.Ordinal),
            "RatingDisplay / same-name culture replacement invalidates retained geometry for custom numeric formatting");
        var step = 0;
        context.NewInputBatch(frame, [idle, idle, idle], new(0, 0, 400, 300), UiTextSettings.Default);
        current = dynamic;
        context.Render(new Screen(ui => { live = new(++step, step * 100); screen.Render(ui); }));
        using (context.GetDrawData())
            check(step == 3 && context.InputProcessingMetrics is { CompletedSteps: 3, BuiltDrawings: 1 }
                && tree.CaptureSnapshot().Elements.Single().Node.Name.Contains("3", StringComparison.Ordinal),
                "RatingDisplay / ordered getter changes publish one final noninteractive presentation");
        check(Fluent.ComponentNames.Contains("RatingDisplay") && Fluent.RequiredComponentNames.Length == 55,
            "RatingDisplay / generated variant adds a factory without inflating the official top-level requirement count");
    }
    private static void RunGpu(Action<bool, string> check)
    {
        foreach (var slots in new[] { 1, 3 })
            foreach (var darkTheme in new[] { false, true })
            {
                using var reference = new PresentationReuseTests.Fixture(slots, false);
                using var candidate = new PresentationReuseTests.Fixture(slots, true);
                var theme = darkTheme ? UiCompiledDesign.Windows11Dark.Theme : UiCompiledDesign.Windows11.Theme;
                reference.Context.SetTheme(theme);
                candidate.Context.SetTheme(theme);
                foreach (var shape in Enum.GetValues<UiRatingShape>())
                    foreach (var color in Enum.GetValues<UiRatingColor>())
                        foreach (var compact in new[] { false, true })
                            foreach (var rtl in new[] { false, true })
                            {
                                using var display = Fluent.RatingDisplay("GPU display", new UiRatingDisplayValue(2.5f),
                                    new UiRatingDisplayOptions { Shape = shape, Color = color, Compact = compact, RightToLeft = rtl, ShowValue = false, ShowCount = false });
                                using var expectedData = reference.Build(ui =>
                                {
                                    ui.EnableRootViewportContentLayout(contentPadding: 0);
                                    var paint = new UiControlContext(ui);
                                    var dark = paint.FluentColorScheme == UiFluentColorScheme.Dark;
                                    var selected = color switch
                                    {
                                        UiRatingColor.Brand => dark ? new UiColor(0x47, 0x9e, 0xf5) : new(0x0f, 0x6c, 0xbd),
                                        UiRatingColor.Marigold => dark ? new UiColor(0xf2, 0xc6, 0x61) : new(0xea, 0xa3, 0),
                                        _ => paint.Theme.Text
                                    };
                                    var empty = color switch
                                    {
                                        UiRatingColor.Brand => dark ? new UiColor(0x08, 0x23, 0x38) : new(0xeb, 0xf3, 0xfc),
                                        UiRatingColor.Marigold => dark ? new UiColor(0x83, 0x5b, 0) : new(0xf9, 0xe2, 0xae),
                                        _ => dark ? new UiColor(0x33, 0x33, 0x33) : new(0xe6, 0xe6, 0xe6)
                                    };
                                    var count = compact ? 1 : 5;
                                    for (var i = 0; i < count; i++)
                                    {
                                        var icon = new UiRect(25 + (rtl ? count - i - 1 : i) * 16, 30, 16, 16);
                                        var fill = compact ? 1 : Math.Clamp(2.5f - i, 0, 1);
                                        void Paint(UiColor tint)
                                        {
                                            switch (shape)
                                            {
                                                case UiRatingShape.Star:
                                                    UiRatingStarFilled.Paint(paint, icon, tint, default);
                                                    break;
                                                case UiRatingShape.Circle:
                                                    UiRatingCircleFilled.Paint(paint, icon, tint, default);
                                                    break;
                                                case UiRatingShape.Square:
                                                    UiRatingSquareFilled.Paint(paint, icon, tint, default);
                                                    break;
                                            }
                                        }
                                        if (fill < 1)
                                        {
                                            using var clip = paint.Clip(new(rtl ? icon.X : icon.X + fill * 16, icon.Y, (1 - fill) * 16, 16));
                                            Paint(empty);
                                        }
                                        if (fill > 0)
                                        {
                                            using var clip = paint.Clip(new(rtl ? icon.X + (1 - fill) * 16 : icon.X, icon.Y, fill * 16, 16));
                                            Paint(selected);
                                        }
                                    }
                                });
                                var expected = reference.Render(expectedData);
                                using var actualData = candidate.Build(ui => { ui.EnableRootViewportContentLayout(contentPadding: 0); ui.SetCursorScreenPos(new(25, 30)); display.Render(ui); });
                                check(candidate.Render(actualData).AsSpan().SequenceEqual(expected),
                                    $"RatingDisplay GPU / dark={darkTheme}, slots={slots}, shape={shape}, color={color}, compact={compact}, RTL={rtl} / independent filled glyph and palette reference");
                            }
            }
    }
    private readonly struct TrackingPresentation : IUiRatingDisplayPresentation
    {
        internal static int Layouts;
        public static bool CanRetainMeasurement => true;
        public static UiRatingDisplayLayout Layout(UiControlContext context, UiRatingDisplayContent content, UiRatingDisplayOptions options, Span<UiRect> items)
        {
            Layouts++;
            return UiFluentRatingDisplayPresentation.Layout(context, content, options, items);
        }
        public static void Paint(UiControlContext context, UiRect bounds, UiRatingDisplayContent content, UiRatingDisplayOptions options, UiRatingDisplayLayout layout, ReadOnlySpan<UiRect> items)
            => UiFluentRatingDisplayPresentation.Paint(context, bounds, content, options, layout, items);
    }
    private sealed class Screen(Action<UiImmediateContext> render) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => render(ui);
    }
}
