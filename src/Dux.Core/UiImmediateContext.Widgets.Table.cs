using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Dux.Core;

public sealed partial class UiImmediateContext
{
    public bool BeginTable(string id, int columns)
    {
        return BeginTable(id, columns, UiTableFlags.None);
    }

    public bool BeginTable(string id, int columns, UiTableFlags flags)
    {
        id ??= "Table";
        if (columns <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(columns), "Columns must be positive.");
        }

        _tableId = id;
        _tableFlags = flags;
        _tableActive = true;
        _tableColumns = columns;
        _tableColumn = 0;
        _tableSetupIndex = 0;
        _tableColumnLabels.Clear();
        _tableRowIndex = 0;

        var current = _layouts.Peek();
        _tableStartX = current.Cursor.X;
        _tableRowY = current.Cursor.Y;
        _tableRowMaxY = _tableRowY;
        _tableStartY = _tableRowY;

        var width = _hasWindowRect
            ? MathF.Max(0f, _windowRect.Width - (WindowPadding * 2f))
            : (InputWidth * columns) + (ItemSpacingX * (columns - 1));

        _tableColumnWidth = MathF.Max(1f, (width - (ItemSpacingX * (columns - 1))) / columns);
        if (_tableColumnWidths.Length != columns)
        {
            _tableColumnWidths = new float[columns];
        }
        if (_tableColumnAlign.Length != columns)
        {
            _tableColumnAlign = new float[columns];
        }
        Array.Clear(_tableColumnWidths);
        Array.Clear(_tableColumnAlign);
        _tableRect = new UiRect(_tableStartX, _tableStartY, GetTableTotalWidth(), 0f);
        return true;
    }

    public void TableSetupColumn(string label)
    {
        TableSetupColumn(label, 0f, 0f);
    }

    public void TableSetupColumn(string label, float width, float alignX)
    {
        if (!_tableActive)
        {
            return;
        }

        label ??= string.Empty;
        if (_tableSetupIndex < _tableColumns)
        {
            if (_tableSetupIndex < _tableColumnLabels.Count)
            {
                _tableColumnLabels[_tableSetupIndex] = label;
            }
            else
            {
                _tableColumnLabels.Add(label);
            }

            _tableColumnWidths[_tableSetupIndex] = MathF.Max(0f, width);
            _tableColumnAlign[_tableSetupIndex] = Math.Clamp(alignX, 0f, 1f);
        }

        _tableSetupIndex++;
    }

    public void TableSetupColumn(string label, float width, float alignX, UiTableColumnFlags flags)
    {
        _ = flags;
        TableSetupColumn(label, width, alignX);
    }

    public void TableSetupScrollFreeze(int cols, int rows)
    {
        _ = cols;
        _ = rows;
    }

    public void TableHeadersRow()
    {
        if (!_tableActive)
        {
            return;
        }

        var headerHeight = GetFrameHeight() + 2f;
        var totalWidth = GetTableTotalWidth();
        var headerRect = new UiRect(_tableStartX, _tableRowY, totalWidth, headerHeight);
        AddRectFilled(headerRect, _theme.TableHeaderBg, _whiteTexture);

        for (var i = 0; i < _tableColumns; i++)
        {
            var label = i < _tableColumnLabels.Count ? _tableColumnLabels[i] : string.Empty;
            if (!string.IsNullOrEmpty(label))
            {
                var columnWidth = GetTableColumnWidth(i);
                var textSize = UiTextBuilder.MeasureText(_fontAtlas, label, _textSettings, _lineHeight);
                var cellX = GetTableColumnX(i) + ButtonPaddingX;
                var available = MathF.Max(0f, columnWidth - (ButtonPaddingX * 2f));
                var align = i < _tableColumnAlign.Length ? _tableColumnAlign[i] : 0f;
                cellX += MathF.Max(0f, available - textSize.X) * align;
                var cellY = _tableRowY + (headerHeight - textSize.Y) * 0.5f;
                _builder.AddText(
                    _fontAtlas,
                    label,
                    new UiVector2(cellX, cellY),
                    _theme.Text,
                    _fontTexture,
                    CurrentClipRect,
                    _textSettings,
                    _lineHeight
                );
            }
        }

        _tableRowY += headerHeight + ItemSpacingY;
        _tableRowMaxY = MathF.Max(_tableRowMaxY, _tableRowY);
        _tableColumn = 0;
        _tableRowIndex = 0;
    }

    public void TableHeader(string label)
    {
        if (!_tableActive)
        {
            return;
        }

        label ??= string.Empty;

        var headerHeight = GetFrameHeight() + 2f;
        var columnWidth = GetTableColumnWidth(_tableColumn);
        var cellRect = new UiRect(GetTableColumnX(_tableColumn), _tableRowY, columnWidth, headerHeight);
        AddRectFilled(cellRect, _theme.TableHeaderBg, _whiteTexture);

        if (!string.IsNullOrEmpty(label))
        {
            var textSize = UiTextBuilder.MeasureText(_fontAtlas, label, _textSettings, _lineHeight);
            var align = _tableColumnAlign.Length > _tableColumn ? _tableColumnAlign[_tableColumn] : 0f;
            var available = MathF.Max(0f, columnWidth - (ButtonPaddingX * 2f));
            var cellX = cellRect.X + ButtonPaddingX + MathF.Max(0f, available - textSize.X) * align;
            var cellY = cellRect.Y + (headerHeight - textSize.Y) * 0.5f;
            _builder.AddText(
                _fontAtlas,
                label,
                new UiVector2(cellX, cellY),
                _theme.Text,
                _fontTexture,
                CurrentClipRect,
                _textSettings,
                _lineHeight
            );
        }

        _tableRowMaxY = MathF.Max(_tableRowMaxY, _tableRowY + headerHeight);
        TableNextColumn();
    }

    public void TableAngledHeadersRow()
    {
        TableHeadersRow();
    }

    public void TableHeadersRowSortable()
    {
        if (!_tableActive || _tableId is null)
        {
            return;
        }

        _tableFlags |= UiTableFlags.Sortable;

        var sortKey = $"{_tableId}##sort";
        var sortState = _state.GetTableSort(sortKey, 0, true);
        var headerHeight = GetFrameHeight() + 2f;
        var totalWidth = GetTableTotalWidth();
        var headerRect = new UiRect(_tableStartX, _tableRowY, totalWidth, headerHeight);
        AddRectFilled(headerRect, _theme.TableHeaderBg, _whiteTexture);

        var columnLabels = _tableColumnLabels;
        var labelCount = columnLabels.Count;
        var columnAlign = _tableColumnAlign;

        for (var i = 0; i < _tableColumns; i++)
        {
            var label = i < labelCount ? columnLabels[i] : string.Empty;
            var columnWidth = GetTableColumnWidth(i);
            var cellRect = new UiRect(GetTableColumnX(i), _tableRowY, columnWidth, headerHeight);

            var pressed = ButtonBehavior($"{sortKey}##col{i}", cellRect, out var hovered, out var held);
            if (pressed)
            {
                if (_state.Modifiers.HasFlag(KeyModifiers.Shift))
                {
                        var specsReadOnly = _state.GetTableSortSpecs(sortKey);
                        var specsCount = specsReadOnly.Count;
                        var pool = ArrayPool<UiTableSortState>.Shared;
                        var buffer = pool.Rent(specsCount + 1);
                        try
                        {
                            for (var s = 0; s < specsCount; s++)
                            {
                                buffer[s] = specsReadOnly[s];
                            }

                            var index = FindSortSpecIndex(buffer.AsSpan(0, specsCount), i);
                            if (index >= 0)
                            {
                                var current = buffer[index];
                                buffer[index] = current with { Ascending = !current.Ascending };
                            }
                            else
                            {
                                buffer[specsCount++] = new UiTableSortState(i, true);
                            }

                            _state.SetTableSortSpecs(sortKey, buffer.AsSpan(0, specsCount));
                            if (specsCount > 0)
                            {
                                sortState = buffer[0];
                            }
                        }
                        finally
                        {
                            pool.Return(buffer, clearArray: false);
                        }
                }
                else
                {
                    sortState = sortState.Column == i
                        ? sortState with { Ascending = !sortState.Ascending }
                        : new UiTableSortState(i, true);
                    _state.SetTableSort(sortKey, sortState);
                }
            }

            if (held || hovered || sortState.Column == i)
            {
                var bg = held ? _theme.HeaderActive : hovered ? _theme.HeaderHovered : _theme.Header;
                AddRectFilled(cellRect, bg, _whiteTexture);
            }

            if (!string.IsNullOrEmpty(label))
            {
                var textSize = UiTextBuilder.MeasureText(_fontAtlas, label, _textSettings, _lineHeight);
                var align = i < columnAlign.Length ? columnAlign[i] : 0f;
                var available = MathF.Max(0f, columnWidth - (ButtonPaddingX * 2f));
                var cellX = cellRect.X + ButtonPaddingX + MathF.Max(0f, available - textSize.X) * align;
                var cellY = cellRect.Y + (headerHeight - textSize.Y) * 0.5f;
                _builder.AddText(
                    _fontAtlas,
                    label,
                    new UiVector2(cellX, cellY),
                    _theme.Text,
                    _fontTexture,
                    CurrentClipRect,
                    _textSettings,
                    _lineHeight
                );
            }

            var activeSpecs = _state.GetTableSortSpecs(sortKey);
            var specIndex = FindSortSpecIndex(activeSpecs, i);
            if (specIndex >= 0)
            {
                var active = activeSpecs[specIndex];
                var arrow = active.Ascending ? "^" : "v";
                var arrowSize = UiTextBuilder.MeasureText(_fontAtlas, arrow, _textSettings, _lineHeight);
                var arrowX = cellRect.X + columnWidth - ButtonPaddingX - arrowSize.X;
                var arrowY = cellRect.Y + (headerHeight - arrowSize.Y) * 0.5f;
                _builder.AddText(
                    _fontAtlas,
                    arrow,
                    new UiVector2(arrowX, arrowY),
                    _theme.Text,
                    _fontTexture,
                    CurrentClipRect,
                    _textSettings,
                    _lineHeight
                );

                if (activeSpecs.Count > 1)
                {
                    var orderText = FormattableString.Invariant($"{specIndex + 1}");
                    var orderSize = UiTextBuilder.MeasureText(_fontAtlas, orderText, _textSettings, _lineHeight);
                    var orderX = arrowX - orderSize.X - 4f;
                    var orderY = cellRect.Y + (headerHeight - orderSize.Y) * 0.5f;
                    _builder.AddText(
                        _fontAtlas,
                        orderText,
                        new UiVector2(orderX, orderY),
                        _theme.SliderGrab,
                        _fontTexture,
                        CurrentClipRect,
                        _textSettings,
                        _lineHeight
                    );
                }
            }
        }

        _tableRowY += headerHeight + ItemSpacingY;
        _tableRowMaxY = MathF.Max(_tableRowMaxY, _tableRowY);
        _tableColumn = 0;
        _tableRowIndex = 0;
    }

    private static int FindSortSpecIndex(IReadOnlyList<UiTableSortState> specs, int column)
    {
        for (var i = 0; i < specs.Count; i++)
        {
            if (specs[i].Column == column)
            {
                return i;
            }
        }

        return -1;
    }

    private static int FindSortSpecIndex(ReadOnlySpan<UiTableSortState> specs, int column)
    {
        for (var i = 0; i < specs.Length; i++)
        {
            if (specs[i].Column == column)
            {
                return i;
            }
        }

        return -1;
    }

    public bool TableGetSortSpecs(out int column, out bool ascending)
    {
        if (!_tableActive || _tableId is null)
        {
            column = 0;
            ascending = true;
            return false;
        }

        var sortState = _state.GetTableSort($"{_tableId}##sort", 0, true);
        column = sortState.Column;
        ascending = sortState.Ascending;
        return true;
    }

    public bool TableGetSortSpecs(out int column, out bool ascending, out bool changed, bool clearDirty = true)
    {
        if (!_tableActive || _tableId is null)
        {
            column = 0;
            ascending = true;
            changed = false;
            return false;
        }

        var sortKey = $"{_tableId}##sort";
        var sortState = _state.GetTableSort(sortKey, 0, true);
        column = sortState.Column;
        ascending = sortState.Ascending;
        changed = _state.IsTableSortDirty(sortKey);
        if (changed && clearDirty)
        {
            _state.ClearTableSortDirty(sortKey);
        }

        return true;
    }

    public bool TableGetSortSpecs(out UiTableSortState[] specs, out bool changed, bool clearDirty = true)
    {
        if (!_tableActive || _tableId is null)
        {
            specs = [];
            changed = false;
            return false;
        }

        var sortKey = $"{_tableId}##sort";
        specs = _state.GetTableSortSpecs(sortKey).ToArray();
        changed = _state.IsTableSortDirty(sortKey);
        if (changed && clearDirty)
        {
            _state.ClearTableSortDirty(sortKey);
        }

        return true;
    }

    public void TableNextRow()
    {
        if (!_tableActive)
        {
            return;
        }

        DrawTableRowBackgroundIfNeeded();
        _tableRowMaxY = MathF.Max(_tableRowMaxY, _tableRowY + GetFrameHeight() + 2f);
        _tableColumn = 0;
        _tableRowY = _tableRowMaxY;
        _tableRowMaxY = _tableRowY;
        _tableRowIndex++;
    }

    public void TableNextRow(UiTableRowFlags flags, float minRowHeight = 0f)
    {
        _ = flags;
        if (!_tableActive)
        {
            return;
        }

        DrawTableRowBackgroundIfNeeded();
        var targetMin = minRowHeight > 0f ? minRowHeight : GetFrameHeight() + 2f;
        _tableRowMaxY = MathF.Max(_tableRowMaxY, _tableRowY + targetMin);
        _tableColumn = 0;
        _tableRowY = _tableRowMaxY;
        _tableRowMaxY = _tableRowY;
        _tableRowIndex++;
    }

    public void TableNextColumn()
    {
        if (!_tableActive)
        {
            return;
        }

        _tableColumn++;
        if (_tableColumn >= _tableColumns)
        {
            TableNextRow();
        }
    }

    public int TableGetColumnIndex()
    {
        return _tableActive ? _tableColumn : -1;
    }

    public string TableGetColumnName(int index)
    {
        if (!_tableActive || index < 0)
        {
            return string.Empty;
        }

        return index < _tableColumnLabels.Count ? _tableColumnLabels[index] : string.Empty;
    }

    public void TableSetColumnIndex(int index)
    {
        if (!_tableActive)
        {
            return;
        }

        _tableColumn = Math.Clamp(index, 0, Math.Max(0, _tableColumns - 1));
    }

    public void TableSetColumnWidth(int index, float width)
    {
        if (!_tableActive || (uint)index >= (uint)_tableColumnWidths.Length)
        {
            return;
        }

        _tableColumnWidths[index] = MathF.Max(0f, width);
    }

    public void TableSetColumnAlign(int index, float alignX)
    {
        if (!_tableActive || (uint)index >= (uint)_tableColumnAlign.Length)
        {
            return;
        }

        _tableColumnAlign[index] = Math.Clamp(alignX, 0f, 1f);
    }

    public float TableGetColumnWidth(int index)
    {
        if (!_tableActive || index < 0 || index >= _tableColumns)
        {
            return 0f;
        }

        return GetTableColumnWidth(index);
    }

    public int TableGetColumnCount()
    {
        return _tableActive ? _tableColumns : 0;
    }

    public UiTableColumnFlags TableGetColumnFlags(int index)
    {
        _ = index;
        return UiTableColumnFlags.None;
    }

    public void TableSetColumnEnabled(int index, bool enabled)
    {
        _ = index;
        _ = enabled;
    }

    public int TableGetRowIndex()
    {
        return _tableActive ? _tableRowIndex : -1;
    }

    public int TableGetHoveredColumn()
    {
        if (!_tableActive)
        {
            return -1;
        }

        for (var i = 0; i < _tableColumns; i++)
        {
            var x = GetTableColumnX(i);
            var width = GetTableColumnWidth(i);
            var rect = new UiRect(x, _tableStartY, width, MathF.Max(0f, _tableRowMaxY - _tableStartY));
            if (IsHovering(rect))
            {
                return i;
            }
        }

        return -1;
    }

    public bool TableColumnIsHovered(int index)
    {
        if (!_tableActive || index < 0 || index >= _tableColumns)
        {
            return false;
        }

        var x = GetTableColumnX(index);
        var width = GetTableColumnWidth(index);
        var rowHeight = MathF.Max(GetFrameHeight(), _tableRowMaxY - _tableRowY);
        var rect = new UiRect(x, _tableRowY, width, rowHeight);
        return IsHovering(rect);
    }

    public void TableRowBg(UiColor color)
    {
        if (!_tableActive)
        {
            return;
        }

        var rowHeight = MathF.Max(GetFrameHeight(), _tableRowMaxY - _tableRowY);
        var totalWidth = GetTableTotalWidth();
        var rect = new UiRect(_tableStartX, _tableRowY, totalWidth, rowHeight);
        AddRectFilled(rect, color, _whiteTexture);
    }

    public void TableRowBgAlternating(UiColor evenColor, UiColor oddColor)
    {
        if (!_tableActive)
        {
            return;
        }

        var color = (_tableRowIndex & 1) == 0 ? evenColor : oddColor;
        TableRowBg(color);
    }

    public void TableRowSeparator(UiColor color)
    {
        if (!_tableActive)
        {
            return;
        }

        var rowHeight = MathF.Max(GetFrameHeight(), _tableRowMaxY - _tableRowY);
        var totalWidth = GetTableTotalWidth();
        var y = _tableRowY + rowHeight - 1f;
        var rect = new UiRect(_tableStartX, y, totalWidth, 1f);
        AddRectFilled(rect, color, _whiteTexture);
    }

    public void TableCellBg(UiColor color)
    {
        if (!_tableActive)
        {
            return;
        }

        var cellX = GetTableColumnX(_tableColumn);
        var cellY = _tableRowY;
        var cellHeight = MathF.Max(GetFrameHeight(), _tableRowMaxY - _tableRowY);
        var rect = new UiRect(cellX, cellY, GetTableColumnWidth(_tableColumn), cellHeight);
        AddRectFilled(rect, color, _whiteTexture);
    }

    public void TableSetBgColor(UiTableBgTarget target, UiColor color, int column = -1)
    {
        _ = column;
        switch (target)
        {
            case UiTableBgTarget.RowBg0:
            case UiTableBgTarget.RowBg1:
                TableRowBg(color);
                break;
            case UiTableBgTarget.CellBg:
                TableCellBg(color);
                break;
        }
    }

    public void EndTable()
    {
        if (!_tableActive)
        {
            return;
        }

        DrawTableRowBackgroundIfNeeded();

        var current = _layouts.Pop();
        var endY = MathF.Max(_tableRowMaxY, _tableRowY);
        current = current with { Cursor = new UiVector2(current.LineStartX, endY) };
        _layouts.Push(current);

        _tableRect = new UiRect(_tableStartX, _tableStartY, GetTableTotalWidth(), MathF.Max(0f, endY - _tableStartY));
        if ((_tableFlags & UiTableFlags.Borders) != 0)
        {
            DrawTableBorders();
        }

        _tableActive = false;
        _tableColumns = 0;
        _tableColumn = 0;
        _tableStartX = 0f;
        _tableRowY = 0f;
        _tableRowMaxY = 0f;
        _tableColumnWidth = 0f;
        _tableColumnLabels.Clear();
        _tableSetupIndex = 0;
        _tableRowIndex = 0;
        _tableColumnWidths = [];
        _tableColumnAlign = [];
        _tableId = null;
        _tableFlags = UiTableFlags.None;
        _tableStartY = 0f;
        _tableRect = default;
    }

    private void DrawTableRowBackgroundIfNeeded()
    {
        if ((_tableFlags & UiTableFlags.RowBg) == 0)
        {
            return;
        }

        var rowHeight = MathF.Max(GetFrameHeight(), _tableRowMaxY - _tableRowY);
        if (rowHeight <= 0f)
        {
            return;
        }

        var color = (_tableRowIndex & 1) == 0 ? _theme.TableRowBg0 : _theme.TableRowBg1;
        var rect = new UiRect(_tableStartX, _tableRowY, GetTableTotalWidth(), rowHeight);
        AddRectFilled(rect, color, _whiteTexture);
    }

    private void DrawTableBorders()
    {
        var borderColor = _theme.TableBorder;
        var rect = _tableRect;

        AddRectFilled(new UiRect(rect.X, rect.Y, rect.Width, 1f), borderColor, _whiteTexture);
        AddRectFilled(new UiRect(rect.X, rect.Y + rect.Height - 1f, rect.Width, 1f), borderColor, _whiteTexture);
        AddRectFilled(new UiRect(rect.X, rect.Y, 1f, rect.Height), borderColor, _whiteTexture);
        AddRectFilled(new UiRect(rect.X + rect.Width - 1f, rect.Y, 1f, rect.Height), borderColor, _whiteTexture);

        for (var i = 1; i < _tableColumns; i++)
        {
            var x = GetTableColumnX(i) - (ItemSpacingX * 0.5f);
            AddRectFilled(new UiRect(x, rect.Y, 1f, rect.Height), borderColor, _whiteTexture);
        }
    }
}
