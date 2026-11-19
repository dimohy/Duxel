using Duxel.Core;

internal static class PopupAutoSizeTests
{
    internal readonly struct ContentMeasure : IUiPopupMeasurement<UiVector2>
    {
        public static UiPopupContentMeasure Measure(UiControlContext context, UiVector2 content, UiPopupMeasureRequest request)
        {
            var width = MathF.Max(8, MathF.Min(request.Width ?? content.X, request.MaxWidth ?? float.PositiveInfinity));
            var fullHeight = content.Y * MathF.Ceiling(content.X / width);
            var height = MathF.Max(8, MathF.Min(request.Height ?? fullHeight, request.MaxHeight ?? float.PositiveInfinity));
            return new(new(width, height), new(width, fullHeight));
        }
    }

    internal static UiPopupLayout Arrange(UiControlContext context, PopupMeasuredCase item, UiRect target,
        UiRect boundary, UiPopupPositioning options) => item.DynamicOffset
        ? UiPopupPlacement.ArrangeMeasured<ContentMeasure, UiVector2, PopupMeasuredTests.Offset>(context, item.NaturalSize,
            target, boundary, options, item.Request)
        : UiPopupPlacement.ArrangeMeasured<ContentMeasure, UiVector2>(context, item.NaturalSize, target, boundary, options, item.Request);

    internal static void Run(Action<bool, string> check)
    {
        var cases = PopupMeasuredTests.ReadCases("PopupAutoReference.json");
        check(cases.Length == 768, "popup auto-size oracle contains768 cases");
        using var context = new UiContext(new(1, 1, UiTextureFormat.Rgba8Unorm, [255, 255, 255, 255]), new(1), new(2));
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        context.NewFrame(new(0.01f, new(320, 240), new(1, 1)), idle, new(0, 0, 320, 240), UiTextSettings.Default);
        context.Render(new Screen(ui =>
        {
            ui.EnableRootViewportContentLayout(contentPadding : 0);
            var control = new UiControlContext(ui);
            foreach (var item in cases)
            {
                var actual = Arrange(control, item, item.Target, item.Boundary, item.Options);
                var equal = PopupCollisionTests.Matches(actual, item.Expected)
                    && Near(actual.ContentSize.X, item.Expected.ContentSize.X) && Near(actual.ContentSize.Y, item.Expected.ContentSize.Y)
                    && SameRequest(actual.Measurement, item.Expected.Measurement);
                check(equal, item.Name + (equal ? string.Empty : " expected : " + item.Expected + " actual : " + actual));
            }
            var rejected = false;
            try
            {
                UiPopupPlacement.ArrangeMeasured<OscillatingMeasure, string>(control, "invalid producer",
                    new(120, 100, 80, 24), new(0, 0, 320, 240),
                    new() { AutoSize = UiPopupAutoSize.Both, Pinned = true });
            }
            catch (InvalidOperationException error) when (error.Message.Contains("51-reset", StringComparison.Ordinal))
            {
                rejected = true;
            }
            check(rejected, "popup nonconvergent producer fails explicitly at reset budget");
        }));
        using var data = context.GetDrawData();
    }

    internal static void Render(Func<string, Action<UiImmediateContext>, byte[]> capture,
        Action<UiInputState> setInput, Action<bool, string> check, UiAccessibilityTree accessibility, UiState state)
    {
        var cases = PopupMeasuredTests.ReadCases("PopupAutoReference.json");
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        foreach (var mode in new[] { UiPopupAutoSize.Width, UiPopupAutoSize.Height, UiPopupAutoSize.Both })
        {
            foreach (var dynamic in new[] { false, true })
            {
                foreach (var item in cases.Where(item => item.Options.AutoSize == mode && item.DynamicOffset == dynamic
                    && item.Target.X >= 0 && item.Target.Y >= 0 && item.Target.X + item.Target.Width <= 320
                    && item.Target.Y + item.Target.Height <= 240).Take(2))
                {
                    Surface.Current = item;
                    using var tooltip = new UiTooltip<UiHoverFocusTooltipBehavior, Surface>(
                        new UiButton<UiReleaseButtonBehavior, Button>("Anchor") { Size = new(item.Target.Width, item.Target.Height) },
                        "Auto", new() { ShowDelaySeconds = 0, Positioning = item.Options });
                    var key = item.Name;
                    setInput(idle);
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
                    check(actual.AsSpan().SequenceEqual(expected), "popup auto-size actual owner matches upstream : " + key);
                }
            }
        }
        RenderScroll(capture, setInput, check, idle);
        RenderScrollbar(capture, setInput, check, idle, accessibility, state);
        setInput(idle);
    }

