using System.Numerics;
using Duxel.Core;

internal static class DividerAllocationRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check, bool vector)
    {
        var index = 0;
        foreach (var vertical in new[] { false, true })
        foreach (var alignment in Enum.GetValues<UiDividerAlignment>())
        foreach (var inset in new[] { false, true })
        foreach (var content in new[] { "Section ## literal\nSecond", string.Empty })
        foreach (var size in new[] { new UiVector2(80, 64), new UiVector2(160, 8) })
        {
            var options = new UiDividerOptions(vertical, alignment, (UiDividerAppearance)(index % 4), inset, Length : 220, RightToLeft : index % 3 == 0);
            using var divider = Fluent.Divider(content, options);
            using var box = Dux.SizedBox(divider, size.X, size.Y);
            var key = $"divider-allocated-{vector}-{index++}";
            var actual = capture(key, ui => Scene(ui, vector, key, () => box.Render(ui)));
            var expected = capture(key + "-reference", ui => Scene(ui, vector, key + "-reference", () =>
                Reference(ui, content, options, new(20, 20, size.X, size.Y))));
            check(actual.AsSpan().SequenceEqual(expected), key + " matches independent lines, typography and root clipping");
        }
        using var zero = Dux.SizedBox(Fluent.Divider("Clipped"), 0, 0);
        var zeroPixels = capture("divider-zero-" + vector, ui => Scene(ui, vector, "zero", () => zero.Render(ui)));
        var emptyPixels = capture("divider-zero-reference-" + vector, ui => Scene(ui, vector, "empty", () => { }));
        check(zeroPixels.AsSpan().SequenceEqual(emptyPixels), "Divider paints no pixels at zero allocation, vector = " + vector);

        using var custom = Dux.SizedBox(new GeneratedAllocatedDivider("User"), 80, 64);
        var customPixels = capture("divider-custom-" + vector, ui => Scene(ui, vector, "custom", () => custom.Render(ui)));
        var customReference = capture("divider-custom-reference-" + vector, ui => Scene(ui, vector, "custom-reference", () =>
        {
            var context = new UiControlContext(ui);
            context.Frame(new(20, 20, 80, 64), context.Theme.Button, context.Theme.Border, 0, 1);
        }));
        check(customPixels.AsSpan().SequenceEqual(customReference), "generated custom Divider paints its actual root, vector = " + vector);
    }

    internal static void VerifyRetained(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check, bool vector)
    {
        using var first = Fluent.Divider("FPS : 0", new(Appearance : UiDividerAppearance.Brand));
        using var second = Fluent.Divider("NF : 0");
        using var firstBox = Dux.SizedBox(first, 100, 64);
        using var secondBox = Dux.SizedBox(second, 100, 64);
        var builds = 0;
        var expectedBuilds = 0;
        long version = 0;
        for (var frame = 0; frame < 20; frame++)
        {
            var changed = false;
            if (frame % 16 == 0)
            {
                first.Text = "FPS : " + frame / 16;
                changed = true;
            }
            if (frame % 3 == 0)
            {
                second.Text = "NF : " + frame / 3;
                changed = true;
            }
            if (frame == 8)
            {
                firstBox.Options = firstBox.Options with { Width = 24 };
                changed = true;
            }
            if (frame == 12)
            {
                firstBox.Options = firstBox.Options with { Height = 8 };
                changed = true;
            }
            if (changed)
            {
                version++;
                expectedBuilds++;
            }
            var options = new UiLayerOptions(true, 1, default) { ContentVersion = version };
            if (vector)
                options = options with { Scene3D = new(Matrix4x4.Identity, false) };
            var actual = capture($"divider-retained-{vector}-{frame}", ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding : 0);
                if (ui.BeginLayer("divider-allocation-retained-" + vector, options))
                {
                    builds++;
                    ui.SetCursorScreenPos(new(20, 20));
                    firstBox.Render(ui);
                    ui.SetCursorScreenPos(new(140, 20));
                    secondBox.Render(ui);
                }
                ui.EndLayer();
            });
            var expected = capture($"divider-retained-{vector}-{frame}-reference", ui => Scene(ui, vector, "retained-reference-" + frame, () =>
            {
                Reference(ui, first.Text, first.Options, new(20, 20, firstBox.Options.Width!.Value, firstBox.Options.Height!.Value));
                Reference(ui, second.Text, second.Options, new(140, 20, 100, 64));
            }));
            check(actual.AsSpan().SequenceEqual(expected), $"paced Divider content and clip changes stay exact across acquired frame images; vector = {vector}, frame = {frame}");
        }
        check(builds == expectedBuilds, "unchanged allocated Divider layers retain their content, vector = " + vector);
    }

    private static void Reference(UiImmediateContext ui, string text, UiDividerOptions options, UiRect root)
    {
        var context = new UiControlContext(ui);
        using var clip = context.Clip(root);
        var paint = UiFluentDividerPalette.Resolve(context.FluentColorScheme, options.Appearance);
        var inset = options.Inset ? 12f : 0;
        var major = MathF.Max(0, (options.Vertical ? root.Height : root.Width) - 2 * inset);
        var origin = (options.Vertical ? root.Y : root.X) + inset;
        void Segment(float start, float length)
        {
            if (length <= 0)
                return;
            var rect = options.Vertical ? new UiRect(root.X + (root.Width - 1) / 2, start, 1, length)
                : new UiRect(start, root.Y + (root.Height - 1) / 2, length, 1);
            context.Frame(rect, paint.Line, paint.Line, 0, 0);
        }
        if (text.Length == 0)
        {
            Segment(origin, major);
            return;
        }
        using var font = context.BeginTypography<UiRegularTypeface, UiFluentType200>();
        var normalized = text.Replace("\r", string.Empty, StringComparison.Ordinal);
        if (!options.Vertical)
            normalized = context.WrapText(normalized, MathF.Max(1, major - 40));
        var block = context.MeasureTextBlock(normalized);
        var alignment = options.Alignment;
        if (!options.Vertical && options.RightToLeft)
            alignment = alignment == UiDividerAlignment.Start ? UiDividerAlignment.End
                : alignment == UiDividerAlignment.End ? UiDividerAlignment.Start : alignment;
        var free = major - (options.Vertical ? block.Size.Y : block.Size.X) - 24;
        var before = alignment == UiDividerAlignment.Start ? 8
            : alignment == UiDividerAlignment.End ? MathF.Max(8, free - 8) : MathF.Max(8, free / 2);
        Segment(origin, before);
        Segment(origin + before + 24 + (options.Vertical ? block.Size.Y : block.Size.X), MathF.Max(8, free - before));
        var bounds = options.Vertical ? new UiRect(root.X + (root.Width - block.Size.X) / 2, origin + before + 12, block.Size.X, block.Size.Y)
            : new UiRect(origin + before + 12, root.Y + (root.Height - block.Size.Y) / 2, block.Size.X, block.Size.Y);
        var align = options.Vertical || alignment == UiDividerAlignment.Center ? UiItemHorizontalAlign.Center
            : alignment == UiDividerAlignment.End ? UiItemHorizontalAlign.Right : UiItemHorizontalAlign.Left;
        context.TypographyTextBlock(block, bounds, paint.Content, align);
    }

    private static void Scene(UiImmediateContext ui, bool vector, string id, Action draw)
    {
        ui.EnableRootViewportContentLayout(contentPadding : 0);
        if (vector)
            ui.BeginLayer("divider-allocation-vector-" + id, new(false, 1, default) { Scene3D = new(Matrix4x4.Identity, false) });
        try
        {
            ui.SetCursorScreenPos(new(20, 20));
            draw();
        }
        finally
        {
            if (vector)
                ui.EndLayer();
        }
    }
}
