using System.Globalization;

namespace Duxel.Core;

internal readonly record struct UiVisualTextLine(int Start, int End, string Text, float Width, float Y = 0, float Height = 0);

internal sealed partial class UiVisualTextLines
{
    internal bool Wrap => _wrap;
    private readonly List<UiVisualTextLine> _lines = [];
    private string? _text;
    internal string Text => _text ?? throw new InvalidOperationException("Visual text must be prepared before painting.");
    private int[] _boundaries = [];
    private float[] _advances = [];
    private UiLayoutMeasurementKey _key;
    private float _requestedWidth;
    private float _requestedHeight;
    private float _lineHeight;
    private float _gutter;
    private float _horizontalGutter;
    private bool _wrap = true;
    private bool _valid;
    private int _frame = -1;
    private int _affinityIndex = -1;
    private float? _verticalX;
    private int _verticalCaret = -1;
    private UiVisualTextLines? _composition;
    private UiVisualTextLines? _placeholder;
    private UiStyledTextSource? _styledSource;
    private long _styledRevision;
    private int _activeSourceIndex = -1;
    private bool _preserveStyledBlock;
    internal UiStyledTextSource? StyledSource => _styledSource;
    internal bool HasUnwrappedOverflow
    {
        get; private set;
    }
    internal UiVisualTextLines Placeholder => _placeholder ??= new();
    internal void ClearPlaceholder()
    {
        _placeholder?.Clear();
        _placeholder = null;
    }
    internal int Count => _lines.Count;
    internal int Builds
    {
        get; private set;
    }
    internal int Version
    {
        get; private set;
    }
    internal int LayoutRevision
    {
        get; private set;
    }
    internal float Width
    {
        get; private set;
    }
    internal float Height
    {
        get; private set;
    }
    internal float Y(int line) => _lines[line].Y;
    internal float LineHeight(int line) => _lines[line].Height;
    internal float EffectiveWidth
    {
        get; private set;
    }
    internal float EffectiveHeight
    {
        get; private set;
    }
    internal UiVisualTextLine this[int index] => _lines[index];
    internal UiVisualTextLines Composition => _composition ??= new();
    internal void ClearComposition()
    {
        _composition?.Clear();
        _composition = null;
    }

