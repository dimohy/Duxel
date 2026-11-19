namespace Duxel.Core;

public sealed record UiCalendarHoliday
{
    public DateOnly Date { get; }
    public string Name { get; }
    public UiCalendarHoliday(DateOnly date, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Any(char.IsControl)) throw new ArgumentException("Holiday names must be single-line text.", nameof(name));
        Date = date; Name = name;
    }
}
/// <summary>Immutable prepared annotations; rendering performs no I/O or holiday calculation.</summary>
public sealed class UiCalendarHolidaySet
{
    private readonly Dictionary<DateOnly, string> _names;
    public static UiCalendarHolidaySet Empty { get; } = new([]);
    public IReadOnlyList<UiCalendarHoliday> Items { get; }
    public UiCalendarHolidaySet(IEnumerable<UiCalendarHoliday> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var prepared = items.Distinct().OrderBy(item => item.Date).ThenBy(item => item.Name, StringComparer.Ordinal).ToArray();
        Items = Array.AsReadOnly(prepared);
        _names = prepared.GroupBy(item => item.Date).ToDictionary(group => group.Key, group => string.Join(" · ", group.Select(item => item.Name)));
    }
    public bool TryGetName(DateOnly date, out string name) => _names.TryGetValue(date, out name!);
}
public enum UiCalendarHolidayStatus { Available, NetworkUnavailable, ServiceRejected, InvalidResponse }
public sealed record UiCalendarHolidayResult(UiCalendarHolidayStatus Status, UiCalendarHolidaySet Holidays, string? DiagnosticCode = null)
{
    /// <summary>Failures intentionally yield no calendar annotation and no user-facing error.</summary>
    public UiCalendarHolidaySet? DisplayHolidays => Status == UiCalendarHolidayStatus.Available ? Holidays : null;
}
public interface IUiCalendarHolidayProvider
{
    ValueTask<UiCalendarHolidayResult> GetHolidaysAsync(int year, int month, CancellationToken cancellationToken = default);
}
