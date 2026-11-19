namespace Duxel.Core;

internal static class UiPopoverMotion
{
    internal const double DurationSeconds = .4;
    private static readonly UiCubicBezier EntryCurve = new(0, 0, 0, 1);

    internal static (float Opacity, UiVector2 Offset) Sample(double elapsed, UiPopupPosition position, bool rightToLeft)
    {
        var progress = Math.Clamp(elapsed / DurationSeconds, 0, 1);
        var opacity = (float)EntryCurve.Evaluate(progress);
        var distance = 10 * (1 - opacity);
        var direction = position switch
        {
            UiPopupPosition.Above or UiPopupPosition.AboveStart or UiPopupPosition.AboveEnd => new UiVector2(0, 1),
            UiPopupPosition.Below or UiPopupPosition.BelowStart or UiPopupPosition.BelowEnd => new UiVector2(0, -1),
            UiPopupPosition.Before or UiPopupPosition.BeforeTop or UiPopupPosition.BeforeBottom => new UiVector2(rightToLeft ? -1 : 1, 0),
            UiPopupPosition.After or UiPopupPosition.AfterTop or UiPopupPosition.AfterBottom => new UiVector2(rightToLeft ? 1 : -1, 0),
            _ => throw new ArgumentOutOfRangeException(nameof(position))
        };
        return (opacity, new(direction.X * distance, direction.Y * distance));
    }

    internal static UiPopupLayout Translate(UiPopupLayout layout, UiVector2 offset)
    {
        UiVector2 Move(UiVector2 p) => new(p.X + offset.X, p.Y + offset.Y);
        return layout with
        {
            Bounds = layout.Bounds with
            {
                X = layout.Bounds.X + offset.X,
                Y = layout.Bounds.Y + offset.Y
            },
            Arrow = layout.Arrow is { } arrow ? new(Move(arrow.Tip), Move(arrow.BaseStart), Move(arrow.BaseEnd)) : null
        };
    }
}
