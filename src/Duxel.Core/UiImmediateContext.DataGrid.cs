namespace Duxel.Core;

public sealed partial class UiImmediateContext
{
    private const float DataGridSelectionWidth = 36;

    internal double RenderDataGrid<T>(UiDataGrid<T> grid, UiVector2 size, UiVirtualListGeometry geometry,
        double offset, UiScrollOwner scroll, Action<UiImmediateContext, UiVirtualViewport> renderRows)
    {
        var height = MathF.Min(grid.Options.RowHeight, size.Y);
        var bodyHeight = size.Y - height;
        var origin = AllocateLayoutBox(new(size.X, height));
        var bounds = new UiRect(origin.X, origin.Y, size.X, size.Y);
        var parent = _accessibilityParent;
        var widget = _accessibilityWidget;
        var id = ResolveId("DataGrid");
        grid.GridId = id;
        ExposeAccessibility(id, parent, UiAccessibilityRole.Grid, "Data grid", bounds,
            focusable: false, valuePattern: false, literalLabel: true);
        _accessibility?.SetGrid(id, new(grid.RowCount + 1, grid.Columns.Length + (grid.Selectable ? 1 : 0))
        {
            IsVirtualized = true
        });
        if (grid.Selectable)
            _accessibility?.SetListContainer(id, grid.Options.Selection == UiDataGridSelection.Multiple);
        _accessibilityWidget = null;
        _accessibilityParent = id;
        using var clip = BeginControlClip(bounds);
        try
        {
            var header = bounds with { Height = height };
            var headerContent = header with
            {
                Width = MathF.Max(0, size.X - (geometry.ContentExtent > bodyHeight ? ScrollbarSize : 0))
            };
            var sort = (grid.SortColumn, grid.SortDescending);
            PaintDataGridHeader(grid, header, headerContent, id);
            if (sort != (grid.SortColumn, grid.SortDescending))
                offset = 0;
            if (bodyHeight > 0)
            {
                SetCursorScreenPos(new(origin.X, origin.Y + height));
                offset = RenderVirtualViewport("DataGridScroll", new(size.X, bodyHeight), geometry, offset,
                    false, false, false, scroll, renderRows, null, null, out _, ownsWheelInput: true);
            }
            FinishLayoutBox(origin, size);
            return offset;
        }
        finally
        {
            _accessibilityWidget = widget;
            _accessibilityParent = parent;
        }
    }

