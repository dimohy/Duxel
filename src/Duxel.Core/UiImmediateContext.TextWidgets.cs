namespace Duxel.Core;

public readonly partial struct UiControlContext
{
    public UiVector2 MeasureText<TPresentation>(string text) where TPresentation : IUiTextPresentation =>
        _ui.MeasureTextPresentation<TPresentation>(text);
}

public sealed partial class UiImmediateContext
{
    private int _allocatedTextPlacements = -1;

    public void Text<TPresentation>(string? text, UiColor? color = null) where TPresentation : IUiTextPresentation =>
        Text<TPresentation>(text, color, false);

    public void Text<TPresentation>(string? text, UiColor? color, bool constrainLayout) where TPresentation : IUiTextPresentation
    {
        EnsureNotMeasuring();
        text ??= string.Empty;
        var context = new UiControlContext(this);
        var assigned = constrainLayout && _layoutChildConstraints && _inputSlotBoundsTier == _popupTierDepth
            ? _inputSlotBounds : null;
        var previousPlacements = _allocatedTextPlacements;
        var columns = _columnsActive;
        var table = _tableActive;
        _allocatedTextPlacements = assigned.HasValue && TPresentation.CanArrange ? 0 : -1;
        if (assigned.HasValue)
        {
            _columnsActive = false;
            _tableActive = false;
        }
        try
        {
            if (assigned is { } bounds && !TPresentation.CanArrange)
            {
                var natural = TPresentation.Measure(context, text);
                if (natural != new UiVector2(bounds.Width, bounds.Height))
                    throw new NotSupportedException("This text presentation must opt into CanArrange and place one common text root to accept a changed parent allocation.");
            }
            TPresentation.Render(context, text, color);
            if (assigned is { } root && TPresentation.CanArrange &&
                (_allocatedTextPlacements != 1 || _lastItemPos != new UiVector2(root.X, root.Y) || _lastItemSize != new UiVector2(root.Width, root.Height)))
                throw new InvalidOperationException("An allocated text presentation must place exactly one common root at the assigned bounds.");
        }
        finally
        {
            _allocatedTextPlacements = previousPlacements;
            _columnsActive = columns;
            _tableActive = table;
        }
    }

    internal UiVector2 MeasureTextPresentation<TPresentation>(string text) where TPresentation : IUiTextPresentation
    {
        var previous = _allocatedTextPlacements;
        _allocatedTextPlacements = 0;
        try
        {
            var context = new UiControlContext(this);
            var natural = TPresentation.Measure(context, text);
            if (!float.IsFinite(natural.X) || !float.IsFinite(natural.Y) || natural.X < 0 || natural.Y < 0)
                throw new InvalidOperationException("Text presentation returned invalid extents.");
            var allocated = ConstrainToLayout(natural);
            if (allocated != natural && !TPresentation.CanArrange)
                throw new NotSupportedException("This text presentation must opt into CanArrange to accept a changed parent allocation.");
            return allocated;
        }
        finally
        {
            _allocatedTextPlacements = previous;
        }
    }

    internal UiTextLayout LayoutControlText(string text)
        => PlaceControlText(MeasureControlText(text));

    internal UiVector2 MeasureControlText(string text) => MeasureTextInternal(text, _textSettings, _lineHeight);

    internal UiTextLayout PlaceControlText(UiVector2 textSize)
    {
        var size = textSize;
        if (_allocatedTextPlacements >= 0)
        {
            _allocatedTextPlacements++;
            size = ConstrainToLayout(textSize);
        }
        var cursor = AdvanceCursor(size);
        CaptureTextAccessibilityRoot(new(cursor.X, cursor.Y, size.X, size.Y));
        if (_tableActive && _tableColumns > 0)
        {
            var columnWidth = GetTableColumnWidth(_tableColumn);
            var align = _tableColumnAlign.Length > _tableColumn ? _tableColumnAlign[_tableColumn] : 0;
            var padding = ButtonPaddingX;
            var available = MathF.Max(0, columnWidth - padding * 2);
            cursor = new(cursor.X + padding + MathF.Max(0, available - textSize.X) * align, cursor.Y);
        }
        var clip = ResolveItemClipRect();
        if (_allocatedTextPlacements >= 0)
            clip = IntersectRect(clip, new(cursor.X, cursor.Y, size.X, size.Y));
        var visible = IntersectRect(clip, new(cursor.X, cursor.Y, textSize.X, textSize.Y));
        return new(cursor, size, clip, visible.Width > 0 && visible.Height > 0);
    }

    internal string WrapControlText(string text)
    {
        if (_allocatedTextPlacements >= 0)
        {
            var parentWidth = GetContentRegionAvail().X;
            return parentWidth > 0 ? WrapText(text, parentWidth) : text;
        }
        var availableWidth = GetWrapWidth(_layouts.Peek().Cursor);
        if (availableWidth <= 0 && _hasWindowRect)
            availableWidth = GetContentRegionAvail().X;
        return availableWidth > 0 ? WrapText(text, availableWidth) : text;
    }
    internal string WrapControlText(string text, float width) => WrapText(text, width);
    internal float ControlTypographyLineHeight => _controlTypographyHeight > 0 ? _controlTypographyHeight : _lineHeight;

    internal void PaintControlText(string text, UiTextLayout layout, UiColor color) =>
        AddTextInternal(_builder, text, layout.Position, color, layout.ClipRect, _textSettings, _lineHeight);
}
