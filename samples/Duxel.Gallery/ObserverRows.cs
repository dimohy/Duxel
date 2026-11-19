using Duxel.Core;

namespace Duxel.Gallery;

internal sealed class ObserverRows : IUiVirtualListAutomationSource
{
    private readonly Dictionary<int, string> _values = [];
    internal IReadOnlyDictionary<int, string> Values => _values;
    internal void SetValue(int key, string value) => _values[key] = value;
    internal bool Swapped;
    internal int Created, Disposed;
    private int Key(int index) => Swapped ? index switch { 0 => 999_999_999, 999_999_999 => 0, _ => index } : index;
    public string GetKey(int index) => Key(index).ToString(System.Globalization.CultureInfo.InvariantCulture);
    public string GetAccessibleItemName(int index) => "행 : " + GetKey(index);
    public int FindAccessibleItem(UiVirtualItemSearchProperty property, string value, int startIndex, int itemCount)
    {
        const string prefix = "행 : ";
        if (property == UiVirtualItemSearchProperty.Name && !value.StartsWith(prefix, StringComparison.Ordinal))
            return -1;
        var text = property == UiVirtualItemSearchProperty.Name ? value.AsSpan(prefix.Length) : value.AsSpan();
        if (!int.TryParse(text, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var key))
            return -1;
        var index = Key(key);
        return index >= startIndex && index < itemCount && text.SequenceEqual(GetKey(index)) ? index : -1;
    }
    public UiWidget Create(int index)
    {
        Created++;
        var key = Key(index);
        var input = Fluent.Input("행", new UiBinding<string>(() => _values.GetValueOrDefault(key, "행 : " + key), value => SetValue(key, value)), 256);
        input.AccessibleName = "행 값 : " + key;
        return new RowLifetime(this, Dux.Row(new(Spacing : 12, CrossAxisAlignment : UiCrossAxisAlignment.Center),
            Fluent.Caption1("키 : " + key), Dux.Expanded(input)));
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
