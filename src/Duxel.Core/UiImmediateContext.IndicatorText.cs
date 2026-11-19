namespace Duxel.Core;

public readonly partial struct UiControlContext
{
    internal void TypographyIndicatorText(string text, UiRect bounds, UiVector2 textSize, UiColor color) =>
        _ui.PaintTypographyIndicatorText(text, bounds, textSize, color);

    internal float IndicatorBlockY(in UiControlTextBlock block, float indicatorCenter, float naturalY) =>
        _ui.AlignIndicatorBlockY(block, indicatorCenter, naturalY);
}

public sealed partial class UiImmediateContext
{
    // Indicator labels align visible glyph ink; line metrics still determine measurement and flow.
    internal void PaintTypographyIndicatorText(string text, UiRect bounds, UiVector2 textSize, UiColor color)
    {
        if (!IsPresentationPass || text.Length == 0)
            return;
        if (_controlTypographyHeight <= 0)
            throw new InvalidOperationException("Indicator text requires its typography scope.");
        var clip = ResolveItemClipRect();
        if (clip.Width <= 0 || clip.Height <= 0)
            return;
        var (inkHeight, originOffset, _) = IndicatorTextInk(text);
        var position = new UiVector2(bounds.X + (bounds.Width - textSize.X) / 2,
            bounds.Y + (bounds.Height - inkHeight) / 2 - originOffset);
        if (!TryRenderDirectText(_builder, text, position, color, clip, _textSettings, out _))
            throw new InvalidOperationException("Indicator text rendering failed.");
    }

    internal float AlignIndicatorBlockY(in UiControlTextBlock block, float centerY, float naturalY)
    {
        if (!IsPresentationPass || block.LineCount == 0 || block.GetLine(0).Text.Length == 0)
            return naturalY;
        var (height, originOffset, baselineOrigin) = IndicatorTextInk(block.GetLine(0).Text);
        var leading = (_controlTypographyHeight - (_controlTypographyMetrics.Ascent + _controlTypographyMetrics.Descent) / _contentScale) / 2;
        return centerY - height / 2 - originOffset - (_controlTypographyMetrics.Ascent - baselineOrigin) / _contentScale - leading;
    }

    private (float Height, float OriginOffset, float BaselineOrigin) IndicatorTextInk(string text)
    {
        if (IsVectorTextLayer())
        {
            var ink = GetVectorTextInk(text, _pushedFontSize);
            return (ink.Height / _contentScale, ink.Top / _contentScale, ink.Baseline);
        }
        if (!TryGetOrCreateDirectText(text, _pushedFontSize, out var entry))
            throw new InvalidOperationException("Indicator glyph metrics are unavailable.");
        return (entry.Height / _contentScale, (entry.FontAscent - entry.Baseline) / _contentScale, entry.FontAscent);
    }
}
