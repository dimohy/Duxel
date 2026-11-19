using System.Text.Json;
using Duxel.Core;

internal sealed record PopupMeasuredCase(string Name, UiRect Target, UiVector2 NaturalSize, UiPopupMeasureRequest Request,
    bool DynamicOffset, UiRect Boundary, UiPopupPositioning Options, UiPopupLayout Expected);

internal static class PopupMeasuredTests
{
    internal static PopupMeasuredCase[] ReadCases(string fixture = "PopupMeasuredReference.json") => JsonSerializer.Deserialize(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", fixture)),
        PopupReferenceJson.Default.PopupMeasuredCaseArray) ?? throw new InvalidOperationException("Missing measured fixture.");

    internal readonly struct Offset : IUiPopupOffset
    {
        public static UiPopupOffset Resolve(UiPopupOffsetContext context) => new(
            (context.Options.Gap ?? 4) + (context.Size.X - context.Target.Width) * 0.125f + (int)context.Side * 2,
            context.Options.CrossAxisOffset + (int)context.Position % 3 * 3 - context.Target.Height * 0.25f);
    }

    internal readonly struct NumericMeasure : IUiPopupMeasurement<UiVector2>
    {
        public static UiPopupContentMeasure Measure(UiControlContext context, UiVector2 content, UiPopupMeasureRequest request)
        {
            var width = request.Width ?? content.X;
            return new(new UiVector2(width, content.Y * MathF.Ceiling(content.X / width)));
        }
    }

    internal static UiPopupLayout Arrange(UiControlContext context, PopupMeasuredCase item, UiPopupPositioning options,
        UiRect target, UiRect boundary) =>
        item.DynamicOffset
            ? UiPopupPlacement.ArrangeMeasured<NumericMeasure, UiVector2, Offset>(context, item.NaturalSize,
                target, boundary, options, item.Request)
            : UiPopupPlacement.ArrangeMeasured<NumericMeasure, UiVector2>(context, item.NaturalSize,
                target, boundary, options, item.Request);

    internal static void Run(Action<bool, string> check)
    {
        var cases = ReadCases();
        check(cases.Length == 384, "popup measured reference contains384 source cases");
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        ControlFontFixture.Configure(context);
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        context.NewFrame(new(0.01f, new(320, 240), new(1, 1)), idle, new(0, 0, 320, 240), UiTextSettings.Default);
        context.Render(new Screen(ui =>
        {
            ui.EnableRootViewportContentLayout(contentPadding : 0);
            var control = new UiControlContext(ui);
            var cursor = ui.GetCursorScreenPos();
            foreach (var item in cases)
            {
                var actual = Arrange(control, item, item.Options, item.Target, item.Boundary);
                var equal = PopupCollisionTests.Matches(actual, item.Expected);
                check(equal, item.Name + (equal ? string.Empty : " expected : " + item.Expected + " actual : " + actual));
            }
            check(cursor == ui.GetCursorScreenPos(), "popup measurement preserves parent cursor");
            const string text = "One two three four five six seven eight nine ten.";
            var natural = UiPopupPlacement.ArrangeMeasured<TextSurface, string>(control, text,
                new(120, 100, 80, 24), new(0, 0, 320, 240));
            var matched = UiPopupPlacement.ArrangeMeasured<TextSurface, string>(control, text,
                new(120, 100, 80, 24), new(0, 0, 320, 240), new() { MatchTargetWidth = true });
            check(matched.Bounds.Width == 80 && matched.Bounds.Height > natural.Bounds.Height,
                "popup target width remeasures actual wrapped text height");
            var rejected = false;
            try
            {
                UiPopupPlacement.Arrange(new(120, 100, 80, 24), new(200, 20), new(0, 0, 320, 240),
                    new() { MatchTargetWidth = true });
            }
            catch (InvalidOperationException)
            {
                rejected = true;
            }
            check(rejected, "popup explicit-size API requires content remeasurement for width matching");
            rejected = false;
            try
            {
                UiPopupPlacement.ArrangeMeasured<InputMeasure, string>(control, text,
                    new(120, 100, 80, 24), new(0, 0, 320, 240));
            }
            catch (InvalidOperationException)
            {
                rejected = true;
            }
            control.ActivateOnRelease("after-popup-measure", new(0, 0, 10, 10));
            check(rejected && cursor == ui.GetCursorScreenPos(), "popup input rejection restores measurement scope for later input");
        }));
        using var draw = context.GetDrawData();
    }

