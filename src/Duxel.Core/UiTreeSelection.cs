namespace Duxel.Core;

/// <summary>Terminal item values are the single multiselect authority; branch states are derived.</summary>
internal sealed class UiTreeSelection
{
    private readonly UiTreeHierarchy _hierarchy;
    private readonly UiBinding<string[]> _checkedLeaves;
    private string[] _snapshot = [];
    private bool _initialized;
    private bool[] _selected = [];
    private int[] _checkedCount = [];
    internal long Revision { get; private set; }
    internal UiTreeSelection(UiTreeHierarchy hierarchy, UiBinding<string[]> checkedLeaves)
    {
        _hierarchy = hierarchy ?? throw new ArgumentNullException(nameof(hierarchy));
        _checkedLeaves = checkedLeaves ?? throw new ArgumentNullException(nameof(checkedLeaves));
        Synchronize();
    }
    internal bool? Checked(int index) => _checkedCount[index] == 0 ? false
        : _checkedCount[index] == _hierarchy.LeafCount[index] ? true : null;
    internal void Synchronize()
    {
        var values = _checkedLeaves.Value ?? throw new InvalidOperationException("Checked tree leaves cannot be null.");
        if (_initialized && values.AsSpan().SequenceEqual(_snapshot)) return;
        var selected = new bool[_hierarchy.Count];
        foreach (var value in values)
        {
            if (value is null) throw new InvalidOperationException("Checked tree values cannot contain null.");
            var index = _hierarchy.Index(value);
            if (_hierarchy.IsBranch(index) || selected[index])
                throw new InvalidOperationException("Checked values must identify distinct terminal items.");
            selected[index] = true;
        }
        var counts = new int[_hierarchy.Count];
        for (var order = _hierarchy.Count - 1; order >= 0; order--)
        {
            var index = _hierarchy.Preorder[order];
            if (!_hierarchy.IsBranch(index)) counts[index] = selected[index] ? 1 : 0;
            if (_hierarchy.Parent[index] is var parent && parent >= 0)
            {
                counts[parent] += counts[index];
            }
        }
        _snapshot = values.ToArray(); _selected = selected; _checkedCount = counts; _initialized = true;
        Revision++;
    }
    internal bool SetChecked(int index, bool selected)
    {
        Synchronize();
        if (Checked(index) == selected) return true;
        var start = _hierarchy.PreorderIndex[index];
        var end = _hierarchy.SubtreeEnd[index];
        var values = new List<string>();
        for (var order = 0; order < _hierarchy.Count; order++)
        {
            var item = _hierarchy.Preorder[order];
            if (_hierarchy.IsBranch(item)) continue;
            if (order >= start && order < end ? selected : _selected[item]) values.Add(_hierarchy.Entries[item].Value);
        }
        if (values.Count == _snapshot.Length && values.SequenceEqual(_snapshot, StringComparer.Ordinal)) return Checked(index) == selected;
        _checkedLeaves.Value = values.ToArray();
        Synchronize();
        return Checked(index) == selected;
    }
}
