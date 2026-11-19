namespace Duxel.Core;

[UiFluentIcon("star", UiFluentIconStyle.Regular, 20)]
internal readonly partial struct UiRatingStarOutline;
[UiFluentIcon("star", UiFluentIconStyle.Filled, 20)]
internal readonly partial struct UiRatingStarFilled;
[UiFluentIcon("circle", UiFluentIconStyle.Regular, 20)]
internal readonly partial struct UiRatingCircleOutline;
[UiFluentIcon("circle", UiFluentIconStyle.Filled, 20)]
internal readonly partial struct UiRatingCircleFilled;
[UiFluentIcon("square", UiFluentIconStyle.Regular, 20)]
internal readonly partial struct UiRatingSquareOutline;
[UiFluentIcon("square", UiFluentIconStyle.Filled, 20)]
internal readonly partial struct UiRatingSquareFilled;

// Fluent UI64f3279c: RatingItem styles, web brand variants and color aliases.
internal static class UiFluentRatingGlyphs
{
    internal static void Validate(UiRatingShape shape, UiRatingColor color)
    {
        if ((uint)shape > (uint)UiRatingShape.Square)
            throw new ArgumentOutOfRangeException(nameof(shape));
        if ((uint)color > (uint)UiRatingColor.Marigold)
            throw new ArgumentOutOfRangeException(nameof(color));
    }
    internal static float Size(UiRatingSize size) => size switch
    {
        UiRatingSize.Small => 12,
        UiRatingSize.Medium => 16,
        UiRatingSize.Large => 20,
        UiRatingSize.ExtraLarge => 28,
        _ => throw new ArgumentOutOfRangeException(nameof(size))
    };
    internal static (UiColor Selected, UiColor Empty) Colors(UiControlContext context, UiRatingColor color)
    {
        var dark = context.FluentColorScheme == UiFluentColorScheme.Dark;
        var selected = color switch
        {
            UiRatingColor.Neutral => context.Theme.Text,
            UiRatingColor.Brand => dark ? new UiColor(0x47, 0x9e, 0xf5) : new(0x0f, 0x6c, 0xbd),
            UiRatingColor.Marigold => dark ? new UiColor(0xf2, 0xc6, 0x61) : new(0xea, 0xa3, 0),
            _ => throw new ArgumentOutOfRangeException(nameof(color))
        };
        var empty = color switch
        {
            UiRatingColor.Neutral => dark ? new UiColor(0x33, 0x33, 0x33) : new(0xe6, 0xe6, 0xe6),
            UiRatingColor.Brand => dark ? new UiColor(0x08, 0x23, 0x38) : new(0xeb, 0xf3, 0xfc),
            UiRatingColor.Marigold => dark ? new UiColor(0x83, 0x5b, 0) : new(0xf9, 0xe2, 0xae),
            _ => throw new ArgumentOutOfRangeException(nameof(color))
        };
        return (context.IsEnabled ? selected : context.Theme.TextDisabled, empty);
    }
    internal static void Paint(UiControlContext context, UiRect icon, float fill, UiRatingShape shape,
        UiRatingColor color, bool rightToLeft, bool display, UiButtonInteraction interaction = default)
    {
        var colors = Colors(context, color);
        if (fill < 1)
        {
            using var empty = context.Clip(new(rightToLeft ? icon.X : icon.X + icon.Width * fill, icon.Y, icon.Width * (1 - fill), icon.Height));
            PaintShape(context, icon, display ? colors.Empty : colors.Selected, shape, display, interaction);
        }
        if (fill > 0)
        {
            using var selected = context.Clip(new(rightToLeft ? icon.X + icon.Width * (1 - fill) : icon.X, icon.Y, icon.Width * fill, icon.Height));
            PaintShape(context, icon, colors.Selected, shape, true, interaction);
        }
    }
    private static void PaintShape(UiControlContext context, UiRect bounds, UiColor color,
        UiRatingShape shape, bool filled, UiButtonInteraction interaction)
    {
        switch (shape)
        {
            case UiRatingShape.Star:
                if (filled)
                    UiRatingStarFilled.Paint(context, bounds, color, interaction);
                else
                    UiRatingStarOutline.Paint(context, bounds, color, interaction);
                break;
            case UiRatingShape.Circle:
                if (filled)
                    UiRatingCircleFilled.Paint(context, bounds, color, interaction);
                else
                    UiRatingCircleOutline.Paint(context, bounds, color, interaction);
                break;
            case UiRatingShape.Square:
                if (filled)
                    UiRatingSquareFilled.Paint(context, bounds, color, interaction);
                else
                    UiRatingSquareOutline.Paint(context, bounds, color, interaction);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(shape));
        }
    }
}
