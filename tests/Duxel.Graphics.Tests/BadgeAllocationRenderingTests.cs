using System.Numerics;
using Duxel.Core;

internal static class BadgeAllocationRenderingTests
{
    internal static void Run(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check, bool vector)
    {
        Compare<UiNoBadgeIcon>("plain", capture, check, vector);
        Compare<BadgeValidationIcon>("icon", capture, check, vector);
        using var counter = Fluent.CounterBadge();
        using var box = Dux.SizedBox(counter, 80, 64);
        var cache = new UiBadgeTextCache();
        var index = 0;
        foreach (var value in new[] { new UiCounterBadgeValue(), new(0, ShowZero : true), new(108), new(0, Dot : true) })
        {
            counter.Source = value;
            var content = UiCounterBadgeBehavior.Resolve(value, ref cache);
            var key = $"badge-counter-{vector}-{index++}";
            var actual = capture(key, ui => Scene(ui, vector, key, () => box.Render(ui)));
            var expected = capture(key + "-reference", ui => Scene(ui, vector, key + "-reference", () =>
                Reference<UiNoBadgeIcon>(ui, content, counter.Options, new(20, 20, 80, 64))));
            check(actual.AsSpan().SequenceEqual(expected), key + " preserves hidden/show-zero/overflow/dot paint in an allocated root");
        }
        using var custom = Dux.SizedBox(new GeneratedAllocatedBadge("User"), 80, 64);
        var customPixels = capture("badge-custom-" + vector, ui => Scene(ui, vector, "custom", () => custom.Render(ui)));
        var customReference = capture("badge-custom-reference-" + vector, ui => Scene(ui, vector, "custom-reference", () =>
        {
            var context = new UiControlContext(ui);
            context.Frame(new(20, 20, 80, 64), context.Theme.Button, context.Theme.Border, 0, 1);
        }));
        check(customPixels.AsSpan().SequenceEqual(customReference), "generated custom Badge paints its actual root, vector = " + vector);
    }

    private static void Compare<TIcon>(string name, Func<string, Action<UiImmediateContext>, byte[]> capture,
        Action<bool, string> check, bool vector) where TIcon : IUiBadgeIcon
    {
        var index = 0;
        foreach (var badgeSize in Enum.GetValues<UiBadgeSize>())
        foreach (var size in new[] { new UiVector2(80, 64), new UiVector2(12, 8) })
        {
            var options = new UiFluentBadgeOptions(badgeSize, (UiBadgeShape)(index % 3), (UiBadgeAppearance)(index % 4),
                (UiBadgeColor)(index % 8), (UiBadgeIconPosition)(index % 2));
            var text = TIcon.IsPresent && index % 2 == 1 ? string.Empty : "8";
            var content = new UiBadgeContent(text, AccessibleLabel : "Status");
            using var badge = new UiBadge<string, UiTextBadgeBehavior, UiFluentBadgePresentation<TIcon>>(text, options) { AccessibleLabel = "Status" };
            using var box = Dux.SizedBox(badge, size.X, size.Y);
            var key = $"badge-allocated-{name}-{vector}-{index++}";
            var actual = capture(key, ui => Scene(ui, vector, key, () => box.Render(ui)));
            var expected = capture(key + "-reference", ui => Scene(ui, vector, key + "-reference", () =>
                Reference<TIcon>(ui, content, options, new(20, 20, size.X, size.Y))));
            check(actual.AsSpan().SequenceEqual(expected), key + " matches independent frame, fixed glyph/icon metrics and root clipping");
        }
        using var zero = Dux.SizedBox(new UiBadge<string, UiTextBadgeBehavior, UiFluentBadgePresentation<TIcon>>("Clipped"), 0, 0);
        var zeroPixels = capture($"badge-zero-{name}-{vector}", ui => Scene(ui, vector, name + "-zero", () => zero.Render(ui)));
        var emptyPixels = capture($"badge-zero-{name}-{vector}-reference", ui => Scene(ui, vector, name + "-empty", () => { }));
        check(zeroPixels.AsSpan().SequenceEqual(emptyPixels), name + " paints no frame, glyph or icon at zero allocation, vector = " + vector);
    }

