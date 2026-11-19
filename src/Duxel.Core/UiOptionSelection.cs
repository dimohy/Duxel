namespace Duxel.Core;

/// <summary>A stable option value and its complete searchable, accessible text.</summary>
public readonly record struct UiSelectOption(string Value, string Text, bool Enabled = true)
{
    public UiMenuIcon Icon
    {
        get; init;
    }
    public string? Description
    {
        get; init;
    }
}

/// <summary>One selection authority shared by readonly dropdowns and editable option controls.</summary>
internal sealed class UiOptionSelection : IUiOptionSelectionView
{
    private readonly UiBinding<string?>? _single;
    private readonly UiBinding<string[]>? _multiple;
    private string[] _snapshot = [];
    private readonly Dictionary<string, int> _indices = new(StringComparer.Ordinal);
    internal readonly UiSelectOption[] Options;
    internal readonly UiMenuOption[] MenuItems;
    internal readonly bool[] Selected;
    internal bool Required { get; init; }
    internal bool Multiple => _multiple is not null;
    internal int Count => _snapshot.Length;
    internal ReadOnlySpan<string> Values => _snapshot;
    internal long Revision { get; private set; }
    internal bool PreserveSelectionOrder { get; init; }
    internal string Text { get; private set; } = string.Empty;
    bool IUiOptionSelectionView.Multiple => Multiple;
    bool[] IUiOptionSelectionView.Selected => Selected;
    bool IUiOptionSelectionView.Editable => false;
    string? IUiOptionSelectionView.EmptyText => null;
    bool IUiOptionSelectionView.Navigated
    {
        get; set;
    }
    void IUiOptionSelectionView.Synchronize() => Synchronize();
    int IUiOptionSelectionView.FirstSelected() => FirstSelected();
    int IUiOptionSelectionView.Search(string text, double now, int focused) => Search(text, now, focused);
    bool IUiOptionSelectionView.HasSearch(double now) => HasSearch(now);
    void IUiOptionSelectionView.ResetSearch() => ResetSearch();
    void IUiOptionSelectionView.Activate(int index) => Activate(index);
    private readonly UiIncrementalSearch _search = new();

    internal UiOptionSelection(UiSelectOption[] options, UiBinding<string?> binding) : this(options)
        => _single = binding ?? throw new ArgumentNullException(nameof(binding));
    internal UiOptionSelection(UiSelectOption[] options, UiBinding<string[]> binding) : this(options)
        => _multiple = binding ?? throw new ArgumentNullException(nameof(binding));
    private UiOptionSelection(UiSelectOption[] options)
    {
        ArgumentNullException.ThrowIfNull(options);
        for (var i = 0; i < options.Length; i++)
        {
            var option = options[i];
            if (string.IsNullOrWhiteSpace(option.Value) || string.IsNullOrWhiteSpace(option.Text)
                || UiAccessibilityTree.ContainsControl(option.Text, false) || !_indices.TryAdd(option.Value, i))
                throw new ArgumentException("Options require unique nonempty values and single-line text.", nameof(options));
            if (option.Description is { } description && (string.IsNullOrWhiteSpace(description)
                || UiAccessibilityTree.ContainsControl(description, false)))
                throw new ArgumentException("Option descriptions must be nonempty single-line text.", nameof(options));
        }
        Options = options.ToArray();
        MenuItems = options.Select(option => new UiMenuOption(option.Value, option.Text, option.Enabled)
        {
            Icon = option.Icon,
            SecondaryContent = option.Description
        }).ToArray();
        Selected = new bool[options.Length];
    }
    internal void Synchronize(bool force = false)
    {
        string[] values;
        if (_multiple is { } multiple)
        {
            values = multiple.Value ?? throw new InvalidOperationException("Multiple selection cannot be null.");
            if (!force && values.AsSpan().SequenceEqual(_snapshot))
                return;
        }
        else
        {
            var value = _single!.Value;
            if (!force && (value is null && _snapshot.Length == 0 || _snapshot.Length == 1 && _snapshot[0] == value))
                return;
            values = value is null ? [] : [value];
        }
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in values)
            if (value is null || !_indices.ContainsKey(value) || !seen.Add(value))
                throw new InvalidOperationException("Selection must contain distinct values from the option collection.");
        var valuesChanged = !values.AsSpan().SequenceEqual(_snapshot);
        _snapshot = values.ToArray();
        if (valuesChanged) Revision++;
        Array.Clear(Selected);
        foreach (var value in values)
            Selected[_indices[value]] = true;
        Text = string.Join(", ", Options.Where((_, i) => Selected[i]).Select(option => option.Text));
    }
    internal void Select(int index, bool add)
    {
        if (Selected[index] && (add && Multiple || Count == 1))
            return;
        if (!add || !Multiple)
            Array.Clear(Selected);
        Selected[index] = true;
        Write();
    }
    internal void Deselect(int index)
    {
        if (!Selected[index])
            return;
        Selected[index] = false;
        Write();
    }
    internal void Activate(int index)
    {
        if (Multiple && Selected[index])
            Deselect(index);
        else
            Select(index, add: Multiple);
    }
    internal void CommitText(ref string text, bool freeform)
    {
        var query = text;
        var match = Array.FindIndex(Options, option => option.Enabled && string.Equals(option.Text, query, StringComparison.OrdinalIgnoreCase));
        if (match >= 0)
        {
            Select(match, add: Multiple);
            text = Multiple ? string.Empty : Text;
        }
        else if (!freeform)
            text = Multiple ? string.Empty : Text;
        else if (!Multiple && Array.FindIndex(Selected, static value => value) is var selected && selected >= 0)
            Deselect(selected);
    }
    private void Write()
    {
        var values = PreserveSelectionOrder
            ? _snapshot.Where(value => Selected[_indices[value]])
                .Concat(Options.Where((option, i) => Selected[i] && !_snapshot.Contains(option.Value, StringComparer.Ordinal)).Select(option => option.Value)).ToArray()
            : Options.Where((_, i) => Selected[i]).Select(option => option.Value).ToArray();
        if (values.AsSpan().SequenceEqual(_snapshot))
            return;
        if (_multiple is { } multiple)
            multiple.Value = values;
        else
            _single!.Value = values.Length == 0 ? null : values[0];
        Synchronize(force: true);
    }
    internal int FirstSelected()
    {
        for (var i = 0; i < Selected.Length; i++)
            if (Selected[i] && Options[i].Enabled)
                return i;
        return -1;
    }
    internal int Search(string character, double now, int focused)
    {
        var query = _search.Append(character, now, out var cycle);
        for (var step = cycle ? 1 : 0; step < Options.Length + (cycle ? 1 : 0); step++)
        {
            var index = (focused + step + Options.Length) % Options.Length;
            if (Options[index].Enabled && Options[index].Text.AsSpan().TrimStart().StartsWith(query, StringComparison.OrdinalIgnoreCase))
                return index;
        }
        return focused;
    }
    internal bool HasSearch(double now) => _search.Active(now);
    internal void ResetSearch() => _search.Reset();
}
