namespace Duxel.Core;

[UiFluentIcon("person")]
internal readonly partial struct UiAvatarPersonIcon;

public readonly struct UiFluentAvatarPresentation : IUiAvatarPresentation
{
    public static bool CanRetainMeasurement => true;
    public static bool CanRetainPaint => true;
    private static readonly UiMenuIcon Person = UiMenuIcon.Create<UiAvatarPersonIcon, UiAvatarPersonIcon>();
    public static UiVector2 Measure(UiControlContext context, UiAvatarOptions options)
    {
        var layout = NaturalLayout(options);
        return layout.Extent;
    }

    internal readonly record struct AvatarLayout(UiVector2 Extent, UiRect Body, UiRect Ring, UiRect Badge);
    internal static AvatarLayout NaturalLayout(UiAvatarOptions options)
    {
        var body = options.PixelSize;
        var outset = ActivityOutset(options);
        var badge = options.Presence is { } presence
            ? UiFluentPresenceBadgePresentation.Edge(presence.Resolve(body).Size) : 0;
        // Explicit badges may be larger than the body. Reserve their complete visual union.
        var ring = new UiRect(-outset, -outset, body + outset * 2, body + outset * 2);
        var badgeRect = new UiRect(options.RightToLeft ? 0 : body - badge, body - badge, badge, badge);
        var visual = options.ShowActivityShadow
            ? UiFluentShadow.Resolve(UiFluentColorScheme.Light, ShadowElevation(options)).PaintBounds(ring) : ring;
        var left = MathF.Min(visual.X, badgeRect.X);
        var top = MathF.Min(visual.Y, badgeRect.Y);
        var right = MathF.Max(visual.X + visual.Width, badgeRect.X + badgeRect.Width);
        var bottom = MathF.Max(visual.Y + visual.Height, badgeRect.Y + badgeRect.Height);
        return new(new(right - left, bottom - top), new(-left, -top, body, body),
            ring with { X = ring.X - left, Y = ring.Y - top },
            badgeRect with { X = badgeRect.X - left, Y = badgeRect.Y - top });
    }

    public static UiShadowElevation ShadowElevation(UiAvatarOptions options) => options.PixelSize switch
    {
        <= 28 => UiShadowElevation.Shadow4, <= 48 => UiShadowElevation.Shadow8,
        <= 64 => UiShadowElevation.Shadow16, _ => UiShadowElevation.Shadow28
    };

    public static float ActivityStroke(UiAvatarOptions options) => options.PixelSize <= 48 ? 2 : options.PixelSize <= 64 ? 3 : 4;
    public static float ActivityOutset(UiAvatarOptions options) => options.ShowActivityRing ? ActivityStroke(options) * 2 : 0;

    public static float CornerRadius(UiAvatarOptions options, float edge) => options.Shape == UiAvatarShape.Circular
        ? edge * .5f : MathF.Min(edge * .5f, options.PixelSize <= 24 ? 2 : options.PixelSize <= 48 ? 4 : options.PixelSize <= 72 ? 6 : 8);

    internal static UiControlTypographyScope Typography(UiControlContext context, int size) => size switch
    {
        <= 24 => context.BeginTypography<UiSemiboldTypeface, UiFluentType100>(),
        <= 28 => context.BeginTypography<UiSemiboldTypeface, UiFluentType200>(),
        <= 40 => context.BeginTypography<UiSemiboldTypeface, UiFluentType300>(),
        <= 56 => context.BeginTypography<UiSemiboldTypeface, UiFluentType400>(),
        <= 96 => context.BeginTypography<UiSemiboldTypeface, UiFluentType500>(),
        _ => context.BeginTypography<UiSemiboldTypeface, UiFluentType600>()
    };
    private readonly struct HalfMetrics<T> : IUiTypographyMetrics where T : IUiTypographyMetrics
    {
        public static float FontSize => T.FontSize * .5f;
        public static float LineHeight => T.LineHeight * .5f;
    }
    private static UiControlTypographyScope HalfTypography(UiControlContext context, int size) => size switch
    {
        <= 24 => context.BeginTypography<UiSemiboldTypeface, HalfMetrics<UiFluentType100>>(),
        <= 28 => context.BeginTypography<UiSemiboldTypeface, HalfMetrics<UiFluentType200>>(),
        <= 40 => context.BeginTypography<UiSemiboldTypeface, HalfMetrics<UiFluentType300>>(),
        <= 56 => context.BeginTypography<UiSemiboldTypeface, HalfMetrics<UiFluentType400>>(),
        <= 96 => context.BeginTypography<UiSemiboldTypeface, HalfMetrics<UiFluentType500>>(),
        _ => context.BeginTypography<UiSemiboldTypeface, HalfMetrics<UiFluentType600>>()
    };
    public static void Paint(UiControlContext context, UiRect bounds, UiAvatarContent content, UiAvatarOptions options)
        => PaintCore(context, bounds, content, options, false);
    internal static void PaintGroupSlice(UiControlContext context, UiRect bounds, UiAvatarContent content, UiAvatarOptions options)
        => PaintCore(context, bounds, content, options, true);
    private static void PaintCore(UiControlContext context, UiRect bounds, UiAvatarContent content, UiAvatarOptions options, bool squareEdges)
    {
        var layout = NaturalLayout(options);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        var scale = MathF.Min(bounds.Width / layout.Extent.X, bounds.Height / layout.Extent.Y);
        var origin = new UiVector2(bounds.X + (bounds.Width - layout.Extent.X * scale) * .5f,
            bounds.Y + (bounds.Height - layout.Extent.Y * scale) * .5f);
        UiRect Fit(UiRect rect) => new(origin.X + rect.X * scale, origin.Y + rect.Y * scale,
            rect.Width * scale, rect.Height * scale);
        var body = Fit(layout.Body);
        var ring = Fit(layout.Ring);
        var paint = UiAvatarPalette.Resolve(context.FluentColorScheme, options.Color, content.ColorIndex);
        if (options.ShowActivityShadow)
        {
            var shadow = content.Shadow ?? throw new InvalidOperationException("Avatar shadow painting requires its owned shadow resource.");
            var style = UiFluentShadow.Resolve(context.FluentColorScheme, ShadowElevation(options));
            static UiShadowLayer Scaled(UiShadowLayer layer, float factor) => layer with
            { Offset = new(layer.Offset.X * factor, layer.Offset.Y * factor), BlurRadius = layer.BlurRadius * factor };
            shadow.Options = new(Scaled(style.First, scale), Scaled(style.Second, scale));
            context.BoxShadow(shadow, ring, CornerRadius(options, ring.Width));
        }
        if (options.Presence is { } presence)
        {
            var badgeOptions = presence.Resolve(options.PixelSize);
            var badge = Fit(layout.Badge);
            var gap = badgeOptions.Size is UiBadgeSize.Large or UiBadgeSize.ExtraLarge ? 2 : 1;
            using (context.CircleCutout(new(badge.X + badge.Width * .5f, badge.Y + badge.Height * .5f),
                badge.Width * .5f + gap * scale))
                PaintBody(context, body, ring, content, options, paint, scale, squareEdges);
            UiFluentPresenceBadgePresentation.Paint(context, badge, new(content.PresenceText, default), badgeOptions);
        }
        else PaintBody(context, body, ring, content, options, paint, scale, squareEdges);
    }

    private static void PaintBody(UiControlContext context, UiRect square, UiRect ring,
        UiAvatarContent content, UiAvatarOptions options, UiAvatarPaint paint, float scale, bool squareEdges)
    {
        if (options.ShowActivityRing)
            context.Frame(ring, default, paint.Ring, CornerRadius(options, ring.Width), ActivityStroke(options) * scale);
        var edge = square.Width;
        var radius = squareEdges ? 0 : CornerRadius(options, edge);
        using var clip = context.Clip(square);
        if (content.Image is { } image)
        {
            context.Image(image, square, new(255, 255, 255), radius, UiImageFit.Cover);
            return;
        }
        context.Frame(square, paint.Background, default, radius, 0);
        if (content.Initials.Length > 0)
        {
            using var typography = squareEdges && scale < .75f ? HalfTypography(context, options.PixelSize) : Typography(context, options.PixelSize);
            var textSize = context.MeasureText(content.Initials);
            context.TypographyButtonText(content.Initials, square, textSize, paint.Foreground);
        }
        else
        {
            var iconSize = MathF.Min(edge, (options.PixelSize switch
            {
                <= 16 => 12, <= 24 => 16, <= 40 => 20, <= 48 => 24,
                <= 56 => 28, <= 72 => 32, _ => 48
            }) * (squareEdges ? scale : 1));
            var icon = content.Icon.IsEmpty ? Person : content.Icon;
            icon.Paint(context, new(square.X + (edge - iconSize) * .5f,
                square.Y + (edge - iconSize) * .5f, iconSize, iconSize), paint.Foreground, default);
        }
    }
}

public readonly partial struct UiControlContext
{
    /// <summary>Paints borrowed image pixels without placing another layout or input item.</summary>
    public void Image(UiImage image, UiRect bounds, UiColor tint, float cornerRadius = 0,
        UiImageFit fit = UiImageFit.Stretch) => _ui.PaintImage(image, bounds, tint, cornerRadius, fit);
}
