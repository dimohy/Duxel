namespace Duxel.Core;

/// <summary>Shared committed-text prefix buffer; it never owns focus, selection or a frame timer.</summary>
internal sealed class UiIncrementalSearch
{
    private string _text = string.Empty;
    private double _at;
    internal string Append(string character, double now, out bool cycle)
    {
        if (now - _at > 1 || now < _at) _text = string.Empty;
        cycle = _text == character;
        _text = cycle ? character : _text + character;
        _at = now;
        return _text;
    }
    internal bool Active(double now) => _text.Length > 0 && now >= _at && now - _at <= 1;
    internal void Reset() => _text = string.Empty;
}