    internal void Prepare(UiImmediateContext ui, string text, float width, float height, float lineHeight,
        float gutter, bool wrap = true, float horizontalGutter = 0, UiStyledTextSource? styled = null, int activeSourceIndex = -1,
        bool preserveActiveBlock = false)
    {
        styled?.Prepare(text, activeSourceIndex, preserveActiveBlock);
        _activeSourceIndex = activeSourceIndex;
        _preserveStyledBlock = preserveActiveBlock;
        var key = ui.LayoutMeasurementKey;
        var frame = ui.GetFrameCount();
        var same = _text == text && _key == key && _requestedWidth == width && _requestedHeight == height
            && _lineHeight == lineHeight && _gutter == gutter && _wrap == wrap && _horizontalGutter == horizontalGutter
            && ReferenceEquals(_styledSource, styled) && _styledRevision == (styled?.Revision ?? 0);
        var retain = ui.CanRetainControlTextLayout;
        if (same && (_valid && retain || _frame == frame && _valid == retain))
            return;
        // Plain/code geometry depends on width, not the clipping height. A code
        // view measures its natural height first, then paints that same layout.
        if (retain && _valid && wrap && _wrap && _requestedWidth == width
            && EffectiveWidth == width && (gutter == 0 || Height <= height)
            && (horizontalGutter == 0 || Width <= width)
            && (styled is null || styled is UiCodeDocument)
            && _text == text && _key == key && _lineHeight == lineHeight
            && ReferenceEquals(_styledSource, styled) && _styledRevision == (styled?.Revision ?? 0))
        {
            _requestedHeight = height;
            _gutter = gutter;
            _horizontalGutter = horizontalGutter;
            UpdateUnwrappedViewport(width, height, gutter, horizontalGutter);
            _frame = frame;
            return;
        }
        // Unwrapped plain/code paragraphs have no width-dependent objects or line breaks.
        if (!wrap && !_wrap && retain && _valid && (styled is null || styled is UiCodeDocument)
            && _text == text && _key == key && _lineHeight == lineHeight
            && ReferenceEquals(_styledSource, styled) && _styledRevision == (styled?.Revision ?? 0))
        {
            _requestedWidth = width;
            _requestedHeight = height;
            _gutter = gutter;
            _horizontalGutter = horizontalGutter;
            _frame = frame;
            UpdateUnwrappedViewport(width, height, gutter, horizontalGutter);
            return;
        }
        if (!same)
        {
            ClearAffinity();
            LayoutRevision++;
        }
        _valid = false;
        _frame = -1;
        _styledSource = styled;
        _styledRevision = styled?.Revision ?? 0;
        _lineHeight = lineHeight;
        var starts = StringInfo.ParseCombiningCharacters(text);
        _boundaries = new int[starts.Length + 1];
        starts.CopyTo(_boundaries, 0);
        _boundaries[^1] = text.Length;
        _advances = new float[_boundaries.Length];
        Build(ui, text, width, wrap);
        EffectiveHeight = height;
        if (wrap && _tableSourceRows.Count > 0)
        {
            var vertical = false;
            var horizontal = false;
            for (var pass = 0; pass < 3; pass++)
            {
                horizontal |= Width - (width - (vertical ? gutter : 0)) > .5f;
                vertical |= Height - (height - (horizontal ? horizontalGutter : 0)) > .5f;
                var available = MathF.Max(0, width - (vertical ? gutter : 0));
                if (EffectiveWidth != available)
                    Build(ui, text, available, wrap);
            }
            EffectiveHeight = MathF.Max(0, height - (horizontal ? horizontalGutter : 0));
        }
        else if (wrap)
        {
            if (Height - height > 0.5f && gutter > 0)
                Build(ui, text, MathF.Max(0, width - gutter), wrap);
        }
        else
        {
            UpdateUnwrappedViewport(width, height, gutter, horizontalGutter);
        }
        PrepareGeometry(ui);
        if (_lines.Capacity > Math.Max(64, _lines.Count * 4L))
            _lines.Capacity = Math.Max(64, _lines.Count);
        _text = text;
        _key = key;
        _requestedWidth = width;
        _requestedHeight = height;
        _lineHeight = lineHeight;
        _gutter = gutter;
        _horizontalGutter = horizontalGutter;
        _wrap = wrap;
        _valid = retain;
        _frame = frame;
        Builds++;
        Version++;
    }

    private void UpdateUnwrappedViewport(float width, float height, float gutter, float horizontalGutter)
    {
        var vertical = false;
        var horizontal = false;
        for (var pass = 0; pass < 3; pass++)
        {
            horizontal |= Width - (width - (vertical ? gutter : 0)) > .5f;
            vertical |= Height - (height - (horizontal ? horizontalGutter : 0)) > .5f;
        }
        EffectiveWidth = MathF.Max(0, width - (vertical ? gutter : 0));
        EffectiveHeight = MathF.Max(0, height - (horizontal ? horizontalGutter : 0));
    }

