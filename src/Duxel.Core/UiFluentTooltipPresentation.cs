// Surface constants and arrow composition follow Fluent UI (MIT, Microsoft Corporation).
// See Duxel.Core.ThirdPartyNotices.txt.
namespace Duxel.Core;

/// <summary>Fluent tooltip surface and Type200 text. The complete Fluent Tooltip control is still separate work.</summary>
public readonly struct UiFluentTooltipPresentation : IUiTooltipPresentation, IUiPopupMeasurement<string>
{
    public static UiPopupLayout Arrange(UiControlContext context, string text, UiRect target, UiRect viewport,
        UiTooltipOptions options) => Arrange<UiConfiguredPopupOffset>(context, text, target, viewport, options);

    public static UiPopupLayout Arrange<TOffset>(UiControlContext context, string text, UiRect target, UiRect viewport,
        UiTooltipOptions options) where TOffset : IUiPopupOffset
    {
        var positioning = options.Positioning;
        if (positioning.ArrowHeight != 0 && positioning.ArrowHeight != 6
            || positioning.ArrowHeight != 0 && positioning.ArrowWidth is { } width && width != 8.484f)
        {
            throw new NotSupportedException("The Fluent tooltip arrow uses height 6 and square edge 8.484.");
        }
        positioning = positioning with { ArrowWidth = positioning.ArrowHeight == 0 ? 0 : 8.484f };
        return UiPopupPlacement.ArrangeMeasured<UiFluentTooltipPresentation, string, TOffset>(context, text,
            target, viewport, positioning);
    }

    public static UiPopupContentMeasure Measure(UiControlContext context, string text, UiPopupMeasureRequest request)
    {
        using var typography = context.BeginTypography<UiRegularTypeface, UiFluentType200>();
        var maximumWidth = MathF.Min(240, request.MaxWidth ?? 240);
        var limit = MathF.Min(request.Width ?? maximumWidth, maximumWidth);
        var block = context.MeasureTextBlock(context.WrapText(text, MathF.Max(1, limit - 24)));
        var width = MathF.Max(24, MathF.Min(request.Width ?? block.Size.X + 24, maximumWidth));
        var height = MathF.Max(12, MathF.Min(request.Height ?? block.Size.Y + 12,
            request.MaxHeight ?? float.PositiveInfinity));
        return new(new(width, height), new(MathF.Max(width, block.Size.X + 24), block.Size.Y + 12));
    }

    public static void Paint(UiControlContext context, string text, UiPopupLayout layout) =>
        Paint(context, text, layout, false);

    internal static void Paint(UiControlContext context, string text, UiPopupLayout layout, bool inverted)
    {
        context.FluentTooltipSurface(layout, inverted);
        using var typography = context.BeginTypography<UiRegularTypeface, UiFluentType200>();
        var bounds = layout.Bounds;
        var block = context.MeasureTextBlock(context.WrapText(text, MathF.Max(1, bounds.Width - 24)));
        using var clip = context.Clip(bounds);
        var foreground = inverted || context.FluentColorScheme == UiFluentColorScheme.Dark
            ? new UiColor(255, 255, 255) : new UiColor(36, 36, 36);
        context.TypographyTextBlock(block,
            new(bounds.X + 12 - layout.ScrollOffset.X, bounds.Y + 5 - layout.ScrollOffset.Y,
                MathF.Max(0, bounds.Width - 24), block.Size.Y), foreground);
    }
}

public readonly struct UiFluentInvertedTooltipPresentation : IUiTooltipPresentation
{
    public static UiPopupLayout Arrange(UiControlContext context, string text, UiRect target, UiRect viewport,
        UiTooltipOptions options) => UiFluentTooltipPresentation.Arrange(context, text, target, viewport, options);

    public static void Paint(UiControlContext context, string text, UiPopupLayout layout) =>
        UiFluentTooltipPresentation.Paint(context, text, layout, true);
}
