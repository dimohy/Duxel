namespace Duxel.Core;

/// <summary>Persona metrics follow pinned Fluent8830c10b; text/avatar/presence reuse the existing pipelines.</summary>
public readonly struct UiFluentPersonaPresentation : IUiPersonaPresentation
{
    public static bool CanRetainMeasurement => true;
    public static UiAvatarSize AvatarSize(UiPersonaOptions options) => options.Size switch
    {
        UiPersonaSize.ExtraSmall => UiAvatarSize.Size20,
        UiPersonaSize.Small => UiAvatarSize.Size28,
        UiPersonaSize.Medium => UiAvatarSize.Size32,
        UiPersonaSize.Large => UiAvatarSize.Size36,
        UiPersonaSize.ExtraLarge => UiAvatarSize.Size40,
        UiPersonaSize.Huge => UiAvatarSize.Size56,
        _ => throw new ArgumentOutOfRangeException(nameof(options))
    };
    public static UiPresenceBadgeOptions Presence(UiAvatarPresenceOptions presence, UiPersonaOptions options)
        => new(presence.Status, presence.Size ?? (options.Size switch
        {
            UiPersonaSize.ExtraSmall => UiBadgeSize.Tiny,
            UiPersonaSize.Small => UiBadgeSize.ExtraSmall,
            UiPersonaSize.Medium => UiBadgeSize.Small,
            UiPersonaSize.Large => UiBadgeSize.Medium,
            UiPersonaSize.ExtraLarge or UiPersonaSize.Huge => UiBadgeSize.Large,
            _ => throw new ArgumentOutOfRangeException(nameof(options))
        }), presence.OutOfOffice);
    public static UiVector2 MeasurePresence(UiPresenceBadgeOptions presence)
    {
        var edge = UiFluentPresenceBadgePresentation.Edge(presence.Size);
        return new(edge, edge);
    }
    private static float Gap(UiPersonaOptions options) => options.Size switch
    {
        UiPersonaSize.ExtraSmall => 6,
        UiPersonaSize.Small => options.PresenceOnly ? 6 : 8,
        UiPersonaSize.Medium => 8,
        UiPersonaSize.Large or UiPersonaSize.ExtraLarge => 10,
        UiPersonaSize.Huge => 12,
        _ => throw new ArgumentOutOfRangeException(nameof(options))
    };
    private static UiControlTypographyScope Typography(UiControlContext context, UiPersonaOptions options, bool primary, int lines)
    {
        if (primary)
        {
            if (options.Size is UiPersonaSize.ExtraLarge or UiPersonaSize.Huge)
                return context.BeginTypography<UiSemiboldTypeface, UiFluentType400>();
            if (options.PresenceOnly && options.Size == UiPersonaSize.ExtraSmall && lines <= 1)
                return context.BeginTypography<UiRegularTypeface, UiFluentType200>();
        }
        else if (options.PresenceOnly || options.Size != UiPersonaSize.Huge)
            return context.BeginTypography<UiRegularTypeface, UiFluentType200>();
        return context.BeginTypography<UiRegularTypeface, UiFluentType300>();
    }
    public static UiPersonaLayout Measure(UiControlContext context, UiPersonaContent content, UiPersonaOptions options, UiVector2 media)
    {
        var constraints = context.LayoutConstraints.Validate();
        var values = new[] { content.Primary, content.SecondaryText, content.TertiaryText, content.QuaternaryText };
        var lines = values.Count(static text => !string.IsNullOrEmpty(text));
        var blocks = new UiControlTextBlock[4];
        var rects = new UiRect[4];
        var textWidth = 0f;
        var textHeight = 0f;
        for (var i = 0; i < values.Length; i++)
        {
            if (string.IsNullOrEmpty(values[i]))
                continue;
            using var typography = Typography(context, options, i == 0, lines);
            blocks[i] = context.MeasureTextBlock(values[i]!);
            if (i == 1 && textHeight > 0)
                textHeight -= 2;
            rects[i] = new(0, textHeight, blocks[i].Size.X, blocks[i].Size.Y);
            textHeight += blocks[i].Size.Y;
            textWidth = MathF.Max(textWidth, blocks[i].Size.X);
        }
        if (media.X > constraints.MaxWidth && media.X > 0)
        {
            var scale = constraints.MaxWidth / media.X;
            media = new(media.X * scale, media.Y * scale);
        }
        var alignToPrimary = options.PresenceOnly && options.TextAlignment == UiPersonaTextAlignment.Start
            && options.TextPosition != UiPersonaTextPosition.Below
            && options.Size is not (UiPersonaSize.ExtraLarge or UiPersonaSize.Huge) && blocks[0].LineCount > 0;
        var primaryRow = blocks[0].Size.Y;
        if (alignToPrimary && media.Y > primaryRow)
        {
            var extra = media.Y - primaryRow;
            for (var i = 1; i < rects.Length; i++)
            {
                if (blocks[i].LineCount > 0)
                    rects[i] = rects[i] with
                    {
                        Y = rects[i].Y + extra
                    };
            }
            textHeight += extra;
            primaryRow = media.Y;
        }
        var hasMedia = media.X > 0 && media.Y > 0;
        var gap = hasMedia && lines > 0 ? Gap(options) : 0;
        var below = options.TextPosition == UiPersonaTextPosition.Below;
        var natural = below ? new UiVector2(MathF.Max(media.X, textWidth), media.Y + gap + textHeight)
            : new UiVector2(media.X + gap + textWidth, MathF.Max(media.Y, textHeight));
        var size = constraints.Constrain(natural);
        var mediaRect = new UiRect(0, 0, media.X, media.Y);
        float tx, ty, tw;
        if (below)
        {
            mediaRect = mediaRect with
            {
                X = (size.X - media.X) / 2
            };
            tx = 0;
            ty = media.Y + gap;
            tw = size.X;
        }
        else
        {
            gap = MathF.Min(gap, MathF.Max(0, size.X - media.X));
            var before = options.TextPosition == UiPersonaTextPosition.Before;
            tw = MathF.Max(0, size.X - media.X - gap);
            tx = before ? 0 : media.X + gap;
            mediaRect = mediaRect with
            {
                X = before ? size.X - media.X : 0
            };
            ty = options.TextAlignment == UiPersonaTextAlignment.Center ? (size.Y - textHeight) / 2 : 0;
            var my = options.TextAlignment == UiPersonaTextAlignment.Center ? (size.Y - media.Y) / 2 : 0;
            if (alignToPrimary)
                my = (primaryRow - media.Y) / 2;
            mediaRect = mediaRect with
            {
                Y = my
            };
        }
        for (var i = 0; i < rects.Length; i++)
        {
            if (blocks[i].LineCount == 0)
                continue;
            rects[i] = new(tx, ty + rects[i].Y, tw, rects[i].Height);
            if (options.RightToLeft)
                rects[i] = rects[i] with
                {
                    X = size.X - rects[i].X - rects[i].Width
                };
        }
        if (options.RightToLeft)
            mediaRect = mediaRect with
            {
                X = size.X - mediaRect.X - mediaRect.Width
            };
        return new(size, mediaRect, blocks, rects);
    }
    public static void PaintText(UiControlContext context, UiPersonaLayout layout, UiPersonaOptions options, UiVector2 origin)
    {
        var lines = layout.Text.Count(static text => text.LineCount > 0);
        var dark = context.FluentColorScheme == UiFluentColorScheme.Dark;
        var secondary = dark ? new UiColor(214, 214, 214) : new UiColor(66, 66, 66);
        var primary = dark ? new UiColor(255, 255, 255) : new UiColor(36, 36, 36);
        var align = options.TextPosition == UiPersonaTextPosition.Below ? UiItemHorizontalAlign.Center
            : (options.TextPosition == UiPersonaTextPosition.Before) != options.RightToLeft ? UiItemHorizontalAlign.Right : UiItemHorizontalAlign.Left;
        for (var i = 0; i < layout.Text.Length; i++)
        {
            if (layout.Text[i].LineCount == 0)
                continue;
            using var typography = Typography(context, options, i == 0, lines);
            var local = layout.TextBounds[i];
            var bounds = local with
            {
                X = origin.X + local.X,
                Y = origin.Y + local.Y
            };
            using var clip = context.Clip(bounds);
            context.TypographyTextBlock(layout.Text[i], bounds, i == 0 ? primary : secondary, align);
        }
    }
    public static void PaintPresence(UiControlContext context, UiRect bounds, UiPresenceBadgeOptions presence)
        => UiFluentPresenceBadgePresentation.Paint(context, bounds, new(presence.StatusText, default), presence);
}
