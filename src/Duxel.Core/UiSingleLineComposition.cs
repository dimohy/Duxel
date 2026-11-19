namespace Duxel.Core;

internal sealed class UiSingleLineComposition
{
    private string? _source;
    private string? _preedit;
    internal int Start
    {
        get; private set;
    }
    internal int Length => _preedit!.Length;
    internal string Text { get; private set; } = string.Empty;
    internal UiSingleLineTextLayout Layout { get; } = new();
    internal float CaretX => Layout.Geometry!.TrailingCaretX(Start + Length) * Layout.Scale;

    internal void Prepare(UiImmediateContext ui, string source, int start, string preedit)
    {
        if (_source != source || Start != start || _preedit != preedit)
        {
            Text = string.Concat(source.AsSpan(0, start), preedit, source.AsSpan(start));
            _source = source;
            _preedit = preedit;
            Start = start;
        }
        Layout.Prepare(ui, Text);
    }

    internal int Display(int source, bool end = false) => new UiCompositionTextMap(Start, Length).Display(source, end);
    internal int Source(int display) => new UiCompositionTextMap(Start, Length).Source(display);
}
