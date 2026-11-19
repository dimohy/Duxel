using System.Buffers;

namespace Duxel.Core;

/// <summary>A source-grapheme fragment in physical line coordinates, in backend font pixels.</summary>
public readonly record struct PlatformTextCharacterGeometry(int Start, int Length, float Left, float Width, uint BidiLevel)
{
    /// <summary>Native pointer transition in physical coordinates; null uses the geometric midpoint.</summary>
    public float? HitBoundaryX
    {
        get; init;
    }
}
public readonly record struct PlatformTextSelectionFragment(float Left, float Width);
public readonly record struct PlatformTextHitTestResult(int Position, float X, bool Trailing);

/// <summary>Optional complete-line geometry for the same explicit fonts and glyph placement used by text rendering.</summary>
public interface IPlatformTextGeometryBackend
{
    PlatformTextLineGeometry CreateLineGeometry(in PlatformTextMeasureRequest request, bool vectorMetrics);
}

/// <summary>Immutable original-UTF16 caret and selection snapshot; no native objects outlive construction.</summary>
public sealed partial class PlatformTextLineGeometry
{
    private readonly float[] _carets;
    private readonly PlatformTextCharacterGeometry[] _characters;
    public int Length => _carets.Length - 1;
    public float Advance
    {
        get;
    }

    public PlatformTextLineGeometry(float advance, ReadOnlySpan<float> carets, ReadOnlySpan<PlatformTextCharacterGeometry> characters)
    {
        if (!float.IsFinite(advance) || advance < 0 || carets.IsEmpty)
            throw new ArgumentException("Line geometry requires finite nonnegative advance and all source boundaries.");
        foreach (var x in carets)
            if (!float.IsFinite(x))
                throw new ArgumentException("Line caret coordinates must be finite.");
        var covered = 0;
        var previousStart = -1;
        foreach (var character in characters)
        {
            if (character.Start < 0 || character.Length <= 0 || character.Start > carets.Length - 1 - character.Length
                || !float.IsFinite(character.Left) || !float.IsFinite(character.Width) || character.Width < 0
                || character.HitBoundaryX is { } hit && (!float.IsFinite(hit) || hit < character.Left || hit > character.Left + character.Width)
                || character.Start < previousStart || character.Start > covered)
                throw new ArgumentException("Line character geometry must cover valid source ranges with finite extents.");
            previousStart = character.Start;
            covered = Math.Max(covered, character.Start + character.Length);
        }
        if (covered != carets.Length - 1)
            throw new ArgumentException("Line character geometry must cover the complete original source.");
        Advance = advance;
        _carets = carets.ToArray();
        _characters = characters.ToArray();
    }

    public float CaretX(int sourcePosition) => _carets[sourcePosition];
    internal float TrailingCaretX(int sourceEnd)
    {
        if (sourceEnd == 0)
            return _carets[0];
        for (var index = _characters.Length - 1; index >= 0; index--)
        {
            var character = _characters[index];
            if (character.Start < sourceEnd && character.Start + character.Length >= sourceEnd)
                return character.Left + ((character.BidiLevel & 1) == 0 ? character.Width : 0);
        }
        throw new ArgumentOutOfRangeException(nameof(sourceEnd));
    }

    public int HitTest(float x) => HitTestCaret(x).Position;

    internal PlatformTextHitTestResult? MoveVisualCaret(float x, bool right)
    {
        if (!float.IsFinite(x))
            throw new ArgumentOutOfRangeException(nameof(x));
        PlatformTextHitTestResult? closest = null;
        foreach (var character in _characters)
        {
            if (character.Width == 0)
                continue;
            var target = character.Left + (right ? character.Width : 0);
            if (right ? target <= x + .0001f : target >= x - .0001f)
                continue;
            if (closest is { } previous && (right ? target >= previous.X : target <= previous.X))
                continue;
            var trailing = right == ((character.BidiLevel & 1) == 0);
            closest = new(character.Start + (trailing ? character.Length : 0), target, trailing);
        }
        return closest;
    }

    public PlatformTextHitTestResult HitTestCaret(float x)
    {
        if (!float.IsFinite(x))
            throw new ArgumentOutOfRangeException(nameof(x));
        var closest = new PlatformTextHitTestResult(0, _carets[0], false);
        var distance = float.PositiveInfinity;
        foreach (var character in _characters)
        {
            var left = character.Left;
            var right = left + character.Width;
            var rtl = (character.BidiLevel & 1) != 0;
            var fromLeft = x < (character.HitBoundaryX ?? (left + right) * .5f);
            var position = fromLeft == !rtl ? character.Start : character.Start + character.Length;
            var candidate = x < left ? left - x : x > right ? x - right : 0;
            if (candidate < distance)
            {
                closest = new(position, fromLeft ? left : right, position != character.Start);
                distance = candidate;
            }
        }
        return closest;
    }

    public PlatformTextSelectionFragment[] SelectionFragments(int start, int end)
    {
        if (start < 0 || end < start || end > Length)
            throw new ArgumentOutOfRangeException(nameof(start));
        const int LocalCapacity = 64;
        PlatformTextSelectionFragment[]? rented = null;
        Span<PlatformTextSelectionFragment> fragments = stackalloc PlatformTextSelectionFragment[LocalCapacity];
        try
        {
            var length = 0;
            for (var index = 0; index < _characters.Length; index++)
            {
                var character = _characters[index];
                if (character.Start >= end)
                    break;
                if (character.Start + character.Length <= start || character.Width <= 0)
                    continue;
                if (length == fragments.Length)
                {
                    var capacity = length + 1;
                    for (var next = index + 1; next < _characters.Length && _characters[next].Start < end; next++)
                        if (_characters[next].Start + _characters[next].Length > start && _characters[next].Width > 0)
                            capacity++;
                    var storage = capacity <= 4096
                        ? rented = ArrayPool<PlatformTextSelectionFragment>.Shared.Rent(capacity)
                        : new PlatformTextSelectionFragment[capacity];
                    fragments.CopyTo(storage);
                    fragments = storage;
                }
                fragments[length++] = new(character.Left, character.Width);
            }
            fragments[..length].Sort(static (a, b) => a.Left.CompareTo(b.Left));
            var count = 0;
            for (var index = 0; index < length; index++)
            {
                var fragment = fragments[index];
                if (count > 0 && fragment.Left <= fragments[count - 1].Left + fragments[count - 1].Width + .0001f)
                {
                    var previous = fragments[count - 1];
                    fragments[count - 1] = previous with
                    {
                        Width = MathF.Max(previous.Width, fragment.Left + fragment.Width - previous.Left)
                    };
                }
                else
                    fragments[count++] = fragment;
            }
            return fragments[..count].ToArray();
        }
        finally
        {
            if (rented is not null)
                ArrayPool<PlatformTextSelectionFragment>.Shared.Return(rented);
        }
    }
}
