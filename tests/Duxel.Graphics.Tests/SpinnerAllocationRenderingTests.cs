using Duxel.Core;

internal static class SpinnerAllocationRenderingTests
{
    internal static void Run(
        Func<string, Action<UiImmediateContext>, byte[]> capture,
        Action<bool, string> check)
    {
        foreach (var size in Enum.GetValues<UiSpinnerSize>())
        foreach (var position in Enum.GetValues<UiSpinnerLabelPosition>())
        foreach (var rtl in new[] { false, true })
        {
            var options = new UiSpinnerOptions(size, position, RightToLeft : rtl) { Width = 260 };
            var name = $"allocated-{size}-{position}-rtl{rtl}";
            const string label = "Waiting\nfor data";
            using var box = Dux.SizedBox(
                new UiSpinner<FrozenSpinnerBehavior, UiFluentSpinnerPresentation>(label, options),
                100, 120);

            var actual = capture(name, ui =>
            {
                Root(ui);
                box.Render(ui);
            });
            var expected = capture(name + "-reference", ui =>
            {
                Root(ui);
                Reference(ui, label, options, new(100, 120));
            });
            check(actual.AsSpan().SequenceEqual(expected),
                name + " preserves centered full-size ring, text and 8px gap with exact parent clipping");

            if (size != UiSpinnerSize.Huge || rtl)
                continue;

            box.Options = new(80, 16);
            actual = capture(name + "-short", ui =>
            {
                Root(ui);
                box.Render(ui);
            });
            expected = capture(name + "-short-reference", ui =>
            {
                Root(ui);
                Reference(ui, label, options, new(80, 16));
            });
            check(actual.AsSpan().SequenceEqual(expected),
                name + " clips the natural ring and multiline label inside a short changed root");

            for (var image = 0; image < 4; image++)
            {
                var replay = capture(name + "-replay-" + image, ui =>
                {
                    Root(ui);
                    box.Render(ui);
                });
                check(expected.AsSpan().SequenceEqual(replay),
                    name + " replays changed geometry across frame images " + image);
            }
        }

        using var custom = Dux.SizedBox(new GeneratedAllocatedSpinner(), 80, 96);
        var customPixels = capture("allocated-user-spinner", ui =>
        {
            Root(ui);
            custom.Render(ui);
        });
        var customExpected = capture("allocated-user-spinner-reference", ui =>
        {
            Root(ui);
            var context = new UiControlContext(ui);
            context.Frame(new(0, 0, 80, 96), context.Theme.Button, context.Theme.Border, 0, 1);
        });
        check(customPixels.AsSpan().SequenceEqual(customExpected),
            "a generated user Spinner paints its assigned root through the common path");
    }

    private static void Reference(
        UiImmediateContext ui,
        string label,
        UiSpinnerOptions options,
        UiVector2 root)
    {
        var context = new UiControlContext(ui);
        using var clip = context.Clip(new(0, 0, root.X, root.Y));
        using var font = UiFluentSpinnerPresentation.BeginLabel(context, options, true);
        var text = context.MeasureTextBlock(label);
        var diameter = UiFluentSpinnerPresentation.Diameter(options.Size);
        var vertical = options.LabelPosition is UiSpinnerLabelPosition.Above or UiSpinnerLabelPosition.Below;
        var first = options.LabelPosition is UiSpinnerLabelPosition.Before or UiSpinnerLabelPosition.Above;
        if (!vertical && options.RightToLeft)
            first = !first;

        UiRect ring;
        UiRect labelBounds;
        if (vertical)
        {
            var top = (root.Y - diameter - 8 - text.Size.Y) * .5f;
            ring = new((root.X - diameter) * .5f, top + (first ? text.Size.Y + 8 : 0), diameter, diameter);
            labelBounds = new((root.X - text.Size.X) * .5f, top + (first ? 0 : diameter + 8), text.Size.X, text.Size.Y);
        }
        else
        {
            var left = (root.X - diameter - 8 - text.Size.X) * .5f;
            ring = new(left + (first ? text.Size.X + 8 : 0), (root.Y - diameter) * .5f, diameter, diameter);
            labelBounds = new(left + (first ? 0 : diameter + 8), (root.Y - text.Size.Y) * .5f, text.Size.X, text.Size.Y);
        }

        var state = default(UiFluentSpinnerBehavior);
        var frame = UiFluentSpinnerBehavior.Evaluate(ref state, options, 0);
        UiFluentSpinnerPresentation.Paint(context, options, frame, new(root, ring, labelBounds, text), default);
    }

    private static void Root(UiImmediateContext ui)
    {
        ui.EnableRootViewportContentLayout(contentPadding : 0);
        ui.SetCursorScreenPos(default);
    }
}

internal struct FrozenSpinnerBehavior : IUiSpinnerBehavior<FrozenSpinnerBehavior>
{
    public static bool IsVisible(in FrozenSpinnerBehavior state, UiSpinnerOptions options, double time) => true;

    public static UiSpinnerFrame Evaluate(ref FrozenSpinnerBehavior state, UiSpinnerOptions options, double time)
    {
        var reference = default(UiFluentSpinnerBehavior);
        return UiFluentSpinnerBehavior.Evaluate(ref reference, options, 0);
    }
}
