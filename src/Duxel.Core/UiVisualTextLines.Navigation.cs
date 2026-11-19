namespace Duxel.Core;

internal sealed partial class UiVisualTextLines
{
    internal int MoveHorizontal(int index, bool right, float? physicalX = null)
    {
        var line = FindLine(index);
        if (physicalX is { } requestedX)
        {
            _hitAffinityIndex = index;
            _hitAffinityLine = line;
            _hitAffinityX = requestedX;
        }
        _verticalX = null;
        _verticalCaret = -1;
        if (StyledTableRows.TryGetValue(line, out var table))
            return MoveTableHorizontal(index, line, table, right);
        if (_geometry.Count <= line || _geometry[line] is not { } geometry)
            return right ? Next(index) : Previous(index);
        var x = (physicalX ?? X(index, line)) / geometry.Scale;
        if (geometry.Layout.MoveVisualCaret(x, right) is { } target)
        {
            var result = AcceptGeometryHit(target, line, geometry);
            _affinityIndex = result == _lines[line].End ? result : -1;
            return result;
        }
        var nextLine = line + (right ? 1 : -1);
        if (nextLine < 0 || nextLine >= Count)
            return index;
        ClearAffinity();
        var next = HitLine(right ? -1 : Width + 1, nextLine);
        _affinityIndex = next == _lines[nextLine].End ? next : -1;
        return next;
    }

    internal int CollapseHorizontal(int start, int end, bool right)
    {
        var firstLine = FindLine(start);
        var lastLine = FindLine(end);
        if (firstLine != lastLine)
        {
            ClearAffinity();
            return right ? Math.Max(start, end) : Math.Min(start, end);
        }
        var firstX = X(start, firstLine);
        var lastX = X(end, lastLine);
        var useFirst = right ? firstX > lastX : firstX < lastX;
        var result = useFirst ? start : end;
        ClearAffinity();
        _hitAffinityIndex = result;
        _hitAffinityLine = firstLine;
        _hitAffinityX = useFirst ? firstX : lastX;
        _affinityIndex = result == _lines[firstLine].End ? result : -1;
        return result;
    }
}
