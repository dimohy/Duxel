using Duxel.Core;

internal static class PopupPositioningTests
{
    internal readonly record struct Example(UiPopupPosition Position, bool Rtl, UiRect Bounds, UiPopupArrow Arrow);

    internal static IEnumerable<Example> Examples()
    {
        UiRect[] leftToRight =
        [
            new(120, 62, 80, 28), new(140, 62, 80, 28), new(100, 62, 80, 28),
            new(120, 134, 80, 28), new(140, 134, 80, 28), new(100, 134, 80, 28),
            new(50, 98, 80, 28), new(50, 99, 80, 28), new(50, 97, 80, 28),
            new(190, 98, 80, 28), new(190, 99, 80, 28), new(190, 97, 80, 28)
        ];
        int[] rtlIndices = [0, 2, 1, 3, 5, 4, 9, 10, 11, 6, 7, 8];
        for (var rtl = 0; rtl < 2; rtl++)
        {
            for (var i = 0; i < 12; i++)
            {
                var index = rtl == 0 ? i : rtlIndices[i];
                const int sideCenter = 112;
                var arrow = (index / 3) switch
                {
                    0 => new UiPopupArrow(new(160, 96), new(154, 90), new(166, 90)),
                    1 => new UiPopupArrow(new(160, 128), new(154, 134), new(166, 134)),
                    2 => new UiPopupArrow(new(136, sideCenter), new(130, sideCenter - 6), new(130, sideCenter + 6)),
                    _ => new UiPopupArrow(new(184, sideCenter), new(190, sideCenter - 6), new(190, sideCenter + 6))
                };
                yield return new((UiPopupPosition)i, rtl != 0, leftToRight[index], arrow);
            }
        }
    }

    internal static void Run(Action<bool, string> check)
    {
        foreach (var item in Examples())
        {
            var layout = UiPopupPlacement.Arrange(new(140, 100, 40, 24), new(80, 28), new(0, 0, 320, 240),
                new() { Position = item.Position, RightToLeft = item.Rtl, ArrowHeight = 6 });
            check(layout == new UiPopupLayout(item.Bounds, item.Position, item.Arrow),
                "popup literal placement and arrow : " + item.Position + " / RTL " + item.Rtl);
        }
        var boundary = new UiRect(0, 0, 320, 240);
        var top = UiPopupPlacement.Arrange(new(140, 0, 40, 24), new(80, 28), boundary);
        check(top.Bounds == new UiRect(120, 28, 80, 28) && top.Position == UiPopupPosition.Below,
            "popup top boundary flips below");
        var bottom = UiPopupPlacement.Arrange(new(140, 216, 40, 24), new(80, 28), boundary,
            new() { Position = UiPopupPosition.Below });
        check(bottom.Bounds == new UiRect(120, 184, 80, 28) && bottom.Position == UiPopupPosition.Above,
            "popup bottom boundary flips above");
        var left = UiPopupPlacement.Arrange(new(0, 100, 40, 24), new(80, 28), boundary,
            new() { Position = UiPopupPosition.Before });
        check(left.Bounds == new UiRect(44, 98, 80, 28) && left.Position == UiPopupPosition.After,
            "popup left boundary flips after");
        var right = UiPopupPlacement.Arrange(new(280, 100, 40, 24), new(80, 28), boundary,
            new() { Position = UiPopupPosition.After });
        check(right.Bounds == new UiRect(196, 98, 80, 28) && right.Position == UiPopupPosition.Before,
            "popup right boundary flips before");
        var shifted = UiPopupPlacement.Arrange(new(0, 100, 10, 24), new(80, 28), boundary,
            new() { ArrowHeight = 6 });
        check(shifted.Bounds == new UiRect(0, 62, 80, 28)
            && shifted.Arrow == new UiPopupArrow(new(14, 96), new(8, 90), new(20, 90)),
            "popup shifts across edge and clamps arrow inside padding");
        var pinned = UiPopupPlacement.Arrange(new(140, 0, 40, 24), new(80, 28), boundary, new() { Pinned = true });
        check(pinned.Bounds == new UiRect(120, -32, 80, 28) && pinned.Position == UiPopupPosition.Above,
            "popup pinned preserves side without shrinking");
        var oversized = UiPopupPlacement.Arrange(new(140, 100, 40, 24), new(400, 300), boundary);
        check(oversized.Bounds.Width == 400 && oversized.Bounds.Height == 300 && oversized.Bounds.X == 0,
            "popup oversized surface retains requested size");
        var translated = UiPopupPlacement.Arrange(new(240, 150, 40, 24), new(80, 28), new(100, 50, 320, 240));
        check(translated.Bounds == new UiRect(220, 118, 80, 28), "popup nonzero viewport origin");
        foreach (var options in new UiPopupPositioning[]
        {
            new() { Position = (UiPopupPosition)12 }, new() { Gap = float.NegativeInfinity }, new() { Gap = float.NaN },
            new() { ArrowHeight = float.PositiveInfinity }, new() { ArrowPadding = -1 }
        })
        {
            var rejected = false;
            try
            {
                UiPopupPlacement.Arrange(new(140, 100, 40, 24), new(80, 28), boundary, options);
            }
            catch (ArgumentOutOfRangeException)
            {
                rejected = true;
            }
            check(rejected, "popup rejects invalid positioning : " + options);
        }
    }

