namespace Duxel.Core;

internal interface IUiOptionSelectionView
{
    bool Multiple
    {
        get;
    }
    bool[] Selected
    {
        get;
    }
    bool Editable
    {
        get;
    }
    string? EmptyText
    {
        get;
    }
    bool Navigated
    {
        get; set;
    }
    void Synchronize();
    int FirstSelected();
    int Search(string text, double now, int focused);
    bool HasSearch(double now);
    void ResetSearch();
    void Activate(int index);
}

/// <summary>Projects visible rows onto one canonical selection without discarding hidden selections.</summary>
internal sealed class UiFilteredOptionSelection(UiOptionSelection source) : IUiOptionSelectionView
{
    internal bool ExcludeSelected;
    private long _selectionRevision = -1;
    private string? _filter;
    private int[] _indices = [];
    internal bool HadFocus;
    internal int HandledDismissalVersion;
    internal string? LastText;
    internal readonly List<UiKeyEvent> EditorKeys = [];
    internal readonly List<UiCharEvent> EditorCharacters = [];
    internal UiMenuOption[] Items { get; private set; } = [];
    internal string CurrentFilter => _filter ?? string.Empty;
    public bool[] Selected { get; private set; } = [];
    public bool Multiple => source.Multiple;
    public bool Editable => true;
    public string EmptyText { get; set; } = "No matches";
    public bool Navigated
    {
        get; set;
    }
    internal bool Contains(int canonicalIndex) => Array.IndexOf(_indices, canonicalIndex) >= 0;
    internal void Filter(string filter)
    {
        source.Synchronize();
        if (_filter == filter && (!ExcludeSelected || _selectionRevision == source.Revision))
            return;
        _filter = filter;
        _selectionRevision = source.Revision;
        _indices = Enumerable.Range(0, source.Options.Length)
            .Where(i => (!ExcludeSelected || !source.Selected[i]) && source.Options[i].Text.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToArray();
        Items = _indices.Select(i => source.MenuItems[i]).ToArray();
        Selected = new bool[_indices.Length];
        Navigated = false;
        Synchronize();
    }
    public void Synchronize()
    {
        source.Synchronize();
        for (var i = 0; i < _indices.Length; i++)
            Selected[i] = source.Selected[_indices[i]];
    }
    public int FirstSelected()
    {
        for (var i = 0; i < Selected.Length; i++)
            if (Selected[i] && Items[i].Enabled)
                return i;
        return -1;
    }
    public int Search(string text, double now, int focused) => focused;
    public bool HasSearch(double now) => false;
    public void ResetSearch()
    {
    }
    public void Activate(int index)
    {
        source.Activate(_indices[index]);
        Synchronize();
    }
}