    private void Build(UiImmediateContext ui, string text, float width, bool wrap)
    {
        ClearNativeParagraph();
        if (_styledSource is not null)
        {
            BuildStyled(ui, text, width, wrap);
            return;
        }
        _lines.Clear();
        ClearStyledTables();
        Width = 0;
        EffectiveWidth = width;
        var begin = 0;
        while (true)
        {
            var end = begin;
            var whitespace = -1;
            var measured = 0f;
            while (end < _boundaries.Length - 1 && text[_boundaries[end]] is not ('\r' or '\n'))
            {
                var next = end + 1;
                var candidate = ui.MeasureMultilineSpan(text.AsSpan(_boundaries[begin], _boundaries[next] - _boundaries[begin]));
                if (!float.IsFinite(candidate) || candidate < 0)
                    throw new InvalidOperationException("Wrapped text returned invalid advance geometry.");
                if (wrap && candidate > width && end > begin)
                    break;
                measured = candidate;
                _advances[next] = candidate;
                end = next;
                if (char.IsWhiteSpace(text[_boundaries[end] - 1]))
                    whitespace = end;
                if (wrap && candidate > width)
                    break;
            }
            if (wrap && end < _boundaries.Length - 1 && text[_boundaries[end]] is not ('\r' or '\n')
                && whitespace > begin && whitespace < end)
            {
                end = whitespace;
                measured = _advances[end];
            }
            var start = _boundaries[begin];
            var stop = _boundaries[end];
            _lines.Add(new(start, stop, text[start..stop], measured, _lines.Count * _lineHeight, _lineHeight));
            Width = MathF.Max(Width, measured);
            if (end == _boundaries.Length - 1)
                break;
            begin = text[_boundaries[end]] is '\r' or '\n' ? end + 1 : end;
        }
        Height = Count * _lineHeight;
    }

    internal int FindLine(int index)
    {
        if (_hitAffinityIndex == index && _hitAffinityLine >= 0)
            return _hitAffinityLine;
        for (var line = 0; line < Count; line++)
        {
            var row = _lines[line];
            if (index < row.End || index == row.End
                && (line == Count - 1 || _lines[line + 1].Start > index || _affinityIndex == index))
                return line;
        }
        return Count - 1;
    }

    internal float X(int index, int line)
    {
        var row = _lines[line];
        if (_hitAffinityIndex == index && _hitAffinityLine == line)
            return _hitAffinityX;
        if (_geometry.Count > line && _geometry[line] is { } geometry)
            return geometry.Layout.CaretX(GeometryPosition(Math.Clamp(index, row.Start, row.End), geometry)) * geometry.Scale;
        if (index <= row.Start)
            return 0;
        var boundary = Array.BinarySearch(_boundaries, Math.Min(index, row.End));
        if (boundary < 0)
            boundary = ~boundary - 1;
        return _advances[Math.Max(0, boundary)];
    }

    internal int Hit(float x, float y, float lineHeight)
    {
        var line = LineAtY(y);
        ClearAffinity();
        var index = HitLine(x, line);
        _affinityIndex = index == _lines[line].End ? index : -1;
        return index;
    }

    private int HitLine(float x, int line)
    {
        if (StyledTableRows.TryGetValue(line, out var table))
            return AtomicBoundary(HitStyledTableCell(x, line, table), forward: true);
        if (_geometry.Count > line && _geometry[line] is { } geometry)
            return HitGeometry(x, line, geometry);
        var row = _lines[line];
        var first = Array.BinarySearch(_boundaries, row.Start);
        var last = Array.BinarySearch(_boundaries, row.End);
        var previous = 0f;
        for (var boundary = first + 1; boundary <= last; boundary++)
        {
            var next = _advances[boundary];
            if (x < (previous + next) * 0.5f)
                return AtomicBoundary(_boundaries[boundary - 1], forward: true);
            previous = next;
        }
        return row.End;
    }

    internal int MoveVertical(int index, int delta)
    {
        var from = FindLine(index);
        var target = Math.Clamp((long)from + delta, 0, Count - 1);
        if (_verticalCaret != index)
            _verticalX = null;
        var x = _verticalX ?? X(index, from);
        _verticalX = x;
        var result = HitLine(x, (int)target);
        _verticalCaret = result;
        _affinityIndex = result == _lines[(int)target].End ? result : -1;
        return result;
    }

