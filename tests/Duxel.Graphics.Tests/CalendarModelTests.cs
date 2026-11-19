using Duxel.Core;
using System.Globalization;

internal static class CalendarModelTests
{
    internal static void Run(Action<bool, string> check)
    {
        var us = new UiCultureCalendar(CultureInfo.GetCultureInfo("en-US"));
        var uk = new UiCultureCalendar(CultureInfo.GetCultureInfo("en-GB"));
        var march = new UiCalendarMonth(us, new(2024, 3, 15));
        check(march.Cells.Length == 42 && march.WeekCount == 6 && march.LeadingDays == 5
            && march.Cells.Span[0].Date == new DateOnly(2024, 2, 25)
            && march.Cells.Span[^1].Date == new DateOnly(2024, 4, 6),
            "US March2024 prepares six complete Sunday-first weeks with adjacent-month dates");
        var british = new UiCalendarMonth(uk, new(2024, 3, 15));
        check(british.LeadingDays == 4 && british.Cells.Span[0].Date == new DateOnly(2024, 2, 26),
            "UK first weekday changes grid arithmetic rather than only its labels");
        var leap = new UiCalendarMonth(us, new(2024, 2, 1));
        var century = new UiCalendarMonth(us, new(2100, 2, 1));
        check(leap.DaysInMonth == 29 && century.DaysInMonth == 28,
            "Gregorian leap-month geometry handles divisible-by400 versus century exceptions");
        var original = (CultureInfo)CultureInfo.GetCultureInfo("en-GB").Clone();
        var frozen = new UiCultureCalendar(original);
        original.DateTimeFormat.FirstDayOfWeek = DayOfWeek.Sunday;
        check(frozen.FirstDayOfWeek == DayOfWeek.Monday && frozen.Culture.IsReadOnly && frozen.Calendar.IsReadOnly,
            "calendar locale is an immutable snapshot and ignores later caller mutation");
        var native = new UiCultureCalendar(CultureInfo.GetCultureInfo("ar-SA"));
        var latin = new UiCultureCalendar(CultureInfo.GetCultureInfo("ar-SA"), dayNumbers : UiCalendarDayNumbers.Latin);
        var ramadan = DateOnly.FromDateTime(native.Calendar.ToDateTime(1445, 9, 1, 0, 0, 0, 0));
        check(native.RightToLeft && native.FormatNumber(123) == "١٢٣" && native.FormatDay(ramadan) == "١" && latin.FormatDay(ramadan) == "1",
            "Arabic native digits are shaped explicitly while Latin digits remain available");
        var arabic = new UiCalendarMonth(native, new(2024, 3, 15));
        check(arabic.RightToLeft && arabic.PhysicalColumn(0) == 6 && arabic.PhysicalColumn(6) == 0
            && arabic.Cells.ToArray().Where(cell => cell.Date.HasValue).Select(cell => cell.Date!.Value.DayNumber)
                .Zip(arabic.Cells.ToArray().Where(cell => cell.Date.HasValue).Select(cell => cell.Date!.Value.DayNumber).Skip(1))
                .All(pair => pair.Second == pair.First + 1),
            "RTL mirrors physical columns without reversing chronological cell data");
        var hebrewCalendar = new HebrewCalendar();
        var hebrew = new UiCultureCalendar(CultureInfo.GetCultureInfo("he-IL"), hebrewCalendar);
        var adarOne = DateOnly.FromDateTime(hebrewCalendar.ToDateTime(5784, 6, 1, 0, 0, 0, 0));
        var adar = new UiCalendarMonth(hebrew, adarOne);
        check(adar.TryGetAdjacentMonth(1, out var adarTwo) && hebrew.GetParts(adarTwo).Month == 7
            && adar.Title != new UiCalendarMonth(hebrew, adarTwo).Title,
            "Hebrew leap-year navigation preserves distinct AdarI and AdarII months");
        var hebrewFirst = DateOnly.FromDateTime(hebrewCalendar.ToDateTime(5784, 1, 1, 0, 0, 0, 0));
        var hebrewFifteenth = DateOnly.FromDateTime(hebrewCalendar.ToDateTime(5784, 1, 15, 0, 0, 0, 0));
        check(hebrew.FormatDay(hebrewFirst).Contains("א", StringComparison.Ordinal)
            && hebrew.FormatDay(hebrewFifteenth).Contains("ט", StringComparison.Ordinal),
            "Hebrew culture day cells retain non-positional letter numerals instead of forcing decimal digits");
        var latinHebrew = new UiCultureCalendar(CultureInfo.GetCultureInfo("he-IL"), hebrewCalendar,
            UiCalendarDayNumbers.Latin);
        check(latinHebrew.FormatDay(hebrewFifteenth) == "15"
            && latinHebrew.FormatFullDate(hebrewFirst) == hebrew.FormatFullDate(hebrewFirst),
            "explicit Latin day cells preserve native full-date accessibility wording");
        var lastHebrew = new UiCalendarMonth(hebrew, DateOnly.FromDateTime(hebrewCalendar.ToDateTime(5784, 13, 1, 0, 0, 0, 0)));
        check(lastHebrew.TryGetAdjacentMonth(1, out var nextHebrew)
            && hebrew.GetParts(nextHebrew) is { Year : 5785, Month : 1 },
            "thirteenth Hebrew month navigates into the next year without a twelve-month assumption");
        var japanese = new UiCultureCalendar(CultureInfo.GetCultureInfo("ja-JP"), new JapaneseCalendar());
        var eraMonth = new UiCalendarMonth(japanese, new(1989, 1, 15));
        check(eraMonth.Title.Contains("昭和", StringComparison.Ordinal) && eraMonth.Title.Contains("平成", StringComparison.Ordinal)
            && eraMonth.Title == new UiCalendarMonth(japanese, new(1989, 1, 1)).Title,
            "Japanese era-transition month names both eras and does not depend on its focused day");
        var julian = new UiCultureCalendar(CultureInfo.GetCultureInfo("en-US"));
        var edgeSystem = new JulianSystem(julian);
        var edge = new UiCalendarMonth(edgeSystem, DateOnly.MinValue);
        check(edge.MonthStartDayNumber == -2 && !edge.TryGetAdjacentMonth(-1, out _)
            && edge.Cells.Span.ToArray().Any(cell => cell.Date == DateOnly.MinValue && cell.Parts?.Day == 3),
            "partial Julian boundary month retains negative conceptual start without creating an invalid DateOnly");
        foreach (var system in new IUiCalendarSystem[] { us, uk, native, hebrew, japanese,
            new UiCultureCalendar(CultureInfo.GetCultureInfo("ko-KR")),
            new UiCultureCalendar(CultureInfo.GetCultureInfo("fa-IR")),
            new UiCultureCalendar(CultureInfo.GetCultureInfo("th-TH")) })
        {
            var first = new UiCalendarMonth(system, system.MinimumDate);
            var last = new UiCalendarMonth(system, system.MaximumDate);
            check(!first.TryGetAdjacentMonth(-1, out _) && !last.TryGetAdjacentMonth(1, out _)
                && first.Cells.ToArray().Where(cell => cell.IsSupported).All(cell => cell.Date >= system.MinimumDate)
                && last.Cells.ToArray().Where(cell => cell.IsSupported).All(cell => cell.Date <= system.MaximumDate),
                "supported-range endpoints remain bounded for " + ((UiCultureCalendar)system).Culture.Name);
        }
        var rejected = false;
        try { _ = new UiCultureCalendar(CultureInfo.GetCultureInfo("en-US"), new HebrewCalendar()); }
        catch (ArgumentException) { rejected = true; }
        check(rejected, "unsupported culture/calendar pairs fail explicitly instead of substituting Gregorian");
        var range = UiCalendarDateRange.FromEndpoints(new(2026, 11, 20), new(2026, 11, 10));
        check(range.Start == new DateOnly(2026, 11, 10) && range.End == new DateOnly(2026, 11, 20)
            && range.Contains(range.Start) && range.Contains(range.End)
            && range.Intersects(new(new(2026, 11, 20), new(2026, 11, 25))),
            "date ranges normalize reverse endpoints and use inclusive containment and intersection");
    }
    private sealed class JulianSystem(UiCultureCalendar text) : IUiCalendarSystem
    {
        private readonly JulianCalendar _calendar = new();
        public DateOnly MinimumDate => DateOnly.MinValue;
        public DateOnly MaximumDate => DateOnly.MaxValue;
        public DayOfWeek FirstDayOfWeek => DayOfWeek.Sunday;
        public bool RightToLeft => false;
        public UiCalendarDateParts GetParts(DateOnly date)
        {
            var value = date.ToDateTime(TimeOnly.MinValue);
            return new(1, _calendar.GetYear(value), _calendar.GetMonth(value), _calendar.GetDayOfMonth(value), false);
        }
        public int GetDaysInMonth(DateOnly date) { var parts = GetParts(date); return _calendar.GetDaysInMonth(parts.Year, parts.Month); }
        public string FormatDay(DateOnly date) => text.FormatNumber(GetParts(date).Day);
        public string FormatFullDate(DateOnly date) => GetParts(date).ToString();
        public string FormatMonthHeader(DateOnly first, DateOnly last) => GetParts(first).ToString();
        public string GetWeekdayName(DayOfWeek day, bool abbreviated) => text.GetWeekdayName(day, abbreviated);
    }
}
