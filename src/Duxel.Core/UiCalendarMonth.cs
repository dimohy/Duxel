namespace Duxel.Core;

public readonly record struct UiCalendarCell(DateOnly? Date, UiCalendarDateParts? Parts, bool IsCurrentMonth,
    string Text, string AccessibleName)
{
    public bool IsSupported => Parts.HasValue;
}

/// <summary>Prepared immutable chronological cells; RTL maps columns without reversing date arithmetic.</summary>
public sealed class UiCalendarMonth
{
    private readonly UiCalendarCell[] _cells;
    public IUiCalendarSystem System { get; }
    public DateOnly Anchor { get; }
    public int MonthStartDayNumber { get; }
    public int DaysInMonth { get; }
    public int LeadingDays { get; }
    public DayOfWeek FirstDayOfWeek { get; }
    public bool RightToLeft { get; }
    public string Title { get; }
    public ReadOnlyMemory<UiCalendarCell> Cells => _cells;
    public int WeekCount => _cells.Length / 7;
    public UiCalendarMonth(IUiCalendarSystem system, DateOnly anchor, DayOfWeek? firstDayOfWeek = null, bool? rightToLeft = null)
    {
        ArgumentNullException.ThrowIfNull(system);
        if (anchor < system.MinimumDate || anchor > system.MaximumDate) throw new ArgumentOutOfRangeException(nameof(anchor));
        System = system;
        Anchor = anchor;
        FirstDayOfWeek = firstDayOfWeek ?? system.FirstDayOfWeek;
        if ((uint)FirstDayOfWeek > 6) throw new ArgumentOutOfRangeException(nameof(firstDayOfWeek));
        RightToLeft = rightToLeft ?? system.RightToLeft;
        var parts = system.GetParts(anchor);
        DaysInMonth = system.GetDaysInMonth(anchor);
        if (parts.Day < 1 || DaysInMonth < parts.Day) throw new InvalidOperationException("Calendar returned an invalid month.");
        MonthStartDayNumber = checked(anchor.DayNumber - parts.Day + 1);
        var weekday = ((MonthStartDayNumber + 1) % 7 + 7) % 7;
        LeadingDays = (weekday - (int)FirstDayOfWeek + 7) % 7;
        var cellCount = checked(Math.Max(6, (LeadingDays + DaysInMonth + 6) / 7) * 7);
        _cells = new UiCalendarCell[cellCount];
        var gridStart = MonthStartDayNumber - LeadingDays;
        for (var i = 0; i < cellCount; i++)
        {
            var day = checked(gridStart + i);
            if (day < DateOnly.MinValue.DayNumber || day > DateOnly.MaxValue.DayNumber)
            {
                _cells[i] = new(null, null, false, string.Empty, string.Empty);
                continue;
            }
            var date = DateOnly.FromDayNumber(day);
            var current = day >= MonthStartDayNumber && day < (long)MonthStartDayNumber + DaysInMonth;
            _cells[i] = date < system.MinimumDate || date > system.MaximumDate
                ? new(date, null, current, string.Empty, string.Empty)
                : new(date, system.GetParts(date), current, system.FormatDay(date), system.FormatFullDate(date));
        }
        var first = DateOnly.FromDayNumber(Math.Max(MonthStartDayNumber, system.MinimumDate.DayNumber));
        var last = DateOnly.FromDayNumber((int)Math.Min((long)MonthStartDayNumber + DaysInMonth - 1, system.MaximumDate.DayNumber));
        Title = system.FormatMonthHeader(first, last);
    }
    public int PhysicalColumn(int logicalColumn)
    {
        if ((uint)logicalColumn > 6) throw new ArgumentOutOfRangeException(nameof(logicalColumn));
        return RightToLeft ? 6 - logicalColumn : logicalColumn;
    }
    public bool TryGetAdjacentMonth(int direction, out DateOnly anchor)
    {
        if (direction is not (-1 or 1)) throw new ArgumentOutOfRangeException(nameof(direction));
        var day = (long)MonthStartDayNumber + (direction < 0 ? -1 : DaysInMonth);
        if (day < System.MinimumDate.DayNumber || day > System.MaximumDate.DayNumber)
        {
            anchor = default;
            return false;
        }
        anchor = DateOnly.FromDayNumber((int)day);
        return true;
    }
}

public readonly record struct UiCalendarDateRange
{
    public DateOnly Start { get; }
    public DateOnly End { get; }
    public UiCalendarDateRange(DateOnly start, DateOnly end)
    {
        if (end < start) throw new ArgumentException("A date range must end on or after its start.", nameof(end));
        Start = start;
        End = end;
    }
    public static UiCalendarDateRange FromEndpoints(DateOnly first, DateOnly second) => first <= second ? new(first, second) : new(second, first);
    public bool Contains(DateOnly date) => date >= Start && date <= End;
    public bool Intersects(UiCalendarDateRange other) => Start <= other.End && other.Start <= End;
}
