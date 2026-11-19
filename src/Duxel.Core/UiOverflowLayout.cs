using System.Buffers;

namespace Duxel.Core;

public enum UiOverflowCollapseDirection { End, Start }
public readonly record struct UiOverflowItem(float Extent, int Priority = 0);
public readonly record struct UiOverflowOptions(float Spacing = 0, int MinimumVisible = 0,
    UiOverflowCollapseDirection CollapseFrom = UiOverflowCollapseDirection.End, bool RightToLeft = false)
{
    public UiOverflowOptions Validate()
    {
        if (!float.IsFinite(Spacing) || Spacing < 0 || MinimumVisible < 0 || (uint)CollapseFrom > 1)
            throw new ArgumentOutOfRangeException(nameof(UiOverflowOptions));
        return this;
    }
}
public readonly record struct UiOverflowResult(int VisibleCount, int HiddenCount, double ContentExtent,
    double OccupiedExtent, float IndicatorExtent, bool IndicatorClipped, bool Fits)
{
    public bool IsOverflowing => HiddenCount != 0;
}

/// <summary>Priority removal preserves source order and reserves the indicator only when items are hidden.</summary>
public static class UiOverflowLayout
{
    private struct Candidate(int index, int priority, int rank, float extent) : IComparable<Candidate>
    {
        internal readonly int Index = index;
        private readonly int _priority = priority, _rank = rank;
        internal readonly float Extent = extent;
        internal double SuffixExtent;
        public readonly int CompareTo(Candidate other)
        {
            var priority = _priority.CompareTo(other._priority);
            return priority != 0 ? priority : _rank.CompareTo(other._rank);
        }
    }

    public static UiOverflowResult Resolve(ReadOnlySpan<UiOverflowItem> items, float available,
        float indicatorExtent, Span<bool> visible, UiOverflowOptions options = default)
    {
        options.Validate();
        if (!float.IsFinite(available) || available < 0 || !float.IsFinite(indicatorExtent) || indicatorExtent < 0)
            throw new ArgumentOutOfRangeException(nameof(available));
        if (visible.Length < items.Length) throw new ArgumentException("Visibility storage is shorter than the item list.", nameof(visible));
        double total = Math.Max(0, items.Length - 1) * (double)options.Spacing;
        foreach (var item in items)
        {
            if (!float.IsFinite(item.Extent) || item.Extent < 0)
                throw new ArgumentOutOfRangeException(nameof(items));
            total += item.Extent;
        }
        visible[..items.Length].Fill(true);
        if (total <= available)
            return new(items.Length, 0, total, total, 0, false, true);

        Candidate[]? rented = null;
        Span<Candidate> candidates = items.Length <= 128 ? stackalloc Candidate[items.Length]
            : (rented = ArrayPool<Candidate>.Shared.Rent(items.Length)).AsSpan(0, items.Length);
        try
        {
            for (var i = 0; i < items.Length; i++)
                candidates[i] = new(i, items[i].Priority,
                    options.CollapseFrom == UiOverflowCollapseDirection.End ? items.Length - 1 - i : i, items[i].Extent);
            candidates.Sort();
            double suffix = 0;
            for (var i = candidates.Length - 1; i >= 0; i--)
            {
                suffix += candidates[i].Extent;
                candidates[i].SuffixExtent = suffix + (candidates.Length - i - 1) * (double)options.Spacing;
            }
            var indicator = MathF.Min(indicatorExtent, available);
            var limit = items.Length - Math.Min(items.Length, options.MinimumVisible);
            var removed = 0;
            while (removed < limit)
            {
                if (candidates[removed].SuffixExtent + options.Spacing + indicator <= available) break;
                visible[candidates[removed].Index] = false;
                removed++;
            }
            var count = items.Length - removed;
            var content = count == 0 ? 0 : candidates[removed].SuffixExtent;
            var usedIndicator = removed == 0 ? 0 : indicator;
            var occupied = content + usedIndicator + (removed != 0 && count != 0 ? options.Spacing : 0);
            return new(count, removed, content, occupied, usedIndicator,
                removed != 0 && indicator < indicatorExtent, occupied <= available);
        }
        finally
        {
            if (rented is not null) ArrayPool<Candidate>.Shared.Return(rented);
        }
    }
}
