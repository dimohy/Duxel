namespace Duxel.Core;

public readonly struct UiFluentRatingDisplayPresentation : IUiRatingDisplayPresentation
{
    public static bool CanRetainMeasurement => true;
    private static UiControlTypographyScope BeginText(UiControlContext context, UiRatingSize size, bool strong)
        => size switch
        {
            UiRatingSize.Small or UiRatingSize.Medium => strong ? context.BeginTypography<UiSemiboldTypeface, UiFluentType200>() : context.BeginTypography<UiRegularTypeface, UiFluentType200>(),
            UiRatingSize.Large => strong ? context.BeginTypography<UiSemiboldTypeface, UiFluentType300>() : context.BeginTypography<UiRegularTypeface, UiFluentType300>(),
            UiRatingSize.ExtraLarge => strong ? context.BeginTypography<UiSemiboldTypeface, UiFluentType400>() : context.BeginTypography<UiRegularTypeface, UiFluentType400>(),
            _ => throw new ArgumentOutOfRangeException(nameof(size))
        };
    public static UiRatingDisplayLayout Layout(UiControlContext context, UiRatingDisplayContent content,
        UiRatingDisplayOptions options, Span<UiRect> items)
    {
        if (items.Length != options.IconCount + 2)
            throw new ArgumentException("Rating display layout requires one slot per icon and two text slots.", nameof(items));
        var size = UiFluentRatingGlyphs.Size(options.Size);
        var gap = options.Size switch
        {
            UiRatingSize.Large => 6,
            UiRatingSize.ExtraLarge => 8,
            _ => 4
        };
        UiControlTextBlock score = default, count = default;
        if (content.ValueText.Length != 0)
        {
            using var typography = BeginText(context, options.Size, true);
            score = context.MeasureTextBlock(content.ValueText);
        }
        if (content.CountText.Length != 0)
        {
            using var typography = BeginText(context, options.Size, false);
            count = context.MeasureTextBlock(content.CountText);
        }
        var available = MathF.Min(context.AvailableWidth, context.LayoutConstraints.MaxWidth);
        var x = 0f;
        var y = 0f;
        var lineHeight = 0f;
        var maxWidth = 0f;
        var start = 0;
        for (var i = 0; i < items.Length; i++)
        {
            var desired = i < options.IconCount ? new UiVector2(size, size) : i == options.IconCount ? score.Size : count.Size;
            if (desired.Y == 0)
            {
                items[i] = default;
                continue;
            }
            var margin = i >= options.IconCount ? gap : 0;
            if (x > 0 && x + margin + desired.X > available)
            {
                CenterLine(items, start, i, lineHeight);
                maxWidth = MathF.Max(maxWidth, x);
                y += lineHeight;
                x = lineHeight = 0;
                start = i;
            }
            items[i] = new(x + margin, y, desired.X, desired.Y);
            x += margin + desired.X;
            lineHeight = MathF.Max(lineHeight, desired.Y);
        }
        CenterLine(items, start, items.Length, lineHeight);
        var root = context.LayoutConstraints.Constrain(new(MathF.Max(maxWidth, x), y + lineHeight));
        if (options.RightToLeft)
            for (var i = 0; i < items.Length; i++)
                items[i] = items[i] with
                {
                    X = root.X - items[i].X - items[i].Width
                };
        return new(root, score, count);
    }
    private static void CenterLine(Span<UiRect> items, int start, int end, float height)
    {
        for (var i = start; i < end; i++)
            if (items[i].Height > 0)
                items[i] = items[i] with
                {
                    Y = items[i].Y + (height - items[i].Height) / 2
                };
    }
    public static void Paint(UiControlContext context, UiRect bounds, UiRatingDisplayContent content,
        UiRatingDisplayOptions options, UiRatingDisplayLayout layout, ReadOnlySpan<UiRect> items)
    {
        var value = MathF.Round((content.Value ?? 0) * 2, MidpointRounding.AwayFromZero) / 2;
        for (var i = 0; i < options.IconCount; i++)
            UiFluentRatingGlyphs.Paint(context, World(items[i], bounds), options.Compact ? 1 : Math.Clamp(value - i, 0, 1),
                options.Shape, options.Color, options.RightToLeft, true);
        var textColor = context.IsEnabled ? context.Theme.Text : context.Theme.TextDisabled;
        if (layout.ValueText.LineCount > 0)
        {
            using var typography = BeginText(context, options.Size, true);
            context.TypographyTextBlock(layout.ValueText, World(items[options.IconCount], bounds), textColor);
        }
        if (layout.CountText.LineCount > 0)
        {
            using var typography = BeginText(context, options.Size, false);
            context.TypographyTextBlock(layout.CountText, World(items[options.IconCount + 1], bounds), textColor);
        }
    }
    private static UiRect World(UiRect item, UiRect bounds) => item with { X = item.X + bounds.X, Y = item.Y + bounds.Y };
}
