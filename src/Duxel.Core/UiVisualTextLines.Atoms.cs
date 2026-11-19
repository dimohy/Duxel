namespace Duxel.Core;

internal sealed partial class UiVisualTextLines
{
    internal int SnapCaret(int index, bool forward) => AtomicBoundary(forward ? SnapUp(index) : Snap(index), forward);

    private LineGeometry AtomicGeometry(LineGeometry geometry)
    {
        if (_styledSource is not { } source)
            return geometry;
        var projection = source.Projection;
        List<(int Start, int Length)>? ranges = null;
        foreach (var atom in projection.Atoms)
        {
            var start = projection.SourceToDisplay[atom.Start] - geometry.DisplayStart;
            var length = projection.SourceToDisplay[atom.End] - projection.SourceToDisplay[atom.Start];
            if (length > 1 && start >= 0 && start <= geometry.Layout.Length - length)
                (ranges ??= []).Add((start, length));
        }
        return ranges is null ? geometry : geometry with
        {
            Layout = geometry.Layout.WithAtomicRanges(ranges.ToArray())
        };
    }
    private int AtomicBoundary(int index, bool forward)
    {
        if (_styledSource is not { } source)
            return index;
        var atoms = source.Projection.Atoms;
        var low = 0;
        var high = atoms.Length;
        while (low < high)
        {
            var middle = low + (high - low) / 2;
            if (atoms[middle].Start < index)
                low = middle + 1;
            else
                high = middle;
        }
        if (low == 0 || index >= atoms[low - 1].End)
            return index;
        return forward ? SnapUp(atoms[low - 1].End) : Snap(atoms[low - 1].Start);
    }
}