    internal static void VerifyRetained(Func<string, Action<UiImmediateContext>, byte[]> capture, Action<bool, string> check, bool vector)
    {
        using var first = Fluent.Badge("FPS : 0");
        using var second = Fluent.CounterBadge();
        using var firstBox = Dux.SizedBox(first, 100, 64);
        using var secondBox = Dux.SizedBox(second, 100, 64);
        var cache = new UiBadgeTextCache();
        long version = 0;
        var builds = 0;
        var expectedBuilds = 0;
        for (var frame = 0; frame < 20; frame++)
        {
            var changed = false;
            if (frame % 16 == 0)
            {
                first.Source = "FPS : " + frame / 16;
                changed = true;
            }
            if (frame % 3 == 0)
            {
                second.Count = frame / 3;
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
            var actual = capture($"badge-retained-{vector}-{frame}", ui =>
            {
                ui.EnableRootViewportContentLayout(contentPadding : 0);
                if (ui.BeginLayer("allocated-badge-retained-" + vector, options))
                {
                    builds++;
                    ui.SetCursorScreenPos(new(20, 20));
                    firstBox.Render(ui);
                    ui.SetCursorScreenPos(new(140, 20));
                    secondBox.Render(ui);
                }
                ui.EndLayer();
            });
            var secondContent = UiCounterBadgeBehavior.Resolve(second.Source, ref cache);
            var expected = capture($"badge-retained-{vector}-{frame}-reference", ui => Scene(ui, vector, "retained-reference-" + frame, () =>
            {
                Reference<UiNoBadgeIcon>(ui, new(first.Source), first.Options, new(20, 20, firstBox.Options.Width!.Value, firstBox.Options.Height!.Value));
                Reference<UiNoBadgeIcon>(ui, secondContent, second.Options, new(140, 20, 100, 64));
            }));
            check(actual.AsSpan().SequenceEqual(expected), $"paced Badge/counter content and clip changes remain exact across acquired images; vector = {vector}, frame = {frame}");
        }
        check(builds == expectedBuilds, "unchanged allocated Badge layers retain content, vector = " + vector);
    }

    private static void Reference<TIcon>(UiImmediateContext ui, UiBadgeContent content, UiFluentBadgeOptions options, UiRect root) where TIcon : IUiBadgeIcon
    {
        switch (options.Size)
        {
            case UiBadgeSize.Tiny:
                Reference<TIcon, TinyMetrics>(ui, content, options, root);
                break;
            case UiBadgeSize.ExtraSmall:
                Reference<TIcon, ExtraSmallMetrics>(ui, content, options, root);
                break;
            case UiBadgeSize.Small:
                Reference<TIcon, UiFluentType100>(ui, content, options, root);
                break;
            default:
                Reference<TIcon, UiFluentType200>(ui, content, options, root);
                break;
        }
    }

    private static void Reference<TIcon, TMetrics>(UiImmediateContext ui, UiBadgeContent content, UiFluentBadgeOptions options, UiRect root)
        where TIcon : IUiBadgeIcon where TMetrics : IUiTypographyMetrics
    {
        if (!content.Visible || root.Width == 0 || root.Height == 0)
            return;
        var context = new UiControlContext(ui);
        using var clip = context.Clip(root);
        var paint = UiFluentBadgePalette.Resolve(context.Theme.BadgeColorScheme, options.Appearance, options.Color);
        var radius = options.Shape switch
        {
            UiBadgeShape.Circular => root.Height / 2,
            UiBadgeShape.Rounded => options.Size is UiBadgeSize.Tiny or UiBadgeSize.ExtraSmall or UiBadgeSize.Small ? 2 : 4,
            _ => 0
        };
        if ((paint.Background.Rgba >> 24) != 0 || (paint.Border.Rgba >> 24) != 0)
            context.Frame(root, paint.Background, paint.Border, radius, options.Appearance == UiBadgeAppearance.Ghost ? 0 : 1);
        using var font = context.BeginTypography<UiSemiboldTypeface, TMetrics>(content.Text.Length > 0);
        var block = content.Text.Length > 0 ? context.MeasureTextBlock(content.Text) : default;
        var iconSize = UiBadgeMetrics.FromSize(options.Size).IconSize;
        var gap = options.Size == UiBadgeSize.ExtraLarge ? 6 : 4;
        var contribution = TIcon.IsPresent ? iconSize - 2 + (content.Text.Length > 0 ? gap : -2) : 0;
        var start = root.X + (root.Width - block.Size.X - contribution) / 2;
        var textX = start;
        if (TIcon.IsPresent)
        {
            var iconX = options.IconPosition == UiBadgeIconPosition.Before ? start - 2 : start + block.Size.X + (content.Text.Length > 0 ? gap : -2);
            if (options.IconPosition == UiBadgeIconPosition.Before)
                textX += contribution;
            TIcon.Paint(context, new(iconX, root.Y + (root.Height - iconSize) / 2, iconSize, iconSize), paint.Foreground);
        }
        if (content.Text.Length > 0)
            context.TypographyTextBlock(block, new(textX, root.Y + (root.Height - TMetrics.LineHeight) / 2, block.Size.X, block.Size.Y), paint.Foreground);
    }

    private static void Scene(UiImmediateContext ui, bool vector, string id, Action draw)
    {
        ui.EnableRootViewportContentLayout(contentPadding : 0);
        if (vector)
            ui.BeginLayer("badge-allocation-vector-" + id, new(false, 1, default) { Scene3D = new(Matrix4x4.Identity, false) });
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
    private readonly struct TinyMetrics : IUiTypographyMetrics
    {
        public static float FontSize => 4;
        public static float LineHeight => 4;
    }
    private readonly struct ExtraSmallMetrics : IUiTypographyMetrics
    {
        public static float FontSize => 6;
        public static float LineHeight => 6;
    }
}

internal readonly struct CustomAllocatedBadgePresentation : IUiBadgePresentation
{
    public static bool CanArrange => true;
    public static UiVector2 Measure(UiControlContext context, UiBadgeContent content, UiFluentBadgeOptions options) => new(24, 12);
    public static void Render(UiControlContext context, UiBadgeContent content, UiFluentBadgeOptions options)
    {
        var layout = context.PlaceText(new(24, 12));
        context.Frame(new(layout.Position.X, layout.Position.Y, layout.Size.X, layout.Size.Y), context.Theme.Button, context.Theme.Border, 0, 1);
    }
}
[UiWidgetStyle(typeof(UiBadge<,,>), typeof(string), typeof(UiTextBadgeBehavior), typeof(CustomAllocatedBadgePresentation))]
internal partial class GeneratedAllocatedBadge;
