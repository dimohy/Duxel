using System.Runtime.CompilerServices;

namespace Duxel.Core;

/// <summary>Scalar source offsets: End excludes the line separator; NextStart includes it.</summary>
public readonly record struct UiAccessibilityTextLine(int Start, int End, int NextStart);

internal readonly record struct UiAccessibilityTextComposition(int StartUtf16, string Text, float Width);
internal readonly record struct UiAccessibilityTextVisualKey(float Width, bool Wrap, UiStyledTextProjection? Projection);

public sealed partial class UiAccessibilityTree
{
    internal UiVisualTextLines GetUnwrappedTextLines(string id)
    {
        if (!_building)
            throw new InvalidOperationException("Unwrapped text geometry requires an active accessibility frame.");
        return GetEntry(id).UnwrappedTextLines ??= new();
    }
}

/// <summary>Immutable logical text boxes from the editor's current painting layout, in client coordinates.</summary>
public sealed partial class UiAccessibilityTextLayout
{
    internal UiAccessibilityTextVisualKey? VisualKey { get; init; }
    internal UiAccessibilityTextComposition? Composition { get; init; }
    public bool HasComposition => Composition.HasValue;
    private readonly UiRect[] _characters, _carets;
    private readonly UiAccessibilityTextLine[] _lines;
    private readonly UiVector2 _geometryOrigin, _geometryOffset;
    internal UiLayoutMeasurementKey MeasurementKey
    {
        get;
    }
    internal UiVector2 Origin
    {
        get;
    }
    internal float LineHeight
    {
        get;
    }
    internal int VisualVersion
    {
        get;
    }
    public UiAccessibilityTextContent Content
    {
        get;
    }
    public UiRect ClipBounds
    {
        get;
    }
    public int LineCount => _lines.Length;
    public UiAccessibilityTextLine GetLine(int index)
    {
        if ((uint)index >= (uint)_lines.Length)
            throw new ArgumentOutOfRangeException(nameof(index));
        return _lines[index];
    }

    internal UiAccessibilityTextLayout(UiAccessibilityTextContent content, UiLayoutMeasurementKey key,
        UiVector2 origin, UiRect clip, float height, int visualVersion, UiRect[] characters, UiRect[] carets,
        UiAccessibilityTextLine[] lines, UiVector2? geometryOrigin = null)
    {
        Content = content;
        MeasurementKey = key;
        Origin = origin;
        ClipBounds = clip;
        LineHeight = height;
        VisualVersion = visualVersion;
        _characters = characters;
        _carets = carets;
        _lines = lines;
        _geometryOrigin = geometryOrigin ?? origin;
        _geometryOffset = new(origin.X - _geometryOrigin.X, origin.Y - _geometryOrigin.Y);
    }

    internal UiAccessibilityTextLayout? WithViewport(UiVector2 origin, UiRect clip) => _widgetSource is null
        ? new(Content, MeasurementKey, origin, clip, LineHeight, VisualVersion, _characters, _carets, _lines, _geometryOrigin)
        {
            VisualKey = VisualKey,
            Composition = Composition
        } : null;

    internal bool CanRetainClippedGeometry => _widgetSource is null
        && ((long)_characters.Length + _carets.Length) * Unsafe.SizeOf<UiRect>()
            + (long)_lines.Length * Unsafe.SizeOf<UiAccessibilityTextLine>() <= 256 * 1024;

    private UiRect AtViewport(UiRect box) => box with { X = box.X + _geometryOffset.X, Y = box.Y + _geometryOffset.Y };

    public UiRect GetCharacterExtents(int characterOffset)
    {
        if ((uint)characterOffset >= (uint)_characters.Length)
            throw new ArgumentOutOfRangeException(nameof(characterOffset));
        return AtViewport(_characters[characterOffset]);
    }
    public UiRect GetRangeExtents(int start, int end)
    {
        if (start < 0 || end < start || end > Content.CharacterCount)
            throw new ArgumentOutOfRangeException(nameof(end));
        if (start == end)
            return AtViewport(_carets[start]);
        var box = GetCharacterExtents(start);
        for (var index = start + 1; index < end; index++)
        {
            var next = GetCharacterExtents(index);
            var right = MathF.Max(box.X + box.Width, next.X + next.Width);
            var bottom = MathF.Max(box.Y + box.Height, next.Y + next.Height);
            var x = MathF.Min(box.X, next.X);
            var y = MathF.Min(box.Y, next.Y);
            box = new(x, y, right - x, bottom - y);
        }
        return box;
    }
    public int GetOffsetAtPoint(float x, float y)
    {
        static bool Contains(UiRect box, float x, float y) => x >= box.X && y >= box.Y && x < box.X + box.Width && y < box.Y + box.Height;
        if (!Contains(ClipBounds, x, y))
            return -1;
        if (_widgetSource is { } source)
        {
            if (_widgetClips is { } clips && !clips.ContainsPoint(new(x, y))) return -1;
            var point = UiWidgetAffineLayout.MapPoint(new(x, y), _widgetInverse);
            return source.GetOffsetAtPoint(point.X, point.Y);
        }
        for (var index = 0; index < _characters.Length; index++)
            if (Contains(GetCharacterExtents(index), x, y))
                return index;
        return -1;
    }
    public (int Start, int End) GetLineRangeAt(int offset)
    {
        if ((uint)offset > (uint)Content.CharacterCount)
            throw new ArgumentOutOfRangeException(nameof(offset));
        for (var index = _lines.Length - 1; index >= 0; index--)
            if (offset >= _lines[index].Start)
                return (_lines[index].Start, _lines[index].NextStart);
        throw new InvalidOperationException("Text layout has no source line.");
    }
}
