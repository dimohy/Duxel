using Duxel.Core;

internal static class ProgressAllocationRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check)
    {
        foreach (var shape in Enum.GetValues<UiProgressShape>())
        foreach (var thickness in Enum.GetValues<UiProgressThickness>())
        foreach (var color in Enum.GetValues<UiProgressColor>())
        foreach (var fraction in new[] { 0f, .25f, 1f })
        {
            var value = new UiProgressValue(fraction, Shape : shape, Thickness : thickness, Color : color);
            using var widget = Fluent.ProgressBar(fraction);
            widget.Source = value;
            widget.Size = new(220, 4);
            using var box = Dux.SizedBox(widget, 80, 64);
            var name = $"progress-allocated-{shape}-{thickness}-{color}-{fraction}";
            var actual = capture(name, ui =>
            {
                Root(ui);
                box.Render(ui);
            });
            var expected = capture(name + "-reference", ui =>
            {
                Root(ui);
                Reference(ui, value, new(80, 64), 0);
            });
            check(actual.AsSpan().SequenceEqual(expected),
                name + " fills the assigned root with the correct independent fraction and status color");
        }

        foreach (var shape in Enum.GetValues<UiProgressShape>())
        foreach (var reduced in new[] { false, true })
        {
            var value = new UiProgressValue(null, Shape : shape, ReducedMotion : reduced);
            using var widget = new UiProgress<ClockedProgressPresentation>(null) { Source = value };
            using var box = Dux.SizedBox(widget, 80, 16);
            var name = $"progress-allocated-motion-{shape}-{reduced}";
            var actual = capture(name, ui =>
            {
                Root(ui);
                box.Render(ui);
            });
            var time = ClockedProgressPresentation.Time;
            var expected = capture(name + "-reference", ui =>
            {
                Root(ui);
                Reference(ui, value, new(80, 16), time);
            });
            check(actual.AsSpan().SequenceEqual(expected),
                name + " uses the actual root for its gradient or opacity pulse with exact clipping");
        }

        var reads = 0;
        var target = new UiProgressValue(.25f, Shape : UiProgressShape.Square, Thickness : UiProgressThickness.Large);
        using var dynamic = new UiProgress<Func<UiProgressValue>, UiGetterProgressSource, UiFluentProgressPresentation>(() =>
        {
            reads++;
            return target;
        });
        using var dynamicBox = Dux.SizedBox(dynamic, 80, 64);
        var dynamicActual = capture("progress-allocated-getter", ui =>
        {
            Root(ui);
            dynamicBox.Render(ui);
        });
        var dynamicExpected = capture("progress-allocated-getter-reference", ui =>
        {
            Root(ui);
            Reference(ui, target, new(80, 64), 0);
        });
        check(reads == 1 && dynamicActual.AsSpan().SequenceEqual(dynamicExpected),
            "getter-driven allocated Progress has independent exact pixels and one render-time source read");

        dynamicBox.Options = new(120, 16);
        target = target with { Value = .75f, Thickness = UiProgressThickness.Medium, Color = UiProgressColor.Error };
        dynamicActual = capture("progress-allocated-getter-updated", ui =>
        {
            Root(ui);
            dynamicBox.Render(ui);
        });
        dynamicExpected = capture("progress-allocated-getter-updated-reference", ui =>
        {
            Root(ui);
            Reference(ui, target, new(120, 16), 0);
        });
        check(reads == 2 && dynamicActual.AsSpan().SequenceEqual(dynamicExpected),
            "live getter values and parent sizes update the actual fill without a stale measurement");
        for (var frame = 0; frame < 4; frame++)
        {
            var replay = capture("progress-allocated-getter-replay-" + frame, ui =>
            {
                Root(ui);
                dynamicBox.Render(ui);
            });
            check(dynamicExpected.AsSpan().SequenceEqual(replay),
                "allocated Progress geometry remains exact across acquired frame images " + frame);
        }
        check(reads == 6, "replayed getter Progress keeps one read per actual rendered frame");

        using var custom = Dux.SizedBox(new GeneratedAllocatedProgress(.5f), 80, 64);
        var customActual = capture("progress-allocated-custom", ui =>
        {
            Root(ui);
            custom.Render(ui);
        });
        var customExpected = capture("progress-allocated-custom-reference", ui =>
        {
            Root(ui);
            var context = new UiControlContext(ui);
            context.Frame(new(0, 0, 80, 64), context.Theme.Button, context.Theme.Border, 0, 1);
        });
        check(customActual.AsSpan().SequenceEqual(customExpected),
            "a generated user Progress paints independently assigned bounds through its static presentation");
    }

    private static void Reference(UiImmediateContext ui, UiProgressValue value, UiVector2 size, double time)
    {
        var context = new UiControlContext(ui);
        var bounds = new UiRect(0, 0, size.X, size.Y);
        using var clip = context.Clip(bounds);
        var background = context.Theme.ProgressBarBg;
        var fill = value.Fraction is null ? context.Theme.ProgressBarFill : value.Color switch
        {
            UiProgressColor.Brand => context.Theme.ProgressBarFill,
            UiProgressColor.Success => new UiColor(0x10, 0x7c, 0x10),
            UiProgressColor.Warning => new UiColor(0xda, 0x3b, 0x01),
            UiProgressColor.Error => new UiColor(0xd1, 0x34, 0x38),
            _ => throw new ArgumentOutOfRangeException(nameof(value))
        };
        var radius = value.Shape == UiProgressShape.Rounded ? 4 : 0;
        if (value.Fraction is { } fraction)
        {
            context.Frame(bounds, background, background, radius, 0);
            if (fraction > 0)
                context.Frame(bounds with { Width = size.X * fraction }, fill, fill, radius, 0);
            return;
        }

        var phase = (float)(time % 3 / 3);
        if (value.ReducedMotion)
        {
            context.Frame(bounds, background, background, radius, 0);
            var opacity = .2f + .8f * (1 - MathF.Abs(phase * 2 - 1));
            var pulsed = new UiColor((fill.Rgba & 0x00ffffff) | ((uint)MathF.Round((fill.Rgba >> 24) * opacity) << 24));
            context.Frame(bounds, pulsed, pulsed, radius, 0);
        }
        else
        {
            var width = size.X * .33f;
            context.ProgressGradient(new(width * (-1 + 4 * phase), 0, width, size.Y), bounds, background, fill, radius);
        }
    }

    private static void Root(UiImmediateContext ui)
    {
        ui.EnableRootViewportContentLayout(contentPadding : 0);
        ui.SetCursorScreenPos(default);
    }
}

internal readonly struct ClockedProgressPresentation : IUiProgressPresentation
{
    internal static double Time;
    public static UiVector2 Measure(UiControlContext context, UiProgressValue value, UiVector2 size) =>
        UiFluentProgressPresentation.Measure(context, value, size);
    public static UiVector2 Measure(UiControlContext context, UiVector2 size) => UiFluentProgressPresentation.Measure(context, size);
    public static UiVector2 Arrange(UiControlContext context, UiVector2 natural, UiVector2 allocated) => allocated;
    public static void Paint(UiControlContext context, UiProgressValue value, UiRect bounds)
    {
        Time = context.TimeSeconds;
        UiFluentProgressPresentation.Paint(context, value, bounds);
    }
}
