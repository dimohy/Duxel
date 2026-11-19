namespace Duxel.Core;

/// <summary>Keeps the two bounded width variants used by scrollbar probing and committed layout.</summary>
internal sealed class UiVisualTextVariants
{
    private UiVisualTextLines _first = new();
    private UiVisualTextLines? _second;
    private float _firstWidth = float.NaN, _secondWidth = float.NaN;
    internal int Builds => _first.Builds + (_second?.Builds ?? 0);
    internal UiVisualTextLines Latest => _first;
    internal UiVisualTextLines ForWidth(float width)
    {
        if (width == _firstWidth) return _first;
        if (float.IsNaN(_firstWidth))
        {
            _firstWidth = width;
            return _first;
        }
        (_first, _second) = (_second ?? new(), _first);
        (_firstWidth, _secondWidth) = (_secondWidth, _firstWidth);
        if (width != _firstWidth)
        {
            _first.Clear();
            _firstWidth = width;
        }
        return _first;
    }
    internal void Clear()
    {
        _first.Clear();
        _second?.Clear();
        _firstWidth = _secondWidth = float.NaN;
    }
}