    private void PaintDataGridHeader<T>(UiDataGrid<T> grid, UiRect header, UiRect content, string gridId)
    {
        var context = new UiControlContext(this);
        using var text = context.BeginTypography<UiSemiboldTypeface, UiFluentType300>();
        var theme = context.Theme;
        context.Frame(header, theme.FrameBg, default, 0, 0);
        PushID("DataGridHeaders");
        try
        {
            var rowId = ResolveId("Row");
            ExposeAccessibility(rowId, gridId, UiAccessibilityRole.Row, "Column headers", header,
                focusable: false, valuePattern: false, literalLabel: true);
            var x = content.X;
            var selectorWidth = grid.Selectable ? MathF.Min(DataGridSelectionWidth, content.Width / 2) : 0;
            if (selectorWidth > 0)
            {
                var cell = content with { Width = selectorWidth };
                if (grid.Options.Selection == UiDataGridSelection.Multiple && grid.RowCount > 0)
                {
                    var id = ResolveId("SelectAll");
                    var all = grid.SelectedCount == grid.RowCount;
                    var interaction = ProcessButton<SpaceControlKeys>("SelectAll", cell, UiButtonActivation.Release);
                    if (interaction.Activated || ApplyAccessibilityToggle(id, cell))
                    {
                        grid.SelectAllDuringRender(!all);
                        all = !all;
                    }
                    var label = all ? "Clear selection" : "Select all rows";
                    var state = all ? UiAccessibilityToggleState.On
                        : grid.SelectedCount == 0 ? UiAccessibilityToggleState.Off : UiAccessibilityToggleState.Indeterminate;
                    ExposeAccessibility(id, rowId, UiAccessibilityRole.Checkbox, label, cell,
                        valuePattern: false, literalLabel: true, toggleState: state);
                    _accessibility?.SetGridItem(id, new(gridId, 0, 0));
                    UiFluentListPresentation.PaintCheckbox(context, cell, interaction, UiAccessibilityToggleStates.ToChecked(state));
                }
                x += selectorWidth;
            }
            var remaining = content.Width - selectorWidth;
            for (var c = 0; c < grid.Columns.Length; c++)
            {
                var column = grid.Columns[c];
                var cell = DataGridCellBounds(content, c, grid.Columns.Length, remaining, column.Weight, grid.TotalWeight, ref x);
                var key = DataGridColumnKey(c);
                var cellId = ResolveId(key);
                var sortable = column.Compare is not null;
                var label = column.Header;
                using var cellClip = BeginControlClip(cell);
                var labelBounds = cell;
                if (sortable)
                {
                    var interaction = ApplyAccessibilityInvoke(cellId, cell, ProcessButton(key, cell, UiButtonActivation.Release));
                    if (interaction.Activated)
                        grid.Sort(c, grid.SortColumn == c && !grid.SortDescending);
                    if (interaction.Hovered)
                        context.Frame(cell, theme.FrameBgHovered, default, 0, 0);
                    if (grid.SortColumn == c)
                    {
                        label += grid.SortDescending ? ", descending" : ", ascending";
                        var arrow = new UiRect(cell.X + cell.Width - 26, cell.Y + (cell.Height - 16) / 2, 16, 16);
                        DrawChevronIcon(arrow, grid.SortDescending ? 0 : 180, .85f, 1.5f, theme.Text);
                    }
                    labelBounds = cell with { Width = MathF.Max(0, cell.Width - 20) };
                    if (interaction.FocusVisible)
                        context.FocusOutline(cell, 4);
                }
                PaintDataGridText(context, column.Header, labelBounds, column.Alignment);
                ExposeAccessibility(cellId, rowId, sortable ? UiAccessibilityRole.Button : UiAccessibilityRole.GridCell, label, cell,
                    readOnly: !sortable, focusable: sortable, valuePattern: false, literalLabel: true);
                _accessibility?.SetGridItem(cellId, new(gridId, 0, c + (grid.Selectable ? 1 : 0)));
            }
        }
        finally
        {
            PopID();
        }
        context.Frame(new(header.X, header.Y + header.Height - 1, header.Width, 1), theme.Border, default, 0, 0);
    }

