namespace Duxel.Core;

/// <summary>Provides cumulative item boundaries along the scrolling axis of a list snapshot.</summary>
public interface IUiVirtualListExtentIndex
{
    /// <summary>Returns the position before index. Index zero is zero and ItemCount gives the total extent.</summary>
    double GetBoundary(int index);

    /// <summary>Returns the current index of a stable key, or -1 if it is absent.</summary>
    int FindIndexByKey(string key, int itemCount);
}

internal readonly record struct UiVirtualItemExtent(double Start, double End, float Length);

internal readonly struct UiVirtualListGeometry
{
    internal readonly int Count;
    internal readonly double ContentExtent;
    private readonly float _fixedExtent;
    private readonly IUiVirtualListExtentIndex? _index;

    internal UiVirtualListGeometry(int count, float fixedExtent, IUiVirtualListExtentIndex? index)
    {
        Count = count;
        _fixedExtent = fixedExtent;
        _index = index;
        ContentExtent = index is null ? (double)count * fixedExtent : index.GetBoundary(count);
        if (index is not null && (index.GetBoundary(0) != 0 || !double.IsFinite(ContentExtent)
            || (count == 0 ? ContentExtent != 0 : ContentExtent <= 0)))
        {
            throw new InvalidOperationException("Virtual item boundaries must start at zero and define a finite positive extent for a nonempty list.");
        }
    }

    internal UiVirtualItemExtent Item(int index)
    {
        if (_index is null)
            return new((double)index * _fixedExtent, (double)(index + 1) * _fixedExtent, _fixedExtent);

        var start = Boundary(index);
        var end = Boundary(index + 1);
        var extent = (float)(end - start);
        if (!float.IsFinite(extent) || extent <= 0)
            throw new InvalidOperationException("Consecutive virtual item boundaries must define a finite, positive item extent.");
        return new(start, end, extent);
    }

    internal int First(double position)
    {
        if (_index is null)
            return (int)Math.Clamp(Math.Floor(position / _fixedExtent), 0, Count);
        return position >= ContentExtent ? Count : Math.Max(0, Search(position, afterEqual : true) - 1);
    }

    internal int End(double position) => _index is null
        ? (int)Math.Clamp(Math.Ceiling(position / _fixedExtent), 0, Count)
        : Search(position, afterEqual : false);

    internal (int First, int End) OverscanRange(in UiVirtualViewport viewport, int overscan, out int visibleFirst)
    {
        visibleFirst = 0;
        if (Count == 0 || viewport.Clip is not { Width : > 0, Height : > 0 })
            return default;
        var start = viewport.Offset + viewport.LeadingInset;
        var end = viewport.Offset + viewport.TrailingInset;
        if (start >= ContentExtent)
            return default;
        visibleFirst = First(start);
        return ((int)Math.Max(0, (long)visibleFirst - overscan), (int)Math.Min(Count, (long)End(end) + overscan));
    }

    private int Search(double position, bool afterEqual)
    {
        var low = 0;
        var high = Count;
        while (low < high)
        {
            var middle = low + (high - low) / 2;
            var boundary = Boundary(middle);
            if (boundary < position || afterEqual && boundary == position)
                low = middle + 1;
            else
                high = middle;
        }
        return low;
    }

    private double Boundary(int index)
    {
        var position = _index!.GetBoundary(index);
        if (!double.IsFinite(position) || position < 0 || position > ContentExtent)
            throw new InvalidOperationException("A virtual item boundary is outside the current extent.");
        return position;
    }
}
