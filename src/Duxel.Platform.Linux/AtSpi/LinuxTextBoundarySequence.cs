using Duxel.Core;

namespace Duxel.Platform.Linux;

// A view over retained native offsets or real visual lines; queries allocate no boundary arrays.
internal readonly struct LinuxTextBoundarySequence
{
    private readonly int[]? _offsets;
    private readonly UiAccessibilityTextLayout? _layout;
    private readonly bool _lineEnds, _boundaryAtEnd;
    private readonly int _count;
    internal LinuxTextBoundarySequence(int[] offsets, int count, bool boundaryAtEnd)
        => (_offsets, _layout, _lineEnds, _boundaryAtEnd, _count) = (offsets, null, false, boundaryAtEnd, count);
    internal LinuxTextBoundarySequence(UiAccessibilityTextLayout layout, bool lineEnds)
        => (_offsets, _layout, _lineEnds, _boundaryAtEnd, _count) = (null, layout, lineEnds,
            lineEnds || layout.GetLine(layout.LineCount - 1).Start == layout.Content.CharacterCount, layout.Content.CharacterCount);
    private int Length => _offsets?.Length ?? _layout!.LineCount + 1;
    private int Point(int index) => _offsets is { } offsets ? offsets[index]
        : index == 0 ? 0 : _lineEnds ? _layout!.GetLine(index - 1).End
        : index == _layout!.LineCount ? _count : _layout.GetLine(index).Start;
    private int UpperBound(int value)
    {
        var low = 0;
        var high = Length;
        while (low < high)
        {
            var middle = low + (high - low) / 2;
            if (Point(middle) <= value)
                low = middle + 1;
            else
                high = middle;
        }
        return low;
    }
    private int LowerBound(int value)
    {
        var low = 0;
        var high = Length;
        while (low < high)
        {
            var middle = low + (high - low) / 2;
            if (Point(middle) < value)
                low = middle + 1;
            else
                high = middle;
        }
        return low;
    }
    internal (int Start, int End) GetRange(int offset, int relative)
    {
        Validate(_count, offset, relative);
        var lookup = offset == _count && offset > 0 && !_boundaryAtEnd ? offset - 1 : offset;
        var start = Point(UpperBound(lookup) - 1);
        var endIndex = UpperBound(start);
        var end = endIndex < Length ? Point(endIndex) : _count;
        if (relative < 0)
        {
            var previous = LowerBound(start) - 1;
            return (previous >= 0 ? Point(previous) : 0, start);
        }
        if (relative > 0)
        {
            var next = UpperBound(end);
            return (end, next < Length ? Point(next) : _count);
        }
        return (start, end);
    }
    internal static (int Start, int End) Character(int count, int offset, int relative)
    {
        Validate(count, offset, relative);
        if (relative < 0)
            return (offset > 0 ? offset - 1 : 0, offset);
        var start = relative > 0 && offset < count ? offset + 1 : offset;
        return (start, start < count ? start + 1 : start);
    }
    private static void Validate(int count, int offset, int relative)
    {
        if ((uint)offset > (uint)count)
            throw new ArgumentOutOfRangeException(nameof(offset));
        if (relative is < -1 or > 1)
            throw new ArgumentOutOfRangeException(nameof(relative));
    }
}
