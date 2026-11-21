using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class ObserverRows(string rowLabel, string rowName, string valueName, string keyLabel) : IUiVirtualListAutomationSource
{
    private readonly Dictionary<int, string> _values = [];
    private readonly string[] _nameParts = rowName.Split("{0}", StringSplitOptions.None);
    internal IReadOnlyDictionary<int, string> Values => _values;
    internal void SetValue(int key, string value) => _values[key] = value;
    internal bool Swapped;
    internal int Created, Disposed;
    private int Key(int index) => Swapped ? index switch { 0 => 999_999_999, 999_999_999 => 0, _ => index } : index;
    public string GetKey(int index) => Key(index).ToString(System.Globalization.CultureInfo.InvariantCulture);
    public string GetAccessibleItemName(int index) => string.Format(rowName, GetKey(index));
    public int FindAccessibleItem(UiVirtualItemSearchProperty property, string value, int startIndex, int itemCount)
    {
        var text = value.AsSpan();
        if (property == UiVirtualItemSearchProperty.Name)
        {
            var prefix = _nameParts[0];
            var suffix = _nameParts[1];
            if (text.Length <= prefix.Length + suffix.Length || !text.StartsWith(prefix, StringComparison.Ordinal)
                || !text.EndsWith(suffix, StringComparison.Ordinal))
                return -1;
            text = text[prefix.Length..(text.Length - suffix.Length)];
        }
        if (!int.TryParse(text, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var key))
            return -1;
        var index = Key(key);
        return index >= startIndex && index < itemCount && text.SequenceEqual(GetKey(index)) ? index : -1;
    }
    public UiWidget Create(int index)
    {
        Created++;
        var key = Key(index);
        var keyText = GetKey(index);
        var initial = string.Format(rowName, keyText);
        var input = Fluent.Input(rowLabel, new UiBinding<string>(() => _values.GetValueOrDefault(key, initial), value => SetValue(key, value)), 256);
        input.AccessibleName = string.Format(valueName, keyText);
        return new RowLifetime(this, Dux.Row(new(Spacing : 12, CrossAxisAlignment : UiCrossAxisAlignment.Center),
            Fluent.Caption1(string.Format(keyLabel, keyText)), Dux.Expanded(input)));
    }
    public void Bind(UiWidget widget, int index) { }
    private sealed class RowLifetime : UiControlWidget
    {
        private readonly ObserverRows _source;
        private readonly UiWidget _content;
        internal RowLifetime(ObserverRows source, UiWidget content) { _source = source; _content = content; OwnChild("content", content); }
        protected override UiVector2 MeasureContent(UiControlContext context) => throw new InvalidOperationException("Virtual rows use their declared extents.");
        protected override void RenderControl(UiImmediateContext ui) => _content.Render(ui);
        protected override void OnDispose() => _source.Disposed++;
    }
}

internal sealed class ObserverRowExtents(ObserverRows source, bool horizontal = false) : IUiVirtualListExtentIndex
{
    public double GetBoundary(int index) => (double)(index / 2) * (horizontal ? 760 : 100)
        + (index % 2 == 0 ? 0 : horizontal ? 340 : 36);
    public int FindIndexByKey(string key, int itemCount) => source.FindAccessibleItem(UiVirtualItemSearchProperty.AutomationId, key, 0, itemCount);
}