    internal static void Render(Func<string, Action<UiImmediateContext>, byte[]> capture,
        Action<UiInputState> setInput, Action<bool, string> check)
    {
        var idle = new UiInputState(new(-100, -100), false, false, false, false, false, false, 0, 0, [], [], default, default);
        foreach (var item in Examples())
        {
            var options = new UiTooltipOptions
            {
                ShowDelaySeconds = 0,
                Positioning = new() { Position = item.Position, RightToLeft = item.Rtl, ArrowHeight = 6 }
            };
            using var tooltip = new UiTooltip<UiHoverFocusTooltipBehavior, Surface>(
                new UiButton<UiReleaseButtonBehavior, Button>("Anchor") { Size = new(40, 24) }, "Popup", options);
            var key = "popup-position-" + item.Position + "-rtl-" + item.Rtl;
            setInput(idle);
            var expected = capture(key + "-expected", ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding : 0);
                var context = new UiControlContext(ui);
                context.Frame(new(140, 100, 40, 24), new(20, 40, 80), new(0, 0, 0, 0), 0, 0);
                context.Frame(item.Bounds, new(220, 240, 255), new(0, 0, 0, 0), 0, 0);
                context.Triangle(item.Arrow.Tip, item.Arrow.BaseStart, item.Arrow.BaseEnd, new(220, 240, 255));
            });
            setInput(idle with { MousePosition = new(150, 110) });
            var actual = capture(key, ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding : 0);
                ui.SetCursorScreenPos(new(140, 100));
                tooltip.Render(ui);
                // A late change must not replace this frame's captured geometry options.
                tooltip.Options = new() { Positioning = new() { Position = UiPopupPosition.Below } };
            });
            check(actual.AsSpan().SequenceEqual(expected), "popup tooltip consumes literal placement and snapshot : " + key);
        }
        setInput(idle);
    }

    private readonly struct Button : IUiButtonPresentation
    {
        public static UiVector2 Measure(UiControlContext context, UiVector2 textSize, UiVector2 requestedSize) => requestedSize;
        public static void Paint(UiControlContext context, string text, UiVector2 textSize, UiRect bounds,
            UiButtonInteraction interaction) => context.Frame(bounds, new(20, 40, 80), new(0, 0, 0, 0), 0, 0);
    }

    private readonly struct Surface : IUiTooltipPresentation
    {
        public static UiPopupLayout Arrange(UiControlContext context, string text, UiRect target, UiRect viewport,
            UiTooltipOptions options) => UiPopupPlacement.Arrange(target, new(80, 28), viewport, options.Positioning);
        public static void Paint(UiControlContext context, string text, UiPopupLayout layout)
        {
            context.Frame(layout.Bounds, new(220, 240, 255), new(0, 0, 0, 0), 0, 0);
            var arrow = layout.Arrow!.Value;
            context.Triangle(arrow.Tip, arrow.BaseStart, arrow.BaseEnd, new(220, 240, 255));
        }
    }
}
