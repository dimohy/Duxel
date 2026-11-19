namespace Duxel.Core;

/// <summary>Immutable date bounds and normalized inclusive blackout intervals.</summary>
public sealed class UiCalendarAvailability
{
    private readonly UiCalendarDateRange[] _blackouts;
    public DateOnly Minimum { get; }
    public DateOnly Maximum { get; }
    public UiCalendarAvailability(DateOnly? minimum = null, DateOnly? maximum = null,
        IEnumerable<UiCalendarDateRange>? blackouts = null)
    {
        Minimum = minimum ?? DateOnly.MinValue;
        Maximum = maximum ?? DateOnly.MaxValue;
        if (Maximum < Minimum) throw new ArgumentException("Calendar maximum precedes its minimum.");
        var merged = new List<UiCalendarDateRange>();
        foreach (var item in (blackouts ?? []).OrderBy(item => item.Start))
        {
            if (merged.Count > 0 && item.Start.DayNumber <= (long)merged[^1].End.DayNumber + 1)
                merged[^1] = new(merged[^1].Start, item.End > merged[^1].End ? item.End : merged[^1].End);
            else merged.Add(item);
        }
        _blackouts = merged.ToArray();
    }
    public bool ContainsDate(DateOnly date) => ContainsRange(new(date, date));
    public bool ContainsRange(UiCalendarDateRange range)
    {
        if (range.Start < Minimum || range.End > Maximum) return false;
        var low = 0;
        var high = _blackouts.Length;
        while (low < high)
        {
            var middle = (low + high) / 2;
            if (_blackouts[middle].Start <= range.End) low = middle + 1;
            else high = middle;
        }
        return low == 0 || !_blackouts[low - 1].Intersects(range);
    }
}
