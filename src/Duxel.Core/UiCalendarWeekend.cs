using System.Globalization;

namespace Duxel.Core;

/// <summary>Inclusive recurring weekend; independent of the first weekday and public holidays.</summary>
public readonly record struct UiCalendarWeekend
{
    public DayOfWeek Start { get; }
    public DayOfWeek End { get; }
    public static UiCalendarWeekend SaturdaySunday { get; } = new(DayOfWeek.Saturday, DayOfWeek.Sunday);
    public UiCalendarWeekend(DayOfWeek start, DayOfWeek end)
    {
        if ((uint)start > 6 || (uint)end > 6) throw new ArgumentOutOfRangeException(nameof(start));
        Start = start; End = end;
    }
    public bool Contains(DayOfWeek day)
    {
        if ((uint)day > 6) throw new ArgumentOutOfRangeException(nameof(day));
        return ((int)day - (int)Start + 7) % 7 <= ((int)End - (int)Start + 7) % 7;
    }
    /// <summary>Unicode CLDR48 weekData; neutral/invariant locales use its documented world rule001.</summary>
    public static UiCalendarWeekend ForCulture(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        var territory = culture.Name.Length == 0 || culture.IsNeutralCulture ? "001" : new RegionInfo(culture.Name).TwoLetterISORegionName;
        return territory switch
        {
            "AF" => new(DayOfWeek.Thursday, DayOfWeek.Friday),
            "IR" => new(DayOfWeek.Friday, DayOfWeek.Friday),
            "BH" or "DZ" or "EG" or "IL" or "IQ" or "JO" or "KW" or "LY" or "OM" or "QA" or "SA" or "SD" or "SY" or "YE"
                => new(DayOfWeek.Friday, DayOfWeek.Saturday),
            "IN" or "UG" => new(DayOfWeek.Sunday, DayOfWeek.Sunday),
            _ => SaturdaySunday
        };
    }
}
public interface IUiCalendarWeekendSystem { UiCalendarWeekend Weekend { get; } }
public sealed record UiCalendarDayColors(bool Enabled = true, UiColor? WeekendStart = null, UiColor? WeekendEnd = null);
