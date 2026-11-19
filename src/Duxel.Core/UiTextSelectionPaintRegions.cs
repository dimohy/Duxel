namespace Duxel.Core;

internal readonly record struct UiTextSelectionPaintRegion(UiRect Clip, bool Selected);

/// <summary>Partitions a text clip without allocations or overlapping glyph alpha.</summary>
internal ref struct UiTextSelectionPaintRegions
{
    private readonly UiRect _clip;
    private readonly float _originX;
    private readonly float _scale;
    private readonly ReadOnlySpan<PlatformTextSelectionFragment> _fragments;
    private readonly float _right;
    private float _left;
    private float _pendingEnd;
    private int _index;

    internal UiTextSelectionPaintRegions(UiRect clip, float originX,
        ReadOnlySpan<PlatformTextSelectionFragment> fragments, float scale = 1)
    {
        _clip = clip;
        _originX = originX;
        _scale = scale;
        _fragments = fragments;
        _right = clip.X + clip.Width;
        _left = _pendingEnd = clip.X;
        _index = 0;
        Current = default;
    }

    public UiTextSelectionPaintRegion Current { get; private set; }
    public readonly UiTextSelectionPaintRegions GetEnumerator() => this;

    public bool MoveNext()
    {
        if (_pendingEnd > _left)
            return Emit(_pendingEnd, true);
        while (_index < _fragments.Length)
        {
            var fragment = _fragments[_index++];
            var start = MathF.Max(_left, _originX + fragment.Left * _scale);
            var end = MathF.Min(_right, _originX + (fragment.Left + fragment.Width) * _scale);
            if (end <= start)
                continue;
            if (start > _left)
            {
                _pendingEnd = end;
                return Emit(start, false);
            }
            return Emit(end, true);
        }
        return _left < _right && Emit(_right, false);
    }

    private bool Emit(float end, bool selected)
    {
        Current = new(_clip with { X = _left, Width = end - _left }, selected);
        _left = end;
        return true;
    }
}
