using Duxel.Core;

internal static class OverflowRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check, int width)
    {
        var baseline = capture("baseline", static _ => { });
        var red = new UiColor(220, 40, 50);
        var green = new UiColor(40, 210, 80);
        var blue = new UiColor(30, 90, 230);
        var orange = new UiColor(240, 150, 30);
        var overflow = Dux.Overflow([new Box(new(50, 24), red), new Box(new(50, 24), green), new Box(new(50, 24), blue)],
            new Box(new(20, 16), orange), new(Spacing : 10));
        using var root = Dux.SizedBox(overflow, width : 200, height : 60);
        void Render(UiImmediateContext ui)
        {
            ui.EnableRootViewportContentLayout(contentPadding : 0);
            root.Render(ui);
        }
        Case("all-visible", pixels => Pixel(pixels, 25, 30, red) && Pixel(pixels, 85, 30, green)
            && Pixel(pixels, 145, 30, blue) && Pixel(pixels, 190, 30, default));
        root.Options = root.Options with { Width = 150 };
        Case("collapsed-end", pixels => Pixel(pixels, 25, 30, red) && Pixel(pixels, 85, 30, green)
            && Pixel(pixels, 130, 30, orange) && Pixel(pixels, 145, 30, default));
        root.Options = root.Options with { Width = 90 };
        overflow.SetPriorities([0, 0, 2]);
        Case("promoted-last", pixels => Pixel(pixels, 25, 30, blue) && Pixel(pixels, 70, 30, orange)
            && Pixel(pixels, 85, 30, default));
        overflow.Options = overflow.Options with { RightToLeft = true };
        Case("right-to-left", pixels => Pixel(pixels, 65, 30, blue) && Pixel(pixels, 20, 30, orange)
            && Pixel(pixels, 85, 30, blue));
        overflow.Options = new(Spacing : 10);
        overflow.SetPriorities([0, 0, 0]);
        root.Options = root.Options with { Width = 10 };
        Case("clipped-indicator", pixels => Pixel(pixels, 5, 30, orange) && Pixel(pixels, 15, 30, default));
        overflow.Options = overflow.Options with { MinimumVisible = 1 };
        Case("required-child-clipped", pixels => Pixel(pixels, 5, 30, red) && Pixel(pixels, 15, 30, default));
        overflow.Options = new(Spacing : 10);
        root.Options = root.Options with { Width = 0 };
        Case("zero-width-clears", pixels => Pixel(pixels, 5, 30, default) && Pixel(pixels, 85, 30, default));
        root.Options = root.Options with { Width = 200 };
        Case("restored-all", pixels => Pixel(pixels, 25, 30, red) && Pixel(pixels, 85, 30, green) && Pixel(pixels, 145, 30, blue));

        using var vertical = Dux.SizedBox(Dux.VerticalOverflow([new Box(new(30, 40), red), new Box(new(50, 40), green)],
            new Box(new(20, 20), orange), new(Spacing : 4)), width : 70, height : 70);
        var verticalPixels = capture("vertical", ui => { ui.EnableRootViewportContentLayout(contentPadding : 0); vertical.Render(ui); });
        check(Pixel(verticalPixels, 35, 20, red) && Pixel(verticalPixels, 35, 54, orange) && Pixel(verticalPixels, 35, 80, default),
            "actual vertical overflow GPU image matches independent child and indicator positions");

        void Case(string name, Func<byte[], bool> matches)
        {
            var expected = capture(name, Render);
            check(matches(expected), "actual overflow GPU geometry, clipping and removed-content clearing : " + name);
            for (var i = 0; i < 3; i++)
                check(capture(name + "-replay-" + i, Render).AsSpan().SequenceEqual(expected),
                    "actual overflow GPU layout survives acquired-image reuse : " + name + " / " + i);
        }
        bool Pixel(byte[] pixels, int x, int y, UiColor color)
        {
            var index = (y * width + x) * 4;
            for (var channel = 0; channel < 3; channel++)
            {
                var expected = color.Rgba >> 24 == 0 ? baseline[index + channel] : (byte)(color.Rgba >> (channel * 8));
                if (Math.Abs(pixels[index + channel] - expected) > 1) return false;
            }
            return true;
        }
    }
    private sealed class Box(UiVector2 desired, UiColor color) : UiControlWidget
    {
        protected override bool CanRetainMeasurement => true;
        protected override UiVector2 MeasureContent(UiControlContext context) => context.LayoutConstraints.Constrain(desired);
        protected override void RenderControl(UiImmediateContext ui)
        {
            var context = new UiControlContext(ui);
            var size = context.ConstrainToLayout(desired);
            var origin = ui.AllocateLayoutBox(size);
            context.Frame(new(origin.X, origin.Y, size.X, size.Y), color, default, 0, 0);
            ui.FinishLayoutBox(origin, size);
        }
    }
}