    private static void RenderScrollbar(Func<string, Action<UiImmediateContext>, byte[]> capture,
        Action<UiInputState> setInput, Action<bool, string> check, UiInputState idle, UiAccessibilityTree accessibility, UiState state)
    {
        const string text = "Line zero\nLine one\nLine two\nLine three\nLine four\nLine five\nLine six\nLine seven\nLine eight\nLine nine";
        using var tooltip = new UiTooltip<UiHoverFocusTooltipBehavior, ScrollSurface>(
            new UiButton<UiReleaseButtonBehavior, Button>("Anchor") { Size = new(80, 24) }, text,
            new()
            {
                Positioning = new()
                {
                    Position = UiPopupPosition.BelowStart, Pinned = true, AutoSize = UiPopupAutoSize.Height,
                    OverflowBoundary = new(0, 60, 320, 100)
                }
            }) { Visible = true };
        void Scene(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding : 0);
            ui.SetCursorScreenPos(new(120, 100));
            tooltip.Render(ui);
        }
        setInput(idle);
        var plain = capture("popup-scrollbar-opt-out", Scene);
        tooltip.Options = tooltip.Options with { ShowScrollbars = true };
        var initial = capture("popup-scrollbar-top", Scene);
        var upper = (135 * 320 + 195) * 4;
        var lower = (152 * 320 + 195) * 4;
        check(!initial.AsSpan(upper, 4).SequenceEqual(plain.AsSpan(upper, 4)),
            "opt-in scrollbar paints a thumb over the overflowing viewport");
        setInput(idle with { MousePosition = new(195, 135), LeftMouseDown = true, LeftMousePressed = true });
        _ = capture("popup-scrollbar-capture", Scene);
        setInput(idle with { MousePosition = new(195, 190), LeftMouseDown = true });
        _ = capture("popup-scrollbar-drag", Scene);
        setInput(idle with { LeftMouseReleased = true });
        _ = capture("popup-scrollbar-bottom-release", Scene);
        state.AdvanceTime(1);
        var bottom = capture("popup-scrollbar-bottom", Scene);
        check(initial.AsSpan(upper, 4).SequenceEqual(bottom.AsSpan(lower, 4))
            && initial.AsSpan(lower, 4).SequenceEqual(bottom.AsSpan(upper, 4)),
            "scrollbar thumb and exposed track swap positions at the end of the drag");
        check(!initial.AsSpan((140 * 320 + 125) * 4, 48 * 4).SequenceEqual(bottom.AsSpan((140 * 320 + 125) * 4, 48 * 4)),
            "real clipped tooltip text moves with the scrollbar thumb");
        var targetId = accessibility.Root.First ?? throw new InvalidOperationException("Tooltip target was not published.");
        if (!accessibility.TryGet(targetId, out var target) || target.FirstChild is not { } childId
            || !accessibility.TryGet(childId, out var surface))
        {
            throw new InvalidOperationException("Tooltip viewport was not published.");
        }
        var queued = accessibility.RequestScroll(childId, surface.RuntimeId, UiAccessibilityScrollCommand.ByPercent(-1, 0), wait : false);
        setInput(idle);
        _ = capture("popup-scrollbar-uia-reset-start", Scene);
        state.AdvanceTime(1);
        var restored = capture("popup-scrollbar-uia-reset", Scene);
        check(queued == UiAccessibilityResult.Success && restored.AsSpan().SequenceEqual(initial),
            "accessibility scroll to top restores the exact thumb and text pixels");
    }

    private static void RenderScroll(Func<string, Action<UiImmediateContext>, byte[]> capture,
        Action<UiInputState> setInput, Action<bool, string> check, UiInputState idle)
    {
        const string text = "Line zero\nLine one\nLine two\nLine three\nLine four\nLine five\nLine six\nLine seven\nLine eight\nLine nine";
        using var tooltip = new UiTooltip<UiHoverFocusTooltipBehavior, ScrollSurface>(
            new UiButton<UiReleaseButtonBehavior, Button>("Anchor") { Size = new(80, 24) }, text,
            new()
            {
                ShowDelaySeconds = 0,
                ScrollStep = 16,
                Positioning = new()
                {
                    Position = UiPopupPosition.BelowStart,
                    Pinned = true,
                    AutoSize = UiPopupAutoSize.Height,
                    OverflowBoundary = new(0, 60, 320, 100)
                }
            });
        var expectedFrames = new byte[6][];
        setInput(idle);
        for (var frame = 0; frame < 6; frame++)
        {
            var expectedOffset = frame == 1 ? 16 : frame is 2 or 3 ? 128 : 0;
            var visible = frame != 4;
            expectedFrames[frame] = capture("popup-auto-scroll-" + frame + "-expected", ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding : 0);
                var context = new UiControlContext(ui);
                context.Frame(new(120, 100, 80, 24), new(20, 40, 80), new(0, 0, 0, 0), 0, 0);
                if (visible)
                {
                    PopupMeasuredTests.TextSurface.Paint(context, text,
                        new(new(120, 128, 80, 32), UiPopupPosition.BelowStart) { ScrollOffset = new(0, expectedOffset) });
                }
            });
        }
        for (var frame = 0; frame < 6; frame++)
        {
            setInput(idle with
            {
                MousePosition = frame is 0 or 5 ? new(130, 110) : new(130, 140),
                MouseWheel = frame == 2 ? -1000 : frame is 1 or 3 or 4 ? -1 : 0,
                KeyEvents = frame == 4 ? [new(UiKey.Escape, true, KeyModifiers.None)] : []
            });
            var remainingWheel = 0f;
            var actual = capture("popup-auto-scroll-" + frame, ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding : 0);
                ui.SetCursorScreenPos(new(120, 100));
                tooltip.Render(ui);
                remainingWheel = ui.GetMouseWheel();
            });
            check(actual.AsSpan().SequenceEqual(expectedFrames[frame]), "popup auto-size actual text clips and scrolls : " + frame);
            check(remainingWheel == (frame is 3 or 4 ? -1 : 0), "popup wheel consumes movement and chains at edge/close : " + frame);
        }
    }

    private static void Paint(UiControlContext context, UiPopupLayout layout)
    {
        context.Frame(layout.Bounds, new(220, 240, 255), new(0, 0, 0, 0), 0, 0);
        if (layout.Arrow is { } arrow)
        {
            context.Triangle(arrow.Tip, arrow.BaseStart, arrow.BaseEnd, new(220, 240, 255));
        }
    }

    private readonly struct Surface : IUiTooltipPresentation
    {
        internal static PopupMeasuredCase Current = null!;
        public static UiPopupLayout Arrange(UiControlContext context, string text, UiRect target, UiRect viewport,
            UiTooltipOptions options) => PopupAutoSizeTests.Arrange(context, Current, target, viewport, options.Positioning);
        public static void Paint(UiControlContext context, string text, UiPopupLayout layout) => PopupAutoSizeTests.Paint(context, layout);
    }

    private readonly struct ScrollSurface : IUiTooltipPresentation, IUiPopupMeasurement<string>
    {
        public static UiPopupContentMeasure Measure(UiControlContext context, string text, UiPopupMeasureRequest request) =>
            PopupMeasuredTests.TextSurface.Measure(context, text, request);
        public static UiPopupLayout Arrange(UiControlContext context, string text, UiRect target, UiRect viewport,
            UiTooltipOptions options) => UiPopupPlacement.ArrangeMeasured<ScrollSurface, string>(context, text,
                target, viewport, options.Positioning, new(80));
        public static void Paint(UiControlContext context, string text, UiPopupLayout layout) =>
            PopupMeasuredTests.TextSurface.Paint(context, text, layout);
    }

    private readonly struct Button : IUiButtonPresentation
    {
        public static UiVector2 Measure(UiControlContext context, UiVector2 textSize, UiVector2 requestedSize) => requestedSize;
        public static void Paint(UiControlContext context, string text, UiVector2 textSize, UiRect bounds,
            UiButtonInteraction interaction) => context.Frame(bounds, new(20, 40, 80), new(0, 0, 0, 0), 0, 0);
    }

    private readonly struct OscillatingMeasure : IUiPopupMeasurement<string>
    {
        private static int _phase;
        public static UiPopupContentMeasure Measure(UiControlContext context, string content, UiPopupMeasureRequest request)
        {
            _phase++;
            return new(new UiVector2((_phase & 1) == 0 ? 80 : 160, 16));
        }
    }

    private static bool Near(float a, float b) => MathF.Abs(a - b) <= 0.001f;
    private static bool Near(float? a, float? b) => a.HasValue == b.HasValue && (!a.HasValue || Near(a.Value, b!.Value));
    private static bool SameRequest(UiPopupMeasureRequest a, UiPopupMeasureRequest b) => Near(a.Width, b.Width)
        && Near(a.Height, b.Height) && Near(a.MaxWidth, b.MaxWidth) && Near(a.MaxHeight, b.MaxHeight);

    private sealed class Screen(Action<UiImmediateContext> draw) : UiScreen
    {
        public override void Render(UiImmediateContext ui) => draw(ui);
    }
}
