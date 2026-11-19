using System.Text.Json;
using System.Text.Json.Serialization;
using Duxel.Core;

internal sealed record PopupReferenceCase(string Name, UiRect Target, UiVector2 Size, UiRect Boundary,
    UiPopupPositioning Options, UiPopupLayout Expected);

[JsonSerializable(typeof(PopupReferenceCase[]))]
[JsonSerializable(typeof(PopupMeasuredCase[]))]
internal partial class PopupReferenceJson : JsonSerializerContext;

internal static class PopupCollisionTests
{
    private static PopupReferenceCase[] ReadCases(string fixture = "PopupPositioningReference.json")
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", fixture);
        return JsonSerializer.Deserialize(File.ReadAllText(path), PopupReferenceJson.Default.PopupReferenceCaseArray)
            ?? throw new InvalidOperationException("Positioning reference fixture is missing.");
    }

    internal static void Run(Action<bool, string> check)
    {
        var cases = ReadCases();
        check(cases.Length == 768, "popup upstream fixture covers 768 locked-version reference cases");
        CheckCases(cases, check);
    }

    internal static void RunOffsets(Action<bool, string> check)
    {
        var cases = ReadCases("PopupOffsetCoverReference.json");
        check(cases.Length == 1536, "popup upstream fixture covers 1536 offset/cover reference cases");
        CheckCases(cases, check);
    }

    private static void CheckCases(PopupReferenceCase[] cases, Action<bool, string> check)
    {
        foreach (var item in cases)
        {
            var actual = UiPopupPlacement.Arrange(item.Target, item.Size, item.Boundary, item.Options);
            var expected = item.Expected;
            var equal = Matches(actual, expected);
            check(equal, item.Name + (equal ? string.Empty : " expected : " + expected + " actual : " + actual));
        }
    }

    internal static bool Matches(UiPopupLayout actual, UiPopupLayout expected)
    {
        var equal = actual.Position == expected.Position && Near(actual.Bounds, expected.Bounds)
            && Near(actual.ArrowCenterOffset, expected.ArrowCenterOffset)
            && actual.Arrow.HasValue == expected.Arrow.HasValue;
        if (actual.Arrow is { } arrow && expected.Arrow is { } reference)
        {
            equal &= Near(arrow.Tip, reference.Tip) && Near(arrow.BaseStart, reference.BaseStart)
                && Near(arrow.BaseEnd, reference.BaseEnd);
        }
        return equal;
    }

    internal static void Render(Func<string, Action<UiImmediateContext>, byte[]> capture,
        Action<UiInputState> setInput, Action<bool, string> check)
    {
        RenderCases(ReadCases(), [1, 7, 9, 34, 48, 49, 61, 67], "popup-upstream-", capture, setInput, check);
    }

    internal static void RenderOffsets(Func<string, Action<UiImmediateContext>, byte[]> capture,
        Action<UiInputState> setInput, Action<bool, string> check)
    {
        RenderCases(ReadCases("PopupOffsetCoverReference.json"), [2, 7, 12, 14, 20, 25, 35, 45, 48, 58, 59, 60],
            "popup-offset-cover-", capture, setInput, check);
    }

    private static void RenderCases(PopupReferenceCase[] cases, int[] indices, string prefix,
        Func<string, Action<UiImmediateContext>, byte[]> capture, Action<UiInputState> setInput, Action<bool, string> check)
    {
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        foreach (var index in indices)
        {
            var item = cases[index];
            Surface.Size = item.Size;
            using var tooltip = new UiTooltip<UiHoverFocusTooltipBehavior, Surface>(
                new UiButton<UiReleaseButtonBehavior, Button>("Anchor") { Size = new(item.Target.Width, item.Target.Height) },
                "Reference", new() { ShowDelaySeconds = 0, Positioning = item.Options });
            setInput(idle);
            var key = prefix + index;
            var expected = capture(key + "-expected", ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding : 0);
                var context = new UiControlContext(ui);
                context.Frame(item.Target, new(20, 40, 80), new(0, 0, 0, 0), 0, 0);
                Paint(context, item.Expected);
            });
            setInput(idle with { MousePosition = new(item.Target.X + item.Target.Width * 0.5f, item.Target.Y + item.Target.Height * 0.5f) });
            var actual = capture(key, ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding : 0);
                ui.SetCursorScreenPos(new(item.Target.X, item.Target.Y));
                tooltip.Render(ui);
                tooltip.Options = default;
            });
            check(actual.AsSpan().SequenceEqual(expected), "popup actual owner consumes upstream fixture : " + key);
        }
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

    private readonly struct Surface : IUiTooltipPresentation
    {
        internal static UiVector2 Size;
        public static UiPopupLayout Arrange(UiControlContext context, string text, UiRect target, UiRect viewport,
            UiTooltipOptions options) => UiPopupPlacement.Arrange(target, Size, viewport, options.Positioning);
        public static void Paint(UiControlContext context, string text, UiPopupLayout layout) => PopupCollisionTests.Paint(context, layout);
    }

    private static bool Near(float a, float b) => MathF.Abs(a - b) <= 0.001f;
    private static bool Near(UiVector2 a, UiVector2 b) => Near(a.X, b.X) && Near(a.Y, b.Y);
    private static bool Near(UiRect a, UiRect b) => Near(a.X, b.X) && Near(a.Y, b.Y)
        && Near(a.Width, b.Width) && Near(a.Height, b.Height);
}
