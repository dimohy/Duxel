namespace Duxel.Core;

internal sealed partial class UiVisualTextLines
{
    private float[] _alignedOffsets = [];
    private int _alignedVersion = -1;
    private float _alignedWidth;
    private UiItemHorizontalAlign _alignment;

    internal static float AlignmentOffset(float width, float lineWidth, UiItemHorizontalAlign alignment)
    {
        var free = MathF.Max(0, width - lineWidth);
        return alignment switch
        {
            UiItemHorizontalAlign.Left => 0,
            UiItemHorizontalAlign.Center => free / 2,
            UiItemHorizontalAlign.Right => free,
            _ => throw new ArgumentOutOfRangeException(nameof(alignment))
        };
    }

    internal ReadOnlySpan<float> AlignmentOffsets(float width, UiItemHorizontalAlign alignment)
    {
        if (alignment == UiItemHorizontalAlign.Left)
            return default;
        if (_alignedVersion == Version && _alignedWidth == width && _alignment == alignment)
            return _alignedOffsets;
        if (_alignedOffsets.Length != Count)
            _alignedOffsets = new float[Count];
        for (var line = 0; line < Count; line++)
            _alignedOffsets[line] = AlignmentOffset(width, this[line].Width, alignment);
        _alignedWidth = width;
        _alignment = alignment;
        _alignedVersion = Version;
        return _alignedOffsets;
    }

    private void ClearAlignment()
    {
        _alignedOffsets = [];
        _alignedVersion = -1;
    }
}
