using System.Globalization;
using System.Text;

namespace Duxel.Core;

/// <summary>Controls numeric day cells; month headings and full date names retain their culture format.</summary>
public enum UiCalendarDayNumbers { Culture, Latin }
public readonly record struct UiCalendarDateParts(int Era, int Year, int Month, int Day, bool IsLeapMonth);

/// <summary>Explicit arithmetic and text contract, independent of widget geometry and selection.</summary>
public interface IUiCalendarSystem
{
    DateOnly MinimumDate { get; }
    DateOnly MaximumDate { get; }
    DayOfWeek FirstDayOfWeek { get; }
    bool RightToLeft { get; }
    UiCalendarDateParts GetParts(DateOnly date);
    int GetDaysInMonth(DateOnly date);
    string FormatDay(DateOnly date);
    string FormatFullDate(DateOnly date);
    string FormatMonthHeader(DateOnly first, DateOnly last);
    string GetWeekdayName(DayOfWeek day, bool abbreviated);
}

public interface IUiCalendarYearNavigation
{
    bool TryMoveYear(DateOnly date, int direction, out DateOnly target);
}

/// <summary>Immutable snapshot of one supported .NET culture/calendar pair.</summary>
public sealed class UiCultureCalendar : IUiCalendarSystem, IUiCalendarYearNavigation, IUiCalendarWeekendSystem, IUiCalendarPeriodSystem
{
    private readonly string[] _digits, _shortDays, _fullDays;
    private readonly bool _shapeDigits, _multipleEras;
    private readonly string _monthPattern, _yearPattern;
    public bool MonthBeforeYear { get; }
    public CultureInfo Culture { get; }
    public Calendar Calendar { get; }
    public DateOnly MinimumDate { get; }
    public DateOnly MaximumDate { get; }
    public DayOfWeek FirstDayOfWeek => Culture.DateTimeFormat.FirstDayOfWeek;
    public bool RightToLeft => Culture.TextInfo.IsRightToLeft;
    public UiCalendarDayNumbers DayNumbers { get; }
    public UiCalendarWeekend Weekend { get; }
    public UiCultureCalendar(CultureInfo? culture = null, Calendar? calendar = null, UiCalendarDayNumbers dayNumbers = UiCalendarDayNumbers.Culture)
    {
        if ((uint)dayNumbers > 1) throw new ArgumentOutOfRangeException(nameof(dayNumbers));
        var snapshot = (CultureInfo)(culture ?? CultureInfo.CurrentCulture).Clone();
        if (calendar is not null)
            snapshot.DateTimeFormat.Calendar = System.Globalization.Calendar.ReadOnly((Calendar)calendar.Clone());
        Culture = CultureInfo.ReadOnly(snapshot);
        Calendar = Culture.DateTimeFormat.Calendar;
        MinimumDate = DateOnly.FromDateTime(Calendar.MinSupportedDateTime);
        MaximumDate = DateOnly.FromDateTime(Calendar.MaxSupportedDateTime);
        _digits = Culture.NumberFormat.NativeDigits;
        _shortDays = Culture.DateTimeFormat.ShortestDayNames;
        _fullDays = Culture.DateTimeFormat.DayNames;
        DayNumbers = dayNumbers;
        Weekend = UiCalendarWeekend.ForCulture(Culture);
        var pattern = Culture.DateTimeFormat.YearMonthPattern;
        static int Token(string value, char token)
        {
            var quote = '\0';
            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                if (c == '\\') { i++; continue; }
                if (quote != 0) { if (c == quote) quote = '\0'; continue; }
                if (c is '\'' or '"') { quote = c; continue; }
                if (c == token) return i;
            }
            throw new NotSupportedException("The calendar year/month pattern must contain both year and month fields.");
        }
        var monthAt = Token(pattern, 'M');
        var yearAt = Token(pattern, 'y');
        MonthBeforeYear = monthAt < yearAt;
        var split = Math.Max(monthAt, yearAt);
        _monthPattern = (MonthBeforeYear ? pattern[..split] : pattern[split..]).Trim();
        _yearPattern = (MonthBeforeYear ? pattern[split..] : pattern[..split]).Trim();
        _shapeDigits = Enumerable.Range(0, 10).Any(index => _digits[index] != index.ToString(CultureInfo.InvariantCulture));
        _multipleEras = Calendar.Eras.Length > 1;
    }
    private DateTime Date(DateOnly date)
    {
        if (date < MinimumDate || date > MaximumDate) throw new ArgumentOutOfRangeException(nameof(date));
        return date.ToDateTime(TimeOnly.MinValue);
    }
    public UiCalendarDateParts GetParts(DateOnly date)
    {
        var value = Date(date);
        var era = Calendar.GetEra(value);
        var year = Calendar.GetYear(value);
        var month = Calendar.GetMonth(value);
        return new(era, year, month, Calendar.GetDayOfMonth(value), Calendar.IsLeapMonth(year, month, era));
    }
    public int GetDaysInMonth(DateOnly date)
    {
        var parts = GetParts(date);
        return Calendar.GetDaysInMonth(parts.Year, parts.Month, parts.Era);
    }
    public string FormatDay(DateOnly date)
    {
        var value = Date(date);
        return DayNumbers == UiCalendarDayNumbers.Latin
            ? Calendar.GetDayOfMonth(value).ToString(CultureInfo.InvariantCulture)
            : ShapeDigits(value.ToString("%d", Culture));
    }
    public string FormatNumber(int number) => ShapeDigits(number.ToString(CultureInfo.InvariantCulture));
    private string ShapeDigits(string value)
    {
        if (!_shapeDigits) return value;
        var result = new StringBuilder(value.Length);
        foreach (var character in value)
            if (character is >= '0' and <= '9') result.Append(_digits[character - '0']);
            else result.Append(character);
        return result.ToString();
    }
    private string WithEra(DateTime date, string value)
    {
        if (!_multipleEras) return value;
        var era = Culture.DateTimeFormat.GetEraName(Calendar.GetEra(date));
        return value.Contains(era, StringComparison.Ordinal) ? value : era + " " + value;
    }
    public string FormatFullDate(DateOnly date)
    {
        var value = Date(date);
        return ShapeDigits(WithEra(value, value.ToString("D", Culture)));
    }
    public string FormatMonthHeader(DateOnly first, DateOnly last)
    {
        var begin = Date(first);
        var end = Date(last);
        var label = WithEra(begin, begin.ToString("Y", Culture));
        if (Calendar.GetEra(begin) != Calendar.GetEra(end))
            label += " – " + WithEra(end, end.ToString("Y", Culture));
        return ShapeDigits(label);
    }
    public string GetWeekdayName(DayOfWeek day, bool abbreviated)
    {
        if ((uint)day > 6) throw new ArgumentOutOfRangeException(nameof(day));
        return (abbreviated ? _shortDays : _fullDays)[(int)day];
    }
    public string FormatMonth(DateOnly date) => ShapeDigits(Date(date).ToString(_monthPattern.Length == 1 ? "%" + _monthPattern : _monthPattern, Culture));
    public string FormatYear(DateOnly date)
    {
        var value = Date(date);
        return ShapeDigits(WithEra(value, value.ToString(_yearPattern.Length == 1 ? "%" + _yearPattern : _yearPattern, Culture)));
    }
    public bool TryMovePeriods(DateOnly date, int offset, bool years, out DateOnly target)
    {
        var value = Date(date);
        try
        {
            target = DateOnly.FromDateTime(years ? Calendar.AddYears(value, offset) : Calendar.AddMonths(value, offset));
            return target >= MinimumDate && target <= MaximumDate;
        }
        catch (ArgumentOutOfRangeException) { target = default; return false; }
    }
    public bool TryMoveYear(DateOnly date, int direction, out DateOnly target)
    {
        if (direction is not (-1 or 1)) throw new ArgumentOutOfRangeException(nameof(direction));
        var value = Date(date);
        try { target = DateOnly.FromDateTime(Calendar.AddYears(value, direction)); return true; }
        catch (ArgumentOutOfRangeException) { target = default; return false; }
    }
}
