namespace Duxel.Core;

/// <summary>Retained expansion, visible order and focus. Selection is a separate authority.</summary>
internal sealed class UiTreeModel
{
    private readonly UiBinding<string[]> _expanded;
    private string[] _expandedSnapshot = [];
    private bool[] _open = [];
    private int[] _visible = [];
    private int[] _visiblePosition = [];
    private bool _initialized;
    internal UiTreeHierarchy Hierarchy { get; }
    internal ReadOnlySpan<int> Visible => _visible;
    internal int Focused { get; private set; } = -1;
    internal long VisibilityRevision { get; private set; }
    internal UiTreeModel(UiTreeHierarchy hierarchy, UiBinding<string[]> expanded)
    {
        Hierarchy = hierarchy ?? throw new ArgumentNullException(nameof(hierarchy));
        _expanded = expanded ?? throw new ArgumentNullException(nameof(expanded));
        Synchronize();
    }
    internal bool IsExpanded(int index) => _open[index];
    internal bool IsVisible(int index) => _visiblePosition[index] >= 0;
    internal void Synchronize()
    {
        var values = _expanded.Value ?? throw new InvalidOperationException("Expanded tree values cannot be null.");
        if (_initialized && values.AsSpan().SequenceEqual(_expandedSnapshot)) return;
        var open = new bool[Hierarchy.Count];
        foreach (var value in values)
        {
            if (value is null) throw new InvalidOperationException("Expanded tree values cannot contain null.");
            var index = Hierarchy.Index(value);
            if (!Hierarchy.IsBranch(index) || open[index])
                throw new InvalidOperationException("Expanded values must identify distinct branch nodes.");
            open[index] = true;
        }
        var positions = new int[Hierarchy.Count];
        Array.Fill(positions, -1);
        var visible = new int[Hierarchy.Count];
        var count = 0;
        foreach (var index in Hierarchy.Preorder)
        {
            var parent = Hierarchy.Parent[index];
            if (parent >= 0 && (!open[parent] || positions[parent] < 0)) continue;
            positions[index] = count;
            visible[count++] = index;
        }
        Array.Resize(ref visible, count);
        _expandedSnapshot = values.ToArray();
        _open = open; _visible = visible; _visiblePosition = positions; _initialized = true;
        VisibilityRevision++;
        while (Focused >= 0 && positions[Focused] < 0) Focused = Hierarchy.Parent[Focused];
    }
    internal bool SetExpanded(int index, bool expanded)
    {
        Synchronize();
        if (!Hierarchy.IsBranch(index)) return false;
        if (_open[index] == expanded) return true;
        var values = new List<string>();
        foreach (var item in Hierarchy.Preorder)
            if (item == index ? expanded : _open[item]) values.Add(Hierarchy.Entries[item].Value);
        _expanded.Value = values.ToArray();
        Synchronize();
        return _open[index] == expanded;
    }
    internal bool Focus(int index)
    {
        Synchronize();
        if (_visiblePosition[index] < 0) return false;
        Focused = index;
        return true;
    }

    internal bool Reveal(int index)
    {
        Synchronize();
        if (IsVisible(index)) return true;
        var open = (bool[])_open.Clone();
        for (var parent = Hierarchy.Parent[index]; parent >= 0; parent = Hierarchy.Parent[parent])
            open[parent] = true;
        var values = new List<string>();
        foreach (var item in Hierarchy.Preorder)
            if (open[item]) values.Add(Hierarchy.Entries[item].Value);
        _expanded.Value = values.ToArray();
        Synchronize();
        return IsVisible(index);
    }
    internal bool Search(string query, bool cycle)
    {
        Synchronize();
        if (_visible.Length == 0) return false;
        var start = Focused >= 0 ? _visiblePosition[Focused] : 0;
        for (var step = cycle ? 1 : 0; step < _visible.Length + (cycle ? 1 : 0); step++)
        {
            var index = _visible[(start + step) % _visible.Length];
            if (Hierarchy.Entries[index].Label.AsSpan().TrimStart().StartsWith(query, StringComparison.OrdinalIgnoreCase))
            { Focused = index; return true; }
        }
        return false;
    }
    internal void Move(int delta)
    {
        Synchronize();
        if (_visible.Length == 0) return;
        var position = Focused < 0 ? (delta < 0 ? _visible.Length - 1 : 0)
            : (int)Math.Clamp((long)_visiblePosition[Focused] + delta, 0, _visible.Length - 1L);
        Focused = _visible[position];
    }
    internal void Boundary(bool last)
    {
        Synchronize();
        if (_visible.Length > 0) Focused = last ? _visible[^1] : _visible[0];
    }
    internal void Inward()
    {
        Synchronize();
        if (Focused < 0) { Boundary(false); return; }
        var children = Hierarchy.Children[Focused];
        if (children.Length == 0) return;
        if (!_open[Focused]) SetExpanded(Focused, true);
        else Focus(children[0]);
    }
    internal void Outward()
    {
        Synchronize();
        if (Focused < 0) return;
        if (_open[Focused]) SetExpanded(Focused, false);
        else if (Hierarchy.Parent[Focused] is var parent && parent >= 0) Focus(parent);
    }
}
