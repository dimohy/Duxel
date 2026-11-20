namespace Duxel.Core;

public enum UiDataGridSelection
{
    None, Single, Multiple
}

/// <summary>Validates an edit and returns the replacement row. The replacement must keep the same key.</summary>
public delegate bool UiDataGridEdit<T>(T row, string text, out T replacement);

/// <summary>Defines a text column, its relative width, and an optional ordering for the values.</summary>
public sealed record UiDataGridColumn<T>(string Header, Func<T, string> Text,
    float Weight = 1, Comparison<T>? Compare = null, UiItemHorizontalAlign Alignment = UiItemHorizontalAlign.Left,
    UiDataGridEdit<T>? Edit = null, Func<T, string>? EditText = null);

public sealed record UiDataGridOptions(float Width = 720, float Height = 320, float RowHeight = 36,
    UiDataGridSelection Selection = UiDataGridSelection.None);

/// <summary>A virtualized data grid with stable row identity, column sorting and optional selection.</summary>
public sealed class UiDataGrid<T> : UiControlWidget
{
    private readonly Func<T, string> _getKey;
    private readonly HashSet<string> _selection = new(StringComparer.Ordinal);
    private readonly Action<UiImmediateContext, UiVirtualViewport> _renderRows;
    private readonly UiScrollOwner _scroll = new();
    private readonly UiDataGridColumn<T>[] _columns;
    private T[] _items = [];
    private string[] _keys = [];
    private int[] _order = [];
    private HashSet<string> _rowKeys = new(StringComparer.Ordinal);
    private double _offset;
    private double? _navigationOffset;
    private bool _notifySelectionAfterRender;
    private (bool Commit, UiVirtualViewport Viewport)? _endEdit;
    internal UiDataGridEditor? Editor { get; private set; }
    internal string? BodyFocusId;
    internal (string Key, int Column)? PendingFocus;
    internal bool Selectable => Options.Selection != UiDataGridSelection.None;
    internal ReadOnlySpan<UiDataGridColumn<T>> Columns => _columns;
    internal UiDataGridOptions Options { get; }
    internal string? GridId;
    internal double TotalWeight { get; }
    public int RowCount => _items.Length;
    public int? SortColumn { get; private set; }
    public bool SortDescending { get; private set; }
    public event Action? SelectionChanged;
    public event Action<string, int, T>? CellEdited;
    public bool IsEditing => Editor is not null;

    public UiDataGrid(IReadOnlyList<T> rows, Func<T, string> getKey, IReadOnlyList<UiDataGridColumn<T>> columns,
        UiDataGridOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(getKey);
        ArgumentNullException.ThrowIfNull(columns);
        Options = options ?? new();
        if (!float.IsFinite(Options.Width) || Options.Width <= 0 || !float.IsFinite(Options.Height)
            || !float.IsFinite(Options.RowHeight) || Options.RowHeight <= 0 || Options.Height < Options.RowHeight * 2
            || Options.Selection is < UiDataGridSelection.None or > UiDataGridSelection.Multiple)
            throw new ArgumentOutOfRangeException(nameof(options));
        _columns = columns.ToArray();
        if (_columns.Length == 0)
            throw new ArgumentException("A data grid requires at least one column.", nameof(columns));
        foreach (var column in _columns)
        {
            ArgumentNullException.ThrowIfNull(column);
            ArgumentException.ThrowIfNullOrWhiteSpace(column.Header);
            ArgumentNullException.ThrowIfNull(column.Text);
            if (!float.IsFinite(column.Weight) || column.Weight <= 0
                || column.Alignment is not (UiItemHorizontalAlign.Left or UiItemHorizontalAlign.Center or UiItemHorizontalAlign.Right))
                throw new ArgumentOutOfRangeException(nameof(columns));
            TotalWeight += column.Weight;
        }
        _getKey = getKey;
        _renderRows = (ui, viewport) => ui.RenderDataGridRows(this, viewport);
        SetRows(rows);
    }

