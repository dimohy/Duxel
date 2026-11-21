namespace Duxel.Core;

public readonly partial struct UiControlContext
{
    internal float InlineTextLayoutWidth => _ui.InlineTextLayoutWidth;
    internal UiVector2 MeasureInlineText(UiInlineTextSource source, UiVisualTextLines lines, bool wrap)
        => _ui.MeasureInlineText(source, lines, wrap);
}

public sealed partial class UiImmediateContext
{
    internal float InlineTextLayoutWidth => ConstrainLayoutWidth(MathF.Max(0, GetContentRegionAvail().X));

    private UiVector2 PrepareInlineText(UiInlineTextSource source, UiVisualTextLines lines, bool wrap)
    {
        var width = InlineTextLayoutWidth;
        lines.Prepare(this, source.Source, width, float.PositiveInfinity, ControlTypographyLineHeight, 0,
            wrap, styled: source);
        return new(lines.Width, lines.Height);
    }

    internal UiVector2 MeasureInlineText(UiInlineTextSource source, UiVisualTextLines lines, bool wrap)
    {
        using var typography = BeginStyledTypography(source.BaseStyle);
        return ConstrainToLayout(PrepareInlineText(source, lines, wrap));
    }

    internal string? RenderInlineText(UiInlineTextSource source, UiVisualTextLines lines, bool wrap, UiColor? color,
        ref string? identity, string? accessibleText = null, UiItemHorizontalAlign alignment = UiItemHorizontalAlign.Left)
    {
        EnsureNotMeasuring();
        using var typography = BeginStyledTypography(source.BaseStyle);
        var assigned = _layoutChildConstraints && _inputSlotBoundsTier == _popupTierDepth && _inputSlotBounds.HasValue;
        var previous = _allocatedTextPlacements;
        var columns = _columnsActive;
        var table = _tableActive;
        _allocatedTextPlacements = assigned ? 0 : -1;
        if (assigned)
        {
            _columnsActive = false;
            _tableActive = false;
        }
        try
        {
            var measured = PrepareInlineText(source, lines, wrap);
            var size = alignment == UiItemHorizontalAlign.Left ? measured : ConstrainToLayout(measured);
            var layout = PlaceControlText(size);
            if (layout.Visible)
                PaintInlineVisualLines(lines, layout.Position, color ?? _theme.Text, layout.ClipRect,
                    lines.AlignmentOffsets(size.X, alignment));
            return ExposePassiveText(ref identity, accessibleText ?? source.Source,
                new(layout.Position.X, layout.Position.Y, layout.Size.X, layout.Size.Y));
        }
        finally
        {
            _allocatedTextPlacements = previous;
            _columnsActive = columns;
            _tableActive = table;
        }
    }

    private void PaintInlineVisualLines(UiVisualTextLines lines, UiVector2 origin, UiColor color, UiRect clip,
        ReadOnlySpan<float> lineOffsets = default)
    {
        if (!lineOffsets.IsEmpty && lineOffsets.Length != lines.Count)
            throw new InvalidOperationException("Inline alignment offsets must match the prepared line count.");
        if (clip.Width <= 0 || clip.Height <= 0 || lines.Count == 0)
            return;
        if (TryPaintWholeInlineParagraph(lines, origin, color, clip, lineOffsets))
            return;
        // Include neighboring lines for glyph ink extending beyond the advance box.
        var first = Math.Max(0, lines.LineAtY(MathF.Max(0, clip.Y - origin.Y)) - 1);
        var end = Math.Min(lines.Count, lines.LineAtY(MathF.Max(0, clip.Y + clip.Height - origin.Y)) + 2);
        for (var line = first; line < end; line++)
            PaintStyledLine(lines, line, new(origin.X + (lineOffsets.IsEmpty ? 0 : lineOffsets[line]), origin.Y), color, clip);
    }
}
