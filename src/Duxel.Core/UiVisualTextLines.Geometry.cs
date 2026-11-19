namespace Duxel.Core;

internal sealed partial class UiVisualTextLines
{
    private readonly record struct LineGeometry(PlatformTextLineGeometry Layout, int DisplayStart, float Scale);
    private readonly List<LineGeometry?> _geometry = [];
    private int _hitAffinityIndex = -1;
    private int _hitAffinityLine = -1;
    private float _hitAffinityX;

    private void PrepareGeometry(UiImmediateContext ui)
    {
        _geometry.Clear();
        if (_nativeParagraph is { } paragraph)
        {
            foreach (var row in paragraph.Lines.Span)
                _geometry.Add(AtomicGeometry(new(row.Geometry, row.Start, _nativeParagraphScale)));
            return;
        }
        for (var line = 0; line < Count; line++)
        {
            if (StyledParagraphRows.TryGetValue(line, out var fragment))
            {
                var geometry = fragment.Layout._geometry[fragment.Line];
                _geometry.Add(geometry is { } native ? native with
                {
                    DisplayStart = native.DisplayStart + fragment.DisplayStart
                } : null);
                continue;
            }
            var row = _lines[line];
            if (_styledSource is not { } styled)
            {
                _geometry.Add(ui.TryPrepareLineGeometry(row.Text, out var plain, out var scale) ? new(plain!, row.Start, scale) : null);
                continue;
            }
            var projection = styled.Projection;
            var start = projection.SourceToDisplay[row.Start];
            var end = projection.SourceToDisplay[row.End];
            var style = projection.StyleAt(start);
            var uniform = !StyledTableRows.ContainsKey(line) && style.ImageIndex == 0 && style.ObjectIndex == 0
                && (style.Flags & (UiStyledTextFlags.Task | UiStyledTextFlags.Rule)) == 0;
            foreach (var run in projection.Runs)
                if (run.Start < end && run.Start + run.Length > start && run.Style != style)
                    uniform = false;
            if (!uniform)
            {
                _geometry.Add(null);
                continue;
            }
            using var typography = ui.BeginStyledTypography(style);
            _geometry.Add(ui.TryPrepareLineGeometry(projection.Text[start..end], out var layout, out var logicalScale)
                ? AtomicGeometry(new(layout!, start, logicalScale)) : null);
        }
    }

    private int GeometryPosition(int source, LineGeometry geometry) =>
        (_styledSource?.Projection.SourceToDisplay[source] ?? source) - geometry.DisplayStart;

    internal PlatformTextSelectionFragment[] SelectionFragments(int start, int end, int line)
    {
        if (StyledTableRows.TryGetValue(line, out var table))
        {
            var fragments = new List<PlatformTextSelectionFragment>();
            for (var column = 0; column < table.Source.Cells.Length; column++)
            {
                var cell = table.Source.Cells[column];
                var first = Math.Max(start, cell.Start);
                var last = Math.Min(end, cell.End);
                if (first >= last)
                    continue;
                foreach (var fragment in table.CellLayouts[column].SelectionFragments(first - cell.Start, last - cell.Start, 0))
                    fragments.Add(fragment with
                    {
                        Left = table.CellX[column] + fragment.Left
                    });
            }
            return fragments.ToArray();
        }
        if (_geometry.Count > line && _geometry[line] is { } geometry)
        {
            var fragments = geometry.Layout.SelectionFragments(GeometryPosition(start, geometry), GeometryPosition(end, geometry));
            for (var index = 0; index < fragments.Length; index++)
            {
                var fragment = fragments[index];
                fragments[index] = new(fragment.Left * geometry.Scale, fragment.Width * geometry.Scale);
            }
            return fragments;
        }
        var left = X(start, line);
        return [new(left, MathF.Max(0, X(end, line) - left))];
    }

    private int HitGeometry(float x, int line, LineGeometry geometry)
    {
        var hit = geometry.Layout.HitTestCaret(x / geometry.Scale);
        return AcceptGeometryHit(hit, line, geometry);
    }

    private int AcceptGeometryHit(PlatformTextHitTestResult hit, int line, LineGeometry geometry)
    {
        var display = hit.Position + geometry.DisplayStart;
        var source = display;
        if (_styledSource is { } styled)
        {
            var low = _lines[line].Start;
            var high = _lines[line].End;
            var map = styled.Projection.SourceToDisplay;
            while (low < high)
            {
                var middle = low + (high - low) / 2;
                if (map[middle] < display)
                    low = middle + 1;
                else
                    high = middle;
            }
            source = low;
            if (!hit.Trailing)
                while (source < _lines[line].End && map[source + 1] == display)
                    source++;
        }
        source = AtomicBoundary(Snap(source), forward: hit.Trailing);
        _hitAffinityIndex = source;
        _hitAffinityLine = line;
        _hitAffinityX = hit.X * geometry.Scale;
        return source;
    }
}

public sealed partial class UiImmediateContext
{
    internal bool TryPrepareLineGeometry(string text, out PlatformTextLineGeometry? geometry, out float scale)
    {
        geometry = null;
        scale = 1f / _contentScale;
        if (text.Length == 0 || _platformTextBackend is not IPlatformTextGeometryBackend backend
            || !ShouldUseDirectTextForText(text, _textSettings))
            return false;
        geometry = backend.CreateLineGeometry(new(_directTextPrimaryFontPath!, _directTextSecondaryFontPath,
            text, ResolveDirectTextFontSize(_textSettings)), IsVectorTextLayer());
        if (geometry.Length != text.Length)
            throw new InvalidOperationException("Line geometry must preserve every original UTF16 source boundary.");
        return true;
    }
}