    /// <summary>Replaces the row snapshot. Surviving keys keep their selection.</summary>
    public void SetRows(IReadOnlyList<T> rows)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(rows);
        var items = rows.ToArray();
        var keys = new string[items.Length];
        var rowKeys = new HashSet<string>(items.Length, StringComparer.Ordinal);
        for (var i = 0; i < items.Length; i++)
        {
            var key = _getKey(items[i]);
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            if (!rowKeys.Add(key))
                throw new ArgumentException($"Duplicate data grid row key '{key}'.", nameof(rows));
            keys[i] = key;
        }
        var order = CreateOrder(items, SortColumn, SortDescending);
        _items = items;
        _keys = keys;
        _rowKeys = rowKeys;
        _order = order;
        CloseEditor();
        _offset = 0;
        PendingFocus = null;
        _navigationOffset = null;
        if (_selection.RemoveWhere(key => !rowKeys.Contains(key)) != 0)
            SelectionChanged?.Invoke();
        RequestFrame();
    }

    public void Sort(int column, bool descending = false)
    {
        ThrowIfDisposed();
        if ((uint)column >= (uint)_columns.Length || _columns[column].Compare is null)
            throw new ArgumentOutOfRangeException(nameof(column), "The column must support sorting.");
        if (SortColumn == column && SortDescending == descending)
            return;
        _order = CreateOrder(_items, column, descending);
        CloseEditor();
        SortColumn = column;
        SortDescending = descending;
        _offset = 0;
        _navigationOffset = null;
        PendingFocus = null;
        RequestFrame();
    }

    public bool IsSelected(string rowKey) => _selection.Contains(rowKey);
    public int SelectedCount => _selection.Count;

    public void SetSelected(string rowKey, bool selected)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(rowKey);
        if (!Selectable || !_rowKeys.Contains(rowKey))
            throw new InvalidOperationException("Selection requires an existing row and an enabled selection mode.");
        if (ChangeSelection(rowKey, selected))
            SelectionChanged?.Invoke();
    }

    internal void SelectDuringRender(string rowKey, bool selected) =>
        _notifySelectionAfterRender |= ChangeSelection(rowKey, selected);

    internal void SelectAllDuringRender(bool selected)
    {
        if (selected ? _selection.Count == RowCount : _selection.Count == 0)
            return;
        if (selected)
            _selection.UnionWith(_keys);
        else
            _selection.Clear();
        _notifySelectionAfterRender = true;
        RequestFrame();
    }

    private bool ChangeSelection(string rowKey, bool selected)
    {
        if (_selection.Contains(rowKey) == selected)
            return false;
        if (selected && Options.Selection == UiDataGridSelection.Single)
            _selection.Clear();
        if (selected) _selection.Add(rowKey);
        else _selection.Remove(rowKey);
        RequestFrame();
        return true;
    }

    internal T ItemAt(int row) => _items[_order[row]];
    internal string KeyAt(int row) => _keys[_order[row]];

    internal void BeginEdit(int row, int column)
    {
        var item = ItemAt(row);
        var definition = _columns[column];
        CloseEditor();
        Editor = new(KeyAt(row), column, definition.Header, (definition.EditText ?? definition.Text)(item));
        RequestFrame();
    }

    internal void QueueEditEnd(bool commit, UiVirtualViewport viewport) => _endEdit = (commit, viewport);

    private void FinishEdit(bool commit, UiVirtualViewport viewport)
    {
        if (Editor is not { } editor)
            return;
        var index = Array.IndexOf(_keys, editor.Key);
        T replacement = default!;
        if (commit)
        {
            var edit = _columns[editor.Column].Edit!;
            if (!edit(_items[index], editor.Text.Value, out replacement))
            {
                editor.Input.Options = editor.Input.Options with { Invalid = true };
                editor.Input.NeedsFocus = true;
                RequestFrame();
                return;
            }
            if (!StringComparer.Ordinal.Equals(_getKey(replacement), editor.Key))
                throw new InvalidOperationException("A data grid edit must preserve its row key.");
            if (SortColumn is not null)
            {
                var items = (T[])_items.Clone();
                items[index] = replacement;
                var order = CreateOrder(items, SortColumn, SortDescending);
                _items = items;
                _order = order;
            }
            else
                _items[index] = replacement;
        }
        CloseEditor();
        if (viewport.Bounds.Height > 0)
            Navigate(Array.IndexOf(_order, index), editor.Column, viewport);
        if (commit)
            CellEdited?.Invoke(editor.Key, editor.Column, replacement);
        RequestFrame();
    }

    private void CloseEditor()
    {
        var previous = Editor;
        Editor = null;
        _endEdit = null;
        previous?.Input.Dispose();
    }

    public void CancelEdit()
    {
        ThrowIfDisposed();
        CloseEditor();
        RequestFrame();
    }

    internal void Navigate(int row, int column, UiVirtualViewport viewport)
    {
        if (RowCount == 0)
            return;
        row = Math.Clamp(row, 0, RowCount - 1);
        PendingFocus = (KeyAt(row), Math.Clamp(column, Selectable ? -1 : 0, _columns.Length - 1));
        var top = (double)row * Options.RowHeight;
        _navigationOffset = Math.Min(Math.Max(viewport.Offset, top + Options.RowHeight - viewport.Bounds.Height), top);
        RequestFrame();
    }

    private int[] CreateOrder(T[] items, int? column, bool descending)
    {
        var order = new int[items.Length];
        for (var i = 0; i < order.Length; i++) order[i] = i;
        if (column is { } index)
        {
            var compare = _columns[index].Compare!;
            Array.Sort(order, (left, right) =>
            {
                var result = descending ? compare(items[right], items[left]) : compare(items[left], items[right]);
                return result != 0 ? result : left.CompareTo(right);
            });
        }
        return order;
    }

    protected override bool CanRetainMeasurement => true;
    protected override UiVector2 MeasureContent(UiControlContext context) => context.MeasureVirtualViewport(Options.Width, Options.Height);
    protected override void RenderControl(UiImmediateContext ui)
    {
        var offset = _navigationOffset ?? _offset;
        _navigationOffset = null;
        var geometry = new UiVirtualListGeometry(RowCount, Options.RowHeight, null);
        _offset = ui.RenderDataGrid(this, MeasureContent(new(ui)), geometry, offset, _scroll, _renderRows);
        if (_endEdit is { } edit)
        {
            _endEdit = null;
            FinishEdit(edit.Commit, edit.Viewport);
        }
        if (_notifySelectionAfterRender)
        {
            _notifySelectionAfterRender = false;
            SelectionChanged?.Invoke();
        }
    }

    private protected override void ReleaseOwnedResources()
    {
        _scroll.Release();
        CloseEditor();
        SelectionChanged = null;
        CellEdited = null;
        _selection.Clear();
        _rowKeys.Clear();
        _items = [];
        _keys = [];
        _order = [];
        base.ReleaseOwnedResources();
    }
}

