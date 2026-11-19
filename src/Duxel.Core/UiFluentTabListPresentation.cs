namespace Duxel.Core;

public readonly partial struct UiFluentTabListPresentation : IUiTabListPresentation
{
    public static bool CanRetainMeasurement => true;
    private static UiVector2 TextSize<TFace>(UiControlContext context, string label, UiTabListSize size) where TFace : IUiTypographyTypeface => size switch
    {
        UiTabListSize.Small => context.MeasureTypography<TFace, UiFluentType300>(label),
        UiTabListSize.Large => context.MeasureTypography<TFace, UiFluentType400>(label),
        UiTabListSize.Medium => context.MeasureTypography<TFace, UiFluentType300>(label),
        _ => throw new ArgumentOutOfRangeException(nameof(size))
    };
    private static UiControlTypographyScope BeginText<TFace>(UiControlContext context, UiTabListSize size) where TFace : IUiTypographyTypeface => size switch
    {
        UiTabListSize.Small => context.BeginTypography<TFace, UiFluentType300>(),
        UiTabListSize.Large => context.BeginTypography<TFace, UiFluentType400>(),
        UiTabListSize.Medium => context.BeginTypography<TFace, UiFluentType300>(),
        _ => throw new ArgumentOutOfRangeException(nameof(size))
    };
    private static UiVector2 Padding(UiTabListOptions options)
    {
        if (options.Circular)
            return new(options.Size == UiTabListSize.Small ? 6 : 10,
                options.Size == UiTabListSize.Small ? 1 : options.Size == UiTabListSize.Medium ? 5 : 7);
        return options.Size switch
        {
            UiTabListSize.Small => new(6, options.Vertical ? 2 : 6),
            UiTabListSize.Large => new(10, options.Vertical ? 8 : 16),
            UiTabListSize.Medium => new(10, options.Vertical ? 6 : 12),
            _ => throw new ArgumentOutOfRangeException(nameof(options))
        };
    }
    private static UiVector2 PrimaryContentSize(UiControlContext context, UiTabOption item, bool selected, UiTabListOptions options)
    {
        var size = item.IconOnly ? default
            : selected && !options.ReserveSelectedSpace ? TextSize<UiSemiboldTypeface>(context, item.Label, options.Size)
            : TextSize<UiRegularTypeface>(context, item.Label, options.Size);
        if (options.ReserveSelectedSpace && !item.IconOnly)
        {
            var reserved = TextSize<UiSemiboldTypeface>(context, item.Label, options.Size);
            size = new(MathF.Max(size.X, reserved.X), MathF.Max(size.Y, reserved.Y));
        }
        if (!item.IconOnly)
            size = size with
            {
                X = size.X + 4
            };
        if (item.Icon.IsEmpty)
            return size;
        var edge = options.Size == UiTabListSize.Large ? 24 : 20;
        var gap = item.IconOnly ? 0 : options.Size == UiTabListSize.Small ? 2 : 6;
        return new(size.X + edge + gap, MathF.Max(size.Y, edge));
    }
    private static UiVector2 ContentSize(UiControlContext context, UiTabOption item, bool selected, UiTabListOptions options)
    {
        var size = PrimaryContentSize(context, item, selected, options);
        if (item.Badge is not { } badge)
            return size;
        var badgeSize = context.MeasureFluentBadge<UiNoBadgeIcon>(new(badge.Text), badge.Options);
        return new(size.X + badgeSize.X + 6, MathF.Max(size.Y, badgeSize.Y));
    }
    public static UiVector2 MeasureTab(UiControlContext context, string label, bool selected, UiTabListOptions options) =>
        MeasureTab(context, new UiTabOption(string.Empty, label), selected, options);
    public static UiVector2 MeasureTab(UiControlContext context, UiTabOption item, bool selected, UiTabListOptions options)
    {
        var size = ContentSize(context, item, selected, options);
        var padding = Padding(options);
        var border = options.Circular ? 2 : 0;
        return new(size.X + padding.X * 2 + border, size.Y + padding.Y * 2 + border);
    }
    public static void PaintTab(UiControlContext context, string label, UiRect bounds, bool selected,
        UiButtonInteraction interaction, UiTabListOptions options) =>
        PaintTab(context, new UiTabOption(string.Empty, label), bounds, selected, interaction, options);

    public static (UiRect Bounds, UiColor Color)? SelectionIndicator(UiControlContext context,
        UiRect bounds, UiTabListOptions options, UiButtonVisualState state)
        => options.Circular ? null : (IndicatorBounds(bounds, options),
            UiFluentTabListPalette.Resolve(context.FluentColorScheme, state, options.Appearance, true).Indicator);

    private static UiRect IndicatorBounds(UiRect bounds, UiTabListOptions options)
    {
        var inset = options.Size == UiTabListSize.Small ? 6 : 12;
        var thickness = options.Size == UiTabListSize.Small && !options.Vertical ? 2 : 3;
        var verticalInset = options.Size == UiTabListSize.Small ? 4 : options.Size == UiTabListSize.Medium ? 8 : 10;
        return options.Vertical
            ? new(options.RightToLeft ? bounds.X + bounds.Width - thickness : bounds.X, bounds.Y + verticalInset,
                thickness, MathF.Max(0, bounds.Height - verticalInset * 2))
            : new(bounds.X + inset, bounds.Y + bounds.Height - thickness, MathF.Max(0, bounds.Width - inset * 2), thickness);
    }

    public static void PaintTab(UiControlContext context, UiTabOption item, UiRect bounds, bool selected,
        UiButtonInteraction interaction, UiTabListOptions options)
    {
        using var clip = context.Clip(bounds);
        var enabled = context.IsEnabled;
        var state = !enabled ? UiButtonVisualState.Disabled
            : interaction.Held && (interaction.Hovered || interaction.Focused) ? UiButtonVisualState.Pressed
            : interaction.Hovered ? UiButtonVisualState.Hover : UiButtonVisualState.Rest;
        var paint = UiFluentTabListPalette.Resolve(context.FluentColorScheme, state, options.Appearance, selected);
        var radius = options.Circular ? MathF.Min(bounds.Width, bounds.Height) * .5f : 4;
        context.Frame(bounds, paint.Background, paint.Border, radius, options.Circular ? 1 : 0);
        var content = ContentSize(context, item, selected, options);
        var padding = Padding(options);
        var outer = padding.X + (options.Circular ? 1 : 0);
        var start = options.Vertical
            ? options.RightToLeft ? bounds.X + bounds.Width - outer - content.X : bounds.X + outer
            : bounds.X + (bounds.Width - content.X) * .5f;
        var primary = PrimaryContentSize(context, item, selected, options);
        var primaryWidth = primary.X;
        UiRect? badgeBounds = null;
        if (item.Badge is { } badge)
        {
            var badgeSize = context.MeasureFluentBadge<UiNoBadgeIcon>(new(badge.Text), badge.Options);
            if (options.Vertical)
            {
                primaryWidth = MathF.Min(primaryWidth, MathF.Max(0, bounds.Width - 2 * outer - badgeSize.X - 6));
                start = options.RightToLeft ? bounds.X + bounds.Width - outer - primaryWidth : bounds.X + outer;
            }
            else if (options.RightToLeft)
                start += badgeSize.X + 6;
            var badgeX = options.Vertical
                ? options.RightToLeft ? bounds.X + outer : bounds.X + bounds.Width - outer - badgeSize.X
                : options.RightToLeft ? start - badgeSize.X - 6 : start + primaryWidth + 6;
            badgeBounds = new(badgeX, bounds.Y + (bounds.Height - badgeSize.Y) / 2, badgeSize.X, badgeSize.Y);
        }
        var textStart = start;
        var textWidth = primaryWidth;
        using (item.Badge.HasValue ? context.Clip(new(start, bounds.Y, primaryWidth, bounds.Height)) : default)
        {
            if (!item.Icon.IsEmpty)
            {
                var edge = options.Size == UiTabListSize.Large ? 24 : 20;
                var gap = item.IconOnly ? 0 : options.Size == UiTabListSize.Small ? 2 : 6;
                var iconX = options.RightToLeft ? start + primaryWidth - edge : start;
                item.Icon.Paint(context, new(iconX, bounds.Y + (bounds.Height - edge) * .5f, edge, edge), paint.Icon, selected, interaction);
                textWidth -= edge + gap;
                if (!options.RightToLeft)
                    textStart += edge + gap;
            }
            if (!item.IconOnly)
                using (selected ? BeginText<UiSemiboldTypeface>(context, options.Size) : BeginText<UiRegularTypeface>(context, options.Size))
                {
                    var textBounds = new UiRect(textStart + 2, bounds.Y, MathF.Max(0, textWidth - 4), bounds.Height);
                    context.TypographyButtonText(item.Label, textBounds, context.MeasureText(item.Label), paint.Text);
                }
        }
        if (item.Badge is { } status && badgeBounds is { } statusBounds)
            context.PaintFluentBadge(statusBounds, new(status.Text), status.Options);
        if (!options.Circular && (selected ? !context.SeparateTabSelectionIndicator : enabled && interaction.Hovered))
        {
            var indicator = IndicatorBounds(bounds, options);
            var thickness = options.Vertical ? indicator.Width : indicator.Height;
            context.Frame(indicator, paint.Indicator, paint.Indicator, thickness * .5f, 0);
        }
        if (enabled && interaction.FocusVisible)
        {
            context.FocusOutline(bounds, radius);
            if (options.Circular)
            {
                var inner = bounds with
                {
                    X = bounds.X + 1,
                    Y = bounds.Y + 1,
                    Width = MathF.Max(0, bounds.Width - 2),
                    Height = MathF.Max(0, bounds.Height - 2)
                };
                var color = context.FluentColorScheme == UiFluentColorScheme.Dark ? new UiColor(41, 41, 41) : new(255, 255, 255);
                context.Frame(inner, default, color, MathF.Max(0, radius - 1), 1);
            }
        }
    }
}