    internal int Home(int index)
    {
        var result = _lines[FindLine(index)].Start;
        ClearAffinity();
        return result;
    }
    internal (int Start, int End) HardLineRange(int index)
    {
        var first = FindLine(index);
        var last = first;
        while (first > 0 && _lines[first].Start == _lines[first - 1].End)
            first--;
        while (last < Count - 1 && _lines[last + 1].Start == _lines[last].End)
            last++;
        return (_lines[first].Start, _lines[last].End);
    }
    internal int End(int index)
    {
        var result = _lines[FindLine(index)].End;
        ClearAffinity();
        _affinityIndex = result;
        return result;
    }
    internal int Previous(int index)
    {
        ClearAffinity();
        var atomicBoundary = AtomicBoundary(index, forward: false);
        if (atomicBoundary != index)
            return atomicBoundary;
        var position = Array.BinarySearch(_boundaries, index);
        position = Math.Max(0, position >= 0 ? position - 1 : ~position - 1);
        if (_styledSource is { } styled)
        {
            var display = styled.Projection.SourceToDisplay;
            while (position > 0 && display[_boundaries[position]] == display[index])
                position--;
        }
        return AtomicBoundary(_boundaries[position], forward: false);
    }
    internal int Next(int index)
    {
        ClearAffinity();
        var atomicBoundary = AtomicBoundary(index, forward: true);
        if (atomicBoundary != index)
            return atomicBoundary;
        var position = Array.BinarySearch(_boundaries, index);
        position = Math.Min(_boundaries.Length - 1, position >= 0 ? position + 1 : ~position);
        if (_styledSource is { } styled)
        {
            var display = styled.Projection.SourceToDisplay;
            while (position < _boundaries.Length - 1 && display[_boundaries[position]] == display[index])
                position++;
        }
        return AtomicBoundary(_boundaries[position], forward: true);
    }
    internal void ClearAffinity()
    {
        _hitAffinityIndex = _hitAffinityLine = -1;
        _affinityIndex = -1;
        _verticalX = null;
        _verticalCaret = -1;
    }
    internal void Refresh(UiImmediateContext ui, string text, int caretIndex) =>
        Prepare(ui, text, _requestedWidth, _requestedHeight, _lineHeight, _gutter, _wrap, _horizontalGutter, _styledSource,
            _activeSourceIndex < 0 ? -1 : caretIndex, _preserveStyledBlock);
    internal int Snap(int index)
    {
        var position = Array.BinarySearch(_boundaries, index);
        return position >= 0 ? index : _boundaries[Math.Clamp(~position - 1, 0, _boundaries.Length - 1)];
    }
    internal int SnapUp(int index)
    {
        var position = Array.BinarySearch(_boundaries, index);
        return position >= 0 ? index : _boundaries[Math.Clamp(~position, 0, _boundaries.Length - 1)];
    }
    internal UiTextSelection AlignSelection(UiTextSelection selection) => selection.Start <= selection.End
        ? new(SnapCaret(selection.Start, forward: false), SnapCaret(selection.End, forward: true))
        : new(SnapCaret(selection.Start, forward: true), SnapCaret(selection.End, forward: false));
    internal void Clear()
    {
        ClearAlignment();
        ClearNativeParagraph();
        ClearPlaceholder();
        _lines.Clear();
        _lines.Capacity = 0;
        _boundaries = [];
        _advances = [];
        _geometry.Clear();
        _text = null;
        _styledSource = null;
        ClearStyledTables();
        _styledRevision = 0;
        _activeSourceIndex = -1;
        _preserveStyledBlock = false;
        Height = 0;
        _key = default;
        _valid = false;
        _frame = -1;
        ClearAffinity();
        _composition?.Clear();
        _composition = null;
        Version++;
    }
}

public sealed partial class UiImmediateContext
{
    internal float MeasureMultilineSpan(ReadOnlySpan<char> text) => MeasureTextWidthSpan(text);
}
