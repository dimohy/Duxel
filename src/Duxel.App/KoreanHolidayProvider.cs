using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using Duxel.Core;

namespace Duxel.App;

/// <summary>Loads Korea's public holidays from the holidays-kr annual JSON files without credentials.</summary>
public sealed class KoreanHolidayProvider(HttpClient http, TimeProvider? clock = null) : IUiCalendarHolidayProvider
{
    private const string DataRoot = "https://raw.githubusercontent.com/hyunbinseo/holidays-kr/main/public/";
    private const int MaxResponseBytes = 64 * 1024;
    private readonly HttpClient _http = http ?? throw new ArgumentNullException(nameof(http));
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<int, CachedYear> _years = [];

    private sealed record CachedYear(UiCalendarHolidayResult[] Months, DateTimeOffset Expires);

    public async ValueTask<UiCalendarHolidayResult> GetHolidaysAsync(int year, int month,
        CancellationToken cancellationToken = default)
    {
        _ = new DateOnly(year, month, 1);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_years.TryGetValue(year, out var cached) && cached.Expires > _clock.GetUtcNow())
                return cached.Months[month - 1];

            var months = await FetchAsync(year, cancellationToken).ConfigureAwait(false);
            if (_years.Count >= 8 && !_years.ContainsKey(year))
                _years.Remove(_years.MinBy(item => item.Value.Expires).Key);
            var lifetime = months[0].Status == UiCalendarHolidayStatus.Available
                ? TimeSpan.FromHours(6)
                : TimeSpan.FromSeconds(30);
            _years[year] = new(months, _clock.GetUtcNow() + lifetime);
            return months[month - 1];
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<UiCalendarHolidayResult[]> FetchAsync(int year, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            var uri = new Uri(DataRoot + year.ToString("D4", CultureInfo.InvariantCulture) + ".json");
            using var response = await _http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return Failure(UiCalendarHolidayStatus.ServiceRejected, "KR_HOLIDAYS_YEAR_UNAVAILABLE");
            if ((int)response.StatusCode >= 500 || response.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests)
                return Failure(UiCalendarHolidayStatus.NetworkUnavailable, "KR_HOLIDAYS_HTTP_UNAVAILABLE");
            if (!response.IsSuccessStatusCode)
                return Failure(UiCalendarHolidayStatus.ServiceRejected, "KR_HOLIDAYS_HTTP_REJECTED");
            if (response.Content.Headers.ContentLength > MaxResponseBytes)
                return Failure(UiCalendarHolidayStatus.InvalidResponse, "KR_HOLIDAYS_RESPONSE_TOO_LARGE");

            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
            var bytes = new byte[MaxResponseBytes + 1];
            var count = await stream.ReadAtLeastAsync(bytes, bytes.Length, false, timeout.Token).ConfigureAwait(false);
            if (count > MaxResponseBytes)
                return Failure(UiCalendarHolidayStatus.InvalidResponse, "KR_HOLIDAYS_RESPONSE_TOO_LARGE");

            using var document = JsonDocument.Parse(bytes.AsMemory(0, count));
            return ParseYear(document.RootElement, year);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failure(UiCalendarHolidayStatus.NetworkUnavailable, "KR_HOLIDAYS_TIMEOUT");
        }
        catch (HttpRequestException)
        {
            return Failure(UiCalendarHolidayStatus.NetworkUnavailable, "KR_HOLIDAYS_NETWORK_UNAVAILABLE");
        }
        catch (IOException)
        {
            return Failure(UiCalendarHolidayStatus.NetworkUnavailable, "KR_HOLIDAYS_CONNECTION_INTERRUPTED");
        }
        catch (JsonException)
        {
            return Failure(UiCalendarHolidayStatus.InvalidResponse, "KR_HOLIDAYS_JSON_INVALID");
        }
    }

    private static UiCalendarHolidayResult[] ParseYear(JsonElement root, int year)
    {
        if (root.ValueKind != JsonValueKind.Object)
            return Failure(UiCalendarHolidayStatus.InvalidResponse, "KR_HOLIDAYS_JSON_INVALID");
        var entries = new List<UiCalendarHoliday>();
        foreach (var day in root.EnumerateObject())
        {
            if (!DateOnly.TryParseExact(day.Name, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                || date.Year != year || day.Value.ValueKind != JsonValueKind.Array || day.Value.GetArrayLength() == 0)
                return Failure(UiCalendarHolidayStatus.InvalidResponse, "KR_HOLIDAYS_ENTRY_INVALID");
            foreach (var label in day.Value.EnumerateArray())
            {
                var name = label.ValueKind == JsonValueKind.String ? label.GetString() : null;
                if (string.IsNullOrWhiteSpace(name) || name.Any(char.IsControl))
                    return Failure(UiCalendarHolidayStatus.InvalidResponse, "KR_HOLIDAYS_ENTRY_INVALID");
                entries.Add(new(date, name));
            }
        }
        if (entries.Count == 0)
            return Failure(UiCalendarHolidayStatus.InvalidResponse, "KR_HOLIDAYS_YEAR_EMPTY");

        var byMonth = entries.ToLookup(item => item.Date.Month);
        return Enumerable.Range(1, 12).Select(month =>
            new UiCalendarHolidayResult(UiCalendarHolidayStatus.Available, new(byMonth[month]))).ToArray();
    }

    private static UiCalendarHolidayResult[] Failure(UiCalendarHolidayStatus status, string code)
    {
        var months = new UiCalendarHolidayResult[12];
        Array.Fill(months, new UiCalendarHolidayResult(status, UiCalendarHolidaySet.Empty, code));
        return months;
    }
}