internal sealed class UiDataGridEditor
{
    internal string Key { get; }
    internal int Column { get; }
    internal UiState<string> Text { get; }
    internal UiDataGridInput Input { get; }

    internal UiDataGridEditor(string key, int column, string label, string text)
    {
        Key = key;
        Column = column;
        Text = new(text);
        Input = new(Text.Binding, Math.Max(text.Length, 4096))
        {
            AccessibleName = label,
            Options = new(UiInputTextFlags.AutoSelectAll)
        };
    }
}

internal sealed class UiDataGridInput(UiBinding<string> value, int maxLength)
    : UiInput<UiFluentTextInputBehavior, UiFluentInputPresentation<UiFluentMediumInputSize, UiFluentOutlineInput>>(
        string.Empty, value, maxLength)
{
    internal bool NeedsFocus = true;
    internal string? InputId;

    protected override void RenderControl(UiImmediateContext ui)
    {
        InputId = ui.PrepareDataGridInput(PrimaryInputId(Label), ref NeedsFocus);
        base.RenderControl(ui);
    }
}

public static partial class Fluent
{
    public static UiDataGrid<T> DataGrid<T>(IReadOnlyList<T> rows, Func<T, string> getKey,
        IReadOnlyList<UiDataGridColumn<T>> columns, UiDataGridOptions? options = null) => new(rows, getKey, columns, options);
}