    internal void RenderDataGridRows<T>(UiDataGrid<T> grid, UiVirtualViewport viewport)
    {
        var geometry = new UiVirtualListGeometry(grid.RowCount, grid.Options.RowHeight, null);
        var (first, end) = geometry.OverscanRange(viewport, 0, out _);
        if (first == end)
            return;
        var context = new UiControlContext(this);
        var theme = context.Theme;
        var tabStart = _currentTabStops.Count;
        var focusedAtStart = _state.FocusedId;
        var pending = grid.PendingFocus;
        var editor = grid.Editor;
        var editorVisible = false;
        grid.PendingFocus = null;
        var parent = _accessibilityParent;
        var gridId = grid.GridId!;
        using var clip = BeginControlClip(viewport.Clip);
        using var text = context.BeginTypography<UiRegularTypeface, UiFluentType300>();
        for (var r = first; r < end; r++)
        {
            var item = grid.ItemAt(r);
            var key = grid.KeyAt(r);
            var row = viewport.Place(geometry.Item(r).Start - viewport.Offset, grid.Options.RowHeight);
            var selected = grid.IsSelected(key);
            var hovered = IsControlEnabled && IsHovering(IntersectRect(CurrentClipRect, row));
            if (selected || hovered)
                context.Frame(row, selected ? theme.Header : theme.FrameBgHovered, default, 0, 0);
            context.Frame(new(row.X, row.Y + row.Height - 1, row.Width, 1), theme.Border, default, 0, 0);
            var rowScope = UiVirtualItemContainer.Id("DataGridRows", key);
            PushID(rowScope);
            try
            {
                var rowId = ResolveId("Row");
                if (grid.Selectable)
                {
                    foreach (var action in DataGridSelectionActions)
                    {
                        var request = _accessibility?.Take(rowId, action);
                        if (request is null)
                            continue;
                        request.Result = RadioAccessibilityAvailability(IsControlEnabled && _popupTierDepth >= _blockingKeyboardTier,
                            IntersectRect(CurrentClipRect, row));
                        if (request.Result == UiAccessibilityResult.Success)
                            grid.SelectDuringRender(key, action != UiAccessibilityAction.Deselect);
                    }
                }
                ExposeAccessibility(rowId, gridId, UiAccessibilityRole.Row, string.Empty, row,
                    focusable: false, valuePattern: false, literalLabel: true);
                var selectorWidth = grid.Selectable ? MathF.Min(DataGridSelectionWidth, row.Width / 2) : 0;
                var x = row.X + selectorWidth;
                var remaining = row.Width - selectorWidth;
                for (var c = grid.Selectable ? -1 : 0; c < grid.Columns.Length; c++)
                {
                    var column = c < 0 ? null : grid.Columns[c];
                    var cell = column is null ? row with { Width = selectorWidth }
                        : DataGridCellBounds(row, c, grid.Columns.Length, remaining, column.Weight, grid.TotalWeight, ref x);
                    if (cell.Width <= 0)
                        continue;
                    var columnKey = DataGridColumnKey(c);
                    var cellId = ResolveId(columnKey);
                    if (editor is not null && ReferenceEquals(editor, grid.Editor) && editor.Key == key && editor.Column == c)
                    {
                        editorVisible = true;
                        ExposeAccessibility(cellId, rowId, UiAccessibilityRole.GridCell, column!.Header, cell,
                            focusable: false, valuePattern: false, literalLabel: true);
                        _accessibility?.SetGridItem(cellId, new(gridId, r + 1, c + (grid.Selectable ? 1 : 0)));
                        RenderDataGridEditor(grid, editor, cellId, cell, viewport);
                        continue;
                    }
                    var restore = pending == (key, c) && IsControlEnabled && _popupTierDepth >= _blockingKeyboardTier
                        && !_leftMousePressed;
                    if (restore)
                        FocusGroupControl(cellId, true);
                    var editable = column?.Edit is not null;
                    var interaction = editable
                        ? grid.Selectable
                            ? ProcessButton<SpaceControlKeys>(columnKey, cell, UiButtonActivation.Release)
                            : ProcessButton<RangeControlKeys>(columnKey, cell, UiButtonActivation.Release)
                        : grid.Selectable
                            ? ProcessButton(columnKey, cell, UiButtonActivation.Release)
                            : ProcessButton<RangeControlKeys>(columnKey, cell, UiButtonActivation.Release);
                    var beginEdit = editable && IsControlEnabled && _popupTierDepth >= _blockingKeyboardTier && !restore
                        && (interaction.Hovered && IsMouseDoubleClicked((int)UiMouseButton.Left)
                            || interaction.Focused && DataGridEditKey(begin: true) != UiKey.None);
                    if (beginEdit)
                        grid.BeginEdit(r, c);
                    else if (interaction.Activated && grid.Selectable && !restore)
                        grid.SelectDuringRender(key, !grid.IsSelected(key));
                    if (!beginEdit && interaction.Focused && cellId == focusedAtStart && pending is null)
                        NavigateDataGrid(grid, r, c, viewport);

                    var label = column is null ? "Select row" : column.Text(item);
                    using var cellClip = BeginControlClip(cell);
                    if (column is null)
                        UiFluentListPresentation.PaintCheckmark(context, cell, interaction, grid.IsSelected(key));
                    else
                        PaintDataGridText(context, label, cell, column.Alignment);
                    if (interaction.FocusVisible)
                        context.FocusOutline(cell, 4);
                    ExposeAccessibility(cellId, rowId, UiAccessibilityRole.GridCell, label, cell,
                        readOnly: !editable, valuePattern: false, literalLabel: true);
                    _accessibility?.SetGridItem(cellId, new(gridId, r + 1, c + (grid.Selectable ? 1 : 0)));
                }
                if (grid.Selectable)
                    _accessibility?.SetListItem(rowId, grid.IsSelected(key), false, false);
                if (grid.IsSelected(key) != selected)
                    _requestFrame?.Invoke();
            }
            finally
            {
                PopID();
            }
        }
        if (!editorVisible && editor is not null && ReferenceEquals(editor, grid.Editor))
            grid.CancelEdit();
        KeepDataGridTabStop(grid, tabStart);
    }

    internal string PrepareDataGridInput(string localId, ref bool needsFocus)
    {
        var id = ResolveId(localId);
        if (needsFocus && IsControlEnabled && _popupTierDepth >= _blockingKeyboardTier)
        {
            FocusGroupControl(id, true);
            needsFocus = false;
        }
        return id;
    }