    internal static void Render(Func<string, Action<UiImmediateContext>, byte[]> capture,
        Action<UiInputState> setInput, Action<bool, string> check)
    {
        var cases = ReadCases();
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        foreach (var index in new[] { 1, 3, 10, 11, 17, 24, 55, 58, 60, 69, 76, 79 })
        {
            var item = cases[index];
            NumericSurface.Current = item;
            using var tooltip = new UiTooltip<UiHoverFocusTooltipBehavior, NumericSurface>(
                new UiButton<UiReleaseButtonBehavior, Button>("Anchor") { Size = new(item.Target.Width, item.Target.Height) },
                "Measured", new() { ShowDelaySeconds = 0, Positioning = item.Options });
            var key = "popup-measured-" + index;
            setInput(idle);
            var expected = capture(key + "-expected", ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding : 0);
                var control = new UiControlContext(ui);
                control.Frame(item.Target, new(20, 40, 80), new(0, 0, 0, 0), 0, 0);
                Paint(control, item.Expected);
            });
            setInput(idle with { MousePosition = new(item.Target.X + item.Target.Width * 0.5f, item.Target.Y + item.Target.Height * 0.5f) });
            var actual = capture(key, ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding : 0);
                ui.SetCursorScreenPos(new(item.Target.X, item.Target.Y));
                tooltip.Render(ui);
                tooltip.Options = default;
            });
            check(actual.AsSpan().SequenceEqual(expected), "popup actual measured owner matches upstream : " + index);
        }
        const string text = "One two three four";
        for (var mode = 0; mode < 3; mode++)
        {
            TextSurface.ExplicitWidth = mode == 1 ? 96 : null;
            var match = mode != 2;
            using var tooltip = new UiTooltip<UiHoverFocusTooltipBehavior, TextSurface>(
                new UiButton<UiReleaseButtonBehavior, Button>("Anchor") { Size = new(80, 24) }, text,
                new() { ShowDelaySeconds = 0, Positioning = new() { Position = UiPopupPosition.AboveStart, MatchTargetWidth = match } });
            var key = "popup-wrapped-text-" + mode;
            setInput(idle);
            var expected = capture(key + "-expected", ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding : 0);
                var control = new UiControlContext(ui);
                control.Frame(new(120, 100, 80, 24), new(20, 40, 80), new(0, 0, 0, 0), 0, 0);
                using var typography = control.BeginTypography<UiRegularTypeface, UiFluentType200>();
                var width = mode == 0 ? 80f : mode == 1 ? 96f : 240f;
                var block = control.MeasureTextBlock(control.WrapText(text, width));
                if (mode == 2)
                {
                    width = block.Size.X;
                }
                var bounds = new UiRect(120, 96 - block.Size.Y, width, block.Size.Y);
                control.Frame(bounds, new(220, 240, 255), new(0, 0, 0, 0), 0, 0);
                using var clip = control.Clip(bounds);
                control.TypographyTextBlock(block, bounds, new(20, 40, 80));
            });
            setInput(idle with { MousePosition = new(130, 110) });
            var actual = capture(key, ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding : 0);
                ui.SetCursorScreenPos(new(120, 100));
                tooltip.Render(ui);
            });
            check(actual.AsSpan().SequenceEqual(expected), "popup measures and paints actual wrapped typography : " + mode);
        }
        TextSurface.ExplicitWidth = null;
        setInput(idle);
    }

    private static void Paint(UiControlContext context, UiPopupLayout layout)
    {
        context.Frame(layout.Bounds, new(220, 240, 255), new(0, 0, 0, 0), 0, 0);
        if (layout.Arrow is { } arrow)
        {
            context.Triangle(arrow.Tip, arrow.BaseStart, arrow.BaseEnd, new(220, 240, 255));
        }
    }

    private readonly struct Button : IUiButtonPresentation
    {
        public static UiVector2 Measure(UiControlContext context, UiVector2 textSize, UiVector2 requestedSize) => requestedSize;
        public static void Paint(UiControlContext context, string text, UiVector2 textSize, UiRect bounds,
            UiButtonInteraction interaction) => context.Frame(bounds, new(20, 40, 80), new(0, 0, 0, 0), 0, 0);
    }

    private readonly struct NumericSurface : IUiTooltipPresentation
    {
        internal static PopupMeasuredCase Current = null!;
        public static UiPopupLayout Arrange(UiControlContext context, string text, UiRect target, UiRect viewport,
            UiTooltipOptions options) => PopupMeasuredTests.Arrange(context, Current, options.Positioning, target, viewport);
        public static void Paint(UiControlContext context, string text, UiPopupLayout layout) => PopupMeasuredTests.Paint(context, layout);
    }

    internal readonly struct TextSurface : IUiTooltipPresentation, IUiPopupMeasurement<string>
    {
        internal static float? ExplicitWidth = null;
        public static UiPopupContentMeasure Measure(UiControlContext context, string text, UiPopupMeasureRequest request)
        {
            using var typography = context.BeginTypography<UiRegularTypeface, UiFluentType200>();
            var limit = MathF.Min(request.Width ?? request.MaxWidth ?? 240, request.MaxWidth ?? float.PositiveInfinity);
            var block = context.MeasureTextBlock(context.WrapText(text, MathF.Max(1, limit)));
            return new(new(MathF.Max(1, MathF.Min(request.Width ?? block.Size.X, request.MaxWidth ?? float.PositiveInfinity)),
                MathF.Max(1, MathF.Min(request.Height ?? block.Size.Y, request.MaxHeight ?? float.PositiveInfinity))), block.Size);
        }
        public static UiPopupLayout Arrange(UiControlContext context, string text, UiRect target, UiRect viewport,
            UiTooltipOptions options) => UiPopupPlacement.ArrangeMeasured<TextSurface, string>(context, text,
                target, viewport, options.Positioning, new(ExplicitWidth));
        public static void Paint(UiControlContext context, string text, UiPopupLayout layout)
        {
            using var typography = context.BeginTypography<UiRegularTypeface, UiFluentType200>();
            context.Frame(layout.Bounds, new(220, 240, 255), new(0, 0, 0, 0), 0, 0);
            var block = context.MeasureTextBlock(context.WrapText(text, layout.Bounds.Width));
            using var clip = context.Clip(layout.Bounds);
            context.TypographyTextBlock(block, new(layout.Bounds.X - layout.ScrollOffset.X,
                layout.Bounds.Y - layout.ScrollOffset.Y, layout.Bounds.Width, block.Size.Y), new(20, 40, 80));
        }
    }

    private readonly struct InputMeasure : IUiPopupMeasurement<string>
    {
        public static UiPopupContentMeasure Measure(UiControlContext context, string content, UiPopupMeasureRequest request)
        {
            context.ActivateOnRelease("invalid-popup-measure", new(0, 0, 10, 10));
            return new(new UiVector2(10, 10));
        }
    }

    private sealed class Screen(Action<UiImmediateContext> draw) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => draw(ui);
    }
}