    private void RenderDataGridEditor<T>(UiDataGrid<T> grid, UiDataGridEditor editor, string cellId, UiRect cell,
        UiVirtualViewport viewport)
    {
        if (_leftMousePressed ? !IsHovering(IntersectRect(CurrentClipRect, cell))
            : !editor.Input.NeedsFocus && editor.Input.InputId != _state.FocusedId)
        {
            grid.CancelEdit();
            return;
        }
        var parent = _accessibilityParent;
        _accessibilityParent = cellId;
        var composing = !string.IsNullOrEmpty(_imeHandler?.GetCompositionText());
        try { RenderLayoutChild(editor.Input, cell, viewport.Bounds); }
        finally { _accessibilityParent = parent; }
        if (!composing && editor.Input.InputId is { } id && id == _state.FocusedId)
        {
            var key = DataGridEditKey(begin: false);
            if (key != UiKey.None)
                grid.QueueEditEnd(key == UiKey.Enter, viewport);
        }
    }

    private UiKey DataGridEditKey(bool begin)
    {
        foreach (var input in _keyEvents)
            if (input.IsDown && input.Modifiers == KeyModifiers.None
                && (input.Key == UiKey.Enter || input.Key == (begin ? UiKey.F2 : UiKey.Escape)))
                return input.Key;
        return UiKey.None;
    }

    private static readonly UiAccessibilityAction[] DataGridSelectionActions =
        [UiAccessibilityAction.Select, UiAccessibilityAction.AddToSelection, UiAccessibilityAction.Deselect];

    private void NavigateDataGrid<T>(UiDataGrid<T> grid, int row, int column, UiVirtualViewport viewport)
    {
        foreach (var input in _keyEvents)
        {
            if (!input.IsDown || input.Modifiers != KeyModifiers.None)
                continue;
            var page = Math.Max(1, (int)(viewport.Bounds.Height / grid.Options.RowHeight) - 1);
            switch (input.Key)
            {
                case UiKey.UpArrow: row--; break;
                case UiKey.DownArrow: row++; break;
                case UiKey.LeftArrow: column--; break;
                case UiKey.RightArrow: column++; break;
                case UiKey.Home: column = grid.Selectable ? -1 : 0; break;
                case UiKey.End: column = grid.Columns.Length - 1; break;
                case UiKey.PageUp: row -= page; break;
                case UiKey.PageDown: row = (int)Math.Min(int.MaxValue, (long)row + page); break;
                default: continue;
            }
            grid.Navigate(row, column, viewport);
            break;
        }
    }

    private void KeepDataGridTabStop<T>(UiDataGrid<T> grid, int start)
    {
        if (_currentTabStops.Count == start)
            return;
        var keep = start;
        for (var i = start; i < _currentTabStops.Count; i++)
        {
            var id = _currentTabStops[i].Id;
            if (id == grid.BodyFocusId)
                keep = i;
            if (id == _state.FocusedId)
            {
                keep = i;
                break;
            }
        }
        var stop = _currentTabStops[keep];
        grid.BodyFocusId = stop.Id;
        if (_currentTabStops.Count > start + 1)
        {
            _currentTabStops[start] = stop;
            _currentTabStops.RemoveRange(start + 1, _currentTabStops.Count - start - 1);
        }
    }

    private static string DataGridColumnKey(int column) =>
        column < 0 ? "Selector" : column.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static UiRect DataGridCellBounds(UiRect row, int index, int count, float remaining, float weight, double total, ref float x)
    {
        var right = row.X + row.Width;
        var width = index == count - 1 ? right - x : MathF.Min(right - x, (float)(remaining * (weight / total)));
        var cell = new UiRect(x, row.Y, MathF.Max(0, width), row.Height);
        x += cell.Width;
        return cell;
    }

    private static void PaintDataGridText(UiControlContext context, string label, UiRect cell, UiItemHorizontalAlign alignment)
    {
        const float padding = 10;
        if (cell.Width <= 2 * padding || string.IsNullOrEmpty(label))
            return;
        var text = context.MeasureText(label);
        var available = cell.Width - 2 * padding;
        var inset = MathF.Max(0, available - text.X);
        var left = cell.X + padding + (alignment switch
        {
            UiItemHorizontalAlign.Center => inset / 2,
            UiItemHorizontalAlign.Right => inset,
            _ => 0
        });
        using var clip = context.Clip(new(cell.X + padding, cell.Y, available, cell.Height));
        context.TypographyButtonText(label, new(left, cell.Y, text.X, cell.Height), text,
            context.IsEnabled ? context.Theme.Text : context.Theme.TextDisabled);
    }
}
